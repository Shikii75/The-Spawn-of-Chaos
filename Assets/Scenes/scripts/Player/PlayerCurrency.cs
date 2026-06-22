using System;
using UnityEngine;

public class PlayerCurrency : MonoBehaviour
{
    public static PlayerCurrency Instance { get; private set; }

    [Header("Currency Data")]
    [SerializeField] private int coins = 0;
    [SerializeField] private int healingPotions = 0;

    public int Coins => coins;
    public int HealingPotions => healingPotions;

    public event Action<int> onCoinsChanged;
    public event Action<int> onPotionsChanged;

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
    }

    public void AddCoins(int amount)
    {
        if (amount < 0) return;
        coins += amount;
        onCoinsChanged?.Invoke(coins);
        Debug.Log("Player gained " + amount + " coins. Total: " + coins);
    }

    public bool SpendCoins(int amount)
    {
        if (amount < 0) return false;
        if (coins >= amount)
        {
            coins -= amount;
            onCoinsChanged?.Invoke(coins);
            Debug.Log("Player spent " + amount + " coins. Total: " + coins);
            return true;
        }
        return false;
    }

    public void AddPotions(int count)
    {
        if (count < 0) return;
        healingPotions += count;
        onPotionsChanged?.Invoke(healingPotions);
        Debug.Log("Player gained " + count + " healing potions. Total: " + healingPotions);
    }

    public bool UsePotion()
    {
        if (healingPotions > 0)
        {
            Health playerHealth = GetComponent<Health>();
            if (playerHealth != null)
            {
                // If already at full health, we might want to prevent use or just consume it. Let's heal 40 health points.
                if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
                {
                    Debug.Log("Player is already at full health!");
                    return false;
                }
                
                healingPotions--;
                onPotionsChanged?.Invoke(healingPotions);
                playerHealth.Heal(4);
                Debug.Log("Player consumed a potion. Remaining: " + healingPotions);
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        // Consuming potion on H key press
        if (Input.GetKeyDown(KeyCode.H))
        {
            // Ignore key while typing in any input fields
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                var go = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (go.GetComponent<TMPro.TMP_InputField>() != null || go.GetComponent<UnityEngine.UI.InputField>() != null)
                {
                    return;
                }
            }

            // Ignore key if dialogue, shop, or chat panels are open
            if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return;
            if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return;
            if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf) return;

            UsePotion();
        }
    }
}
