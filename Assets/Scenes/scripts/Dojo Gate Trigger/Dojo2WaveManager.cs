using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// Dojo2WaveManager - Spawns Samurai Clan members across 5 escalating waves in Dojo 2.
/// Spawns all enemies directly from the mobspawner points in the arena.
/// Clones live scene objects to preserve custom mob colors, sizes, and scale.
/// Starts immediately upon arriving in the Dojo 2 arena with 0s delay.
///
/// Wave 1: 1 Normal Male Samurai + 1 Normal Female Samurai (from mobspawner 0 & 2)
/// Wave 2: 2 Normal Male Samurai + 1 Normal Female Samurai (from mobspawners 0, 1, 2)
/// Wave 3: 1 Normal Female Samurai + 1 Fat Kabuto + 1 Normal Male Samurai (FatKabuto from FatKabutoOnlySpawner)
/// Wave 4: 3 Health Orbs (from mobspawners 0, 1, 2)
/// Wave 5: 2 Normal Female Samurai + 2 Normal Male Samurai + 1 Fat Kabuto (Boss Round across all mobspawners)
/// </summary>
public class Dojo2WaveManager : MonoBehaviour
{
    // ── Singleton Instance ───────────────────────────────────────────
    public static Dojo2WaveManager Instance { get; private set; }

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
    [Tooltip("Seconds after entering scene before Wave 1 begins (0 for immediate).")]
    public float autoStartDelay = 0.0f;
    public float betweenWaveDelay = 2.0f;
    public float spawnStagger = 0.35f;

