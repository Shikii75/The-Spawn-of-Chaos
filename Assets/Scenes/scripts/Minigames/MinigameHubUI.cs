using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Central Minigame Hub Selector UI opened directly from the Pause Menu.
    /// Premium dark arcade aesthetic with neon-accented game cards, rounded corners,
    /// colored accent stripes, and rich visual hierarchy.
    /// Lists all available arcade minigames with expandable slots for future additions!
    /// </summary>
    public class MinigameHubUI : MonoBehaviour
    {
        public static MinigameHubUI Instance { get; private set; }

        private GameObject hubModal;

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
            }
        }

        public static void OpenHub()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("MinigameHubManager");
                Instance = go.AddComponent<MinigameHubUI>();
            }
            Instance.ShowHubWindow();
        }

        public void ShowHubWindow()
        {
            if (hubModal == null) BuildUI();
            hubModal.SetActive(true);
        }

        public void CloseHubWindow()
        {
            if (hubModal != null) hubModal.SetActive(false);
        }

        private void BuildUI()
        {
            Canvas canvas = UIFactory.CreateCanvas("MinigameHubCanvas", 22);
            canvas.transform.SetParent(transform, false);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.matchWidthOrHeight = 1.0f;

            // ── Fullscreen dark overlay with subtle blue tint ──
            RectTransform overlayRT = UIFactory.CreateFullScreenPanel(
                canvas.transform, "HubOverlay", new Color(0.01f, 0.015f, 0.04f, 0.95f));
            hubModal = overlayRT.gameObject;

            // ── Main Panel (centered modal) ──
            RectTransform panelRT = UIFactory.CreatePanel(
                overlayRT, "HubPanel", new Color(0.025f, 0.02f, 0.06f, 0.97f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panelRT.sizeDelta = new Vector2(620f, 760f);

            // Rounded corners for main panel
            Image panelImg = panelRT.GetComponent<Image>();
            Sprite roundedSprite = UIFactory.GetRoundedSprite();
            if (roundedSprite != null && panelImg != null)
            {
                panelImg.sprite = roundedSprite;
                panelImg.type = Image.Type.Sliced;
            }

            // Subtle border glow
            RectTransform borderGlow = UIFactory.CreatePanel(panelRT, "BorderGlow",
                new Color(0.57f, 0.41f, 1f, 0.06f),
                Vector2.zero, Vector2.one, new Vector2(-3f, -3f), new Vector2(3f, 3f));
            Image borderImg = borderGlow.GetComponent<Image>();
            if (roundedSprite != null && borderImg != null)
            {
                borderImg.sprite = roundedSprite;
                borderImg.type = Image.Type.Sliced;
            }

            // ══════════════════════════════════════════
            //   HEADER SECTION
            // ══════════════════════════════════════════

            // Small decorative top accent bar
            RectTransform topAccent = UIFactory.CreatePanel(panelRT, "TopAccent",
                new Color(0.57f, 0.41f, 1f, 0.5f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            topAccent.sizeDelta = new Vector2(540f, 2f);
            topAccent.anchoredPosition = new Vector2(0f, -20f);

            // Title: "ARCADE"
            TextMeshProUGUI titleText = UIFactory.CreateText(
                panelRT, "Title", "ARCADE", 40f,
                new Color(0.57f, 0.41f, 1f, 1f), TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 8f;
            UIFactory.SetRectFixed(titleText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -52f), new Vector2(500f, 50f));

            // Subtitle
            TextMeshProUGUI subtitleText = UIFactory.CreateText(
                panelRT, "Subtitle", "Select a minigame to play while paused", 13f,
                new Color(0.55f, 0.6f, 0.75f, 0.7f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(subtitleText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -88f), new Vector2(500f, 24f));

            // Divider below header
            RectTransform headerDiv = UIFactory.CreatePanel(panelRT, "HeaderDivider",
                new Color(0.57f, 0.41f, 1f, 0.12f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            headerDiv.sizeDelta = new Vector2(540f, 1f);
            headerDiv.anchoredPosition = new Vector2(0f, -108f);

            // ══════════════════════════════════════════
            //   GAME CARD GRID
            // ══════════════════════════════════════════

            GameObject gridGO = new GameObject("MinigameGrid", typeof(RectTransform));
            gridGO.transform.SetParent(panelRT, false);
            RectTransform gridRT = gridGO.GetComponent<RectTransform>();
            UIFactory.SetRectFixed(gridRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(560f, 480f));

            VerticalLayoutGroup vlg = gridGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.padding = new RectOffset(0, 0, 4, 4);

            // ── CARD 1: SKYBOUND BOX ──
            CreateMinigameCard(
                gridRT,
                "SKYBOUND BOX",
                "Survive by landing on rising platforms! Avoid the deadly ceiling & floor.",
                "⏱️",
                "PLAY",
                new Color(0f, 0.94f, 1f, 1f),     // Neon cyan accent
                () => {
                    CloseHubWindow();
                    SkyboundArcadeUI.ShowMinigame();
                }
            );

            // ── CARD 2: SHADOW RUNNER ──
            CreateMinigameCard(
                gridRT,
                "SHADOW RUNNER",
                "High-speed endless runner! Jump spikes, duck bats, and grab gems!",
                "🦖",
                "PLAY",
                new Color(0.85f, 0.28f, 1f, 1f),   // Neon purple accent
                () => {
                    CloseHubWindow();
                    ShadowRunnerArcadeUI.ShowMinigame();
                }
            );

            // ── CARD 3: VOID SURGE (Neon Space Combat — replaces Void Spectrum) ──
            CreateMinigameCard(
                gridRT,
                "VOID SURGE",
                "Neon space combat! Particle explosions, boss battles & weapon upgrades!",
                "⚡",
                "PLAY",
                new Color(1f, 0f, 0.67f, 1f),       // Neon magenta accent
                () => {
                    CloseHubWindow();
                    VoidSurgeArcadeUI.ShowMinigame();
                }
            );

            // ── CARD 4: ORB SPLASH LAB ──
            CreateMinigameCard(
                gridRT,
                "ORB SPLASH LAB",
                "Dynamic procedural Health, Mana, Currency, & EP Orbs with splash physics!",
                "🧪",
                "PLAY",
                new Color(0.2f, 0.75f, 1f, 1f),     // Bright blue accent
                () => {
                    CloseHubWindow();
                    OrbSplashArcadeUI.ShowMinigame();
                }
            );

            // ══════════════════════════════════════════
            //   FOOTER
            // ══════════════════════════════════════════

            // Divider above footer
            RectTransform footerDiv = UIFactory.CreatePanel(panelRT, "FooterDivider",
                new Color(0.57f, 0.41f, 1f, 0.08f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            footerDiv.sizeDelta = new Vector2(540f, 1f);
            footerDiv.anchoredPosition = new Vector2(0f, 75f);

            // Close button with rounded corners
            GameObject closeBtnGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtnGO.transform.SetParent(panelRT, false);
            RectTransform closeRT = closeBtnGO.GetComponent<RectTransform>();
            closeRT.sizeDelta = new Vector2(260f, 44f);
            closeRT.anchoredPosition = new Vector2(0f, -338f);

            Image closeImg = closeBtnGO.GetComponent<Image>();
            closeImg.color = new Color(0.08f, 0.06f, 0.16f, 0.9f);
            if (roundedSprite != null)
            {
                closeImg.sprite = roundedSprite;
                closeImg.type = Image.Type.Sliced;
            }

            TextMeshProUGUI closeLabel = UIFactory.CreateText(
                closeBtnGO.transform, "Label", "◀  BACK TO PAUSE MENU", 12f,
                new Color(0.6f, 0.65f, 0.8f, 0.9f), TextAlignmentOptions.Center);
            closeLabel.characterSpacing = 2f;
            UIFactory.SetRect(closeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Button closeBtn = closeBtnGO.GetComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            ColorBlock closeCB = closeBtn.colors;
            closeCB.normalColor = new Color(0.08f, 0.06f, 0.16f, 0.9f);
            closeCB.highlightedColor = new Color(0.14f, 0.12f, 0.28f, 1f);
            closeCB.pressedColor = new Color(0.05f, 0.04f, 0.10f, 1f);
            closeCB.fadeDuration = 0.08f;
            closeBtn.colors = closeCB;
            closeBtn.onClick.AddListener(() => CloseHubWindow());

            hubModal.SetActive(false);
        }

        /// <summary>
        /// Creates a premium minigame card with colored accent stripe, icon, title, description, and play button.
        /// </summary>
        private void CreateMinigameCard(Transform parent, string title, string desc,
            string icon, string btnText, Color accentColor, UnityEngine.Events.UnityAction onClick, bool isEnabled = true)
        {
            Sprite roundedSprite = UIFactory.GetRoundedSprite();

            // ── Card Container ──
            GameObject cardGO = new GameObject("Card_" + title, typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(parent, false);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.sizeDelta = new Vector2(540f, 108f);

            Image cardImg = cardGO.GetComponent<Image>();
            cardImg.color = isEnabled
                ? new Color(0.04f, 0.035f, 0.10f, 0.95f)
                : new Color(0.025f, 0.02f, 0.06f, 0.5f);

            if (roundedSprite != null)
            {
                cardImg.sprite = roundedSprite;
                cardImg.type = Image.Type.Sliced;
            }

            // ── Colored Left Accent Stripe ──
            RectTransform accentStripe = UIFactory.CreatePanel(cardRT, "AccentStripe",
                accentColor,
                new Vector2(0f, 0f), new Vector2(0f, 1f));
            accentStripe.sizeDelta = new Vector2(4f, 0f);
            accentStripe.anchoredPosition = new Vector2(2f, 0f);
            // Slight margin from edges
            accentStripe.offsetMin = new Vector2(0f, 8f);
            accentStripe.offsetMax = new Vector2(4f, -8f);

            // ── Icon ──
            TextMeshProUGUI iconText = UIFactory.CreateText(cardRT, "Icon", icon, 26f,
                accentColor, TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(iconText.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(32f, 0f), new Vector2(40f, 40f));

            // ── Title ──
            TextMeshProUGUI tText = UIFactory.CreateText(cardRT, "Title", title, 18f,
                accentColor, TextAlignmentOptions.Left);
            tText.fontStyle = FontStyles.Bold;
            tText.characterSpacing = 3f;
            UIFactory.SetRectFixed(tText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(62f, -30f), new Vector2(300f, 28f));

            // ── Description ──
            TextMeshProUGUI dText = UIFactory.CreateText(cardRT, "Desc", desc, 12f,
                new Color(0.55f, 0.6f, 0.75f, 0.8f), TextAlignmentOptions.Left);
            UIFactory.SetRectFixed(dText.rectTransform,
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(62f, 22f), new Vector2(320f, 40f));

            // ── Play Button ──
            if (isEnabled && onClick != null)
            {
                GameObject btnGO = new GameObject("ActionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(cardRT, false);
                RectTransform btnRT = btnGO.GetComponent<RectTransform>();
                btnRT.sizeDelta = new Vector2(110f, 38f);
                btnRT.anchorMin = new Vector2(1f, 0.5f);
                btnRT.anchorMax = new Vector2(1f, 0.5f);
                btnRT.anchoredPosition = new Vector2(-72f, 0f);

                Image btnImg = btnGO.GetComponent<Image>();
                // Button background uses a tinted version of the accent color
                Color btnBg = new Color(accentColor.r * 0.15f, accentColor.g * 0.15f, accentColor.b * 0.15f, 0.9f);
                btnImg.color = btnBg;

                if (roundedSprite != null)
                {
                    btnImg.sprite = roundedSprite;
                    btnImg.type = Image.Type.Sliced;
                }

                TextMeshProUGUI btnLabel = UIFactory.CreateText(
                    btnGO.transform, "Text", btnText + " ▶", 13f,
                    accentColor, TextAlignmentOptions.Center);
                btnLabel.fontStyle = FontStyles.Bold;
                btnLabel.characterSpacing = 3f;
                UIFactory.SetRect(btnLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                Button btn = btnGO.GetComponent<Button>();
                btn.targetGraphic = btnImg;
                ColorBlock cb = btn.colors;
                cb.normalColor = btnBg;
                cb.highlightedColor = new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 1f);
                cb.pressedColor = new Color(accentColor.r * 0.1f, accentColor.g * 0.1f, accentColor.b * 0.1f, 1f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
                btn.onClick.AddListener(onClick);
            }
        }
    }
}
