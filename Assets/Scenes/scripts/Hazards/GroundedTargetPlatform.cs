using System.Collections;
using UnityEngine;

/// <summary>
/// GroundedTargetPlatform - A platform that triggers movement towards a target GameObject / Transform
/// when a player character lands or becomes grounded on top of it.
/// Features:
/// - Smooth Kinematic physics movement using Rigidbody2D.MovePosition.
/// - Automatically parents the player when grounded on top so they ride along seamlessly.
/// - Unparents the player safely on exit while preserving DontDestroyOnLoad persistence.
/// - Configurable speed, start delay, return options, and Editor Gizmos path preview.
/// </summary>
public class GroundedTargetPlatform : MonoBehaviour
{
    [Header("Target Location")]
    [Tooltip("The destination GameObject or Transform the platform will move towards when triggered.")]
    public Transform targetLocation;

    [Header("Movement Settings")]
    [Tooltip("Movement speed towards the target location.")]
    public float moveSpeed = 4f;

    [Tooltip("Delay in seconds after player lands on platform before movement begins.")]
    public float startDelay = 0f;

    [Tooltip("If true, platform only moves while the player remains on top. If false, once triggered it continues to target.")]
    public bool moveOnlyWhileGrounded = false;

    [Tooltip("If true, platform returns to its starting position when the player steps off.")]
    public bool returnToStartWhenEmpty = false;

    [Tooltip("Tolerance distance to consider target position reached.")]
    public float stoppingDistance = 0.05f;

    [Header("Events & Audio (Optional)")]
    [Tooltip("Optional AudioSource to play moving sound while traveling.")]
    public AudioSource moveAudioSource;

    private Vector3 startPosition;
    private Vector3 lastPlatformPosition;
    private Rigidbody2D rb;
    private Transform ridingPlayer;
    private Rigidbody2D ridingRb;
    private bool isPlayerOnPlatform = false;
    private bool isActivated = false;
    private float delayTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
        }
    }

    void Start()
    {
        startPosition = transform.position;
        lastPlatformPosition = transform.position;
        if (targetLocation == null)
        {
            Debug.LogWarning($"[GroundedTargetPlatform] Target Location is not assigned on '{gameObject.name}'.", this);
        }
    }

    void FixedUpdate()
    {
        if (targetLocation == null) return;

        Vector3 currentPos = transform.position;
        Vector3 destPos = startPosition;

        // Determine destination based on trigger state
        if (isActivated)
        {
            if (moveOnlyWhileGrounded && !isPlayerOnPlatform)
            {
                destPos = returnToStartWhenEmpty ? startPosition : currentPos;
            }
            else
            {
                if (delayTimer > 0f)
                {
                    delayTimer -= Time.fixedDeltaTime;
                    destPos = currentPos; // Wait during start delay
                }
                else
                {
                    destPos = targetLocation.position;
                }
            }
        }
        else if (returnToStartWhenEmpty && !isPlayerOnPlatform)
        {
            destPos = startPosition;
        }
        else
        {
            destPos = currentPos;
        }

        // Move smoothly towards destination via Kinematic Rigidbody2D
        if (Vector3.Distance(currentPos, destPos) > stoppingDistance)
        {
            Vector3 nextPos = Vector3.MoveTowards(currentPos, destPos, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(nextPos);

            if (moveAudioSource != null && !moveAudioSource.isPlaying)
            {
                moveAudioSource.Play();
            }
        }
        else
        {
            rb.MovePosition(destPos);
            if (moveAudioSource != null && moveAudioSource.isPlaying)
            {
                moveAudioSource.Stop();
            }
        }

        // 👇 Calculate actual platform delta position and move the riding player to prevent parent scale distortion
        Vector3 updatedPos = transform.position;
        Vector3 deltaPosition = updatedPos - lastPlatformPosition;
        lastPlatformPosition = updatedPos;

        if (isPlayerOnPlatform && ridingPlayer != null)
        {
            if (ridingRb != null)
            {
                ridingRb.position += (Vector2)deltaPosition;
            }
            else
            {
                ridingPlayer.position += deltaPosition;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPlayerGrounding(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!isPlayerOnPlatform)
        {
            CheckPlayerGrounding(collision);
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!IsPlayer(collision.collider)) return;

        isPlayerOnPlatform = false;
        ridingPlayer = null;
        ridingRb = null;
    }

    private void CheckPlayerGrounding(Collision2D collision)
    {
        if (!IsPlayer(collision.collider)) return;

        // Verify player is standing on top of platform (contact normal points downward relative to platform)
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                isPlayerOnPlatform = true;
                if (!isActivated)
                {
                    isActivated = true;
                    delayTimer = startDelay;
                }

                ridingPlayer = collision.transform;
                ridingRb = collision.gameObject.GetComponent<Rigidbody2D>();
                return;
            }
        }
    }

    private bool IsPlayer(Collider2D col)
    {
        if (col == null) return false;
        return col.CompareTag("Player") || col.GetComponent<move>() != null;
    }

    /// <summary>
    /// Resets platform back to initial start position and unactivated state.
    /// </summary>
    public void ResetPlatform()
    {
        isActivated = false;
        isPlayerOnPlatform = false;
        delayTimer = 0f;
        transform.position = startPosition;
        if (rb != null) rb.position = startPosition;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? startPosition : transform.position;
        Vector3 target = targetLocation != null ? targetLocation.position : start + Vector3.up * 3f;

        // Path Line
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(start, target);

        // Start Sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(start, 0.3f);

        // Target Sphere
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target, 0.4f);

        // Platform bounding preview at target
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Vector3 size = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 0.1f);
            Gizmos.DrawWireCube(target, size);
        }
    }
}
