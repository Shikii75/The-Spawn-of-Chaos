using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Main menu controller that builds its entire Canvas and panel hierarchy from code in Awake().
/// Exposes font sizes, styles, custom background, and logo sprites to the Inspector.
/// Buttons feature custom gothic frames with border radii (rounded corners) and hover/press animations.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Audio Customization")]
    [Tooltip("Sound clip to play when menu buttons are clicked.")]
    public AudioClip clickSound;
    [Tooltip("Music clip to play when the title menu is active.")]
    public AudioClip titleMusic;

    /// <summary>
    /// Scene name to load when the player presses Play.
    /// </summary>
    public string gameSceneName = "SampleScene";

    // State to track if we are currently playing, allowing single-scene menu clearance
    public static bool isPlaying = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        isPlaying = false;
    }

    [Header("Visual Customization")]
    [Tooltip("Logo sprite to display at the top of the menu. Automatically searched in Assets/Scenes/art/main_menu_logo.png.")]
    public Sprite logoSprite;
    [Tooltip("Size of the logo image on screen.")]
    public Vector2 logoSize = new Vector2(875f, 500f);
    [Tooltip("Background texture to display behind the menu. Automatically searched in Assets/Scenes/art/main_menu_bg.png.")]
    public Sprite backgroundSprite;
    [Tooltip("Color tint to apply to the background texture.")]
    public Color backgroundColor = new Color(0.85f, 0.82f, 0.95f, 1f); // Subtle gothic violet/blue tint to smoke

    [Header("Custom Button Sprites")]
    [Tooltip("PLAY button.")]
    public Sprite playButtonSprite;
    [Tooltip("OPTIONS button.")]
    public Sprite optionsButtonSprite;
    [Tooltip("QUIT button.")]
    public Sprite quitButtonSprite;

    [Header("Button Configuration")]
    [Tooltip("Dimensions of the menu buttons (width, height) - adjustable directly in Inspector.")]
    public Vector2 buttonSize = new Vector2(220f, 55f);

    [Header("Text Configuration")]
    [Tooltip("Font size of the game title text (used if no logo sprite is assigned).")]
    public float titleFontSize = 58f;
    [Tooltip("Whether the title text should be bold.")]
    public bool boldTitle = true;
    [Tooltip("Font size of the subtitle text.")]
    public float subtitleFontSize = 24f;
    [Tooltip("Font size of the buttons text.")]
    public float buttonFontSize = 26f;
    [Tooltip("Whether the button labels should be bold.")]
    public bool boldButtons = true;

    // ── Private UI references (built from code) ──
    private GameObject mainPanel;
    private GameObject optionsPanel;

#if UNITY_EDITOR
    void Reset()
    {
        // Try to automatically find and assign the logo and background sprites in the assets
        if (logoSprite == null)
        {
            logoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/main_menu_logo.png");
        }
        if (backgroundSprite == null)
        {
            backgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/main_menu_bg.png");
        }
        if (playButtonSprite == null)
        {
            playButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/play_btn.png");
        }
        if (optionsButtonSprite == null)
        {
            optionsButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/options_btn.png");
        }
        if (quitButtonSprite == null)
        {
            quitButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/quit_btn.png");
        }
    }
