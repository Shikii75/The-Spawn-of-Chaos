using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// Anime & Fighting-Game style Impact Frame FX Engine.
    /// Creates stark high-contrast negative silhouettes, radial manga speed lines,
    /// time-dilation micro-freezes, and dynamic slash arcs when the Boss or enemies take heavy hits.
    /// </summary>
    public class ImpactFrameFX : MonoBehaviour
    {
        private static ImpactFrameFX instance;
        public static ImpactFrameFX Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ImpactFrameFX>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ImpactFrameFX_Engine");
                        instance = go.AddComponent<ImpactFrameFX>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private Canvas overlayCanvas;
        private RawImage backgroundFlashImage;
        private RawImage speedlinesImage;
        private Texture2D speedlinesTexture;
        private Coroutine activeImpactRoutine;

        private static readonly int TextureSize = 512;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                BuildOverlayCanvas();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void BuildOverlayCanvas()
        {
            if (overlayCanvas != null) return;

            GameObject canvasGO = new GameObject("ImpactFrame_OverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            overlayCanvas = canvasGO.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 999; // Top priority over gameplay & HUD

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Background Solid Flash
            GameObject bgGO = new GameObject("Impact_BackgroundFlash", typeof(RectTransform), typeof(RawImage));
            bgGO.transform.SetParent(canvasGO.transform, false);
            backgroundFlashImage = bgGO.GetComponent<RawImage>();
            backgroundFlashImage.color = Color.clear;
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Radial Speedlines / Manga Slash Rays
            GameObject linesGO = new GameObject("Impact_Speedlines", typeof(RectTransform), typeof(RawImage));
            linesGO.transform.SetParent(canvasGO.transform, false);
            speedlinesImage = linesGO.GetComponent<RawImage>();
            speedlinesImage.color = Color.clear;
            RectTransform linesRT = linesGO.GetComponent<RectTransform>();
            linesRT.anchorMin = Vector2.zero;
            linesRT.anchorMax = Vector2.one;
            linesRT.sizeDelta = Vector2.zero;

            GenerateSpeedlinesTexture();
        }

        private void GenerateSpeedlinesTexture()
        {
            speedlinesTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            speedlinesTexture.filterMode = FilterMode.Bilinear;
            speedlinesTexture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[TextureSize * TextureSize];
            Vector2 center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
            float maxRadius = TextureSize * 0.5f;

            int rayCount = 48;
            float angleStep = 360f / rayCount;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    Vector2 dir = pos - center;
                    float dist = dir.magnitude;
                    float normalizedDist = Mathf.Clamp01(dist / maxRadius);

                    if (normalizedDist < 0.12f)
                    {
                        // Clear hole around impact center
                        pixels[y * TextureSize + x] = Color.clear;
                        continue;
                    }

                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;

                    // Sharp ray calculation
                    float rayPhase = (angle % angleStep) / angleStep; // 0..1 inside ray segment
                    float raySharpness = Mathf.Sin(rayPhase * Mathf.PI);
                    raySharpness = Mathf.Pow(raySharpness, 3.5f);

                    // Add slight angular noise
                    float noise = Mathf.PerlinNoise(angle * 0.2f, dist * 0.05f);
                    float alpha = raySharpness * normalizedDist * (0.65f + noise * 0.35f);

                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            speedlinesTexture.SetPixels(pixels);
            speedlinesTexture.Apply();

            if (speedlinesImage != null)
            {
                speedlinesImage.texture = speedlinesTexture;
            }
        }

        /// <summary>
        /// Triggers an anime-style high contrast Impact Frame at the given world position.
        /// </summary>
        public static void Trigger(Vector3 worldHitPos, float duration = 0.075f, bool isNegativeInversion = true)
        {
            if (Instance != null)
            {
                Instance.PlayImpact(worldHitPos, duration, isNegativeInversion);
            }
        }

        public void PlayImpact(Vector3 worldHitPos, float duration, bool isNegativeInversion)
        {
            if (activeImpactRoutine != null) StopCoroutine(activeImpactRoutine);
            activeImpactRoutine = StartCoroutine(ImpactFrameRoutine(worldHitPos, duration, isNegativeInversion));
        }

        private IEnumerator ImpactFrameRoutine(Vector3 worldHitPos, float duration, bool isNegativeInversion)
        {
            if (overlayCanvas == null) BuildOverlayCanvas();

            // Calculate screen point for rays center
            Vector2 screenPoint = Vector2.one * 0.5f;
            if (Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToViewportPoint(worldHitPos);
                screenPoint = new Vector2(screenPos.x, screenPos.y);
            }

            // Hitstop: freeze time briefly for intense punch
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0.02f;

            // Frame 1: Stark white/invert flash + high-contrast radial rays
            if (backgroundFlashImage != null)
            {
                backgroundFlashImage.color = isNegativeInversion ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.04f, 0.02f, 0.08f, 0.95f);
            }

            if (speedlinesImage != null)
            {
                speedlinesImage.color = isNegativeInversion ? new Color(0.08f, 0.04f, 0.15f, 0.95f) : new Color(0.95f, 0.35f, 1.0f, 0.90f);
            }

            CameraShakeManager.Shake(0.15f, 0.12f);

            // Wait for impact frame 1 (in real time)
            yield return new WaitForSecondsRealtime(duration * 0.5f);

            // Frame 2: Invert contrast for electric flicker
            if (backgroundFlashImage != null)
            {
                backgroundFlashImage.color = isNegativeInversion ? new Color(0.05f, 0.01f, 0.1f, 0.88f) : new Color(1f, 1f, 1f, 0.85f);
            }

            if (speedlinesImage != null)
            {
                speedlinesImage.color = isNegativeInversion ? new Color(1f, 0.2f, 0.45f, 0.95f) : new Color(0.1f, 0.05f, 0.2f, 0.9f);
            }

            yield return new WaitForSecondsRealtime(duration * 0.5f);

            // Cleanup & Restore
            if (backgroundFlashImage != null) backgroundFlashImage.color = Color.clear;
            if (speedlinesImage != null) speedlinesImage.color = Color.clear;

            Time.timeScale = (MainMenuUIToolkitController.isPlaying) ? 1.0f : 0f;
            activeImpactRoutine = null;
        }
    }
}
