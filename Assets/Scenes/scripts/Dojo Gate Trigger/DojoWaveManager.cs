using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dojo 1 Wave Manager — spawns Strawhat clan members across 4 escalating waves.
///
/// TEST MODE: Waves auto-start 5 seconds after scene load.
/// PRODUCTION: Call StartChallenge() from your dialogue callback to begin manually.
///
/// Wave 1: 2 Basic Strawhats
/// Wave 2: 2 Fat Strawhats
/// Wave 3: 3 Basic Strawhats
/// Wave 4: 1 Female Strawhat + 1 Fat Strawhat (boss round)
/// </summary>
public class DojoWaveManager : MonoBehaviour
{
    // ── Prefab Slots ─────────────────────────────────────────────────
    [Header("Enemy Prefabs")]
    [Tooltip("Basic strawhat swordsman prefab.")]
    public GameObject basicStrawhatPrefab;

    [Tooltip("Fat strawhat heavy-hitter prefab.")]
    public GameObject fatStrawhatPrefab;

    [Tooltip("Female strawhat phantom-dash fighter prefab.")]
    public GameObject femaleStrawhatPrefab;

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

    // ── Runtime ───────────────────────────────────────────────────────
    private int currentWaveIndex = 0;
    private bool challengeStarted = false;
    private bool challengeCompleted = false;
    private bool isSpawningWave = false;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    public bool IsChallengeStarted => challengeStarted;
    public bool IsChallengeCompleted => challengeCompleted;

    private struct SpawnEntry
    {
        public GameObject prefab;
        public int pointIndex;
    }

    private List<List<SpawnEntry>> waveBlueprints;

    public static DojoWaveManager Instance { get; private set; }

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Clone scene-placed enemies into hidden templates so they survive destruction
        ResolveSceneObject(ref basicStrawhatPrefab, "Basic");
        ResolveSceneObject(ref fatStrawhatPrefab, "Fat");
        ResolveSceneObject(ref femaleStrawhatPrefab, "Female");

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

        // ── TEST MODE: auto-start after delay (bypassed when StrawhatLeaderNPC handles dialogue flow) ──
        StartCoroutine(AutoStartAfterDelay());

        Debug.Log($"[DojoWaveManager] Initialized.");
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
    //  PUBLIC API — call from dialogue callback later
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this to begin the Dojo challenge.
    /// In test mode it auto-fires after autoStartDelay seconds.
    /// In production, call from your dialogue-end callback.
    /// </summary>
    public void StartChallenge()
    {
        if (challengeStarted) return;

        challengeStarted = true;
        currentWaveIndex = 0;
        SetGatesActive(true);

        // Switch background music to Temple Thunder when combat begins
        if (combatMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(combatMusic, fade: true);
            Debug.Log("[DojoWaveManager] ★ Fighting started! Switched BGM to temple-thunder");
        }

        Debug.Log("[DojoWaveManager] ★ CHALLENGE STARTED — Gates locked!");
    }

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

        Debug.Log("[DojoWaveManager] ★ CHALLENGE COMPLETE — Gates opened!");
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

        // Brief pause between waves
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
            enemy.SetActive(true); // ensure it's active (template clones are inactive)
            activeEnemies.Add(enemy);

            // Ensure EnemySpawnFX is present to run procedural entrance
            EnemySpawnFX spawnFX = enemy.GetComponent<EnemySpawnFX>();
            if (spawnFX == null)
            {
                spawnFX = enemy.AddComponent<EnemySpawnFX>();
                spawnFX.spawnStyle = EnemySpawnFX.SpawnStyle.NinjaSmokeDrop;
            }

            // Force aggro immediately
            ForceAggroOnPlayer(enemy);

            Debug.Log($"[DojoWaveManager] Spawned '{enemy.name}' at {point.name}");

