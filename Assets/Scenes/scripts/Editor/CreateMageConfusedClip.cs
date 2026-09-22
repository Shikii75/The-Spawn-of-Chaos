#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Creates the MageConfused animation clip from the frame folder and attaches it to the player animator controller.
/// Use: Tools > Create Mage Confused Animation
/// </summary>
public static class CreateMageConfusedClip
{
    [MenuItem("Tools/Create Mage Confused Animation")]
    public static void Create()
    {
        string sourceFolder = "Assets/Scenes/animations/MageAnimations/Unused/mageconfused-cca60cc5";
        string savePath = "Assets/Scenes/animations/MageAnimations/Used/MageConfused.anim";

        if (!Directory.Exists(sourceFolder))
        {
            Debug.LogError("[CreateMageConfusedClip] Source folder not found: " + sourceFolder);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/animations/MageAnimations/Used"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/animations/MageAnimations"))
            {
                AssetDatabase.CreateFolder("Assets/Scenes/animations", "MageAnimations");
            }
            AssetDatabase.CreateFolder("Assets/Scenes/animations/MageAnimations", "Used");
        }

        List<Sprite> sprites = LoadSpritesFromFolder(sourceFolder);
        if (sprites.Count == 0)
        {
            Debug.LogError("[CreateMageConfusedClip] No PNG sprites found in: " + sourceFolder);
            return;
        }

        AnimationClip clip = new AnimationClip();
        clip.name = "MageConfused";
        clip.frameRate = 12f;

        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe();
            keyframes[i].time = i / clip.frameRate;
            keyframes[i].value = sprites[i];
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, savePath);

        string controllerPath = "Assets/Scenes/animations/animator controller/Player.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller != null)
        {
            AddStateIfMissing(controller, "MageConfused", clip);
        }
        else
        {
            Debug.LogWarning("[CreateMageConfusedClip] Player.controller not found at: " + controllerPath + ". Clip created anyway.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateMageConfusedClip] Created: " + savePath);
    }

    private static void AddStateIfMissing(AnimatorController controller, string stateName, AnimationClip clip)
    {
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        foreach (var state in root.states)
        {
            if (state.state.name == stateName)
            {
                state.state.motion = clip;
                return;
            }
        }

        var newState = root.AddState(stateName);
        newState.motion = clip;

        if (root.defaultState == null)
        {
            root.defaultState = newState;
        }

        // Idle -> MageConfused transition
        AnimatorState idle = FindState(root, "idle");
        if (idle != null)
        {
            var transition = idle.AddTransition(newState);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0, "Confused");
        }

        // MageConfused -> Idle transition
        var returnTransition = newState.AddTransition(idle ?? root.defaultState);
        returnTransition.hasExitTime = true;
        returnTransition.exitTime = 0.9f;
        returnTransition.duration = 0.05f;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (var state in stateMachine.states)
        {
            if (string.Equals(state.state.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return state.state;
            }
        }

        return null;
    }

    private static List<Sprite> LoadSpritesFromFolder(string folderPath)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (!Directory.Exists(folderPath)) return sprites;

        string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
        System.Array.Sort(files);

        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            int index = assetPath.IndexOf("Assets/");
            if (index >= 0)
            {
                assetPath = assetPath.Substring(index);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites;
    }
}
#endif
