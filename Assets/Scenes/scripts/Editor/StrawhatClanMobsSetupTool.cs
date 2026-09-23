#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Setup tool to automatically configure animations and prefabs for:
/// 1. Strawhat Katana Swordsman (newidlestraw, newstancestraw, newwalkstraw, newattackstraw)
/// 2. Strawhat Shadow Brute (newfat, newfatwalk, newfatattack)
/// 
/// Menu Item: Tools > Setup All Strawhat Mobs
/// </summary>
public static class StrawhatClanMobsSetupTool
{
    private const string ANIM_DIR = "Assets/Scenes/animations/animators";
    private const string PREFAB_DIR = "Assets/Prefabs/Enemies/Dojo1";
    private const string RESOURCES_PREFAB_DIR = "Assets/Resources/Prefabs/Enemies/Dojo1";

    // Sword Folders
    private const string SWORD_IDLE_DIR = "Assets/Scenes/animations/frames/newidlestraw-838517e4";
    private const string SWORD_STANCE_DIR = "Assets/Scenes/animations/frames/newstancestraw-5fb4cdc3";
    private const string SWORD_WALK_DIR = "Assets/Scenes/animations/frames/newwalkstraw-9edabc38";
    private const string SWORD_ATTACK_DIR = "Assets/Scenes/animations/frames/newattackstraw-ba1be727";

    // Brute Folders
    private const string BRUTE_IDLE_DIR = "Assets/Scenes/animations/frames/newfat-e5a73ac3";
    private const string BRUTE_WALK_DIR = "Assets/Scenes/animations/frames/newfatwalk-5b766109";
    private const string BRUTE_ATTACK_DIR = "Assets/Scenes/animations/frames/newfatattack-68451e5a";

    [InitializeOnLoadMethod]
    private static void AutoRunOnCompile()
    {
        EditorApplication.delayCall += () =>
        {
            SetupAllMobs();
        };
    }

    [MenuItem("Tools/Setup All Strawhat Mobs")]
    public static void SetupAllMobs()
    {
        Debug.Log("<color=#C840FF>[StrawhatClanMobsSetupTool] Starting setup for Strawhat Sword and Brute Mobs...</color>");

        EnsureFolderExists("Assets/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Resources/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Scenes/animations", "animators");

        // 1. Configure Texture Importers
        ConfigureFolderTextures(SWORD_IDLE_DIR);
        ConfigureFolderTextures(SWORD_STANCE_DIR);
        ConfigureFolderTextures(SWORD_WALK_DIR);
        ConfigureFolderTextures(SWORD_ATTACK_DIR);

        ConfigureFolderTextures(BRUTE_IDLE_DIR);
        ConfigureFolderTextures(BRUTE_WALK_DIR);
        ConfigureFolderTextures(BRUTE_ATTACK_DIR);

        AssetDatabase.Refresh();

        // 2. Setup Strawhat Sword Mob
        SetupSwordMob();

        // 3. Setup Strawhat Brute Mob
        SetupBruteMob();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[StrawhatClanMobsSetupTool] All Strawhat Mobs Successfully Configured and Baked!</color>");
    }

    private static void SetupSwordMob()
    {
        // Animation Clips
        AnimationClip idleClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatSwordIdle.anim", SWORD_IDLE_DIR, 12, true);
        AnimationClip stanceClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatSwordStance.anim", SWORD_STANCE_DIR, 14, true);
        AnimationClip walkClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatSwordWalk.anim", SWORD_WALK_DIR, 14, true);
        AnimationClip attackClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatSwordAttack.anim", SWORD_ATTACK_DIR, 18, false);

        if (idleClip == null || stanceClip == null || walkClip == null || attackClip == null)
        {
            Debug.LogError("[StrawhatClanMobsSetupTool] Failed to build Sword mob clips!");
            return;
        }

        // Animator Controller
        string controllerPath = $"{ANIM_DIR}/StrawhatSwordController.controller";
        AnimatorController controller = CreateSwordAnimatorController(controllerPath, idleClip, stanceClip, walkClip, attackClip);

        // Initial Sprite
        List<Sprite> idles = LoadSprites(SWORD_IDLE_DIR);
        Sprite initialSprite = idles.Count > 0 ? idles[0] : null;

        // Prefabs
        BuildAndSaveSwordPrefab($"{PREFAB_DIR}/StrawhatSwordMob.prefab", controller, initialSprite);
        BuildAndSaveSwordPrefab($"{RESOURCES_PREFAB_DIR}/StrawhatSwordMob.prefab", controller, initialSprite);
    }

