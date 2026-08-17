using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// TutorialCaveWaveManager - Manages the 3 combat wave trials inside the Tutorial Cavern Arena.
/// Stage 1: Melee 2-hit combo (J key) vs Training Samurai Mobs.
/// Stage 2: Unlocks Magic Projectiles (K key) vs flying Nightmare Orbs.
/// Stage 3: Shift Dash invulnerability (LeftShift) vs hazard barriers & Samurai Mobs.
/// Opens cavern exit gate leading to High Mountain Peak Nyxaris Shrine upon completion.
/// </summary>
public class TutorialCaveWaveManager : MonoBehaviour
{
    public static TutorialCaveWaveManager Instance { get; private set; }

    [Header("Wave Progression State")]
    public int currentWave = 0; // 0 = Inactive, 1 = Melee, 2 = Magic, 3 = Dash, 4 = Complete
    public bool isArenaActive = false;

    [Header("Spawn Points & Portals")]
    public Transform waveSpawnPointLeft;
    public Transform waveSpawnPointRight;
    public GameObject cavernExitGate;
    public GameObject magicPedestalFX;

    [Header("Tutorial UI Banners")]
    public string wave1Message = "⚔️ COMBAT TRIAL 1: Press 'J' for Melee Slash! Tap 'J' twice for 2-Hit Combo!";
    public string wave2Message = "✨ MAGIC UNLOCKED! Press 'K' to fire Magic Projectiles at flying targets!";
    public string wave3Message = "⚡ DASH DODGE: Press 'Shift' while moving for Invulnerable Dash!";

    private List<GameObject> activeWaveMobs = new List<GameObject>();
    private bool waveTransitioning = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (cavernExitGate != null)
        {
            cavernExitGate.SetActive(true); // Lock exit until waves complete
        }
    }

    public void TriggerCavernArena()
    {
        if (isArenaActive || currentWave > 0) return;
        isArenaActive = true;
        StartCoroutine(StartWave1_Melee());
    }

    void Update()
    {
        if (!isArenaActive || waveTransitioning) return;

        // Check if all active mobs in wave are defeated
        activeWaveMobs.RemoveAll(mob => mob == null || !mob.activeInHierarchy);

        if (activeWaveMobs.Count == 0)
        {
            if (currentWave == 1)
            {
                StartCoroutine(TransitionToWave2_Magic());
            }
            else if (currentWave == 2)
            {
                StartCoroutine(TransitionToWave3_Dash());
            }
            else if (currentWave == 3)
            {
                StartCoroutine(CompleteCavernArena());
            }
        }
    }

    private IEnumerator StartWave1_Melee()
    {
        waveTransitioning = true;
        currentWave = 1;
        ShowTutorialBanner(wave1Message);
        yield return new WaitForSeconds(1.5f);

        // Spawn 2 Samurai Mobs
        Vector3 posL = waveSpawnPointLeft != null ? waveSpawnPointLeft.position : transform.position + new Vector3(-4f, 0f, 0f);
        Vector3 posR = waveSpawnPointRight != null ? waveSpawnPointRight.position : transform.position + new Vector3(4f, 0f, 0f);

        GameObject mob1 = SpawnSamuraiMob("TutorialSamurai_1", posL);
        GameObject mob2 = SpawnSamuraiMob("TutorialSamurai_2", posR);

        if (mob1 != null) activeWaveMobs.Add(mob1);
        if (mob2 != null) activeWaveMobs.Add(mob2);

        waveTransitioning = false;
    }

    private IEnumerator TransitionToWave2_Magic()
    {
        waveTransitioning = true;
        currentWave = 2;

        // Unlock Magic Projectile capability on MageCombat
        if (MageCombat.Instance != null)
        {
            MageCombat.Instance.UnlockProjectile();
        }

        if (magicPedestalFX != null)
        {
            magicPedestalFX.SetActive(true);
        }

        ShowTutorialBanner(wave2Message);
        yield return new WaitForSeconds(2.0f);

        // Spawn 3 Nightmare Orbs
        Vector3 centerPos = transform.position + Vector3.up * 3f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 orbPos = centerPos + new Vector3((i - 1) * 3.5f, Random.Range(-0.5f, 1f), 0f);
            GameObject orb = SpawnNightmareOrb($"TutorialOrb_{i+1}", orbPos);
            if (orb != null) activeWaveMobs.Add(orb);
        }

        waveTransitioning = false;
    }

    private IEnumerator TransitionToWave3_Dash()
    {
        waveTransitioning = true;
        currentWave = 3;

        ShowTutorialBanner(wave3Message);
        yield return new WaitForSeconds(2.0f);

        // Spawn 2 Samurai Mobs with higher aggressiveness
        Vector3 posL = waveSpawnPointLeft != null ? waveSpawnPointLeft.position : transform.position + new Vector3(-5f, 0f, 0f);
        Vector3 posR = waveSpawnPointRight != null ? waveSpawnPointRight.position : transform.position + new Vector3(5f, 0f, 0f);

        GameObject mob1 = SpawnSamuraiMob("TutorialSamurai_Dash1", posL);
        GameObject mob2 = SpawnSamuraiMob("TutorialSamurai_Dash2", posR);

        if (mob1 != null) activeWaveMobs.Add(mob1);
        if (mob2 != null) activeWaveMobs.Add(mob2);

        waveTransitioning = false;
    }

    private IEnumerator CompleteCavernArena()
    {
        waveTransitioning = true;
        currentWave = 4;

        ShowTutorialBanner("🌟 CAVERN ARENA CLEAR! Proceed upward to the High Mountain Peak Shrine!");
        yield return new WaitForSeconds(1.5f);

        // Unlock cavern exit gate
        if (cavernExitGate != null)
        {
            cavernExitGate.SetActive(false);
        }

        // Spawn loot rewards
        OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 1f, 10);
        waveTransitioning = false;
    }

    private GameObject SpawnSamuraiMob(string name, Vector3 pos)
    {
        GameObject mob = new GameObject(name);
        mob.transform.position = pos;
        mob.tag = "enemy";

        SpriteRenderer sr = mob.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.9f, 0.25f, 0.25f, 1f); // Red samurai tint

        BoxCollider2D col = mob.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.2f, 2.8f);

        Rigidbody2D rb = mob.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        NormalMaleSamuraiAI ai = mob.AddComponent<NormalMaleSamuraiAI>();

        return mob;
    }

    private GameObject SpawnNightmareOrb(string name, Vector3 pos)
    {
        GameObject orb = new GameObject(name);
        orb.transform.position = pos;
        orb.tag = "enemy";

        SpriteRenderer sr = orb.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.6f, 0.1f, 0.9f, 1f); // Purple orb tint

        CircleCollider2D col = orb.AddComponent<CircleCollider2D>();
        col.radius = 0.8f;

        Rigidbody2D rb = orb.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // Flying orb

        NightmareOrbAI ai = orb.AddComponent<NightmareOrbAI>();

        return orb;
    }

    private void ShowTutorialBanner(string text)
    {
        Debug.Log($"[TutorialCaveWaveManager] {text}");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 pPos = player.transform.position;
            FloatingDamageNumber.SpawnText(pPos + Vector3.up * 2.5f, text, new Color(1f, 0.85f, 0.3f, 1f));
        }
    }
}
