using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BaseMob : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    public Transform leftPoint;
    public Transform rightPoint;
    public float moveSpeed = 2.0f;
    public float waitAtTurn = 0.5f;
    public string walkBool = "isWalking";

    [Header("Health")]
    public int maxHealth = 50;
    public float hitFlashDuration = 0.1f;

    [Header("Animation")]
    public Animator animator;

    private Rigidbody2D rb;
    private int currentHealth;
    private bool movingRight = true;
    private float waitTimer;
    private bool isDead;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = animator ?? GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead)
            return;

        Patrol();
    }

    private void Patrol()
    {
        if (leftPoint == null || rightPoint == null)
        {
            SetWalking(false);
            return;
        }

        Vector2 target = movingRight ? (Vector2)rightPoint.position : (Vector2)leftPoint.position;
        float distance = Vector2.Distance(transform.position, target);

        if (distance <= 0.2f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetWalking(false);
            if (waitTimer <= 0f)
            {
                waitTimer = waitAtTurn;
                movingRight = !movingRight;
                Flip();
            }
            else
            {
                waitTimer -= Time.deltaTime;
            }
            return;
        }

        float direction = movingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        SetWalking(true);
    }

    private void SetWalking(bool walking)
    {
        if (animator != null)
            animator.SetBool(walkBool, walking);
    }

    private void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (movingRight ? 1f : -1f);
        transform.localScale = scale;
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (animator != null)
            animator.SetTrigger("hit");

        if (spriteRenderer != null)
            StartCoroutine(FlashHit());

        if (currentHealth <= 0)
            Die();
    }

    private System.Collections.IEnumerator FlashHit()
    {
        Color original = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(hitFlashDuration);
        spriteRenderer.color = original;
    }

    private void Die()
    {
        isDead = true;
        if (animator != null)
            animator.SetTrigger("die");

        rb.linearVelocity = Vector2.zero;
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        Destroy(gameObject, 2f);
    }
}

