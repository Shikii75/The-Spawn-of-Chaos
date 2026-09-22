#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor setup tool to configure the Staff Strawhat Mob:
/// 1. Generates animation clips directly via Unity Editor API using ONLY:
///    - Assets/Scenes/animations/frames/newfemalestrawstaffrun-80c96966
///    - Assets/Scenes/animations/frames/newfemalestrawstaffattack-1f468d59
/// 2. Creates the AnimatorController with fluid state transitions.
/// 3. Builds and saves the complete mob Prefabs at:
///    - Assets/Prefabs/Enemies/StrawhatStaffMob.prefab
///    - Assets/Resources/Prefabs/Enemies/StrawhatStaffMob.prefab
/// 
/// Menu Item: Tools > Setup Staff Strawhat Mob
/// </summary>
public static class StrawhatStaffSetupTool
{
    private const string ANIM_DIR = "Assets/Scenes/animations/animators";
    private const string PREFAB_DIR = "Assets/Prefabs/Enemies";
    private const string RESOURCES_PREFAB_DIR = "Assets/Resources/Prefabs/Enemies";
    private const string RUN_FRAMES_DIR = "Assets/Scenes/animations/frames/newfemalestrawstaffrun-80c96966";
    private const string ATTACK_FRAMES_DIR = "Assets/Scenes/animations/frames/newfemalestrawstaffattack-1f468d59";

    [InitializeOnLoadMethod]
    private static void AutoRunOnCompile()
    {
        EditorApplication.delayCall += () =>
        {
            SetupMob();
        };
    }

    [MenuItem("Tools/Setup Staff Strawhat Mob")]
    public static void SetupMob()
    {
        Debug.Log("<color=#C840FF>[StrawhatStaffSetupTool] Starting automated setup for Staff Strawhat Mob...</color>");

        EnsureFolderExists("Assets/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Resources/Prefabs", "Enemies");

        // 1. Generate Run Clip (14 fps, looping)
        AnimationClip runClip = BuildSpriteAnimationClip(
            $"{ANIM_DIR}/StrawhatStaffRun.anim",
            RUN_FRAMES_DIR,
            14,
            true
        );

        // 2. Generate Attack Clip (18 fps, non-looping)
        AnimationClip attackClip = BuildSpriteAnimationClip(
            $"{ANIM_DIR}/StrawhatStaffAttack.anim",
            ATTACK_FRAMES_DIR,
            18,
            false
        );

        // 3. Generate Idle Clip (uses staff guard frame_001 from attack folder, looping)
        AnimationClip idleClip = BuildSingleSpriteIdleClip(
            $"{ANIM_DIR}/StrawhatStaffIdle.anim",
            $"{ATTACK_FRAMES_DIR}/frame_001.png"
        );

        if (idleClip == null || runClip == null || attackClip == null)
        {
            Debug.LogError("[StrawhatStaffSetupTool] Failed to build animation clips!");
            return;
        }

        // 4. Create Animator Controller
        string controllerPath = $"{ANIM_DIR}/StrawhatStaffController.controller";
        AnimatorController controller = CreateAnimatorController(controllerPath, idleClip, runClip, attackClip);

        // 5. Load Initial Sprite (Staff guard stance from newfemalestrawstaffattack-1f468d59)
        Sprite initialSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ATTACK_FRAMES_DIR}/frame_001.png");

        // 6. Assemble and save prefabs
        string prefabPath1 = $"{PREFAB_DIR}/StrawhatStaffMob.prefab";
        string prefabPath2 = $"{RESOURCES_PREFAB_DIR}/StrawhatStaffMob.prefab";

        BuildAndSavePrefab(prefabPath1, controller, initialSprite);
        BuildAndSavePrefab(prefabPath2, controller, initialSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[StrawhatStaffSetupTool] Setup Complete! Staff Strawhat Mob successfully created and bound.</color>");
    }

