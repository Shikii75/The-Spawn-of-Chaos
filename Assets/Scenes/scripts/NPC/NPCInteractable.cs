using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NPCInteractable : MonoBehaviour
{
    [Header("NPC Configuration")]
    public string npcName = "Villager";
    [TextArea(3, 5)]
    public string[] dialogueLines;

    [Header("Special Triggers")]
    public bool isNyxaris = false;
    public bool isShopKeeper = false;

    [Header("UI Prompts")]
    [Tooltip("Visual prompt GameObject like 'Press E to talk' (optional)")]
    public GameObject interactPrompt;

    private bool playerInRange = false;

    void Awake()
    {
        // Force the Collider2D to be a trigger
        GetComponent<Collider2D>().isTrigger = true;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }
    }

    void Update()
    {
        if (playerInRange)
        {
            // Check if player presses interaction key E
            if (Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
        }
    }

    private void Interact()
    {
        // Hide interaction prompt while talking
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (isNyxaris)
        {
            if (NyxarisManager.Instance != null)
            {
                NyxarisManager.Instance.ShowInterface();
            }
            else
            {
                Debug.LogWarning("NyxarisManager not found in scene!");
            }
        }
        else if (isShopKeeper)
        {
            // First display greeting, then open shop
            if (dialogueLines != null && dialogueLines.Length > 0 && NPCDialogueUI.Instance != null)
            {
                NPCDialogueUI.Instance.ShowDialogue(npcName, dialogueLines, () =>
                {
                    if (ShopUI.Instance != null)
                    {
                        ShopUI.Instance.OpenShop();
                    }
                });
            }
            else if (ShopUI.Instance != null)
            {
                ShopUI.Instance.OpenShop();
            }
        }
        else
        {
            // Normal dialogue NPC
            if (NPCDialogueUI.Instance != null)
            {
                NPCDialogueUI.Instance.ShowDialogue(npcName, dialogueLines, () =>
                {
                    // Dialogue complete callback - show prompt again if player is still in range
                    if (playerInRange && interactPrompt != null)
                    {
                        interactPrompt.SetActive(true);
                    }
                });
            }
            else
            {
                Debug.LogWarning("NPCDialogueUI not found in scene!");
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactPrompt != null && !IsAnyDialogueOrShopOpen())
            {
                interactPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false);
            }

            // Automatically close dialogue if player walks away
            if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive)
            {
                NPCDialogueUI.Instance.CloseDialogue();
            }

            // Automatically close shop if player walks away
            if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive)
            {
                ShopUI.Instance.CloseShop();
            }
        }
    }

    private bool IsAnyDialogueOrShopOpen()
    {
        bool dialogueActive = (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive);
        bool shopActive = (ShopUI.Instance != null && ShopUI.Instance.IsShopActive);
        bool nyxarisActive = (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf);
        return dialogueActive || shopActive || nyxarisActive;
    }
}
