#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// BasePlayerBuilder - Editor tool to automatically build the standalone BasePlayer prefab for tutorial / alternate skin use.
/// Features:
/// 1. Generates animation clips for Idle, Walk, Run, Jump, Melee Attack, Shadow Attack, and Blob Transformation from source PNG frames.
/// 2. Generates BasePlayerController AnimatorController with all states and parameters.
/// 3. Assembles the BasePlayer prefab with Physics, Components, Health, Move (with enableOrbCompanion = false, useTeleportJump = false, jumpForce = 11), and Combat.
/// 4. Saves BasePlayer.prefab to Assets/Prefabs/BasePlayer.prefab.
/// </summary>
public class BasePlayerBuilder
{
    [MenuItem("Tools/Build BasePlayer Prefab")]
    public static void BuildBasePlayerPrefab()
    {
        Debug.Log("Starting BasePlayer Prefab & Animator Controller Generation...");

        // Ensure directories exist
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/animations/animators"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/animations"))
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "animations");
            }
            AssetDatabase.CreateFolder("Assets/Scenes/animations", "animators");
        }

        string baseFramesPath = "Assets/Scenes/animations/frames/basePlayer";
        string blobFramesPath = "Assets/Scenes/animations/frames/SHADOW BLOB animations";
        string animSaveDir = "Assets/Scenes/animations/animators";

        // 1. Create or load animation clips
        AnimationClip idleClip = CreateClipFromFolder(FindSubfolder(baseFramesPath, "baseplayeridle"), animSaveDir + "/basePlayerIdle.anim", 12f, true);
        AnimationClip walkClip = CreateClipFromFolder(FindSubfolder(baseFramesPath, "baseplayerwalk"), animSaveDir + "/basePlayerWalk.anim", 12f, true);
        AnimationClip runClip = CreateClipFromFolder(FindSubfolder(baseFramesPath, "baseplayerrun"), animSaveDir + "/basePlayerRun.anim", 14f, true);
        AnimationClip jumpClip = CreateClipFromFolder(FindSubfolder(baseFramesPath, "baseplayerjump"), animSaveDir + "/basePlayerJump.anim", 12f, false);
        AnimationClip attackClip = CreateClipFromFolder(FindSubfolder(baseFramesPath, "baseplayerpunches"), animSaveDir + "/basePlayerAttack.anim", 16f, false);
        AnimationClip shadowAttackClip = CreateClipFromFolder(FindSubfolder(baseFramesPath, "baseplayershadowattack"), animSaveDir + "/basePlayerShadowAttack.anim", 14f, false);
        AnimationClip blobClip = CreateClipFromFolder(blobFramesPath, animSaveDir + "/basePlayerBlob.anim", 12f, true);

        // 2. Create AnimatorController
        string controllerPath = animSaveDir + "/BasePlayerController.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isBlob", AnimatorControllerParameterType.Bool);
        controller.AddParameter("jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);

        var rootStateMachine = controller.layers[0].stateMachine;

        // States
        var idleState = rootStateMachine.AddState("Idle"); idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        var runState = rootStateMachine.AddState("Run"); runState.motion = runClip;
        var walkState = rootStateMachine.AddState("Walk"); walkState.motion = walkClip;
        var jumpState = rootStateMachine.AddState("Jump"); jumpState.motion = jumpClip;
        var blobState = rootStateMachine.AddState("Blob"); blobState.motion = blobClip;
        var attackState = rootStateMachine.AddState("Attack"); attackState.motion = attackClip;
        var shadowState = rootStateMachine.AddState("ShadowAttack"); shadowState.motion = shadowAttackClip;

        // Transitions: Idle <-> Run
        var idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "isRunning");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.05f;

        var runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.05f;

        // AnyState -> Jump
        var anyToJumpBool = rootStateMachine.AddAnyStateTransition(jumpState);
        anyToJumpBool.AddCondition(AnimatorConditionMode.If, 0, "isJumping");
        anyToJumpBool.hasExitTime = false;
        anyToJumpBool.duration = 0.02f;

        var anyToJumpTrig = rootStateMachine.AddAnyStateTransition(jumpState);
        anyToJumpTrig.AddCondition(AnimatorConditionMode.If, 0, "jump");
        anyToJumpTrig.hasExitTime = false;
        anyToJumpTrig.duration = 0.02f;

        var jumpToIdle = jumpState.AddTransition(idleState);
        jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isJumping");
        jumpToIdle.hasExitTime = false;
        jumpToIdle.duration = 0.05f;

        var jumpToRun = jumpState.AddTransition(runState);
        jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "isRunning");
        jumpToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "isJumping");
        jumpToRun.hasExitTime = false;
        jumpToRun.duration = 0.05f;

        // AnyState -> Blob
        var anyToBlob = rootStateMachine.AddAnyStateTransition(blobState);
        anyToBlob.AddCondition(AnimatorConditionMode.If, 0, "isBlob");
        anyToBlob.hasExitTime = false;
        anyToBlob.duration = 0.05f;

        var blobToIdle = blobState.AddTransition(idleState);
        blobToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isBlob");
        blobToIdle.hasExitTime = false;
        blobToIdle.duration = 0.05f;

        // AnyState -> Attack
        var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.02f;

        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;

        // AnyState -> ShadowAttack (Cast)
        var anyToShadow = rootStateMachine.AddAnyStateTransition(shadowState);
        anyToShadow.AddCondition(AnimatorConditionMode.If, 0, "Cast");
        anyToShadow.hasExitTime = false;
        anyToShadow.duration = 0.02f;

        var shadowToIdle = shadowState.AddTransition(idleState);
        shadowToIdle.hasExitTime = true;
        shadowToIdle.exitTime = 0.9f;
        shadowToIdle.duration = 0.1f;

        // 3. Assemble BasePlayer GameObject & Prefab
        GameObject basePlayerGO = new GameObject("BasePlayer");
        basePlayerGO.tag = "Player";

        PhysicsMaterial2D noFrictionMat = new PhysicsMaterial2D("BasePlayerNoFriction") { friction = 0f, bounciness = 0f };

        SpriteRenderer sr = basePlayerGO.AddComponent<SpriteRenderer>();
        Sprite defaultSprite = GetFirstSpriteFromFolder(FindSubfolder(baseFramesPath, "baseplayeridle"));
        if (defaultSprite != null) sr.sprite = defaultSprite;
        sr.sortingOrder = 1;

        Rigidbody2D rb = basePlayerGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = 1f;
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sharedMaterial = noFrictionMat;

        BoxCollider2D col = basePlayerGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.4f, 3.4f);
        col.offset = new Vector2(0f, -0.05f);
        col.sharedMaterial = noFrictionMat;

        Animator anim = basePlayerGO.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        move moveScript = basePlayerGO.AddComponent<move>();
        moveScript.moveSpeed = 7f;
        moveScript.jumpForce = 11f;
        moveScript.gravityScale = 1f;
        moveScript.useTeleportJump = false;
        moveScript.enableOrbCompanion = false;

        Health health = basePlayerGO.AddComponent<Health>();
        health.maxHealth = 1000;

        basePlayerGO.AddComponent<PlayerCurrency>();

        MageCombat combat = basePlayerGO.AddComponent<MageCombat>();
        combat.meleeDamage = 20;
        combat.secondHitDamage = 28;
        combat.meleeAttackDuration = 0.25f;

        GameObject hitboxGO = new GameObject("hitbox");
        hitboxGO.transform.SetParent(basePlayerGO.transform, false);
        hitboxGO.transform.localPosition = new Vector3(0.02f, -0.458f, 0f);
        hitboxGO.transform.localScale = new Vector3(2.9767f, 2.2285f, 2.2285f);

        BoxCollider2D hitBoxCol = hitboxGO.AddComponent<BoxCollider2D>();
        hitBoxCol.isTrigger = true;
        hitBoxCol.size = new Vector2(1f, 1f);

        Attack attackScript = hitboxGO.AddComponent<Attack>();
        attackScript.damage = 20;
        attackScript.attackCollider = hitBoxCol;
        attackScript.animator = anim;

        combat.meleeAttackCollider = hitBoxCol;

        // Save Prefab
        string prefabPath = "Assets/Prefabs/BasePlayer.prefab";
        PrefabUtility.SaveAsPrefabAsset(basePlayerGO, prefabPath);
        Object.DestroyImmediate(basePlayerGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("BasePlayer Prefab successfully created at: " + prefabPath);
    }

    private static string FindSubfolder(string parentPath, string prefix)
    {
        if (!Directory.Exists(parentPath)) return parentPath;

        string[] dirs = Directory.GetDirectories(parentPath);
        foreach (var dir in dirs)
        {
            string dirName = Path.GetFileName(dir).ToLower();
            if (dirName.StartsWith(prefix.ToLower()))
            {
                return dir.Replace("\\", "/");
            }
        }
        return parentPath;
    }

    private static AnimationClip CreateClipFromFolder(string folderPath, string savePath, float frameRate, bool loop)
    {
        AnimationClip clip = new AnimationClip();
        clip.frameRate = frameRate;

        List<Sprite> sprites = LoadSpritesFromFolder(folderPath);

        if (sprites.Count > 0)
        {
            EditorCurveBinding binding = new EditorCurveBinding();
            binding.type = typeof(SpriteRenderer);
            binding.path = "";
            binding.propertyName = "m_Sprite";

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe();
                keyframes[i].time = i / frameRate;
                keyframes[i].value = sprites[i];
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, savePath);
        return clip;
    }

    private static List<Sprite> LoadSpritesFromFolder(string folderPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (!Directory.Exists(folderPath)) return sprites;

        string[] filePaths = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
        System.Array.Sort(filePaths);

        foreach (string file in filePaths)
        {
            string assetPath = file.Replace("\\", "/");
            int assetsIdx = assetPath.IndexOf("Assets/");
            if (assetsIdx >= 0) assetPath = assetPath.Substring(assetsIdx);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites;
    }

    private static Sprite GetFirstSpriteFromFolder(string folderPath)
    {
        List<Sprite> sprites = LoadSpritesFromFolder(folderPath);
        return sprites.Count > 0 ? sprites[0] : null;
    }
}
#endif