#endif

    void Awake()
    {
        if (isPlaying)
        {
            gameObject.SetActive(false);
            return;
        }

        // Editor runtime auto-load fallback
#if UNITY_EDITOR
        if (logoSprite == null)
        {
            logoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/main_menu_logo.png");
        }
        if (backgroundSprite == null)
        {
            backgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/main_menu_bg.png");
        }
        if (playButtonSprite == null)
        {
            playButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/play_btn.png");
        }
        if (optionsButtonSprite == null)
        {
            optionsButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/options_btn.png");
        }
        if (quitButtonSprite == null)
        {
            quitButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/quit_btn.png");
        }
#endif

        BuildUI();
    }

    void Start()
    {
        if (titleMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(titleMusic, true);
        }
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        // Create the menu canvas
        Canvas canvas = UIFactory.CreateCanvas("MainMenuCanvas", 0);
        canvas.transform.SetParent(transform, false);

        // Adjust CanvasScaler to match height (1.0f) to prevent vertical layout overflow on custom resolutions
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.matchWidthOrHeight = 1.0f;
        }

        // ═══════════════════════════════════════════════════════════
        //  MAIN PANEL — full-screen background
        // ═══════════════════════════════════════════════════════════
        RectTransform mainRT = UIFactory.CreateFullScreenPanel(
            canvas.transform, "MainPanel", Color.white);
        mainPanel = mainRT.gameObject;

        Image mainBgImg = mainPanel.GetComponent<Image>();
        if (backgroundSprite != null)
        {
            mainBgImg.sprite = backgroundSprite;
            mainBgImg.color = backgroundColor;
        }
        else
        {
            mainBgImg.color = UIFactory.PanelBackgroundSolid;
        }

        // Vertical Layout Group configured to NOT force expand or control width of child elements
        VerticalLayoutGroup mainVlg = UIFactory.AddVerticalLayout(mainPanel, 16f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        mainVlg.childControlWidth = false;
        mainVlg.childForceExpandWidth = false;

        // ── Title or Logo ──
        if (logoSprite != null)
        {
            RectTransform logoRT = UIFactory.CreatePanel(
                mainRT, "LogoImage", Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
            );
            logoRT.sizeDelta = logoSize;
            Image logoImg = logoRT.GetComponent<Image>();
            logoImg.sprite = logoSprite;
            logoImg.preserveAspect = true;
            UIFactory.AddLayoutElement(logoRT.gameObject, preferredWidth: logoSize.x, preferredHeight: logoSize.y);
        }
        else
        {
            // Fallback to text title if no sprite is assigned
            TextMeshProUGUI titleText = UIFactory.CreateText(
                mainRT, "GameTitle", "THE SPAWN OF CHAOS",
                titleFontSize, UIFactory.TextWhite, TextAlignmentOptions.Center);
            titleText.fontStyle = boldTitle ? FontStyles.Bold : FontStyles.Normal;
            titleText.rectTransform.sizeDelta = new Vector2(800f, 70f);
            UIFactory.AddLayoutElement(titleText.gameObject, preferredHeight: 70f, preferredWidth: 800f);
        }

        // ── Subtitle ──
        TextMeshProUGUI subtitleText = UIFactory.CreateText(
            mainRT, "Subtitle", "A Dark Fantasy Platformer",
            subtitleFontSize, UIFactory.TextMuted, TextAlignmentOptions.Center);
        subtitleText.rectTransform.sizeDelta = new Vector2(600f, 35f);
        UIFactory.AddLayoutElement(subtitleText.gameObject, preferredHeight: 35f, preferredWidth: 500f);

        // Decorative Separator
        RectTransform topDiv = UIFactory.CreateDivider(mainRT, "MenuTopDivider");
        topDiv.sizeDelta = new Vector2(buttonSize.x * 1.5f, 2f);
        UIFactory.AddLayoutElement(topDiv.gameObject, preferredHeight: 2f, preferredWidth: buttonSize.x * 1.5f);

        // Spacer
        RectTransform spacer1 = UIFactory.CreatePanel(mainRT, "Spacer1", Color.clear,
            Vector2.zero, Vector2.zero);
        spacer1.sizeDelta = new Vector2(10f, 15f);
        UIFactory.AddLayoutElement(spacer1.gameObject, preferredHeight: 15f, preferredWidth: 10f);

        // ── PLAY button ──
        Button playBtn;
        if (playButtonSprite != null)
        {
            playBtn = CreateImageButton(mainRT, "PlayButton", playButtonSprite, new Vector2(300f, 90f),
                () => PlayGame(gameSceneName));
            UIFactory.AddLayoutElement(playBtn.gameObject, preferredWidth: 300f, preferredHeight: 90f);
        }
        else
        {
            playBtn = CreateCustomButton(mainRT, "PlayButton", "PLAY", buttonFontSize, buttonSize,
                () => PlayGame(gameSceneName));
            UIFactory.AddLayoutElement(playBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);
        }

        // ── OPTIONS button ──
        Button optionsBtn;
        if (optionsButtonSprite != null)
        {
            optionsBtn = CreateImageButton(mainRT, "OptionsButton", optionsButtonSprite, new Vector2(300f, 85.7f),
                () => OpenOptions());
            UIFactory.AddLayoutElement(optionsBtn.gameObject, preferredWidth: 300f, preferredHeight: 85.7f);
        }
        else
        {
            optionsBtn = CreateCustomButton(mainRT, "OptionsButton", "OPTIONS", buttonFontSize, buttonSize,
                () => OpenOptions());
            UIFactory.AddLayoutElement(optionsBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);
        }

        // ── QUIT button ──
        Button quitBtn;
        if (quitButtonSprite != null)
        {
            quitBtn = CreateImageButton(mainRT, "QuitButton", quitButtonSprite, new Vector2(300f, 81.4f),
                () => QuitGame());
            UIFactory.AddLayoutElement(quitBtn.gameObject, preferredWidth: 300f, preferredHeight: 81.4f);
        }
        else
        {
            quitBtn = CreateCustomButton(mainRT, "QuitButton", "QUIT", buttonFontSize, buttonSize,
                () => QuitGame());
            UIFactory.AddLayoutElement(quitBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);
        }

        // Spacer
        RectTransform spacerB = UIFactory.CreatePanel(mainRT, "SpacerB", Color.clear,
            Vector2.zero, Vector2.zero);
        spacerB.sizeDelta = new Vector2(10f, 15f);
        UIFactory.AddLayoutElement(spacerB.gameObject, preferredHeight: 15f, preferredWidth: 10f);

        // Bottom Ornament
        TextMeshProUGUI ornament = UIFactory.CreateText(
            mainRT, "Ornament", "✦ ❖ ✦",
            18f, UIFactory.AccentDim, TextAlignmentOptions.Center
        );
        ornament.rectTransform.sizeDelta = new Vector2(200f, 30f);
        UIFactory.AddLayoutElement(ornament.gameObject, preferredHeight: 30f, preferredWidth: 200f);

        // ═══════════════════════════════════════════════════════════
        //  OPTIONS PANEL — full-screen background (hidden)
        // ═══════════════════════════════════════════════════════════
        RectTransform optionsRT = UIFactory.CreateFullScreenPanel(
            canvas.transform, "OptionsPanel", Color.white);
        optionsPanel = optionsRT.gameObject;

        Image optBgImg = optionsPanel.GetComponent<Image>();
        if (backgroundSprite != null)
        {
            optBgImg.sprite = backgroundSprite;
            optBgImg.color = backgroundColor;
        }
        else
        {
            optBgImg.color = UIFactory.PanelBackgroundSolid;
        }

        VerticalLayoutGroup optionsVlg = UIFactory.AddVerticalLayout(optionsPanel, 16f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        optionsVlg.childControlWidth = false;
        optionsVlg.childForceExpandWidth = false;

        // ── OPTIONS title ──
        TextMeshProUGUI optionsTitleText = UIFactory.CreateText(
            optionsRT, "OptionsTitle", "OPTIONS",
            titleFontSize * 0.8f, UIFactory.Accent, TextAlignmentOptions.Center);
        optionsTitleText.fontStyle = boldTitle ? FontStyles.Bold : FontStyles.Normal;
        optionsTitleText.rectTransform.sizeDelta = new Vector2(400f, 60f);
        UIFactory.AddLayoutElement(optionsTitleText.gameObject, preferredHeight: 60f, preferredWidth: 400f);

        // Divider
        RectTransform divider = UIFactory.CreateDivider(optionsRT, "OptionsDivider");
        divider.sizeDelta = new Vector2(500f, 2f);
        UIFactory.AddLayoutElement(divider.gameObject, preferredHeight: 2f, preferredWidth: 500f);

        // Spacer
        RectTransform spacer2 = UIFactory.CreatePanel(optionsRT, "Spacer2", Color.clear,
            Vector2.zero, Vector2.zero);
        spacer2.sizeDelta = new Vector2(10f, 60f);
        UIFactory.AddLayoutElement(spacer2.gameObject, preferredHeight: 60f, preferredWidth: 10f);

        // Placeholder info text
        TextMeshProUGUI placeholderText = UIFactory.CreateText(
            optionsRT, "PlaceholderText", "Options coming soon...",
            subtitleFontSize, UIFactory.TextMuted, TextAlignmentOptions.Center);
        placeholderText.rectTransform.sizeDelta = new Vector2(400f, 30f);
        UIFactory.AddLayoutElement(placeholderText.gameObject, preferredHeight: 30f, preferredWidth: 400f);

        // Spacer
        RectTransform spacer3 = UIFactory.CreatePanel(optionsRT, "Spacer3", Color.clear,
            Vector2.zero, Vector2.zero);
        spacer3.sizeDelta = new Vector2(10f, 40f);
        UIFactory.AddLayoutElement(spacer3.gameObject, preferredHeight: 40f, preferredWidth: 10f);

        // ── BACK button ──
        Button backBtn = CreateCustomButton(optionsRT, "BackButton", "BACK", buttonFontSize * 0.9f, new Vector2(200f, 50f),
            () => CloseOptions());
        UIFactory.AddLayoutElement(backBtn.gameObject, preferredWidth: 200f, preferredHeight: 50f);

        // Start with main panel active, options hidden
        mainPanel.SetActive(true);
        optionsPanel.SetActive(false);
    }

    // ── Button Generation Helper ────────────────────────────────────

    private Button CreateCustomButton(Transform parent, string name, string label, float fontSize, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        // Load the built-in Unity 9-slice rounded rectangle sprite to support border radius (rounded corners)
        Sprite roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        // 1. Create Border Container
        RectTransform borderRT = UIFactory.CreatePanel(
            parent, name + "_Border", 
            UIFactory.BorderColor, // Subtle purple border outline
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
        );
        borderRT.sizeDelta = size;
        
        Image borderImg = borderRT.GetComponent<Image>();
        if (roundedSprite != null)
        {
            borderImg.sprite = roundedSprite;
            borderImg.type = Image.Type.Sliced; // standard 9-slicing
        }
        borderImg.color = new Color(146f/255f, 104f/255f, 255f/255f, 0.65f);

        // 2. Create Inner Background
        RectTransform innerRT = UIFactory.CreatePanel(
            borderRT, name + "_Bg", 
            new Color(0.08f, 0.04f, 0.18f, 0.94f), // richer anime/VG purple button background
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f) // 3px inner padding for a stronger border effect
        );

        Image innerImg = innerRT.GetComponent<Image>();
        if (roundedSprite != null)
        {
            innerImg.sprite = roundedSprite;
            innerImg.type = Image.Type.Sliced;
        }
        innerImg.color = new Color(0.10f, 0.06f, 0.22f, 0.96f);

        // 3. Add Button component to the border container (to capture clicks)
        Button btn = borderRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = innerImg; // Transition the inner graphic color
        
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.12f, 0.08f, 0.25f, 0.92f);
        cb.highlightedColor = new Color(0.45f, 0.28f, 1f, 0.95f);
        cb.pressedColor = new Color(0.28f, 0.16f, 0.78f, 0.95f);
        cb.disabledColor = new Color(0.18f, 0.14f, 0.22f, 0.45f);
        cb.colorMultiplier = 1.1f;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        // 4. Add Text Label
        TextMeshProUGUI text = UIFactory.CreateText(
            innerRT, "Label", label,
            fontSize, UIFactory.TextWhite, TextAlignmentOptions.Center
        );
        UIFactory.SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        text.fontStyle = boldButtons ? FontStyles.Bold : FontStyles.Normal;
        text.characterSpacing = 4f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.78f);
        text.outlineWidth = 0.24f;

        // 5. Add Custom Hover/Scale Script
        MenuButtonEffects fx = borderRT.gameObject.AddComponent<MenuButtonEffects>();
        fx.Initialize(text, innerImg, borderImg);

        return btn;
    }

    private Button CreateImageButton(Transform parent, string name, Sprite buttonSprite, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        // 1. Create Container (invisible, captures clicks)
        RectTransform containerRT = UIFactory.CreatePanel(
            parent, name + "_Container", 
            Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
        );
        containerRT.sizeDelta = size;

        // 2. Create Glow Underlay (slightly larger, starts fully transparent)
        RectTransform glowRT = UIFactory.CreatePanel(
            containerRT, name + "_Glow", 
            Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
        );
        glowRT.sizeDelta = size * 1.15f; 
        Image glowImg = glowRT.gameObject.GetComponent<Image>();
        glowImg.sprite = buttonSprite;
        glowImg.color = new Color(0.8f, 0.4f, 1f, 0f); // Bright violet/purple glow
        glowImg.preserveAspect = true;

        // 3. Create Button Image
        RectTransform buttonRT = UIFactory.CreatePanel(
            containerRT, name + "_Image", 
            Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
        );
        buttonRT.sizeDelta = size;
        Image buttonImg = buttonRT.gameObject.GetComponent<Image>();
        buttonImg.sprite = buttonSprite;
        buttonImg.preserveAspect = true;

        // 4. Add Button component to container
        Button btn = containerRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = buttonImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        cb.colorMultiplier = 1.1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        // 5. Add custom Hover/Scale/Glow effect script
        ImageButtonEffects fx = containerRT.gameObject.AddComponent<ImageButtonEffects>();
        fx.Initialize(buttonImg, glowImg);

        return btn;
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>
    /// Loads a gameplay scene by name to start the demo.
    /// </summary>
    public void PlayGame(string sceneName)
    {
        PlayClickSFX();
        isPlaying = true; // Set playing to true so menu clears on reload

        // Hide panels immediately
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("MainMenuController: Scene name to play is empty!", this);
        }
    }

    /// <summary>
    /// Opens the options panel and hides the main menu panel.
    /// </summary>
    public void OpenOptions()
    {
        PlayClickSFX();
        if (optionsPanel != null) optionsPanel.SetActive(true);
        if (mainPanel != null) mainPanel.SetActive(false);
    }

    /// <summary>
    /// Closes options panel and returns to the main menu panel.
    /// </summary>
    public void CloseOptions()
    {
        PlayClickSFX();
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    /// <summary>
    /// Quits the built standalone executable or stops play mode in editor.
    /// </summary>
    public void QuitGame()
    {
        PlayClickSFX();
        Debug.Log("MainMenuController: Quit application requested.");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    private void PlayClickSFX()
    {
        if (clickSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clickSound);
        }
    }
}

