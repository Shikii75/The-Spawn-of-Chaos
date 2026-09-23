using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SpawnOfChaos.Editor
{
    [InitializeOnLoad]
    public static class FixBuildSettingsOnLoad
    {
        static FixBuildSettingsOnLoad()
        {
            EditorApplication.delayCall += EnsureCorrectBuildSettings;
        }

        [MenuItem("Tools/Ensure Build Settings Correct")]
        public static void EnsureCorrectBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/CaveScene.unity", true));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Dojo1Scene.unity", true));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Dojo2Scene.unity", true));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/TutorialScene.unity", true));

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("<color=green>[FixBuildSettingsOnLoad] Verified: 'Assets/Scenes/SampleScene.unity' is Scene 0 in Build Settings.</color>");
        }
    }
}
