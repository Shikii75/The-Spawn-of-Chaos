using UnityEngine;
using UnityEngine.UI;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Unity MonoBehaviour UI component for displaying procedural splash Orbs on any Canvas or Minigame UI.
    /// Supports real-time fluid rendering, fill ratio animation, and splash particle triggers.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RawImage))]
    public class ProceduralOrbUI : MonoBehaviour
    {
        [Header("Orb Configuration")]
        public OrbType orbType = OrbType.Health;
        [Range(0f, 1f)] public float fillAmount = 0.75f;
        public int textureResolution = 128;

        [Header("Animation Settings")]
        public bool autoUpdate = true;
        public bool autoOscillateFill = false;
        public float waveSpeed = 3.5f;

        [Header("Player Auto-Link")]
        public bool autoLinkToPlayer = true;

        private RawImage rawImage;
        private ProceduralOrbRenderer orbRenderer;
        private RectTransform rectTransform;

        // Visual lerp & hit shake fields
        private float currentVisualFill = 0.75f;
        private float catchUpVisualFill = 0.75f;
        private Vector2 baseAnchoredPosition;
        private bool hasBasePosition = false;
        private float shakeTimer = 0f;
        private float shakeDuration = 0.35f;
        private float shakeIntensity = 1.0f;

        private Health linkedHealth;

        public ProceduralOrbRenderer Renderer => orbRenderer;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                hasBasePosition = true;
            }
            InitializeRenderer();
        }

        void OnEnable()
        {
            if (rectTransform != null && !hasBasePosition)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                hasBasePosition = true;
            }
            if (orbRenderer == null)
            {
                InitializeRenderer();
            }
        }

        void Start()
        {
            if (orbRenderer == null)
            {
                InitializeRenderer();
            }
        }

        void OnDisable()
        {
            if (linkedHealth != null)
            {
                linkedHealth.onDamageTaken -= OnPlayerDamageTaken;
                linkedHealth = null;
            }
        }

        public void InitializeRenderer()
        {
            rawImage = GetComponent<RawImage>();
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null && !hasBasePosition)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                hasBasePosition = true;
            }

            currentVisualFill = fillAmount;
            catchUpVisualFill = fillAmount;
            orbRenderer = new ProceduralOrbRenderer(textureResolution, textureResolution);
            orbRenderer.CurrentOrbType = orbType;
            orbRenderer.FillAmount = currentVisualFill;
            orbRenderer.CatchUpFillAmount = catchUpVisualFill;
            orbRenderer.WaveSpeed = waveSpeed;

            // Render initial frame immediately so Texture2D is fully populated from frame 0
            orbRenderer.UpdateAndRender(0f);

            if (rawImage != null)
            {
                rawImage.texture = orbRenderer.Texture;
                rawImage.color = Color.white;
                rawImage.enabled = true;
            }
        }

        void Update()
        {
            if (orbRenderer == null)
            {
                InitializeRenderer();
            }

            if (rawImage != null && !rawImage.enabled)
            {
                rawImage.enabled = true;
            }

            if (!autoUpdate || orbRenderer == null) return;

            if (autoLinkToPlayer)
            {
                AutoLinkStats();
            }

            if (autoOscillateFill)
            {
                fillAmount = 0.5f + Mathf.Sin(Time.unscaledTime * 1.5f) * 0.35f;
            }

            // Real-time liquid level reduction lerp
            currentVisualFill = Mathf.Lerp(currentVisualFill, fillAmount, Time.unscaledDeltaTime * 7f);
            if (Mathf.Abs(currentVisualFill - fillAmount) < 0.001f)
            {
                currentVisualFill = fillAmount;
            }

            // Trailing damage ghost fill lerp (slowly drains to reveal damage lost)
            if (fillAmount < catchUpVisualFill)
            {
                catchUpVisualFill = Mathf.Lerp(catchUpVisualFill, currentVisualFill, Time.unscaledDeltaTime * 2.5f);
                if (catchUpVisualFill - currentVisualFill < 0.002f)
                {
                    catchUpVisualFill = currentVisualFill;
                }
            }
            else
            {
                catchUpVisualFill = fillAmount;
            }

            orbRenderer.CurrentOrbType = orbType;
            orbRenderer.FillAmount = currentVisualFill;
            orbRenderer.CatchUpFillAmount = catchUpVisualFill;
            orbRenderer.WaveSpeed = waveSpeed;

            // Handle hit shake animation on the UI RectTransform (Health Orb damage response)
            if (shakeTimer > 0f && rectTransform != null)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeDuration));
                float currentMag = 12f * shakeIntensity * progress;

                float offsetX = Random.Range(-currentMag, currentMag);
                float offsetY = Random.Range(-currentMag, currentMag);
                rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(offsetX, offsetY);

                if (shakeTimer <= 0f)
                {
                    rectTransform.anchoredPosition = baseAnchoredPosition;
                }
            }

            orbRenderer.UpdateAndRender(Time.unscaledDeltaTime);
        }

        private void AutoLinkStats()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player") 
                        ?? GameObject.Find("Player") 
                        ?? GameObject.Find("BasePlayer") 
                        ?? (move.Instance != null ? move.Instance.gameObject : null);
            if (p == null) return;

            switch (orbType)
            {
                case OrbType.Health:
                    Health h = p.GetComponent<Health>();
                    if (h != null)
                    {
                        if (linkedHealth != h)
                        {
                            if (linkedHealth != null) linkedHealth.onDamageTaken -= OnPlayerDamageTaken;
                            linkedHealth = h;
                            linkedHealth.onDamageTaken += OnPlayerDamageTaken;
                        }
                        fillAmount = (float)h.CurrentHealth / Mathf.Max(1, h.MaxHealth);
                    }
                    break;

                case OrbType.Mana:
                    MageCombat mc = p.GetComponent<MageCombat>();
                    if (mc != null)
                    {
                        fillAmount = mc.currentMana / Mathf.Max(1f, mc.maxMana);
                    }
                    break;

                case OrbType.Currency:
                    PlayerCurrency pc = p.GetComponent<PlayerCurrency>();
                    if (pc == null) pc = PlayerCurrency.Instance;
                    if (pc != null)
                    {
                        fillAmount = Mathf.Clamp01(0.10f + (pc.Coins / 100f) * 0.90f);
                    }
                    break;

                case OrbType.EP:
                    if (SpawnOfChaos.Systems.PlayerLevelSystem.Instance != null)
                    {
                        fillAmount = SpawnOfChaos.Systems.PlayerLevelSystem.Instance.ExpRatio;
                    }
                    break;
            }
        }

        private void OnPlayerDamageTaken(int damageTaken)
        {
            if (damageTaken > 0 && orbType == OrbType.Health)
            {
                TriggerDamageEffect(1.5f, 0.4f);
            }
        }

        /// <summary>
        /// Call to trigger a violent liquid splash burst outward from the orb surface!
        /// </summary>
        public void TriggerSplash(float intensity = 1f)
        {
            if (orbRenderer != null)
            {
                orbRenderer.TriggerSplash(intensity);
            }
        }

        /// <summary>
        /// Triggers full real-time damage feedback on the Health Orb: hit shake, red flash, wave surge, and splash burst!
        /// </summary>
        public void TriggerDamageEffect(float intensity = 1.5f, float duration = 0.4f)
        {
            TriggerShake(intensity, duration);
            if (orbRenderer != null)
            {
                orbRenderer.TriggerDamageFlash(duration);
            }
        }

        /// <summary>
        /// Triggers a punchy visual shake on the Orb UI element when hit or taking damage!
        /// </summary>
        public void TriggerShake(float intensity = 1f, float duration = 0.35f)
        {
            if (rectTransform != null)
            {
                if (shakeTimer <= 0f || !hasBasePosition)
                {
                    baseAnchoredPosition = rectTransform.anchoredPosition;
                    hasBasePosition = true;
                }
                shakeIntensity = intensity;
                shakeDuration = duration;
                shakeTimer = duration;
            }

            TriggerSplash(intensity * 1.2f);
        }

        public void SetBasePosition(Vector2 pos)
        {
            baseAnchoredPosition = pos;
            hasBasePosition = true;
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null) rectTransform.anchoredPosition = pos;
        }
    }
}
