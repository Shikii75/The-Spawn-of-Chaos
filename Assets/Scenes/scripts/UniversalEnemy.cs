using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class UniversalEnemy : MonoBehaviour, IDamageable
{
    public enum MobBehavior
    {
        SimpleWalkTouchDamage,  // Walks back/forth, contact damage, 1 movement animation
        PatrolChaseAttack,      // Patrols, chases & attacks when player spotted, 3-4 anims
        PatrolIdleWaitAttack,   // Patrols with wait times, has multiple attacks (heavy double damage)
        FlyingChaseAttack       // Flying movement, ignores gravity, dash attacks
    }

    [Header("Behavior Settings")]
    [Tooltip("Choose the AI archetype for this mob.")]
    public MobBehavior behavior = MobBehavior.PatrolChaseAttack;

    [Tooltip("If checked, the mob stands still guarding its starting position until it spots the player (perfect for Dojo/Pantheon rooms).")]
    public bool standStillUntilSpotted = false;

    [Header("Movement & Patrol")]
    public float moveSpeed = 3f;
    public float flySpeed = 2.5f;
    public Transform leftPatrolPoint;
    public Transform rightPatrolPoint;
    [Tooltip("Time (seconds) Patrol Mobs 2 wait at each patrol point before turning.")]
    public float waitTimeAtPoints = 1f;

    [Header("Edge & Wall Detection (Ground Mobs)")]
    [Tooltip("If checked, walking mobs turn around before walking off ledges.")]
    public bool avoidLedges = true;
    public Transform ledgeCheckOrigin;
    public LayerMask groundLayer;

    [Header("Sensors & Combat Ranges")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.5f;

    [Header("Damage & Health Config")]
    public int maxHealth = 30;
    public int baseDamage = 4;
    [Tooltip("Cooldown between contact damage hits for simple touch-damage mobs.")]
    public float contactDamageCooldown = 1f;

    [Header("Flying Dash Attack Settings")]
    [Tooltip("The speed multiplier during a flying dash attack.")]
    public float flyingDashMultiplier = 2.5f;
    public float flyingDashDuration = 0.4f;

    [Header("Multi-Attack Config (Patrol Mobs 2)")]
    [Tooltip("Number of attack variants this mob has (up to 3).")]
    [Range(1, 3)]
    public int attackVariantCount = 1;
    [Tooltip("Chance (0 to 1) that an attack triggers a Heavy Attack dealing double damage.")]
    [Range(0f, 1f)]
    public float heavyAttackChance = 0.25f;

    [Header("References")]
    public Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private SpriteJuice spriteJuice;

    // Internal State
    private int currentHealth;
    private bool movingRight = true;
    private bool isDead = false;
    private bool isChasing = false;
    private bool isDashing = false;
    private float lastAttackTime;
    private float lastContactDamageTime;
    private float waitTimer;
    private float originalGravity;
    private Vector3 startingPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = anim ?? GetComponent<Animator>();
        spriteJuice = GetComponent<SpriteJuice>();

        currentHealth = maxHealth;
        startingPosition = transform.position;
        originalGravity = rb.gravityScale;

        // Configure physics based on behavior
        if (behavior == MobBehavior.FlyingChaseAttack)
        {
            rb.gravityScale = 0f;
        }

        // Find player by tag
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;
    }

    void Update()
    {
        if (isDead || isDashing) return;

        // 1. Evaluate player sensors
        float distanceToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;
        bool playerSpotted = distanceToPlayer <= detectionRange;
        isChasing = playerSpotted && behavior != MobBehavior.SimpleWalkTouchDamage;

        // 2. State Actions Loop
        if (isChasing)
        {
            if (distanceToPlayer <= attackRange)
            {
                TriggerAttack();
            }
            else
            {
                ChaseBehavior(distanceToPlayer);
            }
        }
        else
        {
            PatrolBehavior();
        }
    }

    private void PatrolBehavior()
    {
        // Stand still/guard behavior in Dojo/Pantheon rooms
        if (standStillUntilSpotted)
        {
            StopMoving();
            return;
        }

        // Flying Patrol behavior
        if (behavior == MobBehavior.FlyingChaseAttack)
        {
            FlyingPatrol();
            return;
        }

        if (leftPatrolPoint == null || rightPatrolPoint == null)
        {
            StopMoving();
            return;
        }

        // Check if currently waiting at a patrol point (Patrol Mobs 2)
        if (behavior == MobBehavior.PatrolIdleWaitAttack && waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            StopMoving();
            return;
        }

        // Evaluate platform edge or walls
        if (avoidLedges && ledgeCheckOrigin != null)
        {
            bool groundAhead = Physics2D.Raycast(ledgeCheckOrigin.position, Vector2.down, 0.5f, groundLayer);
            if (!groundAhead)
            {
                TurnAround();
            }
        }

        Transform targetPoint = movingRight ? rightPatrolPoint : leftPatrolPoint;
        float direction = targetPoint.position.x > transform.position.x ? 1f : -1f;

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        FlipSprite(direction);
        SetAnimMoving(true);

        // Arrived at patrol point
        if (Mathf.Abs(transform.position.x - targetPoint.position.x) < 0.2f)
        {
            if (behavior == MobBehavior.PatrolIdleWaitAttack)
            {
                waitTimer = waitTimeAtPoints;
            }
            movingRight = !movingRight;
        }
    }

    private void FlyingPatrol()
    {
        if (leftPatrolPoint == null || rightPatrolPoint == null)
        {
            StopMoving();
            return;
        }

        Transform targetPoint = movingRight ? rightPatrolPoint : leftPatrolPoint;
        Vector2 flyDir = (targetPoint.position - transform.position).normalized;

        rb.linearVelocity = flyDir * flySpeed;
        FlipSprite(flyDir.x);
        SetAnimMoving(true);

        if (Vector2.Distance(transform.position, targetPoint.position) < 0.3f)
        {
            movingRight = !movingRight;
        }
    }

    private void ChaseBehavior(float distanceToPlayer)
    {
        if (player == null) return;

        SetAnimMoving(true);

        if (behavior == MobBehavior.FlyingChaseAttack)
        {
            Vector2 chaseDir = (player.position - transform.position).normalized;
            rb.linearVelocity = chaseDir * flySpeed;
            FlipSprite(chaseDir.x);
        }
        else
        {
            // Ground Chase
            float chaseDir = player.position.x > transform.position.x ? 1f : -1f;
            rb.linearVelocity = new Vector2(chaseDir * moveSpeed, rb.linearVelocity.y);
            FlipSprite(chaseDir);
        }
    }

    private void TriggerAttack()
    {
        StopMoving();

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;

            if (behavior == MobBehavior.FlyingChaseAttack)
            {
                StartCoroutine(FlyingDashAttackRoutine());
            }
            else
            {
                PerformMeleeStrike();
            }
        }
    }

    private void PerformMeleeStrike()
    {
        int dmg = baseDamage;
        bool isHeavy = false;

        // Determine attack variant (Patrol Mobs 2 heavy attacks deal double damage)
        if (behavior == MobBehavior.PatrolIdleWaitAttack && attackVariantCount > 1)
        {
            int randAttack = Random.Range(1, attackVariantCount + 1);
            isHeavy = Random.value < heavyAttackChance;

            if (isHeavy)
            {
                dmg *= 2; // Double damage for heavy strike!
            }

            anim.SetTrigger("attack" + randAttack);
        }
        else
        {
            // Default single attack
            anim.SetTrigger("attack");
        }

        // Trigger deal damage checking (after a short delay matching visual strike frame)
        StartCoroutine(ExecuteDamageCheck(dmg));
    }

    private IEnumerator ExecuteDamageCheck(int damageDealt)
    {
        yield return new WaitForSeconds(0.15f); // Hit frame alignment delay

        if (player != null && Vector2.Distance(transform.position, player.position) <= attackRange + 0.3f)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damageDealt);
                Debug.Log($"{name} hit player for {damageDealt} damage!");
            }
        }
    }

    private IEnumerator FlyingDashAttackRoutine()
    {
        isDashing = true;
        SetAnimMoving(false);
        anim.SetTrigger("attack");

        Vector2 dashDir = (player.position - transform.position).normalized;
        rb.linearVelocity = dashDir * (flySpeed * flyingDashMultiplier);
        FlipSprite(dashDir.x);

        float elapsed = 0f;
        while (elapsed < flyingDashDuration)
        {
            // Deal damage on physical impact during the dash charge
            if (player != null && Vector2.Distance(transform.position, player.position) < 0.9f)
            {
                Health pH = player.GetComponent<Health>();
                if (pH != null)
                {
                    pH.TakeDamage(baseDamage);
                    break;
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.2f); // Settle time
        isDashing = false;
    }

    private void StopMoving()
    {
        rb.linearVelocity = behavior == MobBehavior.FlyingChaseAttack ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        SetAnimMoving(false);
    }

    private void SetAnimMoving(bool moving)
    {
        if (anim != null)
        {
            anim.SetBool("isMoving", moving);
        }
    }

    private void TurnAround()
    {
        movingRight = !movingRight;
        FlipSprite(movingRight ? 1f : -1f);
    }

    private void FlipSprite(float directionX)
    {
        if (directionX > 0f)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else if (directionX < 0f)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    // IDamageable Implementation
    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (anim != null)
        {
            anim.SetTrigger("hit");
        }

        // Trigger SpriteJuice if attached
        if (spriteJuice != null)
        {
            Vector2 hitDir = player != null ? (Vector2)(transform.position - player.position).normalized : Vector2.right;
            spriteJuice.PlayHitReaction(hitDir, 4f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        StopMoving();

        if (anim != null)
        {
            anim.SetTrigger("die");
        }

        // Disable collision and drop gravity to fall naturally if flying
        GetComponent<Collider2D>().enabled = false;
        if (behavior == MobBehavior.FlyingChaseAttack)
        {
            rb.gravityScale = originalGravity; // Let flying mobs drop when dead
        }

        Destroy(gameObject, 2f);
    }

    // Contact Damage for Simple Mobs
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || behavior != MobBehavior.SimpleWalkTouchDamage) return;

        if (collision.gameObject.CompareTag("Player") && Time.time >= lastContactDamageTime + contactDamageCooldown)
        {
            Health pH = collision.gameObject.GetComponent<Health>();
            if (pH == null) pH = collision.gameObject.GetComponentInParent<Health>();

            if (pH != null)
            {
                lastContactDamageTime = Time.time;
                pH.TakeDamage(baseDamage);
                Debug.Log($"{name} dealt contact damage to player.");
            }
        }
    }

    // Visualize ranges in Unity Editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
