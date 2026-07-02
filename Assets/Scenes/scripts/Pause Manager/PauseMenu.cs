using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Pause menu that builds a small, centered pause box with styled buttons.
/// Triggered by the "P" key or Escape.
/// Canvas sort order 10 keeps it above other game UI.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    public bool isPaused { get; private set; }

    /// <summary>
    /// Scene name to load when the player selects "Quit to Menu".
    /// </summary>
    public string menuSceneName = "MainMenu";

    // ── Private UI references (built from code) ──
    private GameObject pauseOverlay;

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
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If any UI is active and can consume Escape, return early
            if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf)
            {
                return;
            }

            if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive)
            {
                return;
            }

            if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive)
            {
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas at sort order 10, above all other game UI
        Canvas canvas = UIFactory.CreateCanvas("PauseCanvas", 10);
        canvas.transform.SetParent(transform, false);

        // Adjust CanvasScaler to match height (1.0f) to prevent vertical layout overflow
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.matchWidthOrHeight = 1.0f;
        }

        // Full-screen dark semi-transparent overlay
        RectTransform overlayRT = UIFactory.CreateFullScreenPanel(
            canvas.transform, "PauseOverlay", new Color(0f, 0f, 0f, 0.6f));
        pauseOverlay = overlayRT.gameObject;

        // The smaller box for the pause menu itself
        RectTransform panelRT = UIFactory.CreatePanel(
            overlayRT, "PauseBox", 
            new Color(0.10f, 0.06f, 0.22f, 0.98f), // Dark gothic purple
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panelRT.sizeDelta = new Vector2(500f, 620f);
        
        // Add rounded corners to the box
        Image panelImg = panelRT.GetComponent<Image>();
        Sprite roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        if (roundedSprite != null)
        {
            panelImg.sprite = roundedSprite;
            panelImg.type = Image.Type.Sliced;
        }

        // Add an outline border to the box
        Outline outline = panelRT.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(146f / 255f, 104f / 255f, 255f / 255f, 0.8f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Vertical layout for centered content inside the small box
        VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(panelRT.gameObject, 14f,
            new RectOffset(30, 30, 36, 36), TextAnchor.UpperCenter);
        vlg.childControlWidth = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // ── PAUSED title ──
        TextMeshProUGUI titleText = UIFactory.CreateText(
            panelRT, "PausedTitle", "PAUSED",
            64f, UIFactory.TextWhite, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 12f;
        UIFactory.AddLayoutElement(titleText.gameObject, preferredHeight: 85f, preferredWidth: 440f);

        // Decorative Separator
        RectTransform topDiv = UIFactory.CreateDivider(panelRT, "MenuTopDivider");
        topDiv.sizeDelta = new Vector2(360f, 3f);
        Image topDivImg = topDiv.GetComponent<Image>();
        if (topDivImg != null) topDivImg.color = new Color(146f / 255f, 104f / 255f, 255f / 255f, 0.8f);
        UIFactory.AddLayoutElement(topDiv.gameObject, preferredHeight: 3f, preferredWidth: 360f);

        // Spacer
        RectTransform spacer = UIFactory.CreatePanel(panelRT, "Spacer", Color.clear,
            Vector2.zero, Vector2.zero);
        UIFactory.AddLayoutElement(spacer.gameObject, preferredHeight: 16f, preferredWidth: 10f);

        // ── Load Sprites (Editor only for auto-pickup) ──
        Sprite resumeSprite = null;
        Sprite restartSprite = null;
        Sprite quitSprite = null;

#if UNITY_EDITOR
        resumeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/pause_resume.png");
        restartSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/pause_restart.png");
        quitSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/art/pause_quit.png");
#endif

        // Also try loading from Resources as a fallback for builds
        if (resumeSprite == null)  resumeSprite  = Resources.Load<Sprite>("pause_resume");
        if (restartSprite == null) restartSprite = Resources.Load<Sprite>("pause_restart");
        if (quitSprite == null)    quitSprite    = Resources.Load<Sprite>("pause_quit");

        Vector2 buttonSize = new Vector2(400f, 120f); // Bigger size for the gothic bat-wing buttons

        // ── RESUME button ──
        Button resumeBtn;
        if (resumeSprite != null)
        {
            resumeBtn = CreateImageButton(panelRT, "ResumeButton", resumeSprite, buttonSize, () => ResumeGame());
        }
        else
        {
            resumeBtn = CreateCustomButton(panelRT, "ResumeButton", "RESUME", 32f, new Vector2(340f, 70f), () => ResumeGame());
        }
        UIFactory.AddLayoutElement(resumeBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);

        // ── RESTART LEVEL button ──
        Button restartBtn;
        if (restartSprite != null)
        {
            restartBtn = CreateImageButton(panelRT, "RestartButton", restartSprite, buttonSize, () => RestartLevel());
        }
        else
        {
            restartBtn = CreateCustomButton(panelRT, "RestartButton", "RESTART", 32f, new Vector2(340f, 70f), () => RestartLevel());
        }
        UIFactory.AddLayoutElement(restartBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);

        // ── QUIT TO MENU button ──
        Button quitBtn;
        if (quitSprite != null)
        {
            quitBtn = CreateImageButton(panelRT, "QuitButton", quitSprite, buttonSize, () => QuitToMenu(menuSceneName));
        }
        else
        {
            quitBtn = CreateCustomButton(panelRT, "QuitButton", "QUIT", 32f, new Vector2(340f, 70f), () => QuitToMenu(menuSceneName));
        }
        UIFactory.AddLayoutElement(quitBtn.gameObject, preferredWidth: buttonSize.x, preferredHeight: buttonSize.y);

        // Start hidden
        pauseOverlay.SetActive(false);
    }

    // ── Button Generation Helpers ──

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

        // 5. Add custom Hover/Scale/Glow effect script (from MainMenuController.cs)
        ImageButtonEffects fx = containerRT.gameObject.AddComponent<ImageButtonEffects>();
        fx.Initialize(buttonImg, glowImg);

        return btn;
    }

    private Button CreateCustomButton(Transform parent, string name, string label, float fontSize, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Sprite roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        // 1. Create Border Container
        RectTransform borderRT = UIFactory.CreatePanel(
            parent, name + "_Border", 
            UIFactory.BorderColor, 
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
        );
        borderRT.sizeDelta = size;
        
        Image borderImg = borderRT.GetComponent<Image>();
        if (roundedSprite != null)
        {
            borderImg.sprite = roundedSprite;
            borderImg.type = Image.Type.Sliced;
        }
        borderImg.color = new Color(146f / 255f, 104f / 255f, 255f / 255f, 0.65f);

        // 2. Create Inner Background
        RectTransform innerRT = UIFactory.CreatePanel(
            borderRT, name + "_Bg", 
            new Color(0.08f, 0.04f, 0.18f, 0.94f),
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f)
        );

        Image innerImg = innerRT.GetComponent<Image>();
        if (roundedSprite != null)
        {
            innerImg.sprite = roundedSprite;
            innerImg.type = Image.Type.Sliced;
        }
        innerImg.color = new Color(0.10f, 0.06f, 0.22f, 0.96f);

        // 3. Add Button component
        Button btn = borderRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = innerImg;
        
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
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 4f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.78f);
        text.outlineWidth = 0.24f;

        // 5. Add Custom Hover/Scale Script (from MainMenuController.cs)
        MenuButtonEffects fx = borderRT.gameObject.AddComponent<MenuButtonEffects>();
        fx.Initialize(text, innerImg, borderImg);

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
        MainMenuController.isPlaying = false; // Reset play state so menu shows on reload

        if (Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
        }
        else
        {
            // If the menu scene doesn't exist, reload the current scene to go back to the start and show the menu
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
