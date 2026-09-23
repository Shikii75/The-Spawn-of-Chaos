using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Tooltip("If true, automatically triggers the transition as soon as the player steps into the trigger volume (no keypress required).")]
    public bool autoTriggerOnWalkIn = false;

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
        if (isPlayerInside && !autoTriggerOnWalkIn)
        {
            if (Input.GetKeyDown(interactKey))
            {
                TriggerSceneTransition();
            }
        }
    }

    private bool CanTransition(out string reason)
    {
        reason = null;

        // Check Dojo 1 Re-entry Lock: Completed trials cannot be re-entered
        if ((targetSceneName == "Dojo1Scene" || targetSceneName == "Assets/Scenes/Dojo1Scene.unity") &&
            PlayerPrefs.GetInt("Dojo1_Completed", 0) == 1)
        {
            reason = "Dojo Cleared (The Strawhat Clan acknowledges your strength)";
            return false;
        }

        // Check Dojo 1 Combat Lock: Cannot leave Dojo 1 while challenge is active
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene.Contains("Dojo1") && (targetSceneName == "SampleScene" || targetSceneName.Contains("SampleScene")))
        {
            if (DojoWaveManager.Instance != null && !DojoWaveManager.Instance.IsChallengeCompleted)
            {
                reason = "The Dojo doors are sealed until the trial is complete!";
                return false;
            }
        }

        return true;
    }

    private void ShowFloatingNotice(string msg, Color col)
    {
        Debug.Log($"[entersign] Notice: {msg}");
        GameObject txtGO = new GameObject("EnterSignNotice");
        txtGO.transform.position = transform.position + Vector3.up * 1.5f;
        TextMeshPro tmp = txtGO.AddComponent<TextMeshPro>();
        tmp.text = msg;
        tmp.fontSize = 4.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = col;
        tmp.sortingOrder = 60;
        StartCoroutine(AnimateNotice(txtGO, 2.0f));
    }

    private IEnumerator AnimateNotice(GameObject go, float duration)
    {
        if (go == null) yield break;
        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        Color startCol = tmp != null ? tmp.color : Color.white;
        Vector3 startPos = go.transform.position;
        float elapsed = 0f;

        while (elapsed < duration && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            go.transform.position = startPos + Vector3.up * (t * 0.8f);
            if (tmp != null)
            {
                tmp.color = new Color(startCol.r, startCol.g, startCol.b, 1f - t);
            }
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    public void TriggerSceneTransition()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[entersign] Cannot load scene: Target Scene Name is empty in the Inspector on this GameObject!");
            return;
        }

        if (!CanTransition(out string reason))
        {
            ShowFloatingNotice(reason, Color.yellow);
            return;
        }

        Debug.Log($"[entersign] Transitioning to '{targetSceneName}' at spawn point '{targetSpawnPointName}'");
        PlayerSpawnPointManager.targetSpawnPointName = targetSpawnPointName;
        PlayerSpawnPointManager.lastUsedSpawnPointName = targetSpawnPointName;

        // If target scene is current active scene, teleport player directly without reloading scene
        if (targetSceneName == SceneManager.GetActiveScene().name)
        {
            TeleportPlayerSameScene(targetSpawnPointName);
        }
        else
        {
            SpawnOfChaos.Systems.ArcaneLoadingScreen.LoadScene(targetSceneName);
        }
    }

    private void TeleportPlayerSameScene(string spawnName)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject spawnPoint = !string.IsNullOrEmpty(spawnName) ? GameObject.Find(spawnName) : null;

        if (player != null && spawnPoint != null)
        {
            player.transform.position = spawnPoint.transform.position;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Debug.Log($"[entersign] Teleported '{player.name}' to '{spawnPoint.name}' at position {spawnPoint.transform.position}");
        }

        // Trigger Dojo 2 Wave Manager challenge if inside Dojo 2
        Dojo2WaveManager dojo2Manager = Object.FindFirstObjectByType<Dojo2WaveManager>();
        if (dojo2Manager != null && !dojo2Manager.IsChallengeStarted)
        {
            dojo2Manager.StartChallenge();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("entersign triggered by: " + other.name + " tag=" + other.tag);
        if (other.CompareTag("Player"))
        {
            if (!CanTransition(out string reason))
            {
                ShowFloatingNotice(reason, Color.yellow);
                return;
            }

            isPlayerInside = true;
            if (autoTriggerOnWalkIn)
            {
                TriggerSceneTransition();
                return;
            }
            AnimatePrompt(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            if (!autoTriggerOnWalkIn)
            {
                AnimatePrompt(false);
            }
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
            
            if (spriteRenderer != null)
            {
                SetVisualAlpha(Mathf.Lerp(startAlpha, endAlpha, t));
            }

            if (uiPromptObject != gameObject)
            {
                if (visible)
                {
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
                    promptTransform.localScale = Vector3.Lerp(startScale, endScale, t);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

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
