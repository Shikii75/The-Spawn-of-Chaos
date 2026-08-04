using System;
using System.Collections.Generic;
using UnityEngine;

public enum OrbHealMode
{
    FullHealAllMP,    // Restores major HP, empties 100% MP (requires min 20 MP)
    ProportionalMP,   // Converts current MP to proportional HP, empties MP
    EmergencyAuto     // Automatically triggers heal when HP <= 20%
}

[System.Serializable]
public class OrbItem
{
    public string id;
    public string name;
    public string description;
    public int count;
    public int maxStack = 99;
    public Color itemColor = Color.cyan;

    public OrbItem(string id, string name, string description, int count, Color itemColor)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        this.count = count;
        this.itemColor = itemColor;
    }
}

/// <summary>
/// Inventory and settings storage backend for Lumi the Orb of Light companion.
/// </summary>
public class OrbInventorySystem : MonoBehaviour
{
    public static OrbInventorySystem Instance { get; private set; }

    public int totalSlots = 20;
    public List<OrbItem> items = new List<OrbItem>();

    public OrbHealMode activeHealMode = OrbHealMode.FullHealAllMP;
    public bool isCaveLightEnabled = true;

    public event Action OnInventoryUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeStarterItems();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeStarterItems()
    {
        items.Clear();

        // Populate with starter items
        items.Add(new OrbItem("pot_hp", "Health Elixir", "Restores 4 HP units when consumed.", 3, new Color(220f/255f, 50f/255f, 50f/255f)));
        items.Add(new OrbItem("pot_mp", "Arcane Flask", "Instantly refills 50 MP.", 2, new Color(80f/255f, 100f/255f, 240f/255f)));
        items.Add(new OrbItem("cryst_light", "Starlight Core", "Glowing crystal harvested from Nyxaris shrines.", 5, new Color(255f/255f, 220f/255f, 100f/255f)));
        items.Add(new OrbItem("spear_dust", "Spear Essence", "Dust used to forge radiant light spears.", 12, new Color(240f/255f, 180f/255f, 40f/255f)));
        items.Add(new OrbItem("ancient_coin", "Gold Token", "Currency used in dojo shops.", 50, new Color(255f/255f, 200f/255f, 0f/255f)));

        // Fill remaining slots as empty
        while (items.Count < totalSlots)
        {
            items.Add(null);
        }
    }

    public bool AddItem(OrbItem newItem)
    {
        // Try stacking
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].id == newItem.id && items[i].count < items[i].maxStack)
            {
                items[i].count += newItem.count;
                OnInventoryUpdated?.Invoke();
                return true;
            }
        }

        // Place in first empty slot
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
            {
                items[i] = newItem;
                OnInventoryUpdated?.Invoke();
                return true;
            }
        }

        Debug.LogWarning("OrbInventorySystem: Storage is full!");
        return false;
    }

    public void RemoveItem(int slotIndex, int count = 1)
    {
        if (slotIndex >= 0 && slotIndex < items.Count && items[slotIndex] != null)
        {
            items[slotIndex].count -= count;
            if (items[slotIndex].count <= 0)
            {
                items[slotIndex] = null;
            }
            OnInventoryUpdated?.Invoke();
        }
    }

    public void CycleHealMode()
    {
        int nextMode = ((int)activeHealMode + 1) % Enum.GetNames(typeof(OrbHealMode)).Length;
        activeHealMode = (OrbHealMode)nextMode;
        Debug.Log($"OrbInventorySystem: Switched Heal Mode to {activeHealMode}");
        OnInventoryUpdated?.Invoke();
    }
}
