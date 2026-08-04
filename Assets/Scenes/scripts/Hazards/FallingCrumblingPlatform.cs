using System.Collections;
using UnityEngine;

/// <summary>
/// FallingCrumblingPlatform - Mario-style falling/crumbling platform.
/// Shakes when the Player steps on it, falls into the void after a short delay,
/// and respawns automatically at its original position after a cooldown.
/// </summary>
public class FallingCrumblingPlatform : MonoBehaviour
{
    [Header("Timings")]
    [Tooltip("Duration of shaking vibration before dropping.")]
    public float shakeDuration = 0.4f;

    [Tooltip("Total delay from first step until platform starts falling.")]
    public float fallDelay = 0.6f;

    [Tooltip("Time in seconds before the platform respawns at its origin.")]
    public float respawnDelay = 3.5f;

    [Header("Shake Settings")]
    [Tooltip("Intensity of vibration shake offset.")]
    public float shakeIntensity = 0.08f;

    [Header("Fall Physics")]
    [Tooltip("Downward speed when falling.")]
    public float fallSpeed = 12f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private SpriteRenderer spriteRenderer;
    private Collider2D platformCollider;
    private Rigidbody2D rb;

    private bool isTriggered = false;
    private bool isFalling = false;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        platformCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTriggered) return;

        // Check if player landed on top of platform
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<move>() != null)
        {
            // Verify collision normal is pointing downwards (player is above platform)
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.5f)
                {
                    StartCoroutine(CrumbleSequence());
                    break;
                }
            }
        }
    }

    private IEnumerator CrumbleSequence()
    {
        isTriggered = true;

        // Phase 1: Micro-shake vibration warning
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetY = Random.Range(-shakeIntensity, shakeIntensity);
            transform.position = initialPosition + new Vector3(offsetX, offsetY, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset to initial position before fall
        transform.position = initialPosition;

        // Remaining delay before fall if fallDelay > shakeDuration
        if (fallDelay > shakeDuration)
        {
            yield return new WaitForSeconds(fallDelay - shakeDuration);
        }

        // Phase 2: Falling
        isFalling = true;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 2.5f;
        }

        // Disable platform collision after dropping slightly so player falls through cleanly
        yield return new WaitForSeconds(0.15f);
        if (platformCollider != null) platformCollider.enabled = false;

        // Fade out visual sprite while falling
        elapsed = 0f;
        float fadeDuration = 0.8f;
        Color origColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        while (elapsed < fadeDuration)
        {
            if (rb == null)
            {
                transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            }
            if (spriteRenderer != null)
            {
                Color c = origColor;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                spriteRenderer.color = c;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Hide completely
        if (spriteRenderer != null)
        {
            Color c = origColor;
            c.a = 0f;
            spriteRenderer.color = c;
        }

        // Phase 3: Wait for Respawn Cooldown
        float remainingRespawnTime = Mathf.Max(0.5f, respawnDelay - (shakeDuration + fadeDuration));
        yield return new WaitForSeconds(remainingRespawnTime);

        // Phase 4: Respawn & Re-enable
        ResetPlatform(origColor);
    }

    private void ResetPlatform(Color origColor)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        transform.position = initialPosition;
        transform.rotation = initialRotation;

        if (platformCollider != null) platformCollider.enabled = true;
        if (spriteRenderer != null) spriteRenderer.color = origColor;

        isTriggered = false;
        isFalling = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 pos = Application.isPlaying ? initialPosition : transform.position;
        Gizmos.DrawWireCube(pos, transform.localScale);
    }
}