    private static void SetupBruteMob()
    {
        // Animation Clips
        AnimationClip idleClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatBruteIdle.anim", BRUTE_IDLE_DIR, 12, true);
        AnimationClip walkClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatBruteWalk.anim", BRUTE_WALK_DIR, 12, true);
        AnimationClip attackClip = BuildSpriteAnimationClip($"{ANIM_DIR}/StrawhatBruteAttack.anim", BRUTE_ATTACK_DIR, 14, false);

        if (idleClip == null || walkClip == null || attackClip == null)
        {
            Debug.LogError("[StrawhatClanMobsSetupTool] Failed to build Brute mob clips!");
            return;
        }

        // Animator Controller
        string controllerPath = $"{ANIM_DIR}/StrawhatBruteController.controller";
        AnimatorController controller = CreateBruteAnimatorController(controllerPath, idleClip, walkClip, attackClip);

        // Initial Sprite
        List<Sprite> idles = LoadSprites(BRUTE_IDLE_DIR);
        Sprite initialSprite = idles.Count > 0 ? idles[0] : null;

        // Prefabs
        BuildAndSaveBrutePrefab($"{PREFAB_DIR}/StrawhatBruteMob.prefab", controller, initialSprite);
        BuildAndSaveBrutePrefab($"{RESOURCES_PREFAB_DIR}/StrawhatBruteMob.prefab", controller, initialSprite);
    }

    private static AnimatorController CreateSwordAnimatorController(string path, AnimationClip idle, AnimationClip stance, AnimationClip walk, AnimationClip attack)
    {
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isStance", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Parry", AnimatorControllerParameterType.Trigger);

        var rootSm = controller.layers[0].stateMachine;

        var idleState = rootSm.AddState("Idle");
        idleState.motion = idle;
        rootSm.defaultState = idleState;

        var stanceState = rootSm.AddState("Stance");
        stanceState.motion = stance;

        var walkState = rootSm.AddState("Walk");
        walkState.motion = walk;

        var attackState = rootSm.AddState("Attack");
        attackState.motion = attack;

        // Idle <-> Walk
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.05f;

        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.05f;

        // Idle <-> Stance
        var idleToStance = idleState.AddTransition(stanceState);
        idleToStance.AddCondition(AnimatorConditionMode.If, 0, "isStance");
        idleToStance.hasExitTime = false;
        idleToStance.duration = 0.05f;

        var stanceToIdle = stanceState.AddTransition(idleState);
        stanceToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isStance");
        stanceToIdle.hasExitTime = false;
        stanceToIdle.duration = 0.05f;

        // Walk <-> Stance
        var walkToStance = walkState.AddTransition(stanceState);
        walkToStance.AddCondition(AnimatorConditionMode.If, 0, "isStance");
        walkToStance.hasExitTime = false;
        walkToStance.duration = 0.05f;

        var stanceToWalk = stanceState.AddTransition(walkState);
        stanceToWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        stanceToWalk.hasExitTime = false;
        stanceToWalk.duration = 0.05f;

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

    private static AnimatorController CreateBruteAnimatorController(string path, AnimationClip idle, AnimationClip walk, AnimationClip attack)
    {
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var rootSm = controller.layers[0].stateMachine;

        var idleState = rootSm.AddState("Idle");
        idleState.motion = idle;
        rootSm.defaultState = idleState;

        var walkState = rootSm.AddState("Walk");
        walkState.motion = walk;

        var attackState = rootSm.AddState("Attack");
        attackState.motion = attack;

        // Idle <-> Walk
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.06f;

        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.06f;

        // AnyState -> Attack
        var anyToAttack = rootSm.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.02f;

        // Attack -> Idle
        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.92f;
        attackToIdle.duration = 0.06f;

        return controller;
    }

