using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DojoWaveManager : MonoBehaviour
{
    [System.Serializable]
    public struct EnemySpawnConfig
    {
        public GameObject enemyPrefab;
        public int spawnPointIndex;
    }

    [System.Serializable]
    public struct DojoWave
    {
        public string waveName;
        public float startDelay;
        public List<EnemySpawnConfig> enemies;
    }

    [Header("Wave Definition")]
    public List<DojoWave> waves;
    
    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    [Header("Gates & Obstacles")]
    [Tooltip("GameObjects to activate to lock the player inside (e.g., doors)")]
    public GameObject[] dojoGates;

    [Header("Rewards")]
    public GameObject coinPrefab;
    public Transform rewardSpawnPoint;
    public int coinRewardCount = 5;

    private int currentWaveIndex = 0;
    private bool challengeStarted = false;
    private bool challengeCompleted = false;
    private bool isSpawningWave = false;
    private List<GameObject> activeEnemies = new List<GameObject>();

    public bool IsChallengeStarted => challengeStarted;
    public bool IsChallengeCompleted => challengeCompleted;

    void Awake()
    {
        // Ensure gates are initially open/inactive
        SetGatesActive(false);
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        if (!challengeStarted || challengeCompleted || isSpawningWave) return;

        // Clean up destroyed enemies from the list
        activeEnemies.RemoveAll(item => item == null);

        // If no active enemies are left, advance to next wave
        if (activeEnemies.Count == 0)
        {
            StartCoroutine(AdvanceWave());
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !challengeStarted && !challengeCompleted)
        {
            StartChallenge();
        }
    }

    private void StartChallenge()
    {
        challengeStarted = true;
        SetGatesActive(true);
        currentWaveIndex = 0;
        Debug.Log("Dojo challenge started! Gates locked.");
    }

    private IEnumerator AdvanceWave()
    {
        isSpawningWave = true;

        if (currentWaveIndex < waves.Count)
        {
            DojoWave wave = waves[currentWaveIndex];
            Debug.Log("Spawning wave: " + wave.waveName);
            
            yield return new WaitForSeconds(wave.startDelay);

            foreach (var spawnConfig in wave.enemies)
            {
                if (spawnConfig.enemyPrefab == null) continue;
                
                Transform spawnPoint = this.transform;
                if (spawnPoints != null && spawnConfig.spawnPointIndex >= 0 && spawnConfig.spawnPointIndex < spawnPoints.Length)
                {
                    spawnPoint = spawnPoints[spawnConfig.spawnPointIndex];
                }

                GameObject enemyObj = Instantiate(spawnConfig.enemyPrefab, spawnPoint.position, Quaternion.identity);
                
                // Set player target reference on EnemyPatrol2D if applicable
                EnemyPatrol2D patrolScript = enemyObj.GetComponent<EnemyPatrol2D>();
                if (patrolScript != null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        patrolScript.TargetA = player.transform;
                    }
                }

                activeEnemies.Add(enemyObj);
            }

            currentWaveIndex++;
        }
        else
        {
            CompleteChallenge();
        }

        isSpawningWave = false;
    }

    private void CompleteChallenge()
    {
        challengeCompleted = true;
        SetGatesActive(false);
        SpawnRewards();
        Debug.Log("Dojo challenge completed! Gates opened, rewards spawned.");
    }

    private void SetGatesActive(bool active)
    {
        if (dojoGates == null) return;
        foreach (GameObject gate in dojoGates)
        {
            if (gate != null)
            {
                gate.SetActive(active);
            }
        }
    }

    private void SpawnRewards()
    {
        if (coinPrefab == null) return;
        
        Vector3 spawnLoc = rewardSpawnPoint != null ? rewardSpawnPoint.position : transform.position;
        for (int i = 0; i < coinRewardCount; i++)
        {
            // Add a slight random offset so they spread out
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.5f), 0f);
            Instantiate(coinPrefab, spawnLoc + offset, Quaternion.identity);
        }
    }
}
