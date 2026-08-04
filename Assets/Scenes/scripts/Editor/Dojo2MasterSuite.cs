#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dojo2MasterSuite - Unified Editor Menu Tool for all Dojo 2 operations.
/// Accessible via 'Tools -> Dojo 2 -> Run Complete Dojo 2 Auto-Setup'.
/// </summary>
public static class Dojo2MasterSuite
{
    [MenuItem("Tools/Dojo 2/Run Complete Dojo 2 Auto-Setup", false, 1)]
    public static void RunFullSetup()
    {
        Debug.Log("[Dojo2MasterSuite] === STARTING COMPLETE DOJO 2 AUTO-SETUP ===");

        // 0. Ensure Dojo1Scene and Dojo2Scene are in Build Settings
        EnsureBuildSettingsRegistered();

        // 1. Setup Dojo 2 Wave Manager, Spawn Points, Gates & Health Orbs
        Dojo2SetupTool.SetupDojo2WaveManager();

        // 2. Duplicate Enter Sign & Position at Dojo 2 Entrance
        Dojo2EnterSignTool.DuplicateAndAttachEnterSign();

        // 3. Build/Rebuild Parkour Climbing Route
        Dojo2ParkourBuilder.BuildParkourSection();

        // 4. Generate Mob Animations & Prefabs
        MobAnimationGenerator.GenerateAllMobs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Dojo2MasterSuite] === DOJO 2 COMPLETE AUTO-SETUP FINISHED SUCCESSFULLY! ===");
        EditorUtility.DisplayDialog("Dojo 2 Auto-Setup Complete", 
            "All Dojo 2 components have been configured:\n\n" +
            "1. Dojo 2 Wave Manager (5 Waves + Health Orbs)\n" +
            "2. Enter Sign configured to load Dojo2Scene -> Dojo2_SpawnPoint\n" +
            "3. Parkour Climbing Section Built\n" +
            "4. Mob Animations & Prefabs Generated\n\n" +
            "You are ready to play!", "Awesome!");
    }

    public static void EnsureBuildSettingsRegistered()
    {
        string[] scenePaths = new string[]
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/MountainPathScene.unity",
            "Assets/Scenes/Dojo1Scene.unity",
            "Assets/Scenes/Dojo2Scene.unity",
            "Assets/Scenes/CaveScene.unity"
        };

        var currentScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool modified = false;

        foreach (string path in scenePaths)
        {
            if (System.IO.File.Exists(path))
            {
                bool exists = false;
                foreach (var s in currentScenes)
                {
                    if (s.path == path) { exists = true; break; }
                }
                if (!exists)
                {
                    currentScenes.Add(new EditorBuildSettingsScene(path, true));
                    modified = true;
                    Debug.Log($"[Dojo2MasterSuite] Added '{path}' to Build Settings.");
                }
            }
        }

        if (modified)
        {
            EditorBuildSettings.scenes = currentScenes.ToArray();
        }
    }
}
#endif
