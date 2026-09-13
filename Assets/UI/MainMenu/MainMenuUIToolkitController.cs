using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using SpawnOfChaos.Systems;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

/// <summary>
/// High-aesthetic Controller for the UI Toolkit Main Menu.
/// Plays full-speed frame animation sequence from the 'keep-54947fd1' folder for the centerpiece logo.
/// Features gentle fireflies floating upward through the menu atmosphere.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuUIToolkitController : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Target scene to load when Enter Realm is clicked.")]
    public string gameSceneName = "TutorialScene";

    /// <summary>
    /// Global state to track if gameplay is active or title menu is showing.
    /// </summary>
    public static bool isPlaying = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitStaticState()
    {
        isPlaying = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureMainMenuActive()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene") return;
        if (PlayerSpawnPointManager.isRespawning) return;

        var allMenus = Resources.FindObjectsOfTypeAll<MainMenuUIToolkitController>();
        foreach (var m in allMenus)
        {
            if (m.gameObject.scene == SceneManager.GetActiveScene())
            {
                if (!m.gameObject.activeSelf && !isPlaying)
                {
                    Debug.Log("[MainMenuUIToolkitController] Auto-activating MainMenu_UIToolkit on scene start.");
                    m.gameObject.SetActive(true);
                }
                break;
            }
        }
    }

    [Header("Logo Frame Animation & Sizing")]
    [Tooltip("Dimensions of the logo in pixels (Width, Height).")]
    public Vector2 logoDimensions = new Vector2(560f, 315f);
    [Tooltip("Overall scale multiplier for the logo animation.")]
    [Range(0.2f, 2.5f)]
    public float logoScale = 1.0f;
    [Tooltip("Folder path containing the animation frames.")]
    public string framesFolderPath = "Assets/Scenes/animations/frames/keep-54947fd1";
    [Tooltip("Frames per second for the logo animation.")]
    [Range(4f, 60f)]
    public float animationFPS = 16f;
    [Tooltip("Whether to continuously loop the logo animation.")]
    public bool loopAnimation = true;
    [Tooltip("Pre-assigned animation frames (populated automatically if empty).")]
    public Texture2D[] logoAnimationFrames;

    [Header("Logo Wing Flap & Pause Behavior")]
    [Tooltip("Whether to pause the logo animation when the wings reach full extension.")]
    public bool pauseWhenWingsFullyExtended = true;

    [Tooltip("Frame index where wings are fully extended. Frame 8 (frame_009) is the first full horizontal wing expansion. Frame 46 (frame_047) is the grand wingspan with smoke.")]
    public int fullyExtendedFrameIndex = 8;

    [Tooltip("Duration in seconds to stay paused on fully extended wings. If <= 0, pauses indefinitely on extended wings.")]
    public float fullyExtendedHoldDuration = 0f;

    [Tooltip("Duration in seconds to linger/hold when the wings reach their highest apex (frames 16-18, especially frame 17).")]
    public float highestWingsHoldDuration = 0.65f;

    [Header("Gentle Fireflies (Bottom to Top)")]
    [Tooltip("Total number of gentle fireflies.")]
    [Range(10, 80)]
    public int orbCount = 38;
    [Tooltip("Upward drift speed multiplier.")]
    [Range(0.2f, 3f)]
    public float fallSpeedMultiplier = 1.0f;
    [Tooltip("Horizontal firefly sway intensity.")]
    [Range(0.2f, 3f)]
    public float swayIntensity = 1.0f;

    [Header("Audio SFX & Music")]
    [Tooltip("Background music track played on the title menu.")]
    public AudioClip titleMusic;
    public AudioClip hoverClip;
    public AudioClip clickClip;
    public AudioClip modalOpenClip;

    private UIDocument uiDocument;
    private VisualElement root;

    // Visual Elements for Animation
    private VisualElement titleLogo;
    private VisualElement titleContainer;
    private VisualElement ambientGlow;
    private VisualElement orbsContainer;
    private Button btnPlay;
    private Button btnContinue;
    private Button btnNewGame;
    private Button btnLoadGame;
    private Button btnSettings;
    private Button btnQuit;

    // Modals & Settings
    private VisualElement modalSettings;
    private VisualElement modalLoadGame;
    private Button btnCloseSettings;
    private Button btnCloseLoadGame;
    private VisualElement slotsGrid;
    private Slider sliderMaster;
    private Slider sliderMusic;
    private Slider sliderSFX;
    private Toggle toggleFullscreen;
    private Toggle toggleScreenShake;

    private Coroutine frameAnimationCoroutine;
    private Coroutine glowPulseCoroutine;

    // Spirit Orb Particle Simulation
    private class SpiritOrb
    {
        public VisualElement element;
        public float baseX;         // Fixed horizontal anchor (0..1)
        public float posY;          // Vertical position (0..1, where 1 is bottom, 0 is top)
        public float speedY;        // Upward drift speed
        public float swayAmp;       // Horizontal sway amplitude
        public float swayFreq;      // Horizontal sway frequency
        public float swayPhase;     // Phase offset
        public float size;          // Diameter in px
        public float baseAlpha;     // Peak opacity
        public float pulseSpeed;    // Gentle breathing frequency
        public float pulsePhase;    // Breathing offset
        public Color color;         // Glowing purple/lavender color
        public int depthLayer;      // 0 = Foreground 3D sphere, 1 = Midground, 2 = Deep Blur Bokeh
    }

    private static Texture2D s_blurryBokehTexture;

    private static void EnsureOrbTextures()
    {
        if (s_blurryBokehTexture == null)
        {
            s_blurryBokehTexture = GenerateBlurryBokehTexture();
        }
    }

    private static Texture2D GenerateBlurryBokehTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.49f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (dist >= 1.0f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // Pure out-of-focus optical bokeh Gaussian blur with smooth zero-falloff edge
                float edgeFactor = Mathf.Clamp01(1.0f - dist);
                float smoothEdge = edgeFactor * edgeFactor * (3.0f - 2.0f * edgeFactor);
                // Ultra-diffuse wide Gaussian curve - soft, misty, dreamy ethereal glow
                float alpha = Mathf.Exp(-1.9f * dist * dist) * smoothEdge;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    private readonly List<SpiritOrb> activeOrbs = new List<SpiritOrb>();

    // Warm, restrained lights that read as fireflies against the dark menu.
    // Deep cosmic purple and royal violet atmospheric lights matching Tutorial prologue
    private static readonly Color[] OrbColors = new Color[]
    {
        new Color(0.65f, 0.20f, 0.95f, 0.85f), // Radiant violet (Opening Scene)
        new Color(0.85f, 0.35f, 1.00f, 0.88f), // Neon purple (Opening Scene)
        new Color(0.92f, 0.50f, 0.98f, 0.82f), // Ethereal magenta (Opening Scene)
        new Color(0.48f, 0.15f, 0.85f, 0.86f), // Deep amethyst (Opening Scene)
        new Color(0.78f, 0.45f, 0.90f, 0.84f)  // Soft lilac (Opening Scene)
    };

    private void Awake()
    {
        // One-time auto-migration: adjust to refined goldilocks scale (720x405)
        if (logoScale > 1.4f)
        {
            logoScale = 1.0f;
        }
        if (logoDimensions.x < 620f || logoDimensions.x > 800f)
        {
            logoDimensions = new Vector2(720f, 405f);
        }

        uiDocument = GetComponent<UIDocument>();
        LoadFramesIfNeeded();
        EnsureTitleMusicAssigned();
    }

    private void Start()
    {
        if (!isPlaying)
        {
            Time.timeScale = 0f;
            EnablePlayerGameplay(false);
            if (HUDManager.Instance != null) HUDManager.Instance.UpdateVisibility();
            if (SpawnOfChaos.Minigames.HUDOrbPanel.Instance != null) SpawnOfChaos.Minigames.HUDOrbPanel.Instance.UpdateVisibility();
        }
        PlayTitleMusic();
    }

    private void EnablePlayerGameplay(bool enable)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("Player");
        if (p == null) p = GameObject.Find("BasePlayer");

        if (p != null)
        {
            move m = p.GetComponent<move>();
            if (m != null) m.enabled = enable;

            MageCombat mc = p.GetComponent<MageCombat>();
            if (mc != null) mc.enabled = enable;

            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                if (!enable)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.simulated = false;
                }
                else
                {
                    rb.simulated = true;
                }
            }
        }
    }

    private void OnEnable()
    {
        if (PlayerSpawnPointManager.isRespawning)
        {
            isPlaying = true;
            Time.timeScale = 1f;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                uiDocument.rootVisualElement.style.display = DisplayStyle.None;
            }
            gameObject.SetActive(false);
            return;
        }

        isPlaying = false;
        Time.timeScale = 0f; // Freeze game background when Main Menu is up
        EnablePlayerGameplay(false);
        if (HUDManager.Instance != null) HUDManager.Instance.UpdateVisibility();
        if (SpawnOfChaos.Minigames.HUDOrbPanel.Instance != null) SpawnOfChaos.Minigames.HUDOrbPanel.Instance.UpdateVisibility();

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null) return;

        root = uiDocument.rootVisualElement;
        if (root == null) return;
        root.style.display = DisplayStyle.Flex;

        BindVisualElements();
        RegisterCallbacks();
        LoadSavedSettings();
        InitializeSpiritOrbs();
        PlayTitleMusic();

        // Start logo frame-by-frame animation
        if (frameAnimationCoroutine != null) StopCoroutine(frameAnimationCoroutine);
        frameAnimationCoroutine = StartCoroutine(PlayLogoFrameAnimation());

        // Start ambient background pulse
        if (glowPulseCoroutine != null) StopCoroutine(glowPulseCoroutine);
        glowPulseCoroutine = StartCoroutine(AnimateGlowPulse());
    }

    private void OnDisable()
    {
        if (frameAnimationCoroutine != null)
        {
            StopCoroutine(frameAnimationCoroutine);
            frameAnimationCoroutine = null;
        }
        if (glowPulseCoroutine != null)
        {
            StopCoroutine(glowPulseCoroutine);
            glowPulseCoroutine = null;
        }
        ClearSpiritOrbs();
        UnregisterCallbacks();
    }

    private void Update()
    {
        // Continuously ensure game background is frozen while in title screen
        if (!isPlaying)
        {
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            EnablePlayerGameplay(false);
        }

        // ESC key closes modal
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (modalLoadGame != null && modalLoadGame.ClassListContains("panel--visible"))
            {
                CloseModal(modalLoadGame);
            }
            else if (modalSettings != null && modalSettings.ClassListContains("panel--visible"))
            {
                CloseModal(modalSettings);
            }
        }

        // Update atmospheric living void purple orbs drifting bottom to top
        UpdateSpiritOrbs(Time.unscaledDeltaTime);
    }

    private void BindVisualElements()
    {
        titleLogo = root.Q<VisualElement>("title-logo");
        titleContainer = root.Q<VisualElement>("title-container");
        ambientGlow = root.Q<VisualElement>("ambient-glow");
        orbsContainer = root.Q<VisualElement>("orbs-container");
        if (orbsContainer != null)
        {
            orbsContainer.pickingMode = PickingMode.Ignore;
            orbsContainer.style.display = DisplayStyle.Flex;
        }

        ApplyLogoSize();

        btnPlay = root.Q<Button>("btn-play");
        btnContinue = root.Q<Button>("btn-continue");
        btnNewGame = root.Q<Button>("btn-new-game");
        btnLoadGame = root.Q<Button>("btn-load-game");
        btnSettings = root.Q<Button>("btn-settings");
        btnQuit = root.Q<Button>("btn-quit");

        modalSettings = root.Q<VisualElement>("modal-settings");
        modalLoadGame = root.Q<VisualElement>("modal-load-game");
        btnCloseSettings = root.Q<Button>("btn-close-settings");
        btnCloseLoadGame = root.Q<Button>("btn-close-load-game");
        slotsGrid = root.Q<VisualElement>("slots-grid");

        RefreshContinueButtonState();

        sliderMaster = root.Q<Slider>("slider-master");
        sliderMusic = root.Q<Slider>("slider-music");
        sliderSFX = root.Q<Slider>("slider-sfx");
        toggleFullscreen = root.Q<Toggle>("toggle-fullscreen");
        toggleScreenShake = root.Q<Toggle>("toggle-screenshake");
    }

    public void ApplyLogoSize()
    {
        if (titleLogo != null)
        {
            if (logoScale > 1.4f) logoScale = 1.0f;
            logoDimensions = new Vector2(560f, 315f);

            // Prominent 16:9 centerpiece logo scaling
            titleLogo.style.width = logoDimensions.x * logoScale;
            titleLogo.style.height = logoDimensions.y * logoScale;
            titleLogo.style.maxWidth = Length.Percent(68f);
            titleLogo.style.maxHeight = Length.Percent(46f);
            titleLogo.style.flexShrink = 1f;
        }
    }

    private void OnValidate()
    {
        if (root != null && titleLogo != null)
        {
            ApplyLogoSize();
        }
    }

    #region Gentle Fireflies Simulation (Bottom to Top)

    private void InitializeSpiritOrbs()
    {
        ClearSpiritOrbs();
        if (orbsContainer == null) return;

        for (int i = 0; i < orbCount; i++)
        {
            SpiritOrb orb = CreateSpiritOrb(randomY: true);
            activeOrbs.Add(orb);
            orbsContainer.Add(orb.element);
        }
    }

    private void ClearSpiritOrbs()
    {
        if (orbsContainer != null)
        {
            orbsContainer.Clear();
        }
        activeOrbs.Clear();
    }

    private SpiritOrb CreateSpiritOrb(bool randomY)
    {
        EnsureOrbTextures();

        SpiritOrb orb = new SpiritOrb();
        orb.element = new VisualElement();
        orb.element.AddToClassList("menu-firefly");
        orb.element.pickingMode = PickingMode.Ignore;

        // Pure out-of-focus optical bokeh blur for all orbs
        orb.element.style.backgroundImage = new StyleBackground(s_blurryBokehTexture);

        // Multi-Plane Depth Layers with rich blurry bokeh sizing:
        // Layer 2: Deep background blur (soft ambient, smaller)
        // Layer 1: Midground dreamy bokeh
        // Layer 0: Foreground large out-of-focus bokeh orbs
        float layerRoll = Random.value;
        if (layerRoll < 0.38f)
        {
            orb.depthLayer = 2; // Deep Background Ambient Haze
            orb.size = Random.Range(45f, 80f);
            orb.baseAlpha = Random.Range(0.20f, 0.40f);
            orb.speedY = Random.Range(0.040f, 0.068f) * fallSpeedMultiplier;
        }
        else if (layerRoll < 0.78f)
        {
            orb.depthLayer = 1; // Midground Dreamy Bokeh
            orb.size = Random.Range(85f, 140f);
            orb.baseAlpha = Random.Range(0.26f, 0.48f);
            orb.speedY = Random.Range(0.060f, 0.090f) * fallSpeedMultiplier;
        }
        else
        {
            orb.depthLayer = 0; // Large Foreground Out-of-Focus Bokeh Blooms
            orb.size = Random.Range(145f, 220f);
            orb.baseAlpha = Random.Range(0.30f, 0.52f);
            orb.speedY = Random.Range(0.075f, 0.110f) * fallSpeedMultiplier;
        }

        orb.baseX = Random.Range(0.03f, 0.97f);
        orb.posY = randomY ? Random.Range(0.02f, 0.98f) : (1.08f + Random.Range(0f, 0.08f));

        // Subtle peaceful horizontal wafting
        orb.swayAmp = Random.Range(0.012f, 0.026f) * swayIntensity;
        orb.swayFreq = Random.Range(0.4f, 1.1f);
        orb.swayPhase = Random.Range(0f, Mathf.PI * 2f);

        orb.pulseSpeed = Random.Range(1.1f, 2.0f);
        orb.pulsePhase = Random.Range(0f, Mathf.PI * 2f);
        orb.color = OrbColors[Random.Range(0, OrbColors.Length)];

        // Visual styling: Transparent background with 3D tinted texture
        orb.element.style.width = orb.size;
        orb.element.style.height = orb.size;
        orb.element.style.backgroundColor = Color.clear;
        orb.element.style.unityBackgroundImageTintColor = orb.color;

        float currentX = Mathf.Clamp(orb.baseX, 0.01f, 0.99f);
        orb.element.style.left = Length.Percent(currentX * 100f);
        orb.element.style.top = Length.Percent(orb.posY * 100f);
        return orb;
    }

    private void UpdateSpiritOrbs(float dt)
    {
        if (activeOrbs.Count == 0 || orbsContainer == null) return;

        float time = Time.unscaledTime;

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            SpiritOrb orb = activeOrbs[i];

            // Gentle steady upward movement (bottom to top)
            orb.posY -= orb.speedY * dt;

            // Gentle organic horizontal sway relative to fixed baseX (never accumulates!)
            float swayOffset = Mathf.Sin(time * orb.swayFreq + orb.swayPhase) * orb.swayAmp;
            float currentX = Mathf.Clamp(orb.baseX + swayOffset, 0.01f, 0.99f);

            // Vertical soft fade in at bottom and fade out at top
            float verticalFade = 1.0f;
            if (orb.posY > 0.88f)
            {
                verticalFade = Mathf.InverseLerp(1.08f, 0.88f, orb.posY);
            }
            else if (orb.posY < 0.12f)
            {
                verticalFade = Mathf.InverseLerp(-0.08f, 0.12f, orb.posY);
            }

            // Gentle breathing alpha pulse
            float pulse = 0.72f + 0.28f * Mathf.Sin(time * orb.pulseSpeed + orb.pulsePhase);
            float alpha = orb.baseAlpha * verticalFade * pulse;
            orb.element.style.opacity = Mathf.Clamp01(alpha);

            // Set coordinates directly
            orb.element.style.left = Length.Percent(currentX * 100f);
            orb.element.style.top = Length.Percent(orb.posY * 100f);

            // Wrap orb back to below screen once it rises past top
            if (orb.posY < -0.10f)
            {
                orb.posY = 1.08f + Random.Range(0f, 0.06f);
                orb.baseX = Random.Range(0.03f, 0.97f);
                orb.color = OrbColors[Random.Range(0, OrbColors.Length)];
                orb.element.style.unityBackgroundImageTintColor = orb.color;
            }
        }
    }

    #endregion

    private void LoadFramesIfNeeded()
    {
        if (logoAnimationFrames != null && logoAnimationFrames.Length > 0) return;

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(framesFolderPath) && Directory.Exists(framesFolderPath))
        {
            string[] filePaths = Directory.GetFiles(framesFolderPath, "frame_*.png");
            System.Array.Sort(filePaths);

            List<Texture2D> loadedList = new List<Texture2D>();
            foreach (string path in filePaths)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    loadedList.Add(tex);
                }
            }
            logoAnimationFrames = loadedList.ToArray();
            Debug.Log($"[MainMenuUIToolkitController] Loaded {logoAnimationFrames.Length} logo animation frames from {framesFolderPath}");
        }
