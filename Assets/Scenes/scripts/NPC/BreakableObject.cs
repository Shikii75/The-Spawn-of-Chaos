using UnityEngine;

public class BreakableObject : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public int health = 3;
    
    [Header("Visual Effects")]
    [Tooltip("Prefab spawned when the object breaks.")]
    public GameObject breakParticlesPrefab;
    [Tooltip("Optional sprite to show after breaking. If null, the object is destroyed.")]
    public Sprite brokenSprite;
    
    [Header("Loot Drops")]
    [Tooltip("Pool of items that can drop when broken.")]
    public GameObject[] lootPrefabs;
    [Range(0f, 1f)]
    public float dropChance = 0.5f;

    private bool isBroken = false;
    private SpriteRenderer sr;
    private Collider2D col;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        health -= damage;
        
        // Simple hit flash visual feedback
        StartCoroutine(HitFlash());

        if (health <= 0)
        {
            Break();
        }
    }

    private Color baseBreakableColor = Color.white;
    private bool hasCachedBreakableColor = false;

    private System.Collections.IEnumerator HitFlash()
    {
        if (sr == null) yield break;
        if (!hasCachedBreakableColor)
        {
            baseBreakableColor = sr.color;
            hasCachedBreakableColor = true;
        }

        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        sr.color = baseBreakableColor;
    }

    private void Break()
    {
        isBroken = true;

        // Spawn particles
        if (breakParticlesPrefab != null)
        {
            Instantiate(breakParticlesPrefab, transform.position, Quaternion.identity);
        }

        // Drop random loot item
        if (lootPrefabs != null && lootPrefabs.Length > 0 && Random.value <= dropChance)
        {
            int index = Random.Range(0, lootPrefabs.Length);
            if (lootPrefabs[index] != null)
            {
                Instantiate(lootPrefabs[index], transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }
        }
        else
        {
            // Drop collectible loot orbs via OrbSpawner standard system
            SpawnOfChaos.Systems.OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 0.5f, Random.Range(2, 4));
        }

        // Change appearance or destroy
        if (brokenSprite != null && sr != null)
        {
            sr.sprite = brokenSprite;
            if (col != null) col.enabled = false; // Disable collision
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
