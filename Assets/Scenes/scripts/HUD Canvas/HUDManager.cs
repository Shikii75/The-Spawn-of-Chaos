using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Player Reference")]
    public GameObject playerGameObject;

    [Header("UI Scale Settings")]
    [Tooltip("Scale multiplier for HUD elements. Default is 2f.")]
    public float hudScale = 10f;

    // ── Runtime-built UI references ──
    private Canvas canvas;

    private Slider manaSlider;
    private Slider catchUpManaSlider;
    private TextMeshProUGUI manaText;

    // ── Mana Liquid Wave animation references ──
    private Sprite manaWaveSprite;
    private RawImage manaWaveOverlay1;
    private RawImage manaWaveOverlay2;
    private float manaWaveScroll1 = 0f;
    private float manaWaveScroll2 = 0f;

    private TextMeshProUGUI coinsText;
    private TextMeshProUGUI potionsText;

    // ── Cached player components ──
    private Health playerHealth;
    private MageCombat playerCombat;
    private PlayerCurrency playerCurrency;

    // ── Unique HUD elements ──
    private List<GameObject> manaNotches = new List<GameObject>();
    private float lastMaxMana = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapHUD()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (Instance == null)
        {
            HUDManager existing = FindFirstObjectByType<HUDManager>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                GameObject hudGo = new GameObject("HUDManager");
                Instance = hudGo.AddComponent<HUDManager>();
            }
        }

        if (Instance != null)
        {
            Instance.ValidateHUD();
        }
    }

    void Awake()
    {
        // Force the scale multiplier to a compact size of 2.0f to prevent covering the screen
        hudScale = 2.0f;

        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (Instance != this)
        {
            // Do NOT destroy gameObject if HUDManager is attached to Player!
            Destroy(this);
            return;
        }

        BuildUI();

        // Ensure PlayerLevelSystem exists
        if (SpawnOfChaos.Systems.PlayerLevelSystem.Instance == null)
        {
            GameObject levelSysGO = new GameObject("PlayerLevelSystem");
            levelSysGO.AddComponent<SpawnOfChaos.Systems.PlayerLevelSystem>();
        }

        UpdateVisibility();
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION — entire hierarchy built from code
    // ══════════════════════════════════════════════════════════════════

    public void ValidateHUD()
    {
        if (canvas == null || canvas.gameObject == null)
        {
            BuildUI();
        }
        else
        {
            if (canvas.transform.parent != null)
            {
                canvas.transform.SetParent(null, false);
            }
            DontDestroyOnLoad(canvas.gameObject);
            canvas.sortingOrder = 50;

            EnsureOrbPanelExists();
        }
    }

    private void EnsureOrbPanelExists()
    {
        if (canvas == null) return;

        SpawnOfChaos.Minigames.HUDOrbPanel orbPanel = canvas.GetComponentInChildren<SpawnOfChaos.Minigames.HUDOrbPanel>(true);
        if (orbPanel == null)
        {
            Transform existingChild = canvas.transform.Find("HUDOrbPanel");
            if (existingChild != null)
            {
                orbPanel = existingChild.GetComponent<SpawnOfChaos.Minigames.HUDOrbPanel>();
                if (orbPanel == null) orbPanel = existingChild.gameObject.AddComponent<SpawnOfChaos.Minigames.HUDOrbPanel>();
            }
        }

        if (orbPanel == null)
        {
            Transform existingMgr = canvas.transform.Find("HUDOrbPanelManager");
            if (existingMgr != null)
            {
                orbPanel = existingMgr.GetComponent<SpawnOfChaos.Minigames.HUDOrbPanel>();
                if (orbPanel == null) orbPanel = existingMgr.gameObject.AddComponent<SpawnOfChaos.Minigames.HUDOrbPanel>();
            }
        }

        if (orbPanel == null)
        {
            GameObject orbGO = new GameObject("HUDOrbPanel");
            orbGO.transform.SetParent(canvas.transform, false);
            orbPanel = orbGO.AddComponent<SpawnOfChaos.Minigames.HUDOrbPanel>();
        }

        orbPanel.EnsureWidgetBuilt();
    }

    private void BuildUI()
    {
        // ── Canvas (sort order 50, persistent across scenes) ──
        if (canvas == null)
        {
            GameObject existingCanvas = GameObject.Find("HUDCanvas");
            if (existingCanvas != null)
            {
                canvas = existingCanvas.GetComponent<Canvas>();
            }
            else
            {
                canvas = UIFactory.CreateCanvas("HUDCanvas", 50);
            }
        }

        if (canvas != null)
        {
            if (canvas.transform.parent != null)
            {
                canvas.transform.SetParent(null, false);
            }
            DontDestroyOnLoad(canvas.gameObject);
            canvas.sortingOrder = 50;
        }

        manaSlider = null;
        catchUpManaSlider = null;
        manaText = null;

        // ──────────────────── TOP-RIGHT: Coins & Potions ────────────────
        if (canvas != null)
        {
            // Coins text — top-right
            Transform existingCoins = canvas.transform.Find("CoinsText");
            if (existingCoins != null)
            {
                coinsText = existingCoins.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                coinsText = UIFactory.CreateText(
                    canvas.transform, "CoinsText", "Coins: 0",
                    24f, UIFactory.TextGold, TextAlignmentOptions.TopRight
                );
                coinsText.rectTransform.pivot = new Vector2(1f, 1f);
                coinsText.rectTransform.anchorMin = new Vector2(1f, 1f);
                coinsText.rectTransform.anchorMax = new Vector2(1f, 1f);
                coinsText.rectTransform.anchoredPosition = new Vector2(-50f, -40f);
                coinsText.rectTransform.sizeDelta = new Vector2(220f, 36f);
                coinsText.enableWordWrapping = false;
                coinsText.fontStyle = FontStyles.Bold;
            }

            // Potions text — below coins
            Transform existingPotions = canvas.transform.Find("PotionsText");
            if (existingPotions != null)
            {
                potionsText = existingPotions.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                potionsText = UIFactory.CreateText(
                    canvas.transform, "PotionsText", "Potions: 0 [H]",
                    20f, UIFactory.TextMuted, TextAlignmentOptions.TopRight
                );
                potionsText.rectTransform.pivot = new Vector2(1f, 1f);
                potionsText.rectTransform.anchorMin = new Vector2(1f, 1f);
                potionsText.rectTransform.anchorMax = new Vector2(1f, 1f);
                potionsText.rectTransform.anchoredPosition = new Vector2(-50f, -80f);
                potionsText.rectTransform.sizeDelta = new Vector2(220f, 32f);
                potionsText.enableWordWrapping = false;
                potionsText.fontStyle = FontStyles.Bold;
            }

            EnsureOrbPanelExists();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    void Start()
    {
        manaWaveSprite = Resources.Load<Sprite>("mana_wave");
        if (manaWaveSprite != null && manaSlider != null && manaSlider.fillRect != null)
        {
            // First wave layer (back/mid layer)
            GameObject waveGo1 = new GameObject("ManaWaveOverlay1");
            waveGo1.transform.SetParent(manaSlider.fillRect, false);
            manaWaveOverlay1 = waveGo1.AddComponent<RawImage>();
            manaWaveOverlay1.texture = manaWaveSprite.texture;
            manaWaveOverlay1.color = new Color(1f, 1f, 1f, 0.75f);
            RectTransform waveRT1 = waveGo1.GetComponent<RectTransform>();
            UIFactory.SetRect(waveRT1, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Second wave layer (front layer, moving opposite direction, slightly larger/tiled)
            GameObject waveGo2 = new GameObject("ManaWaveOverlay2");
            waveGo2.transform.SetParent(manaSlider.fillRect, false);
            manaWaveOverlay2 = waveGo2.AddComponent<RawImage>();
            manaWaveOverlay2.texture = manaWaveSprite.texture;
            manaWaveOverlay2.color = new Color(1f, 1f, 1f, 0.95f);
            RectTransform waveRT2 = waveGo2.GetComponent<RectTransform>();
            UIFactory.SetRect(waveRT2, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        RebindPlayerReferences();
        UpdateVisibility();
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribePlayerEvents();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ValidateHUD();
        RebindPlayerReferences();
        UpdateVisibility();
    }

    public Canvas Canvas => canvas;

    public static bool IsInMainMenu()
    {
        // 1. If an active player character is in the scene, we are definitively in gameplay!
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("Player");
        if (player == null) player = GameObject.Find("BasePlayer");
        if (player != null && player.activeInHierarchy)
        {
            return false;
        }

        // 2. Check if the UI Toolkit main menu controller is active and the game has not been started yet
        MainMenuUIToolkitController menu = FindFirstObjectByType<MainMenuUIToolkitController>();
        if (menu != null && menu.gameObject.activeInHierarchy && !MainMenuUIToolkitController.isPlaying)
        {
            return true;
        }

        // 3. Dedicated MainMenu scene
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static bool IsGameplayActive()
    {
        // In the Unity Editor when not playing, always show HUD so it's visible in Scene View!
        if (!Application.isPlaying) return true;

        // Hide during Main Menu / Title Screen
        if (IsInMainMenu()) return false;

        // Hide during Pause Menu
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return false;

        // Hide during Shop UI
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return false;

        // Hide during Nyxaris AI Chat
        if (NyxarisManager.IsChatActive) return false;

        // Hide during NPC Speech Bubble Dialogue
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return false;

        return true;
    }

    public void UpdateVisibility()
    {
        bool shouldShowHUD = IsGameplayActive();

        if (canvas != null)
        {
            if (canvas.enabled != shouldShowHUD) canvas.enabled = shouldShowHUD;
            if (canvas.gameObject.activeSelf != shouldShowHUD) canvas.gameObject.SetActive(shouldShowHUD);
        }

        if (SpawnOfChaos.Minigames.HUDOrbPanel.Instance != null)
        {
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance.UpdateVisibility();
        }
        else if (canvas != null)
        {
            var panel = canvas.GetComponentInChildren<SpawnOfChaos.Minigames.HUDOrbPanel>(true);
            if (panel != null)
            {
                panel.gameObject.SetActive(shouldShowHUD);
            }
            else if (shouldShowHUD)
            {
                EnsureOrbPanelExists();
            }
        }
    }

    public void RebindPlayerReferences()
    {
        UnsubscribePlayerEvents();

        playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject == null) playerGameObject = GameObject.Find("Player");
        if (playerGameObject == null) playerGameObject = GameObject.Find("BasePlayer");
        if (playerGameObject != null)
        {
            playerHealth = playerGameObject.GetComponent<Health>();
            playerCombat = playerGameObject.GetComponent<MageCombat>();
            playerCurrency = playerGameObject.GetComponent<PlayerCurrency>();

            if (playerCurrency != null)
            {
                playerCurrency.onCoinsChanged += UpdateCoinsUI;
                playerCurrency.onPotionsChanged += UpdatePotionsUI;
            }

            InitializeUI();
        }

        if (SpawnOfChaos.Minigames.HUDOrbPanel.Instance != null)
        {
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance.FindPlayer();
        }
    }

    private void UnsubscribePlayerEvents()
    {
        if (playerCurrency != null)
        {
            playerCurrency.onCoinsChanged -= UpdateCoinsUI;
            playerCurrency.onPotionsChanged -= UpdatePotionsUI;
        }
    }

    void OnDestroy()
    {
        UnsubscribePlayerEvents();
    }

    void Update()
    {
        UpdateVisibility();

        if (playerGameObject == null)
        {
            RebindPlayerReferences();
        }

        // Scroll and oscillate the mana liquid wave overlays
        if (manaWaveOverlay1 != null)
        {
            manaWaveScroll1 += Time.deltaTime * 0.15f;
            manaWaveOverlay1.uvRect = new Rect(manaWaveScroll1, 0f, 1f, 1f);
            
            // Vertical sloshing effect (sine oscillation of Y scale)
            float slosh = Mathf.Sin(Time.time * 2.5f) * 0.05f + 0.95f;
            manaWaveOverlay1.rectTransform.localScale = new Vector3(1f, slosh, 1f);
        }
        if (manaWaveOverlay2 != null)
        {
            manaWaveScroll2 -= Time.deltaTime * 0.25f;
            manaWaveOverlay2.uvRect = new Rect(manaWaveScroll2, 0.1f, 1.2f, 1f);
            
            float slosh2 = Mathf.Cos(Time.time * 3.0f) * 0.07f + 0.93f;
            manaWaveOverlay2.rectTransform.localScale = new Vector3(1f, slosh2, 1f);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI UPDATE METHODS
    // ══════════════════════════════════════════════════════════════════

    private void InitializeUI()
    {
        if (playerCurrency != null)
        {
            UpdateCoinsUI(playerCurrency.Coins);
            UpdatePotionsUI(playerCurrency.HealingPotions);
        }
    }

    private void UpdateCoinsUI(int coins)
    {
        if (coinsText != null)
        {
            coinsText.text = "Coins: " + coins;
        }
    }

    private void UpdatePotionsUI(int potions)
    {
        if (potionsText != null)
        {
            potionsText.text = "Potions: " + potions + " [H]";
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  NOTCH DIVISION GENERATION
    // ══════════════════════════════════════════════════════════════════

    private void RebuildNotches(Slider slider, List<GameObject> notchesList, float maxVal, float interval)
    {
        // Clear old notches
        foreach (var notch in notchesList)
        {
            if (notch != null) Destroy(notch);
        }
        notchesList.Clear();

        // Notch generation is disabled entirely for a clean, borderless wave appearance
    }
}
