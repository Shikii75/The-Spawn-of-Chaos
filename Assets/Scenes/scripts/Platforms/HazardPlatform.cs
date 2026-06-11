using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HazardPlatform : PlatformBase
{
    public int damage = 1;
    public float damageCooldown = 0.8f;
    public string playerTag = "Player";

    private float lastDamageTime;

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag))
            return;

        if (Time.time - lastDamageTime < damageCooldown)
            return;

        var damageable = collision.collider.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
}
