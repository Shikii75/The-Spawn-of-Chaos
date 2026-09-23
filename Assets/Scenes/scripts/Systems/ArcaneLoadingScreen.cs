using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// Arcane Loading Screen - High-aesthetic asynchronous scene loader.
    /// Features living cosmic void backdrop with floating purple bokeh orbs,
    /// rotating dual-ring arcane mandala seal, majestic centerpiece crest,
    /// deeply satisfying cyber-gothic glassmorphic capsule progress bar with liquid gradient fill,
    /// sweeping specular sheen, traveling plasma comet core, and dynamic lore tips.
    /// </summary>
    public class ArcaneLoadingScreen : MonoBehaviour
    {
        public static ArcaneLoadingScreen Instance { get; private set; }

        [Header("Visual Palette")]
        public Color voidBackdropColor = new Color(0.02f, 0.005f, 0.04f, 1.0f);
        public Color runeGlowColor = new Color(0.85f, 0.40f, 1.0f, 0.75f);
        public Color barFillColor = new Color(0.80f, 0.30f, 1.0f, 1.0f);

        private Canvas loadingCanvas;
        private CanvasGroup canvasGroup;
        private RectTransform barTrackRT;
        private Image progressBarFill;
        private RectTransform barSparkRT;
        private Image barSparkHaloImg;
        private Image barSparkCoreImg;
        private Image barAmbientGlowImg;
        private RectTransform barAmbientGlowRT;
        private RectTransform sheenRT;
        private Image sheenImg;
        private TextMeshProUGUI progressTextComp;
        private TextMeshProUGUI statusPhaseTextComp;
        private TextMeshProUGUI tipTextComp;
        private RectTransform outerRuneSealRT;
        private RectTransform innerRuneSealRT;
        private RectTransform centerEmblemRT;
        private Sprite softBokehSprite;
        private Sprite radialVignetteSprite;
        private Sprite gradientBarSprite;
        private Sprite sheenSprite;

        private float progressVelocity = 0f;
        private float sheenTimer = 0f;
        private const float BAR_MAX_WIDTH = 650f;
        private const float BAR_HEIGHT = 22f;

        private class LoadingOrb
        {
            public RectTransform rt;
            public Image img;
            public float speedY;
            public float baseX;
            public float swayAmp;
            public float swayFreq;
            public float swayPhase;
            public float baseAlpha;
            public Color baseColor;
        }

        private readonly List<LoadingOrb> activeOrbs = new List<LoadingOrb>();

        private static readonly string[] LoreTips = new string[]
        {
            "Dash grants invulnerability frames through physical attacks and corrupt hazards.",
            "Lumi's celestial radiance cuts through dark cavern fog and illuminates hidden passages.",
            "Voluntary sacrifice at Drifter Altars anchors your immortal soul to that exact point in time.",
            "Double-tapping directional controls initiates a high-velocity sprint to outrun crumbling terrain.",
            "Consult Nyxaris with [C] or through the HUD to uncover secrets, lore, and companion guidance.",
            "Ancient telepathy orbs decode the mysterious tongues of clan warriors.",
            "Tsuchigumo weaves deceptive webs and commands ravenous spider swarms deep below the earth.",
            "Press [F1] during the opening lore prologue to cycle between ancient runic typography styles.",
            "Tap the centerpiece wings emblem on the title screen to command the Spawn of Chaos."
        };

        private static bool isCurrentlyLoading = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Instance = null;
            isCurrentlyLoading = false;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                softBokehSprite = GenerateSoftBokehSprite();
                radialVignetteSprite = GenerateRadialVignetteSprite();
                gradientBarSprite = GenerateGradientBarSprite();
                sheenSprite = GenerateSheenSprite();
                BuildVisualHierarchy();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("ArcaneLoadingScreen");
                Instance = go.AddComponent<ArcaneLoadingScreen>();
            }
        }

        public static void LoadScene(string sceneName)
        {
            if (isCurrentlyLoading)
            {
                Debug.LogWarning($"[ArcaneLoadingScreen] Scene load already in progress. Ignoring redundant call for '{sceneName}'.");
                return;
            }
            isCurrentlyLoading = true;
            EnsureExists();
            Instance.StartCoroutine(Instance.LoadSceneAsyncRoutine(sceneName));
        }

        private Sprite GenerateSoftBokehSprite()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.49f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    if (dist >= 1.0f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    float edge = Mathf.Clamp01(1.0f - dist);
                    float smooth = edge * edge * (3f - 2f * edge);
                    float alpha = Mathf.Exp(-1.9f * dist * dist) * smooth;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite GenerateRadialVignetteSprite()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = Mathf.SmoothStep(0f, 1f, dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite GenerateGradientBarSprite()
        {
            int width = 256;
            int height = 32;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color colTail = new Color(0.38f, 0.10f, 0.82f, 1f);      // Deep arcane violet
            Color colMid = new Color(0.76f, 0.18f, 0.94f, 1f);       // Electric orchid
            Color colHead = new Color(0.95f, 0.42f, 1.0f, 1f);       // Radiant plasma magenta
            Color colTip = new Color(1.0f, 0.92f, 1.0f, 1f);         // White-hot plasma crest

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                Color baseCol;
                if (u < 0.55f)
                {
                    baseCol = Color.Lerp(colTail, colMid, u / 0.55f);
                }
                else if (u < 0.94f)
                {
                    baseCol = Color.Lerp(colMid, colHead, (u - 0.55f) / 0.39f);
                }
                else
                {
                    baseCol = Color.Lerp(colHead, colTip, (u - 0.94f) / 0.06f);
                }

                for (int y = 0; y < height; y++)
                {
                    float v = (float)y / (height - 1);
                    // Subtle glass gloss highlight on upper cylinder half
                    float gloss = 0f;
                    if (v > 0.5f)
                    {
                        gloss = Mathf.Sin((v - 0.5f) * 2f * Mathf.PI * 0.5f) * 0.18f;
                    }
                    else
                    {
                        gloss = -Mathf.Sin((0.5f - v) * 2f * Mathf.PI * 0.5f) * 0.08f;
                    }

                    Color finalCol = new Color(
                        Mathf.Clamp01(baseCol.r + gloss),
                        Mathf.Clamp01(baseCol.g + gloss),
                        Mathf.Clamp01(baseCol.b + gloss),
                        1f
                    );
                    tex.SetPixel(x, y, finalCol);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0f, 0.5f));
        }

        private Sprite GenerateSheenSprite()
        {
            int width = 64;
            int height = 32;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Angled diagonal specular band
                    float diagX = (float)x - ((float)y / height) * 16f;
                    float centerDist = Mathf.Abs(diagX - 24f) / 18f;
                    float alpha = Mathf.Clamp01(1f - centerDist * centerDist) * 0.45f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private Sprite LoadSpriteAsset(string path)
        {
            Sprite s = Resources.Load<Sprite>(path);
            if (s != null) return s;
#if UNITY_EDITOR
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
#endif
            return null;
        }

        private void BuildVisualHierarchy()
        {
            loadingCanvas = UIFactory.CreateCanvas("ArcaneLoadingCanvas", 999);
            loadingCanvas.transform.SetParent(transform, false);
            canvasGroup = loadingCanvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            CanvasScaler scaler = loadingCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // 1. Fullscreen Cosmic Backdrop
            RectTransform backdropRT = UIFactory.CreateFullScreenPanel(loadingCanvas.transform, "VoidBackdrop", voidBackdropColor);

            // Vignette Overlay
            GameObject vignetteGO = new GameObject("Vignette");
            RectTransform vigRT = vignetteGO.AddComponent<RectTransform>();
            vigRT.SetParent(backdropRT, false);
            vigRT.anchorMin = Vector2.zero;
            vigRT.anchorMax = Vector2.one;
            vigRT.sizeDelta = Vector2.zero;
            Image vigImg = vignetteGO.AddComponent<Image>();
            vigImg.sprite = radialVignetteSprite;
            vigImg.color = new Color(0.02f, 0.005f, 0.06f, 0.92f);
            vigImg.raycastTarget = false;

            // 2. Living Void Floating Purple Orbs Container
            GameObject orbsContainerGO = new GameObject("LoadingOrbsContainer");
            RectTransform orbsContainerRT = orbsContainerGO.AddComponent<RectTransform>();
            orbsContainerRT.SetParent(backdropRT, false);
            orbsContainerRT.anchorMin = Vector2.zero;
            orbsContainerRT.anchorMax = Vector2.one;
            orbsContainerRT.sizeDelta = Vector2.zero;

            SpawnFloatingOrbs(orbsContainerGO.transform);

            // 3. Centerpiece Arcane Mandala & Wings Emblem
            GameObject centerContainer = new GameObject("CenterpieceContainer");
            RectTransform centerContainerRT = centerContainer.AddComponent<RectTransform>();
            centerContainerRT.SetParent(backdropRT, false);
            centerContainerRT.anchorMin = new Vector2(0.5f, 0.60f);
            centerContainerRT.anchorMax = new Vector2(0.5f, 0.60f);
            centerContainerRT.sizeDelta = new Vector2(600f, 360f);

            // Backlight Glow Disc
            GameObject glowDiscGO = new GameObject("CenterGlowDisc");
            RectTransform glowDiscRT = glowDiscGO.AddComponent<RectTransform>();
            glowDiscRT.SetParent(centerContainerRT, false);
            glowDiscRT.sizeDelta = new Vector2(480f, 480f);
            Image glowDiscImg = glowDiscGO.AddComponent<Image>();
            glowDiscImg.sprite = softBokehSprite;
            glowDiscImg.color = new Color(0.65f, 0.20f, 0.95f, 0.38f);
            glowDiscImg.raycastTarget = false;

            // Outer Arcane Rune Mandala (Rotating Clockwise)
            Sprite runeSealSprite = LoadSpriteAsset("Assets/Scenes/art/arcane_rune_seal.png");
            GameObject outerSealGO = new GameObject("OuterRuneSeal");
            outerRuneSealRT = outerSealGO.AddComponent<RectTransform>();
            outerRuneSealRT.SetParent(centerContainerRT, false);
            outerRuneSealRT.sizeDelta = new Vector2(320f, 320f);
            Image outerSealImg = outerSealGO.AddComponent<Image>();
            outerSealImg.sprite = runeSealSprite ?? UIFactory.GetRoundedSprite();
            outerSealImg.color = new Color(0.78f, 0.42f, 1.0f, 0.65f);
            outerSealImg.raycastTarget = false;

            // Inner Sacred Geometry Sigil (Rotating Counter-Clockwise)
            GameObject innerSealGO = new GameObject("InnerRuneSeal");
            innerRuneSealRT = innerSealGO.AddComponent<RectTransform>();
            innerRuneSealRT.SetParent(centerContainerRT, false);
            innerRuneSealRT.sizeDelta = new Vector2(210f, 210f);
            Image innerSealImg = innerSealGO.AddComponent<Image>();
            innerSealImg.sprite = runeSealSprite ?? UIFactory.GetRoundedSprite();
            innerSealImg.color = new Color(0.92f, 0.68f, 1.0f, 0.85f);
            innerSealImg.raycastTarget = false;

            // Centerpiece Wings Emblem
            Sprite emblemSprite = LoadSpriteAsset("Assets/Scenes/animations/frames/keep-54947fd1/frame_009.png")
                                  ?? LoadSpriteAsset("Assets/Scenes/art/mainmenulogo.png");
            if (emblemSprite != null)
            {
                GameObject emblemGO = new GameObject("CenterEmblem");
                centerEmblemRT = emblemGO.AddComponent<RectTransform>();
                centerEmblemRT.SetParent(centerContainerRT, false);
                centerEmblemRT.sizeDelta = new Vector2(540f, 304f);
                Image emblemImg = emblemGO.AddComponent<Image>();
                emblemImg.sprite = emblemSprite;
                emblemImg.color = Color.white;
                emblemImg.raycastTarget = false;
            }

            // 4. Satisfying Cyber-Gothic Glassmorphic Capsule Progress Bar Section
            // Container Panel for Loading HUD
            GameObject barContainerGO = new GameObject("ProgressBarHUDSection");
            RectTransform hudSectionRT = barContainerGO.AddComponent<RectTransform>();
            hudSectionRT.SetParent(backdropRT, false);
            hudSectionRT.anchorMin = new Vector2(0.5f, 0.315f);
            hudSectionRT.anchorMax = new Vector2(0.5f, 0.315f);
            hudSectionRT.sizeDelta = new Vector2(BAR_MAX_WIDTH + 60f, 90f);

            // Dynamic Arcane Status Phase Subtitle (Upper Left/Center)
            statusPhaseTextComp = UIFactory.CreateText(
                hudSectionRT, "StatusPhaseText", "✦ CONJURING VOID ESSENCE ✦", 12.5f,
                new Color(0.85f, 0.70f, 1.0f, 0.95f), TextAlignmentOptions.Left);
            statusPhaseTextComp.characterSpacing = 3.2f;
            RectTransform statusRT = statusPhaseTextComp.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0f, 1f);
            statusRT.anchorMax = new Vector2(0.7f, 1f);
            statusRT.pivot = new Vector2(0f, 1f);
            statusRT.anchoredPosition = new Vector2(30f, -6f);
            statusRT.sizeDelta = new Vector2(450f, 24f);

            // Progress Percentage Readout (Upper Right HUD badge)
            progressTextComp = UIFactory.CreateText(
                hudSectionRT, "ProgressText", "<size=16><b>0</b></size><size=11><color=#D47BFF>%</color></size>", 15f,
                Color.white, TextAlignmentOptions.Right);
            RectTransform ptRT = progressTextComp.GetComponent<RectTransform>();
            ptRT.anchorMin = new Vector2(0.7f, 1f);
            ptRT.anchorMax = new Vector2(1f, 1f);
            ptRT.pivot = new Vector2(1f, 1f);
            ptRT.anchoredPosition = new Vector2(-30f, -6f);
            ptRT.sizeDelta = new Vector2(180f, 24f);

            // Ambient Glow Underlay Behind Bar (Breathing Violet Bloom)
            GameObject glowUnderlayGO = new GameObject("BarAmbientGlow");
            barAmbientGlowRT = glowUnderlayGO.AddComponent<RectTransform>();
            barAmbientGlowRT.SetParent(hudSectionRT, false);
            barAmbientGlowRT.anchorMin = new Vector2(0.5f, 0.35f);
            barAmbientGlowRT.anchorMax = new Vector2(0.5f, 0.35f);
            barAmbientGlowRT.sizeDelta = new Vector2(BAR_MAX_WIDTH + 60f, BAR_HEIGHT + 28f);
            barAmbientGlowImg = glowUnderlayGO.AddComponent<Image>();
            barAmbientGlowImg.sprite = softBokehSprite;
            barAmbientGlowImg.color = new Color(0.68f, 0.20f, 0.98f, 0.30f);
            barAmbientGlowImg.raycastTarget = false;

            // Outer Capsule Glass Frame (Smooth Rounded Pill)
            GameObject barTrackGO = new GameObject("ProgressBarTrack");
            barTrackRT = barTrackGO.AddComponent<RectTransform>();
            barTrackRT.SetParent(hudSectionRT, false);
            barTrackRT.anchorMin = new Vector2(0.5f, 0.35f);
            barTrackRT.anchorMax = new Vector2(0.5f, 0.35f);
            barTrackRT.sizeDelta = new Vector2(BAR_MAX_WIDTH, BAR_HEIGHT);

            Image trackImg = barTrackGO.AddComponent<Image>();
            trackImg.sprite = UIFactory.GetRoundedSprite();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(0.05f, 0.02f, 0.12f, 0.94f); // Deep translucent obsidian glass

            // Glowing Purple Outline
            var trackOutline = barTrackGO.AddComponent<Outline>();
            trackOutline.effectColor = new Color(0.78f, 0.35f, 1.0f, 0.70f);
            trackOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Subtle Glass Milestone Ticks at 25%, 50%, 75%
            float[] tickMarks = new float[] { 0.25f, 0.50f, 0.75f };
            for (int i = 0; i < tickMarks.Length; i++)
            {
                GameObject tickGO = new GameObject($"MilestoneTick_{i}");
                RectTransform tickRT = tickGO.AddComponent<RectTransform>();
                tickRT.SetParent(barTrackRT, false);
                tickRT.anchorMin = new Vector2(tickMarks[i], 0.5f);
                tickRT.anchorMax = new Vector2(tickMarks[i], 0.5f);
                tickRT.sizeDelta = new Vector2(1.5f, BAR_HEIGHT - 6f);
                Image tImg = tickGO.AddComponent<Image>();
                tImg.color = new Color(0.85f, 0.55f, 1.0f, 0.22f);
                tImg.raycastTarget = false;
            }

            // RectMask2D Mask Container (Clips liquid fill & sheen to rounded capsule)
            GameObject maskContainerGO = new GameObject("MaskContainer");
            RectTransform maskRT = maskContainerGO.AddComponent<RectTransform>();
            maskRT.SetParent(barTrackRT, false);
            maskRT.anchorMin = Vector2.zero;
            maskRT.anchorMax = Vector2.one;
            maskRT.sizeDelta = Vector2.zero;
            maskContainerGO.AddComponent<RectMask2D>();

            // Progress Bar Liquid Gradient Fill
            GameObject barFillGO = new GameObject("ProgressBarFill");
            RectTransform barFillRT = barFillGO.AddComponent<RectTransform>();
            barFillRT.SetParent(maskRT, false);
            barFillRT.anchorMin = new Vector2(0f, 0f);
            barFillRT.anchorMax = new Vector2(0f, 1f);
            barFillRT.pivot = new Vector2(0f, 0.5f);
            barFillRT.anchoredPosition = Vector2.zero;
            barFillRT.sizeDelta = new Vector2(0f, 0f);

            progressBarFill = barFillGO.AddComponent<Image>();
            progressBarFill.sprite = gradientBarSprite;
            progressBarFill.type = Image.Type.Simple;
            progressBarFill.color = Color.white;

            // Sweeping Specular Sheen (Sliding highlight shimmer)
            GameObject sheenGO = new GameObject("SweepingSheen");
            sheenRT = sheenGO.AddComponent<RectTransform>();
            sheenRT.SetParent(maskRT, false);
            sheenRT.anchorMin = new Vector2(0f, 0.5f);
            sheenRT.anchorMax = new Vector2(0f, 0.5f);
            sheenRT.pivot = new Vector2(0.5f, 0.5f);
            sheenRT.sizeDelta = new Vector2(90f, BAR_HEIGHT);
            sheenImg = sheenGO.AddComponent<Image>();
            sheenImg.sprite = sheenSprite;
            sheenImg.color = new Color(1f, 1f, 1f, 0.55f);
            sheenImg.raycastTarget = false;

            // Traveling Leading-Edge Plasma Comet Core
            GameObject sparkGO = new GameObject("LeadingPlasmaCore");
            barSparkRT = sparkGO.AddComponent<RectTransform>();
            barSparkRT.SetParent(barFillRT, false);
            barSparkRT.anchorMin = new Vector2(1.0f, 0.5f);
            barSparkRT.anchorMax = new Vector2(1.0f, 0.5f);
            barSparkRT.pivot = new Vector2(0.5f, 0.5f);
            barSparkRT.sizeDelta = new Vector2(36f, 36f);

            // Halo Bloom Layer
            GameObject haloGO = new GameObject("HaloBloom");
            RectTransform haloRT = haloGO.AddComponent<RectTransform>();
            haloRT.SetParent(barSparkRT, false);
            haloRT.anchorMin = new Vector2(0.5f, 0.5f);
            haloRT.anchorMax = new Vector2(0.5f, 0.5f);
            haloRT.sizeDelta = new Vector2(36f, 36f);
            barSparkHaloImg = haloGO.AddComponent<Image>();
            barSparkHaloImg.sprite = softBokehSprite;
            barSparkHaloImg.color = new Color(0.92f, 0.35f, 1.0f, 0.65f);
            barSparkHaloImg.raycastTarget = false;

            // White-hot Center Core
            GameObject coreGO = new GameObject("WhiteHotSparkCore");
            RectTransform coreRT = coreGO.AddComponent<RectTransform>();
            coreRT.SetParent(barSparkRT, false);
            coreRT.anchorMin = new Vector2(0.5f, 0.5f);
            coreRT.anchorMax = new Vector2(0.5f, 0.5f);
            coreRT.sizeDelta = new Vector2(16f, 16f);
            barSparkCoreImg = coreGO.AddComponent<Image>();
            barSparkCoreImg.sprite = softBokehSprite;
            barSparkCoreImg.color = new Color(1.0f, 0.98f, 1.0f, 0.95f);
            barSparkCoreImg.raycastTarget = false;

            // 5. Whispers of the Void (Glassmorphic Lore Card)
            RectTransform loreCardRT = UIFactory.CreatePanel(
                backdropRT, "LoreCardPanel", new Color(0.08f, 0.04f, 0.16f, 0.85f),
                new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.14f));
            loreCardRT.sizeDelta = new Vector2(780f, 84f);

            var loreOutline = loreCardRT.gameObject.AddComponent<Outline>();
            loreOutline.effectColor = new Color(0.66f, 0.33f, 0.97f, 0.40f);
            loreOutline.effectDistance = new Vector2(1f, -1f);

            // Lore Card Header Title
            TextMeshProUGUI loreTitleComp = UIFactory.CreateText(
                loreCardRT, "LoreHeader", "✦ WHISPERS OF THE VOID ✦", 11.5f,
                new Color(0.85f, 0.70f, 1.0f, 0.85f), TextAlignmentOptions.Center);
            loreTitleComp.characterSpacing = 2.5f;
            RectTransform ltRT = loreTitleComp.GetComponent<RectTransform>();
            ltRT.anchoredPosition = new Vector2(0f, 22f);

            // Random Gameplay Lore Tip Text
            tipTextComp = UIFactory.CreateText(
                loreCardRT, "LoreTipText", "Entering the Realm of Chaos...", 14f,
                new Color(0.92f, 0.94f, 0.98f, 0.92f), TextAlignmentOptions.Center);
            RectTransform tipRT = tipTextComp.GetComponent<RectTransform>();
            tipRT.anchoredPosition = new Vector2(0f, -8f);
            tipRT.sizeDelta = new Vector2(740f, 44f);
        }

        private void SpawnFloatingOrbs(Transform parent)
        {
            activeOrbs.Clear();
            int count = 22;

            Color[] palette = new Color[]
            {
                new Color(0.65f, 0.20f, 0.95f), // Radiant violet
                new Color(0.85f, 0.35f, 1.00f), // Neon purple
                new Color(0.92f, 0.50f, 0.98f), // Ethereal magenta
                new Color(0.48f, 0.15f, 0.85f), // Deep amethyst
                new Color(0.78f, 0.45f, 0.90f)  // Soft lilac
            };

            for (int i = 0; i < count; i++)
            {
                GameObject orbGO = new GameObject($"LoadingOrb_{i}");
                orbGO.transform.SetParent(parent, false);

                Image img = orbGO.AddComponent<Image>();
                img.sprite = softBokehSprite;
                img.raycastTarget = false;

                RectTransform rt = orbGO.GetComponent<RectTransform>();

                float layerRoll = Random.value;
                float size;
                float alpha;
                float speed;

                if (layerRoll < 0.4f)
                {
                    size = Random.Range(45f, 75f);
                    alpha = Random.Range(0.18f, 0.35f);
                    speed = Random.Range(24f, 45f);
                }
                else if (layerRoll < 0.8f)
                {
                    size = Random.Range(78f, 130f);
                    alpha = Random.Range(0.24f, 0.44f);
                    speed = Random.Range(36f, 60f);
                }
                else
                {
                    size = Random.Range(135f, 210f);
                    alpha = Random.Range(0.28f, 0.48f);
                    speed = Random.Range(48f, 72f);
                }

                rt.sizeDelta = new Vector2(size, size);
                float initialX = Random.Range(-920f, 920f);
                float initialY = Random.Range(-540f, 540f);
                rt.anchoredPosition = new Vector2(initialX, initialY);

                Color chosenColor = palette[Random.Range(0, palette.Length)];
                img.color = new Color(chosenColor.r, chosenColor.g, chosenColor.b, alpha);

                activeOrbs.Add(new LoadingOrb
                {
                    rt = rt,
                    img = img,
                    speedY = speed,
                    baseX = initialX,
                    swayAmp = Random.Range(16f, 38f),
                    swayFreq = Random.Range(0.6f, 1.4f),
                    swayPhase = Random.Range(0f, Mathf.PI * 2f),
                    baseAlpha = alpha,
                    baseColor = chosenColor
                });
            }
        }

        private void UpdateFloatingOrbs(float dt, float time)
        {
            for (int i = 0; i < activeOrbs.Count; i++)
            {
                LoadingOrb orb = activeOrbs[i];
                Vector2 pos = orb.rt.anchoredPosition;

                pos.y += orb.speedY * dt;
                pos.x = orb.baseX + Mathf.Sin(time * orb.swayFreq + orb.swayPhase) * orb.swayAmp;

                if (pos.y > 560f)
                {
                    pos.y = -560f;
                    orb.baseX = Random.Range(-920f, 920f);
                    pos.x = orb.baseX;
                }

                orb.rt.anchoredPosition = pos;
                float pulse = 0.75f + 0.25f * Mathf.Sin(time * 1.8f + orb.swayPhase);
                orb.img.color = new Color(orb.baseColor.r, orb.baseColor.g, orb.baseColor.b, orb.baseAlpha * pulse);
            }
        }

        private IEnumerator LoadSceneAsyncRoutine(string sceneName)
        {
            Time.timeScale = 1f;
            canvasGroup.blocksRaycasts = true;
            progressVelocity = 0f;
            sheenTimer = 0f;

            // Select random tip
            if (tipTextComp != null)
            {
                tipTextComp.text = LoreTips[Random.Range(0, LoreTips.Length)];
            }

            // Fade in
            float fade = 0f;
            while (fade < 0.35f)
            {
                fade += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(fade / 0.35f);
                UpdateFloatingOrbs(Time.unscaledDeltaTime, Time.unscaledTime);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Start asynchronous load
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float currentProgress = 0f;

            while (!op.isDone)
            {
                float dt = Time.unscaledDeltaTime;
                float time = Time.unscaledTime;

                // Animate rotating arcane seals
                if (outerRuneSealRT != null)
                {
                    outerRuneSealRT.Rotate(0f, 0f, 24f * dt);
                }
                if (innerRuneSealRT != null)
                {
                    innerRuneSealRT.Rotate(0f, 0f, -48f * dt);
                }

                // Breathing scale on centerpiece emblem
                if (centerEmblemRT != null)
                {
                    float scale = 1.0f + 0.024f * Mathf.Sin(time * 2.2f);
                    centerEmblemRT.localScale = new Vector3(scale, scale, 1.0f);
                }

                // Update living void orbs
                UpdateFloatingOrbs(dt, time);

                // Normal progress ranges from 0 to 0.9 before activation
                float targetProgress = Mathf.Clamp01(op.progress / 0.9f);
                // Satisfying smooth damping spring interpolation for juicy, weighted advancement
                currentProgress = Mathf.SmoothDamp(currentProgress, targetProgress, ref progressVelocity, 0.16f, 3.2f, dt);

                float currentWidth = currentProgress * BAR_MAX_WIDTH;

                if (progressBarFill != null)
                {
                    progressBarFill.rectTransform.sizeDelta = new Vector2(currentWidth, BAR_HEIGHT);
                }

                // Animate sweeping specular sheen across the liquid fill
                if (sheenRT != null)
                {
                    sheenTimer += dt * 1.35f;
                    if (sheenTimer > 2.8f) sheenTimer = 0f;
                    float sheenT = Mathf.Clamp01(sheenTimer / 1.5f);
                    float sheenX = Mathf.Lerp(-40f, Mathf.Max(currentWidth + 30f, 10f), sheenT);
                    sheenRT.anchoredPosition = new Vector2(sheenX, 0f);
                    if (sheenImg != null)
                    {
                        sheenImg.color = new Color(1f, 1f, 1f, (sheenT < 0.9f ? 0.50f : (1f - sheenT) * 5f * 0.50f));
                    }
                }

                // Ambient glow breathing pulse
                if (barAmbientGlowImg != null)
                {
                    float glowAlpha = 0.26f + 0.12f * Mathf.Sin(time * 3.5f);
                    barAmbientGlowImg.color = new Color(0.70f, 0.22f, 1.0f, glowAlpha);
                }

                // Plasma comet head scaling & pulse
                if (barSparkRT != null)
                {
                    float pulse = 1.0f + 0.22f * Mathf.Sin(time * 7f);
                    barSparkRT.localScale = Vector3.one * pulse;
                }
                if (barSparkHaloImg != null)
                {
                    float haloAlpha = 0.50f + 0.25f * Mathf.Cos(time * 8.5f);
                    barSparkHaloImg.color = new Color(0.92f, 0.35f, 1.0f, haloAlpha);
                }

                // Modern percentage readout
                if (progressTextComp != null)
                {
                    int pct = Mathf.Clamp(Mathf.RoundToInt(currentProgress * 100f), 0, 100);
                    progressTextComp.text = $"<size=17><b>{pct}</b></size><size=11><color=#D47BFF>%</color></size>";
                }

                // Arcane status phase labels
                if (statusPhaseTextComp != null)
                {
                    if (currentProgress < 0.32f)
                        statusPhaseTextComp.text = "✦ CONJURING VOID ESSENCE ✦";
                    else if (currentProgress < 0.72f)
                        statusPhaseTextComp.text = "✦ WEAVING REALM PARTICLES ✦";
                    else if (currentProgress < 0.96f)
                        statusPhaseTextComp.text = "✦ ALIGNING MULTIVERSE GATEWAYS ✦";
                    else
                        statusPhaseTextComp.text = "✦ REALM MANIFESTED ✦";
                }

                if (op.progress >= 0.9f && currentProgress >= 0.985f)
                {
                    break;
                }

                yield return null;
            }

            if (progressBarFill != null) progressBarFill.rectTransform.sizeDelta = new Vector2(BAR_MAX_WIDTH, BAR_HEIGHT);
            if (progressTextComp != null) progressTextComp.text = "<size=17><b>100</b></size><size=11><color=#D47BFF>%</color></size>";
            if (statusPhaseTextComp != null) statusPhaseTextComp.text = "✦ REALM MANIFESTED ✦";

            yield return new WaitForSecondsRealtime(0.25f);

            op.allowSceneActivation = true;

            // Wait until scene actually loads
            while (!op.isDone)
            {
                yield return null;
            }

            // Fade out
            fade = 0.38f;
            while (fade > 0f)
            {
                fade -= Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(fade / 0.38f);
                UpdateFloatingOrbs(Time.unscaledDeltaTime, Time.unscaledTime);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            isCurrentlyLoading = false;
        }
    }
}
