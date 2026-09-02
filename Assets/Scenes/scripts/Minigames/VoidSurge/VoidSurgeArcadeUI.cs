using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Singleton UI Modal Controller for the Void Surge neon arcade space combat game.
    /// Builds fullscreen dark overlay, 600x800 RawImage display, HUD score bar,
    /// control guide start menu, and game over retry panel.
    /// Themed in neon cyan/magenta to match the Void Surge aesthetic.
    /// </summary>
    public class VoidSurgeArcadeUI : MonoBehaviour
    {
        public static VoidSurgeArcadeUI Instance { get; private set; }

        private GameObject modal;
        private VoidSurgeEngine engine;
        private GameObject startPanel;
        private GameObject overPanel;

        public bool IsMinigameActive => modal != null && modal.activeSelf;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            BuildUI();
        }

        public static void ShowMinigame()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("VoidSurgeArcadeManager");
                Instance = go.AddComponent<VoidSurgeArcadeUI>();
            }
            Instance.Open();
        }

        public void Open()
        {
            OrientationManager.SetPortrait();
            if (modal == null) BuildUI();
            modal.SetActive(true);
            if (startPanel != null) startPanel.SetActive(true);
            if (overPanel != null) overPanel.SetActive(false);
        }

        public void Close()
        {
            OrientationManager.SetLandscape();
            if (modal != null) modal.SetActive(false);
        }

        public void Play()
        {
            if (startPanel != null) startPanel.SetActive(false);
            if (overPanel != null) overPanel.SetActive(false);
            if (engine != null) engine.StartNewGame();
        }

        private void BuildUI()
        {
            // ── Canvas at sort order 25 ──
            Canvas canvas = UIFactory.CreateCanvas("VoidSurgeArcadeCanvas", 25);
            canvas.transform.SetParent(transform, false);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.matchWidthOrHeight = 1.0f;

            // ── Fullscreen dark overlay ──
            RectTransform overlay = UIFactory.CreateFullScreenPanel(
                canvas.transform, "MinigameOverlay", new Color(0.01f, 0.015f, 0.04f, 0.94f));
            modal = overlay.gameObject;

            // ── Centered Arcade Frame (660x860) ──
            RectTransform frame = UIFactory.CreatePanel(overlay, "ArcadeFrame",
                new Color(0.03f, 0.025f, 0.07f, 0.97f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            frame.sizeDelta = new Vector2(660f, 860f);

            // Frame border accent (thin cyan outline simulated by slightly larger backing panel)
            RectTransform frameBorder = UIFactory.CreatePanel(frame, "FrameBorder",
                new Color(0f, 0.94f, 1f, 0.12f),
                Vector2.zero, Vector2.one, new Vector2(-2f, -2f), new Vector2(2f, 2f));

            // ── RawImage Display (600x800) ──
            GameObject rawGO = new GameObject("GameDisplay", typeof(RectTransform), typeof(RawImage));
            rawGO.transform.SetParent(frame, false);
            RectTransform rawRT = rawGO.GetComponent<RectTransform>();
            rawRT.anchorMin = new Vector2(0.5f, 0.5f);
            rawRT.anchorMax = new Vector2(0.5f, 0.5f);
            rawRT.sizeDelta = new Vector2(600f, 800f);
            rawRT.anchoredPosition = new Vector2(0f, -10f);
            RawImage rawImg = rawGO.GetComponent<RawImage>();
            rawImg.color = Color.white;
            rawImg.material = UIFactory.GetArcadeCRTMaterial();

            // ── HUD Top Bar ──
            RectTransform hudBar = UIFactory.CreatePanel(frame, "HUD",
                new Color(0.015f, 0.02f, 0.06f, 0.9f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            hudBar.sizeDelta = new Vector2(600f, 48f);
            hudBar.anchoredPosition = new Vector2(0f, -10f);

            // Score label & value (left side)
            TextMeshProUGUI scoreLabel = UIFactory.CreateText(hudBar, "ScoreLabel", "SCORE", 10f,
                new Color(0.5f, 0.6f, 0.7f, 0.9f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(scoreLabel.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, 10f), new Vector2(120f, 14f));

            TextMeshProUGUI scoreVal = UIFactory.CreateText(hudBar, "ScoreVal", "0", 24f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Left);
            scoreVal.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(scoreVal.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, -10f), new Vector2(160f, 30f));

            // High score (right side)
            TextMeshProUGUI bestLabel = UIFactory.CreateText(hudBar, "BestLabel", "HIGH SCORE", 10f,
                new Color(0.5f, 0.6f, 0.7f, 0.9f), TextAlignmentOptions.Right);
            UIFactory.SetRectFixed(bestLabel.rectTransform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-75f, 10f), new Vector2(120f, 14f));

            TextMeshProUGUI bestVal = UIFactory.CreateText(hudBar, "BestVal", "0", 20f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Right);
            UIFactory.SetRectFixed(bestVal.rectTransform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-75f, -10f), new Vector2(160f, 28f));

            // Wave display (center)
            TextMeshProUGUI waveLabel = UIFactory.CreateText(hudBar, "WaveLabel", "WAVE", 10f,
                new Color(0.5f, 0.6f, 0.7f, 0.9f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(waveLabel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(80f, 14f));

            TextMeshProUGUI waveVal = UIFactory.CreateText(hudBar, "WaveVal", "1", 20f,
                new Color(1f, 0f, 0.67f, 1f), TextAlignmentOptions.Center);
            waveVal.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(waveVal.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(80f, 28f));

            // ── Close button (top-right) ──
            MakeBtn(frame, "CloseBtn", "✕ CLOSE", new Vector2(110f, 32f), new Vector2(250f, 405f),
                new Color(0.15f, 0.05f, 0.05f, 0.9f), new Color(0.4f, 0.1f, 0.1f, 1f), () => Close());

            // ── Engine attachment ──
            // ── Mobile Controls Bar ──
            GameObject controlsBar = new GameObject("MobileControlsBar", typeof(RectTransform));
            controlsBar.transform.SetParent(frame, false);
            RectTransform barRT = controlsBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0f);
            barRT.anchorMax = new Vector2(0.5f, 0f);
            barRT.sizeDelta = new Vector2(600f, 75f);
            barRT.anchoredPosition = new Vector2(0f, 42f);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnLeft", "◀", new Vector2(-220f, 0f), new Vector2(85f, 55f), 
                new Color(0f, 0.94f, 1f, 1f), (held) => VoidSurgeEngine.virtualLeft = held);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnRight", "▶", new Vector2(-120f, 0f), new Vector2(85f, 55f), 
                new Color(0f, 0.94f, 1f, 1f), (held) => VoidSurgeEngine.virtualRight = held);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnFire", "⚡ FIRE", new Vector2(100f, 0f), new Vector2(120f, 55f), 
                new Color(0.2f, 1f, 0.6f, 1f), (held) => VoidSurgeEngine.virtualFire = held);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnBomb", "💥 BOMB", new Vector2(230f, 0f), new Vector2(100f, 55f), 
                new Color(1f, 0f, 0.67f, 1f), null, () => VoidSurgeEngine.virtualBomb = true);

            engine = rawGO.AddComponent<VoidSurgeEngine>();
            engine.displayImage = rawImg;
            engine.scoreText = scoreVal;
            engine.highScoreText = bestVal;
            engine.waveText = waveVal;

            // ══════════════════════════════════════
            // START MENU OVERLAY
            // ══════════════════════════════════════
            RectTransform smrt = UIFactory.CreatePanel(rawGO.transform, "StartMenu",
                new Color(0.015f, 0.02f, 0.06f, 0.97f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            startPanel = smrt.gameObject;

            // Title: VOID SURGE
            TextMeshProUGUI title = UIFactory.CreateText(smrt, "Title", "VOID SURGE", 42f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(title.rectTransform,
                new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(500f, 55f));

            // Subtitle
            TextMeshProUGUI sub = UIFactory.CreateText(smrt, "Sub",
                "NEON SPACE COMBAT",
                12f, new Color(1f, 0f, 0.67f, 0.8f), TextAlignmentOptions.Center);
            sub.fontStyle = FontStyles.Bold;
            sub.characterSpacing = 12f;
            UIFactory.SetRectFixed(sub.rectTransform,
                new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(500f, 24f));

            // Description
            TextMeshProUGUI desc = UIFactory.CreateText(smrt, "Desc",
                "Battle waves of enemies, defeat bosses, collect power-ups,\nand climb the leaderboard in this intense neon shooter!",
                13f, new Color(0.65f, 0.72f, 0.85f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(desc.rectTransform,
                new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(480f, 50f));

            // Divider line
            RectTransform divider = UIFactory.CreatePanel(smrt, "Divider",
                new Color(0f, 0.94f, 1f, 0.15f),
                new Vector2(0.5f, 0.60f), new Vector2(0.5f, 0.60f));
            divider.sizeDelta = new Vector2(400f, 1f);

            // Controls
            string rules =
                "  A / D  or  ← / →   =  Navigate Ship\n" +
                "  W / S  or  ↑ / ↓   =  Adjust Altitude\n" +
                "  Space / J / Left Click  =  Fire Weapons\n" +
                "  K / Left Shift  =  Nova Bomb (Clears Screen)";
            TextMeshProUGUI rulesT = UIFactory.CreateText(smrt, "Rules", rules, 13f,
                new Color(0.78f, 0.84f, 0.95f, 0.9f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(rulesT.rectTransform,
                new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(460f, 120f));

            // Power-up legend
            string powerups =
                "◆ Shield Restore   ◆ Weapon Upgrade   ◆ Nova Bomb";
            TextMeshProUGUI puText = UIFactory.CreateText(smrt, "Powerups", powerups, 11f,
                new Color(0.5f, 0.6f, 0.75f, 0.7f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(puText.rectTransform,
                new Vector2(0.5f, 0.30f), new Vector2(0.5f, 0.30f), Vector2.zero, new Vector2(480f, 24f));

            // Launch button
            MakeBtn(smrt, "StartBtn", "LAUNCH MISSION ▶", new Vector2(260f, 52f), new Vector2(0f, -140f),
                new Color(0f, 0.25f, 0.35f, 0.95f), new Color(0f, 0.45f, 0.6f, 1f), () => Play());

            // ══════════════════════════════════════
            // GAME OVER OVERLAY
            // ══════════════════════════════════════
            RectTransform gort = UIFactory.CreatePanel(rawGO.transform, "GameOverPanel",
                new Color(0.03f, 0.01f, 0.04f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overPanel = gort.gameObject;

            // Title
            TextMeshProUGUI goTitle = UIFactory.CreateText(gort, "GOTitle", "MISSION FAILED", 40f,
                new Color(1f, 0.13f, 0.27f, 1f), TextAlignmentOptions.Center);
            goTitle.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(goTitle.rectTransform,
                new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(500f, 55f));

            // Reason
            TextMeshProUGUI goReason = UIFactory.CreateText(gort, "GOReason", "Hull integrity lost!", 15f,
                new Color(1f, 0.55f, 0.55f, 0.9f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goReason.rectTransform,
                new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(440f, 30f));

            // Score label
            TextMeshProUGUI goScoreLabel = UIFactory.CreateText(gort, "ScoreLabel", "FINAL SCORE", 11f,
                new Color(0.5f, 0.6f, 0.7f, 0.8f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goScoreLabel.rectTransform,
                new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(300f, 20f));

            // Score value
            TextMeshProUGUI goScore = UIFactory.CreateText(gort, "FinalScore", "0", 46f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
            goScore.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(goScore.rectTransform,
                new Vector2(0.5f, 0.50f), new Vector2(0.5f, 0.50f), Vector2.zero, new Vector2(300f, 55f));

            // Best record
            TextMeshProUGUI goBest = UIFactory.CreateText(gort, "BestRecord", "BEST: 0", 15f,
                new Color(1f, 0f, 0.67f, 0.8f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goBest.rectTransform,
                new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(300f, 26f));

            // Wave reached
            TextMeshProUGUI goWave = UIFactory.CreateText(gort, "WaveReached", "WAVE REACHED: 1", 13f,
                new Color(0.5f, 0.6f, 0.7f, 0.7f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goWave.rectTransform,
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(300f, 22f));

            // Connect engine references
            engine.gameOverPanel = overPanel;
            engine.gameOverReasonText = goReason;
            engine.finalScoreText = goScore;
            engine.bestRecordText = goBest;
            engine.waveReachedText = goWave;

            // Retry button
            MakeBtn(gort, "RetryBtn", "RETRY MISSION ↻", new Vector2(240f, 50f), new Vector2(0f, -120f),
                new Color(0f, 0.25f, 0.35f, 0.95f), new Color(0f, 0.45f, 0.6f, 1f), () => Play());

            overPanel.SetActive(false);
            modal.SetActive(false);
        }

        private Button MakeBtn(Transform parent, string name, string label, Vector2 size, Vector2 pos,
            Color normalColor, Color hoverColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            Image img = go.GetComponent<Image>();
            img.color = normalColor;

            // Rounded corners if sprite available
            Sprite roundedSprite = UIFactory.GetRoundedSprite();
            if (roundedSprite != null)
            {
                img.sprite = roundedSprite;
                img.type = Image.Type.Sliced;
            }

            TextMeshProUGUI txt = UIFactory.CreateText(go.transform, "Label", label, 15f,
                new Color(0.9f, 0.95f, 1f, 1f), TextAlignmentOptions.Center);
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 2f;
            UIFactory.SetRect(txt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = normalColor;
            cb.highlightedColor = hoverColor;
            cb.pressedColor = new Color(normalColor.r * 0.6f, normalColor.g * 0.6f, normalColor.b * 0.6f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }
    }
}
