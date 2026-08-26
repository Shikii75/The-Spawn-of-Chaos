using UnityEngine;

/// <summary>
/// Procedural footstep and landing audio system for the player character.
/// Generates warm, cushioned, organic footstep sounds mathematically via code (no clicky pops).
/// Features realistic heel-toe double-contact, low-pass filtered shoe friction, and dynamic walk/run cadences.
/// </summary>
[AddComponentMenu("Player/Player Footstep Audio")]
[RequireComponent(typeof(AudioSource))]
public class PlayerFootstepAudio : MonoBehaviour
{
    public enum FootwearType
    {
        [Tooltip("Soft, cushioned athletic shoes / ninja cloth wraps (warm, muted, zero click).")]
        SoftShoe,
        [Tooltip("Standard travel boots with solid ground thud and subtle leather scuff.")]
        AdventurerBoots,
        [Tooltip("Deep, grounded bass step for heavy or armored movement.")]
        HeavyBoots,
        [Tooltip("Light, swift footsteps with slight gravel / dust friction.")]
        LightStep
    }

    [Header("Footwear & Tone Settings")]
    [Tooltip("Choose the type of footwear and ground resonance.")]
    public FootwearType footwearStyle = FootwearType.SoftShoe;

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for footstep playback.")]
    public float footstepVolume = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for landing thuds.")]
    public float landingVolume = 0.75f;

    [Range(0.5f, 1.8f)]
    [Tooltip("Base pitch tone of footsteps (lower = deeper bass thump, higher = lighter step).")]
    public float stepTonePitch = 0.95f;

    [Range(0f, 1f)]
    [Tooltip("Softness/cushioning of the shoe sole. Higher values eliminate all harsh edges for a warm pad sound.")]
    public float soleCushion = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Amount of muted ground friction / shoe scuff texture in each step.")]
    public float surfaceScuff = 0.35f;

    [Header("Step Timing & Cadence")]
    [Tooltip("Time between footsteps while walking.")]
    public float walkStepInterval = 0.36f;

    [Tooltip("Time between footsteps while running/dashing.")]
    public float runStepInterval = 0.22f;

    [Tooltip("Minimum horizontal velocity required to trigger footsteps.")]
    public float minVelocityThreshold = 0.2f;

    [Header("Organic Variations")]
    [Range(0f, 0.25f)]
    [Tooltip("Random pitch variation per step to keep it natural and organic.")]
    public float pitchRandomness = 0.06f;

    [Range(0f, 0.2f)]
    [Tooltip("Random volume variation per step.")]
    public float volumeRandomness = 0.04f;

    [Header("Custom Clip Overrides (Optional)")]
    [Tooltip("Optional custom audio clips. If left empty, warm procedural clips are generated automatically.")]
    public AudioClip[] customFootstepClips;
    public AudioClip customLandingClip;

    // Component References
    private AudioSource audioSource;
    private Rigidbody2D rb;
    private move playerMove;
    private bool wasGroundedLastFrame = true;

    // Internal State
    private float stepTimer = 0f;
    private AudioClip[] proceduralClips;
    private AudioClip proceduralLandingClip;
    private const int CLIP_VARIATION_COUNT = 6;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D clean audio

        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<move>();

