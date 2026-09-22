#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor setup tool to configure the Staff Strawhat Mob:
/// 1. Verifies animation clips.
/// 2. Creates the AnimatorController with fluid state transitions.
/// 3. Builds and saves the complete mob Prefab at:
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

    [InitializeOnLoadMethod]
    private static void AutoRunIfPending()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists($"{PREFAB_DIR}/StrawhatStaffMob.prefab"))
            {
                SetupMob();
            }
        };
    }

    [MenuItem("Tools/Setup Staff Strawhat Mob")]
    public static void SetupMob()
    {
        Debug.Log("<color=#C840FF>[StrawhatStaffSetupTool] Starting automated setup for Staff Strawhat Mob...</color>");

        EnsureFolderExists("Assets/Prefabs", "Enemies");
        EnsureFolderExists("Assets/Resources/Prefabs", "Enemies");

        // Load Animation Clips
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ANIM_DIR}/StrawhatStaffIdle.anim");
        AnimationClip runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ANIM_DIR}/StrawhatStaffRun.anim");
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ANIM_DIR}/StrawhatStaffAttack.anim");

        if (idleClip == null || runClip == null || attackClip == null)
        {
            Debug.LogError("[StrawhatStaffSetupTool] Failed to find one or more AnimationClips! Run anim generation first.");
            return;
        }

        // Create Animator Controller
        string controllerPath = $"{ANIM_DIR}/StrawhatStaffController.controller";
        AnimatorController controller = CreateAnimatorController(controllerPath, idleClip, runClip, attackClip);

        // Load Initial Sprite
        Sprite initialSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/animations/frames/newfemalestrawstaffattack-1f468d59/frame_001.png");
        if (initialSprite == null)
        {
            initialSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/animations/frames/newfeenalestrawidle-69b4ec11/frame_001.png");
        }

        // Assemble and save prefabs
        string prefabPath1 = $"{PREFAB_DIR}/StrawhatStaffMob.prefab";
        string prefabPath2 = $"{RESOURCES_PREFAB_DIR}/StrawhatStaffMob.prefab";

        BuildAndSavePrefab(prefabPath1, controller, initialSprite);
        BuildAndSavePrefab(prefabPath2, controller, initialSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[StrawhatStaffSetupTool] Setup Complete! Staff Strawhat Mob created at: " + prefabPath1 + "</color>");
    }

    private static void EnsureFolderExists(string parent, string sub)
    {
        string full = $"{parent}/{sub}";
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, sub);
        }
    }

    private static AnimatorController CreateAnimatorController(string path, AnimationClip idleClip, AnimationClip runClip, AnimationClip attackClip)
    {
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
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
        // Idle -> Run
        var idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "isRunning");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.08f;

        // Run -> Idle
        var runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.08f;

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
