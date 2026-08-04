using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RotatingHazardChain - Mario-style rotating firebar/hazard arm.
/// Continuously rotates a chain of hazardous node objects (or generated hazard colliders)
/// around a central pivot point to create timing-based jump obstacles.
/// </summary>
public class RotatingHazardChain : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees per second of rotation.")]
    public float rotationSpeed = 90f;

    [Tooltip("If true, rotates clockwise; otherwise counter-clockwise.")]
    public bool clockwise = true;

    [Header("Hazard Chain Configuration")]
    [Tooltip("Number of hazard orbs/nodes along the arm.")]
    public int hazardCount = 4;

    [Tooltip("Spacing between each hazard node in units.")]
    public float spacing = 1.2f;

    [Tooltip("Damage dealt to player on contact.")]
    public int contactDamage = 15;

    [Tooltip("Knockback force applied to player on contact.")]
    public float knockbackForce = 8f;

    [Header("Visual Customization")]
    public Color hazardColor = new Color(1f, 0.35f, 0.1f, 1f); // Flame/Plasma orange

    private List<GameObject> hazardNodes = new List<GameObject>();

    void Start()
    {
        GenerateHazardChain();
    }

    void Update()
    {
        float direction = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, direction * rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Generates child hazard nodes along the arm if not already assigned.
    /// </summary>
    public void GenerateHazardChain()
    {
        // Clear existing generated children
        foreach (var node in hazardNodes)
        {
            if (node != null) Destroy(node);
        }
        hazardNodes.Clear();

        for (int i = 1; i <= hazardCount; i++)
        {
            GameObject node = new GameObject($"HazardNode_{i}");
            node.transform.SetParent(transform);
            node.transform.localPosition = new Vector3(i * spacing, 0f, 0f);
            node.transform.localRotation = Quaternion.identity;

            // Add SpriteRenderer for visual representation
            SpriteRenderer sr = node.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDefaultOrbSprite();
            sr.color = hazardColor;
            sr.sortingOrder = 5;

            #if UNITY_EDITOR
            // Match material if available
            var mat = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null 
                ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.defaultMaterial 
                : null;
            if (mat != null) sr.material = mat;
            #endif

            // Add CircleCollider2D (Trigger) for hazard damage
            CircleCollider2D col = node.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.isTrigger = true;

            // Attach HazardTrigger helper component
            HazardContactHandler handler = node.AddComponent<HazardContactHandler>();
            handler.damage = contactDamage;
            handler.knockback = knockbackForce;

            hazardNodes.Add(node);
        }
    }

    private Sprite CreateDefaultOrbSprite()
    {
        // Create a 32x32 glowing circle texture procedurally
        int res = 32;
        Texture2D tex = new Texture2D(res, res);
        float center = res / 2f;
        float maxRadius = res / 2f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / maxRadius));
                alpha = Mathf.Pow(alpha, 1.5f); // Smooth radial glow falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 32);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = hazardColor;
        for (int i = 1; i <= hazardCount; i++)
        {
            Vector3 worldPos = transform.TransformPoint(new Vector3(i * spacing, 0f, 0f));
            Gizmos.DrawWireSphere(worldPos, 0.45f);
        }
    }
}

/// <summary>
/// Helper trigger handler on each hazard orb to damage the player on touch.
/// </summary>
public class HazardContactHandler : MonoBehaviour
{
    public int damage = 15;
    public float knockback = 8f;

    private float lastDamageTime = -10f;
    private const float DAMAGE_COOLDOWN = 0.6f;

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        HandleDamage(other);
    }

    private void HandleDamage(Collider2D other)
    {
        if (Time.time < lastDamageTime + DAMAGE_COOLDOWN) return;

        if (other.CompareTag("Player") || other.GetComponent<move>() != null)
        {
            lastDamageTime = Time.time;

            // Damage player health
            Health pHealth = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();
            if (pHealth != null)
            {
                pHealth.TakeDamage(damage);
            }
            else
            {
                IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                }
            }

            // Apply knockback direction away from hazard node
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 dir = (other.transform.position - transform.position).normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                rb.linearVelocity = dir * knockback;
            }
        }
    }
}
