using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FallingPlatform : PlatformBase
{
    public float triggerDelay = 0.5f;
    public float fallSpeed = 12f;
    public float resetDelay = 3.0f;

    private Rigidbody2D rb;
    private Collider2D col;
    private bool triggered;
    private float timer;
    private Vector3 startPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.isKinematic = true;
        startPosition = transform.position;
    }

    void Update()
    {
        if (!triggered)
            return;

        timer += Time.deltaTime;
        if (timer >= triggerDelay && rb.isKinematic)
        {
            rb.isKinematic = false;
            rb.gravityScale = 2f;
            rb.linearVelocity = Vector2.down * fallSpeed;
        }

        if (timer >= triggerDelay + resetDelay)
        {
            ResetPlatform();
        }
    }

    private void ResetPlatform()
    {
        triggered = false;
        timer = 0f;
        rb.isKinematic = true;
        rb.linearVelocity = Vector2.zero;
        transform.position = startPosition;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!triggered && collision.collider.CompareTag("Player"))
        {
            triggered = true;
            timer = 0f;
        }
    }
}

