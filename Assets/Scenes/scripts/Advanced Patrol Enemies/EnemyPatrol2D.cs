using UnityEngine;

public class EnemyPatrol2D : MonoBehaviour, IDamageable
{
    [Header("Targeting")]
    [Tooltip("Primary target slot. Use this for the player or a destructible object.")]
    public Transform TargetA;

    [Tooltip("Secondary target slot. The enemy chooses the closest live target.")]
    public Transform TargetB;

    [Header("Movement & Combat")]
    public float moveSpeed = 2.5f;
    public float dashSpeed = 9f;
    public float attackRange = 1.4f;
    public float dashRange = 4f;
    public float dashDuration = 0.35f;
    public int attackDamage = 1;
    public int dashDamage = 2;
    public float attackCooldown = 1.0f;
    public float dashCooldown = 2.0f;
    public float knockbackDuration = 0.3f;

    [Header("Patrol Settings")]
    [Tooltip("Distance the enemy patrols left and right from its starting position.")]
    public float patrolRadius = 5f;

    [Header("Sprite Settings")]
    [Tooltip("Check this if your sprite asset faces right by default. Uncheck if it faces left.")]
    public bool spriteFacesRight = false;

    [Header("Animator Parameters")]
    public string strwalkBoolName = "strwalk";
    public string slashTriggerName = "slash";
    public string sDashTriggerName = "sdash";
    public string knockbackTriggerName = "knockback";

    public int maxHealth = 100;
    private int currentHealth;

    private Rigidbody2D rb;
    private Animator animator;
    private Transform currentTarget;
    private bool isDashing;
    private bool isKnockedBack;
    private float dashEndTime;
    private float nextAttackTime;
    private float nextDashTime;
    private Vector2 dashDirection;
    private Transform currentPatrolTarget;
    
    private Vector3 initialScale;
    private float patrolDirection = 1f;
    private float startX;
    
    // Animator validation
    private System.Collections.Generic.HashSet<string> _animParamNames = new System.Collections.Generic.HashSet<string>();
    public bool verboseLogs = true;

    [Header("Debug / Fallback")]
    [Tooltip("If true, will use Animator.Play with the provided state names to force a state during testing.")]
    public bool forceDirectPlay = false;
    public string slashStateName = "slash";
    public string sDashStateName = "sdash";
    public string knockbackStateName = "knockback";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (rb == null)
        {
            Debug.LogWarning("EnemyPatrol2D requires a Rigidbody2D component.", this);
        }

        if (animator == null)
        {
            Debug.LogWarning("EnemyPatrol2D requires an Animator component.", this);
        }
        else
        {
            // Cache animator parameter names for quick checks
            foreach (var p in animator.parameters)
            {
                _animParamNames.Add(p.name);
            }

            ValidateAnimatorParameters();
        }

