using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    public int maxHealth = 14;
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public event Action<int> onDamageTaken;
    public event Action<int> onMaxHealthChanged;
    public event Action onDeath;

    void Start()
    {
        if (CompareTag("Player"))
        {
            maxHealth = 14; // Force player max health to 14 (7 units) to override Unity inspector value
        }
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

        if (CompareTag("Player"))
        {
            // Disable movement and combat to prevent input execution after death
            move playerMove = GetComponent<move>();
            if (playerMove != null) playerMove.enabled = false;

            MageCombat playerCombat = GetComponent<MageCombat>();
            if (playerCombat != null) playerCombat.enabled = false;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.isKinematic = true;
            }

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            StartCoroutine(ReloadSceneRoutine(2.0f));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator ReloadSceneRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}