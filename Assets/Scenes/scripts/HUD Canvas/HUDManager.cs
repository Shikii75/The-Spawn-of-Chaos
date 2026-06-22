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
    [Tooltip("Scale multiplier for HUD elements. Default is 10f (10 times larger). Adjust in the Inspector to scale up or down.")]
    public float hudScale = 10f;

    [Header("Health Units Settings")]
    [Tooltip("Base size of each health unit in the HUD.")]
    public float healthUnitSize = 50f;

    // ── Runtime-built UI references ──
    private Canvas canvas;
    private RectTransform healthUnitsContainer;
    private List<Image> unitForegroundFills = new List<Image>();
    private List<Image> unitCatchUpFills = new List<Image>();
    private Sprite healthUnitSprite;
    private float healthPerUnit = 2f;
    private TextMeshProUGUI healthText;
    
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

    void Awake()
    {
        // Force the scale multiplier to a compact size of 2.0f to prevent covering the screen
        hudScale = 2.0f;

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        BuildUI();

        // Hide HUD if the main menu is active and not playing yet
        MainMenuController menu = FindObjectOfType<MainMenuController>();
        if (menu != null && !MainMenuController.isPlaying)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION — entire hierarchy built from code
    // ══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas (sort order 0) ──
        canvas = UIFactory.CreateCanvas("HUDCanvas", 0);
        canvas.transform.SetParent(null, false);

        // Calculate scaled dimensions and positions to keep alignment clean at any scale
        float leftMargin = 30f * Mathf.Min(hudScale, 2f);
        float topMargin = 20f * Mathf.Min(hudScale, 2f);
        float unitSize = healthUnitSize * hudScale; // Configurable unit size
        float healthY = -topMargin - 30f * Mathf.Min(hudScale, 2f);

        // ──────────────────── TOP-LEFT: Health ────────────────────

        // 1. Container for health units (left-aligned)
        GameObject containerGo = new GameObject("HealthUnitsContainer");
        containerGo.transform.SetParent(canvas.transform, false);
        healthUnitsContainer = containerGo.AddComponent<RectTransform>();
        healthUnitsContainer.anchorMin = new Vector2(0f, 1f);
        healthUnitsContainer.anchorMax = new Vector2(0f, 1f);
        healthUnitsContainer.pivot = new Vector2(0f, 1f);
        healthUnitsContainer.anchoredPosition = new Vector2(leftMargin, healthY);
        healthUnitsContainer.sizeDelta = new Vector2(1000f * hudScale, unitSize);

        // 2. Health Text Overlay - Removed to save vertical space and keep it tidy
        healthText = null;

        // ──────────────────── TOP-LEFT: Mana ────────────────────

        float manaWidth = 250f * hudScale;
        float manaHeight = 16f * hudScale;
        float gap = 6f * Mathf.Min(hudScale, 2f); // Reduced gap from 12f
        float manaX = leftMargin + (manaWidth / 2f);
        float manaY = healthY - unitSize - gap - (manaHeight / 2f);

        // 4. Mana Bar Outer Border Panel (Made transparent to remove outline box)
        RectTransform manaBorderPanel = UIFactory.CreatePanel(
            canvas.transform, "ManaBorder", 
            Color.clear,
            new Vector2(0f, 1f), new Vector2(0f, 1f)
        );
        UIFactory.SetRectFixed(manaBorderPanel,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(manaX, manaY), new Vector2(manaWidth, manaHeight)
        );

        // 5. Mana Bar Inner BG (Made transparent to remove background box)
        RectTransform manaInnerBg = UIFactory.CreatePanel(
            manaBorderPanel, "ManaInnerBg", 
            Color.clear,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero
        );

        // 6. Mana Catch-Up Slider (Under-fill mana indicator)
        catchUpManaSlider = UIFactory.CreateSlider(manaInnerBg, "CatchUpManaSlider", new Color(0f, 180f/255f, 220f/255f, 0.7f));
        Transform catchUpBgTransform = catchUpManaSlider.transform.Find("Background");
        if (catchUpBgTransform != null)
        {
            Image catchUpBgImg = catchUpBgTransform.GetComponent<Image>();
            if (catchUpBgImg != null) catchUpBgImg.color = Color.clear; // Make background transparent
        }
        if (catchUpManaSlider.fillRect != null)
        {
            Image catchUpFillImg = catchUpManaSlider.fillRect.GetComponent<Image>();
            if (catchUpFillImg != null) catchUpFillImg.color = Color.clear; // Make solid fill transparent
        }
        UIFactory.SetRect(catchUpManaSlider.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );

        // 7. Main Mana Slider
        manaSlider = UIFactory.CreateSlider(manaInnerBg, "ManaSlider", UIFactory.SliderManaFill);
        Transform manaBgTransform = manaSlider.transform.Find("Background");
        if (manaBgTransform != null)
        {
            Image manaBgImg = manaBgTransform.GetComponent<Image>();
            if (manaBgImg != null) manaBgImg.color = Color.clear; // Make background transparent
        }
        if (manaSlider.fillRect != null)
        {
            Image manaFillImg = manaSlider.fillRect.GetComponent<Image>();
            if (manaFillImg != null) manaFillImg.color = Color.clear; // Make solid fill transparent
        }
        UIFactory.SetRect(manaSlider.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );

        // 8. Mana Text Overlay - Disabled to hide numbers entirely
        manaText = null;

        // ──────────────────── TOP-RIGHT: Coins & Potions ────────────────

        float coinsWidth = 160f * hudScale;
        float coinsHeight = 30f * hudScale;
        float rightMargin = 10f * Mathf.Min(hudScale, 2f);
        float topMarginRight = 5f * Mathf.Min(hudScale, 2f);
        
        float coinsX = -(rightMargin + coinsWidth / 2f);
        float coinsY = -(topMarginRight + coinsHeight / 2f);

        // Coins text — top-right, scaled size and font
        coinsText = UIFactory.CreateText(
            canvas.transform, "CoinsText", "Coins: 0",
            20f * hudScale, UIFactory.TextWhite, TextAlignmentOptions.TopRight
        );
        UIFactory.SetRectFixed(coinsText.rectTransform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(coinsX, coinsY), new Vector2(coinsWidth, coinsHeight)
        );
        coinsText.enableWordWrapping = false;

        float potionsWidth = 160f * hudScale;
        float potionsHeight = 26f * hudScale;
        float gapRight = 2f * Mathf.Min(hudScale, 2f);
        
        float potionsX = coinsX;
        float potionsY = coinsY - (coinsHeight / 2f) - gapRight - (potionsHeight / 2f);

        // Potions text — below coins, scaled size and font, muted color
        potionsText = UIFactory.CreateText(
            canvas.transform, "PotionsText", "Potions: 0 [H]",
            18f * hudScale, UIFactory.TextMuted, TextAlignmentOptions.TopRight
        );
        UIFactory.SetRectFixed(potionsText.rectTransform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(potionsX, potionsY), new Vector2(potionsWidth, potionsHeight)
        );
        potionsText.enableWordWrapping = false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    void Start()
    {
        healthUnitSprite = Resources.Load<Sprite>("health_unit");
        if (healthUnitSprite == null)
        {
            Debug.LogWarning("health_unit sprite could not be loaded from Resources!");
        }

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

        if (playerGameObject == null)
        {
            playerGameObject = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerGameObject != null)
        {
            playerHealth = playerGameObject.GetComponent<Health>();
            playerCombat = playerGameObject.GetComponent<MageCombat>();
            playerCurrency = playerGameObject.GetComponent<PlayerCurrency>();

            // Subscribe to Health Events
            if (playerHealth != null)
            {
                playerHealth.onDamageTaken += UpdateHealthUI;
                playerHealth.onMaxHealthChanged += UpdateMaxHealthUI;
            }

            // Subscribe to Currency Events
            if (playerCurrency != null)
            {
                playerCurrency.onCoinsChanged += UpdateCoinsUI;
                playerCurrency.onPotionsChanged += UpdatePotionsUI;
            }

            InitializeUI();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (playerHealth != null)
        {
            playerHealth.onDamageTaken -= UpdateHealthUI;
            playerHealth.onMaxHealthChanged -= UpdateMaxHealthUI;
        }

        if (playerCurrency != null)
        {
            playerCurrency.onCoinsChanged -= UpdateCoinsUI;
            playerCurrency.onPotionsChanged -= UpdatePotionsUI;
        }
    }

    void Update()
    {
        // Smoothly drain the catch-up health fills
        for (int i = 0; i < unitCatchUpFills.Count; i++)
        {
            if (i < unitForegroundFills.Count)
            {
                Image catchUp = unitCatchUpFills[i];
                Image fg = unitForegroundFills[i];
                
                if (catchUp.fillAmount > fg.fillAmount)
                {
                    catchUp.fillAmount = Mathf.Lerp(catchUp.fillAmount, fg.fillAmount, Time.deltaTime * 3.5f);
                    if (catchUp.fillAmount - fg.fillAmount < 0.005f)
                    {
                        catchUp.fillAmount = fg.fillAmount;
                    }
                }
                else
                {
                    catchUp.fillAmount = fg.fillAmount;
                }
            }
        }

        if (catchUpManaSlider != null && manaSlider != null)
        {
            if (catchUpManaSlider.value > manaSlider.value)
            {
                catchUpManaSlider.value = Mathf.Lerp(catchUpManaSlider.value, manaSlider.value, Time.deltaTime * 3.5f);
                if (catchUpManaSlider.value - manaSlider.value < 0.5f)
                {
                    catchUpManaSlider.value = manaSlider.value;
                }
            }
            else
            {
                catchUpManaSlider.value = manaSlider.value;
            }
        }

        // Regenerating mana slider update (pulled dynamically)
        if (playerCombat != null && manaSlider != null)
        {
            if (Mathf.Abs(playerCombat.maxMana - lastMaxMana) > 0.01f)
            {
                lastMaxMana = playerCombat.maxMana;
                manaSlider.maxValue = lastMaxMana;
                if (catchUpManaSlider != null) catchUpManaSlider.maxValue = lastMaxMana;
                RebuildNotches(manaSlider, manaNotches, lastMaxMana, 20f);
            }

            manaSlider.value = playerCombat.currentMana;

            if (manaText != null)
            {
                manaText.text = Mathf.RoundToInt(playerCombat.currentMana) + " / " + Mathf.RoundToInt(playerCombat.maxMana);
            }
        }

        // Pulsing danger effect for low health (<= 25% health)
        if (playerHealth != null && unitForegroundFills.Count > 0)
        {
            float hpPercent = (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;
            if (hpPercent <= 0.25f && playerHealth.CurrentHealth > 0)
            {
                float pulse = (Mathf.Sin(Time.time * 8f) + 1f) / 2f; // Fast warning pulse
                Color warningColor = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f, 1f), pulse);
                foreach (var fg in unitForegroundFills)
                {
                    if (fg != null) fg.color = warningColor;
                }
            }
            else
            {
                foreach (var fg in unitForegroundFills)
                {
                    if (fg != null) fg.color = Color.white;
                }
            }
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
        if (playerHealth != null)
        {
            UpdateMaxHealthUI(playerHealth.MaxHealth);
            UpdateHealthUI(0);
        }

        if (playerCurrency != null)
        {
            UpdateCoinsUI(playerCurrency.Coins);
            UpdatePotionsUI(playerCurrency.HealingPotions);
        }
    }

    private void UpdateHealthUI(int damageTaken)
    {
        if (playerHealth != null)
        {
            float currentHP = playerHealth.CurrentHealth;
            for (int i = 0; i < unitForegroundFills.Count; i++)
            {
                float unitMin = i * healthPerUnit;
                float fill = Mathf.Clamp01((currentHP - unitMin) / healthPerUnit);
                unitForegroundFills[i].fillAmount = fill;
            }

            if (healthText != null)
            {
                healthText.text = (playerHealth.CurrentHealth * 0.5f) + " / " + (playerHealth.MaxHealth * 0.5f);
            }
        }
    }

    private void UpdateMaxHealthUI(int newMaxHealth)
    {
        RebuildHealthUnits(newMaxHealth);
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
    //  HEALTH UNITS MANAGEMENT
    // ══════════════════════════════════════════════════════════════════

    private void RebuildHealthUnits(float newMaxHealth)
    {
        if (healthUnitsContainer != null)
        {
            foreach (Transform child in healthUnitsContainer)
            {
                Destroy(child.gameObject);
            }
        }
        unitForegroundFills.Clear();
        unitCatchUpFills.Clear();

        if (newMaxHealth <= 0) return;

        int numUnits = Mathf.CeilToInt(newMaxHealth / healthPerUnit);
        float unitSize = healthUnitSize * hudScale; // Configurable unit size
        float spacing = 4f * Mathf.Min(hudScale, 2f); // Halved spacing from 8f

        for (int i = 0; i < numUnits; i++)
        {
            float xOffset = i * (unitSize + spacing);
            CreateHealthUnit(xOffset, unitSize); // Offset relative to container left edge
        }

        UpdateHealthUI(0);

        if (playerHealth != null)
        {
            float currentHP = playerHealth.CurrentHealth;
            for (int i = 0; i < unitForegroundFills.Count; i++)
            {
                float unitMin = i * healthPerUnit;
                float fill = Mathf.Clamp01((currentHP - unitMin) / healthPerUnit);
                unitForegroundFills[i].fillAmount = fill;
                unitCatchUpFills[i].fillAmount = fill;
            }
        }
    }

    private void CreateHealthUnit(float xOffset, float size)
    {
        GameObject unitGo = new GameObject("HealthUnit_" + unitForegroundFills.Count);
        unitGo.transform.SetParent(healthUnitsContainer, false);
        RectTransform unitRT = unitGo.AddComponent<RectTransform>();

        // Anchored to left (0f) and centered vertically (0.5f) inside the container
        unitRT.anchorMin = new Vector2(0f, 0.5f);
        unitRT.anchorMax = new Vector2(0f, 0.5f);
        unitRT.pivot = new Vector2(0f, 0.5f);
        unitRT.anchoredPosition = new Vector2(xOffset, 0f);
        unitRT.sizeDelta = new Vector2(size, size);

        // 1. Background Outline Image (depleted unit state)
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(unitRT, false);
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = healthUnitSprite;
        bgImg.color = new Color(0.12f, 0.05f, 0.2f, 0.65f); // Dark-gothic transparent silhouette
        RectTransform bgRT = bgGo.GetComponent<RectTransform>();
        UIFactory.SetRect(bgRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 2. Catch-Up Image (lagging damage indicator)
        GameObject catchUpGo = new GameObject("CatchUp");
        catchUpGo.transform.SetParent(unitRT, false);
        Image catchUpImg = catchUpGo.AddComponent<Image>();
        catchUpImg.sprite = healthUnitSprite;
        catchUpImg.color = new Color(0.85f, 0.35f, 0.05f, 0.85f); // Orange warning fill
        catchUpImg.type = Image.Type.Filled;
        catchUpImg.fillMethod = Image.FillMethod.Horizontal;
        catchUpImg.fillAmount = 1f;
        RectTransform catchUpRT = catchUpGo.GetComponent<RectTransform>();
        UIFactory.SetRect(catchUpRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        unitCatchUpFills.Add(catchUpImg);

        // 3. Foreground Image (actual current health)
        GameObject fgGo = new GameObject("Foreground");
        fgGo.transform.SetParent(unitRT, false);
        Image fgImg = fgGo.AddComponent<Image>();
        fgImg.sprite = healthUnitSprite;
        fgImg.color = Color.white; // Full color original sprite
        fgImg.type = Image.Type.Filled;
        fgImg.fillMethod = Image.FillMethod.Horizontal;
        fgImg.fillAmount = 1f;
        RectTransform fgRT = fgGo.GetComponent<RectTransform>();
        UIFactory.SetRect(fgRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        unitForegroundFills.Add(fgImg);
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
