using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Spiderling : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    public float speed = 3.5f;
    
    [Header("Combat Settings")]
    public int damage = 10;
    public float attackRange = 0.8f;
    public float attackCooldown = 1.0f;
    public int health = 10;

    private Rigidbody2D rb;
    private Transform playerTransform;
    private float nextAttackTime;
    private bool isDead = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (isDead) return;

        if (playerTransform == null && PlayerCurrency.Instance != null)
        {
            playerTransform = PlayerCurrency.Instance.transform;
        }

        if (playerTransform != null)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            if (distance <= attackRange)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (Time.time >= nextAttackTime)
                {
                    AttackPlayer();
                }
            }
            else
            {
                ChasePlayer();
            }
        }
    }

    private void ChasePlayer()
    {
        if (playerTransform == null) return;

        float direction = Mathf.Sign(playerTransform.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

        // Flip sprite based on direction
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    private void AttackPlayer()
    {
        nextAttackTime = Time.time + attackCooldown;
        Debug.Log("Spiderling biting player!");

        // Trigger attack animation if present
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        Health playerHealth = playerTransform.GetComponent<Health>();
        if (playerHealth == null) playerHealth = playerTransform.GetComponentInParent<Health>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        // Spiderlings explode/die upon attack for visual action feedback
        Die();
    }

    public void TakeDamage(int damageTaken)
    {
        if (isDead) return;

        health -= damageTaken;
        if (health <= 0)
        {
            Die();
        }
        else
        {
            // Flash red on hit
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                StartCoroutine(FlashRed(sr));
            }
        }
    }

    private System.Collections.IEnumerator FlashRed(SpriteRenderer sr)
    {
        Color orig = sr.color;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        sr.color = orig;
    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        // Trigger death animation
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetTrigger("Die");
        }

        // Spawn a coin on death (30% chance)
        if (Random.value < 0.3f)
        {
            PlayerCurrency pc = FindFirstObjectByType<PlayerCurrency>();
            if (pc != null)
            {
                pc.AddCoins(2);
            }
        }

        Destroy(gameObject, 0.5f);
    }
}
