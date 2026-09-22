using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerShadowDashTrail - Complete Shadow Visuals & Juice Engine.
/// Provides:
/// 1. Instant Shadow Silhouette Sprite during dash.
/// 2. Dense shadowy phantom afterimages connecting origin and destination.
/// 3. Dark void motes and retro LowResBlackOrb pixel bursts.
/// 4. Camera micro-shake & optic feedback.
/// 5. Addictive procedural warp swoosh audio.
/// 6. Continuous running shadow trail and floating motes matching the Lumi Spear aesthetic.
/// </summary>
[AddComponentMenu("Player/Player Shadow Dash Trail")]
public class PlayerShadowDashTrail : MonoBehaviour
{
    public static PlayerShadowDashTrail Instance { get; private set; }

    [Header("Shadow Colors")]
    public Color shadowSilhouetteColor = new Color(0.04f, 0.02f, 0.08f, 0.92f); // Dark Void Silhouette
    public Color shadowGlowRimColor = new Color(0.22f, 0.08f, 0.38f, 0.85f);    // Deep Shadow Purple
    public Color shadowTrailStart = new Color(0.06f, 0.03f, 0.12f, 0.85f);      // Obsidian Dark
    public Color shadowTrailEnd = new Color(0.18f, 0.05f, 0.30f, 0f);          // Void Dissolve

    [Header("Dash Juice Settings")]
    public int afterimageCount = 14;
    public float afterimageFadeDuration = 0.28f;
    public bool enableCameraShake = true;
    public float cameraShakeDuration = 0.08f;
    public float cameraShakeMagnitude = 0.12f;

    [Header("Run Trail Settings")]
    public float runGhostInterval = 0.07f;
    public float runGhostFadeDuration = 0.22f;
    public float runMoteSpawnInterval = 0.05f;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float dashSoundVolume = 0.85f;
    public AudioClip customDashAudioClip;

    private SpriteRenderer playerSR;
    private TrailRenderer runTrailRenderer;
    private AudioSource audioSource;
    private move playerMove;

    private Color originalSpriteColor = Color.white;
    private Material originalSpriteMaterial;
    private Coroutine silhouetteCoroutine;
    private float runGhostTimer = 0f;
    private float runMoteTimer = 0f;
    private bool isRunningTrailActive = false;

    private static AudioClip proceduralWarpClip;
    private static Material shadowMaterial;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        playerSR = GetComponent<SpriteRenderer>();
        if (playerSR == null) playerSR = GetComponentInChildren<SpriteRenderer>();
        if (playerSR != null)
        {
            originalSpriteColor = playerSR.color;
            originalSpriteMaterial = playerSR.material;
        }

        playerMove = GetComponent<move>();
        if (afterimageCount < 14)
        {
            afterimageCount = 14;
        }

        // Setup Audio Source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D clean crisp audio

        if (customDashAudioClip == null && proceduralWarpClip == null)
        {
            proceduralWarpClip = GenerateCrispWarpAudio();
        }

