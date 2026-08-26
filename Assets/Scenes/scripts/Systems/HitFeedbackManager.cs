using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyHitType - Specifies the category of attack impact for tailored particle and audio feedback.
/// </summary>
public enum EnemyHitType
{
    PhysicalMelee,
    HeavyCombo,
    MagicSpell,
    ShadowWisp,
    SpiderVenom
}

/// <summary>
/// HitFeedbackManager - Core Game Feel Engine.
/// Provides all 8 hit feedback features when the player damages any enemy:
/// 1. Hitstop (0.03s - 0.08s game freeze)
/// 2. Flash effect (white/red sprite flash)
/// 3. Squash and stretch (sprite scale compression & bounce back)
/// 4. Screen shake (micro camera shake)
/// 5. Particles (sparks, shockwave rings, shadow wisps/magic bursts)
/// 6. Sound effects (procedurally synthesized high-impact audio clips)
/// 7. Health feedback (floating damage text & world health bar reveal)
/// 8. Brief hit stun (enemy attack pause for 0.15s - 0.3s)
/// </summary>
public class HitFeedbackManager : MonoBehaviour
{
    private static HitFeedbackManager instance;

    public static HitFeedbackManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<HitFeedbackManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("Hit Feedback Manager (Auto)");
                    instance = go.AddComponent<HitFeedbackManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Hitstop Duration")]
    public float lightHitstopDuration = 0.04f;
    public float heavyHitstopDuration = 0.08f;

    [Header("Screen Shake Settings")]
    public float lightShakeDuration = 0.1f;
    public float lightShakeMagnitude = 0.06f;
    public float heavyShakeDuration = 0.15f;
    public float heavyShakeMagnitude = 0.12f;

    // Static Procedural Sound Clips (generated at runtime to guarantee zero missing sound assets)
    private static AudioClip meleeHitSFX;
    private static AudioClip heavyHitSFX;
    private static AudioClip magicHitSFX;

    private Coroutine hitstopCoroutine;
    private float lastHitFrameTime = -1f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            GenerateSynthesizedSFXClips();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private static readonly System.Collections.Generic.Dictionary<int, float> lastHitTimes = new System.Collections.Generic.Dictionary<int, float>();

    /// <summary>
    /// Static trigger for playing full combat hit feedback on an enemy target.
    /// </summary>
    public static void TriggerHitFeedback(Transform target, Vector3 contactPoint, int damage, bool isHeavyHit = false, EnemyHitType hitType = EnemyHitType.PhysicalMelee)
    {
        if (target == null) return;
        int targetId = target.GetInstanceID();

        // Prevent duplicate trigger on same target within 60ms window
        if (lastHitTimes.TryGetValue(targetId, out float lastTime) && Time.unscaledTime - lastTime < 0.06f)
        {
            return;
        }
        lastHitTimes[targetId] = Time.unscaledTime;

        if (Instance != null)
        {
            Instance.PlayHitFeedback(target, contactPoint, damage, isHeavyHit, hitType);
        }
    }

    public void PlayHitFeedback(Transform target, Vector3 contactPoint, int damage, bool isHeavyHit, EnemyHitType hitType)
    {
        if (target == null) return;

        // 1. Hitstop (Game Freeze for 0.03s - 0.08s)
        float hitstopTime = isHeavyHit ? heavyHitstopDuration : lightHitstopDuration;
        TriggerHitstop(hitstopTime);

        // 2. Flash Effect & 3. Squash and Stretch (handled on enemy SpriteJuice component)
        SpriteJuice juice = target.GetComponent<SpriteJuice>();
        if (juice == null) juice = target.GetComponentInChildren<SpriteJuice>();
        if (juice != null)
        {
            Vector2 hitDir = (target.position - (contactPoint != Vector3.zero ? contactPoint : transform.position)).normalized;
            if (hitDir == Vector2.zero) hitDir = Vector2.right;
            juice.PlayHitReaction(hitDir, isHeavyHit ? 3f : 1.5f);
        }

        // 4. Screen Shake
        float shakeDur = isHeavyHit ? heavyShakeDuration : lightShakeDuration;
        float shakeMag = isHeavyHit ? heavyShakeMagnitude : lightShakeMagnitude;
        CameraShakeManager.Shake(shakeDur, shakeMag);

        // 5. Particles (Sparks, shockwave ring, shadow wisps)
        Vector3 spawnPos = contactPoint != Vector3.zero ? contactPoint : target.position + new Vector3(0f, 0.5f, 0f);
        SpawnHitParticles(spawnPos, isHeavyHit, hitType);

        // 6. Sound Effects (Procedural High-Impact Audio Clips)
        PlayImpactSFX(spawnPos, isHeavyHit, hitType);

        // 7. Health Feedback (Floating Damage Numbers & Health Bar Reveal)
        FloatingDamageNumber.Spawn(spawnPos, damage, isHeavyHit);

        EnemyHealthBar healthBar = target.GetComponentInChildren<EnemyHealthBar>();
        if (healthBar == null)
        {
            // Auto-attach EnemyHealthBar if enemy missing world space health UI
            Health enemyHealth = target.GetComponent<Health>();
            if (enemyHealth != null)
            {
                GameObject canvasObj = new GameObject("EnemyHealthCanvas");
                canvasObj.transform.SetParent(target, false);
                canvasObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                healthBar = canvasObj.AddComponent<EnemyHealthBar>();
            }
        }

        // 8. Brief Stun (Pause enemy AI attack timer / hit stun routine)
        UniversalEnemy univEnemy = target.GetComponent<UniversalEnemy>();
        if (univEnemy != null)
        {
            univEnemy.TriggerHitStun(isHeavyHit ? 0.3f : 0.18f);
        }
    }