    private static void EnsureFolderExists(string parent, string sub)
    {
        string full = $"{parent}/{sub}";
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, sub);
        }
    }

    private static AnimationClip BuildSpriteAnimationClip(string clipPath, string folderPath, int sampleRate, bool loop)
    {
        string[] files = Directory.GetFiles(folderPath, "*.png");
        System.Array.Sort(files);

        List<Sprite> sprites = new List<Sprite>();
        foreach (var file in files)
        {
            string unityPath = file.Replace('\\', '/');
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(unityPath);
            if (s != null)
            {
                sprites.Add(s);
            }
        }

        if (sprites.Count == 0)
        {
            Debug.LogError($"[StrawhatStaffSetupTool] No sprites found in {folderPath}!");
            return null;
        }

        AnimationClip clip = new AnimationClip();
        clip.frameRate = sampleRate;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = (float)i / sampleRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (File.Exists(clipPath))
        {
            AssetDatabase.DeleteAsset(clipPath);
        }

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static AnimationClip BuildSingleSpriteIdleClip(string clipPath, string spritePath)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (s == null)
        {
            Debug.LogError($"[StrawhatStaffSetupTool] Sprite not found at {spritePath}!");
            return null;
        }

        AnimationClip clip = new AnimationClip();
        clip.frameRate = 2;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[2];
        keyframes[0] = new ObjectReferenceKeyframe { time = 0f, value = s };
        keyframes[1] = new ObjectReferenceKeyframe { time = 0.5f, value = s };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (File.Exists(clipPath))
        {
            AssetDatabase.DeleteAsset(clipPath);
        }

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static AnimatorController CreateAnimatorController(string path, AnimationClip idleClip, AnimationClip runClip, AnimationClip attackClip)
    {
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Parry", AnimatorControllerParameterType.Trigger);

        var rootSm = controller.layers[0].stateMachine;

        // States
        var idleState = rootSm.AddState("Idle");
        idleState.motion = idleClip;
        rootSm.defaultState = idleState;

        var runState = rootSm.AddState("Run");
        runState.motion = runClip;

        var attackState = rootSm.AddState("Attack");
        attackState.motion = attackClip;

        // Transitions:
        // Idle -> Run (via isRunning or isWalking)
        var idleToRun1 = idleState.AddTransition(runState);
        idleToRun1.AddCondition(AnimatorConditionMode.If, 0, "isRunning");
        idleToRun1.hasExitTime = false;
        idleToRun1.duration = 0.05f;

        var idleToRun2 = idleState.AddTransition(runState);
        idleToRun2.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        idleToRun2.hasExitTime = false;
        idleToRun2.duration = 0.05f;

        // Run -> Idle
        var runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.05f;

        // AnyState -> Attack
        var anyToAttack = rootSm.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.02f;

        // Attack -> Idle
        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.92f;
        attackToIdle.duration = 0.05f;

        return controller;
    }

    private static void BuildAndSavePrefab(string savePath, AnimatorController controller, Sprite initialSprite)
    {
        GameObject go = new GameObject("StrawhatStaffMob");
        go.tag = "enemy";

        go.transform.localScale = new Vector3(0.38f, 0.38f, 1.0f);

        // SpriteRenderer
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (initialSprite != null) sr.sprite = initialSprite;
        sr.sortingOrder = 5;

        // Animator
        Animator anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        // Rigidbody2D
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Main Collider
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(2.2f, 3.6f);
        col.offset = new Vector2(0f, 0f);

        // AI Script
        StrawhatStaffAI ai = go.AddComponent<StrawhatStaffAI>();
        ai.maxHealth = 120;
        ai.directSlamDamage = 24;
        ai.shockwaveDamage = 16;
        ai.playerKnockbackForce = 7.5f;
        ai.enableParry = true;
        ai.parryCountPerFive = 3;

        // Health Component
        Health hp = go.AddComponent<Health>();
        hp.maxHealth = 120;

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(go, savePath);
        Object.DestroyImmediate(go);
    }
}
#endif
