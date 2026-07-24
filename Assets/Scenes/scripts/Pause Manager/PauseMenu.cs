using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Pause menu with a dark fantasy / arcane aesthetic — soft glowing frames,
/// mystical orbs, arcane rune patterns, and flowing violet energy.
/// Triggered by P or Escape.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    public bool isPaused { get; private set; }

    /// <summary>
    /// Scene name to load when the player selects "Quit to Menu".
    /// </summary>
    public string menuSceneName = "MainMenu";

    // ── Palette: midnight void + aether cyan + restrained violet ──
    private static readonly Color VoidOverlay       = new Color(0.015f, 0.025f, 0.06f, 0.88f);
    private static readonly Color NeonPurple        = new Color(0.34f, 0.88f, 1f, 1f);
    private static readonly Color NeonPurpleDim     = new Color(0.34f, 0.88f, 1f, 0.42f);
    private static readonly Color ArcaneViolet      = new Color(0.45f, 0.48f, 0.95f, 1f);
    private static readonly Color ArcaneVioletDim   = new Color(0.45f, 0.48f, 0.95f, 0.35f);
    private static readonly Color DeepVoid          = new Color(0.025f, 0.045f, 0.11f, 0.97f);
    private static readonly Color PanelInner        = new Color(0.035f, 0.075f, 0.15f, 0.96f);
    private static readonly Color FrameGlow         = new Color(0.28f, 0.82f, 1f, 0.70f);
    private static readonly Color FrameInner        = new Color(0.30f, 0.38f, 0.92f, 0.55f);

    [Header("Title Screen Artwork & Button Sprites")]
    [Tooltip("Logo sprite from the title screen. Automatically loads Assets/Scenes/art/mainmenulogo.png.")]
    public Sprite logoSprite;
    [Tooltip("Background sprite. Automatically loads Assets/Scenes/art/main_menu_bg.png.")]
    public Sprite backgroundSprite;
    [Tooltip("Resume button sprite. Automatically loads Assets/Scenes/art/pause_resume.png or play_btn.png.")]
    public Sprite resumeButtonSprite;
    [Tooltip("Restart button sprite. Automatically loads Assets/Scenes/art/pause_restart.png.")]
    public Sprite restartButtonSprite;
    [Tooltip("Quit button sprite. Automatically loads Assets/Scenes/art/pause_quit.png or quit_btn.png.")]
    public Sprite quitButtonSprite;

    private GameObject pauseOverlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("PauseManager");
            go.AddComponent<PauseMenu>();
            DontDestroyOnLoad(go);
        }
    }

    void Reset()
    {
        AutoLoadSprites();
    }

    void Awake()
    {
        Instance = this;
        AutoLoadSprites();
        BuildUI();
        Time.timeScale = 1f;
    }

    public void AutoLoadSprites()
    {
        if (logoSprite == null)
        {
            logoSprite = LoadSpriteDirectly("mainmenulogo") ?? LoadSpriteDirectly("main_menu_logo");
        }
        if (backgroundSprite == null)
        {
            backgroundSprite = LoadSpriteDirectly("main_menu_bg");
        }
        if (resumeButtonSprite == null)
        {
            resumeButtonSprite = LoadSpriteDirectly("pause_resume") ?? LoadSpriteDirectly("play_btn");
        }
        if (restartButtonSprite == null)
        {
            restartButtonSprite = LoadSpriteDirectly("pause_restart");
        }
        if (quitButtonSprite == null)
        {
            quitButtonSprite = LoadSpriteDirectly("pause_quit") ?? LoadSpriteDirectly("quit_btn");
        }
    }

    private Sprite LoadSpriteDirectly(string nameNoExt)
    {
        Sprite s = Resources.Load<Sprite>(nameNoExt);
        if (s != null) return s;
#if UNITY_EDITOR
        string[] paths = new[] {
            "Assets/Scenes/art/" + nameNoExt + ".png",
            "Assets/Scenes/" + nameNoExt + ".png"
        };
        foreach (string p in paths)
        {
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) return s;
        }