        if (customFootstepClips == null || customFootstepClips.Length == 0)
        {
            GenerateProceduralAudioClips();
        }
    }

    void Update()
    {
        if (playerMove == null) playerMove = GetComponent<move>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        bool isGrounded = IsPlayerGrounded();

        // 1. Landing detection
        if (isGrounded && !wasGroundedLastFrame)
        {
            PlayLandingSound();
            stepTimer = walkStepInterval * 0.45f;
        }
        wasGroundedLastFrame = isGrounded;

        // 2. Footstep cadence tracking
        if (isGrounded)
        {
            float horizontalSpeed = Mathf.Abs(rb != null ? rb.linearVelocity.x : 0f);
            float inputX = Mathf.Abs(Input.GetAxisRaw("Horizontal"));

            if (horizontalSpeed > minVelocityThreshold || inputX > 0.1f)
            {
                bool isRunning = (playerMove != null && playerMove.moveSpeed > 7f) || horizontalSpeed > 7f || (playerMove != null && playerMove.IsDashing);
                float currentInterval = isRunning ? runStepInterval : walkStepInterval;

                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    PlayFootstep();
                    stepTimer = currentInterval;
                }
            }
            else
            {
                stepTimer = Mathf.Min(stepTimer, 0.05f);
            }
        }
        else
        {
            stepTimer = 0.08f;
        }
    }

    private bool IsPlayerGrounded()
    {
        if (playerMove != null && rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.05f)
        {
            return true;
        }

        Collider2D col = GetComponent<Collider2D>();
        Vector2 origin = col != null ? (Vector2)col.bounds.center + Vector2.down * (col.bounds.extents.y - 0.05f) : (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.25f);
        return hit.collider != null && hit.collider.gameObject != gameObject;
    }

    /// <summary>
    /// Plays a single footstep with organic randomized pitch and volume.
    /// </summary>
    public void PlayFootstep()
    {
        AudioClip clipToPlay = GetRandomFootstepClip();
        if (clipToPlay == null) return;

        float randomPitch = stepTonePitch + Random.Range(-pitchRandomness, pitchRandomness);
        float randomVol = Mathf.Clamp01(footstepVolume + Random.Range(-volumeRandomness, volumeRandomness));

        audioSource.pitch = randomPitch;
        audioSource.PlayOneShot(clipToPlay, randomVol);
    }

    /// <summary>
    /// Plays a deep landing impact sound.
    /// </summary>
    public void PlayLandingSound()
    {
        AudioClip clip = (customLandingClip != null) ? customLandingClip : proceduralLandingClip;
        if (clip == null) return;

        audioSource.pitch = Random.Range(0.92f, 1.04f);
        audioSource.PlayOneShot(clip, landingVolume);
    }

    private AudioClip GetRandomFootstepClip()
    {
        if (customFootstepClips != null && customFootstepClips.Length > 0)
        {
            return customFootstepClips[Random.Range(0, customFootstepClips.Length)];
        }

        if (proceduralClips != null && proceduralClips.Length > 0)
        {
            return proceduralClips[Random.Range(0, proceduralClips.Length)];
        }

        return null;
    }

    #region Organic Procedural Synthesis

    /// <summary>
    /// Synthesizes warm, cushioned, realistic footsteps with heel-toe contact and zero digital clicks.
    /// </summary>
    public void GenerateProceduralAudioClips()
    {
        proceduralClips = new AudioClip[CLIP_VARIATION_COUNT];
        int sampleRate = 44100;

        for (int i = 0; i < CLIP_VARIATION_COUNT; i++)
        {
            // Duration around 110ms - 130ms (enough for full heel-toe acoustic resonance)
            float duration = 0.11f + (i * 0.005f);
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Base resonant frequencies according to style
            float baseStartFreq, baseEndFreq, scuffIntensity, thudDecay;
            switch (footwearStyle)
            {
                case FootwearType.AdventurerBoots:
                    baseStartFreq = 145f + (i * 6f);
                    baseEndFreq = 58f;
                    scuffIntensity = 0.40f * surfaceScuff;
                    thudDecay = 24f;
                    break;
                case FootwearType.HeavyBoots:
                    baseStartFreq = 120f + (i * 4f);
                    baseEndFreq = 42f;
                    scuffIntensity = 0.30f * surfaceScuff;
                    thudDecay = 18f;
                    break;
                case FootwearType.LightStep:
                    baseStartFreq = 165f + (i * 8f);
                    baseEndFreq = 70f;
                    scuffIntensity = 0.50f * surfaceScuff;
                    thudDecay = 30f;
                    break;
                case FootwearType.SoftShoe:
                default:
                    baseStartFreq = 135f + (i * 5f);
                    baseEndFreq = 50f;
                    scuffIntensity = 0.28f * surfaceScuff;
                    thudDecay = 26f;
                    break;
            }

            // Low-pass filter state for warm friction texture
            float filteredNoise = 0f;
            float filterCoeff = Mathf.Lerp(0.12f, 0.04f, soleCushion); // Heavier lowpass filter for softer cushion

            float heelPhase = 0f;
            float toePhase = 0f;
            float toeTimeOffset = 0.024f + (i * 0.002f); // Heel touches, then ball of foot touches ~24ms later

            for (int s = 0; s < totalSamples; s++)
            {
                float timeInSec = (float)s / sampleRate;

                // --- 1. HEEL CONTACT (Light initial thump) ---
                float heelEnvelope = 0f;
                float heelWave = 0f;
                if (timeInSec < 0.05f)
                {
                    float tHeel = timeInSec / 0.05f;
                    // Smooth attack (4ms) to prevent any click, followed by rapid exponential decay
                    float attack = Mathf.SmoothStep(0f, 1f, timeInSec / 0.005f);
                    heelEnvelope = attack * Mathf.Exp(-tHeel * thudDecay) * 0.45f;

                    float heelFreq = Mathf.Lerp(baseStartFreq * 1.15f, baseEndFreq, Mathf.Pow(tHeel, 0.4f));
                    heelPhase += (2f * Mathf.PI * heelFreq) / sampleRate;
                    heelWave = Mathf.Sin(heelPhase) * heelEnvelope;
                }

                // --- 2. MAIN FOOT THUD (Toe / Ball of foot pad) ---
                float toeEnvelope = 0f;
                float toeWave = 0f;
                if (timeInSec >= toeTimeOffset)
                {
                    float tToe = (timeInSec - toeTimeOffset) / (duration - toeTimeOffset);
                    float attackToe = Mathf.SmoothStep(0f, 1f, (timeInSec - toeTimeOffset) / 0.007f);
                    toeEnvelope = attackToe * Mathf.Exp(-tToe * thudDecay * 0.9f);

                    float toeFreq = Mathf.Lerp(baseStartFreq, baseEndFreq, Mathf.Pow(tToe, 0.5f));
                    toePhase += (2f * Mathf.PI * toeFreq) / sampleRate;

                    // Rich fundamental + warm second sub-harmonic (adds earthy warmth)
                    toeWave = (Mathf.Sin(toePhase) * 0.75f + Mathf.Sin(toePhase * 0.5f) * 0.25f) * toeEnvelope;
                }

                // --- 3. WARM LOW-PASS SCUFF / FRICTION (Zero harsh clicks) ---
                float rawWhiteNoise = Random.value * 2f - 1f;
                // Single-pole IIR low-pass filter (simulates cloth/rubber sole dragging against ground)
                filteredNoise = filteredNoise + filterCoeff * (rawWhiteNoise - filteredNoise);
                
                float totalEnvelope = Mathf.Max(heelEnvelope, toeEnvelope);
                float scuffComponent = filteredNoise * scuffIntensity * totalEnvelope * 3.5f;

                // --- 4. COMBINE & SOFT-CLIP ---
                float combined = heelWave + toeWave + scuffComponent;
                
                // Soft tanh saturation for organic acoustic warmth
                samples[s] = (float)System.Math.Tanh(combined * 1.2f);
            }

            AudioClip clip = AudioClip.Create($"Organic_Step_{footwearStyle}_{i}", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            proceduralClips[i] = clip;
        }

        // --- LANDING THUD ---
        {
            float landDuration = 0.20f;
            int landSamples = Mathf.FloorToInt(sampleRate * landDuration);
            float[] lSamples = new float[landSamples];
            float phase = 0f;
            float landFilteredNoise = 0f;

            for (int s = 0; s < landSamples; s++)
            {
                float timeInSec = (float)s / sampleRate;
                float t = timeInSec / landDuration;

                float attack = Mathf.SmoothStep(0f, 1f, timeInSec / 0.008f);
                float envelope = attack * Mathf.Exp(-t * 13f);

                float freq = Mathf.Lerp(130f, 38f, Mathf.Pow(t, 0.35f));
                phase += (2f * Mathf.PI * freq) / sampleRate;

                float rawNoise = Random.value * 2f - 1f;
                landFilteredNoise += 0.08f * (rawNoise - landFilteredNoise);

                float body = (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 0.5f) * 0.3f);
                float noise = landFilteredNoise * 0.4f;

                lSamples[s] = (float)System.Math.Tanh((body + noise) * envelope * 1.3f);
            }

            proceduralLandingClip = AudioClip.Create($"Organic_Landing_{footwearStyle}", landSamples, 1, sampleRate, false);
            proceduralLandingClip.SetData(lSamples, 0);
        }
    }

    #endregion

    #region Editor Tools & Preview

    [ContextMenu("Regenerate & Test Play Footstep")]
    public void EditorTestPlayFootstep()
    {
        GenerateProceduralAudioClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            PlayFootstep();
        }
    }

    [ContextMenu("Regenerate & Test Play Landing")]
    public void EditorTestPlayLanding()
    {
        GenerateProceduralAudioClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            PlayLandingSound();
        }
    }

    #endregion
}
