using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class InspectableDojoObject : MonoBehaviour
{
    [Header("Inspect Info")]
    [Tooltip("The title/name of the object shown in the dialogue badge.")]
    public string objectName = "Ancient Scroll";
    
    [TextArea(2, 5)]
    [Tooltip("The dialogue text lines displayed in the dialogue window.")]
    public string[] loreLines = new string[] {
        "The parchment reads: 'He who seeks the gravity bounds must first still their inner chaos...'"
    };

    [Header("Prompt Visibility")]
    [Tooltip("Optional reference to an E-prompt GameObject (like press E) that appears when in range.")]
    public GameObject promptVisual;

    private bool isPlayerInRange = false;

    private void Awake()
    {
        // Ensure prompt starts hidden
        if (promptVisual != null)
        {
            promptVisual.SetActive(false);
        }
        
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            // Trigger the global dialogue box
            if (NPCDialogueUI.Instance != null && !NPCDialogueUI.Instance.IsDialogueActive)
            {
                // Hide prompt while reading
                if (promptVisual != null) promptVisual.SetActive(false);
                
                NPCDialogueUI.Instance.ShowDialogue(objectName, loreLines, () => {
                    // Reshow prompt when dialogue finishes (if player is still in range)
                    if (isPlayerInRange && promptVisual != null)
                    {
                        promptVisual.SetActive(true);
                    }
                });
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (promptVisual != null && (NPCDialogueUI.Instance == null || !NPCDialogueUI.Instance.IsDialogueActive))
            {
                promptVisual.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            if (promptVisual != null)
            {
                promptVisual.SetActive(false);
            }

            // Optional: Close dialogue if player walks away
            if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive)
            {
                // Verify if this is the active dialogue
                // For simplicity, close dialogue box
                NPCDialogueUI.Instance.CloseDialogue();
            }
        }
    }
}
