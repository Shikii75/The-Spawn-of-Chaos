using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Singleton UI controller for the Skybound Box arcade minigame.
    /// Creates a fullscreen modal with a RawImage game display, HUD timer, 
    /// start menu, and game over overlay. All UI built programmatically.
    /// </summary>
    public class SkyboundArcadeUI : MonoBehaviour
    {
        public static SkyboundArcadeUI Instance { get; private set; }

        private GameObject modal;
        private SkyboundBoxEngine engine;
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
                GameObject go = new GameObject("SkyboundArcadeManager");
                Instance = go.AddComponent<SkyboundArcadeUI>();
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
            // Canvas at sort order 25 (above pause menu)
            Canvas canvas = UIFactory.CreateCanvas("SkyboundArcadeCanvas", 25);
            canvas.transform.SetParent(transform, false);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.matchWidthOrHeight = 1.0f;

            // Fullscreen dark overlay
            RectTransform overlay = UIFactory.CreateFullScreenPanel(
                canvas.transform, "MinigameOverlay", new Color(0.015f, 0.025f, 0.06f, 0.92f));
            modal = overlay.gameObject;

            // Outer frame panel
            RectTransform frame = UIFactory.CreatePanel(overlay, "ArcadeFrame",
                new Color(0.04f, 0.03f, 0.09f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            frame.sizeDelta = new Vector2(560f, 760f);

            // ── RawImage game display ──
            GameObject rawGO = new GameObject("GameDisplay", typeof(RectTransform), typeof(RawImage));
            rawGO.transform.SetParent(frame, false);
            RectTransform rawRT = rawGO.GetComponent<RectTransform>();
            rawRT.anchorMin = new Vector2(0.5f, 0.5f);
            rawRT.anchorMax = new Vector2(0.5f, 0.5f);
            rawRT.sizeDelta = new Vector2(480f, 640f);
            rawRT.anchoredPosition = new Vector2(0f, -20f);
            RawImage rawImg = rawGO.GetComponent<RawImage>();
            rawImg.color = Color.white;
            rawImg.material = UIFactory.GetArcadeCRTMaterial();

            // ── HUD bar at top of frame ──
            RectTransform hudBar = UIFactory.CreatePanel(frame, "HUD",
                new Color(0.02f, 0.03f, 0.08f, 0.85f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            hudBar.sizeDelta = new Vector2(520f, 50f);
            hudBar.anchoredPosition = new Vector2(0f, -10f);

            TextMeshProUGUI timeLabel = UIFactory.CreateText(hudBar, "TimeLabel", "TIME ALIVE", 11f,
                new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(timeLabel.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, 8f), new Vector2(120f, 16f));

            TextMeshProUGUI timeVal = UIFactory.CreateText(hudBar, "TimeVal", "0.00s", 24f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Left);
            timeVal.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(timeVal.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, -10f), new Vector2(150f, 30f));

            TextMeshProUGUI bestLabel = UIFactory.CreateText(hudBar, "BestLabel", "BEST RECORD", 11f,
                new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Right);
            UIFactory.SetRectFixed(bestLabel.rectTransform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-75f, 8f), new Vector2(120f, 16f));

            TextMeshProUGUI bestVal = UIFactory.CreateText(hudBar, "BestVal", "0.00s", 18f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Right);
            UIFactory.SetRectFixed(bestVal.rectTransform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-75f, -10f), new Vector2(150f, 26f));

            // ── Close button ──
            Button closeBtn = MakeBtn(frame, "CloseBtn", "✕ CLOSE", new Vector2(110f, 34f), new Vector2(205f, 355f), () => Close());

            // ── Attach engine to the RawImage GO ──
            // ── Mobile Controls Bar ──
            GameObject controlsBar = new GameObject("MobileControlsBar", typeof(RectTransform));
            controlsBar.transform.SetParent(frame, false);
            RectTransform barRT = controlsBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0f);
            barRT.anchorMax = new Vector2(0.5f, 0f);
            barRT.sizeDelta = new Vector2(500f, 75f);
            barRT.anchoredPosition = new Vector2(0f, 42f);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnLeft", "◀", new Vector2(-150f, 0f), new Vector2(100f, 60f), 
                new Color(0f, 0.94f, 1f, 1f), (held) => SkyboundBoxEngine.virtualLeft = held);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnJump", "▲ JUMP", new Vector2(0f, 0f), new Vector2(140f, 60f), 
                new Color(0.2f, 1f, 0.6f, 1f), null, () => SkyboundBoxEngine.virtualJump = true);

            ArcadeTouchButton.Create(controlsBar.transform, "BtnRight", "▶", new Vector2(150f, 0f), new Vector2(100f, 60f), 
                new Color(0f, 0.94f, 1f, 1f), (held) => SkyboundBoxEngine.virtualRight = held);

            engine = rawGO.AddComponent<SkyboundBoxEngine>();
            engine.displayImage = rawImg;
            engine.timerText = timeVal;
            engine.highScoreText = bestVal;

            // ── Start Menu Overlay ──
            RectTransform smrt = UIFactory.CreatePanel(rawGO.transform, "StartMenu",
                new Color(0.02f, 0.04f, 0.10f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            startPanel = smrt.gameObject;

            TextMeshProUGUI title = UIFactory.CreateText(smrt, "Title", "SKYBOUND BOX", 42f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(title.rectTransform,
                new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(460f, 60f));

            TextMeshProUGUI sub = UIFactory.CreateText(smrt, "Sub",
                "Fall onto rising platforms.\nSurvive the fatal top & bottom zones!",
                15f, new Color(0.7f, 0.8f, 0.95f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(sub.rectTransform,
                new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(440f, 50f));

            string rules =
                "  LAND on rising platforms to stay alive\n" +
                "  LEFT & RIGHT walls are SAFE (bounce)\n" +
                "  TOP & BOTTOM are FATAL (box shatters!)\n" +
                "  Score is measured in survival seconds\n\n" +
                "  A/D or Arrows = Move   |   W/Space = Jump";
            TextMeshProUGUI rulesT = UIFactory.CreateText(smrt, "Rules", rules, 13f,
                new Color(0.85f, 0.90f, 1f, 0.9f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(rulesT.rectTransform,
                new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(400f, 180f));

            MakeBtn(smrt, "StartBtn", "START GAME", new Vector2(260f, 56f), new Vector2(0f, -200f), () => Play());

            // ── Game Over Overlay ──
            RectTransform gort = UIFactory.CreatePanel(rawGO.transform, "GameOverPanel",
                new Color(0.04f, 0.01f, 0.05f, 0.95f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overPanel = gort.gameObject;

            TextMeshProUGUI goTitle = UIFactory.CreateText(gort, "GOTitle", "GAME OVER", 40f,
                new Color(1f, 0f, 0.35f, 1f), TextAlignmentOptions.Center);
            goTitle.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(goTitle.rectTransform,
                new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(460f, 60f));

            TextMeshProUGUI goReason = UIFactory.CreateText(gort, "GOReason", "Crushed by Ceiling!", 17f,
                new Color(1f, 0.65f, 0.65f, 0.9f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goReason.rectTransform,
                new Vector2(0.5f, 0.69f), new Vector2(0.5f, 0.69f), Vector2.zero, new Vector2(440f, 30f));

            TextMeshProUGUI goTime = UIFactory.CreateText(gort, "FinalTime", "0.00s", 48f,
                new Color(0f, 0.94f, 1f, 1f), TextAlignmentOptions.Center);
            goTime.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(goTime.rectTransform,
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(300f, 60f));

            TextMeshProUGUI goBest = UIFactory.CreateText(gort, "BestRecord", "BEST: 0.00s", 17f,
                new Color(0.45f, 0.67f, 0.84f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(goBest.rectTransform,
                new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(300f, 30f));

            engine.gameOverPanel = overPanel;
            engine.gameOverReasonText = goReason;
            engine.finalTimeText = goTime;
            engine.bestRecordText = goBest;

            MakeBtn(gort, "RetryBtn", "PLAY AGAIN", new Vector2(240f, 52f), new Vector2(0f, -140f), () => Play());

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

            TextMeshProUGUI txt = UIFactory.CreateText(go.transform, "Label", label, 17f, Color.white, TextAlignmentOptions.Center);
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
