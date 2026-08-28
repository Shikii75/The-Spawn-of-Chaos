#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dojo2SetupTool - Automatically attaches and configures Dojo2WaveManager
/// inside Dojo2Scene or SampleScene, wiring up mob spawners, gates, audio, and exit doors.
/// </summary>
public class Dojo2SetupTool : EditorWindow
{
    [MenuItem("Tools/Dojo 2/Setup Wave Manager and Arena")]
    public static void ShowWindow()
    {
        GetWindow<Dojo2SetupTool>("Dojo 2 Setup Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Dojo 2 Wave Battle & Arena Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configures Dojo 2 Wave Manager for immediate battle start.\\n" +
            "- Wires up 5 escalating waves of Samurai enemies\\n" +
            "- Connects Left/Right gates and mob spawners\\n" +
            "- Configures temple-thunder (2) BGM and reward loot\\n" +
            "- Ensures spawn points and return doors are established.",
            MessageType.Info);

        if (GUILayout.Button("Setup Dojo 2 in Active Scene", GUILayout.Height(40)))
        {
            SetupDojo2InActiveScene();
        }
    }

    [MenuItem("Tools/Dojo 2/Setup Wave Manager and Health Orbs")]
    public static void SetupDojo2WaveManager()
    {
        SetupDojo2InActiveScene();
    }

    [MenuItem("Tools/Dojo 2/Configure Active Scene")]
    public static void SetupDojo2InActiveScene()
    {
        // 1. Ensure Dojo2_SpawnPoint exists for player entrance
        GameObject spawnPointObj = GameObject.Find("Dojo2_SpawnPoint");
        if (spawnPointObj == null)
        {
            spawnPointObj = new GameObject("Dojo2_SpawnPoint");
            spawnPointObj.transform.position = new Vector3(-15.0f, -6.5f, 0f);
            Undo.RegisterCreatedObjectUndo(spawnPointObj, "Create Dojo2_SpawnPoint");
        }

        // 2. Ensure mob spawners
        Transform mob1 = GameObject.Find("mobspawner")?.transform ?? GameObject.Find("MobSpawner 1")?.transform;
        Transform mob2 = GameObject.Find("mobspawner (1)")?.transform ?? GameObject.Find("MobSpawner 2")?.transform;
        GameObject mob3Obj = GameObject.Find("mobspawner (2)") ?? GameObject.Find("MobSpawner 3");
        if (mob3Obj == null)
        {
            mob3Obj = new GameObject("mobspawner (2)");
            mob3Obj.transform.position = new Vector3(10.5f, -8.0f, 0f);
            Undo.RegisterCreatedObjectUndo(mob3Obj, "Create mobspawner (2)");
        }
        Transform mob3 = mob3Obj.transform;

        Transform fatKabutoSpawner = GameObject.Find("FatKabutoOnlySpawner")?.transform ?? GameObject.Find("FatKabutoSpawner")?.transform;
        if (fatKabutoSpawner == null)
        {
            GameObject fkObj = new GameObject("FatKabutoOnlySpawner");
            fkObj.transform.position = new Vector3(4.15f, -6.0f, 0f);
            Undo.RegisterCreatedObjectUndo(fkObj, "Create FatKabutoOnlySpawner");
            fatKabutoSpawner = fkObj.transform;
        }

        // 3. Find Gates
        GameObject gateL = GameObject.Find("Gate_L") ?? GameObject.Find("LeftGate") ?? GameObject.Find("gate_left");
        GameObject gateR = GameObject.Find("Gate_R") ?? GameObject.Find("RightGate") ?? GameObject.Find("gate_right");

        // 4. Setup DojoChallengeManager GameObject & Dojo2WaveManager
        GameObject challengeMgr = GameObject.Find("DojoChallengeManager") ?? GameObject.Find("Dojo2ChallengeManager");
        if (challengeMgr == null)
        {
            challengeMgr = new GameObject("DojoChallengeManager");
            Undo.RegisterCreatedObjectUndo(challengeMgr, "Create DojoChallengeManager");
        }

        Dojo2WaveManager waveMgr = challengeMgr.GetComponent<Dojo2WaveManager>();
        if (waveMgr == null)
        {
            waveMgr = Undo.AddComponent<Dojo2WaveManager>(challengeMgr);
        }

        System.Collections.Generic.List<Transform> spawners = new System.Collections.Generic.List<Transform>();
        if (mob1 != null) spawners.Add(mob1);
        if (mob2 != null) spawners.Add(mob2);
        if (mob3 != null) spawners.Add(mob3);
        waveMgr.spawnPoints = spawners.ToArray();
        waveMgr.fatKabutoSpawnPoint = fatKabutoSpawner;

        System.Collections.Generic.List<GameObject> gates = new System.Collections.Generic.List<GameObject>();
        if (gateL != null) gates.Add(gateL);
        if (gateR != null) gates.Add(gateR);
        waveMgr.dojoGates = gates.ToArray();

        // Assign mob scene templates
        GameObject maleObj = GameObject.Find("NormalMaleSamurai") ?? GameObject.Find("normalmalesamurai");
        GameObject femaleObj = GameObject.Find("NormalFemaleSamurai") ?? GameObject.Find("normalfemalesamurai");
        GameObject fatObj = GameObject.Find("FatKabuto") ?? GameObject.Find("fatkabuto");

        if (maleObj != null) waveMgr.normalMaleSamuraiPrefab = maleObj;
        if (femaleObj != null) waveMgr.normalFemaleSamuraiPrefab = femaleObj;
        if (fatObj != null) waveMgr.fatKabutoPrefab = fatObj;

        // Wave Timing & Rewards
        waveMgr.autoStartDelay = 0.0f; // Immediate fight start!
        waveMgr.betweenWaveDelay = 2.0f;
        waveMgr.coinRewardCount = 15;

        // Audio Clips
        AudioClip combatClip = Resources.Load<AudioClip>("Audio/temple-thunder (2)") ?? Resources.Load<AudioClip>("Audio/temple-thunder");
        if (combatClip != null) waveMgr.combatMusic = combatClip;

        AudioClip ambientClip = Resources.Load<AudioClip>("Audio/bamboo-incense");
        if (ambientClip != null) waveMgr.ambientMusic = ambientClip;

        // 5. Ensure Exit Door exists to return to SampleScene
        GameObject exitDoor = GameObject.Find("Dojo2_ExitDoor");
        if (exitDoor == null)
        {
            exitDoor = new GameObject("Dojo2_ExitDoor");
            exitDoor.transform.position = new Vector3(-21.0f, -6.0f, 0f);
            var col = exitDoor.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(4.0f, 6.0f);

            var es = exitDoor.AddComponent<entersign>();
            es.targetSceneName = "SampleScene";
            es.targetSpawnPointName = "Dojo2_ReturnPoint";
            es.interactKey = KeyCode.E;
            Undo.RegisterCreatedObjectUndo(exitDoor, "Create Dojo2_ExitDoor");
        }

        EditorUtility.SetDirty(waveMgr);
        EditorUtility.SetDirty(challengeMgr);
        Undo.RegisterCompleteObjectUndo(challengeMgr, "Configure Dojo 2");

        Debug.Log($"[Dojo2SetupTool] Successfully configured Dojo 2 in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'! Fight starts immediately.");
        Selection.activeGameObject = challengeMgr;
    }
}
#endif
