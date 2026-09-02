using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Singleton UI Modal Controller for the Shadow Runner endless arcade minigame.
    /// Builds fullscreen dark overlay, 800x400 RawImage display, HUD distance bar,
    /// control guide start menu, and game over retry panel.
    /// </summary>
    public class ShadowRunnerArcadeUI : MonoBehaviour
    {
        public static ShadowRunnerArcadeUI Instance { get; private set; }

        private GameObject modal;
        private ShadowRunnerEngine engine;
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
                GameObject go = new GameObject("ShadowRunnerArcadeManager");
                Instance = go.AddComponent<ShadowRunnerArcadeUI>();
            }
            Instance.Open();
        }

        public void Open()
        {
            OrientationManager.SetLandscape();
            if (modal == null) BuildUI();
            modal.SetActive(true);
            if (startPanel != null) startPanel.SetActive(true);
            if (overPanel != null) overPanel.SetActive(false);
        }

        public void Close()
        {
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
            // Canvas at sort order 25
            Canvas canvas = UIFactory.CreateCanvas("ShadowRunnerArcadeCanvas", 25);
            canvas.transform.SetParent(transform, false);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.matchWidthOrHeight = 1.0f;

            // Fullscreen overlay
            RectTransform overlay = UIFactory.CreateFullScreenPanel(
                canvas.transform, "MinigameOverlay", new Color(0.015f, 0.025f, 0.06f, 0.92f));
            modal = overlay.gameObject;

            // Centered Arcade Frame
            RectTransform frame = UIFactory.CreatePanel(overlay, "ArcadeFrame",
                new Color(0.04f, 0.03f, 0.09f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            frame.sizeDelta = new Vector2(860f, 520f);

            // RawImage Display (800x400)
            GameObject rawGO = new GameObject("GameDisplay", typeof(RectTransform), typeof(RawImage));
            rawGO.transform.SetParent(frame, false);
            RectTransform rawRT = rawGO.GetComponent<RectTransform>();
            rawRT.anchorMin = new Vector2(0.5f, 0.5f);
            rawRT.anchorMax = new Vector2(0.5f, 0.5f);
            rawRT.sizeDelta = new Vector2(800f, 400f);
            rawRT.anchoredPosition = new Vector2(0f, -20f);
            RawImage rawImg = rawGO.GetComponent<RawImage>();
            rawImg.color = Color.white;
            rawImg.material = UIFactory.GetArcadeCRTMaterial();

            // HUD Top Bar
            RectTransform hudBar = UIFactory.CreatePanel(frame, "HUD",
                new Color(0.02f, 0.03f, 0.08f, 0.85f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            hudBar.sizeDelta = new Vector2(800f, 45f);
            hudBar.anchoredPosition = new Vector2(0f, -10f);

            TextMeshProUGUI distLabel = UIFactory.CreateText(hudBar, "DistLabel", "DISTANCE", 11f,
                new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(distLabel.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, 8f), new Vector2(120f, 16f));

            TextMeshProUGUI distVal = UIFactory.CreateText(hudBar, "DistVal", "0m", 22f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Left);
            distVal.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(distVal.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, -10f), new Vector2(150f, 30f));

            TextMeshProUGUI bestLabel = UIFactory.CreateText(hudBar, "BestLabel", "BEST DISTANCE", 11f,
                new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Right);
            UIFactory.SetRectFixed(bestLabel.rectTransform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-75f, 8f), new Vector2(120f, 16f));

            TextMeshProUGUI bestVal = UIFactory.CreateText(hudBar, "BestVal", "0m", 18f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Right);
            UIFactory.SetRectFixed(bestVal.rectTransform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-75f, -10f), new Vector2(150f, 26f));

            // Close button
            MakeBtn(frame, "CloseBtn", "✕ CLOSE", new Vector2(110f, 32f), new Vector2(350f, 235f), () => Close());

            // Engine attachment
            // ── Mobile Controls Bar ──
            GameObject controlsBar = new GameObject("MobileControlsBar", typeof(RectTransform));
            controlsBar.transform.SetParent(frame, false);
            RectTransform barRT = controlsBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0f);
            barRT.anchorMax = new Vector2(0.5f, 0f);
            barRT.sizeDelta = new Vector2(800f, 65f);
            barRT.anchoredPosition = new Vector2(0f, 35f);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnSlide", "▼ SLIDE", new Vector2(-280f, 0f), new Vector2(160f, 55f), 
                new Color(0.85f, 0.28f, 1f, 1f), (held) => ShadowRunnerEngine.virtualDuck = held);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnJump", "▲ JUMP", new Vector2(280f, 0f), new Vector2(160f, 55f), 
                new Color(0f, 0.94f, 1f, 1f), null, () => ShadowRunnerEngine.virtualJump = true);

            engine = rawGO.AddComponent<ShadowRunnerEngine>();
            engine.displayImage = rawImg;
            engine.distanceText = distVal;
            engine.highScoreText = bestVal;

            // Start Menu Overlay
            RectTransform smrt = UIFactory.CreatePanel(rawGO.transform, "StartMenu",
                new Color(0.02f, 0.04f, 0.10f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            startPanel = smrt.gameObject;

            TextMeshProUGUI title = UIFactory.CreateText(smrt, "Title", "SHADOW RUNNER", 38f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(title.rectTransform,
                new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(500f, 50f));

            TextMeshProUGUI sub = UIFactory.CreateText(smrt, "Sub",
                "High-speed endless runner! Jump spikes, duck bats, and collect gems!",
                14f, new Color(0.7f, 0.8f, 0.95f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(sub.rectTransform,
                new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), Vector2.zero, new Vector2(550f, 40f));

            string rules =
                "  W / Space / Up Arrow = Jump & Double Jump (over spikes)\n" +
                "  S / Down Arrow = Duck / Crouch (under flying bats)\n" +
                "  Collect Cyan Gems for extra distance bonus!";
            TextMeshProUGUI rulesT = UIFactory.CreateText(smrt, "Rules", rules, 13f,
                new Color(0.85f, 0.90f, 1f, 0.9f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(rulesT.rectTransform,
                new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(480f, 100f));

            MakeBtn(smrt, "StartBtn", "START RUN ▶", new Vector2(240f, 48f), new Vector2(0f, -120f), () => Play());

            // Game Over Overlay
            RectTransform gort = UIFactory.CreatePanel(rawGO.transform, "GameOverPanel",
                new Color(0.04f, 0.01f, 0.05f, 0.95f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overPanel = gort.gameObject;

            TextMeshProUGUI goTitle = UIFactory.CreateText(gort, "GOTitle", "RUN OVER!", 38f,
                new Color(1f, 0f, 0.35f, 1f), TextAlignmentOptions.Center);
            goTitle.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(goTitle.rectTransform,
                new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(460f, 50f));

            TextMeshProUGUI goReason = UIFactory.CreateText(gort, "GOReason", "Impaled by Ground Spikes!", 16f,
                new Color(1f, 0.65f, 0.65f, 0.9f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goReason.rectTransform,
                new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(440f, 30f));

            TextMeshProUGUI goDist = UIFactory.CreateText(gort, "FinalDistance", "0m", 42f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
            goDist.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(goDist.rectTransform,
                new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(300f, 50f));

            TextMeshProUGUI goBest = UIFactory.CreateText(gort, "BestRecord", "BEST: 0m", 16f,
                new Color(0.45f, 0.67f, 0.84f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goBest.rectTransform,
                new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(300f, 30f));

            engine.gameOverPanel = overPanel;
            engine.gameOverReasonText = goReason;
            engine.finalDistanceText = goDist;
            engine.bestRecordText = goBest;

            MakeBtn(gort, "RetryBtn", "RUN AGAIN", new Vector2(220f, 46f), new Vector2(0f, -100f), () => Play());

            overPanel.SetActive(false);
            modal.SetActive(false);
        }

        private Button MakeBtn(Transform parent, string name, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            Image img = go.GetComponent<Image>();
            img.color = new Color(0.08f, 0.22f, 0.42f, 0.95f);

            TextMeshProUGUI txt = UIFactory.CreateText(go.transform, "Label", label, 16f, Color.white, TextAlignmentOptions.Center);
            txt.fontStyle = FontStyles.Bold;
            UIFactory.SetRect(txt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.08f, 0.22f, 0.42f, 0.95f);
            cb.highlightedColor = new Color(0.12f, 0.45f, 0.75f, 1f);
            cb.pressedColor = new Color(0.05f, 0.15f, 0.30f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }
    }
}