#endif
    }

    /// <summary>
    /// Frame-by-frame logo animation loop for UI Toolkit.
    /// </summary>
        private void OnLogoTapped(ClickEvent evt)
    {
        TriggerWingFlap();
    }

    /// <summary>
    /// Flaps the wings and pauses when fully extended. Can be triggered on tap or when the game initially starts.
    /// </summary>
    public void TriggerWingFlap()
    {
        if (frameAnimationCoroutine != null)
        {
            StopCoroutine(frameAnimationCoroutine);
        }
        frameAnimationCoroutine = StartCoroutine(PlayFlapSequenceRoutine());
    }

    /// <summary>
    /// Flaps the wings for one majestic cycle from the extended pose, lingers at the highest point, and pauses back at fully extended.
    /// </summary>
    private IEnumerator PlayFlapSequenceRoutine()
    {
        if (logoAnimationFrames == null || logoAnimationFrames.Length == 0)
        {
            LoadFramesIfNeeded();
        }

        if (logoAnimationFrames == null || logoAnimationFrames.Length == 0 || titleLogo == null)
        {
            yield break;
        }

        float frameDuration = 1f / Mathf.Max(1f, animationFPS);
        int startFrame = fullyExtendedFrameIndex;
        int maxFrames = logoAnimationFrames.Length;

        // Play full wing flap stroke (rising to apex, lingering at highest wings, descending back to extended)
        for (int i = 1; i <= 16; i++)
        {
            int currentFrame = (startFrame + i) % maxFrames;
            if (currentFrame < logoAnimationFrames.Length && logoAnimationFrames[currentFrame] != null)
            {
                titleLogo.style.backgroundImage = new StyleBackground(logoAnimationFrames[currentFrame]);
            }

            // When wings are at their highest (apex frames 16-18, especially frame 17):
            if (currentFrame == 17)
            {
                yield return new WaitForSecondsRealtime(highestWingsHoldDuration);
            }
            else if (currentFrame == 16 || currentFrame == 18)
            {
                yield return new WaitForSecondsRealtime(frameDuration * 1.8f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(frameDuration);
            }
        }

        // Return to and pause on the fully extended wings pose (only that logo animation pauses)
        if (fullyExtendedFrameIndex < logoAnimationFrames.Length && logoAnimationFrames[fullyExtendedFrameIndex] != null)
        {
            titleLogo.style.backgroundImage = new StyleBackground(logoAnimationFrames[fullyExtendedFrameIndex]);
        }
    }

    private IEnumerator PlayLogoFrameAnimation()
    {
        if (logoAnimationFrames == null || logoAnimationFrames.Length == 0)
        {
            LoadFramesIfNeeded();
        }

        if (logoAnimationFrames == null || logoAnimationFrames.Length == 0 || titleLogo == null)
        {
            yield break;
        }

        // Initial launch flap: play majestic stroke from extended pose up to highest wings (lingering), then return to extended
        yield return StartCoroutine(PlayFlapSequenceRoutine());

        // If continuous loop is requested without pause, continue cycling
        if (!pauseWhenWingsFullyExtended && loopAnimation)
        {
            int frameIndex = fullyExtendedFrameIndex;
            float frameDuration = 1f / Mathf.Max(1f, animationFPS);

            while (true)
            {
                if (titleLogo != null && frameIndex < logoAnimationFrames.Length && logoAnimationFrames[frameIndex] != null)
                {
                    titleLogo.style.backgroundImage = new StyleBackground(logoAnimationFrames[frameIndex]);
                }

                if (frameIndex == 17 || frameIndex == 2 || frameIndex == 39 || frameIndex == 69 || frameIndex == 86)
                {
                    yield return new WaitForSecondsRealtime(highestWingsHoldDuration);
                }
                else if (frameIndex == 16 || frameIndex == 18)
                {
                    yield return new WaitForSecondsRealtime(frameDuration * 1.8f);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(frameDuration);
                }

                frameIndex = (frameIndex + 1) % logoAnimationFrames.Length;
            }
        }
    }

    /// <summary>
    /// Gentle pulse for ambient background glow.
    /// </summary>
    private IEnumerator AnimateGlowPulse()
    {
        float time = 0f;
        while (true)
        {
            time += Time.unscaledDeltaTime;
            if (ambientGlow != null)
            {
                float glowAlpha = 0.7f + Mathf.Sin(time * 2.0f) * 0.25f;
                ambientGlow.style.opacity = glowAlpha;
            }
            yield return null;
        }
    }

    private void RegisterCallbacks()
    {
        if (titleLogo != null)
        {
            titleLogo.RegisterCallback<ClickEvent>(OnLogoTapped);
        }
        if (titleContainer != null)
        {
            titleContainer.RegisterCallback<ClickEvent>(OnLogoTapped);
        }
        if (btnPlay != null) btnPlay.clicked += OnPlayClicked;
        if (btnContinue != null) btnContinue.clicked += OnContinueClicked;
        if (btnNewGame != null) btnNewGame.clicked += OnNewGameClicked;
        if (btnLoadGame != null) btnLoadGame.clicked += OnLoadGameMenuClicked;
        if (btnSettings != null) btnSettings.clicked += () => OpenModal(modalSettings);
        if (btnQuit != null) btnQuit.clicked += OnQuitClicked;

        if (btnCloseSettings != null) btnCloseSettings.clicked += () => CloseModal(modalSettings);
        if (btnCloseLoadGame != null) btnCloseLoadGame.clicked += () => CloseModal(modalLoadGame);

        root.Query<Button>().ForEach(button =>
        {
            button.RegisterCallback<MouseEnterEvent>(OnButtonHover);
            button.RegisterCallback<ClickEvent>(OnButtonClickSFX);
        });

        if (sliderMaster != null)
        {
            sliderMaster.RegisterValueChangedCallback(evt =>
            {
                AudioListener.volume = evt.newValue;
                if (AudioManager.Instance != null) AudioManager.Instance.masterVolume = evt.newValue;
                PlayerPrefs.SetFloat("MasterVolume", evt.newValue);
            });
        }

        if (sliderMusic != null)
        {
            sliderMusic.RegisterValueChangedCallback(evt =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.musicVolume = evt.newValue;
                PlayerPrefs.SetFloat("MusicVolume", evt.newValue);
            });
        }

        if (sliderSFX != null)
        {
            sliderSFX.RegisterValueChangedCallback(evt =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.sfxVolume = evt.newValue;
                PlayerPrefs.SetFloat("SFXVolume", evt.newValue);
            });
        }

        if (toggleFullscreen != null)
        {
            toggleFullscreen.RegisterValueChangedCallback(evt =>
            {
                Screen.fullScreen = evt.newValue;
                PlayerPrefs.SetInt("Fullscreen", evt.newValue ? 1 : 0);
            });
        }

        if (toggleScreenShake != null)
        {
            toggleScreenShake.RegisterValueChangedCallback(evt =>
            {
                PlayerPrefs.SetInt("ScreenShake", evt.newValue ? 1 : 0);
            });
        }
    }

    private void UnregisterCallbacks()
    {
        if (titleLogo != null) titleLogo.UnregisterCallback<ClickEvent>(OnLogoTapped);
        if (titleContainer != null) titleContainer.UnregisterCallback<ClickEvent>(OnLogoTapped);
        if (btnPlay != null) btnPlay.clicked -= OnPlayClicked;
        if (btnContinue != null) btnContinue.clicked -= OnContinueClicked;
        if (btnNewGame != null) btnNewGame.clicked -= OnNewGameClicked;
        if (btnLoadGame != null) btnLoadGame.clicked -= OnLoadGameMenuClicked;
        if (btnCloseSettings != null) btnCloseSettings.clicked -= () => CloseModal(modalSettings);
        if (btnCloseLoadGame != null) btnCloseLoadGame.clicked -= () => CloseModal(modalLoadGame);
        if (btnQuit != null) btnQuit.clicked -= OnQuitClicked;
    }

    private void LoadSavedSettings()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        float music = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 0.9f);
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        bool screenShake = PlayerPrefs.GetInt("ScreenShake", 1) == 1;

        if (sliderMaster != null) sliderMaster.value = master;
        if (sliderMusic != null) sliderMusic.value = music;
        if (sliderSFX != null) sliderSFX.value = sfx;
        if (toggleFullscreen != null) toggleFullscreen.value = fullscreen;
        if (toggleScreenShake != null) toggleScreenShake.value = screenShake;

        AudioListener.volume = master;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.masterVolume = master;
            AudioManager.Instance.musicVolume = music;
            AudioManager.Instance.sfxVolume = sfx;
        }
    }

    private void OpenModal(VisualElement modal)
    {
        if (modal == null) return;
        PlaySFX(modalOpenClip);
        modal.RemoveFromClassList("panel--hidden");
        modal.AddToClassList("panel--visible");
    }

    private void CloseModal(VisualElement modal)
    {
        if (modal == null) return;
        PlaySFX(clickClip);
        modal.RemoveFromClassList("panel--visible");
        modal.AddToClassList("panel--hidden");
    }

    private void OnPlayClicked()
    {
        PlaySFX(clickClip);
        isPlaying = true;
        Time.timeScale = 1f; // Unpause game on play
        EnablePlayerGameplay(true);

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateVisibility();
        }
        if (SpawnOfChaos.Minigames.HUDOrbPanel.Instance != null)
        {
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance.UpdateVisibility();
        }

        // Trigger Level Gameplay Background Music
        LevelMusicPlayer lmp = FindFirstObjectByType<LevelMusicPlayer>();
        if (lmp != null)
        {
            lmp.StartLevelMusic();
        }

        string targetScene = string.IsNullOrEmpty(gameSceneName) ? "TutorialScene" : gameSceneName;
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene.Equals(targetScene, System.StringComparison.OrdinalIgnoreCase))
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
            gameObject.SetActive(false);
            return;
        }

        ExecuteGameLaunch(targetScene);
    }


    private void RefreshContinueButtonState()
    {
        if (btnContinue == null) return;
        bool hasSave = SaveSlotManager.HasAnySave();
        btnContinue.SetEnabled(hasSave);
        btnContinue.style.opacity = hasSave ? 1.0f : 0.45f;
    }

    private void OnContinueClicked()
    {
        PlaySFX(clickClip);
        int recentIdx = SaveSlotManager.GetMostRecentSlotIndex();
        SaveSlotData slot = SaveSlotManager.GetSlot(recentIdx);
        if (slot != null && !slot.isEmpty)
        {
            LoadGameSlot(slot);
        }
        else
        {
            OnNewGameClicked();
        }
    }

    private void OnNewGameClicked()
    {
        PlaySFX(clickClip);
        int emptySlot = SaveSlotManager.FindFirstEmptySlotIndex();
        StartNewGameInSlot(emptySlot);
    }

    private void OnLoadGameMenuClicked()
    {
        PlaySFX(modalOpenClip);
        PopulateSlotsGrid();
        OpenModal(modalLoadGame);
    }

    public void PopulateSlotsGrid()
    {
        if (slotsGrid == null) return;
        slotsGrid.Clear();

        List<SaveSlotData> slots = SaveSlotManager.GetAllSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            SaveSlotData data = slots[i];
            int slotNumber = i + 1;

            VisualElement card = new VisualElement();
            card.AddToClassList("slot-card");

            if (data.isEmpty)
            {
                card.AddToClassList("slot-card--empty");

                VisualElement header = new VisualElement();
                header.AddToClassList("slot-card-header");
                Label badge = new Label($"SLOT {slotNumber:D2}");
                badge.AddToClassList("slot-badge");
                Label emptyLabel = new Label("UNCLAIMED VESSEL");
                emptyLabel.AddToClassList("slot-date");
                header.Add(badge);
                header.Add(emptyLabel);
                card.Add(header);

                Label loc = new Label("Empty Memory Vessel");
                loc.AddToClassList("slot-location");
                card.Add(loc);

                VisualElement details = new VisualElement();
                details.AddToClassList("slot-details-row");
                Label detailText = new Label("No drifter soul anchored to this timeline.");
                details.Add(detailText);
                card.Add(details);

                VisualElement actions = new VisualElement();
                actions.AddToClassList("slot-actions-row");
                Button btnNew = new Button(() => {
                    CloseModal(modalLoadGame);
                    StartNewGameInSlot(slotNumber);
                }) { text = "+ NEW RUN" };
                btnNew.AddToClassList("slot-btn");
                btnNew.AddToClassList("slot-btn--load");
                actions.Add(btnNew);
                card.Add(actions);
            }
            else
            {
                VisualElement header = new VisualElement();
                header.AddToClassList("slot-card-header");
                Label badge = new Label($"SLOT {slotNumber:D2}");
                badge.AddToClassList("slot-badge");
                Label date = new Label(string.IsNullOrEmpty(data.saveTimestamp) ? "Recorded Soul" : data.saveTimestamp);
                date.AddToClassList("slot-date");
                header.Add(badge);
                header.Add(date);
                card.Add(header);

                Label loc = new Label(data.locationName);
                loc.AddToClassList("slot-location");
                card.Add(loc);

                VisualElement details = new VisualElement();
                details.AddToClassList("slot-details-row");
                Label stats = new Label($"LVL {data.playerLevel}  |  {data.currentWeapon}  |  {data.coins} ❖  |  {data.GetFormattedPlaytime()}");
                details.Add(stats);
                card.Add(details);

                VisualElement actions = new VisualElement();
                actions.AddToClassList("slot-actions-row");

                Button btnDel = new Button(() => {
                    DeleteGameSlot(slotNumber);
                }) { text = "DELETE" };
                btnDel.AddToClassList("slot-btn");
                btnDel.AddToClassList("slot-btn--del");
                actions.Add(btnDel);

                Button btnLoad = new Button(() => {
                    CloseModal(modalLoadGame);
                    LoadGameSlot(data);
                }) { text = "RESUME" };
                btnLoad.AddToClassList("slot-btn");
                btnLoad.AddToClassList("slot-btn--load");
                actions.Add(btnLoad);

                card.Add(actions);
            }

            slotsGrid.Add(card);
        }
    }

    private void StartNewGameInSlot(int slotIndex)
    {
        SaveSlotManager.ActiveSlotIndex = slotIndex;
        SaveSlotData startingSlot = new SaveSlotData
        {
            slotIndex = slotIndex,
            isEmpty = false,
            locationName = "Primordial Void & Tutorial",
            sceneName = "TutorialScene",
            playerLevel = 1,
            coins = 0,
            currentWeapon = "DarkSpear",
            playTimeSeconds = 0f
        };
        SaveSlotManager.SaveSlot(slotIndex, startingSlot);

        ExecuteGameLaunch(startingSlot.sceneName);
    }

    private void LoadGameSlot(SaveSlotData data)
    {
        SaveSlotManager.ActiveSlotIndex = data.slotIndex;
        SaveSlotManager.LastPlayedSlotIndex = data.slotIndex;
        ExecuteGameLaunch(data.sceneName);
    }

    private void DeleteGameSlot(int slotIndex)
    {
        PlaySFX(clickClip);
        SaveSlotManager.DeleteSlot(slotIndex);
        PopulateSlotsGrid();
        RefreshContinueButtonState();
    }

    private void ExecuteGameLaunch(string sceneToLoad)
    {
        isPlaying = true;
        Time.timeScale = 1f;
        EnablePlayerGameplay(true);

        if (sceneToLoad == "TutorialScene")
        {
            GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (existingPlayer != null && !existingPlayer.name.Contains("BasePlayer"))
            {
                Debug.Log("[MainMenu] Clearing existing non-BasePlayer before launching TutorialScene.");
                Destroy(existingPlayer);
            }
            PlayerSpawnPointManager.targetSpawnPointName = "DefaultSpawnPoint";
        }

        if (HUDManager.Instance != null) HUDManager.Instance.UpdateVisibility();
        if (SpawnOfChaos.Minigames.HUDOrbPanel.Instance != null) SpawnOfChaos.Minigames.HUDOrbPanel.Instance.UpdateVisibility();

        LevelMusicPlayer lmp = FindFirstObjectByType<LevelMusicPlayer>();
        if (lmp != null) lmp.StartLevelMusic();

        if (root != null) root.style.display = DisplayStyle.None;
        gameObject.SetActive(false);

        ArcaneLoadingScreen.LoadScene(sceneToLoad);
    }

    private void PlayTitleMusic()
    {
        EnsureTitleMusicAssigned();
        if (titleMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(titleMusic, true);
        }
    }

    private void EnsureTitleMusicAssigned()
    {
        if (titleMusic == null)
        {
            titleMusic = Resources.Load<AudioClip>("Audio/the-spawn-of-chaos");
#if UNITY_EDITOR
            if (titleMusic == null)
            {
                titleMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/the-spawn-of-chaos.mp3");
            }
#endif
        }
    }

    private void OnQuitClicked()
    {
        PlaySFX(clickClip);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnButtonHover(MouseEnterEvent evt)
    {
        PlaySFX(hoverClip);
    }

    private void OnButtonClickSFX(ClickEvent evt)
    {
        PlaySFX(clickClip);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }
}
