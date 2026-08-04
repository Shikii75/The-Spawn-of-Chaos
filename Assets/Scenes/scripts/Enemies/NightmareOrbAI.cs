using System.Collections;
using UnityEngine;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;

/// <summary>
/// NightmareOrbAI - God of War Nightmare-inspired floating mob enemy with Neon Core & Ranged Shooting.
/// 
/// Normal Ranged State:
///   Features a vibrant glowing Neon Core. Hovers at a ranged distance maintaining spacing from the player
///   and shoots glowing neon magic projectiles at the player instead of chasing.
/// 
/// Sent Flying State (Once Struck):
///   When hit by the player, it gets sent flying like a volatile rocket missile.
///   Calls Explode() ONLY if it has been hit AND either covers its max fly distance OR collides with an object/entity.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class NightmareOrbAI : MonoBehaviour, IDamageable
{
    [Header("Neon Core Visuals")]
    public Color neonCoreColor = new Color(0.0f, 1.0f, 0.85f, 1.0f);   // Bright Electric Neon Cyan Core
    public Color neonOuterColor = new Color(0.7f, 0.1f, 1.0f, 1.0f);  // Glowing Neon Arcane Outer Ring
    public Color volatileColor = new Color(1.0f, 0.3f, 0.0f, 1.0f);   // Fiery orange / Volatile red when hit

    [Header("Ranged Floating AI")]
    public float moveSpeed = 3.5f;
    public float hoverFrequency = 3.2f;
    public float hoverAmplitude = 0.4f;
    public float detectionRadius = 13.0f;
    public float preferredHoverDistance = 5.5f; // Maintains spacing from player

    [Header("Ranged Shooting")]
    public float shootCooldown = 2.0f;
    public float projectileSpeed = 9.0f;
    public int projectileDamage = 12;

    [Header("Flying Launch Mechanics (God of War Nightmare Style)")]
    public float launchSpeed = 22.0f;
    public float launchSpinSpeed = 720.0f;
    public float maxFlyDistance = 15.0f; // Auto-explodes after traveling this distance if no surface hit

    [Header("Explosion Settings (Only When Hit)")]
    public float explosionRadius = 2.8f;
    public int explosionDamage = 30;
    public int lootOrbCount = 3;
    public LayerMask hitLayers = ~0; // Everything by default

    [Header("Health Restoration Mode")]
    [Tooltip("If true, this orb acts as a floating Health Orb that heals the player on defeat instead of dealing aggressive rocket damage.")]
    public bool isHealthOrbOnly = false;
    public int healthRestoreAmount = 35;

    [Header("Visual Effects")]
    public bool createProceduralSpriteIfMissing = true;
    public float outerRingRotationSpeed = 180f;

    private Rigidbody2D rb;
    private CircleCollider2D circleCol;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private Transform playerTransform;
    private GameObject outerHaloRing;
    private SpriteRenderer outerHaloRenderer;

    private float hoverTimer;
    private float nextShootTime;
    private bool hasBeenHit = false;
    private bool isExploding = false;

    private Vector2 launchStartPosition;
    private Vector2 launchDirection;
    private Vector2 desiredVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circleCol = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Configure physics for floating mob
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (circleCol != null)
        {
            circleCol.isTrigger = false;
            if (circleCol.radius <= 0.05f) circleCol.radius = 0.4f;
        }

        // Create procedural neon core visual sprite if no texture assigned
        if (createProceduralSpriteIfMissing && (spriteRenderer == null || spriteRenderer.sprite == null))
        {
            SetupProceduralVisuals();
        }

        // Setup Health Orb color overrides if configured as Health Orb
        if (isHealthOrbOnly)
        {
            neonCoreColor = new Color(0.1f, 1.0f, 0.5f, 1.0f);
            neonOuterColor = new Color(0.0f, 0.9f, 0.6f, 1.0f);
        }

        SetupOuterHaloRing();
    }

    void Start()
    {
        FindPlayer();
        nextShootTime = Time.time + Random.Range(0.5f, 1.5f);
    }

    void Update()
    {
        if (isExploding) return;

        // Continuously rotate outer arcane halo ring
        if (outerHaloRing != null)
        {
            outerHaloRing.transform.Rotate(0f, 0f, outerRingRotationSpeed * Time.deltaTime);
        }

        if (hasBeenHit)
        {
            // Volatile state: Spin rapidly while rocket-propelled
            transform.Rotate(0, 0, launchSpinSpeed * Time.deltaTime);

            // Stretch along movement direction for dynamic speed trail feel
            if (rb.linearVelocity.sqrMagnitude > 1f)
            {
                float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // Explode if it has covered the max flight distance through mid-air
            float distanceTraveled = Vector2.Distance(transform.position, launchStartPosition);
            if (distanceTraveled >= maxFlyDistance)
            {
                Explode();
            }
            return;
        }

        // --- NORMAL RANGED HOVER & SHOOTING STATE ---
        hoverTimer += Time.deltaTime * hoverFrequency;
        float yOffset = Mathf.Sin(hoverTimer) * hoverAmplitude;

        if (playerTransform == null && Time.frameCount % 30 == 0)
        {
            FindPlayer();
        }

        if (playerTransform != null)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            if (distance <= detectionRadius)
            {
                // Maintain preferred spacing distance from player
                Vector2 dirToPlayer = (playerTransform.position - transform.position).normalized;
                Vector2 targetPos;

                if (distance < preferredHoverDistance - 1.2f)
                {
                    // Back away from player if too close
                    targetPos = (Vector2)transform.position - dirToPlayer * 2.0f + new Vector2(0f, yOffset);
                }
                else if (distance > preferredHoverDistance + 1.2f)
                {
                    // Move closer if too far
                    targetPos = (Vector2)playerTransform.position - dirToPlayer * preferredHoverDistance + new Vector2(0f, yOffset);
                }
                else
                {
                    // Strafe/hover smoothly at preferred distance
                    Vector2 strafeDir = new Vector2(-dirToPlayer.y, dirToPlayer.x);
                    targetPos = (Vector2)transform.position + strafeDir * (Mathf.Sin(Time.time * 2f) * 1.5f) + new Vector2(0f, yOffset);
                }

                Vector2 moveDir = (targetPos - (Vector2)transform.position).normalized;
                desiredVelocity = moveDir * moveSpeed;

                // Ranged Shooting Logic
                if (Time.time >= nextShootTime)
                {
                    nextShootTime = Time.time + shootCooldown;
                    ShootNeonProjectile(dirToPlayer);
                }
            }
            else
            {
                // Idle bobbing
                desiredVelocity = new Vector2(0f, yOffset * moveSpeed * 0.5f);
            }
        }
        else
        {
            desiredVelocity = new Vector2(0f, yOffset * moveSpeed * 0.5f);
        }

        // Pulse visual Neon Core color/glow
        if (spriteRenderer != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f);
            spriteRenderer.color = neonOuterColor * pulse;
        }
    }

    void FixedUpdate()
    {
        if (isExploding) return;

        if (!hasBeenHit)
        {
            rb.linearVelocity = desiredVelocity;
        }
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
        }
    }

    private void ShootNeonProjectile(Vector2 direction)
    {
        GameObject projGO = new GameObject("NightmareNeonProjectile");
        projGO.transform.position = transform.position + (Vector3)(direction * 0.5f);

        NightmareOrbProjectile proj = projGO.AddComponent<NightmareOrbProjectile>();
        proj.Initialize(direction, projectileSpeed, projectileDamage, neonCoreColor);
    }

    /// <summary>
    /// IDamageable interface implementation.
    /// Being hit puts the orb into Sent Flying state!
    /// </summary>
    public void TakeDamage(int damageTaken)
    {
        if (isExploding) return;

        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damageTaken, true, EnemyHitType.ShadowWisp);

        if (!hasBeenHit)
        {
            // Determine launch direction (away from player/attacker)
            Vector2 attackerPos = playerTransform != null ? (Vector2)playerTransform.position : (Vector2)transform.position - Vector2.right;
            Vector2 dir = ((Vector2)transform.position - attackerPos).normalized;

            // Add slight upward arch for satisfying trajectory physics
            dir.y = Mathf.Clamp(dir.y + 0.25f, -0.5f, 0.8f);
            dir.Normalize();

            LaunchAsMissile(dir);
        }
        else
        {
            // If hit a second time while already flying in mid-air, detonate immediately!
            Explode();
        }
    }

    /// <summary>
    /// Rocket launches the Nightmare Orb into volatile flying state.
    /// </summary>
    public void LaunchAsMissile(Vector2 direction)
    {
        hasBeenHit = true;
        launchStartPosition = transform.position;
        launchDirection = direction;

        rb.freezeRotation = false;
        rb.linearVelocity = launchDirection * launchSpeed;

        // Visual feedback: Switch color to volatile fiery orange/red
        if (spriteRenderer != null)
        {
            spriteRenderer.color = volatileColor;
        }

        // Create/enable trail effect
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
            trailRenderer.time = 0.25f;
            trailRenderer.startWidth = 0.5f;
            trailRenderer.endWidth = 0.0f;
            trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            trailRenderer.startColor = volatileColor;
            trailRenderer.endColor = new Color(1f, 0.8f, 0f, 0f);
        }
        trailRenderer.enabled = true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasBeenHit && !isExploding)
        {
            Explode();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenHit && !isExploding && !other.isTrigger)
        {
            Explode();
        }
    }

    /// <summary>
    /// Trigger violent AoE explosion, damage surrounding entities, spawn loot, and play FX.
    /// ONLY called after being hit and covering distance or colliding with an object.
    /// </summary>
    public void Explode()
    {
        if (isExploding) return;
        isExploding = true;

        Vector3 explosionPos = transform.position;

        // 1. Health Restoration (if isHealthOrbOnly or near player)
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
        {
            Health pHealth = pObj.GetComponent<Health>() ?? pObj.GetComponentInParent<Health>();
            if (pHealth != null && (isHealthOrbOnly || Vector2.Distance(explosionPos, pObj.transform.position) <= explosionRadius + 3.0f))
            {
                pHealth.Heal(healthRestoreAmount);
                if (HUDOrbPanel.Instance != null)
                {
                    HUDOrbPanel.Instance.TriggerSplash(OrbType.Health, 1.0f);
                }
            }
        }

        // 2. AoE Damage to all hit targets (skip damage if health orb only)
        if (!isHealthOrbOnly)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionPos, explosionRadius, hitLayers);
            foreach (Collider2D col in hitColliders)
            {
                if (col.gameObject == gameObject) continue;

                IDamageable damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(explosionDamage);
                }
            }
        }

        // 3. Spawn Procedural Explosion FX (Shockwave Ring & Particles)
        CreateExplosionVisualFX(explosionPos);

        // 4. Drop Collectible Loot Orbs (Spawns Health Orbs if Health Orb Mode)
        if (isHealthOrbOnly)
        {
            for (int i = 0; i < lootOrbCount; i++)
            {
                OrbSpawner.SpawnOrb(explosionPos + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.6f), 0f), OrbType.Health, 15);
            }
        }
        else
        {
            OrbSpawner.SpawnLootCluster(explosionPos, lootOrbCount);
        }

        // 5. Destroy mob entity
        Destroy(gameObject);
    }

    private void CreateExplosionVisualFX(Vector3 pos)
    {
        GameObject fx = new GameObject("NightmareExplosionFX");
        fx.transform.position = pos;

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(64, volatileColor);
        sr.sortingOrder = 20;

        fx.AddComponent<ExplosionFXAnim>().Initialize(volatileColor, explosionRadius);
    }

    private void SetupProceduralVisuals()
    {
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        Texture2D texture = new Texture2D(64, 64);
        Vector2 center = new Vector2(32, 32);
        float maxRadius = 30f;

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= maxRadius)
                {
                    float normDist = dist / maxRadius;
                    // Neon Core + Glowing outer iris
                    if (dist < 9f)
                    {
                        texture.SetPixel(x, y, neonCoreColor); // Bright Electric Neon Core
                    }
                    else if (dist < 18f)
                    {
                        texture.SetPixel(x, y, Color.Lerp(Color.white, neonCoreColor, (dist - 9f) / 9f)); // Core Glow Halo
                    }
                    else
                    {
                        float alpha = 1f - Mathf.Pow(normDist, 2f);
                        texture.SetPixel(x, y, new Color(neonOuterColor.r, neonOuterColor.g, neonOuterColor.b, alpha)); // Outer Neon Aura
                    }
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32);
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 10;
    }

    private void SetupOuterHaloRing()
    {
        if (outerHaloRing != null) return;

        outerHaloRing = new GameObject("ArcaneHaloRing");
        outerHaloRing.transform.SetParent(transform);
        outerHaloRing.transform.localPosition = Vector3.zero;
        outerHaloRing.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);

        outerHaloRenderer = outerHaloRing.AddComponent<SpriteRenderer>();
        
        Texture2D ringTex = new Texture2D(64, 64);
        Vector2 center = new Vector2(32, 32);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist >= 20f && dist <= 28f)
                {
                    float angle = Mathf.Atan2(y - 32, x - 32);
                    float dashPattern = Mathf.Sin(angle * 4f); // 4-dash orbiting ring arcs
                    if (dashPattern > -0.2f)
                    {
                        ringTex.SetPixel(x, y, neonOuterColor);
                    }
                    else
                    {
                        ringTex.SetPixel(x, y, Color.clear);
                    }
                }
                else
                {
                    ringTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        ringTex.Apply();

        outerHaloRenderer.sprite = Sprite.Create(ringTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32);
        outerHaloRenderer.sortingOrder = 9;
    }

    private Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = 1f - (dist / radius);
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredHoverDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    /// <summary>
    /// Nested helper animation component for procedural shockwave expansion.
    /// </summary>
    public class ExplosionFXAnim : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Color startColor;
        private float maxRadius;
        private float timer = 0f;
        private float duration = 0.35f;

        public void Initialize(Color color, float radius)
        {
            sr = GetComponent<SpriteRenderer>();
            startColor = color;
            maxRadius = radius;
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            if (progress >= 1.0f)
            {
                Destroy(gameObject);
                return;
            }

            float currentScale = Mathf.Lerp(0f, maxRadius * 2f, Mathf.Sin(progress * Mathf.PI * 0.5f));
            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);
            }
        }
    }
}

