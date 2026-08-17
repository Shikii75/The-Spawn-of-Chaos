using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// High-aesthetic Controller for the UI Toolkit Main Menu.
/// Plays full-speed frame animation sequence from the 'keep-54947fd1' folder for the centerpiece logo.
/// Features glowing purple spirit orbs / fireflies (faceless Lumi-style) floating gently from top to bottom.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuUIToolkitController : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Target scene to load when Enter Realm is clicked.")]
    public string gameSceneName = "SampleScene";

    /// <summary>
    /// Global state to track if gameplay is active or title menu is showing.
    /// </summary>
    public static bool isPlaying = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitStaticState()
    {
        isPlaying = false;
    }

    [Header("Logo Frame Animation & Sizing")]
    [Tooltip("Dimensions of the logo in pixels (Width, Height).")]
    public Vector2 logoDimensions = new Vector2(820f, 480f);
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

    [Header("Purple Spirit Orbs / Fireflies (Top to Bottom)")]
    [Tooltip("Total number of floating spirit orbs.")]
    [Range(10, 80)]
    public int orbCount = 35;
    [Tooltip("Downward fall speed multiplier.")]
    [Range(0.2f, 3f)]
    public float fallSpeedMultiplier = 1.0f;
    [Tooltip("Horizontal gentle sway intensity.")]
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
    private VisualElement ambientGlow;
    private VisualElement orbsContainer;
    private Button btnPlay;
    private Button btnSettings;
    private Button btnQuit;

    // Modals & Settings
    private VisualElement modalSettings;
    private Button btnCloseSettings;
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
        public float posX;          // Normalized 0..1 across screen width
        public float posY;          // Normalized 0..1 across screen height
        public float speedY;        // Downward fall speed
        public float baseSpeedX;    // Base horizontal wind drift
        public float swayAmp;       // Horizontal sway amplitude
        public float swayFreq;      // Horizontal sway frequency
        public float swayPhase;     // Phase offset
        public float size;          // Diameter in px
        public float baseAlpha;     // Peak opacity
        public float pulseSpeed;    // Gentle breathing frequency
        public float pulsePhase;    // Breathing offset
        public Color color;         // Glowing purple/lavender color
    }

    private readonly List<SpiritOrb> activeOrbs = new List<SpiritOrb>();

    // Ethereal purple / lavender palette for Lumi-style spirit orbs
    private static readonly Color[] OrbColors = new Color[]
    {
        new Color(0.88f, 0.76f, 1.00f, 0.95f), // Glowing Lavender core (#e0c2ff)
        new Color(0.75f, 0.52f, 0.99f, 0.90f), // Luminous Neon Purple (#c084fc)
        new Color(0.66f, 0.33f, 0.97f, 0.85f), // Royal Violet (#a855f7)
        new Color(0.85f, 0.40f, 0.98f, 0.90f), // Amethyst Star (#d966fa)
        new Color(0.93f, 0.58f, 0.98f, 0.85f)  // Soft Orchid Aura (#eda4fa)
    };

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        LoadFramesIfNeeded();
        EnsureTitleMusicAssigned();
    }

    private void Start()
    {
        PlayTitleMusic();
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

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null) return;

        root = uiDocument.rootVisualElement;
        if (root == null) return;

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
        // ESC key closes modal
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (modalSettings != null && modalSettings.ClassListContains("panel--visible"))
            {
                CloseModal(modalSettings);
            }
        }

        // Update atmospheric spirit orbs drifting top to bottom
        UpdateSpiritOrbs(Time.unscaledDeltaTime);
    }

    private void BindVisualElements()
    {
        titleLogo = root.Q<VisualElement>("title-logo");
        ambientGlow = root.Q<VisualElement>("ambient-glow");
        orbsContainer = root.Q<VisualElement>("orbs-container");

        ApplyLogoSize();

        btnPlay = root.Q<Button>("btn-play");
        btnSettings = root.Q<Button>("btn-settings");
        btnQuit = root.Q<Button>("btn-quit");

        modalSettings = root.Q<VisualElement>("modal-settings");
        btnCloseSettings = root.Q<Button>("btn-close-settings");

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
            float w = logoDimensions.x * logoScale;
            float h = logoDimensions.y * logoScale;
            titleLogo.style.width = w;
            titleLogo.style.height = h;
        }
    }

    private void OnValidate()
    {
        if (root != null && titleLogo != null)
        {
            ApplyLogoSize();
        }
    }

    #region Spirit Orbs / Fireflies Simulation (Top to Bottom)

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
        SpiritOrb orb = new SpiritOrb();
        orb.element = new VisualElement();
        orb.element.AddToClassList("purple-spirit-orb");

        orb.posX = Random.value;
        orb.posY = randomY ? Random.value : (-0.05f - Random.Range(0f, 0.05f));

        // Downward slow floating speed (calm, serene snowfall / firefly pace)
        orb.speedY = Random.Range(0.008f, 0.022f) * fallSpeedMultiplier;
        orb.baseSpeedX = Random.Range(-0.005f, 0.008f);

        // Sinusoidal horizontal wafting (gentle lazy sway)
        orb.swayAmp = Random.Range(0.012f, 0.032f) * swayIntensity;
        orb.swayFreq = Random.Range(0.35f, 0.95f);
        orb.swayPhase = Random.Range(0f, Mathf.PI * 2f);

        // Orb sizes (small 4px to prominent 14px)
        orb.size = Random.Range(4.5f, 13.5f);
        orb.baseAlpha = Random.Range(0.55f, 0.95f);
        orb.pulseSpeed = Random.Range(0.6f, 1.8f);
        orb.pulsePhase = Random.Range(0f, Mathf.PI * 2f);
        orb.color = OrbColors[Random.Range(0, OrbColors.Length)];

        // Set visual styling
        orb.element.style.width = orb.size;
        orb.element.style.height = orb.size;
        orb.element.style.backgroundColor = orb.color;
        orb.element.style.borderTopLeftRadius = orb.size * 0.5f;
        orb.element.style.borderTopRightRadius = orb.size * 0.5f;
        orb.element.style.borderBottomLeftRadius = orb.size * 0.5f;
        orb.element.style.borderBottomRightRadius = orb.size * 0.5f;

        PositionOrb(orb);
        return orb;
    }

    private void UpdateSpiritOrbs(float dt)
    {
        if (activeOrbs.Count == 0 || orbsContainer == null) return;

        float time = Time.unscaledTime;

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            SpiritOrb orb = activeOrbs[i];

            // Downward movement (top to bottom)
            orb.posY += orb.speedY * dt;

            // Gentle organic horizontal floating sway
            float swayOffset = Mathf.Sin(time * orb.swayFreq + orb.swayPhase) * orb.swayAmp;
            float currentX = orb.posX + (orb.baseSpeedX * dt) + swayOffset;

            // Smooth horizontal wrapping
            if (currentX < -0.05f) currentX = 1.05f;
            else if (currentX > 1.05f) currentX = -0.05f;
            orb.posX = currentX;

            // Natural fade: Fades in near top (y < 0.15), stays glowing in middle, fades out near bottom (y > 0.85)
            float verticalFade = 1.0f;
            if (orb.posY < 0.15f)
            {
                verticalFade = Mathf.InverseLerp(-0.05f, 0.15f, orb.posY);
            }
            else if (orb.posY > 0.85f)
            {
                verticalFade = Mathf.InverseLerp(1.05f, 0.85f, orb.posY);
            }

            // Soft breathing firefly pulsation
            float pulse = 0.8f + Mathf.Sin(time * orb.pulseSpeed + orb.pulsePhase) * 0.2f;
            float alpha = orb.baseAlpha * verticalFade * pulse;
            orb.element.style.opacity = Mathf.Clamp01(alpha);

            // Subtle scale breathing
            float scale = 1.0f + Mathf.Sin(time * orb.pulseSpeed * 0.8f + orb.pulsePhase) * 0.12f;
            orb.element.style.scale = new Scale(new Vector2(scale, scale));

            PositionOrb(orb);

            // Wrap orb to top when it drifts past bottom of screen
            if (orb.posY > 1.05f)
            {
                orb.posY = -0.05f - Random.Range(0f, 0.05f);
                orb.posX = Random.value;
                orb.speedY = Random.Range(0.008f, 0.022f) * fallSpeedMultiplier;
                orb.color = OrbColors[Random.Range(0, OrbColors.Length)];
                orb.element.style.backgroundColor = orb.color;
            }
        }
    }

    private void PositionOrb(SpiritOrb orb)
    {
        if (orb.element == null) return;
        orb.element.style.left = Length.Percent(orb.posX * 100f);
        orb.element.style.top = Length.Percent(orb.posY * 100f);
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

        int frameIndex = 0;
        float frameDuration = 1f / Mathf.Max(1f, animationFPS);

        while (true)
        {
            if (titleLogo != null && frameIndex < logoAnimationFrames.Length && logoAnimationFrames[frameIndex] != null)
            {
                titleLogo.style.backgroundImage = new StyleBackground(logoAnimationFrames[frameIndex]);
            }

            frameIndex++;
            if (frameIndex >= logoAnimationFrames.Length)
            {
                if (loopAnimation)
                {
                    frameIndex = 0;
                }
                else
                {
                    break;
                }
            }

            yield return new WaitForSecondsRealtime(frameDuration);
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
        if (btnPlay != null) btnPlay.clicked += OnPlayClicked;
        if (btnSettings != null) btnSettings.clicked += () => OpenModal(modalSettings);
        if (btnQuit != null) btnQuit.clicked += OnQuitClicked;

        if (btnCloseSettings != null) btnCloseSettings.clicked += () => CloseModal(modalSettings);

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
        if (btnPlay != null) btnPlay.clicked -= OnPlayClicked;
        if (btnCloseSettings != null) btnCloseSettings.clicked -= () => CloseModal(modalSettings);
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

        // Trigger Level Gameplay Background Music
        LevelMusicPlayer lmp = FindFirstObjectByType<LevelMusicPlayer>();
        if (lmp != null)
        {
            lmp.StartLevelMusic();
        }

        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(gameSceneName) && activeScene.Equals(gameSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
            gameObject.SetActive(false);
            return;
        }

        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
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
