using System.Collections;
using UnityEngine;

/// <summary>
/// BeeMobAI - Controls flying swarm AI for both standard BeeMob and FatBeeMob.
///
/// Features:
/// 1. Flying Swarm Movement:
///    - Gentle sine-wave vertical hovering when idling.
///    - Line-of-sight detection before aggroing onto the player.
///    - Multi-whisker obstacle avoidance: raycasts ahead for terrain/walls and smoothly steers around/over them.
/// 2. Heavy Sting Contact Damage:
///    - Always deals damage upon touching the player collider (10 dmg for BeeMob, 18 dmg for FatBeeMob).
///    - 1.0s contact cooldown with a physical knockback impulse applied to the player.
/// 3. Dynamic Squash & Stretch Animation:
///    - Organic velocity-based elongation along the flight vector.
///    - High-frequency wing-flap / breathing buzz pulse in idle and pursuit.
///    - Snappy spring-damped turn squash when reversing flight direction.
///    - Heavy recoil impact squash on stinging the player or taking damage.
///    - Extreme launch stretch & settle cushion during the 3-bee fountain pop.
/// 4. Hit Reactions:
///    - Sprite red/white hit flash and slight knockback away from attack source.
/// 5. Death & Splitting:
///    - Standard BeeMob drops 1 collectible orb and expires.
///    - FatBeeMob drops 2 collectible orbs and bursts into 3 BeeMobs with a fountain pop upward arc.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BeeMobAI : MonoBehaviour, IDamageable
{
    [Header("Mob Configuration")]
    [Tooltip("Check if this entity is the large FatBeeMob that splits upon defeat.")]
    public bool isFatBee = false;

    [Tooltip("Small BeeMob prefab spawned in a trio when FatBeeMob is defeated.")]
    public GameObject beeMobPrefab;

    [Header("Health & Combat Stats")]
    public int maxHealth = 20;
    public int contactDamage = 10;
    public float contactCooldown = 1.0f;
    public float playerKnockbackForce = 6.5f;

    [Header("Flight & Detection")]
    public float flySpeed = 3.2f;
    public float acceleration = 8.0f;
    public float detectionRange = 8.5f;
    public float hoverBobFrequency = 3.5f;
    public float hoverBobAmplitude = 0.35f;

    [Header("Obstacle Detection & Avoidance")]
    public float obstacleCheckDistance = 1.4f;
    public LayerMask obstacleLayerMask;

    [Header("Squash & Stretch Settings")]
    [Tooltip("Enable dynamic squash & stretch physics for organic flight movement.")]
    public bool enableSquashAndStretch = true;

    [Tooltip("How strongly movement velocity stretches the sprite forward.")]
    [Range(0f, 0.5f)]
    public float velocityStretchFactor = 0.22f;

    [Tooltip("Amplitude of the idle wing-flap / breathing buzz squash.")]
    [Range(0f, 0.2f)]
    public float flapSquashAmplitude = 0.07f;

    [Tooltip("Frequency of the rapid bee wing-flap buzz squash.")]
    public float flapSquashFrequency = 16f;

    [Tooltip("Squash magnitude when reversing horizontal direction.")]
    [Range(0f, 0.5f)]
    public float turnSquashAmount = 0.24f;

    [Tooltip("Squash magnitude on taking damage or contact sting impact.")]
    [Range(0f, 0.6f)]
    public float impactSquashAmount = 0.32f;

    [Header("Invulnerability & Spawn Protection")]
    [Tooltip("Duration in seconds that spawned BeeMobs are invulnerable to player attacks after bursting from FatBeeMob.")]
    public float spawnInvulnerabilityDuration = 0.85f;

    private int currentHealth;
    private bool isDead = false;
    private bool isFountainPopping = false;
    private bool isInvulnerable = false;
    private float lastContactTime = -10f;
    private Vector3 initialSpawnPos;
    private Vector2 currentVelocity;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private Animator anim;
    private Transform playerTransform;

    private Color originalColor = Color.white;
    private Coroutine flashRoutine;

    // Squash & Stretch Internal State
    private Vector3 baseScale = Vector3.one;
    private float facingDir = 1f;
    private Vector2 springSquashOffset = Vector2.zero;
    private Vector2 springSquashVelocity = Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        if (sr != null)
        {
            originalColor = sr.color;
        }

        // Cache base scale and initial facing direction
        baseScale = new Vector3(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y), transform.localScale.z);
        facingDir = Mathf.Sign(transform.localScale.x);
        if (Mathf.Approximately(facingDir, 0f)) facingDir = 1f;

        // Configure Rigidbody2D for flying physics
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (obstacleLayerMask.value == 0)
        {
            // Default to non-player solid layers
            obstacleLayerMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "UI");
        }

        currentHealth = maxHealth;
        initialSpawnPos = transform.position;

        // Resolve beeMobPrefab if this is a FatBee and it was unassigned
        EnsureBeeMobPrefabAssigned();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (isDead || isFountainPopping) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                HoverIdle();
                UpdateFacing();
                return;
            }
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distToPlayer <= detectionRange && HasLineOfSightToPlayer())
        {
            PursuePlayer();
        }
        else
        {
            HoverIdle();
        }

        UpdateFacing();
    }

    private void LateUpdate()
    {
        if (isDead) return;

        UpdateSquashAndStretch();
    }

    private void HoverIdle()
    {
        // Gentle sinusoidal bobbing around home position
        float bobY = Mathf.Sin(Time.time * hoverBobFrequency) * hoverBobAmplitude;
        Vector2 targetPos = new Vector2(initialSpawnPos.x, initialSpawnPos.y + bobY);
        Vector2 toTarget = targetPos - (Vector2)transform.position;

        currentVelocity = Vector2.MoveTowards(currentVelocity, toTarget * 1.5f, acceleration * Time.deltaTime);
        rb.linearVelocity = currentVelocity;
    }

    private void PursuePlayer()
    {
        Vector2 toPlayer = (playerTransform.position - transform.position);
        Vector2 moveDir = toPlayer.normalized;

        // Obstacle Avoidance: cast feeler rays forward, up-diagonal, and down-diagonal
        Vector2 avoidance = CalculateObstacleAvoidance(moveDir);
        Vector2 finalDir = (moveDir + avoidance).normalized;

        // Add subtle flying wobble to make swarm movement feel organic
        float wobble = Mathf.Sin(Time.time * hoverBobFrequency * 1.5f) * 0.25f;
        Vector2 perpendicular = new Vector2(-finalDir.y, finalDir.x) * wobble;
        finalDir = (finalDir + perpendicular).normalized;

        currentVelocity = Vector2.MoveTowards(currentVelocity, finalDir * flySpeed, acceleration * Time.deltaTime);
        rb.linearVelocity = currentVelocity;
    }

    /// <summary>
    /// Updates sprite facing direction and triggers turn squash on horizontal reversal.
    /// </summary>
    private void UpdateFacing()
    {
        float vx = rb != null ? rb.linearVelocity.x : currentVelocity.x;
        if (Mathf.Abs(vx) > 0.2f)
        {
            float newFacing = Mathf.Sign(vx);
            if (!Mathf.Approximately(newFacing, facingDir))
            {
                facingDir = newFacing;
                TriggerTurnSquash();
            }
        }
    }

    /// <summary>
    /// Evaluates dynamic squash and stretch and applies it smoothly to transform.localScale.
    /// </summary>
    private void UpdateSquashAndStretch()
    {
        if (!enableSquashAndStretch)
        {
            transform.localScale = new Vector3(baseScale.x * facingDir, baseScale.y, baseScale.z);
            return;
        }

        // 1. Velocity-based stretch and perpendicular squash
        float speed = rb != null ? rb.linearVelocity.magnitude : currentVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.5f, flySpeed));
        float velStretch = speedRatio * velocityStretchFactor;

        // 2. High-speed wing-flap buzz / breathing squash
        float flap = Mathf.Sin(Time.time * flapSquashFrequency) * flapSquashAmplitude;
        float hoverBob = Mathf.Cos(Time.time * hoverBobFrequency) * (flapSquashAmplitude * 0.5f);
        float idleBuzz = flap + hoverBob;

        // Target spring offset (stretch horizontally along flight, contract vertically)
        Vector2 targetOffset = new Vector2(velStretch + idleBuzz, -velStretch * 0.65f - idleBuzz);

        // 3. Smooth spring damping towards target offset (smooths impulses from turns, impacts, pops)
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        springSquashOffset = Vector2.SmoothDamp(
            springSquashOffset,
            targetOffset,
            ref springSquashVelocity,
            0.08f,
            16f,
            dt
        );

        // Safety bounds to prevent extreme deformation
        springSquashOffset.x = Mathf.Clamp(springSquashOffset.x, -0.45f, 0.65f);
        springSquashOffset.y = Mathf.Clamp(springSquashOffset.y, -0.45f, 0.65f);

        // 4. Composite final local scale
        float finalX = baseScale.x * facingDir * (1f + springSquashOffset.x);
        float finalY = baseScale.y * (1f + springSquashOffset.y);
        transform.localScale = new Vector3(finalX, finalY, baseScale.z);
    }

    /// <summary>
    /// Triggers an immediate squash compression on turn direction change.
    /// </summary>
    public void TriggerTurnSquash()
    {
        if (!enableSquashAndStretch) return;
        springSquashOffset = new Vector2(-turnSquashAmount, turnSquashAmount * 0.65f);
    }

    /// <summary>
    /// Triggers a sudden impact squash (or forward lunge stretch).
    /// </summary>
    public void TriggerImpactSquash(bool isHorizontalLunge = false)
    {
        if (!enableSquashAndStretch) return;
        if (isHorizontalLunge)
        {
            // Lunge stretch forward
            springSquashOffset = new Vector2(impactSquashAmount * 1.15f, -impactSquashAmount * 0.7f);
        }
        else
        {
            // Heavy impact compression
            springSquashOffset = new Vector2(-impactSquashAmount, impactSquashAmount * 0.85f);
        }
    }

    /// <summary>
    /// Directly applies a custom squash/stretch impulse offset.
    /// </summary>
    public void TriggerSquashOffset(Vector2 offset)
    {
        if (!enableSquashAndStretch) return;
        springSquashOffset = offset;
    }

    /// <summary>
    /// Checks for direct line of sight to player ignoring triggers, enemies, and self.
    /// </summary>
    private bool HasLineOfSightToPlayer()
    {
        if (playerTransform == null) return false;

        Vector2 origin = transform.position;
        Vector2 target = playerTransform.position;
        Vector2 dir = target - origin;
        float dist = dir.magnitude;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir.normalized, dist, obstacleLayerMask);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("enemy") || hit.collider.CompareTag("Enemy")) continue;
            if (hit.collider.gameObject == playerTransform.gameObject || hit.collider.transform.IsChildOf(playerTransform))
            {
                return true; // Direct sight to player!
            }

            // Solid obstacle in the way
            return false;
        }

        return true;
    }

    /// <summary>
    /// Probes ahead for walls and terrain, returning an avoidance steering vector if blocked.
    /// </summary>
    private Vector2 CalculateObstacleAvoidance(Vector2 forwardDir)
    {
        Vector2 origin = transform.position;
        Vector2 avoidance = Vector2.zero;

        // Forward feeler
        RaycastHit2D hitCenter = CastObstacle(origin, forwardDir, obstacleCheckDistance);

        // Diagonal feelers
        Vector2 upDiagonal = (Quaternion.Euler(0, 0, 35f) * forwardDir);
        Vector2 downDiagonal = (Quaternion.Euler(0, 0, -35f) * forwardDir);

        RaycastHit2D hitUp = CastObstacle(origin, upDiagonal, obstacleCheckDistance * 0.85f);
        RaycastHit2D hitDown = CastObstacle(origin, downDiagonal, obstacleCheckDistance * 0.85f);

        if (hitCenter.collider != null)
        {
            // Steer away from the obstruction normal, with a preference for upward flight over terrain
            avoidance += hitCenter.normal * 1.5f;
            avoidance += Vector2.up * 1.2f;
        }
        else if (hitDown.collider != null)
        {
            avoidance += Vector2.up * 1.0f;
        }
        else if (hitUp.collider != null)
        {
            avoidance += Vector2.down * 1.0f;
        }

        return avoidance;
    }

    private RaycastHit2D CastObstacle(Vector2 origin, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, obstacleLayerMask);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("enemy") || hit.collider.CompareTag("Enemy")) continue;

            return hit;
        }
        return default;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.gameObject);
    }

    private void TryDealContactDamage(GameObject target)
    {
        if (isDead || target == null) return;

        if (target.CompareTag("Player") && Time.time >= lastContactTime + contactCooldown)
        {
            Health playerHealth = target.GetComponent<Health>() ?? target.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                lastContactTime = Time.time;
                playerHealth.TakeDamage(contactDamage);
                Debug.Log($"[{name}] Dealt heavy sting contact damage ({contactDamage} HP) to Player.");

                // Trigger forward sting lunge squash & stretch
                TriggerImpactSquash(isHorizontalLunge: true);

                // Apply physical knockback push to player
                Rigidbody2D pRb = target.GetComponent<Rigidbody2D>();
                if (pRb != null)
                {
                    Vector2 pushDir = (target.transform.position - transform.position).normalized;
                    if (pushDir == Vector2.zero) pushDir = Vector2.right;
                    pRb.linearVelocity = new Vector2(pushDir.x * playerKnockbackForce, Mathf.Max(pRb.linearVelocity.y, 2.5f));
                }

                // Bounce bee back slightly upon sting
                Vector2 bounceDir = (transform.position - target.transform.position).normalized;
                currentVelocity = bounceDir * (flySpeed * 0.8f);
                rb.linearVelocity = currentVelocity;
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        // Ignore damage if dead, invulnerable during spawn pop, or in pop motion
        if (isDead || isInvulnerable || isFountainPopping) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth);

        // Flash visual feedback
        if (sr != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlashRoutine());
        }

        // Heavy hit recoil squash
        TriggerImpactSquash(isHorizontalLunge: false);

        // Slight knockback push away from player
        if (playerTransform != null)
        {
            Vector2 hitDir = (transform.position - playerTransform.position).normalized;
            currentVelocity = hitDir * 3.5f;
            rb.linearVelocity = currentVelocity;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        sr.color = new Color(1f, 0.35f, 0.35f, 1f);
        yield return new WaitForSeconds(0.12f);
        sr.color = originalColor;
    }

    /// <summary>
    /// Launches the mob in a physics fountain pop arc before resuming flight.
    /// Used when 3 BeeMobs burst from a defeated FatBeeMob.
    /// Protects newly spawned bees with invulnerability from the ongoing player weapon attack.
    /// </summary>
    public void LaunchFountainPop(Vector2 launchVelocity)
    {
        StartCoroutine(FountainPopRoutine(launchVelocity));
    }

    private IEnumerator FountainPopRoutine(Vector2 launchVelocity)
    {
        isFountainPopping = true;
        isInvulnerable = true;

        // Visual feedback: bright golden-white spawn burst flash
        if (sr != null)
        {
            sr.color = new Color(1f, 1f, 0.45f, 1f);
        }

        rb.gravityScale = 1.6f;
        rb.linearVelocity = launchVelocity;

        // Visual stretch along launch arc
        TriggerSquashOffset(new Vector2(0.45f, -0.32f));

        // Arc airtime
        yield return new WaitForSeconds(0.45f);

        // Smoothly settle back into floating swarm flight
        float elapsed = 0f;
        float easeDuration = 0.25f;
        while (elapsed < easeDuration)
        {
            elapsed += Time.deltaTime;
            rb.gravityScale = Mathf.Lerp(1.6f, 0f, elapsed / easeDuration);
            yield return null;
        }

        rb.gravityScale = 0f;
        initialSpawnPos = transform.position;

        // Restore original color
        if (sr != null)
        {
            sr.color = originalColor;
        }

        // Landing cushion squash upon resuming flight
        TriggerSquashOffset(new Vector2(-0.30f, 0.22f));
        isFountainPopping = false;

        // Maintain brief invulnerability window so player must land a new distinct attack
        yield return new WaitForSeconds(Mathf.Max(0.1f, spawnInvulnerabilityDuration - 0.70f));
        isInvulnerable = false;
    }

    private void EnsureBeeMobPrefabAssigned()
    {
        if (!isFatBee || beeMobPrefab != null) return;

        // Fallback 1: Resources folder
        beeMobPrefab = Resources.Load<GameObject>("Prefabs/Enemies/BeeMob");
        if (beeMobPrefab == null)
        {
            beeMobPrefab = Resources.Load<GameObject>("BeeMob");
        }

        // Fallback 2: AssetDatabase in Editor
#if UNITY_EDITOR
        if (beeMobPrefab == null)
        {
            beeMobPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/BeeMob.prefab");
        }
#endif
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (col != null) col.enabled = false;
        rb.linearVelocity = Vector2.zero;

        if (isFatBee)
        {
            // 1. Drop 2 Collectible Orbs on death
            SpawnOfChaos.Systems.OrbSpawner.SpawnLootCluster(transform.position, 2);

            // 2. Ensure beeMobPrefab is loaded
            EnsureBeeMobPrefabAssigned();

            // 3. Fountain Pop: Spawn 3 small BeeMobs fanning upward and out
            if (beeMobPrefab != null)
            {
                Vector2[] popVectors = new Vector2[]
                {
                    new Vector2(-3.6f, 5.4f), // Left pop
                    new Vector2(0f, 6.2f),    // Center pop
                    new Vector2(3.6f, 5.4f)   // Right pop
                };

                Collider2D[] childCols = new Collider2D[3];

                for (int i = 0; i < 3; i++)
                {
                    Vector3 spawnOffset = new Vector3((i - 1) * 0.45f, 0.4f, 0f);
                    GameObject childBee = Instantiate(beeMobPrefab, transform.position + spawnOffset, Quaternion.identity);
                    childBee.name = $"BeeMob_Spawned_{i + 1}";

                    // Ensure high sorting order for crisp visibility
                    SpriteRenderer childSr = childBee.GetComponentInChildren<SpriteRenderer>();
                    if (childSr != null)
                    {
                        childSr.sortingOrder = 5;
                    }

                    BeeMobAI childAI = childBee.GetComponent<BeeMobAI>();
                    if (childAI != null)
                    {
                        childAI.isFatBee = false;
                        childAI.LaunchFountainPop(popVectors[i]);
                    }

                    childCols[i] = childBee.GetComponent<Collider2D>();
                }

                // Prevent child bees from snagging or pushing each other during launch
                for (int a = 0; a < 3; a++)
                {
                    for (int b = a + 1; b < 3; b++)
                    {
                        if (childCols[a] != null && childCols[b] != null)
                        {
                            Physics2D.IgnoreCollision(childCols[a], childCols[b], true);
                        }
                    }
                }

                Debug.Log($"[{name}] FatBeeMob burst into 3 protected BeeMobs with fountain pop!");
            }
            else
            {
                Debug.LogError($"[{name}] Failed to spawn 3 BeeMobs: beeMobPrefab could not be resolved!");
            }
        }
        else
        {
            // Small BeeMob drops 1 Collectible Orb
            SpawnOfChaos.Systems.OrbSpawner.SpawnLootCluster(transform.position, 1);
        }

        // Brief death fade out
        StartCoroutine(DeathFadeOut());
    }

    private IEnumerator DeathFadeOut()
    {
        float elapsed = 0f;
        float duration = 0.25f;
        Color startCol = sr != null ? sr.color : Color.white;
        Vector3 deathStartScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (sr != null)
            {
                Color c = startCol;
                c.a = Mathf.Lerp(1f, 0f, elapsed / duration);
                sr.color = c;
            }
            transform.localScale = Vector3.Lerp(deathStartScale, Vector3.zero, elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
        }
    }
}
