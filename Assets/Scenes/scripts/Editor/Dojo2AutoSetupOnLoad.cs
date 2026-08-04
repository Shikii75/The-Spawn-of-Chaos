#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dojo2AutoSetupOnLoad - Automatically verifies and attaches the Dojo 2 enter sign
/// (targeting Dojo2Scene -> Dojo2_SpawnPoint) whenever SampleScene is opened or played in Unity.
/// </summary>
[InitializeOnLoad]
public static class Dojo2AutoSetupOnLoad
{
    static Dojo2AutoSetupOnLoad()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EnsureDojo2EnterSignConfigured(scene);
    }

    private static void OnHierarchyChanged()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name == "SampleScene")
        {
            EnsureDojo2EnterSignConfigured(scene);
        }
    }

    public static void EnsureDojo2EnterSignConfigured(Scene scene)
    {
        if (scene.name != "SampleScene" && scene.name != "MountainPathScene") return;

        // Ensure Dojo1Scene and Dojo2Scene are in Build Settings
        Dojo2MasterSuite.EnsureBuildSettingsRegistered();

        GameObject dojo2EnterSign = GameObject.Find("Dojo2_EnterSign");
        if (dojo2EnterSign == null)
        {
            // Find existing entersign template
            GameObject templateSign = GameObject.Find("press\"E\"") ?? GameObject.FindWithTag("entersign");
            if (templateSign != null)
            {
                dojo2EnterSign = Object.Instantiate(templateSign);
                dojo2EnterSign.name = "Dojo2_EnterSign";
                dojo2EnterSign.transform.position = new Vector3(1476.5f, 323.5f, 0f);

                GameObject dojo2Root = GameObject.Find("Dojo 2") ?? GameObject.Find("Dojo2");
                if (dojo2Root != null) dojo2EnterSign.transform.SetParent(dojo2Root.transform);

                Debug.Log("[Dojo2AutoSetupOnLoad] Automatically created 'Dojo2_EnterSign' at position (1476.5, 323.5)!");
            }
        }

        if (dojo2EnterSign != null)
        {
            entersign signComp = dojo2EnterSign.GetComponent<entersign>();
            if (signComp == null) signComp = dojo2EnterSign.AddComponent<entersign>();

            // FORCE target to Dojo2Scene and Dojo2_SpawnPoint
            if (signComp.targetSceneName != "Dojo2Scene" || signComp.targetSpawnPointName != "Dojo2_SpawnPoint")
            {
                signComp.targetSceneName = "Dojo2Scene";
                signComp.targetSpawnPointName = "Dojo2_SpawnPoint";
                signComp.interactKey = KeyCode.E;

                EditorUtility.SetDirty(signComp);
                Debug.Log("[Dojo2AutoSetupOnLoad] Configured 'Dojo2_EnterSign' -> targetSceneName='Dojo2Scene', targetSpawnPointName='Dojo2_SpawnPoint'");
            }
        }

        // Clean up any Dojo2WaveManager attached to SampleScene/MountainPathScene so wave battle & combat music NEVER run outside!
        Dojo2WaveManager outsideWaveMgr = Object.FindFirstObjectByType<Dojo2WaveManager>();
        if (outsideWaveMgr != null && scene.name != "Dojo2Scene")
        {
            Debug.Log($"[Dojo2AutoSetupOnLoad] Removing Dojo2WaveManager from outside scene '{scene.name}' so waves only occur inside Dojo2Scene.");
            Object.DestroyImmediate(outsideWaveMgr);
        }
    }
}
#endif
