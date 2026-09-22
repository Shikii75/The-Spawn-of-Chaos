using UnityEngine;

/// <summary>
/// DamageZone - Trigger zone that deals damage-over-time (DPS) to the player while inside.
/// Used as spike pits, lava pools, or poison clouds beneath parkour sections.
/// Optionally teleports the player to a respawn point on entry.
/// </summary>
public class DamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Damage dealt per second while the player is inside the zone.")]
    public float damagePerSecond = 25f;

    [Tooltip("Damage tick interval in seconds.")]
    public float tickInterval = 0.3f;

    [Tooltip("Knockback force applied upward on each tick (to bounce the player).")]
    public float knockbackUpForce = 8f;

    [Header("Respawn Teleport (Optional)")]
    [Tooltip("If assigned, the player is teleported here when entering the zone.")]
    public Transform respawnPoint;

    [Tooltip("Flat damage dealt instantly on entry before respawn teleport.")]
    public int entryDamage = 30;

    [Header("Fall Hazard / Platform Respawn")]
    [Tooltip("If true, entering this zone routes through PlayerPlatformFallManager for safe platform respawn and 25% max health penalty.")]
    public bool isFallHazard = false;

    [Header("Visual")]
    public Color zoneColor = new Color(1f, 0.2f, 0.1f, 0.2f);

    private float nextTickTime;
    private bool playerInside;
    private Rigidbody2D playerRb;
    private Health playerHealth;

    void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        playerRb = other.GetComponent<Rigidbody2D>();
        playerHealth = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();

        // If configured as a platform fall hazard, route through PlayerPlatformFallManager
        if (isFallHazard)
        {
            if (PlayerPlatformFallManager.Instance != null)
            {
                PlayerPlatformFallManager.Instance.TriggerFallRespawn(other.gameObject);
                playerInside = false;
                return;
            }
        }

        // If respawn point is set, teleport the player immediately + deal flat entry damage
        if (respawnPoint != null)
        {
            if (playerHealth != null)
            {
                if (isFallHazard)
                {
                    playerHealth.TakeFallPenalty(25f);
                }
                else
                {
                    playerHealth.TakeDamage(entryDamage);
                }
            }

            // Teleport player
            other.transform.position = respawnPoint.position;
            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
            playerInside = false;
            return;
        }

        playerInside = true;
        nextTickTime = Time.time;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerInside = false;
        playerRb = null;
        playerHealth = null;
    }

    void Update()
    {
        if (!playerInside || playerHealth == null) return;

        if (Time.time >= nextTickTime)
        {
            int tickDamage = Mathf.CeilToInt(damagePerSecond * tickInterval);
            playerHealth.TakeDamage(tickDamage);

            // Bounce player upward to give them a chance to escape
            if (playerRb != null)
            {
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, knockbackUpForce);
            }

            nextTickTime = Time.time + tickInterval;
        }
    }

    private bool IsPlayer(Collider2D col)
    {
        return col.CompareTag("Player") || col.GetComponent<move>() != null;
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

            // Draw X pattern for danger
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Vector3 halfSize = size * 0.5f;
            Gizmos.DrawLine(center - halfSize, center + halfSize);
            Gizmos.DrawLine(center + new Vector3(-halfSize.x, halfSize.y, 0f), center + new Vector3(halfSize.x, -halfSize.y, 0f));
        }

        // Draw respawn point indicator
        if (respawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(respawnPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, respawnPoint.position);
        }
    }
}