#endif
        return null;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isPaused = false;
        if (pauseOverlay != null)
        {
            pauseOverlay.SetActive(false);
        }
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == menuSceneName)
        {
            return;
        }

        if (NyxarisManager.IsTyping || NyxarisManager.IsChatActive)
        {
            return;
        }

        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        AutoLoadSprites();

        Canvas canvas = UIFactory.CreateCanvas("PauseCanvas", 10);
        canvas.transform.SetParent(transform, false);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.matchWidthOrHeight = 1.0f;

        // ── Layer 0: background image + dark overlay ──
        RectTransform overlayRT = UIFactory.CreateFullScreenPanel(
            canvas.transform, "PauseOverlay", VoidOverlay);
        pauseOverlay = overlayRT.gameObject;

        if (backgroundSprite != null)
        {
            RectTransform bgRT = UIFactory.CreatePanel(
                overlayRT, "TitleBackground", Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image bgImgComp = bgRT.GetComponent<Image>();
            bgImgComp.sprite = backgroundSprite;
            bgImgComp.color = new Color(0.35f, 0.32f, 0.50f, 0.45f); // Soft dark violet overlay tint
            bgImgComp.raycastTarget = false;
        }

        // ── Layer 1: radial vignette ──
        Sprite vignetteSprite = CreateVignetteSprite(512, 512);
        RectTransform vignetteRT = UIFactory.CreatePanel(
            overlayRT, "Vignette", Color.white,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image vignetteImg = vignetteRT.GetComponent<Image>();
        vignetteImg.sprite = vignetteSprite;
        vignetteImg.raycastTarget = false;
        vignetteImg.color = new Color(0.05f, 0.01f, 0.10f, 0.82f);

        // ── Layer 2: main card panel (Semi-transparent gothic glass card) ──
        Sprite roundedSprite = CreateRoundedFrameSprite(128, 128, 24);
        RectTransform glowRT = UIFactory.CreatePanel(
            overlayRT, "PauseGlow",
            new Color(0.22f, 0.78f, 1f, 0.18f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        glowRT.sizeDelta = new Vector2(620f, 720f);
        Image glowImg = glowRT.GetComponent<Image>();
        glowImg.sprite = roundedSprite;
        glowImg.type = Image.Type.Sliced;
        glowImg.raycastTarget = false;

        RectTransform panelRT = UIFactory.CreatePanel(
            overlayRT, "PausePanel", new Color(0.04f, 0.03f, 0.09f, 0.82f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panelRT.sizeDelta = new Vector2(580f, 680f);

        Image panelImg = panelRT.GetComponent<Image>();
        panelImg.sprite = roundedSprite;
        panelImg.type = Image.Type.Sliced;

        // Border ring & inner fill
        RectTransform borderInnerRT = UIFactory.CreatePanel(
            panelRT, "BorderInner", Color.clear,
            Vector2.zero, Vector2.one,
            new Vector2(2f, 2f), new Vector2(-2f, -2f));
        Image borderInnerImg = borderInnerRT.GetComponent<Image>();
        borderInnerImg.sprite = roundedSprite;
        borderInnerImg.type = Image.Type.Sliced;
        borderInnerImg.color = FrameInner;

        RectTransform innerRT = UIFactory.CreatePanel(
            panelRT, "PanelInner", new Color(0.03f, 0.05f, 0.12f, 0.88f),
            Vector2.zero, Vector2.one,
            new Vector2(5f, 5f), new Vector2(-5f, -5f));
        Image innerImg = innerRT.GetComponent<Image>();
        innerImg.sprite = roundedSprite;
        innerImg.type = Image.Type.Sliced;

        // Content vertical layout
        VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(panelRT.gameObject, 12f,
            new RectOffset(32, 32, 24, 20), TextAnchor.UpperCenter);
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // ── Logo / Title Header ──
        if (logoSprite != null)
        {
            GameObject logoGO = new GameObject("TitleLogo", typeof(RectTransform), typeof(Image));
            logoGO.transform.SetParent(panelRT, false);
            Image logoImgComp = logoGO.GetComponent<Image>();
            logoImgComp.sprite = logoSprite;
            logoImgComp.preserveAspect = true;
            logoImgComp.raycastTarget = false;
            
            LayoutElement le = logoGO.AddComponent<LayoutElement>();
            le.preferredWidth = 460f;
            le.preferredHeight = 160f;
            le.minWidth = 460f;
            le.minHeight = 160f;
        }
        else
        {
            TextMeshProUGUI titleText = UIFactory.CreateText(
                panelRT, "PausedTitle", "PAUSED",
                56f, UIFactory.TextWhite, TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 12f;
            
            LayoutElement le = titleText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 500f;
            le.preferredHeight = 70f;
        }

        // Subtitle & divider
        TextMeshProUGUI subtitleText = UIFactory.CreateText(
            panelRT, "Subtitle", "GAME PAUSED",
            16f, NeonPurpleDim, TextAlignmentOptions.Center);
        subtitleText.characterSpacing = 6f;
        subtitleText.fontStyle = FontStyles.Bold;
        
        LayoutElement subLe = subtitleText.gameObject.AddComponent<LayoutElement>();
        subLe.preferredWidth = 500f;
        subLe.preferredHeight = 22f;

        CreateArcaneDivider(panelRT, "TitleDivider");

        // ── Custom Sprite Buttons (Matching Title Screen Dimensions) ──
        Button resumeBtn = CreateCustomSpriteButton(panelRT, "ResumeButton", resumeButtonSprite, "RESUME", () => ResumeGame(), new Vector2(320f, 96f));
        Button restartBtn = CreateCustomSpriteButton(panelRT, "RestartButton", restartButtonSprite, "RESTART", () => RestartLevel(), new Vector2(320f, 92f));
        Button minigamesBtn = CreateArcaneButton(panelRT, "MinigamesButton", "MINIGAMES", "🎮", 18f, new Vector2(320f, 52f), () => OpenMinigamesMenu(), true);
        Button quitBtn = CreateCustomSpriteButton(panelRT, "QuitButton", quitButtonSprite, "QUIT TO MENU", () => QuitToMenu(menuSceneName), new Vector2(320f, 88f));

        // Footer hint
        CreateArcaneDivider(panelRT, "BottomDivider");

        TextMeshProUGUI hintText = UIFactory.CreateText(
            panelRT, "Hint", "PRESS ESC OR P TO RESUME",
            13f, new Color(0.45f, 0.67f, 0.84f, 0.72f), TextAlignmentOptions.Center);
        hintText.characterSpacing = 2f;
        
        LayoutElement hintLe = hintText.gameObject.AddComponent<LayoutElement>();
        hintLe.preferredWidth = 500f;
        hintLe.preferredHeight = 22f;

        // Ambient FX
        PauseMenuAmbientFX ambientFX = pauseOverlay.AddComponent<PauseMenuAmbientFX>();
        ambientFX.Initialize(glowImg, vignetteRT, glowImg.color);

        pauseOverlay.SetActive(false);
    }

    private Button CreateCustomSpriteButton(Transform parent, string name, Sprite btnSprite, string fallbackText, UnityEngine.Events.UnityAction onClick, Vector2 size)
    {
        GameObject containerGO = new GameObject(name, typeof(RectTransform));
        containerGO.transform.SetParent(parent, false);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.sizeDelta = size;

        LayoutElement le = containerGO.AddComponent<LayoutElement>();
        le.minWidth = size.x;
        le.minHeight = size.y;
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        Image btnImg = containerGO.AddComponent<Image>();

        if (btnSprite != null)
        {
            btnImg.sprite = btnSprite;
            btnImg.color = Color.white;
            btnImg.preserveAspect = true;
        }
        else
        {
            // Fallback styled background
            btnImg.sprite = CreateRoundedFrameSprite(64, 64, 14);
            btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.08f, 0.15f, 0.28f, 0.95f);

            TextMeshProUGUI label = UIFactory.CreateText(
                containerRT, "Label", fallbackText,
                22f, UIFactory.TextWhite, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 4f;
        }

        Button btn = containerRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.82f, 0.82f, 0.92f, 1f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        // Attach interactive scaling hover/press animation
        PauseSpriteButtonFX fx = containerRT.gameObject.AddComponent<PauseSpriteButtonFX>();
        fx.Initialize(btnImg);

        return btn;
    }

    // ── Decorative Elements ────────────────────────────────────────

    private void CreateArcaneDivider(Transform parent, string name)
    {
        RectTransform rowRT = UIFactory.CreatePanel(
            parent, name, Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rowRT.sizeDelta = new Vector2(400f, 16f);
        UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 16f, preferredWidth: 400f);

        HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 0f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        hlg.childControlWidth = false;
        hlg.childForceExpandWidth = false;

        // Left fade line
        CreateDividerSegment(rowRT, 140f, new Color(0.65f, 0.20f, 0.95f, 0.50f));
        // Left small orb
        CreateDividerOrb(rowRT, 6f, NeonPurpleDim);
        // Center line
        CreateDividerSegment(rowRT, 60f, new Color(0.50f, 0.15f, 0.85f, 0.65f));
        // Center orb (larger, brighter)
        CreateDividerOrb(rowRT, 10f, NeonPurple);
        // Right line
        CreateDividerSegment(rowRT, 60f, new Color(0.50f, 0.15f, 0.85f, 0.65f));
        // Right small orb
        CreateDividerOrb(rowRT, 6f, NeonPurpleDim);
        // Right fade line
        CreateDividerSegment(rowRT, 140f, new Color(0.65f, 0.20f, 0.95f, 0.50f));
    }

    private void CreateDividerSegment(Transform parent, float width, Color color)
    {
        RectTransform segRT = UIFactory.CreatePanel(
            parent, "Seg", color,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        segRT.sizeDelta = new Vector2(width, 1.5f);
        segRT.GetComponent<Image>().raycastTarget = false;
        UIFactory.AddLayoutElement(segRT.gameObject, preferredWidth: width, preferredHeight: 2f);
    }

    private void CreateDividerOrb(Transform parent, float size, Color color)
    {
        Sprite orbSprite = CreateOrbSprite(32);
        RectTransform orbRT = UIFactory.CreatePanel(
            parent, "DivOrb", Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        orbRT.sizeDelta = new Vector2(size, size);
        Image orbImg = orbRT.GetComponent<Image>();
        orbImg.sprite = orbSprite;
        orbImg.color = color;
        orbImg.raycastTarget = false;
        UIFactory.AddLayoutElement(orbRT.gameObject, preferredWidth: size + 6f, preferredHeight: size);
    }

    // ── Arcane Button ──────────────────────────────────────────────

    private Button CreateArcaneButton(Transform parent, string name, string label, string icon,
        float fontSize, Vector2 size, UnityEngine.Events.UnityAction onClick, bool isPrimary = false)
    {
        Sprite roundedSprite = CreateRoundedFrameSprite(64, 64, 14);

        RectTransform containerRT = UIFactory.CreatePanel(
            parent, name, Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        containerRT.sizeDelta = size;

        // Glow underlay
        RectTransform glowRT = UIFactory.CreatePanel(
            containerRT, name + "_Glow", Color.clear,
            Vector2.zero, Vector2.one,
            new Vector2(-4f, -4f), new Vector2(4f, 4f));
        Image glowImg = glowRT.GetComponent<Image>();
        glowImg.sprite = roundedSprite;
        glowImg.type = Image.Type.Sliced;
        glowImg.color = new Color(0.65f, 0.12f, 1f, 0f);
        glowImg.raycastTarget = false;

        // Border
        RectTransform borderRT = UIFactory.CreatePanel(
            containerRT, name + "_Border", isPrimary ? new Color(0.34f, 0.88f, 1f, 0.95f) : FrameGlow,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image borderImg = borderRT.GetComponent<Image>();
        borderImg.sprite = roundedSprite;
        borderImg.type = Image.Type.Sliced;

        // Inner bg
        RectTransform innerRT = UIFactory.CreatePanel(
            borderRT, name + "_Bg", isPrimary
                ? new Color(0.05f, 0.28f, 0.40f, 0.98f)
                : new Color(0.035f, 0.075f, 0.15f, 0.96f),
            Vector2.zero, Vector2.one,
            new Vector2(2f, 2f), new Vector2(-2f, -2f));
        Image innerImg = innerRT.GetComponent<Image>();
        innerImg.sprite = roundedSprite;
        innerImg.type = Image.Type.Sliced;

        // Left accent — soft glow bar instead of hard stripe
        RectTransform accentRT = UIFactory.CreatePanel(
            innerRT, "AccentGlow",
            isPrimary ? new Color(0.40f, 0.94f, 1f, 0.36f) : new Color(0.28f, 0.68f, 1f, 0.15f),
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(4f, 6f), new Vector2(12f, -6f));
        Image accentImg = accentRT.GetComponent<Image>();
        accentImg.sprite = CreateOrbSprite(16);
        accentImg.raycastTarget = false;

        Button btn = containerRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = innerImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = isPrimary ? new Color(0.05f, 0.28f, 0.40f, 0.98f) : new Color(0.035f, 0.075f, 0.15f, 0.96f);
        cb.highlightedColor = isPrimary ? new Color(0.08f, 0.43f, 0.58f, 1f) : new Color(0.07f, 0.13f, 0.25f, 0.98f);
        cb.pressedColor = isPrimary ? new Color(0.04f, 0.20f, 0.30f, 1f) : new Color(0.04f, 0.09f, 0.18f, 0.98f);
        cb.disabledColor = new Color(0.12f, 0.10f, 0.16f, 0.45f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        // Icon
        TextMeshProUGUI iconText = UIFactory.CreateText(
            innerRT, "Icon", icon,
            fontSize + 4f, NeonPurple, TextAlignmentOptions.Center);
        UIFactory.SetRect(iconText.rectTransform,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(14f, 0f), new Vector2(54f, 0f));

        // Label
        TextMeshProUGUI text = UIFactory.CreateText(
            innerRT, "Label", label,
            fontSize, UIFactory.TextWhite, TextAlignmentOptions.Left);
        UIFactory.SetRect(text.rectTransform,
            new Vector2(0f, 0f), Vector2.one,
            new Vector2(58f, 0f), new Vector2(-12f, 0f));
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 5f;
        text.outlineColor = new Color(0.03f, 0f, 0.06f, 0.65f);
        text.outlineWidth = 0.2f;

        ArcaneButtonEffects fx = containerRT.gameObject.AddComponent<ArcaneButtonEffects>();
        fx.Initialize(text, innerImg, borderImg, glowImg);

        return btn;
    }

    // ── Public API ─────────────────────────────────────────────────

    public void TogglePause()
    {
        isPaused = !isPaused;
        pauseOverlay?.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
        HUDManager.Instance?.UpdateVisibility();
        SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;
        TogglePause();
    }

    public void OpenMinigamesMenu()
    {
        SpawnOfChaos.Minigames.MinigameHubUI.OpenHub();
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu(string menuSceneName)
    {
        Time.timeScale = 1f;
        MainMenuController.isPlaying = false;

        if (Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // ── Procedural Textures ────────────────────────────────────────

    /// <summary>Rounded rectangle frame — 9-slice ready, soft edges.</summary>
    private static Sprite CreateRoundedFrameSprite(int width, int height, int radius)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color fill = Color.white;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, width, height, radius);
                if (inside)
                {
                    // Soft edge antialiasing
                    float edgeDist = GetRoundedRectEdgeDist(x, y, width, height, radius);
                    float alpha = Mathf.Clamp01(edgeDist + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }

        tex.Apply();
        int border = radius + 2;
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static bool IsInsideRoundedRect(int x, int y, int w, int h, int r)
    {
        // Check corners
        if (x < r && y < r) // bottom-left
            return (r - x) * (r - x) + (r - y) * (r - y) <= r * r;
        if (x >= w - r && y < r) // bottom-right
            return (x - (w - r - 1)) * (x - (w - r - 1)) + (r - y) * (r - y) <= r * r;
        if (x < r && y >= h - r) // top-left
            return (r - x) * (r - x) + (y - (h - r - 1)) * (y - (h - r - 1)) <= r * r;
        if (x >= w - r && y >= h - r) // top-right
            return (x - (w - r - 1)) * (x - (w - r - 1)) + (y - (h - r - 1)) * (y - (h - r - 1)) <= r * r;
        return true;
    }

    private static float GetRoundedRectEdgeDist(int x, int y, int w, int h, int r)
    {
        float cx = 0, cy = 0;
        bool inCorner = false;

        if (x < r && y < r) { cx = r; cy = r; inCorner = true; }
        else if (x >= w - r && y < r) { cx = w - r - 1; cy = r; inCorner = true; }
        else if (x < r && y >= h - r) { cx = r; cy = h - r - 1; inCorner = true; }
        else if (x >= w - r && y >= h - r) { cx = w - r - 1; cy = h - r - 1; inCorner = true; }

        if (inCorner)
        {
            float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return r - dist;
        }

        // Edge distances for non-corner areas
        float minEdge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
        return minEdge;
    }

    /// <summary>Creates a soft circular orb sprite with glow falloff.</summary>
    private static Sprite CreateOrbSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float radius = center * 0.85f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float norm = dist / radius;

                // Soft falloff: bright center, fading edges
                float alpha;
                if (norm <= 0.5f)
                    alpha = 1f;
                else if (norm <= 1f)
                    alpha = 1f - (norm - 0.5f) * 2f;
                else
                    alpha = Mathf.Max(0f, 1f - (norm - 1f) * 3f) * 0.3f; // faint outer glow

                alpha = alpha * alpha; // quadratic falloff for softness
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateVignetteSprite(int size, int _)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float maxDist = center * 1.15f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / maxDist;
                float dy = (y - center) / maxDist;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((dist - 0.15f) / 0.85f);
                alpha = alpha * alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Creates a subtle arcane rune/circle pattern tile.</summary>
    private static Sprite CreateRunePatternSprite(int size, int _)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color line = new Color(1f, 1f, 1f, 0.25f);
        Color lineFaint = new Color(1f, 1f, 1f, 0.12f);

        // Clear
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        float cx = size * 0.5f;
        float cy = size * 0.5f;

        // Draw concentric circles (arcane rings)
        DrawCircle(tex, cx, cy, size * 0.38f, line);
        DrawCircle(tex, cx, cy, size * 0.22f, lineFaint);

        // Draw cross lines through center (mystic compass)
        for (int i = 0; i < size; i++)
        {
            int mid = size / 2;
            // Vertical line
            if (Mathf.Abs(i - mid) > size * 0.15f)
            {
                if (i >= 0 && i < size)
                    tex.SetPixel(mid, i, lineFaint);
            }
            // Horizontal line
            if (Mathf.Abs(i - mid) > size * 0.15f)
            {
                if (i >= 0 && i < size)
                    tex.SetPixel(i, mid, lineFaint);
            }
        }

        // Small dots at cardinal points
        DrawDot(tex, (int)cx, (int)(cy + size * 0.38f), 1, line);
        DrawDot(tex, (int)cx, (int)(cy - size * 0.38f), 1, line);
        DrawDot(tex, (int)(cx + size * 0.38f), (int)cy, 1, line);
        DrawDot(tex, (int)(cx - size * 0.38f), (int)cy, 1, line);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void DrawCircle(Texture2D tex, float cx, float cy, float radius, Color color)
    {
        int steps = Mathf.CeilToInt(radius * 8f);
        for (int i = 0; i <= steps; i++)
        {
            float angle = (i / (float)steps) * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(cx + Mathf.Cos(angle) * radius);
            int y = Mathf.RoundToInt(cy + Mathf.Sin(angle) * radius);
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                tex.SetPixel(x, y, color);
        }
    }

    private static void DrawDot(Texture2D tex, int cx, int cy, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy <= radius * radius)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                        tex.SetPixel(px, py, color);
                }
            }
        }
    }
}

// ── Arcane button hover effects (unscaled time for pause compatibility) ──

public class ArcaneButtonEffects : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler,
    UnityEngine.EventSystems.IPointerUpHandler
{
    private RectTransform rectTransform;
    private TextMeshProUGUI buttonText;
    private Image buttonImage;
    private Image borderImage;
    private Image glowImage;
    private string originalText;

    private Vector3 targetScale = Vector3.one;
    private Color targetTextColor;
    private Color targetBorderColor;
    private float targetGlowAlpha;

    private Color originalTextColor;
    private Color originalBorderColor;
    private Vector3 originalScale;

    private float lerpSpeed = 10f; // slightly slower for a more mystical feel
    private bool isHovered;

    private static readonly Color ArcanePurple = new Color(0.34f, 0.88f, 1f, 1f);
    private static readonly Color WarmHighlight = new Color(0.88f, 0.98f, 1f, 1f);

    public void Initialize(TextMeshProUGUI txt, Image buttonBg, Image border, Image glow)
    {
        rectTransform = GetComponent<RectTransform>();
        buttonText = txt;
        buttonImage = buttonBg;
        borderImage = border;
        glowImage = glow;

        originalScale = rectTransform.localScale;
        targetScale = originalScale;

        if (buttonText != null)
        {
            originalText = buttonText.text;
            originalTextColor = buttonText.color;
            targetTextColor = originalTextColor;
        }

        if (borderImage != null)
        {
            originalBorderColor = borderImage.color;
            targetBorderColor = originalBorderColor;
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime * lerpSpeed;

        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, dt);

        if (buttonText != null)
            buttonText.color = Color.Lerp(buttonText.color, targetTextColor, dt);

        if (borderImage != null)
            borderImage.color = Color.Lerp(borderImage.color, targetBorderColor, dt);

        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = Mathf.Lerp(c.a, targetGlowAlpha, dt);
            glowImage.color = c;
        }
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isHovered = true;
        targetScale = originalScale * 1.03f;
        targetTextColor = WarmHighlight;
        targetBorderColor = ArcanePurple;
        targetGlowAlpha = 0.30f;

        if (buttonText != null)
            buttonText.text = "✦  " + originalText;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isHovered = false;
        targetScale = originalScale;
        targetTextColor = originalTextColor;
        targetBorderColor = originalBorderColor;
        targetGlowAlpha = 0f;

        if (buttonText != null)
            buttonText.text = originalText;
    }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 0.97f;
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = isHovered ? originalScale * 1.03f : originalScale;
    }
}

public class PauseSpriteButtonFX : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler,
    UnityEngine.EventSystems.IPointerUpHandler
{
    private RectTransform rectTransform;
    private Image buttonImage;
    private Vector3 originalScale;
    private Vector3 targetScale = Vector3.one;
    private Color originalColor = Color.white;
    private Color targetColor = Color.white;
    private bool isHovered;

    public void Initialize(Image img)
    {
        rectTransform = GetComponent<RectTransform>();
        buttonImage = img;
        originalScale = rectTransform.localScale;
        targetScale = originalScale;
        if (buttonImage != null)
        {
            originalColor = buttonImage.color;
            targetColor = originalColor;
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime * 12f;
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, dt);
        if (buttonImage != null)
        {
            buttonImage.color = Color.Lerp(buttonImage.color, targetColor, dt);
        }
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isHovered = true;
        targetScale = originalScale * 1.06f;
        targetColor = new Color(1f, 1f, 1f, 1f);
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isHovered = false;
        targetScale = originalScale;
        targetColor = originalColor;
    }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 0.94f;
        targetColor = new Color(0.85f, 0.85f, 0.95f, 1f);
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = isHovered ? originalScale * 1.06f : originalScale;
        targetColor = isHovered ? new Color(1f, 1f, 1f, 1f) : originalColor;
    }
}
