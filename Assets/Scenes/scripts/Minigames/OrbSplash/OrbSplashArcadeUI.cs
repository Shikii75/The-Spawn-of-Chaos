using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Interactive Arcade Minigame / Showcase UI for Procedural Liquid Orbs (Health, Mana, Currency, EP).
    /// Displays real-time procedural orb rendering with splash particle physics, fill controls, and splash triggers.
    /// </summary>
    public class OrbSplashArcadeUI : MonoBehaviour
    {
        public static OrbSplashArcadeUI Instance { get; private set; }

        private GameObject modal;
        private List<ProceduralOrbUI> activeOrbUIs = new List<ProceduralOrbUI>();

        public bool IsMinigameActive => modal != null && modal.activeSelf;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void ShowMinigame()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("OrbSplashArcadeManager");
                Instance = go.AddComponent<OrbSplashArcadeUI>();
            }
            Instance.Open();
        }

        public void Open()
        {
            if (modal == null) BuildUI();
            modal.SetActive(true);
        }

        public void Close()
        {
            if (modal != null) modal.SetActive(false);
        }

        private void BuildUI()
        {
            Canvas canvas = UIFactory.CreateCanvas("OrbSplashArcadeCanvas", 26);
            canvas.transform.SetParent(transform, false);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.matchWidthOrHeight = 1.0f;

            // Fullscreen dark backdrop
            RectTransform overlay = UIFactory.CreateFullScreenPanel(
                canvas.transform, "MinigameOverlay", new Color(0.015f, 0.02f, 0.05f, 0.94f));
            modal = overlay.gameObject;

            // Main Window Frame
            RectTransform frame = UIFactory.CreatePanel(overlay, "ArcadeFrame",
                new Color(0.04f, 0.05f, 0.09f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            frame.sizeDelta = new Vector2(900f, 550f);

            // Header Title
            TextMeshProUGUI title = UIFactory.CreateText(
                frame, "Title", "ARCADE PROCEDURAL ORBS", 28f, new Color(0.35f, 0.88f, 1f, 1f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 40f));

            TextMeshProUGUI subtitle = UIFactory.CreateText(
                frame, "Subtitle", "Procedural Dual-Wave Liquid Physics, Emblem Motifs & Dynamic Splashes", 13f, new Color(0.6f, 0.72f, 0.85f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(700f, 24f));

            // Grid Container for 4 Orbs
            GameObject gridGO = new GameObject("OrbGrid", typeof(RectTransform));
            gridGO.transform.SetParent(frame, false);
            RectTransform gridRT = gridGO.GetComponent<RectTransform>();
            UIFactory.SetRectFixed(gridRT, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(840f, 360f));

            GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(195f, 340f);
            glg.spacing = new Vector2(16f, 0f);
            glg.childAlignment = TextAnchor.MiddleCenter;

            activeOrbUIs.Clear();

            // Create 4 Orbs: Health, Mana, Currency, EP
            CreateOrbCard(gridRT, OrbType.Health, "HEALTH ORB", "Crimson Vitality", new Color(1f, 0.2f, 0.35f, 1f));
            CreateOrbCard(gridRT, OrbType.Mana, "MANA ORB", "Arcane Sapphire", new Color(0.2f, 0.75f, 1f, 1f));
            CreateOrbCard(gridRT, OrbType.Currency, "CURRENCY ORB", "Liquid Gold", new Color(1f, 0.78f, 0.15f, 1f));
            CreateOrbCard(gridRT, OrbType.EP, "EP ORB", "Cosmic EXP", new Color(0.75f, 0.25f, 1f, 1f));

            // Bottom Global Action Bar
            GameObject bottomBar = new GameObject("BottomBar", typeof(RectTransform));
            bottomBar.transform.SetParent(frame, false);
            RectTransform bottomRT = bottomBar.GetComponent<RectTransform>();
            UIFactory.SetRectFixed(bottomRT, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(840f, 48f));

            // "SPLASH ALL" Button
            CreateArcaneBtn(bottomRT, "SPLASH ALL ORBS! 💥", new Vector2(-150f, 0f), new Vector2(240f, 42f),
                new Color(0.85f, 0.25f, 0.95f, 1f), () =>
                {
                    foreach (var orb in activeOrbUIs)
                    {
                        orb.TriggerSplash(1.5f);
                    }
                });

            // "CLOSE" Button
            CreateArcaneBtn(bottomRT, "BACK TO MENU", new Vector2(150f, 0f), new Vector2(220f, 42f),
                new Color(0.16f, 0.22f, 0.35f, 1f), () => Close());
        }

        private void CreateOrbCard(Transform parent, OrbType type, string cardTitle, string subtitleText, Color accentColor)
        {
            GameObject cardGO = new GameObject("Card_" + type, typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(parent, false);
            Image cardBg = cardGO.GetComponent<Image>();
            cardBg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f);

            RectTransform cardRT = cardGO.GetComponent<RectTransform>();

            // Title Label
            TextMeshProUGUI title = UIFactory.CreateText(cardRT, "Title", cardTitle, 15f, accentColor, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetRectFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(180f, 22f));

            TextMeshProUGUI sub = UIFactory.CreateText(cardRT, "Sub", subtitleText, 11f, new Color(0.6f, 0.7f, 0.8f, 0.8f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(180f, 18f));

            // RawImage for Procedural Orb Display (140x140)
            GameObject orbGO = new GameObject("ProceduralOrbDisplay", typeof(RectTransform), typeof(RawImage), typeof(ProceduralOrbUI));
            orbGO.transform.SetParent(cardRT, false);

            RectTransform orbRT = orbGO.GetComponent<RectTransform>();
            UIFactory.SetRectFixed(orbRT, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(140f, 140f));

            ProceduralOrbUI orbUI = orbGO.GetComponent<ProceduralOrbUI>();
            orbUI.orbType = type;
            orbUI.fillAmount = 0.75f;
            orbUI.textureResolution = 140;
            orbUI.autoUpdate = true;
            orbUI.autoOscillateFill = false;

            activeOrbUIs.Add(orbUI);

            // SPLASH Button
            CreateArcaneBtn(cardRT, "SPLASH! 💦", new Vector2(0f, -85f), new Vector2(160f, 34f), accentColor, () =>
            {
                orbUI.TriggerSplash(1.2f);
            });

            // FILL + / DRAIN - Buttons
            CreateArcaneBtn(cardRT, "FILL +", new Vector2(45f, -125f), new Vector2(70f, 28f), new Color(0.18f, 0.35f, 0.22f, 1f), () =>
            {
                orbUI.fillAmount = Mathf.Clamp01(orbUI.fillAmount + 0.15f);
                orbUI.TriggerSplash(0.6f);
            });

            CreateArcaneBtn(cardRT, "DRAIN -", new Vector2(-45f, -125f), new Vector2(70f, 28f), new Color(0.38f, 0.18f, 0.22f, 1f), () =>
            {
                orbUI.fillAmount = Mathf.Clamp01(orbUI.fillAmount - 0.15f);
            });
        }

        private GameObject CreateArcaneBtn(Transform parent, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGO = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnGO.GetComponent<Image>();
            img.color = color;

            Button btn = btnGO.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = new Color(color.r * 1.25f, color.g * 1.25f, color.b * 1.25f, 1f);
            cb.pressedColor = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            TextMeshProUGUI txt = UIFactory.CreateText(btnGO.transform, "Text", label, 12f, Color.white, TextAlignmentOptions.Center);
            txt.fontStyle = FontStyles.Bold;
            UIFactory.SetRect(txt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return btnGO;
        }
    }
}
