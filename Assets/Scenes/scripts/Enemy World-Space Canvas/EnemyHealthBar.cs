using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI Reference")]
    public Slider healthSlider;
    public Vector3 offset = new Vector3(0f, 1.5f, 0f);

    [Header("Fade settings")]
    public bool fadeOutWhenFull = true;
    public float showDuration = 3f;

    private Health targetHealth;
    private CanvasGroup canvasGroup;
    private float hideTimer;

    void Start()
    {
        targetHealth = GetComponentInParent<Health>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Setup Slider
        if (targetHealth != null && healthSlider != null)
        {
            healthSlider.maxValue = targetHealth.MaxHealth;
            healthSlider.value = targetHealth.CurrentHealth;

            // Subscribe to damage events
            targetHealth.onDamageTaken += OnDamageTaken;
        }

        // Initially hide health bar if it's full
        if (fadeOutWhenFull && targetHealth != null && targetHealth.CurrentHealth == targetHealth.MaxHealth)
        {
            canvasGroup.alpha = 0f;
        }
    }

    void OnDestroy()
    {
        if (targetHealth != null)
        {
            targetHealth.onDamageTaken -= OnDamageTaken;
        }
    }

    void Update()
    {
        // Follow parent position with offset
        if (transform.parent != null)
        {
            transform.position = transform.parent.position + offset;
            // Ensure UI doesn't flip if the enemy sprite flips scale
            Vector3 parentScale = transform.parent.localScale;
            Vector3 localScale = transform.localScale;
            localScale.x = Mathf.Abs(localScale.x) * (parentScale.x < 0 ? -1 : 1);
            transform.localScale = localScale;
        }

        // Handle auto-fade timing
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
            {
                StartCoroutine(FadeAlpha(0f, 0.3f));
            }
        }
    }

    private void OnDamageTaken(int damage)
    {
        if (healthSlider != null && targetHealth != null)
        {
            healthSlider.value = targetHealth.CurrentHealth;
            
            // Show health bar
            StopAllCoroutines();
            canvasGroup.alpha = 1f;
            
            // Set timer to hide
            hideTimer = showDuration;
        }
    }

    private System.Collections.IEnumerator FadeAlpha(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }
}