            if (i < wave.Count - 1)
            {
                yield return new WaitForSeconds(spawnStagger);
            }
        }

        currentWaveIndex++;
        isSpawningWave = false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  AGGRO — Force enemies to chase the player immediately
    // ══════════════════════════════════════════════════════════════════

    private void ForceAggroOnPlayer(GameObject enemy)
    {
        if (enemy == null) return;

        // UniversalEnemy (basic strawhat)
        UniversalEnemy universal = enemy.GetComponent<UniversalEnemy>();
        if (universal != null)
        {
            universal.detectionRange = aggroDetectionOverride;
            universal.standStillUntilSpotted = false;
            universal.currentState = UniversalEnemy.EnemyState.Chasing;
        }

        // FatStrawhatAI
        FatStrawhatAI fatAI = enemy.GetComponent<FatStrawhatAI>();
        if (fatAI != null)
        {
            fatAI.detectionRange = aggroDetectionOverride;
            fatAI.currentState = FatStrawhatAI.State.Chasing;
        }

        // FemaleStrawhatAI
        FemaleStrawhatAI femaleAI = enemy.GetComponent<FemaleStrawhatAI>();
        if (femaleAI != null)
        {
            femaleAI.detectionRange = aggroDetectionOverride;
            femaleAI.currentState = FemaleStrawhatAI.State.Chasing;
        }

        // FemaleSamuraiWhipAI
        FemaleSamuraiWhipAI samuraiWhipAI = enemy.GetComponent<FemaleSamuraiWhipAI>();
        if (samuraiWhipAI != null)
        {
            samuraiWhipAI.detectionRange = aggroDetectionOverride;
            samuraiWhipAI.currentState = FemaleSamuraiWhipAI.State.Chasing;
        }

        // NormalMaleSamuraiAI
        NormalMaleSamuraiAI maleSamuraiAI = enemy.GetComponent<NormalMaleSamuraiAI>();
        if (maleSamuraiAI != null)
        {
            maleSamuraiAI.detectionRange = aggroDetectionOverride;
            maleSamuraiAI.currentState = NormalMaleSamuraiAI.State.Chasing;
        }

        // NormalFemaleSamuraiAI
        NormalFemaleSamuraiAI femaleSamuraiAI = enemy.GetComponent<NormalFemaleSamuraiAI>();
        if (femaleSamuraiAI != null)
        {
            femaleSamuraiAI.detectionRange = aggroDetectionOverride;
            femaleSamuraiAI.currentState = NormalFemaleSamuraiAI.State.Chasing;
        }

        // FatKabutoAI
        FatKabutoAI fatKabutoAI = enemy.GetComponent<FatKabutoAI>();
        if (fatKabutoAI != null)
        {
            fatKabutoAI.detectionRange = aggroDetectionOverride;
            if (fatKabutoAI.currentState != FatKabutoAI.State.SpawningIn)
            {
                fatKabutoAI.currentState = FatKabutoAI.State.Chasing;
            }
        }

        // EnemyPatrol2D (older script fallback)
        EnemyPatrol2D patrol = enemy.GetComponent<EnemyPatrol2D>();
        if (patrol != null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                patrol.TargetA = playerObj.transform;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  SCENE OBJECT → TEMPLATE CLONING
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// If a prefab slot points to a live scene object instead of a project asset,
    /// clone it as a hidden child template and deactivate the original.
    /// This prevents the reference from breaking when the original is killed.
    /// </summary>
    private void ResolveSceneObject(ref GameObject prefab, string label)
    {
        if (prefab == null) return;

        // Scene objects have a non-null, non-empty scene name
        if (!string.IsNullOrEmpty(prefab.scene.name))
        {
            Debug.Log($"[DojoWaveManager] '{prefab.name}' is a scene object → cloning as template.");

            GameObject template = Instantiate(prefab, this.transform);
            template.name = $"_Template_{label}";
            template.SetActive(false);

            // Hide the original so it doesn't roam the dojo before the fight
            prefab.SetActive(false);

            prefab = template;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  WAVE DEFINITIONS
    // ══════════════════════════════════════════════════════════════════

    private void BuildWaveBlueprints()
    {
        waveBlueprints = new List<List<SpawnEntry>>();

        // Wave 1: 2 Basic Strawhats (warm-up, flanked)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = basicStrawhatPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = basicStrawhatPrefab, pointIndex = 2 },
        });

        // Wave 2: 2 Fat Strawhats (heavy pressure)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = fatStrawhatPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = fatStrawhatPrefab, pointIndex = 1 },
        });

        // Wave 3: 3 Basic Strawhats (mob swarm)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = basicStrawhatPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = basicStrawhatPrefab, pointIndex = 1 },
            new SpawnEntry { prefab = basicStrawhatPrefab, pointIndex = 2 },
        });

        // Wave 4: 1 Female Strawhat + 1 Fat Strawhat (boss duo finale)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = femaleStrawhatPrefab, pointIndex = 1 },
            new SpawnEntry { prefab = fatStrawhatPrefab,    pointIndex = 2 },
        });
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
