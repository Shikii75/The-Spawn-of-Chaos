#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor setup tool to configure the Teleporting Strawhat Mob:
/// 1. Configures TextureImporters on all frame PNGs to Sprite (Single, 100 PPU, Bilinear).
/// 2. Generates the 3 AnimationClips (Idle, Walk, Attack).
/// 3. Creates the AnimatorController with fluid state transitions.
/// 4. Builds and saves the complete mob Prefab at:
///    - Assets/Prefabs/Enemies/StrawhatTeleportMob.prefab
///    - Assets/Resources/Prefabs/Enemies/StrawhatTeleportMob.prefab
/// 
/// Menu Item: Tools > Setup Teleporting Strawhat Mob
/// </summary>
public static class StrawhatTeleportSetupTool
{
    private const string IDLE_FOLDER = "Assets/Scenes/animations/frames/newfeenalestrawidle-69b4ec11";
    private const string WALK_FOLDER = "Assets/Scenes/animations/frames/newfemalestrawwalk-8a077ed8";
    private const string ATTACK_FOLDER = "Assets/Scenes/animations/frames/newfemalestrawattack-8750f70d";

    private const string ANIM_DIR = "Assets/Scenes/animations/animators";
    private const string PREFAB_DIR = "Assets/Prefabs/Enemies/Dojo1";
    private const string RESOURCES_PREFAB_DIR = "Assets/Resources/Prefabs/Enemies/Dojo1";

    [InitializeOnLoadMethod]
    private static void AutoRunIfPending()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists("Assets/Prefabs/Enemies/StrawhatTeleportMob.prefab"))
            {
                SetupMob();
            }
        };
    }

    [MenuItem("Tools/Setup Teleporting Strawhat Mob")]
    public static void SetupMob()
    {
        Debug.Log("<color=#D47BFF>[StrawhatTeleportSetupTool] Starting automated setup...</color>");

        // Step 1: Ensure directories exist
        EnsureFolderExists("Assets/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Resources/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Scenes/animations", "animators");

        // Step 2: Configure texture importers for all 3 frame folders
        ConfigureFolderTextures(IDLE_FOLDER);
        ConfigureFolderTextures(WALK_FOLDER);
        ConfigureFolderTextures(ATTACK_FOLDER);

        AssetDatabase.Refresh();

        // Step 3: Create Animation Clips
        AnimationClip idleClip = CreateAnimationClip(IDLE_FOLDER, $"{ANIM_DIR}/StrawhatTeleportIdle.anim", "Idle", 12f, true);
        AnimationClip walkClip = CreateAnimationClip(WALK_FOLDER, $"{ANIM_DIR}/StrawhatTeleportWalk.anim", "Walk", 16f, true);
        AnimationClip attackClip = CreateAnimationClip(ATTACK_FOLDER, $"{ANIM_DIR}/StrawhatTeleportAttack.anim", "Attack", 20f, false);

        if (idleClip == null || walkClip == null || attackClip == null)
        {
            Debug.LogError("[StrawhatTeleportSetupTool] Failed to create one or more animation clips!");
            return;
        }

        // Step 4: Create Animator Controller
        string controllerPath = $"{ANIM_DIR}/StrawhatTeleportController.controller";
        AnimatorController controller = CreateAnimatorController(controllerPath, idleClip, walkClip, attackClip);

        // Step 5: Assemble and Save Prefab
        List<Sprite> idleSprites = LoadSprites(IDLE_FOLDER);
        Sprite initialSprite = (idleSprites != null && idleSprites.Count > 0) ? idleSprites[0] : null;

        string prefabPath1 = $"{PREFAB_DIR}/StrawhatTeleportMob.prefab";
        string prefabPath2 = $"{RESOURCES_PREFAB_DIR}/StrawhatTeleportMob.prefab";

        BuildAndSavePrefab(prefabPath1, controller, initialSprite);
        BuildAndSavePrefab(prefabPath2, controller, initialSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[StrawhatTeleportSetupTool] Setup Complete! Prefab created at: " + prefabPath1 + "</color>");
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

    private static AnimationClip CreateAnimationClip(string sourceFolder, string savePath, string clipName, float fps, bool isLoop)
    {
        List<Sprite> sprites = LoadSprites(sourceFolder);
        if (sprites == null || sprites.Count == 0)
        {
            Debug.LogError($"[StrawhatTeleportSetupTool] No sprites loaded for {clipName} from {sourceFolder}");
            return null;
        }

        AnimationClip clip = new AnimationClip();
        clip.name = clipName;
        clip.frameRate = fps;

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
                time = i / fps,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = isLoop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Delete existing asset if present
        if (File.Exists(savePath))
        {
            AssetDatabase.DeleteAsset(savePath);
        }

        AssetDatabase.CreateAsset(clip, savePath);
        return clip;
    }

    private static AnimatorController CreateAnimatorController(string path, AnimationClip idleClip, AnimationClip walkClip, AnimationClip attackClip)
    {
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Parameters
        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var rootSm = controller.layers[0].stateMachine;

        // States
        var idleState = rootSm.AddState("Idle");
        idleState.motion = idleClip;
        rootSm.defaultState = idleState;

        var walkState = rootSm.AddState("Walk");
        walkState.motion = walkClip;

        var attackState = rootSm.AddState("Attack");
        attackState.motion = attackClip;

        // Transitions:
        // Idle -> Walk
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.08f;

        // Walk -> Idle
        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.08f;

        // AnyState -> Attack
        var anyToAttack = rootSm.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.04f;

        // Attack -> Idle
        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.95f;
        attackToIdle.duration = 0.08f;

        return controller;
    }

    private static void BuildAndSavePrefab(string savePath, AnimatorController controller, Sprite initialSprite)
    {
        GameObject go = new GameObject("StrawhatTeleportMob");
        go.tag = "enemy";

        // Transform scale: bigger than player (~5.4 units tall in world space)
        go.transform.localScale = new Vector3(0.58f, 0.58f, 1.0f);

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

        // Main Physical Collider
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3.4f, 8.8f);
        col.offset = new Vector2(-0.4f, -0.1f);

        // Body Attack Hitbox Child Trigger
        GameObject hitboxChild = new GameObject("BodyAttackHitbox");
        hitboxChild.transform.SetParent(go.transform, false);
        hitboxChild.tag = "enemy";
        BoxCollider2D hitboxCol = hitboxChild.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector2(4.2f, 9.0f);
        hitboxCol.offset = new Vector2(-0.4f, -0.1f);

        // AI Script
        StrawhatTeleportAI ai = go.AddComponent<StrawhatTeleportAI>();
        ai.maxHealth = 100;
        ai.attackDamage = 18;
        ai.playerKnockbackForce = 6.5f;
        ai.attackHitboxSize = new Vector2(4.2f, 9.0f);
        ai.enableDodge = true;
        ai.dodgeCountPerFive = 3;

        // Health Component
        Health hp = go.AddComponent<Health>();
        hp.maxHealth = 100;

        // Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(go, savePath);
        Object.DestroyImmediate(go);
    }
}
#endif