    private static void BuildAndSaveSwordPrefab(string savePath, AnimatorController controller, Sprite initialSprite)
    {
        GameObject go = new GameObject("StrawhatSwordMob");
        go.tag = "enemy";
        // Scaled bigger than player (player is ~4.5u tall, mob will be ~5.4u tall)
        go.transform.localScale = new Vector3(0.58f, 0.58f, 1.0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (initialSprite != null) sr.sprite = initialSprite;
        sr.sortingOrder = 5;

        Animator anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3.4f, 8.8f);
        col.offset = new Vector2(-0.6f, -0.2f);

        StrawhatSwordAI ai = go.AddComponent<StrawhatSwordAI>();
        ai.maxHealth = 110;
        ai.slashDamage = 20;
        ai.playerKnockbackForce = 6.8f;
        ai.enableParry = true;
        ai.parryCountPerFive = 3;

        ai.idleSprites = LoadSprites(SWORD_IDLE_DIR).ToArray();
        ai.stanceSprites = LoadSprites(SWORD_STANCE_DIR).ToArray();
        ai.walkSprites = LoadSprites(SWORD_WALK_DIR).ToArray();
        ai.attackSprites = LoadSprites(SWORD_ATTACK_DIR).ToArray();

        Health hp = go.AddComponent<Health>();
        hp.maxHealth = 110;

        PrefabUtility.SaveAsPrefabAsset(go, savePath);
        Object.DestroyImmediate(go);
    }

    private static void BuildAndSaveBrutePrefab(string savePath, AnimatorController controller, Sprite initialSprite)
    {
        GameObject go = new GameObject("StrawhatBruteMob");
        go.tag = "enemy";
        // Brute is substantially larger and imposing (~6.7u tall)
        go.transform.localScale = new Vector3(0.68f, 0.68f, 1.0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (initialSprite != null) sr.sprite = initialSprite;
        sr.sortingOrder = 5;

        Animator anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 3.0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(5.6f, 9.4f);
        col.offset = new Vector2(-0.5f, 0.1f);

        StrawhatBruteAI ai = go.AddComponent<StrawhatBruteAI>();
        ai.maxHealth = 180;
        ai.slamDamage = 30;
        ai.playerKnockbackForce = 9.0f;

        ai.idleSprites = LoadSprites(BRUTE_IDLE_DIR).ToArray();
        ai.walkSprites = LoadSprites(BRUTE_WALK_DIR).ToArray();
        ai.attackSprites = LoadSprites(BRUTE_ATTACK_DIR).ToArray();

        Health hp = go.AddComponent<Health>();
        hp.maxHealth = 180;

        PrefabUtility.SaveAsPrefabAsset(go, savePath);
        Object.DestroyImmediate(go);
    }

    private static void EnsureFolderExists(string parent, string sub)
    {
        string full = $"{parent}/{sub}";
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, sub);
        }
    }

    private static void ConfigureFolderTextures(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            int idx = assetPath.IndexOf("Assets/");
            if (idx >= 0) assetPath = assetPath.Substring(idx);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f)
                {
                    importer.spritePixelsPerUnit = 100f;
                    dirty = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }
    }

    private static List<Sprite> LoadSprites(string folderPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (!Directory.Exists(folderPath)) return sprites;

        string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
        System.Array.Sort(files);

        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            int idx = assetPath.IndexOf("Assets/");
            if (idx >= 0) assetPath = assetPath.Substring(idx);

            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (s == null)
            {
                Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                if (allAssets != null)
                {
                    foreach (var obj in allAssets)
                    {
                        if (obj is Sprite spr)
                        {
                            s = spr;
                            break;
                        }
                    }
                }
            }
            if (s != null)
            {
                sprites.Add(s);
            }
        }

        return sprites;
    }

    private static AnimationClip BuildSpriteAnimationClip(string clipPath, string folderPath, int sampleRate, bool loop)
    {
        List<Sprite> sprites = LoadSprites(folderPath);
        if (sprites.Count == 0)
        {
            Debug.LogError($"[StrawhatClanMobsSetupTool] No sprites found in {folderPath}!");
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
}
#endif
