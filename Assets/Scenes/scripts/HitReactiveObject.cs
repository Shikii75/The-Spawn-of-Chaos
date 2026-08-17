using System.Collections;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// HitReactiveObject - Component for interactive objects that react dynamically to hits (Crates, Pots, Dummies, Lanterns, Signs).
/// Implements IDamageable to respond to melee attacks, magic projectiles, and physical impacts.
/// Features directional squash-and-stretch wobble, pendulum swinging, training dummy spring-back, debris shatter physics, and loot drops.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class HitReactiveObject : MonoBehaviour, IDamageable
{
    public enum ObjectType
    {
        BreakableCrate,
        ClayPot,
        TrainingDummy,
        HangingLantern,
        WoodenSign,
        Barrel,
        Custom
    }

    [Header("Object Configuration")]
    public ObjectType objectType = ObjectType.BreakableCrate;
    public int health = 3;
    public int lootOrbCount = 3;

    [Header("Juice & Wobble FX")]
    public float wobbleIntensity = 0.25f;
    public float wobbleSpeed = 25f;
    public float wobbleDamping = 6f;
    public bool flashWhiteOnHit = true;

    [Header("Debris & Destruction FX")]
    public Sprite brokenSprite;
    public Color debrisParticleColor = new Color(0.8f, 0.7f, 0.5f, 1f);
    public int debrisShardCount = 4;
    public float explosionForce = 5f;
    public AudioClip hitSound;
    public AudioClip shatterSound;

    private SpriteRenderer sr;
    private Collider2D col;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Color originalColor;
    private int currentHealth;
    private bool isBroken = false;

    // Animation & Wobble state
    private Vector3 currentScaleOffset = Vector3.zero;
    private Vector3 scaleVelocity = Vector3.zero;
    private float pendulumAngle = 0f;
    private float pendulumVelocity = 0f;
    private Coroutine hitRoutine;

    // Training Dummy Combo Tracker
    private int dummyTotalDamageReceived = 0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
        originalColor = sr.color;
        currentHealth = health;

        ApplyPresetDefaults();
    }

    public void ApplyPresetDefaults()
    {
        switch (objectType)
        {
            case ObjectType.BreakableCrate:
                health = 3;
                lootOrbCount = 3;
                debrisParticleColor = new Color(0.6f, 0.4f, 0.2f, 1f); // Wood brown
                break;
            case ObjectType.ClayPot:
                health = 1;
                lootOrbCount = 2;
                debrisParticleColor = new Color(0.75f, 0.45f, 0.3f, 1f); // Terracotta orange
                break;
            case ObjectType.TrainingDummy:
                health = 9999; // Infinite dummy HP
                lootOrbCount = 0;
                wobbleIntensity = 0.4f;
                debrisParticleColor = new Color(0.9f, 0.8f, 0.6f, 1f); // Straw yellow
                break;
            case ObjectType.HangingLantern:
                health = 999;
                lootOrbCount = 0;
                wobbleIntensity = 0.5f;
                debrisParticleColor = new Color(1f, 0.8f, 0.3f, 1f); // Warm flame yellow
                break;
            case ObjectType.WoodenSign:
                health = 5;
                lootOrbCount = 1;
                debrisParticleColor = new Color(0.5f, 0.35f, 0.2f, 1f);
                break;
            case ObjectType.Barrel:
                health = 4;
                lootOrbCount = 4;
                debrisParticleColor = new Color(0.45f, 0.3f, 0.15f, 1f);
                break;
        }
    }

    // ── IDamageable Implementation ──────────────────────────────────────
    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        if (objectType == ObjectType.TrainingDummy)
        {
            dummyTotalDamageReceived += damage;
            Debug.Log($"[TrainingDummy] Hit for {damage} dmg! Total combo: {dummyTotalDamageReceived}");
        }
        else
        {
            currentHealth -= damage;
        }

        // Determine hit direction from attacker if available
        float hitDir = 1f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            hitDir = Mathf.Sign(transform.position.x - player.transform.position.x);
            if (hitDir == 0) hitDir = 1f;
        }

        // Trigger visual hit reaction
        TriggerHitReaction(hitDir, damage);

        // Trigger Audio
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        // Check Destruction
        if (currentHealth <= 0 && objectType != ObjectType.TrainingDummy)
        {
            Shatter();
        }
    }

    public void TriggerHitReaction(float hitDirection, int damageAmount)
    {
        if (isBroken) return;

        if (objectType == ObjectType.HangingLantern || objectType == ObjectType.WoodenSign)
        {
            // Pendulum Swing FX
            pendulumVelocity = hitDirection * 35f;
            if (hitRoutine == null)
            {
                hitRoutine = StartCoroutine(PendulumRoutine());
            }
        }
        else
        {
            // Squash & Stretch Directional Wobble
            currentScaleOffset = new Vector3(-hitDirection * wobbleIntensity, wobbleIntensity * 0.8f, 0f);
            if (hitRoutine == null)
            {
                hitRoutine = StartCoroutine(WobbleRoutine());
            }
        }

        // Flash White Highlight
        if (flashWhiteOnHit)
        {
            StartCoroutine(FlashWhiteRoutine());
        }
    }

    private IEnumerator WobbleRoutine()
    {
        float t = 0f;
        while (t < 0.35f || currentScaleOffset.magnitude > 0.02f)
        {
            t += Time.deltaTime;
            currentScaleOffset = Vector3.Lerp(currentScaleOffset, Vector3.zero, Time.deltaTime * wobbleDamping);
            transform.localScale = originalScale + currentScaleOffset;
            yield return null;
        }

        transform.localScale = originalScale;
        currentScaleOffset = Vector3.zero;
        hitRoutine = null;
    }

    private IEnumerator PendulumRoutine()
    {
        float stiffness = 160f;
        float damping = 5f;

        while (Mathf.Abs(pendulumAngle) > 0.2f || Mathf.Abs(pendulumVelocity) > 0.2f)
        {
            float force = -stiffness * pendulumAngle;
            pendulumVelocity += force * Time.deltaTime;
            pendulumVelocity -= damping * pendulumVelocity * Time.deltaTime;
            pendulumAngle += pendulumVelocity * Time.deltaTime;

            transform.localRotation = originalRotation * Quaternion.Euler(0, 0, pendulumAngle);
            yield return null;
        }

        transform.localRotation = originalRotation;
        pendulumAngle = 0f;
        pendulumVelocity = 0f;
        hitRoutine = null;
    }

    private IEnumerator FlashWhiteRoutine()
    {
        if (sr == null) yield break;
        sr.color = Color.white * 1.5f;
        yield return new WaitForSeconds(0.08f);
        sr.color = originalColor;
    }

    public void Shatter()
    {
        if (isBroken) return;
        isBroken = true;

        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        // 1. Shatter Sound
        if (shatterSound != null)
        {
            AudioSource.PlayClipAtPoint(shatterSound, transform.position);
        }

        // 2. Spawn Debris Shards
        SpawnDebrisShards();

        // 3. Spawn Particle Dust Cloud
        SpawnDustParticleCloud();

        // 4. Spawn Loot Orbs
        if (lootOrbCount > 0)
        {
            OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 0.5f, lootOrbCount);
        }

        // 5. Update or Destroy Object
        if (brokenSprite != null)
        {
            sr.sprite = brokenSprite;
            if (col != null) col.enabled = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SpawnDebrisShards()
    {
        if (debrisShardCount <= 0 || sr.sprite == null) return;

        for (int i = 0; i < debrisShardCount; i++)
        {
            GameObject shard = new GameObject($"{gameObject.name}_Shard_{i}");
            float angle = (360f / debrisShardCount) * i + Random.Range(-20f, 20f);
            Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.right;

            shard.transform.position = transform.position + (Vector3)(dir * 0.2f);
            shard.transform.localScale = originalScale * Random.Range(0.35f, 0.6f);

            SpriteRenderer shardSr = shard.AddComponent<SpriteRenderer>();
            shardSr.sprite = sr.sprite;
            shardSr.color = originalColor;
            shardSr.sortingLayerID = sr.sortingLayerID;
            shardSr.sortingOrder = sr.sortingOrder + 1;

            Rigidbody2D rb = shard.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.linearVelocity = dir * explosionForce * Random.Range(0.7f, 1.3f);
            rb.angularVelocity = Random.Range(-360f, 360f);

            CircleCollider2D shardCol = shard.AddComponent<CircleCollider2D>();
            shardCol.isTrigger = true;

            StartCoroutine(FadeAndDestroyShard(shard, shardSr, 1.2f));
        }
    }

    private IEnumerator FadeAndDestroyShard(GameObject shardObj, SpriteRenderer shardSr, float duration)
    {
        float elapsed = 0f;
        Color startCol = shardSr.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            if (shardSr != null)
            {
                shardSr.color = new Color(startCol.r, startCol.g, startCol.b, alpha);
            }
            yield return null;
        }

        if (shardObj != null)
        {
            Destroy(shardObj);
        }
    }

    private void SpawnDustParticleCloud()
    {
        GameObject dustObj = new GameObject($"{gameObject.name}_DustCloud");
        dustObj.transform.position = transform.position;

        ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = dustObj.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor = debrisParticleColor;
        main.gravityModifier = 0.2f;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        psr.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(dustObj, 1.2f);
    }
}
