#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SpawnOfChaos.NPC;

public class GenbuBuilder
{
    [MenuItem("Tools/Build Genbu Prefab")]
    public static void BuildGenbuPrefab()
    {
        Debug.Log("[GenbuBuilder] Starting Genbu Animation & Prefab Generation...");

        // Ensure directories exist
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/NPC"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "NPC");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/animations/animators"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/animations"))
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "animations");
            }
            AssetDatabase.CreateFolder("Assets/Scenes/animations", "animators");
        }

        string genbuBaseFrames = "Assets/Scenes/animations/frames/Genbu!";
        string idleFramesPath = genbuBaseFrames + "/genbuidle-e7bd0ded";
        string swimFramesPath = genbuBaseFrames + "/genbuswiminingmp4-25d7d740";
        string animSaveDir = "Assets/Scenes/animations/animators";

        // 1. Create animation clips
        AnimationClip idleClip = CreateClipFromFolder(idleFramesPath, animSaveDir + "/GenbuIdle.anim", 10f, true);
        AnimationClip swimClip = CreateClipFromFolder(swimFramesPath, animSaveDir + "/GenbuSwim.anim", 12f, true);

        // 2. Create AnimatorController
        string controllerPath = animSaveDir + "/GenbuController.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("isSwimming", AnimatorControllerParameterType.Bool);

        var rootStateMachine = controller.layers[0].stateMachine;

        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        var swimState = rootStateMachine.AddState("Swim");
        swimState.motion = swimClip;

        // Transition: Idle -> Swim
        var idleToSwim = idleState.AddTransition(swimState);
        idleToSwim.AddCondition(AnimatorConditionMode.If, 0, "isSwimming");
        idleToSwim.hasExitTime = false;
        idleToSwim.duration = 0.1f;

        // Transition: Swim -> Idle
        var swimToIdle = swimState.AddTransition(idleState);
        swimToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isSwimming");
        swimToIdle.hasExitTime = false;
        swimToIdle.duration = 0.1f;

        // 3. Assemble GameObject & Prefab
        GameObject genbuGO = new GameObject("Genbu_GiantTurtle");
        genbuGO.tag = "Untagged";

        SpriteRenderer sr = genbuGO.AddComponent<SpriteRenderer>();
        Sprite defaultSprite = GetFirstSpriteFromFolder(idleFramesPath);
        if (defaultSprite != null) sr.sprite = defaultSprite;
        sr.sortingOrder = 2;

        Animator anim = genbuGO.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        BoxCollider2D col = genbuGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(4.5f, 2.2f);
        col.offset = new Vector2(0f, -0.2f);

        GenbuFerryController ferry = genbuGO.AddComponent<GenbuFerryController>();

        // Shell Mount Point child
        GameObject mountGO = new GameObject("ShellMountPoint");
        mountGO.transform.SetParent(genbuGO.transform, false);
        mountGO.transform.localPosition = new Vector3(-0.2f, 1.35f, 0f);
        ferry.shellMountPoint = mountGO.transform;

        // Save Prefab
        string prefabPath = "Assets/Prefabs/NPC/Genbu.prefab";
        PrefabUtility.SaveAsPrefabAsset(genbuGO, prefabPath);
        Object.DestroyImmediate(genbuGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[GenbuBuilder] Genbu Prefab successfully created at: " + prefabPath + "</color>");
    }

    [MenuItem("Tools/Spawn Genbu In Tutorial Scene")]
    public static void SpawnGenbuInTutorialScene()
    {
        string scenePath = "Assets/Scenes/TutorialScene.unity";
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.path != scenePath)
        {
            Debug.Log("[GenbuBuilder] Opening TutorialScene at: " + scenePath);
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPC/Genbu.prefab");
        if (prefab == null)
        {
            BuildGenbuPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPC/Genbu.prefab");
        }

        Debug.Log("[GenbuBuilder] Loaded Genbu prefab: " + (prefab != null ? prefab.name : "NULL"));

        GameObject existingGenbu = GameObject.Find("Genbu_GiantTurtle");

        // Create Waypoints Container
        GameObject waypointsHolder = GameObject.Find("Genbu_Waypoints");
        if (waypointsHolder == null)
        {
            waypointsHolder = new GameObject("Genbu_Waypoints");
            Undo.RegisterCreatedObjectUndo(waypointsHolder, "Create Genbu Waypoints");
        }

        Transform startPoint = waypointsHolder.transform.Find("StartLedgePoint");
        if (startPoint == null)
        {
            GameObject spGO = new GameObject("StartLedgePoint");
            spGO.transform.SetParent(waypointsHolder.transform, false);
            spGO.transform.position = new Vector3(33.5f, -5.0f, 0f);
            startPoint = spGO.transform;
        }

        Transform destPoint = waypointsHolder.transform.Find("DestinationPlatformPoint");
        if (destPoint == null)
        {
            GameObject dpGO = new GameObject("DestinationPlatformPoint");
            dpGO.transform.SetParent(waypointsHolder.transform, false);
            dpGO.transform.position = new Vector3(64.0f, 3.5f, 0f);
            destPoint = dpGO.transform;
        }

        Transform disembarkPoint = waypointsHolder.transform.Find("DisembarkLandingPoint");
        if (disembarkPoint == null)
        {
            GameObject dlpGO = new GameObject("DisembarkLandingPoint");
            dlpGO.transform.SetParent(waypointsHolder.transform, false);
            dlpGO.transform.position = new Vector3(67.0f, 4.8f, 0f);
            disembarkPoint = dlpGO.transform;
        }

        GameObject genbuInstance = existingGenbu;
        if (genbuInstance == null)
        {
            if (prefab != null)
            {
                genbuInstance = (GameObject)Object.Instantiate(prefab);
            }
            else
            {
                genbuInstance = new GameObject("Genbu_GiantTurtle");
                genbuInstance.AddComponent<SpriteRenderer>();
                genbuInstance.AddComponent<Animator>();
                genbuInstance.AddComponent<BoxCollider2D>();
                genbuInstance.AddComponent<GenbuFerryController>();
            }

            genbuInstance.name = "Genbu_GiantTurtle";
            Undo.RegisterCreatedObjectUndo(genbuInstance, "Spawn Genbu");
        }
        if (genbuInstance != null)
        {
            genbuInstance.transform.position = startPoint.position;
        }

        GenbuFerryController ferryComp = genbuInstance.GetComponent<GenbuFerryController>();
        if (ferryComp != null)
        {
            ferryComp.startLedgePoint = startPoint;
            ferryComp.destinationPlatformPoint = destPoint;
            ferryComp.disembarkLandingPoint = disembarkPoint;
            EditorUtility.SetDirty(ferryComp);
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Selection.activeGameObject = genbuInstance;
        Debug.Log("<color=#55FF88>[GenbuBuilder] Successfully spawned and configured Genbu in TutorialScene!</color>");
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
            if (sprite == null)
            {
                // In multiple sprite mode, load all sub-assets
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var obj in subAssets)
                {
                    if (obj is Sprite s)
                    {
                        sprite = s;
                        break;
                    }
                }
            }

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
