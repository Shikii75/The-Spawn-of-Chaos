using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// Dojo 1 Wave Manager — Strawhat Clan Dojo
/// Spawns Strawhat clan members from the new Dojo 1 roster across escalating waves:
/// 1. StrawhatTeleportMob  (Nimble shadow teleporter / assassin)
/// 2. StrawhatSwordMob     (Disciplined katana swordsman with 3/5 parry deflect)
/// 3. StrawhatStaffMob     (Swift runner with dark vault slam shockwave)
/// 4. StrawhatBruteMob     (Super armor scythe tank with ground tremor shockwaves)
///
/// Features dynamic difficulty escalation:
/// - Wave 1: 2 Light Skirmishers (Teleport / Sword)
/// - Wave 2: 3 Vanguard Fighters (Teleport / Sword / Staff)
/// - Wave 3: 4 Elite Incursion (Guaranteed Brute / Staff + Sword / Teleport)
/// - Wave 4: 5 Boss Vanguard (Brute Duo + Sword + Staff + Teleport Climax)
///
/// Automatically loads prefabs from Assets/Prefabs/Enemies/Dojo1 or Resources if unassigned.
/// </summary>
public class DojoWaveManager : MonoBehaviour
{
    // ── Dojo 1 Strawhat Clan Prefabs ─────────────────────────────────
    [Header("Dojo 1 Strawhat Clan Prefabs")]
    [Tooltip("Fast teleporting dagger assassin.")]
    public GameObject teleportStrawhatPrefab;

    [Tooltip("Disciplined katana swordsman with low stance and parry deflect.")]
    public GameObject swordStrawhatPrefab;

    [Tooltip("Swift runner with dark ground slam shockwave.")]
    public GameObject staffStrawhatPrefab;

    [Tooltip("Devastating scythe tank with ground tremor.")]
    public GameObject bruteStrawhatPrefab;

    // ── Legacy Prefab Fallbacks ──────────────────────────────────────
    [Header("Legacy Fallbacks (Optional)")]
    public GameObject basicStrawhatPrefab;
    public GameObject fatStrawhatPrefab;
    public GameObject femaleStrawhatPrefab;

    // ── Difficulty & Wave Settings ───────────────────────────────────
    [Header("Difficulty Progression")]
    [Tooltip("If true, wave compositions will be randomized each run with escalating difficulty.")]
    public bool randomizeDifficulty = true;

    [Tooltip("Total number of waves to spawn.")]
    [Range(2, 6)]
    public int totalWaves = 4;

    // ── Spawn Points ─────────────────────────────────────────────────
    [Header("Spawn Points (MobSpawner 1–3)")]
    public Transform[] spawnPoints;

    // ── Gates ─────────────────────────────────────────────────────────
    [Header("Dojo Gates")]
    public GameObject[] dojoGates;

    // ── Timing ────────────────────────────────────────────────────────
    [Header("Wave Timing")]
    [Tooltip("Seconds after scene load before the challenge auto-starts (test mode).")]
    public float autoStartDelay = 5f;

    [Tooltip("Breathing room between waves after all enemies are dead.")]
    public float betweenWaveDelay = 2.5f;

    [Tooltip("Stagger delay between each enemy spawn within a wave.")]
    public float spawnStagger = 0.35f;

    // ── Spawn Polish ──────────────────────────────────────────────────
    [Header("Spawn Polish")]
    public float spawnDropHeight = 1.5f;
    public float aggroDetectionOverride = 50f;

    // ── Rewards ───────────────────────────────────────────────────────
    [Header("Rewards")]
    public GameObject coinPrefab;
    public Transform rewardSpawnPoint;
    public int coinRewardCount = 8;

    // ── Audio Settings ────────────────────────────────────────────────
    [Header("Audio Settings")]
    [Tooltip("Ambient background music track when entering the Dojo scene.")]
    public AudioClip ambientMusic;

    [Tooltip("Combat background music track played when fighting starts (e.g. temple-thunder).")]
    public AudioClip combatMusic;

    // ── Runtime State ─────────────────────────────────────────────────
    private int currentWaveIndex = 0;
    private bool challengeStarted = false;
    private bool challengeCompleted = false;
    private bool isSpawningWave = false;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    public bool IsChallengeStarted => challengeStarted;
    public bool IsChallengeCompleted => challengeCompleted;
    public int CurrentWaveIndex => currentWaveIndex;

