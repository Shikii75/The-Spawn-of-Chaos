using SpawnOfChaos.Weapons;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shop UI that builds its entire Canvas and panel hierarchy from code in Awake().
/// No Inspector references needed for UI — everything is created programmatically via UIFactory.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Header("Shop System Connection")]
    public ShopSystem shopSystem;

    // ── Private UI references (built from code) ──
    private GameObject shopPanel;
    private TextMeshProUGUI currencyText;
    private Transform itemListContainer;

    public bool IsShopActive => shopPanel != null && shopPanel.activeSelf;

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

    void Start()
    {
        if (shopSystem == null)
        {
            shopSystem = FindFirstObjectByType<ShopSystem>();
        }
    }

    void Update()
    {
        if (!IsShopActive) return;

        UpdateCurrencyUI();

        // Escape closes the shop UI
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseShop();
        }
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        // Create dedicated canvas for the shop (sort order 5, above game HUD)
        Canvas canvas = UIFactory.CreateCanvas("ShopCanvas", 5);
        canvas.transform.SetParent(transform, false);

        // Main shop panel — centered, fixed size 600×500
        RectTransform panelRT = UIFactory.CreatePanel(
            canvas.transform, "ShopPanel", UIFactory.PanelBackground,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UIFactory.SetRectFixed(panelRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(660f, 580f));
        shopPanel = panelRT.gameObject;

        // Vertical layout on the panel content
        VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(shopPanel, 6f,
            new RectOffset(25, 25, 20, 20), TextAnchor.UpperCenter);

        // ── Title: SHADOW MERCHANT ──
        TextMeshProUGUI title = UIFactory.CreateText(
            panelRT, "TitleText", "SHADOW MERCHANT",
            28f, UIFactory.Accent, TextAlignmentOptions.Center);
        UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 40f);

        // ── Divider ──
        RectTransform divider = UIFactory.CreateDivider(panelRT, "TitleDivider");
        UIFactory.AddLayoutElement(divider.gameObject, preferredHeight: 2f);

        // ── Currency text ──
        currencyText = UIFactory.CreateText(
            panelRT, "CurrencyText", "Coins: 0",
            20f, UIFactory.TextWhite, TextAlignmentOptions.Center);
        UIFactory.AddLayoutElement(currencyText.gameObject, preferredHeight: 30f);

        // ── Scrollable item list area ──
        // Create a container that will hold dynamically generated item rows
        RectTransform listContainerRT = UIFactory.CreatePanel(
            panelRT, "ItemListContainer", Color.clear,
            Vector2.zero, Vector2.one);
        UIFactory.AddLayoutElement(listContainerRT.gameObject, preferredHeight: 410f);
        UIFactory.AddVerticalLayout(listContainerRT.gameObject, 4f,
            new RectOffset(5, 5, 5, 5), TextAnchor.UpperCenter);
        itemListContainer = listContainerRT;

        // ── Close button (✕) in top-right ──
        UIFactory.CreateCloseButton(panelRT, () => CloseShop());

        // Start hidden
        shopPanel.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────────────

    public void OpenShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
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
        if (currencyText != null)
        {
            int currentCoins = 0;
            // Get currency from the PlayerCurrency singleton or fallback to shopSystem
            PlayerCurrency pc = PlayerCurrency.Instance;
            if (pc != null)
            {
                currentCoins = pc.Coins;
            }
            else if (shopSystem != null)
            {
                currentCoins = shopSystem.playerCurrency;
            }
            currencyText.text = "Coins: " + currentCoins;
        }
    }

    /// <summary>
    /// Rebuilds the item rows from ShopSystem.shopItems[].
    /// Called after purchase to update sold states.
    /// </summary>
    public void RefreshShopItems()
    {
        if (itemListContainer == null || shopSystem == null) return;

        // Clear existing rows
        for (int i = itemListContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(itemListContainer.GetChild(i).gameObject);
        }

        // Build a row for each shop item
        ShopItem[] items = shopSystem.shopItems;
        if (items == null) return;

        for (int i = 0; i < items.Length; i++)
        {
            ShopItem item = items[i];
            int index = i; // capture for closure

            // Row container with horizontal layout
            RectTransform rowRT = UIFactory.CreatePanel(
                itemListContainer, "ItemRow_" + i, UIFactory.ButtonNormal,
                Vector2.zero, Vector2.one);
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 45f);
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(
                rowRT.gameObject, 8f, new RectOffset(12, 12, 5, 5), TextAnchor.MiddleLeft);
            hlg.childControlWidth = false;
            hlg.childForceExpandWidth = false;

            // Item name (left aligned)
            TextMeshProUGUI nameText = UIFactory.CreateText(
                rowRT, "ItemName", item.itemName,
                18f, UIFactory.TextWhite, TextAlignmentOptions.Left);
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 260f, preferredHeight: 35f);

            // Cost text
            TextMeshProUGUI costText = UIFactory.CreateText(
                rowRT, "CostText", item.cost + " coins",
                16f, UIFactory.TextMuted, TextAlignmentOptions.Right);
            UIFactory.AddLayoutElement(costText.gameObject, preferredWidth: 110f, preferredHeight: 35f);

            bool isRepeatable = (item.type == ShopItemType.HealingPotion);

            if (item.purchased && !isRepeatable)
            {
                // Show SOLD label (dimmed) instead of buy button
                TextMeshProUGUI soldText = UIFactory.CreateText(
                    rowRT, "SoldLabel", "SOLD",
                    16f, UIFactory.AccentDim, TextAlignmentOptions.Center);
                UIFactory.AddLayoutElement(soldText.gameObject, preferredWidth: 90f, preferredHeight: 35f);
            }
            else
            {
                // Buy button
                Button buyBtn = UIFactory.CreateButton(rowRT, "BuyBtn_" + i, "BUY", 16f,
                    () => BuyItemFromUI(index));
                UIFactory.AddLayoutElement(buyBtn.gameObject, preferredWidth: 90f, preferredHeight: 35f);
            }
        }

        // ── WEAPON ARSENAL & MASTERY SECTION ──
        if (WeaponManager.Instance != null)
        {
            RectTransform wepDivider = UIFactory.CreateDivider(itemListContainer, "WeaponDivider");
            UIFactory.AddLayoutElement(wepDivider.gameObject, preferredHeight: 2f);

            TextMeshProUGUI wepHeader = UIFactory.CreateText(
                itemListContainer, "WeaponHeader", "WEAPONS & MASTERY",
                20f, UIFactory.Accent, TextAlignmentOptions.Center);
            UIFactory.AddLayoutElement(wepHeader.gameObject, preferredHeight: 30f);

            foreach (var wep in WeaponManager.Instance.AllWeapons)
            {
                var capturedWep = wep;
                bool isUnlocked = WeaponManager.Instance.IsUnlocked(capturedWep.id);
                bool isEquipped = (WeaponManager.Instance.ActiveWeapon == capturedWep.id);
                int tier = WeaponManager.Instance.GetTier(capturedWep.id);

                RectTransform wRowRT = UIFactory.CreatePanel(
                    itemListContainer, "WepRow_" + capturedWep.id, UIFactory.ButtonNormal,
                    Vector2.zero, Vector2.one);
                UIFactory.AddLayoutElement(wRowRT.gameObject, preferredHeight: 50f);
                HorizontalLayoutGroup wHlg = UIFactory.AddHorizontalLayout(
                    wRowRT.gameObject, 8f, new RectOffset(10, 10, 5, 5), TextAnchor.MiddleLeft);
                wHlg.childControlWidth = false;
                wHlg.childForceExpandWidth = false;

                // Weapon Name + Archetype
                string tierLabel = isUnlocked ? $" [T{tier}]" : "";
                TextMeshProUGUI wName = UIFactory.CreateText(
                    wRowRT, "WName", $"{capturedWep.displayName}{tierLabel}",
                    16f, UIFactory.TextWhite, TextAlignmentOptions.Left);
                UIFactory.AddLayoutElement(wName.gameObject, preferredWidth: 260f, preferredHeight: 35f);

                if (!isUnlocked)
                {
                    // Cost & Buy button
                    TextMeshProUGUI wCost = UIFactory.CreateText(
                        wRowRT, "WCost", $"{capturedWep.unlockCost} coins",
                        15f, UIFactory.TextMuted, TextAlignmentOptions.Right);
                    UIFactory.AddLayoutElement(wCost.gameObject, preferredWidth: 90f, preferredHeight: 35f);

                    Button buyBtn = UIFactory.CreateButton(wRowRT, "BuyWepBtn", "UNLOCK", 15f, () =>
                    {
                        if (PlayerCurrency.Instance != null && PlayerCurrency.Instance.Coins >= capturedWep.unlockCost)
                        {
                            PlayerCurrency.Instance.SpendCoins(capturedWep.unlockCost);
                            WeaponManager.Instance.UnlockWeapon(capturedWep.id);
                            WeaponManager.Instance.EquipWeapon(capturedWep.id);
                            UpdateCurrencyUI();
                            RefreshShopItems();
                        }
                    });
                    UIFactory.AddLayoutElement(buyBtn.gameObject, preferredWidth: 90f, preferredHeight: 35f);
                }
                else
                {
                    // Equip button / status
                    if (isEquipped)
                    {
                        TextMeshProUGUI eqLabel = UIFactory.CreateText(
                            wRowRT, "EqLabel", "EQUIPPED",
                            15f, new Color(0.15f, 0.9f, 1f), TextAlignmentOptions.Center);
                        UIFactory.AddLayoutElement(eqLabel.gameObject, preferredWidth: 90f, preferredHeight: 35f);
                    }
                    else
                    {
                        Button eqBtn = UIFactory.CreateButton(wRowRT, "EqBtn", "EQUIP", 15f, () =>
                        {
                            WeaponManager.Instance.EquipWeapon(capturedWep.id);
                            RefreshShopItems();
                        });
                        UIFactory.AddLayoutElement(eqBtn.gameObject, preferredWidth: 90f, preferredHeight: 35f);
                    }

                    // Upgrade Mastery button
                    if (tier < 3)
                    {
                        int upgradeCost = tier == 1 ? capturedWep.tier2Cost : capturedWep.tier3Cost;
                        Button upgBtn = UIFactory.CreateButton(wRowRT, "UpgBtn", $"UPG ({upgradeCost}c)", 13f, () =>
                        {
                            if (PlayerCurrency.Instance != null && PlayerCurrency.Instance.Coins >= upgradeCost)
                            {
                                PlayerCurrency.Instance.SpendCoins(upgradeCost);
                                WeaponManager.Instance.UpgradeWeapon(capturedWep.id);
                                UpdateCurrencyUI();
                                RefreshShopItems();
                            }
                        });
                        UIFactory.AddLayoutElement(upgBtn.gameObject, preferredWidth: 95f, preferredHeight: 35f);
                    }
                    else
                    {
                        TextMeshProUGUI maxLabel = UIFactory.CreateText(
                            wRowRT, "MaxLabel", "MAX TIER",
                            14f, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
                        UIFactory.AddLayoutElement(maxLabel.gameObject, preferredWidth: 95f, preferredHeight: 35f);
                    }
                }
            }
        }
    }
}
