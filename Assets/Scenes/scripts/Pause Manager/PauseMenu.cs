using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Pause menu with the Main Menu's Cyber-Gothic Glassmorphic aesthetic — 
/// removes the card container box and presents sleek neon-accented cyber buttons 
/// floating seamlessly over the misty Torii background.
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

    [Header("Title Screen Artwork & Sprites")]
    public Sprite logoSprite;
    public Sprite backgroundSprite;

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
            bgImgComp.color = new Color(0.40f, 0.36f, 0.55f, 0.50f); // Soft dark violet overlay tint
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
        vignetteImg.color = new Color(0.04f, 0.01f, 0.08f, 0.85f);

        // ── Layer 2: Main Content Column (NO Card Frame - Open & Seamless) ──
        RectTransform contentColumnRT = UIFactory.CreatePanel(
            overlayRT, "PauseContentColumn", Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        contentColumnRT.sizeDelta = new Vector2(440f, 760f);

        // Content vertical layout
        VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(contentColumnRT.gameObject, 14f,
            new RectOffset(10, 10, 10, 10), TextAnchor.MiddleCenter);
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // ── Logo / Title Header ──
        if (logoSprite != null)
        {
            GameObject logoGO = new GameObject("TitleLogo", typeof(RectTransform), typeof(Image));
            logoGO.transform.SetParent(contentColumnRT, false);
            Image logoImgComp = logoGO.GetComponent<Image>();
            logoImgComp.sprite = logoSprite;
            logoImgComp.preserveAspect = true;
            logoImgComp.raycastTarget = false;
            
            LayoutElement le = logoGO.AddComponent<LayoutElement>();
            le.preferredWidth = 380f;
            le.preferredHeight = 110f;
            le.minWidth = 380f;
            le.minHeight = 110f;
        }
        else
        {
            TextMeshProUGUI titleText = UIFactory.CreateText(
                contentColumnRT, "PausedTitle", "PAUSED",
                48f, UIFactory.TextWhite, TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 12f;
            
            LayoutElement le = titleText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 420f;
            le.preferredHeight = 56f;
        }

        // Subtitle & divider
        TextMeshProUGUI subtitleText = UIFactory.CreateText(
            contentColumnRT, "Subtitle", "GAME PAUSED",
            14f, NeonPurpleDim, TextAlignmentOptions.Center);
        subtitleText.characterSpacing = 6f;
        subtitleText.fontStyle = FontStyles.Bold;
        
        LayoutElement subLe = subtitleText.gameObject.AddComponent<LayoutElement>();
        subLe.preferredWidth = 420f;
        subLe.preferredHeight = 20f;

        CreateArcaneDivider(contentColumnRT, "TitleDivider");

        // ── Main Menu Styled Cyber-Gothic Glassmorphic Buttons ──
        // 1. Resume (Cyan/Teal)
        CreateCyberGothicButton(
            contentColumnRT, "ResumeBtn", "RESUME", "CONTINUE EXPEDITION", "✦",
            new Color(0.18f, 0.83f, 0.75f, 1f), // #2dd4bf
            () => ResumeGame()
        );

        // 2. Restart (Arcane Violet)
        CreateCyberGothicButton(
            contentColumnRT, "RestartBtn", "RESTART", "RETRY FROM CHECKPOINT", "❖",
            new Color(0.66f, 0.33f, 0.97f, 1f), // #a855f7
            () => RestartLevel()
        );

        // 3. Minigames (Aether Gold/Cyan)
        CreateCyberGothicButton(
            contentColumnRT, "MinigamesBtn", "ARCADE VAULT", "TRAINING & MINIGAMES", "◈",
            new Color(0.98f, 0.75f, 0.14f, 1f), // #fbbf24
            () => OpenMinigamesMenu()
        );

        // 4. Quit to Menu (Rose Crimson)
        CreateCyberGothicButton(
            contentColumnRT, "QuitBtn", "QUIT TO MENU", "RETURN TO TITLE SCREEN", "✕",
            new Color(0.96f, 0.25f, 0.37f, 1f), // #f43f5e
            () => QuitToMenu(menuSceneName)
        );

        // Footer hint
        CreateArcaneDivider(contentColumnRT, "BottomDivider");

        TextMeshProUGUI hintText = UIFactory.CreateText(
            contentColumnRT, "Hint", "PRESS ESC OR P TO RESUME",
            12f, new Color(0.55f, 0.72f, 0.90f, 0.75f), TextAlignmentOptions.Center);
        hintText.characterSpacing = 3f;
        
        LayoutElement hintLe = hintText.gameObject.AddComponent<LayoutElement>();
        hintLe.preferredWidth = 420f;
        hintLe.preferredHeight = 20f;

        // Ambient FX
        PauseMenuAmbientFX ambientFX = pauseOverlay.AddComponent<PauseMenuAmbientFX>();
        ambientFX.Initialize(null, vignetteRT, new Color(0.4f, 0.2f, 0.8f, 0.25f));

        pauseOverlay.SetActive(false);
    }

    // ── Procedural Cyber-Gothic Button Construction (Matching Main Menu) ──

    private Button CreateCyberGothicButton(
        Transform parent, string name, string labelText, string hintText, string runeIcon,
        Color themeColor, UnityEngine.Events.UnityAction onClick)
    {
        Vector2 size = new Vector2(380f, 68f);

        // Outer Button Container
        GameObject btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.transform.SetParent(parent, false);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.sizeDelta = size;

        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.minWidth = size.x;
        le.minHeight = size.y;
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;

        // Button Background (Dark Glassmorphic Cyber Frame)
        Image bgImg = btnGO.AddComponent<Image>();
        bgImg.sprite = CreateCyberFrameSprite(128, 64, 14);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.07f, 0.08f, 0.12f, 0.96f);

        // Glowing Border Outline
        RectTransform borderRT = UIFactory.CreatePanel(
            btnRT, "Border", new Color(themeColor.r, themeColor.g, themeColor.b, 0.50f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image borderImg = borderRT.GetComponent<Image>();
        borderImg.sprite = CreateCyberFrameSprite(128, 64, 14);
        borderImg.type = Image.Type.Sliced;
        borderImg.raycastTarget = false;

        // Top Accent Glow Line
        RectTransform topAccentRT = UIFactory.CreatePanel(
            btnRT, "TopAccent", new Color(themeColor.r, themeColor.g, themeColor.b, 0.70f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(12f, -3f), new Vector2(-12f, 0f));
        Image topAccentImg = topAccentRT.GetComponent<Image>();
        topAccentImg.raycastTarget = false;

        // Bottom Accent Line
        RectTransform botAccentRT = UIFactory.CreatePanel(
            btnRT, "BotAccent", new Color(themeColor.r, themeColor.g, themeColor.b, 0.30f),
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(20f, 0f), new Vector2(-20f, 2f));
        Image botAccentImg = botAccentRT.GetComponent<Image>();
        botAccentImg.raycastTarget = false;

        // Left Rune Badge Circle
        RectTransform runeLeftRT = UIFactory.CreatePanel(
            btnRT, "RuneLeft", new Color(themeColor.r, themeColor.g, themeColor.b, 0.18f),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(16f, -15f), new Vector2(46f, 15f));
        Image runeLeftBg = runeLeftRT.GetComponent<Image>();
        runeLeftBg.sprite = CreateOrbSprite(32);
        runeLeftBg.raycastTarget = false;

        TextMeshProUGUI runeLeftTxt = UIFactory.CreateText(
            runeLeftRT, "RuneTxt", runeIcon, 14f, themeColor, TextAlignmentOptions.Center);
        runeLeftTxt.fontStyle = FontStyles.Bold;

        // Right Rune Badge Circle
        RectTransform runeRightRT = UIFactory.CreatePanel(
            btnRT, "RuneRight", new Color(themeColor.r, themeColor.g, themeColor.b, 0.18f),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-46f, -15f), new Vector2(-16f, 15f));
        Image runeRightBg = runeRightRT.GetComponent<Image>();
        runeRightBg.sprite = CreateOrbSprite(32);
        runeRightBg.raycastTarget = false;

        TextMeshProUGUI runeRightTxt = UIFactory.CreateText(
            runeRightRT, "RuneTxt", runeIcon, 14f, themeColor, TextAlignmentOptions.Center);
        runeRightTxt.fontStyle = FontStyles.Bold;

        // Center Content Text (Title + Sub-hint)
        RectTransform textContainerRT = UIFactory.CreatePanel(
            btnRT, "TextContainer", Color.clear,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(50f, 0f), new Vector2(-50f, 0f));

        VerticalLayoutGroup textVlg = UIFactory.AddVerticalLayout(textContainerRT.gameObject, 1f,
            new RectOffset(0, 0, 8, 8), TextAnchor.MiddleCenter);
        textVlg.childControlWidth = false;
        textVlg.childControlHeight = false;

        TextMeshProUGUI label = UIFactory.CreateText(
            textContainerRT, "Label", labelText, 17f,
            new Color(0.96f, 0.97f, 1.0f, 1f), TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 5f;

        LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
        labelLe.preferredWidth = 260f;
        labelLe.preferredHeight = 24f;

        TextMeshProUGUI hint = UIFactory.CreateText(
            textContainerRT, "Hint", hintText, 9.5f,
            new Color(0.70f, 0.75f, 0.88f, 0.70f), TextAlignmentOptions.Center);
        hint.characterSpacing = 2f;

        LayoutElement hintLe = hint.gameObject.AddComponent<LayoutElement>();
        hintLe.preferredWidth = 260f;
        hintLe.preferredHeight = 16f;

        // Button Component
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.85f, 0.85f, 0.95f, 1f);
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        // Attach Interactive Cyber-Gothic Hover & Press Animation
        CyberGothicButtonFX fx = btnGO.AddComponent<CyberGothicButtonFX>();
        fx.Initialize(bgImg, borderImg, topAccentImg, botAccentImg, label, hint, themeColor);

        return btn;
    }

    // ── Decorative Elements ────────────────────────────────────────

    private void CreateArcaneDivider(Transform parent, string name)
    {
        RectTransform rowRT = UIFactory.CreatePanel(
            parent, name, Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rowRT.sizeDelta = new Vector2(380f, 16f);
        UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 16f, preferredWidth: 380f);

        HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 0f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        hlg.childControlWidth = false;
        hlg.childForceExpandWidth = false;

        // Left fade line
        CreateDividerSegment(rowRT, 130f, new Color(0.65f, 0.20f, 0.95f, 0.45f));
        // Left small orb
        CreateDividerOrb(rowRT, 6f, NeonPurpleDim);
        // Center line
        CreateDividerSegment(rowRT, 50f, new Color(0.50f, 0.15f, 0.85f, 0.65f));
        // Center orb (larger, brighter)
        CreateDividerOrb(rowRT, 10f, NeonPurple);
        // Right line
        CreateDividerSegment(rowRT, 50f, new Color(0.50f, 0.15f, 0.85f, 0.65f));
        // Right small orb
        CreateDividerOrb(rowRT, 6f, NeonPurpleDim);
        // Right fade line
        CreateDividerSegment(rowRT, 130f, new Color(0.65f, 0.20f, 0.95f, 0.45f));
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
        MainMenuUIToolkitController.isPlaying = false;

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

    /// <summary>Cyber-Gothic rounded rectangle frame — 9-slice ready, smooth corners.</summary>
    private static Sprite CreateCyberFrameSprite(int width, int height, int radius)
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
        if (x < r && y < r) return (r - x) * (r - x) + (r - y) * (r - y) <= r * r;
        if (x >= w - r && y < r) return (x - (w - r - 1)) * (x - (w - r - 1)) + (r - y) * (r - y) <= r * r;
        if (x < r && y >= h - r) return (r - x) * (r - x) + (y - (h - r - 1)) * (y - (h - r - 1)) <= r * r;
        if (x >= w - r && y >= h - r) return (x - (w - r - 1)) * (x - (w - r - 1)) + (y - (h - r - 1)) * (y - (h - r - 1)) <= r * r;
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

        return Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
    }

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

                float alpha;
                if (norm <= 0.5f)
                    alpha = 1f;
                else if (norm <= 1f)
                    alpha = 1f - (norm - 0.5f) * 2f;
                else
                    alpha = Mathf.Max(0f, 1f - (norm - 1f) * 3f) * 0.3f;

                alpha = alpha * alpha;
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
}

// ── Interactive Cyber-Gothic Button Animation (Unscaled Time) ──

public class CyberGothicButtonFX : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler,
    UnityEngine.EventSystems.IPointerUpHandler
{
    private RectTransform rectTransform;
    private Image bgImage;
    private Image borderImage;
    private Image topAccentImage;
    private Image botAccentImage;
    private TextMeshProUGUI labelText;
    private TextMeshProUGUI hintText;
    private Color themeColor;

    private Vector3 originalScale;
    private Vector3 targetScale = Vector3.one;
    private bool isHovered;

    private Color origBgColor;
    private Color targetBgColor;
    private Color origBorderColor;
    private Color targetBorderColor;
    private Color origTopAccentColor;
    private Color targetTopAccentColor;
    private Color origLabelColor;
    private Color targetLabelColor;

    public void Initialize(
        Image bg, Image border, Image topAccent, Image botAccent,
        TextMeshProUGUI label, TextMeshProUGUI hint, Color theme)
    {
        rectTransform = GetComponent<RectTransform>();
        bgImage = bg;
        borderImage = border;
        topAccentImage = topAccent;
        botAccentImage = botAccent;
        labelText = label;
        hintText = hint;
        themeColor = theme;

        originalScale = rectTransform.localScale;
        targetScale = originalScale;

        origBgColor = bgImage != null ? bgImage.color : new Color(0.07f, 0.08f, 0.12f, 0.96f);
        targetBgColor = origBgColor;

        origBorderColor = borderImage != null ? borderImage.color : new Color(theme.r, theme.g, theme.b, 0.50f);
        targetBorderColor = origBorderColor;

        origTopAccentColor = topAccentImage != null ? topAccentImage.color : new Color(theme.r, theme.g, theme.b, 0.70f);
        targetTopAccentColor = origTopAccentColor;

        origLabelColor = labelText != null ? labelText.color : Color.white;
        targetLabelColor = origLabelColor;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime * 14f;

        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, dt);

        if (bgImage != null)
            bgImage.color = Color.Lerp(bgImage.color, targetBgColor, dt);

        if (borderImage != null)
            borderImage.color = Color.Lerp(borderImage.color, targetBorderColor, dt);

        if (topAccentImage != null)
            topAccentImage.color = Color.Lerp(topAccentImage.color, targetTopAccentColor, dt);

        if (labelText != null)
            labelText.color = Color.Lerp(labelText.color, targetLabelColor, dt);
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isHovered = true;
        targetScale = originalScale * 1.04f;
        targetBgColor = new Color(0.14f, 0.08f, 0.24f, 0.98f);
        targetBorderColor = new Color(themeColor.r, themeColor.g, themeColor.b, 1.0f);
        targetTopAccentColor = new Color(1f, 1f, 1f, 1.0f); // Bright white-neon flash
        targetLabelColor = Color.white;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        isHovered = false;
        targetScale = originalScale;
        targetBgColor = origBgColor;
        targetBorderColor = origBorderColor;
        targetTopAccentColor = origTopAccentColor;
        targetLabelColor = origLabelColor;
    }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 0.96f;
        targetBgColor = new Color(themeColor.r * 0.3f, themeColor.g * 0.3f, themeColor.b * 0.3f, 0.98f);
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = isHovered ? originalScale * 1.04f : originalScale;
        targetBgColor = isHovered ? new Color(0.14f, 0.08f, 0.24f, 0.98f) : origBgColor;
    }
}
