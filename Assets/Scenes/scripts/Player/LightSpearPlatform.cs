using System.Collections;
using UnityEngine;

/// <summary>
/// Bouncy 2D Light Spear platform summoned by Lumi the Orb of Light.
/// Acts as a temporary solid/bouncy platform for 5 seconds that players can jump onto or downstab/pogo off of.
/// </summary>
public class LightSpearPlatform : MonoBehaviour
{
    [Header("Spear Settings")]
    public float duration = 5.0f;
    public float bounceForce = 18.0f;
    public Color spearColor = new Color(255f / 255f, 220f / 255f, 60f / 255f, 1f);

    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Light lightComponent;

    void Awake()
    {
        // Setup Collider
        boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(2.4f, 0.4f);

        // Physics Material for Bounciness
        PhysicsMaterial2D bouncyMat = new PhysicsMaterial2D("SpearBounce")
        {
            bounciness = 0.8f,
            friction = 0.2f
        };
        boxCollider.sharedMaterial = bouncyMat;

        // Setup Visual Sprite
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateSpearSprite();
        spriteRenderer.color = spearColor;
        spriteRenderer.sortingOrder = 15;

        // Setup Light Component if available
        lightComponent = gameObject.AddComponent<Light>();
        if (lightComponent != null)
        {
            lightComponent.type = LightType.Point;
            lightComponent.range = 5f;
            lightComponent.color = spearColor;
            lightComponent.intensity = 2f;
        }

        StartCoroutine(SpearLifetimeRoutine());
    }

    private Sprite CreateSpearSprite()
    {
        // Generate a 128x24 procedural sharp glowing spear texture
        int w = 128, h = 24;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w * 0.5f) / (w * 0.5f);
                float dy = (y - h * 0.5f) / (h * 0.5f);
                float dist = Mathf.Abs(dy) + Mathf.Abs(dx) * 0.2f;

                if (dist < 0.8f)
                {
                    float alpha = Mathf.Clamp01(1f - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 0.8f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // Launch player upward with pogo/bounce force
                Vector2 vel = playerRb.linearVelocity;
                if (vel.y <= 1.0f)
                {
                    vel.y = bounceForce;
                    playerRb.linearVelocity = vel;
                    Debug.Log("LightSpearPlatform: Player bounced off Light Spear!");
                }
            }
        }
    }

    private IEnumerator SpearLifetimeRoutine()
    {
        // Pulse animation
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = 1.0f + Mathf.Sin(elapsed * 8f) * 0.05f;
            transform.localScale = initialScale * pulse;

            // Fade out during last second
            if (elapsed > duration - 1.0f)
            {
                float alpha = Mathf.Clamp01(duration - elapsed);
                if (spriteRenderer != null)
                {
                    Color c = spearColor;
                    c.a = alpha;
                    spriteRenderer.color = c;
                }
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
