#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor setup tool to configure the Staff Strawhat Mob:
/// 1. Configures TextureImporters on all frame PNGs to Sprite (Single, 100 PPU, Bilinear).
/// 2. Generates the 3 AnimationClips (Idle, Run, Attack) using ONLY:
///    - Assets/Scenes/animations/frames/newfemalestrawstaffrun-80c96966
///    - Assets/Scenes/animations/frames/newfemalestrawstaffattack-1f468d59
/// 3. Creates the AnimatorController with fluid state transitions.
/// 4. Builds and saves the complete mob Prefabs at:
///    - Assets/Prefabs/Enemies/StrawhatStaffMob.prefab
///    - Assets/Resources/Prefabs/Enemies/StrawhatStaffMob.prefab
/// 
/// Menu Item: Tools > Configure Staff Mob Animation Frames
/// </summary>
public static class StrawhatStaffSetupTool
{
    private const string ANIM_DIR = "Assets/Scenes/animations/animators";
    private const string PREFAB_DIR = "Assets/Prefabs/Enemies/Dojo1";
    private const string RESOURCES_PREFAB_DIR = "Assets/Resources/Prefabs/Enemies/Dojo1";
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

    [MenuItem("Tools/Configure Staff Mob Animation Frames")]
    [MenuItem("Tools/Setup Staff Strawhat Mob")]
    public static void SetupMob()
    {
        Debug.Log("<color=#C840FF>[StrawhatStaffSetupTool] Configuring Staff Strawhat Mob animations and prefabs...</color>");

        EnsureFolderExists("Assets/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Resources/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Scenes/animations", "animators");

        // Step 1: Configure Texture Importers for both frame folders
        ConfigureFolderTextures(RUN_FRAMES_DIR);
        ConfigureFolderTextures(ATTACK_FRAMES_DIR);

        AssetDatabase.Refresh();

        // Step 2: Generate Run Clip (14 fps, looping)
        AnimationClip runClip = BuildSpriteAnimationClip(
            $"{ANIM_DIR}/StrawhatStaffRun.anim",
            RUN_FRAMES_DIR,
            14,
            true
        );

        // Step 3: Generate Attack Clip (18 fps, non-looping)
        AnimationClip attackClip = BuildSpriteAnimationClip(
            $"{ANIM_DIR}/StrawhatStaffAttack.anim",
            ATTACK_FRAMES_DIR,
            18,
            false
        );

        // Delete legacy Idle clip if present (no idle state)
        string legacyIdleClip = $"{ANIM_DIR}/StrawhatStaffIdle.anim";
        if (File.Exists(legacyIdleClip))
        {
            AssetDatabase.DeleteAsset(legacyIdleClip);
        }

        if (runClip == null || attackClip == null)
        {
            Debug.LogError("[StrawhatStaffSetupTool] Failed to build animation clips!");
            return;
        }

        // Step 4: Create Animator Controller (Run is default entry state, no idle)
        string controllerPath = $"{ANIM_DIR}/StrawhatStaffController.controller";
        AnimatorController controller = CreateAnimatorController(controllerPath, runClip, attackClip);

        // Step 5: Load Initial Sprite (first frame of Run)
        List<Sprite> runSpritesList = LoadSprites(RUN_FRAMES_DIR);
        Sprite initialSprite = (runSpritesList.Count > 0) ? runSpritesList[0] : null;

        // Step 6: Assemble and save prefabs
        string prefabPath1 = $"{PREFAB_DIR}/StrawhatStaffMob.prefab";
        string prefabPath2 = $"{RESOURCES_PREFAB_DIR}/StrawhatStaffMob.prefab";

        BuildAndSavePrefab(prefabPath1, controller, initialSprite);
        BuildAndSavePrefab(prefabPath2, controller, initialSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[StrawhatStaffSetupTool] Setup Complete! Staff Strawhat Mob animations and prefabs configured successfully (Idle state removed, continuous Run locomotion).</color>");
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

    private static AnimatorController CreateAnimatorController(string path, AnimationClip runClip, AnimationClip attackClip)
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

        // States: Run is default locomotion! (No Idle state)
        var runState = rootSm.AddState("Run");
        runState.motion = runClip;
        rootSm.defaultState = runState;

        var attackState = rootSm.AddState("Attack");
        attackState.motion = attackClip;

        // Transitions:
        // AnyState -> Attack
        var anyToAttack = rootSm.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.02f;

        // Run -> Attack (Direct transition)
        var runToAttack = runState.AddTransition(attackState);
        runToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        runToAttack.hasExitTime = false;
        runToAttack.duration = 0.02f;

        // Attack -> Run (Return to run when attack finishes)
        var attackToRun = attackState.AddTransition(runState);
        attackToRun.hasExitTime = true;
        attackToRun.exitTime = 0.92f;
        attackToRun.duration = 0.05f;

        return controller;
    }

    private static void BuildAndSavePrefab(string savePath, AnimatorController controller, Sprite initialSprite)
    {
        GameObject go = new GameObject("StrawhatStaffMob");
        go.tag = "enemy";

        // Scaled bigger than player (mob will be ~6.3u tall)
        go.transform.localScale = new Vector3(0.68f, 0.68f, 1.0f);

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

        // Main Collider matching actual sprite silhouette
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3.6f, 9.4f);
        col.offset = new Vector2(-0.15f, 0.0f);

        // AI Script
        StrawhatStaffAI ai = go.AddComponent<StrawhatStaffAI>();
        ai.maxHealth = 120;
        ai.directSlamDamage = 24;
        ai.shockwaveDamage = 16;
        ai.playerKnockbackForce = 7.5f;
        ai.enableParry = true;
        ai.parryCountPerFive = 3;

        // Populate Sprite Sequences directly on Prefab
        List<Sprite> runSprites = LoadSprites(RUN_FRAMES_DIR);
        List<Sprite> attackSprites = LoadSprites(ATTACK_FRAMES_DIR);
        ai.runSprites = runSprites.ToArray();
        ai.attackSprites = attackSprites.ToArray();

        // Health Component
        Health hp = go.AddComponent<Health>();
        hp.maxHealth = 120;

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(go, savePath);
        Object.DestroyImmediate(go);
    }
}
#endif
