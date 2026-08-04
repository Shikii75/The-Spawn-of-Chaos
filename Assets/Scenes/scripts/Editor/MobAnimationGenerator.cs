#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// MobAnimationGenerator - Editor Tool to generate AnimationClips, AnimatorControllers,
/// and Prefabs for NormalMaleSamurai, NormalFemaleSamurai, and FatKabuto mobs.
/// </summary>
public class MobAnimationGenerator : EditorWindow
{
    [MenuItem("Tools/Dojo 2/Generate Dojo 2 Mob Animations and Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<MobAnimationGenerator>("Mob Animation Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Dojo 2 Mob Animation & Prefab Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates .anim clips, .controller animators, and .prefab objects for:\n" +
            "- NormalMaleSamurai\n- NormalFemaleSamurai\n- FatKabuto", 
            MessageType.Info);

        if (GUILayout.Button("Generate All Mob Animations & Prefabs", GUILayout.Height(40)))
        {
            GenerateAllMobs();
        }
    }

    public static void GenerateAllMobs()
    {
        string animBaseDir = "Assets/Scenes/animations";
        string prefabBaseDir = "Assets/Resources/Prefabs/Enemies";
        string altPrefabDir = "Assets/Prefabs/Enemies";

        if (!Directory.Exists(animBaseDir)) Directory.CreateDirectory(animBaseDir);
        if (!Directory.Exists(prefabBaseDir)) Directory.CreateDirectory(prefabBaseDir);
        if (!Directory.Exists(altPrefabDir)) Directory.CreateDirectory(altPrefabDir);

        // 1. Generate NormalMaleSamurai
        GenerateMob(
            "NormalMaleSamurai",
            "Assets/Scenes/animations/frames/normalmalesamurai",
            new Dictionary<string, string>
            {
                { "NormalMaleSamurai_Idle", "normalmalesamuraistopandstep" },
                { "NormalMaleSamurai_Walk", "normalmalesamuraiwalk" },
                { "NormalMaleSamurai_Run", "normalmalesamurairun" },
                { "NormalMaleSamurai_Slash", "normalmalesamuraislash" }
            },
            typeof(NormalMaleSamuraiAI),
            new Vector2(1.5f, 3.2f)
        );

        // 2. Generate NormalFemaleSamurai
        GenerateMob(
            "NormalFemaleSamurai",
            "Assets/Scenes/animations/frames/normalfemalesamurai",
            new Dictionary<string, string>
            {
                { "NormalFemaleSamurai_Idle", "normalfemalesamuraiidle" },
                { "NormalFemaleSamurai_Walk", "normalfemalesamuraiwalk" },
                { "NormalFemaleSamurai_Run", "normalfemalesamurairun" },
                { "NormalFemaleSamurai_StartCharge", "normalfemalesamuraicharge" },
                { "NormalFemaleSamurai_ContinueCharge", "normalfemalesamuraikeepcharging" },
                { "NormalFemaleSamurai_Slash", "normalfemalesamuraislash" }
            },
            typeof(NormalFemaleSamuraiAI),
            new Vector2(1.5f, 3.2f)
        );

        // 3. Generate FatKabuto
        GenerateMob(
            "FatKabuto",
            "Assets/Scenes/animations/frames/fatkabuto",
            new Dictionary<string, string>
            {
                { "FatKabuto_Entrance", "enterancefatkabuto" },
                { "FatKabuto_Idle", "stopandstepfatkabuto" },
                { "FatKabuto_Walk", "fatkabutowalk" },
                { "FatKabuto_Charge", "fatkabutochargingattack" },
                { "FatKabuto_Bash", "fatkabutobash" }
            },
            typeof(FatKabutoAI),
            new Vector2(2.2f, 3.8f)
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MobAnimationGenerator] All Dojo 2 Mob animations, controllers, and prefabs successfully generated!");
    }

