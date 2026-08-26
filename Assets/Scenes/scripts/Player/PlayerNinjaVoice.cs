using UnityEngine;

/// <summary>
/// Procedural Male Ninja Voice System.
/// Synthesizes athletic, disciplined vocalizations (Jump, Attack Kiai, Dash, Hurt Grunts, Death)
/// completely with mathematical formant synthesis in code.
/// Automatically binds to player actions (jumping, attacking, taking damage, dashing).
/// </summary>
[AddComponentMenu("Player/Player Ninja Voice")]
[RequireComponent(typeof(AudioSource))]
public class PlayerNinjaVoice : MonoBehaviour
{
    [Header("Volume & Mix")]
    [Range(0f, 1f)]
    public float masterVoiceVolume = 0.85f;

    [Range(0f, 1f)]
    public float attackKiaiVolume = 0.90f;

    [Range(0f, 1f)]
    public float jumpExertionVolume = 0.70f;

    [Range(0f, 1f)]
    public float dashExhaleVolume = 0.65f;

    [Range(0f, 1f)]
    public float hurtGruntVolume = 0.95f;

    [Range(0f, 1f)]
    public float deathGaspVolume = 1.0f;

    [Header("Ninja Voice Pitch")]
    [Range(0.6f, 1.4f)]
    [Tooltip("Base vocal pitch. 1.0 = standard adult male ninja, 0.85 = deeper warrior, 1.15 = younger/swifter ninja.")]
    public float voicePitch = 1.0f;

    [Range(0f, 0.15f)]
    [Tooltip("Slight random pitch fluctuation per exertion to keep voice natural.")]
    public float pitchJitter = 0.05f;

    [Header("Cooldowns")]
    [Tooltip("Minimum time between attack vocalizations to prevent voice spamming during rapid combos.")]
    public float attackVoiceCooldown = 0.28f;
    private float lastAttackVoiceTime = -10f;

    // References
    private AudioSource audioSource;
    private Health playerHealth;
    private move playerMove;
    private MageCombat mageCombat;

    // Generated Procedural Clips
    private AudioClip[] attackClips;
    private AudioClip[] jumpClips;
    private AudioClip[] hurtClips;
    private AudioClip dashClip;
    private AudioClip deathClip;

