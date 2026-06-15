using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public event Action<int> onDamageTaken;
    public event Action<int> onMaxHealthChanged;
    public event Action onDeath;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        // Check if invulnerable (e.g. player is dashing)
        move playerMove = GetComponent<move>();
        if (playerMove != null && playerMove.IsInvulnerable)
        {
            Debug.Log($"{name} is invulnerable during dash! Ignored damage.");
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        Debug.Log($"{name} took {damage} damage. Health now {currentHealth}/{maxHealth}.");

        EnemyPatrol2D enemy = GetComponent<EnemyPatrol2D>();
        if (enemy != null)
        {
            enemy.TriggerKnockback();
        }

        onDamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onMaxHealthChanged?.Invoke(maxHealth);
        onDamageTaken?.Invoke(0); // trigger UI redraw
        Debug.Log($"{name} max health increased by {amount}. Max: {maxHealth}, Current: {currentHealth}");
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onDamageTaken?.Invoke(0); // trigger UI redraw
        Debug.Log($"{name} healed by {amount}. Health now {currentHealth}/{maxHealth}.");
    }

    void Die()
    {
        Debug.Log($"{name} died.");
        onDeath?.Invoke();
        Destroy(gameObject);
    }
}