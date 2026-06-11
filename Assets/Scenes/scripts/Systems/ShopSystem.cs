using UnityEngine;

public class ShopSystem : MonoBehaviour
{
    public int playerCurrency = 0;
    public ShopItem[] shopItems;

    public void BuyItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= shopItems.Length)
            return;

        ShopItem item = shopItems[itemIndex];
        if (playerCurrency < item.cost || item.purchased)
            return;

        playerCurrency -= item.cost;
        item.purchased = true;
        ApplyPurchase(item);
    }

    private void ApplyPurchase(ShopItem item)
    {
        switch (item.type)
        {
            case ShopItemType.HealthBoost:
                // Hook into player health system.
                break;
            case ShopItemType.DashUpgrade:
                // Hook into player movement system.
                break;
            case ShopItemType.ProjectileUnlock:
                // Hook into player attack system.
                break;
        }
    }
}

[System.Serializable]
public class ShopItem
{
    public string itemName;
    public ShopItemType type;
    public int cost;
    public bool purchased;
}

public enum ShopItemType
{
    HealthBoost,
    DashUpgrade,
    ProjectileUnlock
}