    /// <summary>
    /// Freezes Time.timeScale for brief duration using real-time delay.
    /// Safely guarded so multiple simultaneous hits never leave Time.timeScale frozen.
    /// </summary>
    public void TriggerHitstop(float duration)
    {
        if (duration <= 0f) return;
        // If already in a hitstop, let the existing one finish to prevent timeScale freeze
        if (hitstopCoroutine != null) return;
        hitstopCoroutine = StartCoroutine(HitstopRoutine(duration));
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        try
        {
            Time.timeScale = 0.001f;
            yield return new WaitForSecondsRealtime(duration);
        }
        finally
        {
            Time.timeScale = 1.0f;
            hitstopCoroutine = null;
        }
    }

    /// <summary>
    /// Spawns dynamic procedural hit particles at the contact point.
    /// </summary>
    private void SpawnHitParticles(Vector3 position, bool isHeavyHit, EnemyHitType hitType)
    {
        GameObject burstObj = new GameObject($"HitParticles_{hitType}");
        burstObj.transform.position = position;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // Stop immediately before configuring main module

        ParticleSystemRenderer psRenderer = burstObj.GetComponent<ParticleSystemRenderer>();
        
        // Set material
        Material sparkMat = new Material(Shader.Find("Sprites/Default"));
        psRenderer.material = sparkMat;

        var main = ps.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = isHeavyHit ? 0.3f : 0.2f;
        main.startSpeed = isHeavyHit ? 10f : 6f;
        main.startSize = isHeavyHit ? 0.18f : 0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;

        // Color theme by hit type
        if (hitType == EnemyHitType.MagicSpell)
        {
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.8f, 1.0f), new Color(0.9f, 0.3f, 1.0f)); // Cyan to Magenta magic
        }
        else if (hitType == EnemyHitType.ShadowWisp || hitType == EnemyHitType.SpiderVenom)
        {
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.0f, 0.5f), new Color(0.1f, 0.7f, 0.2f)); // Dark Shadow & Poison Green
        }
        else if (isHeavyHit)
        {
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.8f, 0.2f), new Color(1.0f, 0.3f, 0.0f)); // Fiery Golden Orange
        }
        else
        {
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 1.0f, 0.8f), new Color(1.0f, 0.6f, 0.1f)); // Crisp Yellow Sparks
        }

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(isHeavyHit ? 24 : 12)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

        ps.Play();
        Destroy(burstObj, 0.5f);
    }

    /// <summary>
    /// Plays impact SFX audio clips via AudioManager or PlayClipAtPoint.
    /// </summary>
    private void PlayImpactSFX(Vector3 position, bool isHeavyHit, EnemyHitType hitType)
    {
        AudioClip clipToPlay = meleeHitSFX;
        if (hitType == EnemyHitType.MagicSpell)
        {
            clipToPlay = magicHitSFX != null ? magicHitSFX : meleeHitSFX;
        }
        else if (isHeavyHit)
        {
            clipToPlay = heavyHitSFX != null ? heavyHitSFX : meleeHitSFX;
        }

        if (clipToPlay != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXAtPosition(clipToPlay, position, isHeavyHit ? 1.0f : 0.8f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clipToPlay, position, isHeavyHit ? 1.0f : 0.8f);
            }
        }
    }

    /// <summary>
    /// Synthesizes procedural crisp impact audio clips so hit SFX works without external WAV assets.
    /// </summary>
    private void GenerateSynthesizedSFXClips()
    {
        if (meleeHitSFX == null)
        {
            meleeHitSFX = CreateHitWaveform("LightHitSFX", 800f, 0.08f, true);
        }
        if (heavyHitSFX == null)
        {
            heavyHitSFX = CreateHitWaveform("HeavyHitSFX", 350f, 0.14f, true);
        }
        if (magicHitSFX == null)
        {
            magicHitSFX = CreateHitWaveform("MagicHitSFX", 1200f, 0.12f, false);
        }
    }

    private AudioClip CreateHitWaveform(string name, float baseFreq, float duration, bool addImpactNoise)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 28f); // Fast punchy decay

            // Pitch bend down transient (thud/slash impact)
            float freq = baseFreq * (1.5f - t * 4f);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
            
            float noise = addImpactNoise ? (Random.value * 2f - 1f) * 0.4f : 0f;
            data[i] = (sine + noise) * envelope * 0.7f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
