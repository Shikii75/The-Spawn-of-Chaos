using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// Dojo2WaveManager - Spawns Samurai Clan members across 5 escalating waves in Dojo 2.
/// Clones live scene objects to preserve the user's custom mob colors, sizes, and scale.
///
/// Wave 1: 1 Normal Male Samurai + 1 Normal Female Samurai
/// Wave 2: 2 Normal Male Samurai + 1 Normal Female Samurai
/// Wave 3: 1 Normal Female Samurai + 1 Fat Kabuto + 1 Normal Male Samurai
/// Wave 4: 3 Health Orbs (NightmareOrbs configured as health-restoring floating orbs)
/// Wave 5: 2 Normal Female Samurai + 2 Normal Male Samurai + 1 Fat Kabuto (Boss Round)
/// </summary>
public class Dojo2WaveManager : MonoBehaviour
{
    // ── Prefab / Scene Object Slots ──────────────────────────────────
    [Header("Enemy Prefabs / Scene Templates")]
    [Tooltip("Normal Male Samurai prefab or scene object reference.")]
    public GameObject normalMaleSamuraiPrefab;

    [Tooltip("Normal Female Samurai prefab or scene object reference.")]
    public GameObject normalFemaleSamuraiPrefab;

    [Tooltip("Fat Kabuto heavy mini-boss prefab or scene object reference.")]
    public GameObject fatKabutoPrefab;

    [Tooltip("Nightmare Orb prefab or template for Wave 4 Health Orbs.")]
    public GameObject healthOrbPrefab;

    // ── Spawn Points ─────────────────────────────────────────────────
    [Header("Spawn Points")]
    [Tooltip("Mob Spawners across the Dojo 2 arena floor.")]
    public Transform[] spawnPoints;

    [Tooltip("Fat Kabuto entrance spawn point.")]
    public Transform fatKabutoSpawnPoint;

    // ── Gates & Audio ────────────────────────────────────────────────
    [Header("Dojo Gates")]
    public GameObject[] dojoGates;

    [Header("Audio Settings")]
    public AudioClip ambientMusic;
    public AudioClip combatMusic;

    // ── Wave Timing & Rewards ────────────────────────────────────────
    [Header("Wave Timing")]
    [Tooltip("Seconds after entering scene before Wave 1 begins.")]
    public float autoStartDelay = 5.0f;
    public float betweenWaveDelay = 2.5f;
    public float spawnStagger = 0.35f;

    [Header("Rewards")]
    public GameObject coinPrefab;
    public Transform rewardSpawnPoint;
    public int coinRewardCount = 12;

