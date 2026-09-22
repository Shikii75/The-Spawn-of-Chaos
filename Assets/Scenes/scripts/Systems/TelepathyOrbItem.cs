using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// TelepathyOrbItem - Interactive world collectible artifact for the Telepathy Orb.
    /// Features:
    /// - Self-contained floating procedural psychic orb with dual-wave styling and concentric pulsing halos.
    /// - Harmonic vertical sine float and counter-rotating ethereal aura rings.
    /// - Proximity-activated world-space prompt ("[E] Absorb Telepathy Orb") with smooth alpha fade.
    /// - Psychic Implosion & Shockwave absorption sequence with homing stardust particles.
    /// - Hybrid audio: Plays assigned AudioClip or synthesizes an ethereal resonant psychic chime at runtime.
    /// - Automatically unlocks universal Japanese dialogue comprehension via TelepathyOrbSystem.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TelepathyOrbItem : MonoBehaviour
    {
        [Header("Interaction & Prompt")]
        [Tooltip("If true, requires pressing the interaction key when in proximity. If false, collects immediately on contact.")]
        public bool requireKeyPress = true;
        public KeyCode interactKey = KeyCode.E;
        [Tooltip("Effective proximity distance for the interaction prompt if using trigger collider.")]
        public float interactionRadius = 2.0f;
        public string promptActionText = "Absorb Telepathy Orb";

        [Header("Floating & Rotation Animation")]
        public float hoverAmplitude = 0.25f;
        public float hoverFrequency = 2.4f;
        public float innerRotationSpeed = 45f;
        public float outerRotationSpeed = 30f;
        public float pulseAmplitude = 0.08f;
        public float pulseFrequency = 3.0f;

        [Header("Visual Hierarchy References")]
        public Transform visualCore;
        public Transform innerHalo;
        public Transform outerHalo;
        public CanvasGroup promptCanvasGroup;
        public TextMeshProUGUI promptText;

        [Header("FX & Particles")]
        public ParticleSystem ambientStardustParticles;
        public ParticleSystem shockwaveBurstParticles;

        [Header("Audio")]
        public AudioClip pickupSFX;
        [Range(0f, 1f)] public float soundVolume = 0.9f;

        // Internal State
        private Vector3 startPos;
        private bool isCollected = false;
        private bool isPlayerNearby = false;
        private Transform playerTransform;
        private AudioSource audioSource;
        private Coroutine absorptionCoroutine;

        private void Awake()
        {
            // Ensure audio source exists
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f; // mild 2D/3D blend
            }

            // Ensure collider is configured as trigger
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is CircleCollider2D circleCol)
                {
                    circleCol.radius = Mathf.Max(circleCol.radius, interactionRadius);
                }
            }

            // Build or hook visuals if missing
            EnsureVisualSetup();
        }

        private void Start()
        {
            startPos = transform.position;

            // If already claimed in current save state, deactivate immediately
            if (TelepathyOrbSystem.HasTelepathyOrb)
            {
                gameObject.SetActive(false);
                return;
            }

            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = 0f;
            }

            UpdatePromptLabel();
        }

        private void Update()
        {
            if (isCollected) return;

            // 1. Harmonic vertical sine hovering
            float offsetY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            transform.position = startPos + new Vector3(0f, offsetY, 0f);

            // 2. Halo counter-rotations
            if (innerHalo != null)
            {
                innerHalo.Rotate(0f, 0f, innerRotationSpeed * Time.deltaTime);
            }
            if (outerHalo != null)
            {
                outerHalo.Rotate(0f, 0f, -outerRotationSpeed * Time.deltaTime);
            }

            // 3. Core breathing scale pulse
            if (visualCore != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency) * pulseAmplitude;
                visualCore.localScale = new Vector3(pulse, pulse, 1f);
            }

            // 4. Proximity distance check backup (if player missed OnTriggerEnter)
            CheckPlayerProximity();

            // 5. Smooth prompt fade
            if (promptCanvasGroup != null)
            {
                float targetAlpha = (isPlayerNearby && !isCollected) ? 1f : 0f;
                promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.deltaTime * 6f);
            }

            // 6. Keypress detection
            if (requireKeyPress && isPlayerNearby && Input.GetKeyDown(interactKey))
            {
                Collect();
            }
        }

        private void CheckPlayerProximity()
        {
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            if (playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                bool withinRange = dist <= interactionRadius;

                if (withinRange && !isPlayerNearby)
                {
                    isPlayerNearby = true;
                    if (!requireKeyPress)
                    {
                        Collect();
                    }
                }
                else if (!withinRange && isPlayerNearby)
                {
                    isPlayerNearby = false;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            if (other.CompareTag("Player") || other.name.Contains("Player"))
            {
                if (playerTransform == null) playerTransform = other.transform;
                isPlayerNearby = true;
                if (!requireKeyPress)
                {
                    Collect();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.name.Contains("Player"))
            {
                isPlayerNearby = false;
            }
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            if (absorptionCoroutine != null) StopCoroutine(absorptionCoroutine);
            absorptionCoroutine = StartCoroutine(PsychicImplosionSequenceRoutine());
        }

        private IEnumerator PsychicImplosionSequenceRoutine()
        {
            // Instantly hide prompt
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = 0f;
            }

            // Play resonant psychic chime audio
            PlayPsychicSound();

            // Stop ambient stardust
            if (ambientStardustParticles != null)
            {
                ambientStardustParticles.Stop();
            }

            // Trigger shockwave & homing sparks
            if (shockwaveBurstParticles != null)
            {
                shockwaveBurstParticles.transform.SetParent(null);
                shockwaveBurstParticles.Play();
                Destroy(shockwaveBurstParticles.gameObject, 4f);
            }

            // Psychic charge-up & implosion scale animation
            Vector3 originalScale = visualCore != null ? visualCore.localScale : transform.localScale;
            float chargeDuration = 0.3f;
            float elapsed = 0f;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / chargeDuration;

                // High frequency jitter / vibrating contraction
                float jitter = Mathf.Sin(progress * 40f) * 0.15f * (1f - progress);
                float scaleMod = Mathf.Lerp(1f, 1.45f, progress) + jitter;

                if (visualCore != null)
                {
                    visualCore.localScale = originalScale * scaleMod;
                }

                yield return null;
            }

            // Snap implosion collapse
            elapsed = 0f;
            float implodeDuration = 0.18f;
            while (elapsed < implodeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / implodeDuration;

                float scaleMod = Mathf.Lerp(1.45f, 0.02f, progress * progress);
                if (visualCore != null)
                {
                    visualCore.localScale = originalScale * scaleMod;
                }
                if (innerHalo != null)
                {
                    innerHalo.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.zero, progress);
                }
                if (outerHalo != null)
                {
                    outerHalo.localScale = Vector3.Lerp(Vector3.one * 2f, Vector3.zero, progress);
                }

                yield return null;
            }

            // Unlock narrative language comprehension & display acquisition banner
            TelepathyOrbSystem.EnsureExists();
            if (TelepathyOrbSystem.Instance != null)
            {
                TelepathyOrbSystem.Instance.AcquireTelepathyOrb();
            }

            // Wait a brief moment before fully deactivating GameObject
            yield return new WaitForSeconds(0.15f);
            gameObject.SetActive(false);
        }

        private void PlayPsychicSound()
        {
            AudioClip clipToPlay = pickupSFX;
            if (clipToPlay == null)
            {
                clipToPlay = GenerateProceduralPsychicChime();
            }

            if (clipToPlay != null)
            {
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(clipToPlay, soundVolume);
                }
                else if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(clipToPlay);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(clipToPlay, transform.position, soundVolume);
                }
            }
        }

        /// <summary>
        /// Synthesizes a resonant, ethereal harmonic psychic chord (E5, B5, E6, G#6) with exponential decay.
        /// Fully procedural: requires zero external audio assets.
        /// </summary>
        private AudioClip GenerateProceduralPsychicChime()
        {
            int sampleRate = 44100;
            float duration = 1.6f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Harmonic psychic frequencies
            float[] frequencies = { 659.25f, 987.77f, 1318.51f, 1661.22f, 2637.02f };
            float[] weights = { 0.35f, 0.28f, 0.22f, 0.10f, 0.05f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Ethereal exponential reverb decay curve
                float envelope = Mathf.Exp(-2.8f * t);

                float sampleVal = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    sampleVal += Mathf.Sin(2f * Mathf.PI * frequencies[f] * t) * weights[f];
                }

                // Add subtle vibrato / mindwave shimmer
                float shimmer = 1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 8.5f * t);

                samples[i] = sampleVal * envelope * shimmer * 0.75f;
            }

            AudioClip chimeClip = AudioClip.Create("Procedural_PsychicChime", totalSamples, 1, sampleRate, false);
            chimeClip.SetData(samples, 0);
            return chimeClip;
        }

        public void UpdatePromptLabel()
        {
            if (promptText != null)
            {
                promptText.text = $"✦ [{interactKey}] {promptActionText} ✦";
            }
        }

        /// <summary>
        /// Ensures all child transforms, halos, particles, and prompt canvas are built and assigned.
        /// Can be called in Editor or at runtime.
        /// </summary>
        public void EnsureVisualSetup()
        {
            // 1. Locate or create VisualCore
            if (visualCore == null)
            {
                Transform t = transform.Find("VisualCore");
                if (t != null) visualCore = t;
                else
                {
                    // Fallback to self if it has SpriteRenderer
                    SpriteRenderer sr = GetComponent<SpriteRenderer>();
                    if (sr != null) visualCore = transform;
                }
            }

            // 2. Locate or hook InnerGlow
            if (innerHalo == null)
            {
                Transform t = transform.Find("EtherealGlow") ?? transform.Find("InnerGlow");
                if (t != null) innerHalo = t;
            }

            // 3. Locate or hook OuterHalo
            if (outerHalo == null)
            {
                Transform t = transform.Find("OuterHalo");
                if (t != null) outerHalo = t;
            }

            // 4. Locate or create PromptCanvas
            if (promptCanvasGroup == null)
            {
                Transform promptT = transform.Find("PromptCanvas");
                if (promptT != null)
                {
                    promptCanvasGroup = promptT.GetComponent<CanvasGroup>();
                    promptText = promptT.GetComponentInChildren<TextMeshProUGUI>();
                }
                else if (Application.isPlaying)
                {
                    CreateRuntimePromptCanvas();
                }
            }

            // 5. Locate particles
            if (ambientStardustParticles == null)
            {
                Transform pT = transform.Find("StardustParticles");
                if (pT != null) ambientStardustParticles = pT.GetComponent<ParticleSystem>();
                else if (Application.isPlaying)
                {
                    CreateRuntimeStardustParticles();
                }
            }

            if (shockwaveBurstParticles == null)
            {
                Transform sT = transform.Find("ShockwaveBurst");
                if (sT != null) shockwaveBurstParticles = sT.GetComponent<ParticleSystem>();
            }
        }

        private void CreateRuntimePromptCanvas()
        {
            GameObject canvasGO = new GameObject("PromptCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 1.85f, 0f);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            promptCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
            promptCanvasGroup.alpha = 0f;
            promptCanvasGroup.blocksRaycasts = false;

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(460, 75);
            canvasRT.localScale = new Vector3(0.018f, 0.018f, 0.018f);

            GameObject panelGO = new GameObject("PanelBackground");
            panelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;

            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.03f, 0.14f, 0.88f);

            Outline outline = panelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.80f, 0.45f, 1.0f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, 2.5f);

            GameObject textGO = new GameObject("PromptText");
            textGO.transform.SetParent(panelGO.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            promptText = textGO.AddComponent<TextMeshProUGUI>();
            promptText.text = $"✦ [{interactKey}] {promptActionText} ✦";
            promptText.fontSize = 32f;
            promptText.fontStyle = FontStyles.Bold;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(0.96f, 0.88f, 1f, 1f);
        }

        private void CreateRuntimeStardustParticles()
        {
            GameObject stardustGO = new GameObject("StardustParticles");
            stardustGO.transform.SetParent(transform, false);
            ambientStardustParticles = stardustGO.AddComponent<ParticleSystem>();

            var main = ambientStardustParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.gravityModifier = -0.04f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.45f, 1f, 0.85f),
                new Color(0.55f, 0.20f, 0.95f, 0.40f)
            );
            main.loop = true;
            main.playOnAwake = true;

            var emission = ambientStardustParticles.emission;
            emission.rateOverTime = 12f;

            var shape = ambientStardustParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;

            var vel = ambientStardustParticles.velocityOverLifetime;
            vel.enabled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdatePromptLabel();
        }
#endif
    }
}