    private static void GenerateMob(string mobName, string frameSourceDir, Dictionary<string, string> animMap, System.Type aiComponentType, Vector2 colliderSize)
    {
        string mobAnimDir = $"Assets/Scenes/animations/{mobName}";
        if (!Directory.Exists(mobAnimDir)) Directory.CreateDirectory(mobAnimDir);

        Dictionary<string, AnimationClip> generatedClips = new Dictionary<string, AnimationClip>();

        foreach (var kvp in animMap)
        {
            string clipName = kvp.Key;
            string folderPrefix = kvp.Value;

            Sprite[] sprites = LoadSubfolderSprites(frameSourceDir, folderPrefix);
            if (sprites.Length > 0)
            {
                string clipPath = $"{mobAnimDir}/{clipName}.anim";
                AnimationClip clip = CreateSpriteAnimationClip(clipPath, sprites, 12f, clipName.Contains("Idle") || clipName.Contains("Walk") || clipName.Contains("Run") || clipName.Contains("ContinueCharge"));
                generatedClips[clipName] = clip;
            }
        }

        // Create AnimatorController
        string controllerPath = $"Assets/Scenes/animations/{mobName}Controller.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState defaultState = null;

        foreach (var kvp in generatedClips)
        {
            AnimatorState state = stateMachine.AddState(kvp.Key);
            state.motion = kvp.Value;

            if (defaultState == null && (kvp.Key.Contains("Idle") || kvp.Key.Contains("Entrance")))
            {
                defaultState = state;
            }
        }

        if (defaultState != null)
        {
            stateMachine.defaultState = defaultState;
        }

        // Build Prefab
        CreateMobPrefab(mobName, controller, generatedClips, aiComponentType, colliderSize);
    }

    private static Sprite[] LoadSubfolderSprites(string baseDir, string subfolderPrefix)
    {
        List<Sprite> list = new List<Sprite>();
        if (!Directory.Exists(baseDir)) return list.ToArray();

        string targetDir = null;
        foreach (string d in Directory.GetDirectories(baseDir))
        {
            string folderName = Path.GetFileName(d).ToLower();
            if (folderName.StartsWith(subfolderPrefix.ToLower()))
            {
                targetDir = d;
                break;
            }
        }

        if (targetDir == null) return list.ToArray();

        string[] pngFiles = Directory.GetFiles(targetDir, "*.png");
        System.Array.Sort(pngFiles);

        foreach (string path in pngFiles)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) list.Add(s);
        }

        return list.ToArray();
    }

    private static AnimationClip CreateSpriteAnimationClip(string savePath, Sprite[] sprites, float frameRate, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, savePath);
        }
        else
        {
            clip.ClearCurves();
        }

        clip.frameRate = frameRate;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty settingsProps = serializedClip.FindProperty("m_AnimationClipSettings");
        if (settingsProps != null)
        {
            SerializedProperty loopTimeProp = settingsProps.FindPropertyRelative("m_LoopTime");
            if (loopTimeProp != null) loopTimeProp.boolValue = loop;
        }
        serializedClip.ApplyModifiedProperties();

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void CreateMobPrefab(string mobName, AnimatorController controller, Dictionary<string, AnimationClip> clips, System.Type aiComponentType, Vector2 colliderSize)
    {
        GameObject go = new GameObject(mobName);
        go.tag = "enemy";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (clips.ContainsKey($"{mobName}_Idle") && clips[$"{mobName}_Idle"] != null)
        {
            var curveBindings = AnimationUtility.GetObjectReferenceCurveBindings(clips[$"{mobName}_Idle"]);
            if (curveBindings != null && curveBindings.Length > 0)
            {
                var bindings = AnimationUtility.GetObjectReferenceCurve(clips[$"{mobName}_Idle"], curveBindings[0]);
                if (bindings != null && bindings.Length > 0 && bindings[0].value is Sprite firstSprite)
                {
                    sr.sprite = firstSprite;
                }
            }
        }

        Animator anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = colliderSize;
        col.offset = new Vector2(0f, 0f);

        go.AddComponent<Health>();
        go.AddComponent<SpriteJuice>();

        if (aiComponentType != null)
        {
            go.AddComponent(aiComponentType);
        }

        // Save Prefabs to Assets/Resources/Prefabs/Enemies and Assets/Prefabs/Enemies
        string path1 = $"Assets/Resources/Prefabs/Enemies/{mobName}.prefab";
        string path2 = $"Assets/Prefabs/Enemies/{mobName}.prefab";

        PrefabUtility.SaveAsPrefabAsset(go, path1);
        PrefabUtility.SaveAsPrefabAsset(go, path2);

        DestroyImmediate(go);
    }
}
#endif
