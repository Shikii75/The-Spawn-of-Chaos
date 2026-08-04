using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Code-built Glassmorphism UI panel for Lumi's 20-Slot Dimensional Storage Inventory & Settings.
/// Created programmatically via UIFactory to maintain project styling standards with 0 inspector setup required.
/// </summary>
public class OrbInventoryUI : MonoBehaviour
{
    public static OrbInventoryUI Instance { get; private set; }

    public bool IsInventoryOpen => inventoryPanel != null && inventoryPanel.activeSelf;

    private Canvas canvas;
    private GameObject inventoryPanel;
    private Transform gridContainer;
    private TextMeshProUGUI itemTitleText;
    private TextMeshProUGUI itemDescText;
    private TextMeshProUGUI healModeBtnText;

    private int selectedSlotIndex = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (OrbInventorySystem.Instance != null)
        {
            OrbInventorySystem.Instance.OnInventoryUpdated += RefreshUI;
        }
    }

    private void OnDestroy()
    {
        if (OrbInventorySystem.Instance != null)
        {
            OrbInventorySystem.Instance.OnInventoryUpdated -= RefreshUI;
        }
    }

    private void Update()
    {
        // Toggle Inventory with 'I' Key or Escape to Close
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && IsInventoryOpen)
        {
            CloseInventory();
        }
    }

    private void BuildUI()
    {
        canvas = UIFactory.CreateCanvas("OrbInventoryCanvas", 8);
        DontDestroyOnLoad(canvas.gameObject);

        // Fullscreen translucent backdrop
        RectTransform backdropRT = UIFactory.CreateFullScreenPanel(canvas.transform, "Backdrop", UIFactory.OverlayDark);

        // Main Glass Panel
        RectTransform mainPanelRT = UIFactory.CreatePanel(
            backdropRT, "MainPanel", UIFactory.PanelBackground,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-420f, -300f), new Vector2(420f, 300f));

        inventoryPanel = backdropRT.gameObject;

        // Border Glow
        RectTransform borderRT = UIFactory.CreatePanel(
            mainPanelRT, "BorderGlow", UIFactory.BorderColor,
            Vector2.zero, Vector2.one, new Vector2(-2, -2), new Vector2(2, 2));
        borderRT.SetAsFirstSibling();

        // Main Layout (Vertical)
        VerticalLayoutGroup mainVlg = UIFactory.AddVerticalLayout(mainPanelRT.gameObject, 10f, new RectOffset(20, 20, 16, 16));

        // Header Title
        TextMeshProUGUI title = UIFactory.CreateText(
            mainPanelRT, "HeaderTitle", "LUMI'S DIMENSIONAL STORAGE",
            26f, UIFactory.Accent, TextAlignmentOptions.Center);
        UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 35f);

        // Divider
        RectTransform divider = UIFactory.CreateDivider(mainPanelRT, "Divider");
        UIFactory.AddLayoutElement(divider.gameObject, preferredHeight: 2f);

        // Content Area (Horizontal: Left = Grid, Right = Tooltip & Settings)
        RectTransform contentRT = UIFactory.CreatePanel(mainPanelRT, "ContentArea", Color.clear, Vector2.zero, Vector2.one);
        UIFactory.AddLayoutElement(contentRT.gameObject, preferredHeight: 420f);
        HorizontalLayoutGroup contentHlg = UIFactory.AddHorizontalLayout(contentRT.gameObject, 15f);

        // Left Container: 20 Slot Grid (4 rows x 5 cols)
        RectTransform gridPanelRT = UIFactory.CreatePanel(contentRT, "GridPanel", new Color(0.05f, 0.04f, 0.1f, 0.5f), Vector2.zero, Vector2.one);
        UIFactory.AddLayoutElement(gridPanelRT.gameObject, preferredWidth: 480f, preferredHeight: 420f);
        GridLayoutGroup gridGroup = gridPanelRT.gameObject.AddComponent<GridLayoutGroup>();
        gridGroup.cellSize = new Vector2(80f, 80f);
        gridGroup.spacing = new Vector2(10f, 10f);
        gridGroup.padding = new RectOffset(15, 15, 15, 15);
        gridGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridGroup.constraintCount = 5;
        gridContainer = gridPanelRT;

        // Right Container: Item Tooltip & Orb Settings Panel
        RectTransform infoPanelRT = UIFactory.CreatePanel(contentRT, "InfoPanel", new Color(0.08f, 0.06f, 0.15f, 0.6f), Vector2.zero, Vector2.one);
        UIFactory.AddLayoutElement(infoPanelRT.gameObject, preferredWidth: 280f, preferredHeight: 420f);
        VerticalLayoutGroup infoVlg = UIFactory.AddVerticalLayout(infoPanelRT.gameObject, 12f, new RectOffset(14, 14, 14, 14));

        // Item Title
        itemTitleText = UIFactory.CreateText(infoPanelRT, "ItemTitle", "Select an Item", 20f, UIFactory.TextWhite, TextAlignmentOptions.Left);
        UIFactory.AddLayoutElement(itemTitleText.gameObject, preferredHeight: 30f);

        // Item Description
        itemDescText = UIFactory.CreateText(infoPanelRT, "ItemDesc", "Click any slot in Lumi's storage to view item details and options.", 14f, UIFactory.TextMuted, TextAlignmentOptions.TopLeft);
        UIFactory.AddLayoutElement(itemDescText.gameObject, preferredHeight: 180f);

        // Divider
        RectTransform settingsDivider = UIFactory.CreateDivider(infoPanelRT, "SettingsDivider");
        UIFactory.AddLayoutElement(settingsDivider.gameObject, preferredHeight: 2f);

        // Settings Label
        TextMeshProUGUI settingsLabel = UIFactory.CreateText(infoPanelRT, "SettingsLabel", "ORB SETTINGS", 16f, UIFactory.Accent, TextAlignmentOptions.Left);
        UIFactory.AddLayoutElement(settingsLabel.gameObject, preferredHeight: 25f);

        // Heal Mode Toggle Button
        Button healModeBtn = UIFactory.CreateButton(infoPanelRT, "HealModeBtn", "HEAL: FULL HP (100% MP)", 14f, () =>
        {
            if (OrbInventorySystem.Instance != null)
            {
                OrbInventorySystem.Instance.CycleHealMode();
                UpdateSettingsUI();
            }
        });
        UIFactory.AddLayoutElement(healModeBtn.gameObject, preferredHeight: 40f);
        healModeBtnText = healModeBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Close Button (Top-Right)
        UIFactory.CreateCloseButton(mainPanelRT, () => CloseInventory());

        inventoryPanel.SetActive(false);
    }

    public void ToggleInventory()
    {
        if (IsInventoryOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            RefreshUI();
            UpdateSettingsUI();
        }
    }

    public void CloseInventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    private void RefreshUI()
    {
        if (gridContainer == null || OrbInventorySystem.Instance == null) return;

        // Clear existing slots
        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }

        // Rebuild 20 slot cards
        for (int i = 0; i < OrbInventorySystem.Instance.totalSlots; i++)
        {
            int index = i;
            OrbItem item = (i < OrbInventorySystem.Instance.items.Count) ? OrbInventorySystem.Instance.items[i] : null;

            Color slotBg = (index == selectedSlotIndex) ? UIFactory.ButtonHighlight : UIFactory.ButtonNormal;
            RectTransform slotRT = UIFactory.CreatePanel(gridContainer, $"Slot_{index}", slotBg, Vector2.zero, Vector2.one);

            Button btn = slotRT.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(index));

            if (item != null)
            {
                // Item Icon Visual Box
                RectTransform iconRT = UIFactory.CreatePanel(slotRT, "Icon", item.itemColor, new Vector2(0.15f, 0.25f), new Vector2(0.85f, 0.85f));

                // Stack Count Badge
                if (item.count > 1)
                {
                    TextMeshProUGUI countText = UIFactory.CreateText(slotRT, "Count", item.count.ToString(), 12f, UIFactory.TextWhite, TextAlignmentOptions.BottomRight);
                    countText.rectTransform.anchorMin = Vector2.zero;
                    countText.rectTransform.anchorMax = Vector2.one;
                    countText.rectTransform.offsetMin = new Vector2(4, 4);
                    countText.rectTransform.offsetMax = new Vector2(-4, -4);
                }
            }
        }
    }

    private void OnSlotClicked(int index)
    {
        selectedSlotIndex = index;
        RefreshUI();

        if (OrbInventorySystem.Instance != null && index >= 0 && index < OrbInventorySystem.Instance.items.Count)
        {
            OrbItem item = OrbInventorySystem.Instance.items[index];
            if (item != null)
            {
                itemTitleText.text = item.name.ToUpper();
                itemDescText.text = $"{item.description}\n\nQuantity: {item.count}/{item.maxStack}";
            }
            else
            {
                itemTitleText.text = "EMPTY SLOT";
                itemDescText.text = "This storage slot is currently empty.";
            }
        }
    }

    private void UpdateSettingsUI()
    {
        if (OrbInventorySystem.Instance == null || healModeBtnText == null) return;

        switch (OrbInventorySystem.Instance.activeHealMode)
        {
            case OrbHealMode.FullHealAllMP:
                healModeBtnText.text = "HEAL: FULL HP (100% MP)";
                break;
            case OrbHealMode.ProportionalMP:
                healModeBtnText.text = "HEAL: PROPORTIONAL MP";
                break;
            case OrbHealMode.EmergencyAuto:
                healModeBtnText.text = "HEAL: AUTO EMERGENCY";
                break;
        }
    }
}
