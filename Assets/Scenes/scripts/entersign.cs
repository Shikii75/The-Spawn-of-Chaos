using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class entersign : MonoBehaviour
{
    [Min(0f)]
    public float animationDuration = 0.15f;

    [Tooltip("The visual graphic GameObject (e.g. 'Press E' sprite) to scale or show. If unassigned, defaults to this GameObject.")]
    public GameObject uiPromptObject;

    [Header("Interaction Settings")]
    [Tooltip("The name of the scene to load when interacting.")]
    public string targetSceneName;

    [Tooltip("The name of the spawn point in the target scene where the player should spawn.")]
    public string targetSpawnPointName;
    
    [Tooltip("The key to press to interact.")]
    public KeyCode interactKey = KeyCode.E;

    private SpriteRenderer spriteRenderer;
    private Transform promptTransform;
    private Vector3 targetScale;
    private Color targetColor;
    private Coroutine animationCoroutine;
    private bool isPlayerInside = false;

    private void Awake()
    {
        // Fallback to this GameObject if no specific visual target is assigned
        if (uiPromptObject == null)
        {
            uiPromptObject = gameObject;
        }

        spriteRenderer = uiPromptObject.GetComponent<SpriteRenderer>();
        promptTransform = uiPromptObject.transform;
        
        targetScale = promptTransform.localScale;
        if (targetScale == Vector3.zero)
        {
            targetScale = Vector3.one;
        }

        if (spriteRenderer != null)
        {
            targetColor = spriteRenderer.color;
            spriteRenderer.sortingOrder = 50; // Guarantee rendering on top of background environment sprites
        }
        else
        {
            targetColor = Color.white;
        }

        // Initialize state as hidden (alpha 0 and scale 0 if safe)
        SetVisualAlpha(0f);
        if (uiPromptObject != gameObject)
        {
            promptTransform.localScale = Vector3.zero;
        }
        SetRenderersEnabled(false);
    }

    private void Update()
    {
        if (NyxarisManager.IsTyping || NyxarisManager.IsChatActive)
        {
            return;
        }

        // If the player is inside and presses the interact key, load the destination scene
        if (isPlayerInside)
        {
            if (Input.GetKeyDown(interactKey))
            {
                if (string.IsNullOrEmpty(targetSceneName))
                {
                    Debug.LogWarning("[entersign] Cannot load scene: Target Scene Name is empty in the Inspector on this GameObject!");
                }
                else
                {
                    Debug.Log($"Interacting! Setting target spawn point to '{targetSpawnPointName}' and loading scene: {targetSceneName}");
                    PlayerSpawnPointManager.targetSpawnPointName = targetSpawnPointName;
                    SceneManager.LoadScene(targetSceneName);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("entersign triggered by: " + other.name + " tag=" + other.tag);
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            AnimatePrompt(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            AnimatePrompt(false);
        }
    }

    private void AnimatePrompt(bool visible)
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        if (!gameObject.activeInHierarchy)
        {
            // If the game object is inactive, we can't start a coroutine on it.
            // Directly apply the target states to ensure consistency.
            SetRenderersEnabled(visible);
            SetVisualAlpha(visible ? (spriteRenderer != null ? targetColor.a : 1f) : 0f);
            if (uiPromptObject != gameObject && promptTransform != null)
            {
                promptTransform.localScale = visible ? targetScale : Vector3.zero;
            }
            return;
        }

        SetRenderersEnabled(true);
        animationCoroutine = StartCoroutine(PlayAppearAnimation(visible));
    }

    private IEnumerator PlayAppearAnimation(bool visible)
    {
        float elapsed = 0f;
        float duration = animationDuration > 0f ? animationDuration : 0.15f;

        Vector3 startScale = promptTransform.localScale;
        float startAlpha = spriteRenderer != null ? spriteRenderer.color.a : 0f;
        
        Vector3 endScale = visible ? targetScale : Vector3.zero;
        float endAlpha = visible ? targetColor.a : 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            // 1. Opacity Fade Animation (Standard for all setups)
            if (spriteRenderer != null)
            {
                SetVisualAlpha(Mathf.Lerp(startAlpha, endAlpha, t));
            }

            // 2. Bounce & Scale Animation (Only run if visual target is separate from trigger collider)
            if (uiPromptObject != gameObject)
            {
                if (visible)
                {
                    // Elastic bounce pop-in effect: overshoot to 1.15x and settle back down
                    if (t < 0.7f)
                    {
                        float subT = t / 0.7f;
                        promptTransform.localScale = Vector3.Lerp(startScale, targetScale * 1.15f, subT);
                    }
                    else
                    {
                        float subT = (t - 0.7f) / 0.3f;
                        promptTransform.localScale = Vector3.Lerp(targetScale * 1.15f, targetScale, subT);
                    }
                }
                else
                {
                    // Smooth scale down to zero on exit
                    promptTransform.localScale = Vector3.Lerp(startScale, endScale, t);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Final clean values assignment
        SetVisualAlpha(endAlpha);
        if (uiPromptObject != gameObject)
        {
            promptTransform.localScale = endScale;
        }

        if (!visible)
        {
            SetRenderersEnabled(false);
        }

        animationCoroutine = null;
    }

    private void SetVisualAlpha(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color col = spriteRenderer.color;
            col.a = alpha;
            spriteRenderer.color = col;
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (uiPromptObject != gameObject)
        {
            uiPromptObject.SetActive(enabled);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.enabled = enabled;
        }
    }
}
