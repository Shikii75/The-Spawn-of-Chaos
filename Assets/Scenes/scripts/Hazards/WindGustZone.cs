using UnityEngine;

/// <summary>
/// WindGustZone - Trigger zone that applies a sustained lateral force to the player's Rigidbody2D.
/// Used in parkour sections to push players off narrow platforms or make tight landings harder.
/// </summary>
public class WindGustZone : MonoBehaviour
{
    [Header("Wind Settings")]
    [Tooltip("Direction the wind blows (normalized automatically).")]
    public Vector2 windDirection = Vector2.right;

    [Tooltip("Force strength applied to the player each physics frame.")]
    public float windForce = 6f;

    [Tooltip("If true, the wind oscillates (gusts). If false, constant.")]
    public bool isGusting = false;

    [Tooltip("Oscillation period in seconds (only used if isGusting = true).")]
    public float gustPeriod = 2f;

    [Header("Visual")]
    [Tooltip("Tint color of the trigger zone visualizer.")]
    public Color zoneColor = new Color(0.5f, 0.8f, 1f, 0.15f);

    private Rigidbody2D playerRb;
    private bool playerInside = false;

    void Start()
    {
        // Ensure this object has a trigger collider
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.GetComponent<move>() != null)
        {
            playerRb = other.GetComponent<Rigidbody2D>();
            playerInside = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.GetComponent<move>() != null)
        {
            playerInside = false;
            playerRb = null;
        }
    }

    void FixedUpdate()
    {
        if (!playerInside || playerRb == null) return;

        float currentForce = windForce;
        if (isGusting)
        {
            // Oscillate between 0 and windForce using a sine wave
            currentForce = windForce * Mathf.Max(0f, Mathf.Sin(Time.time * (2f * Mathf.PI / gustPeriod)));
        }

        playerRb.AddForce(windDirection.normalized * currentForce, ForceMode2D.Force);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = zoneColor;
        Collider2D col = GetComponent<Collider2D>();
        if (col is BoxCollider2D box)
        {
            Vector3 center = transform.position + (Vector3)box.offset;
            Vector3 size = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 0.1f);
            Gizmos.DrawCube(center, size);

            // Draw wind direction arrow
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            Vector3 arrowStart = center;
            Vector3 arrowEnd = center + (Vector3)(windDirection.normalized * 2f);
            Gizmos.DrawLine(arrowStart, arrowEnd);
            // Arrow head
            Vector2 perp = Vector2.Perpendicular(windDirection.normalized) * 0.4f;
            Gizmos.DrawLine(arrowEnd, arrowEnd - (Vector3)(windDirection.normalized * 0.5f) + (Vector3)perp);
            Gizmos.DrawLine(arrowEnd, arrowEnd - (Vector3)(windDirection.normalized * 0.5f) - (Vector3)perp);
        }
    }
}