    private bool wasDashingLastFrame = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        SynthesizeNinjaVoiceClips();
    }

    void Start()
    {
        playerHealth = GetComponent<Health>();
        playerMove = GetComponent<move>();
        mageCombat = GetComponent<MageCombat>();

        if (playerHealth != null)
        {
            playerHealth.onDamageTaken += OnDamageTaken;
            playerHealth.onDeath += OnDeath;
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onDamageTaken -= OnDamageTaken;
            playerHealth.onDeath -= OnDeath;
        }
    }

    void Update()
    {
        // 1. Jump detection
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W))
        {
            if (playerMove != null && Mathf.Abs(GetComponent<Rigidbody2D>().linearVelocity.y) < 0.2f)
            {
                PlayJumpVoice();
            }
        }

        // 2. Attack detection (J key / Left click)
        if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
        {
            PlayAttackVoice();
        }

        // 3. Dash detection
        if (playerMove != null)
        {
            if (playerMove.IsDashing && !wasDashingLastFrame)
            {
                PlayDashVoice();
            }
            wasDashingLastFrame = playerMove.IsDashing;
        }
    }

    private void OnDamageTaken(int amount)
    {
        PlayHurtVoice();
    }

    private void OnDeath()
    {
        PlayDeathVoice();
    }

    #region Voice Trigger Methods

    public void PlayAttackVoice()
    {
        if (Time.time - lastAttackVoiceTime < attackVoiceCooldown) return;
        lastAttackVoiceTime = Time.time;

        if (attackClips == null || attackClips.Length == 0) return;
        AudioClip clip = attackClips[Random.Range(0, attackClips.Length)];
        PlayVoiceClip(clip, attackKiaiVolume);
    }

    public void PlayJumpVoice()
    {
        if (jumpClips == null || jumpClips.Length == 0) return;
        AudioClip clip = jumpClips[Random.Range(0, jumpClips.Length)];
        PlayVoiceClip(clip, jumpExertionVolume);
    }

    public void PlayDashVoice()
    {
        if (dashClip == null) return;
        PlayVoiceClip(dashClip, dashExhaleVolume);
    }

    public void PlayHurtVoice()
    {
        if (hurtClips == null || hurtClips.Length == 0) return;
        AudioClip clip = hurtClips[Random.Range(0, hurtClips.Length)];
        PlayVoiceClip(clip, hurtGruntVolume);
    }

    public void PlayDeathVoice()
    {
        if (deathClip == null) return;
        PlayVoiceClip(deathClip, deathGaspVolume);
    }

    private void PlayVoiceClip(AudioClip clip, float volumeScale)
    {
        if (clip == null || audioSource == null) return;

        float rndPitch = voicePitch + Random.Range(-pitchJitter, pitchJitter);
        audioSource.pitch = rndPitch;
        audioSource.PlayOneShot(clip, volumeScale * masterVoiceVolume);
    }

    #endregion

    #region Procedural Formant Voice Synthesis

    /// <summary>
    /// Mathematically synthesizes male vocal cord pulses filtered through human vocal tract formants.
    /// </summary>
    public void SynthesizeNinjaVoiceClips()
    {
        int sampleRate = 44100;

        // 1. ATTACK KIAI VARIATIONS ("Hya!", "Tsh!", "Sei!", "Hah!")
        attackClips = new AudioClip[4];

        // Variant 0: Sharp "Hya!" (Open AH vowel with fast attack)
        attackClips[0] = GenerateVocalExertion(sampleRate, 0.16f, 155f, 120f, 750f, 1350f, 0.35f, "Ninja_Attack_Hya");
        // Variant 1: Disciplined "Tsh!" (Consonant burst + EH vowel)
        attackClips[1] = GenerateVocalExertion(sampleRate, 0.13f, 170f, 135f, 530f, 1750f, 0.45f, "Ninja_Attack_Tsh");
        // Variant 2: Strike "Sei!" (Rising kiai)
        attackClips[2] = GenerateVocalExertion(sampleRate, 0.18f, 145f, 160f, 480f, 1900f, 0.30f, "Ninja_Attack_Sei");
        // Variant 3: Heavy "Hah!" (Deep strike)
        attackClips[3] = GenerateVocalExertion(sampleRate, 0.15f, 140f, 105f, 800f, 1250f, 0.40f, "Ninja_Attack_Hah");

        // 2. JUMP EXERTIONS ("Hup!", "Tah!")
        jumpClips = new AudioClip[2];
        jumpClips[0] = GenerateVocalExertion(sampleRate, 0.11f, 130f, 185f, 620f, 1400f, 0.25f, "Ninja_Jump_Hup");
        jumpClips[1] = GenerateVocalExertion(sampleRate, 0.10f, 145f, 175f, 700f, 1500f, 0.20f, "Ninja_Jump_Tah");

        // 3. HURT GRUNTS ("Ugh!", "Guh!", "Kch!")
        hurtClips = new AudioClip[3];
        // Variant 0: Heavy "Ugh!" (Dropping glottal compression)
        hurtClips[0] = GenerateVocalExertion(sampleRate, 0.17f, 160f, 80f, 480f, 950f, 0.30f, "Ninja_Hurt_Ugh");
        // Variant 1: Struck "Guh!"
        hurtClips[1] = GenerateVocalExertion(sampleRate, 0.15f, 150f, 70f, 520f, 1050f, 0.35f, "Ninja_Hurt_Guh");
        // Variant 2: Sharp impact "Kch!"
        hurtClips[2] = GenerateVocalExertion(sampleRate, 0.14f, 165f, 90f, 450f, 1300f, 0.50f, "Ninja_Hurt_Kch");

        // 4. DASH EXHALE (Fast airy breath burst)
        dashClip = GenerateBreathExhale(sampleRate, 0.18f, "Ninja_Dash_Exhale");

        // 5. DEATH GASP (Long falling gasp)
        deathClip = GenerateVocalExertion(sampleRate, 0.45f, 140f, 45f, 500f, 900f, 0.60f, "Ninja_Death_Gasp");
    }

    /// <summary>
    /// Synthesizes a vocal exertion with glottal pulse excitation and dual resonant formants (F1, F2).
    /// </summary>
    private AudioClip GenerateVocalExertion(
        int sampleRate,
        float duration,
        float startPitch,
        float endPitch,
        float formant1Freq,
        float formant2Freq,
        float breathiness,
        string clipName)
    {
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float glottalPhase = 0f;
        // Two 2nd-order resonant formant filter states (F1 and F2)
        float f1_y1 = 0f, f1_y2 = 0f;
        float f2_y1 = 0f, f2_y2 = 0f;
        float filteredBreath = 0f;

        // Formant bandwidths
        float bw1 = 90f;
        float bw2 = 120f;

        for (int s = 0; s < totalSamples; s++)
        {
            float t = (float)s / totalSamples;

            // Attack (rapid smooth ramp) & Decay Envelope
            float attack = Mathf.SmoothStep(0f, 1f, t / 0.10f);
            float decay = Mathf.Exp(-t * 8.5f) * (1f - Mathf.Pow(t, 5f));
            float envelope = attack * decay;

            // Pitch contour (F0 glottal frequency)
            float pitchF0 = Mathf.Lerp(startPitch, endPitch, Mathf.Pow(t, 0.6f));
            glottalPhase += (2f * Mathf.PI * pitchF0) / sampleRate;
            if (glottalPhase > 2f * Mathf.PI) glottalPhase -= 2f * Mathf.PI;

            // Glottal waveform (Asymmetric pulse simulating vocal fold closure)
            float glottalPulse = (glottalPhase < Mathf.PI * 1.4f) ? Mathf.Sin(glottalPhase * 0.714f) : -(1f - (glottalPhase / (2f * Mathf.PI))) * 0.5f;

            // Breath noise component (turbulent air flowing across vocal cords)
            float rawNoise = Random.value * 2f - 1f;
            filteredBreath += 0.15f * (rawNoise - filteredBreath);
            float excitation = (glottalPulse * (1f - breathiness * 0.5f)) + (filteredBreath * breathiness);

            // Formant 1 (Resonant Bandpass filter)
            float r1 = Mathf.Exp(-Mathf.PI * (bw1 / sampleRate));
            float theta1 = 2f * Mathf.PI * (formant1Freq / sampleRate);
            float a1_1 = 2f * r1 * Mathf.Cos(theta1);
            float a1_2 = -r1 * r1;
            float outF1 = (1f - r1) * excitation + a1_1 * f1_y1 + a1_2 * f1_y2;
            f1_y2 = f1_y1;
            f1_y1 = outF1;

            // Formant 2 (Resonant Bandpass filter)
            float r2 = Mathf.Exp(-Mathf.PI * (bw2 / sampleRate));
            float theta2 = 2f * Mathf.PI * (formant2Freq / sampleRate);
            float a2_1 = 2f * r2 * Mathf.Cos(theta2);
            float a2_2 = -r2 * r2;
            float outF2 = (1f - r2) * excitation + a2_1 * f2_y1 + a2_2 * f2_y2;
            f2_y2 = f2_y1;
            f2_y1 = outF2;

            // Combine formants (F1 gives chest vowel body, F2 gives throat clarity)
            float vocalOutput = (outF1 * 0.65f + outF2 * 0.45f) * envelope;

            // Soft saturation (tanh warmth)
            samples[s] = (float)System.Math.Tanh(vocalOutput * 2.2f);
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Synthesizes a swift breath exhalation for dashing/dodging.
    /// </summary>
    private AudioClip GenerateBreathExhale(int sampleRate, float duration, string clipName)
    {
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float lowNoise = 0f;
        for (int s = 0; s < totalSamples; s++)
        {
            float t = (float)s / totalSamples;
            float attack = Mathf.SmoothStep(0f, 1f, t / 0.15f);
            float decay = Mathf.Exp(-t * 9f);
            float env = attack * decay;

            float raw = Random.value * 2f - 1f;
            lowNoise += 0.12f * (raw - lowNoise);

            // Subtle body pulse + airy noise
            float body = Mathf.Sin(2f * Mathf.PI * 130f * (s / (float)sampleRate)) * 0.2f;
            samples[s] = (float)System.Math.Tanh((lowNoise * 1.5f + body) * env * 1.8f);
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    #endregion

    #region Editor Preview Tools

    [ContextMenu("Test Attack Kiai (Random)")]
    public void EditorTestAttack()
    {
        if (attackClips == null) SynthesizeNinjaVoiceClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        PlayAttackVoice();
    }

    [ContextMenu("Test Jump Voice")]
    public void EditorTestJump()
    {
        if (jumpClips == null) SynthesizeNinjaVoiceClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        PlayJumpVoice();
    }

    [ContextMenu("Test Hurt Grunt")]
    public void EditorTestHurt()
    {
        if (hurtClips == null) SynthesizeNinjaVoiceClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        PlayHurtVoice();
    }

    [ContextMenu("Test Dash Exhale")]
    public void EditorTestDash()
    {
        if (dashClip == null) SynthesizeNinjaVoiceClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        PlayDashVoice();
    }

    [ContextMenu("Test Death Gasp")]
    public void EditorTestDeath()
    {
        if (deathClip == null) SynthesizeNinjaVoiceClips();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        PlayDeathVoice();
    }

    #endregion
}