    [Header("Rewards")]
    public GameObject coinPrefab;
    public Transform rewardSpawnPoint;
    public int coinRewardCount = 15;

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
        Instance = this;
    }

    void Start()
    {
        // 1. Auto-discover mobspawners if unassigned
        ResolveMobSpawners();

        // 2. Auto-load audio clips
        if (combatMusic == null)
        {
            combatMusic = Resources.Load<AudioClip>("Audio/temple-thunder (2)") ?? Resources.Load<AudioClip>("Audio/temple-thunder");
            if (combatMusic == null) combatMusic = Resources.Load<AudioClip>("temple-thunder (2)") ?? Resources.Load<AudioClip>("temple-thunder");
        }
        if (ambientMusic == null)
        {
            ambientMusic = Resources.Load<AudioClip>("Audio/bamboo-incense");
            if (ambientMusic == null) ambientMusic = Resources.Load<AudioClip>("bamboo-incense");
        }

        // 3. Resolve live scene objects so custom scale and color tints are preserved
        ResolveSceneObject(ref normalMaleSamuraiPrefab, "NormalMaleSamurai");
        ResolveSceneObject(ref normalFemaleSamuraiPrefab, "NormalFemaleSamurai");
        ResolveSceneObject(ref fatKabutoPrefab, "FatKabuto");

        // 4. Hide pre-placed scene mobs so arena is clean for wave spawning
        HidePrePlacedSceneMobs();

        SetGatesActive(false);
        BuildWaveBlueprints();

        // 5. Start fight immediately
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == "Dojo2Scene" || autoStartDelay <= 0f)
        {
            StartChallenge();
        }
        else
        {
            Invoke(nameof(AutoStartChallengeIfUnstarted), autoStartDelay);
        }
    }

    private void ResolveMobSpawners()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || HasNullInArray(spawnPoints))
        {
            List<Transform> list = new List<Transform>();
            string[] spawnerNames = new string[] { "mobspawner", "mobspawner (1)", "mobspawner (2)", "MobSpawner 1", "MobSpawner 2", "MobSpawner 3" };
            foreach (string sName in spawnerNames)
            {
                GameObject sObj = GameObject.Find(sName);
                if (sObj != null && !list.Contains(sObj.transform))
                {
                    list.Add(sObj.transform);
                }
            }
            if (list.Count > 0)
            {
                spawnPoints = list.ToArray();
                Debug.Log($"[Dojo2WaveManager] Auto-discovered {spawnPoints.Length} mobspawners in scene.");
            }
        }

        if (fatKabutoSpawnPoint == null)
        {
            GameObject fk = GameObject.Find("FatKabutoOnlySpawner") ?? GameObject.Find("FatKabutoSpawner");
            if (fk != null)
            {
                fatKabutoSpawnPoint = fk.transform;
            }
            else if (spawnPoints != null && spawnPoints.Length > 0)
            {
                fatKabutoSpawnPoint = spawnPoints[0];
            }
        }
    }

    private bool HasNullInArray(Transform[] arr)
    {
        if (arr == null || arr.Length == 0) return true;
        foreach (var t in arr) if (t == null) return true;
        return false;
    }

    private void AutoStartChallengeIfUnstarted()
    {
        if (!challengeStarted && !challengeCompleted)
        {
            Debug.Log("[Dojo2WaveManager] Auto-starting Dojo 2 challenge fight.");
            StartChallenge();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !challengeStarted && !challengeCompleted)
        {
            Debug.Log("[Dojo2WaveManager] Player entered Dojo 2 arena trigger -> Starting challenge immediately!");
            StartChallenge();
        }
    }

    public void StartChallenge()
    {
        if (challengeStarted || challengeCompleted) return;

        challengeStarted = true;
        currentWaveIndex = 0;

        if (combatMusic == null)
        {
            combatMusic = Resources.Load<AudioClip>("Audio/temple-thunder (2)") ?? Resources.Load<AudioClip>("Audio/temple-thunder");
        }

        SetGatesActive(true);

        // Switch to combat music
        if (combatMusic != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(combatMusic, fade: true);
            }
            else
            {
                PlayMusic(combatMusic);
            }
            Debug.Log("[Dojo2WaveManager] ★ Fighting started immediately! Switched BGM to temple-thunder (2)");
        }

        StartCoroutine(SpawnWaveRoutine(currentWaveIndex));
    }

    private IEnumerator SpawnWaveRoutine(int waveIndex)
    {
        isSpawningWave = true;
        activeEnemies.Clear();

        if (waveBlueprints == null || waveIndex >= waveBlueprints.Count)
        {
            CompleteChallenge();
            isSpawningWave = false;
            yield break;
        }

        List<SpawnEntry> currentWave = waveBlueprints[waveIndex];
        Debug.Log($"[Dojo2WaveManager] Starting Wave {waveIndex + 1}/{waveBlueprints.Count} ({currentWave.Count} entities spawning from mobspawners)");

        for (int i = 0; i < currentWave.Count; i++)
        {
            SpawnEntry entry = currentWave[i];
            if (entry.prefab != null)
            {
                Transform targetSpawn = GetSpawnPoint(entry.pointIndex);
                Vector3 spawnPos = targetSpawn != null ? targetSpawn.position : transform.position;

                // Spawn entity directly at the mobspawner position
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

                // Attach spawn effect FX for smoke drop from mobspawner
                EnemySpawnFX spawnFX = spawned.GetComponent<EnemySpawnFX>();
                if (spawnFX == null)
                {
                    spawnFX = spawned.AddComponent<EnemySpawnFX>();
                    spawnFX.spawnStyle = EnemySpawnFX.SpawnStyle.NinjaSmokeDrop;
                }

                // Force aggro towards player immediately
                ForceAggroOnPlayer(spawned);

                activeEnemies.Add(spawned);
                Debug.Log($"[Dojo2WaveManager] Spawned '{spawned.name}' from mobspawner '{(targetSpawn != null ? targetSpawn.name : "Manager")}' at {spawnPos}");
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
        Debug.Log("[Dojo2WaveManager] ★ Dojo 2 Challenge Completed! Victory!");

        SetGatesActive(false);

        // Switch back to ambient music
        if (ambientMusic != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(ambientMusic, fade: true);
            }
            else
            {
                PlayMusic(ambientMusic);
            }
        }

        // Spawn Coin Reward Cluster
        Vector3 rewardPos = rewardSpawnPoint != null ? rewardSpawnPoint.position : transform.position;
        if (coinPrefab != null)
        {
            for (int i = 0; i < coinRewardCount; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.2f, 1f), 0f);
                Instantiate(coinPrefab, rewardPos + offset, Quaternion.identity);
            }
        }
        else
        {
            OrbSpawner.SpawnLootCluster(rewardPos, coinRewardCount);
        }

        // Trigger player level progression EXP reward
        if (PlayerLevelSystem.Instance != null)
        {
            PlayerLevelSystem.Instance.AddExperience(150);
        }
    }

    private void BuildWaveBlueprints()
    {
        waveBlueprints = new List<List<SpawnEntry>>();

        // Wave 1: 1 Normal Male (mobspawner 0) + 1 Normal Female (mobspawner 2)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 2 }
        });

        // Wave 2: 2 Normal Male (mobspawner 0 & 1) + 1 Normal Female (mobspawner 2)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 1 },
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 2 }
        });

        // Wave 3: 1 Normal Female (mobspawner 0) + 1 Fat Kabuto (FatKabutoOnlySpawner) + 1 Normal Male (mobspawner 2)
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = normalFemaleSamuraiPrefab, pointIndex = 0 },
            new SpawnEntry { prefab = fatKabutoPrefab, pointIndex = 3 },
            new SpawnEntry { prefab = normalMaleSamuraiPrefab, pointIndex = 2 }
        });

        // Wave 4: 3 Health Orbs (from mobspawners 0, 1, 2)
        GameObject orbTemplate = healthOrbPrefab != null ? healthOrbPrefab : normalMaleSamuraiPrefab;
        waveBlueprints.Add(new List<SpawnEntry>
        {
            new SpawnEntry { prefab = orbTemplate, pointIndex = 0, isHealthOrb = true },
            new SpawnEntry { prefab = orbTemplate, pointIndex = 1, isHealthOrb = true },
            new SpawnEntry { prefab = orbTemplate, pointIndex = 2, isHealthOrb = true }
        });

        // Wave 5: 2 Normal Female + 2 Normal Male + 1 Fat Kabuto (Boss Round across all mobspawners)
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
            if (found == null)
            {
                GameObject[] all = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var obj in all)
                {
                    if (obj.name.ToLower().Contains(label.ToLower()))
                    {
                        found = obj;
                        break;
                    }
                }
            }
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
            return spawnPoints[safeIndex] != null ? spawnPoints[safeIndex] : transform;
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

        UniversalEnemy universal = enemy.GetComponent<UniversalEnemy>();
        if (universal != null)
        {
            universal.detectionRange = 50f;
            universal.standStillUntilSpotted = false;
            universal.currentState = UniversalEnemy.EnemyState.Chasing;
        }

        FemaleSamuraiWhipAI samuraiWhipAI = enemy.GetComponent<FemaleSamuraiWhipAI>();
        if (samuraiWhipAI != null)
        {
            samuraiWhipAI.detectionRange = 50f;
            samuraiWhipAI.currentState = FemaleSamuraiWhipAI.State.Chasing;
        }
    }

    private void SetGatesActive(bool active)
    {
        if (dojoGates != null)
        {
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
