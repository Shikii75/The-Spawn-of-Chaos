using System.Collections;
using UnityEngine;

[AddComponentMenu("Combat/Sprite Juice")]
public class SpriteJuice : MonoBehaviour
{
    [Header("Hit Stop Settings")]
    [Tooltip("Pause animation speed during hit impacts.")]
    public bool enableHitStop = true;
    public float hitStopDuration = 0.08f;

    [Header("Sprite Shake Settings")]
    [Tooltip("Shake/Jitter the sprite transform on hit.")]
    public bool enableShake = true;
    public float shakeDuration = 0.12f;
    public float shakeMagnitude = 0.08f;

    [Header("Flash Settings")]
    [Tooltip("Flash the sprite color on hit.")]
    public bool enableFlash = true;
    public Color flashColor = Color.white;
    public float flashDuration = 0.08f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Animator anim;
    
    private Vector3 originalLocalPosition;
    private Coroutine juiceCoroutine;
    private MaterialPropertyBlock propBlock;
    private Color baseSpriteColor = Color.white;
    private bool hasBaseSpriteColor = false;

    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        propBlock = new MaterialPropertyBlock();

        if (spriteRenderer != null)
        {
            originalLocalPosition = spriteRenderer.transform.localPosition;
            baseSpriteColor = spriteRenderer.color;
            hasBaseSpriteColor = true;
        }
    }

    /// <summary>
    /// Play the hit feedback (Knockback force + Shake + Flash + Hit-Stop)
    /// </summary>
    public void PlayHitReaction(Vector2 hitDirection, float knockbackForce)
    {
        // 1. Rigidbody Physics Knockback
        if (rb != null && knockbackForce > 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Reset horizontal momentum for clean force
            rb.AddForce(new Vector2(hitDirection.x * knockbackForce, knockbackForce * 0.25f), ForceMode2D.Impulse);
        }

        // 2. Play Coroutine Visuals
        if (juiceCoroutine != null)
        {
            StopCoroutine(juiceCoroutine);
        }
        juiceCoroutine = StartCoroutine(JuiceRoutine());
    }

    private IEnumerator JuiceRoutine()
    {
        if (spriteRenderer != null && !hasBaseSpriteColor)
        {
            baseSpriteColor = spriteRenderer.color;
            hasBaseSpriteColor = true;
        }

        // --- A. Apply Hit-Stop (Freeze Animator) ---
        if (enableHitStop && anim != null)
        {
            anim.speed = 0f;
        }

        // --- B. Apply Flash ---
        if (enableFlash && spriteRenderer != null)
        {
            // Check if the material supports our custom flash shader properties
            if (spriteRenderer.sharedMaterial != null && spriteRenderer.sharedMaterial.HasProperty(FlashAmountId))
            {
                spriteRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(FlashColorId, flashColor);
                propBlock.SetFloat(FlashAmountId, 1f);
                spriteRenderer.SetPropertyBlock(propBlock);
            }
            else
            {
                // Fallback to solid tint color
                spriteRenderer.color = flashColor;
            }
        }

        // --- C. Perform Shake Jitter Loop ---
        float elapsed = 0f;
        while (elapsed < Mathf.Max(shakeDuration, flashDuration))
        {
            if (enableShake && spriteRenderer != null && elapsed < shakeDuration)
            {
                float offset = Random.Range(-shakeMagnitude, shakeMagnitude);
                spriteRenderer.transform.localPosition = originalLocalPosition + new Vector3(offset, 0f, 0f);
            }

            // Restore Flash mid-way if flash duration ends before shake
            if (enableFlash && spriteRenderer != null && elapsed >= flashDuration)
            {
                ClearFlash();
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- D. Reset All States ---
        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localPosition = originalLocalPosition;
            ClearFlash();
        }

        if (enableHitStop && anim != null)
        {
            anim.speed = 1f;
        }

        juiceCoroutine = null;
    }

    private void ClearFlash()
    {
        if (spriteRenderer == null) return;

        if (spriteRenderer.sharedMaterial != null && spriteRenderer.sharedMaterial.HasProperty(FlashAmountId))
        {
            spriteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(FlashAmountId, 0f);
            spriteRenderer.SetPropertyBlock(propBlock);
        }
        else if (hasBaseSpriteColor)
        {
            spriteRenderer.color = baseSpriteColor;
        }
    }

    void OnDisable()
    {
        // Clean up on disable to prevent frozen animator/sprites
        if (anim != null) anim.speed = 1f;
        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localPosition = originalLocalPosition;
            ClearFlash();
        }
    }
}
