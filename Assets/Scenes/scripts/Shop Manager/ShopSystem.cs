using UnityEngine;

public class ShopSystem : MonoBehaviour
{
    public ShopItem[] shopItems;

    // Keep this field for fallback, but redirect to PlayerCurrency if available
    public int playerCurrency
    {
        get
        {
            if (PlayerCurrency.Instance != null)
                return PlayerCurrency.Instance.Coins;
            return fallbackCurrency;
        }
        set
        {
            if (PlayerCurrency.Instance != null)
            {
                // To allow setting via code if needed
                int diff = value - PlayerCurrency.Instance.Coins;
                if (diff > 0) PlayerCurrency.Instance.AddCoins(diff);
                else if (diff < 0) PlayerCurrency.Instance.SpendCoins(-diff);
            }
            else
            {
                fallbackCurrency = value;
            }
        }
    }
    private int fallbackCurrency = 0;

    public void BuyItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= shopItems.Length)
            return;

        ShopItem item = shopItems[itemIndex];
        
        // POTIONS are repeatable; other upgrades are one-time
        bool isRepeatable = (item.type == ShopItemType.HealingPotion);
        
        if (item.purchased && !isRepeatable)
        {
            Debug.Log("Item already purchased: " + item.itemName);
            return;
        }

        // Validate Currency
        if (PlayerCurrency.Instance != null)
        {
            if (PlayerCurrency.Instance.Coins < item.cost)
            {
                Debug.Log("Not enough coins to buy: " + item.itemName);
                return;
            }
            
            PlayerCurrency.Instance.SpendCoins(item.cost);
        }
        else
        {
            if (fallbackCurrency < item.cost)
            {
                Debug.Log("Not enough coins to buy: " + item.itemName);
                return;
            }
            fallbackCurrency -= item.cost;
        }

        if (!isRepeatable)
        {
            item.purchased = true;
        }

        ApplyPurchase(item);
    }

    private void ApplyPurchase(ShopItem item)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("ShopSystem: Player GameObject not found in scene!");
            return;
        }

        switch (item.type)
        {
            case ShopItemType.HealthBoost:
                Health playerHealth = player.GetComponent<Health>();
                if (playerHealth != null)
                {
                    playerHealth.IncreaseMaxHealth(20);
                }
                break;

            case ShopItemType.DashUpgrade:
                move playerMove = player.GetComponent<move>();
                if (playerMove != null)
                {
                    playerMove.UpgradeDash();
                }
                break;

            case ShopItemType.ProjectileUnlock:
                MageCombat playerCombat = player.GetComponent<MageCombat>();
                if (playerCombat != null)
                {
                    playerCombat.UnlockProjectile();
                }
                break;

            case ShopItemType.HealingPotion:
                PlayerCurrency pc = player.GetComponent<PlayerCurrency>();
                if (pc != null)
                {
                    pc.AddPotions(1);
                }
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
    ProjectileUnlock,
    HealingPotion
}