        currentHealth = maxHealth;
        initialScale = transform.localScale;
    }

    private void Start()
    {
        // Force/clamp damage values to bypass Inspector serialization overrides
        attackDamage = 1; // 0.5 units of health (1 HP)
        dashDamage = 2;   // 1 unit of health (2 HP)

        // Clamp dash duration to a safe, reasonable platformer range
        if (dashDuration > 0.6f)
        {
            dashDuration = 0.35f;
        }

        startX = transform.position.x;

        // Auto-assign player as TargetA if targets are unassigned
        if (TargetA == null && TargetB == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                TargetA = player.transform;
                if (verboseLogs) Debug.Log($"[EnemyPatrol2D] {name} auto-assigned player as TargetA.");
            }
        }
    }

    private void Update()
    {
        if (isKnockedBack)
        {
            return;
        }

        currentTarget = ChooseClosestTarget();

        bool targetInDashRange = currentTarget != null && Vector2.Distance(transform.position, currentTarget.position) <= dashRange;

        if (!targetInDashRange)
        {
            PerformPatrol();
            UpdateFacing(0f);
            return;
        }

        if (isDashing)
        {
            UpdateFacing(0f);
            return;
        }

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance <= attackRange)
        {
            BeginAttack();
            if (currentTarget != null)
            {
                UpdateFacing(Mathf.Sign(currentTarget.position.x - transform.position.x));
            }
            return;
        }

        if (Time.time >= nextDashTime)
        {
            BeginDash();
            return;
        }

        ChaseTarget(currentTarget.position);
        UpdateFacing(0f);
    }

    private void FixedUpdate()
    {
        if (isKnockedBack)
        {
            return;
        }

        if (isDashing)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(dashDirection.x * dashSpeed, rb.linearVelocity.y);
            }

            // Wall and Ledge Check during active dash to prevent flying off platforms
            float extentX = 0.5f;
            float extentY = 0.5f;
            var boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider != null)
            {
                extentX = boxCollider.size.x * 0.5f * Mathf.Abs(transform.localScale.x);
                extentY = boxCollider.size.y * 0.5f * Mathf.Abs(transform.localScale.y);
            }

            bool hitWall = false;
            Vector2 checkOrigin = (Vector2)transform.position + new Vector2(0f, -extentY * 0.3f);
            RaycastHit2D[] wallHits = Physics2D.RaycastAll(checkOrigin, Vector2.right * dashDirection.x, extentX + 0.3f);
            foreach (var hit in wallHits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
                {
                    hitWall = true;
                    break;
                }
            }

            bool hasGroundAhead = false;
            Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(dashDirection.x * (extentX + 0.2f), 0f);
            RaycastHit2D[] ledgeHits = Physics2D.RaycastAll(ledgeOrigin, Vector2.down, extentY + 1.2f);
            foreach (var hit in ledgeHits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
                {
                    hasGroundAhead = true;
                    break;
                }
            }

            if (Time.time >= dashEndTime || currentTarget == null ||
                Vector2.Distance(transform.position, currentTarget.position) <= attackRange ||
                hitWall || !hasGroundAhead)
            {
                EndDash();
            }
        }
    }

    private Transform ChooseClosestTarget()
    {
        // Re-resolve player if the reference was lost (e.g. player died and respawned)
        if (TargetA == null && TargetB == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                TargetA = player.transform;
            }
        }

        Transform closest = null;
        float bestDistance = float.MaxValue;

        foreach (Transform candidate in new[] { TargetA, TargetB })
        {
            if (candidate == null)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, candidate.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private bool IsTargetInDashRange()
    {
        if (TargetA != null && Vector2.Distance(transform.position, TargetA.position) <= dashRange)
            return true;
        if (TargetB != null && Vector2.Distance(transform.position, TargetB.position) <= dashRange)
            return true;
        return false;
    }

    private void PerformPatrol()
    {
        if (rb == null)
        {
            return;
        }

        float minX = startX - patrolRadius;
        float maxX = startX + patrolRadius;

        // Check if exceeded bounds
        if (transform.position.x >= maxX && patrolDirection > 0f)
        {
            patrolDirection = -1f;
        }
        else if (transform.position.x <= minX && patrolDirection < 0f)
        {
            patrolDirection = 1f;
        }

        // Auto-patrol: Wall and Ledge Check using BoxCollider2D bounds
        float extentX = 0.5f;
        float extentY = 0.5f;
        var boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            extentX = boxCollider.size.x * 0.5f * Mathf.Abs(transform.localScale.x);
            extentY = boxCollider.size.y * 0.5f * Mathf.Abs(transform.localScale.y);
        }

        // 1. Check for wall in front
        bool hitWall = false;
        Vector2 checkOrigin = (Vector2)transform.position + new Vector2(0f, -extentY * 0.3f);
        RaycastHit2D[] wallHits = Physics2D.RaycastAll(checkOrigin, Vector2.right * patrolDirection, extentX + 0.3f);
        foreach (var hit in wallHits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
            {
                hitWall = true;
                break;
            }
        }

        // 2. Check for ledge in front (downward raycast)
        bool hasGroundAhead = false;
        Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(patrolDirection * (extentX + 0.2f), 0f);
        RaycastHit2D[] ledgeHits = Physics2D.RaycastAll(ledgeOrigin, Vector2.down, extentY + 1.2f);
        foreach (var hit in ledgeHits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
            {
                hasGroundAhead = true;
                break;
            }
        }

        // Turn around if blocked or about to fall
        if (hitWall || !hasGroundAhead)
        {
            patrolDirection = -patrolDirection;
        }

        rb.linearVelocity = new Vector2(patrolDirection * moveSpeed, rb.linearVelocity.y);
        SetWalkState(true);
        UpdateFacing(patrolDirection);
    }

    private void PerformWalkMovement(Vector2 destination)
    {
        if (rb == null)
        {
            return;
        }

        float xDistance = destination.x - transform.position.x;
        float xDirection = Mathf.Sign(xDistance);
        rb.linearVelocity = new Vector2(xDirection * moveSpeed, rb.linearVelocity.y);
        SetWalkState(true);
        UpdateFacing(xDirection);
    }

    private void ChaseTarget(Vector2 destination)
    {
        if (rb == null)
        {
            return;
        }

        float xDistance = destination.x - transform.position.x;
        float xDirection = Mathf.Sign(xDistance);
        if (xDirection == 0f)
        {
            xDirection = transform.localScale.x >= 0 ? 1f : -1f;
        }

        // Ledge & Wall Check for chasing
        float extentX = 0.5f;
        float extentY = 0.5f;
        var boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            extentX = boxCollider.size.x * 0.5f * Mathf.Abs(transform.localScale.x);
            extentY = boxCollider.size.y * 0.5f * Mathf.Abs(transform.localScale.y);
        }

        bool hitWall = false;
        Vector2 checkOrigin = (Vector2)transform.position + new Vector2(0f, -extentY * 0.3f);
        RaycastHit2D[] wallHits = Physics2D.RaycastAll(checkOrigin, Vector2.right * xDirection, extentX + 0.3f);
        foreach (var hit in wallHits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
            {
                hitWall = true;
                break;
            }
        }

        bool hasGroundAhead = false;
        Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(xDirection * (extentX + 0.2f), 0f);
        RaycastHit2D[] ledgeHits = Physics2D.RaycastAll(ledgeOrigin, Vector2.down, extentY + 1.2f);
        foreach (var hit in ledgeHits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
            {
                hasGroundAhead = true;
                break;
            }
        }

        if (hitWall || !hasGroundAhead)
        {
            // Stop at edges/walls to avoid falling off
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetWalkState(false);
        }
        else
        {
            rb.linearVelocity = new Vector2(xDirection * moveSpeed, rb.linearVelocity.y);
            SetWalkState(true);
            UpdateFacing(xDirection);
        }
    }

    private void BeginAttack()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        SetWalkState(false);

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        if (animator != null && (_animParamNames.Contains(slashTriggerName) || forceDirectPlay))
        {
            if (verboseLogs) Debug.Log($"[Enemy] {name} triggering attack: {slashTriggerName}");
            if (forceDirectPlay)
                animator.Play(slashStateName);
            else
                animator.SetTrigger(slashTriggerName);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator trigger '{slashTriggerName}' not found on {name}");
        }

        ApplyDamageToTarget(currentTarget, attackDamage);
    }

    private void BeginDash()
    {
        if (animator == null || isDashing || currentTarget == null)
        {
            return;
        }

        float xDistance = currentTarget.position.x - transform.position.x;
        float xDirection = Mathf.Sign(xDistance);
        if (xDirection == 0f)
        {
            xDirection = transform.localScale.x >= 0 ? 1f : -1f;
        }

        // Check if there is a wall or ledge before starting the dash
        float extentX = 0.5f;
        float extentY = 0.5f;
        var boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            extentX = boxCollider.size.x * 0.5f * Mathf.Abs(transform.localScale.x);
            extentY = boxCollider.size.y * 0.5f * Mathf.Abs(transform.localScale.y);
        }

        bool hasGroundAhead = false;
        Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(xDirection * (extentX + 0.2f), 0f);
        RaycastHit2D[] ledgeHits = Physics2D.RaycastAll(ledgeOrigin, Vector2.down, extentY + 1.2f);
        foreach (var hit in ledgeHits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.collider.CompareTag("Ground"))
            {
                hasGroundAhead = true;
                break;
            }
        }

        if (!hasGroundAhead)
        {
            // Do not dash off ledges
            return;
        }

        dashDirection = new Vector2(xDirection, 0f);
        dashEndTime = Time.time + dashDuration;
        isDashing = true;
        if (animator != null && (_animParamNames.Contains(sDashTriggerName) || forceDirectPlay))
        {
            if (verboseLogs) Debug.Log($"[Enemy] {name} starting dash: {sDashTriggerName}");
            if (forceDirectPlay)
                animator.Play(sDashStateName);
            else
                animator.SetTrigger(sDashTriggerName);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator trigger '{sDashTriggerName}' not found on {name}");
        }
        UpdateFacing(dashDirection.x);
    }

    private void EndDash()
    {
        if (!isDashing)
        {
            return;
        }

        isDashing = false;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        nextDashTime = Time.time + dashCooldown;
        SetWalkState(true);

        if (currentTarget != null && Vector2.Distance(transform.position, currentTarget.position) <= attackRange)
        {
            ApplyDamageToTarget(currentTarget, dashDamage);
        }
    }

    private void UpdateFacing(float directionX)
    {
        float xDir = directionX;
        // Fallback to actual Rigidbody2D velocity if direction parameter is not explicitly passed
        if (Mathf.Abs(xDir) < 0.1f && rb != null)
        {
            xDir = rb.linearVelocity.x;
        }

        if (Mathf.Abs(xDir) > 0.1f)
        {
            float flipMultiplier = spriteFacesRight ? 1f : -1f;
            if (xDir > 0.1f)
            {
                transform.localScale = new Vector3(Mathf.Abs(initialScale.x) * flipMultiplier, initialScale.y, initialScale.z);
            }
            else if (xDir < -0.1f)
            {
                transform.localScale = new Vector3(-Mathf.Abs(initialScale.x) * flipMultiplier, initialScale.y, initialScale.z);
            }
        }
    }

    private void SetWalkState(bool isWalking)
    {
        if (animator == null)
        {
            return;
        }

        if (_animParamNames.Contains(strwalkBoolName))
        {
            animator.SetBool(strwalkBoolName, isWalking);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator bool '{strwalkBoolName}' not found on {name}");
        }
    }

    private void ValidateAnimatorParameters()
    {
        if (animator == null)
            return;

        System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
        if (!_animParamNames.Contains(strwalkBoolName)) missing.Add(strwalkBoolName + "(bool)");
        if (!_animParamNames.Contains(slashTriggerName)) missing.Add(slashTriggerName + "(trigger)");
        if (!_animParamNames.Contains(sDashTriggerName)) missing.Add(sDashTriggerName + "(trigger)");
        if (!_animParamNames.Contains(knockbackTriggerName)) missing.Add(knockbackTriggerName + "(trigger)");

        if (missing.Count > 0)
        {
            Debug.LogWarning($"Animator on {name} is missing parameters: {string.Join(", ", missing)}. Check Animator parameters and names.");
        }
        else if (verboseLogs)
        {
            Debug.Log($"Animator parameters validated on {name}.");
        }
    }

    private void ApplyDamageToTarget(Transform target, int damage)
    {
        if (target == null)
        {
            return;
        }

        IDamageable damageable = GetDamageableComponent(target);
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    private IDamageable GetDamageableComponent(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        return target.GetComponentInParent<IDamageable>();
    }

    public void TriggerKnockback()
    {
        if (animator != null)
        {
            animator.SetTrigger(knockbackTriggerName);
        }

        if (!isKnockedBack)
        {
            StartCoroutine(KnockbackRoutine());
        }
    }
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (verboseLogs)
        {
            Debug.Log($"[Enemy] {name} took {damage} damage. Health now {currentHealth}/{maxHealth}.");
        }

        SpriteJuice juice = GetComponent<SpriteJuice>();
        if (juice != null)
        {
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            Vector2 hitDir = player != null ? (transform.position - player.position).normalized : Vector2.right;
            juice.PlayHitReaction(hitDir, 5.5f);
        }
        else
        {
            if (animator != null && (_animParamNames.Contains(knockbackTriggerName) || forceDirectPlay))
            {
                if (forceDirectPlay)
                    animator.Play(knockbackStateName);
                else
                    animator.SetTrigger(knockbackTriggerName);
            }
            else if (verboseLogs)
            {
                Debug.LogWarning($"Animator trigger '{knockbackTriggerName}' not found on {name}");
            }

            if (!isKnockedBack)
            {
                StartCoroutine(KnockbackRoutine());
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"[Enemy] {name} died.");
        Destroy(gameObject);
    }
    private System.Collections.IEnumerator KnockbackRoutine()
    {
        isKnockedBack = true;
        
        float pushDir = 1f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            pushDir = transform.position.x > player.transform.position.x ? 1f : -1f;
            // Face the attacker when hit
            float faceDirection = Mathf.Sign(player.transform.position.x - transform.position.x);
            UpdateFacing(faceDirection);
        }
        else
        {
            pushDir = -Mathf.Sign(transform.localScale.x);
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(pushDir * 5.5f, 2.5f); // horizontal pushback + vertical hop
        }

        SetWalkState(false);
        yield return new WaitForSeconds(knockbackDuration);
        isKnockedBack = false;
        SetWalkState(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dashRange);

        if (TargetA != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, TargetA.position);
        }

        if (TargetB != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, TargetB.position);
        }

        // Draw patrol radius bounds
        Gizmos.color = Color.blue;
        float currentStartX = Application.isPlaying ? startX : transform.position.x;
        Vector3 startPos = new Vector3(currentStartX, transform.position.y, transform.position.z);
        Vector3 leftBound = startPos + Vector3.left * patrolRadius;
        Vector3 rightBound = startPos + Vector3.right * patrolRadius;
        Gizmos.DrawLine(leftBound, rightBound);
        Gizmos.DrawWireSphere(leftBound, 0.2f);
        Gizmos.DrawWireSphere(rightBound, 0.2f);
    }
}