/// <summary>
/// Glowing Neon Projectile shot by Nightmare Orb.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class NightmareOrbProjectile : MonoBehaviour
{
    private Rigidbody2D rb;
    private CircleCollider2D col;
    private SpriteRenderer sr;

    private Vector2 moveDir;
    private float moveSpeed;
    private int damageAmount;
    private Color neonColor;
    private float lifetime = 3.5f;

    public void Initialize(Vector2 direction, float speed, int damage, Color color)
    {
        moveDir = direction.normalized;
        moveSpeed = speed;
        damageAmount = damage;
        neonColor = color;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.25f;

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        // Create glowing neon projectile texture
        Texture2D tex = new Texture2D(32, 32);
        Vector2 center = new Vector2(16, 16);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d <= 14f)
                {
                    float a = 1f - (d / 14f);
                    tex.SetPixel(x, y, d < 5f ? Color.white : new Color(neonColor.r, neonColor.g, neonColor.b, a));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
        sr.sortingOrder = 15;

        // Add glow trail
        TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
        tr.time = 0.2f;
        tr.startWidth = 0.35f;
        tr.endWidth = 0.0f;
        tr.material = new Material(Shader.Find("Sprites/Default"));
        tr.startColor = neonColor;
        tr.endColor = new Color(neonColor.r, neonColor.g, neonColor.b, 0f);

        rb.linearVelocity = moveDir * moveSpeed;
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Don't collide with other Nightmare Orbs or projectiles
        if (other.GetComponent<NightmareOrbAI>() != null || other.GetComponent<NightmareOrbProjectile>() != null) return;

        if (other.CompareTag("Player"))
        {
            Health pHealth = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();
            if (pHealth != null)
            {
                pHealth.TakeDamage(damageAmount);
            }
            if (HUDOrbPanel.Instance != null)
            {
                HUDOrbPanel.Instance.TriggerSplash(OrbType.Health, 0.6f);
            }
            ExplodeImpact();
        }
        else if (!other.isTrigger)
        {
            ExplodeImpact();
        }
    }

    private void ExplodeImpact()
    {
        GameObject fx = new GameObject("NeonProjectileImpact");
        fx.transform.position = transform.position;

        SpriteRenderer isr = fx.AddComponent<SpriteRenderer>();
        isr.sprite = sr.sprite;
        isr.color = neonColor;
        isr.sortingOrder = 20;

        fx.AddComponent<NightmareOrbAI.ExplosionFXAnim>().Initialize(neonColor, 1.2f);
        Destroy(gameObject);
    }
}
