using UnityEngine;

public class EnemyPatrol2D : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float patrolRange = 5f; // How far left/right it travels from its start position

    private float leftBoundary;
    private float rightBoundary;
    private bool movingRight = true;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // Set the boundaries based on where the enemy starts in the scene
        leftBoundary = transform.position.x - patrolRange;
        rightBoundary = transform.position.x + patrolRange;
    }

    void Update()
    {
        // Handle horizontal movement calculation
        float newX = transform.position.x;

        if (movingRight)
        {
            newX += moveSpeed * Time.deltaTime;
            if (newX >= rightBoundary)
            {
                newX = rightBoundary;
                movingRight = false;
                FlipSprite();
            }
        }
        else
        {
            newX -= moveSpeed * Time.deltaTime;
            if (newX <= leftBoundary)
            {
                newX = leftBoundary;
                movingRight = true;
                FlipSprite();
            }
        }

        // Apply movement
        if (rb != null && !rb.isKinematic)
        {
            // If using standard non-kinematic physics, change position via velocity to prevent clipping
            rb.linearVelocity = new Vector2(movingRight ? moveSpeed : -moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            // Otherwise, move directly via transform
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        }
    }

    void FlipSprite()
    {
        if (spriteRenderer != null)
        {
            // Flips the sprite on the X axis using Unity's built-in toggle
            spriteRenderer.flipX = !movingRight;
        }
    }

    // Optional: Draws the patrol range lines visually in the Unity Editor scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 startPos = Application.isPlaying ? new Vector3((leftBoundary + rightBoundary) / 2f, transform.position.y, transform.position.z) : transform.position;
        
        float currentRange = patrolRange;
        Gizmos.DrawLine(startPos + Vector3.left * currentRange, startPos + Vector3.right * currentRange);
    }
}