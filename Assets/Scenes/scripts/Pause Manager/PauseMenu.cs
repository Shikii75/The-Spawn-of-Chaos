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

    // ── Palette: void black + arcane violet + neon purple ──
    private static readonly Color VoidOverlay       = new Color(0.03f, 0.01f, 0.07f, 0.90f);
    private static readonly Color NeonPurple        = new Color(0.78f, 0.18f, 1f, 1f);
    private static readonly Color NeonPurpleDim     = new Color(0.78f, 0.18f, 1f, 0.40f);
    private static readonly Color ArcaneViolet      = new Color(0.50f, 0.30f, 0.90f, 1f);
    private static readonly Color ArcaneVioletDim   = new Color(0.50f, 0.30f, 0.90f, 0.35f);
    private static readonly Color DeepVoid          = new Color(0.04f, 0.02f, 0.10f, 0.95f);
    private static readonly Color PanelInner        = new Color(0.06f, 0.03f, 0.14f, 0.93f);
    private static readonly Color FrameGlow         = new Color(0.65f, 0.20f, 0.95f, 0.70f);
    private static readonly Color FrameInner        = new Color(0.40f, 0.15f, 0.75f, 0.55f);

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
        BuildUI();
        Time.timeScale = 1f;
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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf)
                return;

            if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive)
                return;

            if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive)
                return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas("PauseCanvas", 10);
        canvas.transform.SetParent(transform, false);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.matchWidthOrHeight = 1.0f;

        // ── Layer 0: void overlay ──
        RectTransform overlayRT = UIFactory.CreateFullScreenPanel(
            canvas.transform, "PauseOverlay", VoidOverlay);
        pauseOverlay = overlayRT.gameObject;

        // ── Layer 1: radial vignette (deep purple tinted) ──
        Sprite vignetteSprite = CreateVignetteSprite(512, 512);
        RectTransform vignetteRT = UIFactory.CreatePanel(
            overlayRT, "Vignette", Color.white,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image vignetteImg = vignetteRT.GetComponent<Image>();
        vignetteImg.sprite = vignetteSprite;
        vignetteImg.raycastTarget = false;
        vignetteImg.color = new Color(0.05f, 0.01f, 0.10f, 0.88f);

        // ── Layer 2: arcane rune pattern (subtle tiled background) ──
        Sprite runeSprite = CreateRunePatternSprite(64, 64);
        RectTransform runeRT = UIFactory.CreatePanel(
            overlayRT, "RunePattern", Color.white,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image runeImg = runeRT.GetComponent<Image>();
        runeImg.sprite = runeSprite;
        runeImg.type = Image.Type.Tiled;
        runeImg.raycastTarget = false;
        runeImg.color = new Color(0.65f, 0.20f, 0.95f, 0.035f);

        // ── Layer 3: floating orb field (expanded area) ──
        RectTransform orbFieldRT = UIFactory.CreatePanel(
            overlayRT, "OrbField", Color.clear,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        orbFieldRT.GetComponent<Image>().raycastTarget = false;

        // ── Layer 4: outer mystic glow (soft rounded) ──
        Sprite roundedSprite = CreateRoundedFrameSprite(128, 128, 24);
        RectTransform glowRT = UIFactory.CreatePanel(
            overlayRT, "PauseGlow",
            new Color(0.65f, 0.12f, 1f, 0.10f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        glowRT.sizeDelta = new Vector2(620f, 720f);
        Image glowImg = glowRT.GetComponent<Image>();
        glowImg.sprite = roundedSprite;
        glowImg.type = Image.Type.Sliced;
        glowImg.raycastTarget = false;

        // ── Layer 5: main panel (rounded, dark) ──
        RectTransform panelRT = UIFactory.CreatePanel(
            overlayRT, "PausePanel", DeepVoid,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panelRT.sizeDelta = new Vector2(560f, 680f);

        Image panelImg = panelRT.GetComponent<Image>();
        panelImg.sprite = roundedSprite;
        panelImg.type = Image.Type.Sliced;

        // ── Soft outer border glow ──
        RectTransform borderGlowRT = UIFactory.CreatePanel(
            panelRT, "BorderGlow", Color.clear,
            Vector2.zero, Vector2.one,
            new Vector2(-3f, -3f), new Vector2(3f, 3f));
        Image borderGlowImg = borderGlowRT.GetComponent<Image>();
        borderGlowImg.sprite = roundedSprite;
        borderGlowImg.type = Image.Type.Sliced;
        borderGlowImg.color = FrameGlow;

        // ── Inner border ring ──
        RectTransform borderInnerRT = UIFactory.CreatePanel(
            panelRT, "BorderInner", Color.clear,
            Vector2.zero, Vector2.one,
            new Vector2(2f, 2f), new Vector2(-2f, -2f));
        Image borderInnerImg = borderInnerRT.GetComponent<Image>();
        borderInnerImg.sprite = roundedSprite;
        borderInnerImg.type = Image.Type.Sliced;
        borderInnerImg.color = FrameInner;

        // ── Inner fill ──
        RectTransform innerRT = UIFactory.CreatePanel(
            panelRT, "PanelInner", PanelInner,
            Vector2.zero, Vector2.one,
            new Vector2(5f, 5f), new Vector2(-5f, -5f));
        Image innerImg = innerRT.GetComponent<Image>();
        innerImg.sprite = roundedSprite;
        innerImg.type = Image.Type.Sliced;

        // ── Top mystic glow wash ──
        RectTransform topGlowRT = UIFactory.CreatePanel(
            innerRT, "TopGlow",
            new Color(0.60f, 0.15f, 0.95f, 0.06f),
            new Vector2(0f, 0.75f), new Vector2(1f, 1f),
            new Vector2(16f, 0f), new Vector2(-16f, -8f));
        Image topGlowImg = topGlowRT.GetComponent<Image>();
        topGlowImg.sprite = roundedSprite;
        topGlowImg.type = Image.Type.Sliced;
        topGlowImg.raycastTarget = false;

        // ── Content layout ──
        VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(panelRT.gameObject, 10f,
            new RectOffset(36, 36, 44, 36), TextAnchor.UpperCenter);
        vlg.childControlWidth = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // ── Ornament above title (arcane symbols) ──
        TextMeshProUGUI topOrnament = UIFactory.CreateText(
            panelRT, "TopOrnament", "⁕ ── ✦ ── ⁕",
            16f, NeonPurpleDim, TextAlignmentOptions.Center);
        topOrnament.characterSpacing = 6f;
        UIFactory.AddLayoutElement(topOrnament.gameObject, preferredHeight: 28f, preferredWidth: 480f);

        // ── Title ──
        TextMeshProUGUI titleText = UIFactory.CreateText(
            panelRT, "PausedTitle", "PAUSED",
            72f, UIFactory.TextWhite, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 18f;
        titleText.outlineColor = new Color32(170, 40, 255, 180);
        titleText.outlineWidth = 0.30f;
        UIFactory.AddLayoutElement(titleText.gameObject, preferredHeight: 90f, preferredWidth: 480f);

        // ── Subtitle (dark fantasy) ──
        TextMeshProUGUI subtitleText = UIFactory.CreateText(
            panelRT, "Subtitle", "✧  TIME STANDS STILL  ✧",
            18f, ArcaneVioletDim, TextAlignmentOptions.Center);
        subtitleText.fontStyle = FontStyles.Italic;
        subtitleText.characterSpacing = 3f;
        UIFactory.AddLayoutElement(subtitleText.gameObject, preferredHeight: 30f, preferredWidth: 480f);

        // ── Arcane divider ──
        CreateArcaneDivider(panelRT, "TitleDivider");

        // Spacer
        RectTransform spacer = UIFactory.CreatePanel(panelRT, "Spacer", Color.clear,
            Vector2.zero, Vector2.zero);
        UIFactory.AddLayoutElement(spacer.gameObject, preferredHeight: 12f, preferredWidth: 10f);

        // ── Buttons ──
        Vector2 buttonSize = new Vector2(420f, 72f);

        Button resumeBtn = CreateArcaneButton(panelRT, "ResumeButton", "RESUME", "▶", 28f, buttonSize, () => ResumeGame());
        UIFactory.AddLayoutElement(resumeBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);

        Button restartBtn = CreateArcaneButton(panelRT, "RestartButton", "RESTART", "↻", 28f, buttonSize, () => RestartLevel());
        UIFactory.AddLayoutElement(restartBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);

        Button quitBtn = CreateArcaneButton(panelRT, "QuitButton", "QUIT TO MENU", "⏻", 26f, buttonSize, () => QuitToMenu(menuSceneName));
        UIFactory.AddLayoutElement(quitBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);

        // Spacer
        RectTransform spacer2 = UIFactory.CreatePanel(panelRT, "Spacer2", Color.clear,
            Vector2.zero, Vector2.zero);
        UIFactory.AddLayoutElement(spacer2.gameObject, preferredHeight: 8f, preferredWidth: 10f);

        // ── Bottom divider + hint ──
        CreateArcaneDivider(panelRT, "BottomDivider");

        TextMeshProUGUI hintText = UIFactory.CreateText(
            panelRT, "Hint", "ESC  ·  P  —  RESUME",
            14f, new Color(0.55f, 0.35f, 0.75f, 0.65f), TextAlignmentOptions.Center);
        hintText.characterSpacing = 4f;
        UIFactory.AddLayoutElement(hintText.gameObject, preferredHeight: 24f, preferredWidth: 480f);

        // ── Ambient animation driver ──
        PauseMenuAmbientFX ambientFX = pauseOverlay.AddComponent<PauseMenuAmbientFX>();
        ambientFX.Initialize(glowImg, orbFieldRT, glowImg.color);

        pauseOverlay.SetActive(false);
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
        float fontSize, Vector2 size, UnityEngine.Events.UnityAction onClick)
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
            containerRT, name + "_Border", FrameGlow,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image borderImg = borderRT.GetComponent<Image>();
        borderImg.sprite = roundedSprite;
        borderImg.type = Image.Type.Sliced;

        // Inner bg
        RectTransform innerRT = UIFactory.CreatePanel(
            borderRT, name + "_Bg", new Color(0.06f, 0.03f, 0.14f, 0.94f),
            Vector2.zero, Vector2.one,
            new Vector2(2f, 2f), new Vector2(-2f, -2f));
        Image innerImg = innerRT.GetComponent<Image>();
        innerImg.sprite = roundedSprite;
        innerImg.type = Image.Type.Sliced;

        // Left accent — soft glow bar instead of hard stripe
        RectTransform accentRT = UIFactory.CreatePanel(
            innerRT, "AccentGlow",
            new Color(0.65f, 0.15f, 0.95f, 0.15f),
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(4f, 6f), new Vector2(12f, -6f));
        Image accentImg = accentRT.GetComponent<Image>();
        accentImg.sprite = CreateOrbSprite(16);
        accentImg.raycastTarget = false;

        Button btn = containerRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = innerImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.07f, 0.04f, 0.15f, 0.94f);
        cb.highlightedColor = new Color(0.12f, 0.07f, 0.25f, 0.98f);
        cb.pressedColor = new Color(0.08f, 0.04f, 0.18f, 0.98f);
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
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;
        TogglePause();
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

    private static readonly Color ArcanePurple = new Color(0.78f, 0.18f, 1f, 1f);
    private static readonly Color WarmHighlight = new Color(0.92f, 0.82f, 1f, 1f);

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
