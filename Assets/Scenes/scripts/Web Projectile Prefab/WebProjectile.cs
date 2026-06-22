using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class WebProjectile : MonoBehaviour
{
    [Header("Web Settings")]
    public float speed = 8.0f;
    public int damage = 1;
    public float slowDuration = 2.5f;
    public float slowMultiplier = 0.5f;
    public float lifetime = 4.0f;

    private Rigidbody2D rb;
    private Transform playerTransform;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);

        // Target the player when spawned
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            rb.linearVelocity = direction * speed;

            // Rotate projectile to face travel direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        else
        {
            // If player not found, just fly forward in whatever direction boss is facing
            rb.linearVelocity = transform.right * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore other enemy/boss colliders
        if (other.CompareTag("Enemy") || other.name.Contains("Boss") || other.name.Contains("Tsuchigumo"))
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth == null) playerHealth = other.GetComponentInParent<Health>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            move playerMove = other.GetComponent<move>();
            if (playerMove == null) playerMove = other.GetComponentInParent<move>();

            if (playerMove != null)
            {
                playerMove.ApplySlow(slowDuration, slowMultiplier);
            }

            Destroy(gameObject);
            return;
        }

        // Hit solid terrain
        if (other.CompareTag("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