    // ── Runtime State ────────────────────────────────────────────────
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
        public bool isHealthOrb;
    }

    private List<List<SpawnEntry>> waveBlueprints;

    void Awake()
    {
        // Enforce: Dojo2WaveManager MUST ONLY run inside Dojo2Scene!
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "Dojo2Scene")
        {
            Debug.Log($"[Dojo2WaveManager] Active scene is '{activeScene}' (not 'Dojo2Scene') → Disabling Dojo2WaveManager in outside scene.");
            this.enabled = false;
            return;
        }
    }

    void Start()
    {
        // Auto-load temple-thunder (2) combat music for Dojo 2
        if (combatMusic == null)
        {
            combatMusic = Resources.Load<AudioClip>("Audio/temple-thunder (2)") ?? Resources.Load<AudioClip>("Audio/temple-thunder");
        }
        if (ambientMusic == null)
        {
            ambientMusic = Resources.Load<AudioClip>("Audio/bamboo-incense");
        }

        // 1. Resolve live scene objects so user's custom scale and color tints are preserved!
        ResolveSceneObject(ref normalMaleSamuraiPrefab, "NormalMaleSamurai");
        ResolveSceneObject(ref normalFemaleSamuraiPrefab, "NormalFemaleSamurai");
        ResolveSceneObject(ref fatKabutoPrefab, "FatKabuto");

        // 2. Hide any remaining pre-placed scene mobs so the arena starts completely empty!
        HidePrePlacedSceneMobs();

        SetGatesActive(false);
        BuildWaveBlueprints();

        // 3. Spawns begin after 5 seconds inside Dojo 2
        Invoke(nameof(AutoStartChallengeIfUnstarted), autoStartDelay);
    }

    private void AutoStartChallengeIfUnstarted()
    {
        if (!challengeStarted && !challengeCompleted)
        {
            Debug.Log("[Dojo2WaveManager] Auto-starting Dojo 2 challenge fight in Dojo2Scene.");
            StartChallenge();
        }
    }

    public void StartChallenge()
    {
        if (!enabled || challengeStarted || challengeCompleted) return;

        challengeStarted = true;
        currentWaveIndex = 0;

        if (combatMusic == null)
        {
            combatMusic = Resources.Load<AudioClip>("Audio/temple-thunder (2)") ?? Resources.Load<AudioClip>("Audio/temple-thunder");
        }

        SetGatesActive(true);
        PlayMusic(combatMusic);

        StartCoroutine(SpawnWaveRoutine(currentWaveIndex));
    }

    private IEnumerator SpawnWaveRoutine(int waveIndex)
    {
        isSpawningWave = true;
        activeEnemies.Clear();

        List<SpawnEntry> currentWave = waveBlueprints[waveIndex];
        Debug.Log($"[Dojo2WaveManager] Starting Wave {waveIndex + 1}/{waveBlueprints.Count} ({currentWave.Count} entities)");

        for (int i = 0; i < currentWave.Count; i++)
        {
            SpawnEntry entry = currentWave[i];
            if (entry.prefab != null)
            {
                Transform targetSpawn = GetSpawnPoint(entry.pointIndex);
                Vector3 spawnPos = targetSpawn != null ? targetSpawn.position : transform.position;

                GameObject spawned = Instantiate(entry.prefab, spawnPos, Quaternion.identity);
                spawned.SetActive(true);

                // Configure health orb if Wave 4
                if (entry.isHealthOrb)
                {
                    NightmareOrbAI orbAI = spawned.GetComponent<NightmareOrbAI>();
                    if (orbAI == null) orbAI = spawned.AddComponent<NightmareOrbAI>();
                    orbAI.isHealthOrbOnly = true;
                    orbAI.healthRestoreAmount = 35;
                }

                // Force aggro towards player
                ForceAggroOnPlayer(spawned);

                activeEnemies.Add(spawned);
            }

            yield return new WaitForSeconds(spawnStagger);
        }

        isSpawningWave = false;
    }

    void Update()
    {
        if (!challengeStarted || challengeCompleted || isSpawningWave) return;

        // Clean null references (defeated mobs)
        activeEnemies.RemoveAll(enemy => enemy == null);

        if (activeEnemies.Count == 0)
        {
            currentWaveIndex++;

            if (currentWaveIndex >= waveBlueprints.Count)
            {
                CompleteChallenge();
            }
            else
            {
                StartCoroutine(NextWaveDelayRoutine());
            }
        }
    }

    private IEnumerator NextWaveDelayRoutine()
    {
        isSpawningWave = true;
        Debug.Log($"[Dojo2WaveManager] Wave cleared! Next wave in {betweenWaveDelay}s...");
        yield return new WaitForSeconds(betweenWaveDelay);
        StartCoroutine(SpawnWaveRoutine(currentWaveIndex));
    }

    private void CompleteChallenge()
    {
        challengeCompleted = true;
        Debug.Log("[Dojo2WaveManager] Dojo 2 Challenge Completed! Victory!");

        SetGatesActive(false);
        PlayMusic(ambientMusic);

        // Spawn Coin Reward Cluster
        Vector3 rewardPos = rewardSpawnPoint != null ? rewardSpawnPoint.position : transform.position;
        OrbSpawner.SpawnLootCluster(rewardPos, coinRewardCount);

        // Trigger player level progression EXP reward
        if (PlayerLevelSystem.Instance != null)
        {
            PlayerLevelSystem.Instance.AddExperience(150);
        }
    }

    private void BuildWaveBlueprints()
    {
        waveBlueprints = new List<List<SpawnEntry>>();

        // Wave 1: 1 Normal Male + 1 Normal Female
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 2 }
        });

        // Wave 2: 2 Normal Male + 1 Normal Female
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 1 },
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 2 }
        });

        // Wave 3: 1 Normal Female + 1 Fat Kabuto + 1 Normal Male
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = fatKabutoPrefab, pointIndex = 3 },
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 2 }
        });

        // Wave 4: 3 Health Orbs (Restores player health on defeat)
        GameObject orbTemplate = healthOrbPrefab != null ? healthOrbPrefab : normalMaleSamuraiPrefab;
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = orbTemplate, pointIndex = 0, isHealthOrb = true },
            new SpawnEntry { prefab = orbTemplate, pointIndex = 1, isHealthOrb = true },
            new SpawnEntry { prefab = orbTemplate, pointIndex = 2, isHealthOrb = true }
        });

        // Wave 5: 2 Normal Female + 2 Normal Male + 1 Fat Kabuto (Boss Round)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 1 },
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 2 },
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = fatKabutoPrefab, pointIndex = 3 }
        });
    }

    private void ResolveSceneObject(ref GameObject prefab, string label)
    {
        if (prefab == null)
        {
            GameObject found = GameObject.Find(label) ?? GameObject.Find(label + "(Clone)");
            if (found != null) prefab = found;
        }

        if (prefab == null) return;

        if (!string.IsNullOrEmpty(prefab.scene.name))
        {
            Debug.Log($"[Dojo2WaveManager] '{prefab.name}' scene instance found → cloning template preserving size & color.");
            GameObject template = Instantiate(prefab, this.transform);
            template.name = $"_Template_{label}";
            template.SetActive(false);

            prefab.SetActive(false);
            prefab = template;
        }
    }

    private void HidePrePlacedSceneMobs()
    {
        string[] mobNames = new string[] { "NormalMaleSamurai", "NormalFemaleSamurai", "FatKabuto", "normalmalesamurai", "normalfemalesamurai", "fatkabuto" };
        foreach (string name in mobNames)
        {
            GameObject[] foundList = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (GameObject obj in foundList)
            {
                if (obj == null || obj == this.gameObject || obj.transform.IsChildOf(this.transform)) continue;
                if (obj.name.StartsWith("_Template_")) continue;

                if (obj.name.Contains(name) || obj.GetComponent<NormalMaleSamuraiAI>() != null || obj.GetComponent<NormalFemaleSamuraiAI>() != null || obj.GetComponent<FatKabutoAI>() != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }

    private Transform GetSpawnPoint(int index)
    {
        if (index == 3 && fatKabutoSpawnPoint != null) return fatKabutoSpawnPoint;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int safeIndex = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            return spawnPoints[safeIndex];
        }
        return transform;
    }

    private void ForceAggroOnPlayer(GameObject enemy)
    {
        if (enemy == null) return;

        NormalMaleSamuraiAI maleAI = enemy.GetComponent<NormalMaleSamuraiAI>();
        if (maleAI != null)
        {
            maleAI.detectionRange = 50f;
            maleAI.currentState = NormalMaleSamuraiAI.State.Chasing;
        }

        NormalFemaleSamuraiAI femaleAI = enemy.GetComponent<NormalFemaleSamuraiAI>();
        if (femaleAI != null)
        {
            femaleAI.detectionRange = 50f;
            femaleAI.currentState = NormalFemaleSamuraiAI.State.Chasing;
        }

        FatKabutoAI fatAI = enemy.GetComponent<FatKabutoAI>();
        if (fatAI != null)
        {
            fatAI.detectionRange = 50f;
            if (fatAI.currentState != FatKabutoAI.State.SpawningIn)
            {
                fatAI.currentState = FatKabutoAI.State.Chasing;
            }
        }
    }

    private void SetGatesActive(bool active)
    {
        if (dojoGates != null)
        {
            foreach (GameObject gate in dojoGates)
            {
                if (gate != null) gate.SetActive(active);
            }
        }
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }
}
