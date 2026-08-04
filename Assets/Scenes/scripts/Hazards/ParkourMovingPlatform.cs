using UnityEngine;

/// <summary>
/// ParkourMovingPlatform - Oscillating platform that carries the player along its path.
/// Uses kinematic Rigidbody2D for proper physics interaction.
/// Automatically parents the player when they stand on top, so they ride the platform.
/// </summary>
public class ParkourMovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Axis of oscillation (normalized internally).")]
    public Vector2 moveAxis = Vector2.right;

    [Tooltip("Total distance traveled from center to one extreme.")]
    public float moveDistance = 3f;

    [Tooltip("Full cycle period in seconds.")]
    public float period = 3f;

    [Tooltip("Phase offset (0-1) to stagger multiple platforms.")]
    [Range(0f, 1f)]
    public float phaseOffset = 0f;

    [Header("Pause Settings")]
    [Tooltip("If > 0, the platform pauses briefly at each endpoint.")]
    public float endPauseDuration = 0f;

    private Vector3 startPosition;
    private Rigidbody2D rb;
    private Transform ridingPlayer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        // Ensure a collider exists
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
        }
    }

    void Start()
    {
        startPosition = transform.position;
    }

    void FixedUpdate()
    {
        float t = (Time.time / Mathf.Max(0.01f, period)) + phaseOffset;
        float wave = Mathf.Sin(t * 2f * Mathf.PI);

        Vector3 targetPos = startPosition + (Vector3)(moveAxis.normalized * wave * moveDistance);
        
        // Move via MovePosition for proper kinematic-to-dynamic physics interaction
        rb.MovePosition(targetPos);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsPlayer(collision.collider)) return;

        // Check if player landed on top (contact normal points up from platform perspective)
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                // Player is standing on top of us — parent them for ride-along
                ridingPlayer = collision.transform;
                collision.transform.SetParent(transform);
                return;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!IsPlayer(collision.collider)) return;

        if (ridingPlayer != null)
        {
            ridingPlayer.SetParent(null);

            // Re-apply DontDestroyOnLoad for the player so it persists across scenes
            move playerMove = ridingPlayer.GetComponent<move>();
            if (playerMove != null)
            {
                DontDestroyOnLoad(ridingPlayer.gameObject);
            }

            ridingPlayer = null;
        }
    }

    private bool IsPlayer(Collider2D col)
    {
        return col.CompareTag("Player") || col.GetComponent<move>() != null;
    }

    void OnDrawGizmos()
    {
        Vector3 center = Application.isPlaying ? startPosition : transform.position;
        Vector3 axisDir = (Vector3)(moveAxis.normalized * moveDistance);

        // Draw movement range
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Gizmos.DrawLine(center - axisDir, center + axisDir);

        // Draw endpoint markers
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center - axisDir, 0.3f);
        Gizmos.DrawWireSphere(center + axisDir, 0.3f);

        // Draw platform position at center
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Vector3 size = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 0.1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
