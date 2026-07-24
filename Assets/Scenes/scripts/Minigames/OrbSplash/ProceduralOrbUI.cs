using UnityEngine;
using UnityEngine.UI;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Unity MonoBehaviour UI component for displaying procedural splash Orbs on any Canvas or Minigame UI.
    /// Supports real-time fluid rendering, fill ratio animation, and splash particle triggers.
    /// </summary>
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

        private RawImage rawImage;
        private ProceduralOrbRenderer orbRenderer;

        public ProceduralOrbRenderer Renderer => orbRenderer;

        void Awake()
        {
            InitializeRenderer();
        }

        void OnEnable()
        {
            if (orbRenderer == null)
            {
                InitializeRenderer();
            }
        }

        public void InitializeRenderer()
        {
            rawImage = GetComponent<RawImage>();
            orbRenderer = new ProceduralOrbRenderer(textureResolution, textureResolution);
            orbRenderer.CurrentOrbType = orbType;
            orbRenderer.FillAmount = fillAmount;
            orbRenderer.WaveSpeed = waveSpeed;

            if (rawImage != null)
            {
                rawImage.texture = orbRenderer.Texture;
                rawImage.color = Color.white;
            }
        }

        void Update()
        {
            bool isGameplay = HUDManager.IsGameplayActive();

            if (rawImage != null && rawImage.enabled != isGameplay)
            {
                rawImage.enabled = isGameplay;
            }

            if (!isGameplay || !autoUpdate || orbRenderer == null) return;

            if (autoOscillateFill)
            {
                fillAmount = 0.5f + Mathf.Sin(Time.unscaledTime * 1.5f) * 0.35f;
            }

            orbRenderer.CurrentOrbType = orbType;
            orbRenderer.FillAmount = fillAmount;
            orbRenderer.WaveSpeed = waveSpeed;

            orbRenderer.UpdateAndRender(Time.unscaledDeltaTime);
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
    }
}
