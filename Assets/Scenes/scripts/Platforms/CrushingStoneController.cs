using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the "CrushingStone" GameObject in the save point cave.
/// The stone hangs from the pulley at its start position. When the player
/// enters the trigger zone, the ropes are "cut" and the stone drops.
/// If the stone lands on the player they die instantly (Lore: the checkpoint
/// was set precisely for this; after death they respawn at the crystal).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class CrushingStoneController : MonoBehaviour
{
    [Header("Drop Settings")]
    [Tooltip("Trigger zone (separate invisible trigger collider) that starts the drop when the player enters.")]
    public Collider2D dropTrigger;
    public float dropDelay  = 1.2f;   // seconds before stone falls (rumble warning)
    public float resetDelay = 4.0f;   // seconds to wait before resetting stone to start pos

    [Header("Visual Shake")]
    public float shakeAmplitude = 0.08f;
    public float shakeFrequency = 18f;

    [Header("Damage")]
    public int crushDamage = 999;      // Instakill

    private Rigidbody2D   rb;
    private BoxCollider2D col;
    private Vector3       startPosition;
    private bool          isDropping    = false;
    private bool          hasLanded     = false;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();

        // Stone hangs in place until triggered
        rb.isKinematic = true;
        rb.gravityScale = 4f; // heavy drop when released

        startPosition = transform.position;
    }

    void Update()
    {
        // Manual trigger check if dropTrigger not assigned (fallback: player proximity)
        if (dropTrigger == null && !isDropping && !hasLanded)
        {
            Collider2D hit = Physics2D.OverlapBox(
                transform.position + Vector3.down * 3f,
                new Vector2(transform.localScale.x * 1.2f, 6f),
                0f,
                LayerMask.GetMask("Default")
            );
            if (hit != null && hit.CompareTag("Player"))
                StartCoroutine(TriggerDrop());
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isDropping || hasLanded) return;
        StartCoroutine(TriggerDrop());
    }

    private IEnumerator TriggerDrop()
    {
        isDropping = true;
        Debug.Log("[CrushingStone] Player entered zone — stone beginning to shake...");

        float elapsed = 0f;
        Vector3 basePos = transform.position;

        // Shake warning
        while (elapsed < dropDelay)
        {
            float offsetX = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI) * shakeAmplitude;
            transform.position = basePos + new Vector3(offsetX, 0f, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = basePos; // snap back before release

        // Release stone
        rb.isKinematic = false;
        Debug.Log("[CrushingStone] Released! Stone falling.");

        // Wait until it settles (or 3 seconds)
        yield return new WaitForSeconds(3f);

        hasLanded = true;

        // Reset after delay
        yield return new WaitForSeconds(resetDelay);
        ResetStone();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDropping) return;
        if (!collision.collider.CompareTag("Player")) return;

        // Crush the player
        var health = collision.collider.GetComponent<Health>();
        if (health != null)
        {
            Debug.Log("[CrushingStone] Player crushed! Dealing instakill damage.");
            health.TakeDamage(crushDamage);
        }
    }

    private void ResetStone()
    {
        rb.isKinematic = true;
        rb.linearVelocity = Vector2.zero;
        transform.position = startPosition;
        isDropping = false;
        hasLanded  = false;
        Debug.Log("[CrushingStone] Stone reset to start position.");
    }
}