    private struct SpawnEntry
    {
        public GameObject prefab;
        public int pointIndex;
        public string mobName;
    }

    private List<List<SpawnEntry>> waveBlueprints;

    public static DojoWaveManager Instance { get; private set; }

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    void Awake()
    {
        Instance = this;
        EnsurePrefabsLoaded();
        CleanOldSceneMobs();
    }

    void Start()
    {
        SetGatesActive(false);
        BuildWaveBlueprints();

        // Dynamically load music clips if not assigned in Inspector
        if (ambientMusic == null)
        {
            ambientMusic = Resources.Load<AudioClip>("Audio/concrete-syntax");
            if (ambientMusic == null) ambientMusic = Resources.Load<AudioClip>("concrete-syntax");
            if (ambientMusic == null) ambientMusic = Resources.Load<AudioClip>("Audio/bamboo-incense");
        }

        if (combatMusic == null)
        {
            combatMusic = Resources.Load<AudioClip>("Audio/temple-thunder");
            if (combatMusic == null) combatMusic = Resources.Load<AudioClip>("temple-thunder");
        }

        // Play initial ambient BGM upon entering scene
        if (ambientMusic != null && AudioManager.Instance != null && StrawhatLeaderNPC.Instance == null)
        {
            AudioManager.Instance.PlayBGM(ambientMusic, fade: true);
        }

        // Auto-start timer (bypassed if StrawhatLeaderNPC is present to handle dialogue flow)
        StartCoroutine(AutoStartAfterDelay());

        Debug.Log("[DojoWaveManager] Initialized with Strawhat Clan Dojo 1 mobs.");
    }

    private IEnumerator AutoStartAfterDelay()
    {
        yield return new WaitForSeconds(autoStartDelay);

        if (!challengeStarted && StrawhatLeaderNPC.Instance == null)
        {
            StartChallenge();
        }
    }

