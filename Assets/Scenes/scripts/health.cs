using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    private int currentHealth;

    public int CurrentHealth => currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        Debug.Log($"{name} took {damage} damage. Health now {currentHealth}/{maxHealth}.");

        EnemyPatrol2D enemy = GetComponent<EnemyPatrol2D>();
        if (enemy != null)
        {
            enemy.TriggerKnockback();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{name} died.");
        Destroy(gameObject);
    }
}