        // Setup Run Shadow Trail Renderer
        SetupRunTrailRenderer();
    }

    private void SetupRunTrailRenderer()
    {
        GameObject trailHolder = new GameObject("PlayerShadowRunTrail");
        trailHolder.transform.SetParent(transform, false);
        trailHolder.transform.localPosition = new Vector3(0f, -0.4f, 0f);

        runTrailRenderer = trailHolder.AddComponent<TrailRenderer>();
        runTrailRenderer.time = 0.25f;
        runTrailRenderer.startWidth = 0.95f;
        runTrailRenderer.endWidth = 0.05f;
        runTrailRenderer.minVertexDistance = 0.08f;
        runTrailRenderer.material = GetShadowMaterial();

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.05f, 0.02f, 0.10f), 0f),
                new GradientColorKey(new Color(0.20f, 0.06f, 0.32f), 0.6f),
                new GradientColorKey(new Color(0.02f, 0.01f, 0.04f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.40f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        runTrailRenderer.colorGradient = grad;
        runTrailRenderer.sortingLayerName = playerSR != null ? playerSR.sortingLayerName : "Default";
        runTrailRenderer.sortingOrder = playerSR != null ? playerSR.sortingOrder - 1 : 0;
        runTrailRenderer.emitting = false;
    }

    public static Material GetShadowMaterial()
    {
        if (shadowMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit")
                    ?? Shader.Find("Unlit/Transparent");
            if (s != null)
            {
                shadowMaterial = new Material(s) { hideFlags = HideFlags.DontSave };
            }
        }
        return shadowMaterial;
    }

    void Update()
    {
        if (playerMove == null) playerMove = GetComponent<move>();

        // Track Running state for the shadowy run trail
        bool isRunningNow = playerMove != null && playerMove.IsGrounded && !playerMove.IsDashing &&
                           (playerMove.IsRunning || (Mathf.Abs(playerMove.virtualHorizontalInput) > 0.1f && playerMove.moveSpeed > 7f));

        SetRunningTrailActive(isRunningNow);

        if (isRunningTrailActive)
        {
            UpdateRunningShadowFX();
        }
    }

    /// <summary>
    /// Updates ambient shadow afterimages and floating motes while running,
    /// matching the aesthetic of the Lumi Spear.
    /// </summary>
    private void UpdateRunningShadowFX()
    {
        runGhostTimer -= Time.deltaTime;
        if (runGhostTimer <= 0f)
        {
            runGhostTimer = runGhostInterval;
            SpawnShadowAfterimage(transform.position, runGhostFadeDuration, 0.55f, 0.95f);
        }

        runMoteTimer -= Time.deltaTime;
        if (runMoteTimer <= 0f)
        {
            runMoteTimer = runMoteSpawnInterval;
            Vector3 motePos = transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.8f, -0.2f), 0f);
            SpawnShadowMote(motePos, Random.Range(0.20f, 0.32f), Random.Range(0.25f, 0.45f));
        }
    }

    public void SetRunningTrailActive(bool active)
    {
        if (isRunningTrailActive == active) return;
        isRunningTrailActive = active;

        if (runTrailRenderer != null)
        {
            if (active)
            {
                runTrailRenderer.Clear();
                runTrailRenderer.emitting = true;
            }
            else
            {
                runTrailRenderer.emitting = false;
            }
        }
    }

    /// <summary>
    /// Triggered at the exact instant a dash starts.
    /// Renders shadow silhouette, dense afterimages along path, particles, camera punch, and crisp audio.
    /// </summary>
    public void OnDashStart(Vector3 startPos, Vector3 endPos, float facingDir, float duration)
    {
        // 1. Play Addictive Crisp Shadow Warp Sound
        PlayDashAudio();

        // 2. Camera micro-kick
        if (enableCameraShake)
        {
            CameraShakeManager.Shake(cameraShakeDuration, cameraShakeMagnitude);
        }

        // 3. Shadow Silhouette Sprite on Player
        ApplyShadowSilhouette(duration);

        // 4. Dense Phantom Shadow Afterimages connecting start and destination
        SpawnDashAfterimages(startPos, endPos, facingDir);

        // 5. Origin Void Burst (LowResBlackOrb motes + dark shockwave)
        SpawnDashVoidBurst(startPos, true);

        // 6. Spawn Shadow Slice Streak line along the warp path
        SpawnWarpStreakLine(startPos, endPos);
    }

    /// <summary>
    /// Triggered when the instantaneous dash concludes at the target position.
    /// </summary>
    public void OnDashEnd(Vector3 endPos)
    {
        // Destination Void Burst
        SpawnDashVoidBurst(endPos, false);
    }

    /// <summary>
    /// Shifts player sprite into a shadow silhouette for the duration of the dash.
    /// </summary>
    private void ApplyShadowSilhouette(float duration)
    {
        if (playerSR == null) return;
        if (silhouetteCoroutine != null) StopCoroutine(silhouetteCoroutine);
        silhouetteCoroutine = StartCoroutine(ShadowSilhouetteRoutine(duration));
    }

    private IEnumerator ShadowSilhouetteRoutine(float duration)
    {
        if (playerSR == null) yield break;

        // Apply dark shadowy silhouette
        playerSR.color = shadowSilhouetteColor;

        yield return new WaitForSeconds(duration);

        // Rapid smooth recovery back to normal
        float fadeElapsed = 0f;
        float fadeDuration = 0.05f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / fadeDuration;
            if (playerSR != null)
            {
                playerSR.color = Color.Lerp(shadowSilhouetteColor, originalSpriteColor, t);
            }
            yield return null;
        }

        if (playerSR != null)
        {
            playerSR.color = originalSpriteColor;
        }
    }

    /// <summary>
    /// Spawns dense shadow afterimages along the path from start to end position.
    /// </summary>
    private void SpawnDashAfterimages(Vector3 start, Vector3 end, float facingDir)
    {
        if (playerSR == null || playerSR.sprite == null) return;

        float dist = Vector3.Distance(start, end);
        int count = Mathf.Max(afterimageCount, Mathf.RoundToInt(dist * 1.35f));
        for (int i = 0; i <= count; i++)
        {
            float t = (float)i / count;
            Vector3 pos = Vector3.Lerp(start, end, t);
            float alpha = Mathf.Lerp(0.90f, 0.65f, t);
            SpawnShadowAfterimage(pos, afterimageFadeDuration + (t * 0.06f), alpha, 1.0f);
        }
    }

    /// <summary>
    /// Spawns a single shadow afterimage sprite.
    /// </summary>
    private void SpawnShadowAfterimage(Vector3 pos, float duration, float initialAlpha, float startScaleMult)
    {
        if (playerSR == null || playerSR.sprite == null) return;

        GameObject ghost = new GameObject("ShadowAfterimage");
        ghost.transform.position = pos;
        ghost.transform.localScale = transform.localScale * startScaleMult;
        ghost.transform.rotation = transform.rotation;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = playerSR.sprite;
        sr.material = GetShadowMaterial();
        sr.sortingLayerName = playerSR.sortingLayerName;
        sr.sortingOrder = playerSR.sortingOrder - 1;

        Color c = shadowSilhouetteColor;
        c.a = initialAlpha;
        sr.color = c;

        ghost.AddComponent<ShadowGhostFadeAnim>().Initialize(duration, initialAlpha, shadowGlowRimColor);
    }

    /// <summary>
    /// Spawns an energetic burst of shadow particles and retro LowResBlackOrb motes.
    /// </summary>
    private void SpawnDashVoidBurst(Vector3 centerPos, bool isOrigin)
    {
        int particleCount = isOrigin ? 12 : 16;
        for (int i = 0; i < particleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(2.5f, 6.5f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            float scale = Random.Range(0.22f, 0.42f);
            float lifetime = Random.Range(0.18f, 0.35f);

            SpawnShadowMote(centerPos, scale, lifetime, vel);
        }
    }

    /// <summary>
    /// Spawns a single retro LowResBlackOrb shadow mote.
    /// </summary>
    private void SpawnShadowMote(Vector3 pos, float scale, float lifetime, Vector2? driftVel = null)
    {
        GameObject orb = LowResBlackOrb.SpawnOrb(
            pos,
            scale,
            lifetime,
            null,
            playerSR != null ? playerSR.sortingOrder + 1 : 20,
            driftVel ?? new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 1.2f))
        );

        if (orb != null)
        {
            var sr = orb.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.02f, 0.01f, 0.04f, 0.95f);
            }
        }
    }

    /// <summary>
    /// Spawns an ephemeral dark warp streak line between origin and destination.
    /// </summary>
    private void SpawnWarpStreakLine(Vector3 start, Vector3 end)
    {
        GameObject streak = new GameObject("ShadowWarpStreak");
        Vector3 mid = (start + end) * 0.5f;
        streak.transform.position = mid;

        float dist = Vector3.Distance(start, end);
        if (dist < 0.1f)
        {
            Destroy(streak);
            return;
        }

        Vector3 dir = (end - start).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        streak.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        LineRenderer lr = streak.AddComponent<LineRenderer>();
        lr.material = GetShadowMaterial();
        lr.startWidth = 0.65f;
        lr.endWidth = 0.15f;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.sortingLayerName = playerSR != null ? playerSR.sortingLayerName : "Default";
        lr.sortingOrder = playerSR != null ? playerSR.sortingOrder - 2 : 0;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.03f, 0.01f, 0.07f), 0f),
                new GradientColorKey(new Color(0.25f, 0.08f, 0.40f), 0.5f),
                new GradientColorKey(new Color(0.03f, 0.01f, 0.07f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.4f),
                new GradientAlphaKey(0.95f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        lr.colorGradient = grad;

        streak.AddComponent<ShadowStreakFadeAnim>().Initialize(0.12f);
    }

    private void PlayDashAudio()
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = customDashAudioClip != null ? customDashAudioClip : proceduralWarpClip;
        if (clipToPlay != null)
        {
            audioSource.pitch = Random.Range(0.94f, 1.06f);
            float masterVol = AudioManager.Instance != null ? AudioManager.Instance.GetRealSFXVolume() : 1f;
            audioSource.PlayOneShot(clipToPlay, dashSoundVolume * masterVol);
        }
    }

    /// <summary>
    /// Generates an ultra-satisfying, punchy shadow warp swoosh mathematically.
    /// Combines a 45Hz sub-bass punch with a high-velocity airy phase sweep.
    /// </summary>
    private static AudioClip GenerateCrispWarpAudio()
    {
        int sampleRate = 44100;
        float duration = 0.22f;
        int sampleCount = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Envelope: Sharp 8ms attack, crisp exponential decay
            float envelope = Mathf.Exp(-progress * 7.5f) * Mathf.Sin(Mathf.Clamp01(t / 0.008f) * Mathf.PI * 0.5f);

            // Bass thump (85Hz down to 42Hz)
            float bassFreq = Mathf.Lerp(85f, 42f, progress);
            float bass = Mathf.Sin(2f * Mathf.PI * bassFreq * t);

            // Phase sweep (1200Hz swoosh sweeping rapidly down to 180Hz)
            float sweepFreq = Mathf.Lerp(1200f, 180f, Mathf.Pow(progress, 0.5f));
            float phaseSweep = Mathf.Sin(2f * Mathf.PI * sweepFreq * t);

            // Ethereal shadow whisper noise
            float noise = (Random.value * 2f - 1f) * (1f - progress);

            // Mix & master
            float sample = (bass * 0.65f) + (phaseSweep * 0.40f) + (noise * 0.22f);
            samples[i] = Mathf.Clamp(sample * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("ProceduralShadowWarp", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// Animates fading and subtle scale collapse for shadow afterimages.
/// </summary>
public class ShadowGhostFadeAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private float lifetime;
    private float initialAlpha;
    private Color rimColor;
    private float elapsed = 0f;
    private Vector3 initialScale;

    public void Initialize(float duration, float alpha, Color rim)
    {
        sr = GetComponent<SpriteRenderer>();
        lifetime = duration;
        initialAlpha = alpha;
        rimColor = rim;
        initialScale = transform.localScale;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (sr != null)
        {
            Color c = Color.Lerp(new Color(0.04f, 0.02f, 0.08f), rimColor, t * 0.4f);
            c.a = Mathf.Lerp(initialAlpha, 0f, t);
            sr.color = c;
        }

        // Gentle scale dissipation
        transform.localScale = initialScale * Mathf.Lerp(1.0f, 0.88f, t);
    }
}

/// <summary>
/// Fades out the ephemeral warp streak line.
/// </summary>
public class ShadowStreakFadeAnim : MonoBehaviour
{
    private LineRenderer lr;
    private float lifetime;
    private float elapsed = 0f;

    public void Initialize(float duration)
    {
        lr = GetComponent<LineRenderer>();
        lifetime = duration;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (lr != null)
        {
            float width = Mathf.Lerp(0.65f, 0.05f, t);
            lr.startWidth = width;
            lr.endWidth = width * 0.25f;
        }
    }
}