    void Update()
    {
        if (!challengeStarted || challengeCompleted || isSpawningWave) return;

        activeEnemies.RemoveAll(e => e == null);

        if (activeEnemies.Count == 0)
        {
            StartCoroutine(AdvanceWave());
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PREFAB RESOLUTION & SCENE CLEANUP
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures all 4 Dojo 1 Strawhat prefabs are resolved from the folder or resources.
    /// </summary>
    private void EnsurePrefabsLoaded()
    {
#if UNITY_EDITOR
        if (teleportStrawhatPrefab == null)
            teleportStrawhatPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Dojo1/StrawhatTeleportMob.prefab");
        if (swordStrawhatPrefab == null)
            swordStrawhatPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Dojo1/StrawhatSwordMob.prefab");
        if (staffStrawhatPrefab == null)
            staffStrawhatPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Dojo1/StrawhatStaffMob.prefab");
        if (bruteStrawhatPrefab == null)
            bruteStrawhatPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Dojo1/StrawhatBruteMob.prefab");
#endif

        if (teleportStrawhatPrefab == null)
            teleportStrawhatPrefab = Resources.Load<GameObject>("Prefabs/Enemies/Dojo1/StrawhatTeleportMob") ?? Resources.Load<GameObject>("Prefabs/Enemies/StrawhatTeleportMob");
        if (swordStrawhatPrefab == null)
            swordStrawhatPrefab = Resources.Load<GameObject>("Prefabs/Enemies/Dojo1/StrawhatSwordMob") ?? Resources.Load<GameObject>("Prefabs/Enemies/StrawhatSwordMob");
        if (staffStrawhatPrefab == null)
            staffStrawhatPrefab = Resources.Load<GameObject>("Prefabs/Enemies/Dojo1/StrawhatStaffMob") ?? Resources.Load<GameObject>("Prefabs/Enemies/StrawhatStaffMob");
        if (bruteStrawhatPrefab == null)
            bruteStrawhatPrefab = Resources.Load<GameObject>("Prefabs/Enemies/Dojo1/StrawhatBruteMob") ?? Resources.Load<GameObject>("Prefabs/Enemies/StrawhatBruteMob");

        // Fallbacks to legacy prefabs if any remain null
        if (swordStrawhatPrefab == null && basicStrawhatPrefab != null) swordStrawhatPrefab = basicStrawhatPrefab;
        if (bruteStrawhatPrefab == null && fatStrawhatPrefab != null) bruteStrawhatPrefab = fatStrawhatPrefab;
        if (staffStrawhatPrefab == null && femaleStrawhatPrefab != null) staffStrawhatPrefab = femaleStrawhatPrefab;
        if (teleportStrawhatPrefab == null && swordStrawhatPrefab != null) teleportStrawhatPrefab = swordStrawhatPrefab;
    }

    /// <summary>
    /// Deactivates any legacy test mobs placed directly in the scene hierarchy so they don't wander.
    /// </summary>
    private void CleanOldSceneMobs()
    {
        string[] oldNames = new string[] { "FemaleStrawhat (1)", "FatStrawhat (1)", "BasicStrawhat", "FemaleStrawhat", "FatStrawhat", "Strawhat swordsman" };
        foreach (string n in oldNames)
        {
            GameObject obj = GameObject.Find(n);
            if (obj != null && string.IsNullOrEmpty(obj.scene.name) == false)
            {
                obj.SetActive(false);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this to begin the Dojo challenge.
    /// In test mode it auto-fires after autoStartDelay seconds.
    /// In production, called when dialogue with StrawhatLeaderNPC completes.
    /// </summary>
    public void StartChallenge()
    {
        if (challengeStarted) return;

        challengeStarted = true;
        currentWaveIndex = 0;
        SetGatesActive(true);

        // Regenerate randomized blueprints for a fresh battle composition
        if (randomizeDifficulty)
        {
            BuildWaveBlueprints();
        }

        // Switch background music to Temple Thunder when combat begins
        if (combatMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(combatMusic, fade: true);
            Debug.Log("[DojoWaveManager] ★ Fighting started! Switched BGM to temple-thunder");
        }

        Debug.Log("[DojoWaveManager] ★ CHALLENGE STARTED — Gates locked! Strawhat Clan assault begins!");
    }

    private bool isExitingToSampleScene = false;

    private void CompleteChallenge()
    {
        challengeCompleted = true;
        SetGatesActive(false);
        SpawnRewards();

        // Switch back to ambient BGM on challenge completion
        if (ambientMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(ambientMusic, fade: true);
        }

        // Persist Dojo 1 trial completion
        PlayerPrefs.SetInt("Dojo1_Completed", 1);
        PlayerPrefs.Save();

        // Auto-save current progress
        SaveSlotManager.SaveCurrentGameState("Cherry Blossom - Strawhat Dojo Cleared");

        Debug.Log("[DojoWaveManager] ★ CHALLENGE COMPLETE — Gates opened! Dojo 1 marked completed & auto-saved.");

        // Start post-victory exit sequence
        StartCoroutine(PostVictoryExitRoutine());
    }

    private IEnumerator PostVictoryExitRoutine()
    {
        // Give player 4.0 seconds to see gates open, vacuum/pick up coin and orb rewards
        yield return new WaitForSeconds(4.0f);

        if (isExitingToSampleScene) yield break;
        isExitingToSampleScene = true;

        Debug.Log("[DojoWaveManager] Auto-transitioning to SampleScene in front of Dojo...");
        PlayerSpawnPointManager.targetSpawnPointName = "Dojo1_ExitSpawnPoint";
        PlayerSpawnPointManager.lastUsedSpawnPointName = "Dojo1_ExitSpawnPoint";
        ArcaneLoadingScreen.LoadScene("SampleScene");
    }

    // ══════════════════════════════════════════════════════════════════
    //  DYNAMIC WAVE GENERATION (INCREASING DIFFICULTY)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds escalating wave blueprints with randomized enemy compositions.
    /// Escalation:
    /// - Wave 1 (Easy / Tier 1): 2 Light Mobs (Teleport & Katana Swordsman)
    /// - Wave 2 (Moderate / Tier 2): 3 Mixed Fighters (Sword, Staff, Teleport)
    /// - Wave 3 (Intense / Tier 3): 4 Elite Incursion (Guaranteed Brute or Staff + Swordsmen)
    /// - Wave 4 (Climax / Boss Wave): 5 Devastating Assault (Brute Tank Duo + Support)
    /// </summary>
    public void BuildWaveBlueprints()
    {
        waveBlueprints = new List<List<SpawnEntry>>();

        EnsurePrefabsLoaded();

        GameObject teleport = teleportStrawhatPrefab;
        GameObject sword = swordStrawhatPrefab != null ? swordStrawhatPrefab : teleport;
        GameObject staff = staffStrawhatPrefab != null ? staffStrawhatPrefab : sword;
        GameObject brute = bruteStrawhatPrefab != null ? bruteStrawhatPrefab : staff;

        int numSpawnPoints = (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints.Length : 3;

        // ── WAVE 1: Initiation / Scout Flank (2 Light Mobs) ──
        // Tier 1 pool: Teleport, Sword
        List<SpawnEntry> wave1 = new List<SpawnEntry>();
        int count1 = 2;
        int[] flankPoints1 = new int[] { 0, numSpawnPoints - 1 };
        for (int i = 0; i < count1; i++)
        {
            GameObject pick = (Random.value < 0.5f) ? teleport : sword;
            int pt = (i < flankPoints1.Length) ? flankPoints1[i] : Random.Range(0, numSpawnPoints);
            wave1.Add(new SpawnEntry { prefab = pick, pointIndex = pt, mobName = pick.name });
        }
        waveBlueprints.Add(wave1);

        // ── WAVE 2: Vanguard Pressure (3 Mixed Combatants) ──
        // Tier 2 pool: Teleport, Sword, Staff
        List<SpawnEntry> wave2 = new List<SpawnEntry>();
        int count2 = 3;
        GameObject[] tier2Pool = new GameObject[] { teleport, sword, staff };
        for (int i = 0; i < count2; i++)
        {
            GameObject pick;
            if (i == 0) pick = staff; // Guarantee at least 1 staff caster
            else pick = tier2Pool[Random.Range(0, tier2Pool.Length)];

            int pt = i % numSpawnPoints;
            wave2.Add(new SpawnEntry { prefab = pick, pointIndex = pt, mobName = pick.name });
        }
        ShuffleEntries(wave2);
        waveBlueprints.Add(wave2);

        // ── WAVE 3: Elite Incursion (4 Heavy Pressure) ──
        // Tier 3 pool: Guaranteed Brute + mixed Staff/Sword/Teleport
        List<SpawnEntry> wave3 = new List<SpawnEntry>();
        int count3 = 4;
        wave3.Add(new SpawnEntry { prefab = brute, pointIndex = 1 % numSpawnPoints, mobName = brute.name }); // Center Brute
        for (int i = 1; i < count3; i++)
        {
            GameObject pick;
            float roll = Random.value;
            if (roll < 0.45f) pick = sword;
            else if (roll < 0.75f) pick = staff;
            else pick = teleport;

            int pt = Random.Range(0, numSpawnPoints);
            wave3.Add(new SpawnEntry { prefab = pick, pointIndex = pt, mobName = pick.name });
        }
        ShuffleEntries(wave3);
        waveBlueprints.Add(wave3);

        // ── WAVE 4: Climax Boss Wave (5 Clan Champions) ──
        // Climax composition: 2 Brutes + 1 Staff + 1 Sword + 1 Teleport/Sword
        List<SpawnEntry> wave4 = new List<SpawnEntry>();
        wave4.Add(new SpawnEntry { prefab = brute, pointIndex = 0, mobName = brute.name });
        wave4.Add(new SpawnEntry { prefab = brute, pointIndex = numSpawnPoints - 1, mobName = brute.name });
        wave4.Add(new SpawnEntry { prefab = staff, pointIndex = 1 % numSpawnPoints, mobName = staff.name });
        wave4.Add(new SpawnEntry { prefab = sword, pointIndex = 0, mobName = sword.name });
        wave4.Add(new SpawnEntry { prefab = (Random.value < 0.5f ? teleport : sword), pointIndex = numSpawnPoints - 1, mobName = "Support" });
        waveBlueprints.Add(wave4);

        Debug.Log($"[DojoWaveManager] Built {waveBlueprints.Count} escalating difficulty waves with new Strawhat Clan roster.");
    }

    private void ShuffleEntries(List<SpawnEntry> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            SpawnEntry temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  WAVE FLOW
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator AdvanceWave()
    {
        isSpawningWave = true;

        if (currentWaveIndex >= waveBlueprints.Count)
        {
            CompleteChallenge();
            isSpawningWave = false;
            yield break;
        }

        // Brief breathing room between waves
        if (currentWaveIndex > 0)
        {
            yield return new WaitForSeconds(betweenWaveDelay);
        }

        int waveNumber = currentWaveIndex + 1;
        Debug.Log($"[DojoWaveManager] ── WAVE {waveNumber} / {waveBlueprints.Count} ──");

        List<SpawnEntry> wave = waveBlueprints[currentWaveIndex];
        for (int i = 0; i < wave.Count; i++)
        {
            SpawnEntry entry = wave[i];
            if (entry.prefab == null)
            {
                Debug.LogWarning($"[DojoWaveManager] Wave {waveNumber} slot {i}: prefab is NULL! Skipping.");
                continue;
            }

            Transform point = GetSpawnPoint(entry.pointIndex);
            Vector3 spawnPos = point.position;

            GameObject enemy = Instantiate(entry.prefab, spawnPos, Quaternion.identity);
            enemy.SetActive(true);
            activeEnemies.Add(enemy);

            // Procedural entrance effect
            EnemySpawnFX spawnFX = enemy.GetComponent<EnemySpawnFX>();
            if (spawnFX == null)
            {
                spawnFX = enemy.AddComponent<EnemySpawnFX>();
                spawnFX.spawnStyle = EnemySpawnFX.SpawnStyle.NinjaSmokeDrop;
            }

            // Immediately target and engage the player
            ForceAggroOnPlayer(enemy);

            Debug.Log($"[DojoWaveManager] Wave {waveNumber}: Spawned '{enemy.name}' at {point.name}");

            if (i < wave.Count - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }

        currentWaveIndex++;
        isSpawningWave = false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  AGGRO DISPATCHER
    // ══════════════════════════════════════════════════════════════════

    private void ForceAggroOnPlayer(GameObject enemy)
    {
        if (enemy == null) return;

        // 1. Strawhat Katana Swordsman
        StrawhatSwordAI swordAI = enemy.GetComponent<StrawhatSwordAI>();
        if (swordAI != null)
        {
            swordAI.detectionRange = aggroDetectionOverride;
            return;
        }

        // 2. Strawhat Shadow Brute
        StrawhatBruteAI bruteAI = enemy.GetComponent<StrawhatBruteAI>();
        if (bruteAI != null)
        {
            bruteAI.detectionRange = aggroDetectionOverride;
            return;
        }

        // 3. Strawhat Staff Runner
        StrawhatStaffAI staffAI = enemy.GetComponent<StrawhatStaffAI>();
        if (staffAI != null)
        {
            staffAI.detectionRange = aggroDetectionOverride;
            return;
        }

        // 4. Strawhat Teleport Assassin
        StrawhatTeleportAI teleportAI = enemy.GetComponent<StrawhatTeleportAI>();
        if (teleportAI != null)
        {
            teleportAI.detectionRange = aggroDetectionOverride;
            return;
        }

        // Legacy fallbacks
        UniversalEnemy universal = enemy.GetComponent<UniversalEnemy>();
        if (universal != null)
        {
            universal.detectionRange = aggroDetectionOverride;
            universal.standStillUntilSpotted = false;
            universal.currentState = UniversalEnemy.EnemyState.Chasing;
            return;
        }

        FatStrawhatAI fatAI = enemy.GetComponent<FatStrawhatAI>();
        if (fatAI != null)
        {
            fatAI.detectionRange = aggroDetectionOverride;
            fatAI.currentState = FatStrawhatAI.State.Chasing;
            return;
        }

        FemaleStrawhatAI femaleAI = enemy.GetComponent<FemaleStrawhatAI>();
        if (femaleAI != null)
        {
            femaleAI.detectionRange = aggroDetectionOverride;
            femaleAI.currentState = FemaleStrawhatAI.State.Chasing;
            return;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  GATES & REWARDS
    // ══════════════════════════════════════════════════════════════════

    private void SetGatesActive(bool active)
    {
        if (dojoGates == null) return;
        foreach (GameObject gate in dojoGates)
        {
            if (gate == null) continue;

            DojoGateController ctrl = gate.GetComponent<DojoGateController>();
            if (ctrl != null)
            {
                if (active) ctrl.CloseGate();
                else ctrl.OpenGate();
            }
            else
            {
                gate.SetActive(active);
            }
        }
    }

    private void SpawnRewards()
    {
        if (coinPrefab == null) return;
        Vector3 loc = rewardSpawnPoint != null ? rewardSpawnPoint.position : transform.position;
        for (int i = 0; i < coinRewardCount; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.2f, 1f), 0f);
            Instantiate(coinPrefab, loc + offset, Quaternion.identity);
        }
    }

    private Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return transform;
        int clamped = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        return spawnPoints[clamped] != null ? spawnPoints[clamped] : transform;
    }
}
