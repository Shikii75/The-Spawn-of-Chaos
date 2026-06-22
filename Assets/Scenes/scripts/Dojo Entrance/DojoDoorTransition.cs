using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DojoDoorTransition : MonoBehaviour
{
    [Header("Door")]
    public string doorName = "Clan Dojo";
    public Transform destinationPoint;
    public bool requiresDojoCleared;
    public DojoWaveManager requiredDojoWave;

    [Header("Prompt")]
    public GameObject interactPrompt;
    public string availablePromptText = "Press E to enter";
    public string lockedPromptText = "Clear the dojo first";
    public bool createPromptIfMissing = true;
    public Vector3 promptOffset = new Vector3(0f, 1.4f, 0f);
    public float promptBobAmount = 0.08f;
    public float promptBobSpeed = 3f;

    [Header("Transition")]
    public float preFadeDelay = 0.35f;
    public float fadeOutDuration = 0.35f;
    public float hiddenHoldDuration = 0.2f;
    public float fadeInDuration = 0.35f;
    public string turnInTrigger = "TurnIn";
    public string arriveTrigger = "ArriveFromDoor";
    public AudioClip transitionSFX;

    [Header("Area Switching")]
    public GameObject[] enableOnArrival;
    public GameObject[] disableOnArrival;

    private bool playerInRange;
    private bool isTransitioning;
    private GameObject playerObject;
    private Vector3 promptStartLocalPosition;

    private void Awake()
    {
        Collider2D doorCollider = GetComponent<Collider2D>();
        doorCollider.isTrigger = true;

        if (interactPrompt == null && createPromptIfMissing)
        {
            interactPrompt = CreateRuntimePrompt();
        }

        if (interactPrompt != null)
        {
            promptStartLocalPosition = interactPrompt.transform.localPosition;
            interactPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        AnimatePrompt();

        if (!playerInRange || isTransitioning || playerObject == null)
        {
            return;
        }

        RefreshPromptText();

        if (Input.GetKeyDown(KeyCode.E) && !IsPlayerUiBlocked())
        {
            if (IsLocked())
            {
                return;
            }

            StartCoroutine(EnterDoorRoutine(playerObject));
        }
    }

    private IEnumerator EnterDoorRoutine(GameObject player)
    {
        isTransitioning = true;
        SetPromptVisible(false);
        SetPlayerControl(player, false);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Animator animator = player.GetComponent<Animator>();
        SetAnimatorTriggerIfPresent(animator, turnInTrigger);

        if (transitionSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(transitionSFX);
        }

        if (preFadeDelay > 0f)
        {
            yield return new WaitForSeconds(preFadeDelay);
        }

        DojoTransitionFader fader = DojoTransitionFader.Ensure();
        fader.PlayFadeOutIn(fadeOutDuration, hiddenHoldDuration, fadeInDuration, () =>
        {
            SwitchAreaObjects();

            if (destinationPoint != null)
            {
                player.transform.position = destinationPoint.position;
            }
            else
            {
                Debug.LogWarning($"{name}: destinationPoint is not assigned for {doorName}.");
            }
        }, () =>
        {
            SetAnimatorTriggerIfPresent(animator, arriveTrigger);
            SetPlayerControl(player, true);
            isTransitioning = false;
        });

        yield break;
    }

    private GameObject CreateRuntimePrompt()
    {
        GameObject prompt = new GameObject("DojoInteractPrompt");
        prompt.transform.SetParent(transform, false);
        prompt.transform.localPosition = promptOffset;

        TextMeshPro text = prompt.AddComponent<TextMeshPro>();
        text.text = availablePromptText;
        text.fontSize = 3.2f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;

        MeshRenderer renderer = prompt.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 50;
        }

        return prompt;
    }

    private void AnimatePrompt()
    {
        if (interactPrompt == null || !interactPrompt.activeSelf)
        {
            return;
        }

        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * promptBobSpeed) * promptBobAmount);
        interactPrompt.transform.localPosition = promptStartLocalPosition + bob;
    }

    private void RefreshPromptText()
    {
        if (interactPrompt == null)
        {
            return;
        }

        string promptText = IsLocked() ? lockedPromptText : availablePromptText;
        TextMeshPro text = interactPrompt.GetComponent<TextMeshPro>();
        if (text != null)
        {
            text.text = promptText;
        }
    }

    private void SwitchAreaObjects()
    {
        SetObjectsActive(enableOnArrival, true);
        SetObjectsActive(disableOnArrival, false);
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }
    }

    private void SetPlayerControl(GameObject player, bool enabled)
    {
        move movement = player.GetComponent<move>();
        if (movement != null)
        {
            movement.enabled = enabled;
        }

        MageCombat combat = player.GetComponent<MageCombat>();
        if (combat != null)
        {
            combat.enabled = enabled;
        }
    }

    private bool IsLocked()
    {
        return requiresDojoCleared && requiredDojoWave != null && !requiredDojoWave.IsChallengeCompleted;
    }

    private bool IsPlayerUiBlocked()
    {
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return true;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return true;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return true;
        if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf) return true;
        return false;
    }

    private void SetAnimatorTriggerIfPresent(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                animator.SetTrigger(triggerName);
                return;
            }
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(visible);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerInRange = true;
        playerObject = other.gameObject;
        RefreshPromptText();
        SetPromptVisible(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerInRange = false;
        playerObject = null;
        SetPromptVisible(false);
    }
}
