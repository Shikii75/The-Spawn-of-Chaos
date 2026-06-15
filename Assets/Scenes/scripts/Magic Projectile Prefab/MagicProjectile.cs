using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MagicProjectile : MonoBehaviour
{
    public float speed = 12f;
    public int damage = 25;
    public float lifetime = 2f;
    public string enemyTag = "Enemy";

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Ensure it's a trigger collider so it doesn't push enemies physically
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Launch(float direction)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        
        // Orient projectile sprite
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;

        rb.linearVelocity = new Vector2(direction * speed, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore parent player colliders
        if (other.CompareTag("Player")) return;

        // Deal damage if it hits an enemy
        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
        {
            target = other.GetComponentInParent<IDamageable>();
        }

        if (target != null)
        {
            target.TakeDamage(damage);
            Explode();
            return;
        }

        // Collide with obstacle or ground
        if (other.CompareTag("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Obstacles"))
        {
            Explode();
        }
    }

    private void Explode()
    {
        // Optional: Spawn explosion effect here
        Destroy(gameObject);
    }
}
