using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : PlatformBase
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2.5f;

    private Vector3 targetPosition;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true;
    }

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning("MovingPlatform requires both pointA and pointB.");
            enabled = false;
            return;
        }
        targetPosition = pointB.position;
        rb.position = transform.position;
    }

    private Transform playerTransform;
    private bool playerOnPlatform = false;

    void FixedUpdate()
    {
        Vector2 currentPos = rb.position;
        Vector2 targetPos = targetPosition;
        Vector2 newPos = Vector2.MoveTowards(currentPos, targetPos, speed * Time.fixedDeltaTime);
        
        Vector2 delta = newPos - currentPos;
        rb.MovePosition(newPos);

        if (playerOnPlatform && playerTransform != null)
        {
            playerTransform.position += (Vector3)delta;
        }

        if (Vector2.Distance(newPos, targetPos) < 0.05f)
        {
            targetPosition = targetPosition == pointA.position ? pointB.position : pointA.position;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            // Only mount the player if they landed on top of the platform
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.5f)
                {
                    playerOnPlatform = true;
                    playerTransform = collision.transform;
                    break;
                }
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            playerOnPlatform = false;
            if (playerTransform == collision.transform)
            {
                playerTransform = null;
            }
        }
    }
}
