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

    // ── Runtime-built UI references ──
    private Canvas canvas;
    private Slider healthSlider;
    private Slider catchUpHealthSlider;
    private Image healthBorderImage;
    private TextMeshProUGUI healthText;
    
    private Slider manaSlider;
    private Slider catchUpManaSlider;
    private TextMeshProUGUI manaText;
    
    private TextMeshProUGUI coinsText;
    private TextMeshProUGUI potionsText;

    // ── Cached player components ──
    private Health playerHealth;
    private MageCombat playerCombat;
    private PlayerCurrency playerCurrency;

    // ── Unique HUD elements ──
    private List<GameObject> healthNotches = new List<GameObject>();
    private List<GameObject> manaNotches = new List<GameObject>();
    private float lastMaxMana = -1f;

    void Awake()
    {
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
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION — entire hierarchy built from code
    // ══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas (sort order 0) ──
        canvas = UIFactory.CreateCanvas("HUDCanvas", 0);
        canvas.transform.SetParent(transform, false);

        // Calculate scaled dimensions and positions to keep alignment clean at any scale
        float healthWidth = 250f * hudScale;
        float healthHeight = 20f * hudScale;
        float leftMargin = 30f * Mathf.Min(hudScale, 2f); // Extra padding for the diamond prefix icon
        float topMargin = 20f * Mathf.Min(hudScale, 2f);

        // Size of the prefix diamond
        float diamondSize = healthHeight * 1.4f;

        float healthX = leftMargin + (diamondSize * 0.5f) + (healthWidth / 2f);
        float healthY = -(topMargin + healthHeight / 2f);

        // ──────────────────── TOP-LEFT: Health ────────────────────

        // 1. Diamond Icon Border
        RectTransform diamondBorder = UIFactory.CreatePanel(
            canvas.transform, "HealthDiamondBorder", 
            UIFactory.BorderColor,
            new Vector2(0f, 1f), new Vector2(0f, 1f)
        );
        UIFactory.SetRectFixed(diamondBorder,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(leftMargin + diamondSize / 2f, healthY), 
            new Vector2(diamondSize, diamondSize)
        );
        diamondBorder.localEulerAngles = new Vector3(0f, 0f, 45f);

        // 2. Diamond Icon Inner
        RectTransform diamondInner = UIFactory.CreatePanel(
            diamondBorder, "HealthDiamondInner", 
            UIFactory.PanelBackground,
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f)
        );

        // 3. Health Symbol (rotated -45f so it sits upright)
        TextMeshProUGUI heartText = UIFactory.CreateText(
            diamondInner, "HealthSymbolText", "H",
            healthHeight * 0.8f, UIFactory.SliderHealthFill, 
            TextAlignmentOptions.Center
        );
        UIFactory.SetRect(heartText.rectTransform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );
        heartText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
        heartText.fontStyle = FontStyles.Bold;

        // 4. Health Bar Outer Border Panel
        RectTransform healthBorderPanel = UIFactory.CreatePanel(
            canvas.transform, "HealthBorder", 
            UIFactory.BorderColor,
            new Vector2(0f, 1f), new Vector2(0f, 1f)
        );
        UIFactory.SetRectFixed(healthBorderPanel,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(healthX, healthY), new Vector2(healthWidth + 6f, healthHeight + 6f)
        );
        healthBorderImage = healthBorderPanel.GetComponent<Image>();

        // 5. Health Bar Inner BG
        RectTransform healthInnerBg = UIFactory.CreatePanel(
            healthBorderPanel, "HealthInnerBg", 
            UIFactory.PanelBackground,
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f)
        );

        // 6. Health Catch-Up Slider (Under-fill damage indicator)
        catchUpHealthSlider = UIFactory.CreateSlider(healthInnerBg, "CatchUpHealthSlider", new Color(200f/255f, 100f/255f, 0f/255f, 0.7f));
        UIFactory.SetRect(catchUpHealthSlider.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );

        // 7. Main Health Slider
        healthSlider = UIFactory.CreateSlider(healthInnerBg, "HealthSlider", UIFactory.SliderHealthFill);
        Transform mainBgTransform = healthSlider.transform.Find("Background");
        if (mainBgTransform != null)
        {
            Image mainBgImg = mainBgTransform.GetComponent<Image>();
            if (mainBgImg != null) mainBgImg.color = Color.clear; // Make transparent to see catch-up slider
        }
        UIFactory.SetRect(healthSlider.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );

        // 8. Health Text Overlay
        healthText = UIFactory.CreateText(
            healthInnerBg, "HealthText", "100 / 100",
            12f * hudScale, UIFactory.TextWhite, TextAlignmentOptions.Center
        );
        UIFactory.SetRect(healthText.rectTransform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );
        healthText.fontStyle = FontStyles.Bold;
        healthText.enableWordWrapping = false;

        // ──────────────────── TOP-LEFT: Mana ────────────────────

        float manaWidth = 250f * hudScale;
        float manaHeight = 16f * hudScale;
        float gap = 12f * Mathf.Min(hudScale, 2f); // Spacer between bars
        float manaDiamondSize = manaHeight * 1.4f;

        float manaX = leftMargin + (manaDiamondSize * 0.5f) + (manaWidth / 2f);
        float manaY = healthY - (healthHeight / 2f) - gap - (manaHeight / 2f);

        // 1. Mana Diamond Icon Border
        RectTransform manaDiamondBorder = UIFactory.CreatePanel(
            canvas.transform, "ManaDiamondBorder", 
            UIFactory.BorderColor,
            new Vector2(0f, 1f), new Vector2(0f, 1f)
        );
        UIFactory.SetRectFixed(manaDiamondBorder,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(leftMargin + manaDiamondSize / 2f, manaY), 
            new Vector2(manaDiamondSize, manaDiamondSize)
        );
        manaDiamondBorder.localEulerAngles = new Vector3(0f, 0f, 45f);

        // 2. Mana Diamond Icon Inner
        RectTransform manaDiamondInner = UIFactory.CreatePanel(
            manaDiamondBorder, "ManaDiamondInner", 
            UIFactory.PanelBackground,
            Vector2.zero, Vector2.one,
            new Vector2(2.5f, 2.5f), new Vector2(-2.5f, -2.5f)
        );

        // 3. Mana Symbol (rotated -45f so it sits upright)
        TextMeshProUGUI manaSymbol = UIFactory.CreateText(
            manaDiamondInner, "ManaSymbolText", "M",
            manaHeight * 0.8f, UIFactory.SliderManaFill, 
            TextAlignmentOptions.Center
        );
        UIFactory.SetRect(manaSymbol.rectTransform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );
        manaSymbol.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
        manaSymbol.fontStyle = FontStyles.Bold;

        // 4. Mana Bar Outer Border Panel
        RectTransform manaBorderPanel = UIFactory.CreatePanel(
            canvas.transform, "ManaBorder", 
            UIFactory.BorderColor,
            new Vector2(0f, 1f), new Vector2(0f, 1f)
        );
        UIFactory.SetRectFixed(manaBorderPanel,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(manaX, manaY), new Vector2(manaWidth + 5f, manaHeight + 5f)
        );

        // 5. Mana Bar Inner BG
        RectTransform manaInnerBg = UIFactory.CreatePanel(
            manaBorderPanel, "ManaInnerBg", 
            UIFactory.PanelBackground,
            Vector2.zero, Vector2.one,
            new Vector2(2.5f, 2.5f), new Vector2(-2.5f, -2.5f)
        );

        // 6. Mana Catch-Up Slider (Under-fill mana indicator)
        catchUpManaSlider = UIFactory.CreateSlider(manaInnerBg, "CatchUpManaSlider", new Color(0f, 180f/255f, 220f/255f, 0.7f));
        UIFactory.SetRect(catchUpManaSlider.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );

        // 7. Main Mana Slider
        manaSlider = UIFactory.CreateSlider(manaInnerBg, "ManaSlider", UIFactory.SliderManaFill);
        Transform manaBgTransform = manaSlider.transform.Find("Background");
        if (manaBgTransform != null)
        {
            Image manaBgImg = manaBgTransform.GetComponent<Image>();
            if (manaBgImg != null) manaBgImg.color = Color.clear; // Make transparent
        }
        UIFactory.SetRect(manaSlider.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );

        // 8. Mana Text Overlay
        manaText = UIFactory.CreateText(
            manaInnerBg, "ManaText", "100 / 100",
            10f * hudScale, UIFactory.TextWhite, TextAlignmentOptions.Center
        );
        UIFactory.SetRect(manaText.rectTransform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero
        );
        manaText.fontStyle = FontStyles.Bold;
        manaText.enableWordWrapping = false;

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
        // Smoothly drain the catch-up sliders (under-fills)
        if (catchUpHealthSlider != null && healthSlider != null)
        {
            if (catchUpHealthSlider.value > healthSlider.value)
            {
                catchUpHealthSlider.value = Mathf.Lerp(catchUpHealthSlider.value, healthSlider.value, Time.deltaTime * 3.5f);
                if (catchUpHealthSlider.value - healthSlider.value < 0.5f)
                {
                    catchUpHealthSlider.value = healthSlider.value;
                }
            }
            else
            {
                catchUpHealthSlider.value = healthSlider.value;
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
        if (healthBorderImage != null && playerHealth != null)
        {
            float hpPercent = (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;
            if (hpPercent <= 0.25f && playerHealth.CurrentHealth > 0)
            {
                float pulse = (Mathf.Sin(Time.time * 8f) + 1f) / 2f; // Fast warning pulse
                healthBorderImage.color = Color.Lerp(UIFactory.BorderColor, Color.red, pulse);
            }
            else
            {
                healthBorderImage.color = UIFactory.BorderColor;
            }
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
            if (catchUpHealthSlider != null) catchUpHealthSlider.value = playerHealth.CurrentHealth;
        }

        if (playerCurrency != null)
        {
            UpdateCoinsUI(playerCurrency.Coins);
            UpdatePotionsUI(playerCurrency.HealingPotions);
        }
    }

    private void UpdateHealthUI(int damageTaken)
    {
        if (playerHealth != null && healthSlider != null)
        {
            healthSlider.value = playerHealth.CurrentHealth;
            if (healthText != null)
            {
                healthText.text = playerHealth.CurrentHealth + " / " + playerHealth.MaxHealth;
            }
        }
    }

    private void UpdateMaxHealthUI(int newMaxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = newMaxHealth;
            if (catchUpHealthSlider != null) catchUpHealthSlider.maxValue = newMaxHealth;
            if (playerHealth != null)
            {
                healthSlider.value = playerHealth.CurrentHealth;
                if (catchUpHealthSlider != null) catchUpHealthSlider.value = playerHealth.CurrentHealth;
            }

            RebuildNotches(healthSlider, healthNotches, newMaxHealth, 25f);
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

        if (maxVal <= 0 || interval <= 0) return;

        int numNotches = Mathf.FloorToInt((maxVal - 0.1f) / interval);
        for (int i = 1; i <= numNotches; i++)
        {
            float percent = (i * interval) / maxVal;

            // Create notch inside slider's Fill Area
            Transform fillArea = slider.transform.Find("Fill Area");
            if (fillArea != null)
            {
                RectTransform notchRT = UIFactory.CreatePanel(
                    fillArea, "Notch_" + i, 
                    new Color(10f/255f, 8f/255f, 20f/255f, 0.75f), // Dark background divider
                    new Vector2(percent, 0f), new Vector2(percent, 1f),
                    new Vector2(-1.5f * hudScale, 0f), new Vector2(1.5f * hudScale, 0f)
                );
                notchesList.Add(notchRT.gameObject);
            }
        }
    }
}
