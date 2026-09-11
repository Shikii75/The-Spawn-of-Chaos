using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpawnOfChaos.Weapons;

/// <summary>
/// Full-Screen Drake-Themed Shop UI with Weapon Grid & Mastery Arsenal,
/// matching the sleek cyber-gothic glassmorphic aesthetic of the Title and Pause screens.
/// Procedurally built at runtime via UIFactory.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Header("Shop System Connection")]
    public ShopSystem shopSystem;

    // UI Root References
    private GameObject shopPanel;
    private TextMeshProUGUI currencyText;
    private TextMeshProUGUI potionText;

    // Tabs & Containers
    private GameObject weaponsTabGo;
    private GameObject suppliesTabGo;
    private Button weaponsTabBtn;
    private Button suppliesTabBtn;
    private Transform weaponGridContainer;
    private Transform suppliesListContainer;

    private enum Tab { Weapons, Supplies }
    private Tab currentTab = Tab.Weapons;

    public bool IsShopActive => shopPanel != null && shopPanel.activeSelf;

    // Drake / Cyber-Gothic Void Palette
    private static readonly Color VoidOverlay       = new Color(0.015f, 0.025f, 0.06f, 0.92f);
    private static readonly Color PanelVoidBg       = new Color(10f/255f, 9f/255f, 20f/255f, 0.98f);
    private static readonly Color HeaderBg          = new Color(7f/255f, 6f/255f, 15f/255f, 1.0f);
    private static readonly Color CardVoidBg        = new Color(17f/255f, 15f/255f, 32f/255f, 0.96f);
    private static readonly Color CardEquippedBg    = new Color(14f/255f, 28f/255f, 42f/255f, 0.98f);
    private static readonly Color NeonCyan          = new Color(0.34f, 0.88f, 1.0f, 1.0f);
    private static readonly Color NeonCyanDim       = new Color(0.34f, 0.88f, 1.0f, 0.35f);
    private static readonly Color ArcaneViolet      = new Color(0.66f, 0.33f, 0.97f, 1.0f);
    private static readonly Color ArcaneVioletDim   = new Color(0.66f, 0.33f, 0.97f, 0.35f);
    private static readonly Color DrakeGold         = new Color(0.98f, 0.78f, 0.16f, 1.0f);
    private static readonly Color CrimsonFlame      = new Color(0.96f, 0.25f, 0.37f, 1.0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        WeaponManager.EnsureExists();
        BuildUI();
    }

    void Start()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        if (shopSystem == null)
        {
            shopSystem = FindFirstObjectByType<ShopSystem>() ?? GetComponent<ShopSystem>() ?? gameObject.AddComponent<ShopSystem>();
        }

        WeaponManager.EnsureExists();
    }

    void Update()
    {
        if (!IsShopActive) return;

        UpdateCurrencyUI();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseShop();
        }
    }

    // ── UI Construction ──────────────────────────────────────────────

    private void BuildUI()
    {
        // Dedicated high-sorting Canvas (Order 70 — firmly above HUD Canvas Order 50)
        Canvas canvas = UIFactory.CreateCanvas("ShopCanvas", 70);
        canvas.transform.SetParent(transform, false);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Layer 0: Fullscreen Void Overlay backdrop — shopPanel is the overlay itself so the entire backdrop hides when closed
        RectTransform overlayRT = UIFactory.CreateFullScreenPanel(canvas.transform, "ShopOverlay", VoidOverlay);
        shopPanel = overlayRT.gameObject;

        // Layer 1: Main Shop Frame — Covers screen like Main Menu (~92% viewport)
        RectTransform frameRT = UIFactory.CreatePanel(
            overlayRT, "ShopFrame", PanelVoidBg,
            Vector2.zero, Vector2.one,
            new Vector2(40f, 25f), new Vector2(-40f, -25f));

        Image frameImg = frameRT.GetComponent<Image>();
        frameImg.sprite = UIFactory.GetRoundedSprite();
        frameImg.type = Image.Type.Sliced;

        // Sleek arcane violet outline
        Outline frameOutline = frameRT.gameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(ArcaneViolet.r, ArcaneViolet.g, ArcaneViolet.b, 0.55f);
        frameOutline.effectDistance = new Vector2(2f, -2f);

        // Vertical Master Layout controlling full inner height
        VerticalLayoutGroup masterVLG = UIFactory.AddVerticalLayout(frameRT.gameObject, 0f,
            new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        masterVLG.childControlWidth = true;
        masterVLG.childControlHeight = true;
        masterVLG.childForceExpandWidth = true;
        masterVLG.childForceExpandHeight = false;

        // ── 1. TOP HEADER BAR ──
        BuildHeader(frameRT);

        // ── 2. TAB SELECTOR BAR ──
        BuildTabBar(frameRT);

        // ── 3. THIN GLOWING DIVIDER ──
        RectTransform tabDivider = UIFactory.CreateDivider(frameRT, "TabDivider");
        tabDivider.GetComponent<Image>().color = new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.35f);
        LayoutElement divLE = tabDivider.gameObject.AddComponent<LayoutElement>();
        divLE.preferredHeight = 2f;
        divLE.minHeight = 2f;
        divLE.flexibleHeight = 0;

        // ── 4. CONTENT AREA CONTAINER (Fills 100% of remaining panel height) ──
        GameObject contentArea = new GameObject("TabContentArea", typeof(RectTransform));
        contentArea.transform.SetParent(frameRT, false);
        LayoutElement contentLE = contentArea.AddComponent<LayoutElement>();
        contentLE.flexibleHeight = 1;
        contentLE.flexibleWidth = 1;
        contentLE.minHeight = 300f;

        // Weapons Tab (Grid View)
        weaponsTabGo = BuildWeaponsTab(contentArea.transform);

        // Supplies Tab (List / Cards)
        suppliesTabGo = BuildSuppliesTab(contentArea.transform);

        // Initialize Tab
        SwitchTab(Tab.Weapons);
        shopPanel.SetActive(false);
    }

    private void BuildHeader(Transform parent)
    {
        RectTransform headerRT = UIFactory.CreatePanel(parent, "HeaderBar", HeaderBg,
            Vector2.zero, Vector2.one);
        LayoutElement hLE = headerRT.gameObject.AddComponent<LayoutElement>();
        hLE.preferredHeight = 70f;
        hLE.minHeight = 70f;
        hLE.flexibleHeight = 0;

        HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(
            headerRT.gameObject, 16f, new RectOffset(35, 35, 10, 10), TextAnchor.MiddleLeft);
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Title Text
        TextMeshProUGUI titleText = UIFactory.CreateText(
            headerRT, "ShopTitle", "❖  SHADOW ARSENAL & BAZAAR  ❖",
            26f, NeonCyan, TextAlignmentOptions.Left);
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 3f;
        UIFactory.AddLayoutElement(titleText.gameObject, preferredWidth: 540f, preferredHeight: 50f);

        // Flexible Spacer
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(headerRT, false);
        LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1;
        spacerLE.preferredWidth = 50f;
        spacerLE.preferredHeight = 50f;

        // Currency Pill: Coins
        RectTransform coinPillRT = UIFactory.CreatePanel(headerRT, "CoinPill",
            new Color(25f/255f, 20f/255f, 10f/255f, 0.95f),
            Vector2.zero, Vector2.one);
        coinPillRT.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
        coinPillRT.GetComponent<Image>().type = Image.Type.Sliced;
        UIFactory.AddLayoutElement(coinPillRT.gameObject, preferredWidth: 190f, preferredHeight: 44f);
        Outline coinOutline = coinPillRT.gameObject.AddComponent<Outline>();
        coinOutline.effectColor = new Color(DrakeGold.r, DrakeGold.g, DrakeGold.b, 0.45f);

        currencyText = UIFactory.CreateText(coinPillRT, "CurrencyText", "❖ 0 COINS",
            17f, DrakeGold, TextAlignmentOptions.Center);
        currencyText.fontStyle = FontStyles.Bold;
        UIFactory.SetRect(currencyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Potion Pill
        RectTransform potPillRT = UIFactory.CreatePanel(headerRT, "PotPill",
            new Color(15f/255f, 25f/255f, 35f/255f, 0.95f),
            Vector2.zero, Vector2.one);
        potPillRT.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
        potPillRT.GetComponent<Image>().type = Image.Type.Sliced;
        UIFactory.AddLayoutElement(potPillRT.gameObject, preferredWidth: 160f, preferredHeight: 44f);
        Outline potOutline = potPillRT.gameObject.AddComponent<Outline>();
        potOutline.effectColor = new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.45f);

        potionText = UIFactory.CreateText(potPillRT, "PotionText", "✦ 0 POTIONS",
            16f, NeonCyan, TextAlignmentOptions.Center);
        potionText.fontStyle = FontStyles.Bold;
        UIFactory.SetRect(potionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Header Close Button (Integrated into Header Bar)
        Button closeBtn = UIFactory.CreateButton(headerRT, "ShopCloseBtn", "✕ CLOSE", 14f, () => CloseShop());
        UIFactory.AddLayoutElement(closeBtn.gameObject, preferredWidth: 115f, preferredHeight: 44f);
        closeBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
        closeBtn.GetComponent<Image>().type = Image.Type.Sliced;

        ColorBlock cb = closeBtn.colors;
        cb.normalColor = new Color(CrimsonFlame.r, CrimsonFlame.g, CrimsonFlame.b, 0.35f);
        cb.highlightedColor = new Color(CrimsonFlame.r, CrimsonFlame.g, CrimsonFlame.b, 0.65f);
        cb.pressedColor = new Color(CrimsonFlame.r, CrimsonFlame.g, CrimsonFlame.b, 0.9f);
        closeBtn.colors = cb;
        closeBtn.GetComponent<Image>().color = cb.normalColor;
    }

    private void BuildTabBar(Transform parent)
    {
        RectTransform tabBarRT = UIFactory.CreatePanel(parent, "TabBar",
            new Color(13f/255f, 11f/255f, 24f/255f, 1.0f),
            Vector2.zero, Vector2.one);
        LayoutElement tLE = tabBarRT.gameObject.AddComponent<LayoutElement>();
        tLE.preferredHeight = 48f;
        tLE.minHeight = 48f;
        tLE.flexibleHeight = 0;

        HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(
            tabBarRT.gameObject, 12f, new RectOffset(35, 35, 5, 5), TextAnchor.MiddleLeft);
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        weaponsTabBtn = UIFactory.CreateButton(tabBarRT, "Tab_Weapons", "⚔  WEAPON ARSENAL & MASTERY", 15f,
            () => SwitchTab(Tab.Weapons));
        UIFactory.AddLayoutElement(weaponsTabBtn.gameObject, preferredWidth: 290f, preferredHeight: 38f);
        weaponsTabBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
        weaponsTabBtn.GetComponent<Image>().type = Image.Type.Sliced;

        suppliesTabBtn = UIFactory.CreateButton(tabBarRT, "Tab_Supplies", "⚗  RELICS & SUPPLIES", 15f,
            () => SwitchTab(Tab.Supplies));
        UIFactory.AddLayoutElement(suppliesTabBtn.gameObject, preferredWidth: 240f, preferredHeight: 38f);
        suppliesTabBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
        suppliesTabBtn.GetComponent<Image>().type = Image.Type.Sliced;
    }

    private void SwitchTab(Tab tab)
    {
        currentTab = tab;
        if (weaponsTabGo != null) weaponsTabGo.SetActive(tab == Tab.Weapons);
        if (suppliesTabGo != null) suppliesTabGo.SetActive(tab == Tab.Supplies);

        UpdateTabBtnStyle(weaponsTabBtn, tab == Tab.Weapons, NeonCyan);
        UpdateTabBtnStyle(suppliesTabBtn, tab == Tab.Supplies, ArcaneViolet);

        RefreshShopItems();
    }

    private void UpdateTabBtnStyle(Button btn, bool isActive, Color activeCol)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = isActive
            ? new Color(activeCol.r, activeCol.g, activeCol.b, 0.40f)
            : new Color(30f/255f, 25f/255f, 50f/255f, 0.35f);
        cb.highlightedColor = isActive
            ? new Color(activeCol.r, activeCol.g, activeCol.b, 0.60f)
            : new Color(45f/255f, 40f/255f, 70f/255f, 0.55f);
        btn.colors = cb;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = cb.normalColor;
    }

    // ── Weapons Tab (Grid View) ──────────────────────────────────────

    private GameObject BuildWeaponsTab(Transform parent)
    {
        GameObject scrollGo = new GameObject("WeaponsScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(parent, false);
        RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
        UIFactory.SetRect(scrollRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        ScrollRect sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 38f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewRT = viewportGo.GetComponent<RectTransform>();
        UIFactory.SetRect(viewRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewRT.pivot = new Vector2(0.5f, 1f);
        viewportGo.AddComponent<RectMask2D>();
        sr.viewport = viewRT;

        GameObject contentGo = new GameObject("WeaponGridContent", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 400f);
        contentRT.anchoredPosition = Vector2.zero;
        sr.content = contentRT;

        GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(330f, 405f);
        grid.spacing = new Vector2(20f, 20f);
        grid.padding = new RectOffset(30, 30, 20, 30);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.Flexible;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        weaponGridContainer = contentRT;
        return scrollGo;
    }

    // ── Supplies Tab (Relics & Potions) ──────────────────────────────

    private GameObject BuildSuppliesTab(Transform parent)
    {
        GameObject scrollGo = new GameObject("SuppliesScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(parent, false);
        RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
        UIFactory.SetRect(scrollRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        ScrollRect sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 32f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewRT = viewportGo.GetComponent<RectTransform>();
        UIFactory.SetRect(viewRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewRT.pivot = new Vector2(0.5f, 1f);
        viewportGo.AddComponent<RectMask2D>();
        sr.viewport = viewRT;

        GameObject contentGo = new GameObject("SuppliesContent", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 300f);
        contentRT.anchoredPosition = Vector2.zero;
        sr.content = contentRT;

        UIFactory.AddVerticalLayout(contentGo, 12f, new RectOffset(50, 50, 20, 30), TextAnchor.UpperCenter);
        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        suppliesListContainer = contentRT;
        return scrollGo;
    }

    // ── Public API ───────────────────────────────────────────────────

    public void OpenShop()
    {
        WeaponManager.EnsureExists();
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (shopSystem == null)
            {
                shopSystem = FindFirstObjectByType<ShopSystem>() ?? GetComponent<ShopSystem>() ?? gameObject.AddComponent<ShopSystem>();
            }

            UpdateCurrencyUI();
            RefreshShopItems();

            HUDManager.Instance?.UpdateVisibility();
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
        }
    }

    public void CloseShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
            HUDManager.Instance?.UpdateVisibility();
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
        }
    }

    public void BuyItemFromUI(int itemIndex)
    {
        if (shopSystem != null)
        {
            shopSystem.BuyItem(itemIndex);
            UpdateCurrencyUI();
            RefreshShopItems();
        }
    }

    public void UpdateCurrencyUI()
    {
        int coins = 0;
        int pots = 0;
        if (PlayerCurrency.Instance != null)
        {
            coins = PlayerCurrency.Instance.Coins;
            pots = PlayerCurrency.Instance.HealingPotions;
        }
        else if (shopSystem != null)
        {
            coins = shopSystem.playerCurrency;
        }

        if (currencyText != null)
            currencyText.text = $"❖ {coins} COINS";
        if (potionText != null)
            potionText.text = $"✦ {pots} POTIONS";
    }

    // ── Refresh & Content Populators ────────────────────────────────

    public void RefreshShopItems()
    {
        WeaponManager.EnsureExists();

        if (currentTab == Tab.Weapons)
        {
            PopulateWeaponsGrid();
        }
        else
        {
            PopulateSuppliesList();
        }
    }

    private void PopulateWeaponsGrid()
    {
        if (weaponGridContainer == null) return;
        ClearContainer(weaponGridContainer);

        if (WeaponManager.Instance == null) return;

        foreach (var weapon in WeaponManager.Instance.AllWeapons)
        {
            try
            {
                CreateWeaponCard(weaponGridContainer, weapon);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ShopUI] Failed to create card for {weapon.displayName}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    private void CreateWeaponCard(Transform container, WeaponInfo wep)
    {
        bool isUnlocked = WeaponManager.Instance.IsUnlocked(wep.id);
        bool isEquipped = (WeaponManager.Instance.ActiveWeapon == wep.id);
        int tier = WeaponManager.Instance.GetTier(wep.id);
        int playerCoins = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Coins : 0;

        // Card root panel
        Color cardBg = isEquipped ? CardEquippedBg : CardVoidBg;
        RectTransform cardRT = UIFactory.CreatePanel(container, "Card_" + wep.id, cardBg,
            Vector2.zero, Vector2.one);
        Image cardImg = cardRT.GetComponent<Image>();
        cardImg.sprite = UIFactory.GetRoundedSprite();
        cardImg.type = Image.Type.Sliced;

        Outline outline = cardRT.gameObject.AddComponent<Outline>();
        if (isEquipped)
        {
            outline.effectColor = NeonCyan;
            outline.effectDistance = new Vector2(2.5f, -2.5f);
        }
        else if (isUnlocked)
        {
            outline.effectColor = new Color(ArcaneViolet.r, ArcaneViolet.g, ArcaneViolet.b, 0.5f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        else
        {
            outline.effectColor = new Color(0.2f, 0.18f, 0.3f, 0.4f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        // Layout inside Card
        VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(cardRT.gameObject, 6f,
            new RectOffset(16, 16, 14, 14), TextAnchor.UpperCenter);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 1. Archetype Pill Badge
        Color badgeColor = wep.auraColor;
        badgeColor.a = 1f;
        TextMeshProUGUI archeText = UIFactory.CreateText(
            cardRT, "Archetype", $"[ {wep.archetype.ToUpper()} ]",
            11f, badgeColor, TextAlignmentOptions.Center);
        archeText.fontStyle = FontStyles.Bold;
        archeText.characterSpacing = 2f;
        UIFactory.AddLayoutElement(archeText.gameObject, preferredHeight: 18f);

        // 2. Weapon Name
        TextMeshProUGUI nameText = UIFactory.CreateText(
            cardRT, "WepName", wep.displayName,
            17f, isEquipped ? NeonCyan : UIFactory.TextWhite, TextAlignmentOptions.Center);
        nameText.fontStyle = FontStyles.Bold;
        UIFactory.AddLayoutElement(nameText.gameObject, preferredHeight: 24f);

        // 3. Sprite / Icon Preview Area
        RectTransform iconBoxRT = UIFactory.CreatePanel(cardRT, "IconBox",
            new Color(10f/255f, 9f/255f, 22f/255f, 0.9f),
            Vector2.zero, Vector2.one);
        iconBoxRT.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
        iconBoxRT.GetComponent<Image>().type = Image.Type.Sliced;
        UIFactory.AddLayoutElement(iconBoxRT.gameObject, preferredHeight: 95f, preferredWidth: 280f);

        Sprite wepSprite = wep.GetSprite();
        if (wepSprite != null)
        {
            GameObject imgGo = new GameObject("WepIcon", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(iconBoxRT, false);
            Image img = imgGo.GetComponent<Image>();
            img.sprite = wepSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform imgRT = img.rectTransform;
            imgRT.anchorMin = new Vector2(0.15f, 0.1f);
            imgRT.anchorMax = new Vector2(0.85f, 0.9f);
            imgRT.sizeDelta = Vector2.zero;
        }
        else
        {
            TextMeshProUGUI rune = UIFactory.CreateText(
                iconBoxRT, "RuneIcon", "⚔", 42f, badgeColor, TextAlignmentOptions.Center);
            UIFactory.SetRect(rune.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        // 4. Combat Stats Row
        int currentDmg = wep.GetDamageForTier(tier);
        int currentExplode = wep.GetExplosionDamageForTier(tier);
        string statStr = $"DMG: <color=#{ColorUtility.ToHtmlStringRGB(CrimsonFlame)}>{currentDmg}</color>  |  BLAST: <color=#{ColorUtility.ToHtmlStringRGB(DrakeGold)}>{currentExplode}</color>";
        TextMeshProUGUI statsText = UIFactory.CreateText(
            cardRT, "Stats", statStr,
            12f, UIFactory.TextMuted, TextAlignmentOptions.Center);
        statsText.fontStyle = FontStyles.Bold;
        UIFactory.AddLayoutElement(statsText.gameObject, preferredHeight: 20f);

        // 5. Ability Description
        string abilityInfo = $"<b><color=#{ColorUtility.ToHtmlStringRGB(badgeColor)}>{wep.abilityName}</color></b>\n{wep.abilityDescription}";
        TextMeshProUGUI abilityText = UIFactory.CreateText(
            cardRT, "AbilityDesc", abilityInfo,
            10.5f, new Color(0.75f, 0.75f, 0.85f, 1f), TextAlignmentOptions.Center);
        abilityText.lineSpacing = -10f;
        UIFactory.AddLayoutElement(abilityText.gameObject, preferredHeight: 52f);

        // Divider
        RectTransform cardDiv = UIFactory.CreateDivider(cardRT, "CardDiv");
        cardDiv.GetComponent<Image>().color = new Color(0.3f, 0.25f, 0.45f, 0.35f);
        UIFactory.AddLayoutElement(cardDiv.gameObject, preferredHeight: 1f);

        // 6. Mastery Tier Status
        string tierLabel = isUnlocked ? (tier == 1 ? "TIER I  •  BASE" : (tier == 2 ? "TIER II  •  AWAKENED" : "✦ TIER III  •  MAX ✦")) : "LOCKED";
        Color tierCol = isUnlocked ? (tier == 3 ? DrakeGold : NeonCyan) : Color.gray;
        TextMeshProUGUI tierText = UIFactory.CreateText(
            cardRT, "TierStatus", tierLabel,
            12f, tierCol, TextAlignmentOptions.Center);
        tierText.fontStyle = FontStyles.Bold;
        UIFactory.AddLayoutElement(tierText.gameObject, preferredHeight: 20f);

        // 7. Actions Row (Buy / Equip & Upgrade)
        if (!isUnlocked)
        {
            int cost = wep.unlockCost;
            bool canAfford = playerCoins >= cost;
            string buyLabel = $"BUY WEAPON ({cost} COINS)";
            Button buyBtn = UIFactory.CreateButton(cardRT, "BuyWepBtn", buyLabel, 13f, () =>
            {
                if (PlayerCurrency.Instance != null && PlayerCurrency.Instance.SpendCoins(cost))
                {
                    WeaponManager.Instance.UnlockWeapon(wep.id);
                    WeaponManager.Instance.EquipWeapon(wep.id);
                    UpdateCurrencyUI();
                    RefreshShopItems();
                }
            });
            UIFactory.AddLayoutElement(buyBtn.gameObject, preferredHeight: 38f);
            buyBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
            buyBtn.GetComponent<Image>().type = Image.Type.Sliced;

            ColorBlock bcb = buyBtn.colors;
            bcb.normalColor = canAfford
                ? new Color(DrakeGold.r, DrakeGold.g, DrakeGold.b, 0.35f)
                : new Color(0.4f, 0.2f, 0.2f, 0.25f);
            bcb.highlightedColor = canAfford
                ? new Color(DrakeGold.r, DrakeGold.g, DrakeGold.b, 0.65f)
                : new Color(0.4f, 0.2f, 0.2f, 0.35f);
            buyBtn.colors = bcb;
            buyBtn.interactable = canAfford;
        }
        else
        {
            GameObject btnRow = new GameObject("ActionRow", typeof(RectTransform));
            btnRow.transform.SetParent(cardRT, false);
            UIFactory.AddLayoutElement(btnRow, preferredHeight: 38f);
            HorizontalLayoutGroup ahlg = UIFactory.AddHorizontalLayout(
                btnRow, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            ahlg.childControlWidth = true;
            ahlg.childForceExpandWidth = true;

            // Equip Item
            if (isEquipped)
            {
                RectTransform eqPanel = UIFactory.CreatePanel(btnRow.transform, "EqBadge",
                    new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.2f),
                    Vector2.zero, Vector2.one);
                eqPanel.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
                eqPanel.GetComponent<Image>().type = Image.Type.Sliced;
                UIFactory.AddLayoutElement(eqPanel.gameObject, preferredHeight: 38f, preferredWidth: 140f);

                TextMeshProUGUI eqLabel = UIFactory.CreateText(
                    eqPanel, "EqLabel", "EQUIPPED",
                    12.5f, NeonCyan, TextAlignmentOptions.Center);
                eqLabel.fontStyle = FontStyles.Bold;
                UIFactory.SetRect(eqLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            else
            {
                Button equipBtn = UIFactory.CreateButton(btnRow.transform, "EquipBtn", "EQUIP", 13f, () =>
                {
                    WeaponManager.Instance.EquipWeapon(wep.id);
                    RefreshShopItems();
                });
                UIFactory.AddLayoutElement(equipBtn.gameObject, preferredHeight: 38f, preferredWidth: 140f);
                equipBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
                equipBtn.GetComponent<Image>().type = Image.Type.Sliced;

                ColorBlock ecb = equipBtn.colors;
                ecb.normalColor = new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.35f);
                ecb.highlightedColor = new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.65f);
                equipBtn.colors = ecb;
            }

            // Upgrade Item
            if (tier < 3)
            {
                int upgCost = (tier == 1) ? wep.tier2Cost : wep.tier3Cost;
                bool canAffordUpg = playerCoins >= upgCost;
                Button upgBtn = UIFactory.CreateButton(btnRow.transform, "UpgBtn", $"UPG ({upgCost}c)", 12.5f, () =>
                {
                    if (PlayerCurrency.Instance != null && PlayerCurrency.Instance.SpendCoins(upgCost))
                    {
                        WeaponManager.Instance.UpgradeWeapon(wep.id);
                        UpdateCurrencyUI();
                        RefreshShopItems();
                    }
                });
                UIFactory.AddLayoutElement(upgBtn.gameObject, preferredHeight: 38f, preferredWidth: 140f);
                upgBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
                upgBtn.GetComponent<Image>().type = Image.Type.Sliced;

                ColorBlock ucb = upgBtn.colors;
                ucb.normalColor = canAffordUpg
                    ? new Color(ArcaneViolet.r, ArcaneViolet.g, ArcaneViolet.b, 0.35f)
                    : new Color(0.3f, 0.2f, 0.3f, 0.25f);
                ucb.highlightedColor = canAffordUpg
                    ? new Color(ArcaneViolet.r, ArcaneViolet.g, ArcaneViolet.b, 0.65f)
                    : new Color(0.3f, 0.2f, 0.3f, 0.35f);
                upgBtn.colors = ucb;
                upgBtn.interactable = canAffordUpg;
            }
            else
            {
                RectTransform maxPanel = UIFactory.CreatePanel(btnRow.transform, "MaxBadge",
                    new Color(DrakeGold.r, DrakeGold.g, DrakeGold.b, 0.2f),
                    Vector2.zero, Vector2.one);
                maxPanel.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
                maxPanel.GetComponent<Image>().type = Image.Type.Sliced;
                UIFactory.AddLayoutElement(maxPanel.gameObject, preferredHeight: 38f, preferredWidth: 140f);

                TextMeshProUGUI maxLabel = UIFactory.CreateText(
                    maxPanel, "MaxLabel", "✦ MAX TIER ✦",
                    12f, DrakeGold, TextAlignmentOptions.Center);
                maxLabel.fontStyle = FontStyles.Bold;
                UIFactory.SetRect(maxLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
        }
    }

    private void PopulateSuppliesList()
    {
        if (suppliesListContainer == null || shopSystem == null) return;
        ClearContainer(suppliesListContainer);

        ShopItem[] items = shopSystem.shopItems;
        if (items == null) return;

        int playerCoins = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Coins : 0;

        for (int i = 0; i < items.Length; i++)
        {
            ShopItem item = items[i];
            int index = i;
            bool isRepeatable = (item.type == ShopItemType.HealingPotion);
            bool canAfford = playerCoins >= item.cost;

            RectTransform rowRT = UIFactory.CreatePanel(
                suppliesListContainer, "SupplyRow_" + i, CardVoidBg,
                Vector2.zero, Vector2.one);
            rowRT.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
            rowRT.GetComponent<Image>().type = Image.Type.Sliced;
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 65f);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(
                rowRT.gameObject, 16f, new RectOffset(25, 25, 10, 10), TextAnchor.MiddleLeft);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Glyph
            string glyph = item.type == ShopItemType.HealingPotion ? "✦" :
                           (item.type == ShopItemType.HealthBoost ? "❖" :
                           (item.type == ShopItemType.DashUpgrade ? "⚔" : "◈"));
            TextMeshProUGUI glyphText = UIFactory.CreateText(
                rowRT, "Glyph", glyph, 24f, DrakeGold, TextAlignmentOptions.Center);
            UIFactory.AddLayoutElement(glyphText.gameObject, preferredWidth: 45f, preferredHeight: 45f);

            // Description
            string desc = item.type == ShopItemType.HealingPotion ? "Restores 45% of maximum HP on use" :
                          (item.type == ShopItemType.HealthBoost ? "Permanently increases player maximum health" :
                          (item.type == ShopItemType.DashUpgrade ? "Reduces dash cooldown and extends invulnerability duration" : "Unlocks mystic ranged arcane projectile"));
            TextMeshProUGUI nameText = UIFactory.CreateText(
                rowRT, "ItemName", $"<b>{item.itemName}</b>\n<size=12><color=#A0A0B5>{desc}</color></size>",
                15f, UIFactory.TextWhite, TextAlignmentOptions.Left);
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 620f, preferredHeight: 48f);

            // Spacer
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(rowRT, false);
            UIFactory.AddLayoutElement(spacer, preferredWidth: 100f, preferredHeight: 40f, flexibleWidth: true);

            // Cost
            TextMeshProUGUI costText = UIFactory.CreateText(
                rowRT, "CostText", $"❖ {item.cost} COINS",
                16f, DrakeGold, TextAlignmentOptions.Right);
            costText.fontStyle = FontStyles.Bold;
            UIFactory.AddLayoutElement(costText.gameObject, preferredWidth: 150f, preferredHeight: 40f);

            // Button / Acquired
            if (item.purchased && !isRepeatable)
            {
                TextMeshProUGUI soldText = UIFactory.CreateText(
                    rowRT, "SoldLabel", "✓ ACQUIRED",
                    14f, new Color(0.5f, 0.5f, 0.6f, 0.6f), TextAlignmentOptions.Center);
                soldText.fontStyle = FontStyles.Bold;
                UIFactory.AddLayoutElement(soldText.gameObject, preferredWidth: 130f, preferredHeight: 40f);
            }
            else
            {
                Button buyBtn = UIFactory.CreateButton(rowRT, "BuyBtn_" + i, "PURCHASE", 14f, () => BuyItemFromUI(index));
                UIFactory.AddLayoutElement(buyBtn.gameObject, preferredWidth: 130f, preferredHeight: 40f);
                buyBtn.GetComponent<Image>().sprite = UIFactory.GetRoundedSprite();
                buyBtn.GetComponent<Image>().type = Image.Type.Sliced;

                ColorBlock bcb = buyBtn.colors;
                bcb.normalColor = canAfford
                    ? new Color(ArcaneViolet.r, ArcaneViolet.g, ArcaneViolet.b, 0.35f)
                    : new Color(0.3f, 0.2f, 0.3f, 0.25f);
                bcb.highlightedColor = canAfford
                    ? new Color(ArcaneViolet.r, ArcaneViolet.g, ArcaneViolet.b, 0.65f)
                    : new Color(0.3f, 0.2f, 0.3f, 0.35f);
                buyBtn.colors = bcb;
                buyBtn.interactable = canAfford;
            }
        }
    }

    private void ClearContainer(Transform container)
    {
        var toDestroy = new List<GameObject>();
        for (int i = 0; i < container.childCount; i++)
        {
            toDestroy.Add(container.GetChild(i).gameObject);
        }
        foreach (var child in toDestroy)
        {
            child.transform.SetParent(null, false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