// ──────────────────────────────────────────────────────────────────────
//  HELPER COMPONENT — Hover & press transitions for main menu buttons
// ──────────────────────────────────────────────────────────────────────

public class MenuButtonEffects : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private RectTransform rectTransform;
    private TextMeshProUGUI buttonText;
    private Image buttonImage;
    private Image borderImage;
    private string originalText;
    
    private Vector3 targetScale = Vector3.one;
    private Color targetTextColor;
    private Color targetBorderColor;
    
    private Color originalTextColor;
    private Color originalBorderColor;
    private Vector3 originalScale;

    private float lerpSpeed = 12f;
    private bool isHovered = false;

    public void Initialize(TextMeshProUGUI txt, Image buttonBg, Image border)
    {
        rectTransform = GetComponent<RectTransform>();
        buttonText = txt;
        buttonImage = buttonBg;
        borderImage = border;
        
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
        // Smoothly scale the button container
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * lerpSpeed);
        
        // Smoothly fade text and border colors
        if (buttonText != null)
        {
            buttonText.color = Color.Lerp(buttonText.color, targetTextColor, Time.deltaTime * lerpSpeed);
        }
        if (borderImage != null)
        {
            borderImage.color = Color.Lerp(borderImage.color, targetBorderColor, Time.deltaTime * lerpSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetScale = originalScale * 1.08f; // Scale up 8%
        targetTextColor = new Color(1f, 0.92f, 0.72f, 1f);  // Warm highlight for game style
        
        if (borderImage != null)
        {
            targetBorderColor = UIFactory.Accent; // Glow border purple
        }
        
        if (buttonText != null)
        {
            // Add energetic pointer arrows
            buttonText.text = "▶  " + originalText + "  ◀";
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetScale = originalScale;
        targetTextColor = originalTextColor;
        
        if (borderImage != null)
        {
            targetBorderColor = originalBorderColor;
        }
        
        if (buttonText != null)
        {
            buttonText.text = originalText;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * 0.95f; // Squish down slightly on click
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isHovered)
        {
            targetScale = originalScale * 1.08f;
        }
        else
        {
            targetScale = originalScale;
        }
    }
}

// ──────────────────────────────────────────────────────────────────────
//  HELPER COMPONENT — Hover, scale & glow transitions for image buttons
// ──────────────────────────────────────────────────────────────────────
public class ImageButtonEffects : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private RectTransform rectTransform;
    private Image buttonImage;
    private Image glowImage;
    
    private Vector3 targetScale = Vector3.one;
    private float targetGlowAlpha = 0f;
    private float currentGlowAlpha = 0f;
    
    private Vector3 originalScale;
    private float lerpSpeed = 12f;
    private bool isHovered = false;
    private float pulseTimer = 0f;

    public void Initialize(Image btnImg, Image glwImg)
    {
        rectTransform = GetComponent<RectTransform>();
        buttonImage = btnImg;
        glowImage = glwImg;
        
        originalScale = rectTransform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smoothly scale the button container
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * lerpSpeed);
        
        // Smoothly fade glow opacity
        currentGlowAlpha = Mathf.Lerp(currentGlowAlpha, targetGlowAlpha, Time.deltaTime * lerpSpeed);
        
        if (glowImage != null)
        {
            Color c = glowImage.color;
            if (isHovered)
            {
                // Pulsate the glow while hovered for extra high quality feel
                pulseTimer += Time.deltaTime * 5f;
                float pulse = 0.7f + Mathf.Sin(pulseTimer) * 0.2f; // pulsates between 0.5 and 0.9
                c.a = currentGlowAlpha * pulse;
            }
            else
            {
                c.a = currentGlowAlpha;
            }
            glowImage.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetScale = originalScale * 1.06f; // Scale up 6%
        targetGlowAlpha = 0.85f;             // Fade in glow
        pulseTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetScale = originalScale;
        targetGlowAlpha = 0f;                // Fade out glow
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * 0.96f; // Squish slightly on press
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isHovered)
        {
            targetScale = originalScale * 1.06f;
        }
        else
        {
            targetScale = originalScale;
        }
    }
}
