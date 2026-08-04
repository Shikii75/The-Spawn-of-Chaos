#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dojo2SetupTool - Editor menu tool to automatically attach and configure Dojo2WaveManager
/// inside Dojo 2 in the active scene (SampleScene), wiring up mob spawners, gates, and Nightmare Orbs.
/// </summary>
public class Dojo2SetupTool : EditorWindow
{
    [MenuItem("Tools/Dojo 2/Setup Wave Manager and Health Orbs")]
    public static void ShowWindow()
    {
        GetWindow<Dojo2SetupTool>("Dojo 2 Setup Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Dojo 2 Wave Battle & Health Orb Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Attaches and configures Dojo2WaveManager under 'Dojo 2' in the active scene.\n" +
            "- Preserves user custom mob scale & colors\n" +
            "- Sets up 5 escalating waves (Wave 4: 3 Health Orbs)\n" +
            "- Connects gates, mob spawners, and Fat Kabuto spawners.", 
            MessageType.Info);

        if (GUILayout.Button("Setup Dojo 2 Wave Manager in Scene", GUILayout.Height(40)))
        {
            SetupDojo2WaveManager();
        }
    }

    public static void SetupDojo2WaveManager()
    {
        GameObject dojo2Root = GameObject.Find("Dojo 2") ?? GameObject.Find("Dojo2");
        if (dojo2Root == null)
        {
            Debug.LogError("[Dojo2SetupTool] Could not find 'Dojo 2' root object in active scene!");
            return;
        }

        Dojo2WaveManager waveMgr = dojo2Root.GetComponent<Dojo2WaveManager>();
        if (waveMgr == null)
        {
            waveMgr = Undo.AddComponent<Dojo2WaveManager>(dojo2Root);
        }

        // Find Mob Spawners under Dojo 2
        Transform mobSpawner1 = dojo2Root.transform.Find("MobSpawner 1") ?? dojo2Root.transform.Find("MobSpawner");
        Transform mobSpawner2 = dojo2Root.transform.Find("MobSpawner 2");
        Transform mobSpawner3 = dojo2Root.transform.Find("MobSpawner 3");
        Transform fatKabutoSpawner = dojo2Root.transform.Find("FatKabutoSpawner") ?? dojo2Root.transform.Find("FatKabutoOnlySpawner");

        System.Collections.Generic.List<Transform> spawners = new System.Collections.Generic.List<Transform>();
        if (mobSpawner1 != null) spawners.Add(mobSpawner1);
        if (mobSpawner2 != null) spawners.Add(mobSpawner2);
        if (mobSpawner3 != null) spawners.Add(mobSpawner3);

        waveMgr.spawnPoints = spawners.ToArray();
        waveMgr.fatKabutoSpawnPoint = fatKabutoSpawner != null ? fatKabutoSpawner : (mobSpawner1 != null ? mobSpawner1 : dojo2Root.transform);

        // Find Dojo Gates
        Transform leftGate = dojo2Root.transform.Find("LeftGate") ?? dojo2Root.transform.Find("gate_left");
        Transform rightGate = dojo2Root.transform.Find("RightGate") ?? dojo2Root.transform.Find("gate_right");

        System.Collections.Generic.List<GameObject> gates = new System.Collections.Generic.List<GameObject>();
        if (leftGate != null) gates.Add(leftGate.gameObject);
        if (rightGate != null) gates.Add(rightGate.gameObject);
        waveMgr.dojoGates = gates.ToArray();

        // Assign mob scene objects / prefabs
        GameObject maleObj = GameObject.Find("NormalMaleSamurai") ?? GameObject.Find("normalmalesamurai");
        GameObject femaleObj = GameObject.Find("NormalFemaleSamurai") ?? GameObject.Find("normalfemalesamurai");
        GameObject fatObj = GameObject.Find("FatKabuto") ?? GameObject.Find("fatkabuto");

        if (maleObj != null) waveMgr.normalMaleSamuraiPrefab = maleObj;
        if (femaleObj != null) waveMgr.normalFemaleSamuraiPrefab = femaleObj;
        if (fatObj != null) waveMgr.fatKabutoPrefab = fatObj;

        // Set combat music to temple-thunder (2) specifically for Dojo 2
        AudioClip templeThunder2 = Resources.Load<AudioClip>("Audio/temple-thunder (2)");
        if (templeThunder2 != null) waveMgr.combatMusic = templeThunder2;

        // Upgrade any NightmareOrb in front of Dojo 1/Dojo 2 to Health Restoration Orbs
        NightmareOrbAI[] orbs = Object.FindObjectsByType<NightmareOrbAI>(FindObjectsSortMode.None);
        foreach (var orb in orbs)
        {
            if (orb != null)
            {
                orb.isHealthOrbOnly = true;
                orb.healthRestoreAmount = 35;
                EditorUtility.SetDirty(orb);
            }
        }

        EditorUtility.SetDirty(waveMgr);
        Undo.RegisterCompleteObjectUndo(dojo2Root, "Setup Dojo 2 Wave Manager");

        Debug.Log("[Dojo2SetupTool] Successfully setup Dojo 2 Wave Manager and Health Orbs!");
        Selection.activeGameObject = dojo2Root;
    }
}
#endif
