using UnityEngine;

public class move : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpForce = 15.5f;
    public float gravityScale = 2.8f;
    public float fallGravityMultiplier = 1.6f;
    public float lowJumpMultiplier = 2.0f;
    
    [Header("Teleport Jump Settings")]
    [Tooltip("If true, jumping teleports the player up a set distance with pixelated dissolve/rebuild FX instead of using standard jump animations.")]
    public bool useTeleportJump = false;
    public float teleportJumpDistance = 4.2f;

    [Header("Blob Form Settings")]
    public float blobSpeedMultiplier = 1.5f; // Makes the blob dash faster than normal running
    private bool isBlobForm = false;

    [Header("Dash Settings")]
    public float dashSpeed = 16f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.8f;
    public bool invulnerableDuringDash = true;

    private bool isDashing = false;
    private float dashTimeLeft;
    private float dashCooldownTimer;
    public float lastFacingSign = 1f;

    public float LastFacingSign => lastFacingSign;
    public bool IsInvulnerable => isDashing && invulnerableDuringDash;
    public bool IsDashing => isDashing;

    [Header("Debuffs")]
    private float speedDebuffMultiplier = 1.0f;
    private float debuffTimer = 0f;

    private System.Collections.Generic.HashSet<Collider2D> ignoredEnemyColliders = new System.Collections.Generic.HashSet<Collider2D>();

    [Header("Run & Double Tap Settings")]
    [Tooltip("Movement speed multiplier when running (triggered by double-tapping horizontal directional input).")]
    public float runSpeedMultiplier = 1.6f;
    [Tooltip("Time window in seconds to register a double tap for running.")]
    public float doubleTapThreshold = 0.25f;

    [Header("Jump Lockout Settings")]
    [Tooltip("Duration in seconds after launching a jump where ground checks are temporarily locked out so the jump animation plays full multi-frame flight.")]
    public float jumpLockoutDuration = 0.25f;
    private float jumpLockoutTimer = 0f;

    private float lastLeftTapTime = -10f;
    private float lastRightTapTime = -10f;
    private bool isDoubleTapRunning = false;
    private int lastTapDirection = 0; // -1 for left, 1 for right
    private float prevHorizontalInput = 0f;

    private Rigidbody2D rb;
    private Animator anim;
    private PlayerPixelDissolveFX dissolveFX;
    private bool isGrounded;
    private Vector3 initialAbsScale = Vector3.one;
    private BoxCollider2D boxCollider;
    private Vector2 standingColSize;
    private Vector2 standingColOffset;
    private bool wasBlobFormLastFrame = false;

    public static move Instance { get; private set; }

    public static bool ExternalMovementLock = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
        ExternalMovementLock = false;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // DontDestroyOnLoad only works on root GameObjects.
        // The Player may be parented under a holder object (e.g. "playerholder") in the scene.
        // Detach first so the player persists across scene transitions.
        if (transform.parent != null)
        {
            Debug.Log($"[move] Detaching Player from parent '{transform.parent.name}' to enable DontDestroyOnLoad.");
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        dissolveFX = GetComponent<PlayerPixelDissolveFX>();
        if (dissolveFX == null) dissolveFX = gameObject.AddComponent<PlayerPixelDissolveFX>();
        rb.gravityScale = gravityScale;
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            PhysicsMaterial2D noFrictionMat = new PhysicsMaterial2D("PlayerNoFriction");
            noFrictionMat.friction = 0f;
            noFrictionMat.bounciness = 0f;
            rb.sharedMaterial = noFrictionMat;
        }

        initialAbsScale = new Vector3(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y), Mathf.Abs(transform.localScale.z));
        if (initialAbsScale == Vector3.zero) initialAbsScale = Vector3.one;

        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            standingColSize = boxCollider.size;
            standingColOffset = boxCollider.offset;
        }

        EnsureLumiCompanionInitialized();
    }

    [Header("Orb Companion Toggle")]
    [Tooltip("If true, initializes Lumi companion orb. Set to false for BasePlayer tutorial skin.")]
    public bool enableOrbCompanion = true;

    private void EnsureLumiCompanionInitialized()
    {
        if (!enableOrbCompanion) return;

        if (OrbInventorySystem.Instance == null)
        {
            GameObject invGO = new GameObject("OrbInventorySystem");
            invGO.AddComponent<OrbInventorySystem>();
        }

        if (OrbInventoryUI.Instance == null)
        {
            GameObject uiGO = new GameObject("OrbInventoryUI");
            uiGO.AddComponent<OrbInventoryUI>();
        }

        if (LightOrbCompanion.Instance == null)
        {
            GameObject orbGO = new GameObject("Lumi_LightOrbCompanion");
            orbGO.transform.position = transform.position + new Vector3(-1.2f, 1.4f, 0f);
            orbGO.AddComponent<LightOrbCompanion>();
        }
    }

    public void UpgradeDash()
    {
        dashSpeed *= 1.25f;
        dashCooldown *= 0.8f;
        Debug.Log("Player dash upgraded! New speed: " + dashSpeed + ", cooldown: " + dashCooldown);
    }

    public void ApplySlow(float duration, float multiplier)
    {
        speedDebuffMultiplier = multiplier;
        debuffTimer = duration;
        Debug.Log("Player slowed! Speed multiplier: " + multiplier + " for " + duration + "s");
    }

    void Update()
    {
        // Update debuff timer
        if (debuffTimer > 0f)
        {
            debuffTimer -= Time.deltaTime;
            if (debuffTimer <= 0f)
            {
                speedDebuffMultiplier = 1.0f;
                Debug.Log("Player slow debuff expired.");
            }
        }

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput != 0)
        {
            lastFacingSign = Mathf.Sign(horizontalInput);
        }

        // Handle active dash
        if (isDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            if (dashTimeLeft <= 0f)
            {
                isDashing = false;
                rb.gravityScale = gravityScale; // Restore gravity
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

                if (anim != null)
                {
                    var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.IsName("Dash") || stateInfo.IsName("dash"))
                    {
                        if (!isGrounded)
                        {
                            if (anim.HasState(0, Animator.StringToHash("jump"))) anim.Play("jump");
                            else if (anim.HasState(0, Animator.StringToHash("Jump"))) anim.Play("Jump");
                        }
                        else if (isDoubleTapRunning && horizontalInput != 0)
                        {
                            if (anim.HasState(0, Animator.StringToHash("Run"))) anim.Play("Run");
                            else if (anim.HasState(0, Animator.StringToHash("run"))) anim.Play("run");
                        }
                        else if (horizontalInput != 0)
                        {
                            if (anim.HasState(0, Animator.StringToHash("walk"))) anim.Play("walk");
                            else if (anim.HasState(0, Animator.StringToHash("Walk"))) anim.Play("Walk");
                        }
                        else
                        {
                            if (anim.HasState(0, Animator.StringToHash("idle"))) anim.Play("idle");
                            else if (anim.HasState(0, Animator.StringToHash("Idle"))) anim.Play("Idle");
                        }
                    }
                }
            }
            else
            {
                rb.linearVelocity = new Vector2(lastFacingSign * dashSpeed, 0f); // zero gravity/vertical movement during dash
                return;
            }
        }

        // 👇 Evaluate grounding with grace buffer at top of Update
        groundedGraceTimer -= Time.deltaTime;
        if (jumpLockoutTimer > 0f)
        {
            jumpLockoutTimer -= Time.deltaTime;
            isGrounded = false;
            groundedGraceTimer = 0f;
        }
        else
        {
            bool wasAirborne = !isGrounded;
            if (CheckIsGrounded())
            {
                groundedGraceTimer = 0.15f; // Grace buffer prevents 1-frame flickering
                if (wasAirborne && PlayerCombatJuice.Instance != null)
                {
                    PlayerCombatJuice.Instance.TriggerLandSquash();
                }
            }
            isGrounded = (groundedGraceTimer > 0f);
        }

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (LightOrbCompanion.Instance != null && LightOrbCompanion.Instance.IsGrappling)
        {
            return;
        }

        if (IsMovementBlocked())
        {
            isDoubleTapRunning = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (anim != null)
            {
                anim.SetBool("isWalking", false);
                anim.SetBool("isRunning", false);
                anim.SetBool("isBlob", false);
                anim.SetBool("isJumping", !isGrounded);
            }
            return;
        }

        // Handle double-tap detection for running (holding Left or Right / A or D)
        bool leftKeyDown = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || (horizontalInput < -0.1f && prevHorizontalInput >= -0.1f);
        bool rightKeyDown = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || (horizontalInput > 0.1f && prevHorizontalInput <= 0.1f);

        if (leftKeyDown)
        {
            if (Time.time - lastLeftTapTime <= doubleTapThreshold && lastTapDirection == -1)
            {
                isDoubleTapRunning = true;
            }
            lastLeftTapTime = Time.time;
            lastTapDirection = -1;
        }

        if (rightKeyDown)
        {
            if (Time.time - lastRightTapTime <= doubleTapThreshold && lastTapDirection == 1)
            {
                isDoubleTapRunning = true;
            }
            lastRightTapTime = Time.time;
            lastTapDirection = 1;
        }

        prevHorizontalInput = horizontalInput;

        // Reset double-tap running state when horizontal input is released or direction changes
        if (horizontalInput == 0f || (isDoubleTapRunning && lastTapDirection != 0 && Mathf.Sign(horizontalInput) != lastTapDirection))
        {
            isDoubleTapRunning = false;
        }

        bool isMoving = horizontalInput != 0;
        bool isRunning = isMoving && isDoubleTapRunning && !isBlobForm;
        bool isWalking = isMoving && !isDoubleTapRunning && !isBlobForm;

        // 👇 Check if pressing M AND moving left or right
        if (Input.GetKey(KeyCode.M) && horizontalInput != 0)
        {
            isBlobForm = true;
        }
        else
        {
            isBlobForm = false;
        }

        // Trigger Dash
        if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownTimer <= 0f && !isBlobForm)
        {
            isDashing = true;
            dashTimeLeft = dashDuration;
            dashCooldownTimer = dashCooldown;
            rb.gravityScale = 0f; // Disable gravity during dash
            rb.linearVelocity = new Vector2(lastFacingSign * dashSpeed, 0f);
            if (PlayerCombatJuice.Instance != null)
            {
                PlayerCombatJuice.Instance.TriggerDashStretch();
            }
            if (anim != null)
            {
                anim.SetTrigger("dash");
                anim.Play("Dash", 0, 0f);
            }
            return;
        }

        // Apply movement speed (faster if running or in blob form, affected by slow debuff)
        float currentSpeed = moveSpeed;
        if (isBlobForm)
        {
            currentSpeed *= blobSpeedMultiplier;
        }
        else if (isRunning)
        {
            currentSpeed *= runSpeedMultiplier;
        }
        currentSpeed *= speedDebuffMultiplier;
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);

        // 👇 Update Animator states — but SUPPRESS during active attack animations
        //    so walk/run/idle bools don't fight with the attack state machine transitions.
        bool combatActive = MageCombat.Instance != null && MageCombat.Instance.IsAttacking;
        if (anim != null && !combatActive)
        {
            bool hasWalkParam = HasAnimatorParameter(anim, "isWalking");
            anim.SetBool("isWalking", isWalking);
            if (hasWalkParam)
            {
                anim.SetBool("isRunning", isRunning);
            }
            else
            {
                anim.SetBool("isRunning", isMoving && !isBlobForm);
            }
            anim.SetBool("isBlob", isBlobForm);
        }

        // Flip sprite based on movement direction (preserving exact initial inspector scales)
        float facing = (lastFacingSign < 0f) ? -1f : 1f;
        float scaleX = Mathf.Max(0.1f, initialAbsScale.x);
        float scaleY = Mathf.Max(0.1f, initialAbsScale.y);
        float scaleZ = Mathf.Max(0.1f, initialAbsScale.z);
        transform.localScale = new Vector3(facing * scaleX, scaleY, scaleZ);

        // Adjust collider size dynamically for Blob Form vs Standing Form
        if (isBlobForm && !wasBlobFormLastFrame)
        {
            wasBlobFormLastFrame = true;
            if (boxCollider != null)
            {
                boxCollider.size = new Vector2(standingColSize.x * 1.1f, standingColSize.y * 0.45f);
                boxCollider.offset = new Vector2(standingColOffset.x, standingColOffset.y - standingColSize.y * 0.25f);
            }
        }
        else if (!isBlobForm && wasBlobFormLastFrame)
        {
            wasBlobFormLastFrame = false;
            if (boxCollider != null)
            {
                boxCollider.size = standingColSize;
                boxCollider.offset = standingColOffset;
            }
        }

        // 👇 Jump logic (disable jumping while in blob form)
        if ((Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)) && isGrounded && !isBlobForm)
        {
            jumpLockoutTimer = jumpLockoutDuration; // Lockout ground checks for initial launch phase so full animation plays
            groundedGraceTimer = 0f; // Reset grace timer on jump
            isGrounded = false;

            if (useTeleportJump)
            {
                // Calculate target position in air with ceiling raycast check
                Vector2 startPos = transform.position;
                float maxDist = teleportJumpDistance;
                RaycastHit2D hit = Physics2D.Raycast(startPos, Vector2.up, maxDist, ~0);
                float actualDist = (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject) ? Mathf.Max(0.5f, hit.distance - 0.5f) : maxDist;
                Vector2 targetPos = startPos + new Vector2(0f, actualDist);

                // Execute Pixelated Dissolve & Rebuild Visual FX
                if (dissolveFX != null)
                {
                    dissolveFX.PlayDissolveTeleport(startPos, targetPos, () => {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 3.5f); // Natural air float momentum
                        if (anim != null)
                        {
                            anim.SetBool("isJumping", true);
                            if (anim.HasState(0, Animator.StringToHash("jump")))
                                anim.Play("jump", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("Jump")))
                                anim.Play("Jump", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("air")))
                                anim.Play("air", 0, 0f);
                        }
                    });
                }
                else
                {
                    transform.position = targetPos;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 3.5f);
                }
            }
            else
            {
                // Standard physics jump
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }

            if (PlayerCombatJuice.Instance != null)
            {
                PlayerCombatJuice.Instance.TriggerJumpStretch();
            }

            if (anim != null)
            {
                anim.SetBool("isJumping", true);
                anim.SetTrigger("jump");
                anim.SetTrigger("Jump");
                if (anim.HasState(0, Animator.StringToHash("jump")))
                    anim.Play("jump", 0, 0f);
                else if (anim.HasState(0, Animator.StringToHash("Jump")))
                    anim.Play("Jump", 0, 0f);
                else if (anim.HasState(0, Animator.StringToHash("air")))
                    anim.Play("air", 0, 0f);
            }
        }

        if (!combatActive && anim != null)
        {
            anim.SetBool("isJumping", !isGrounded);
        }

        // Dynamic Gravity Modifiers for Snappy 2D Jump Physics
        if (!isDashing && rb != null)
        {
            if (rb.linearVelocity.y < 0f)
            {
                rb.gravityScale = gravityScale * fallGravityMultiplier;
            }
            else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump"))
            {
                rb.gravityScale = gravityScale * lowJumpMultiplier;
            }
            else
            {
                rb.gravityScale = gravityScale;
            }
        }

        // Safeguard: Instantly force exit from jump state when grounded
        // BUT skip during active attacks so we don't rip the player out of attack animations
        if (isGrounded && jumpLockoutTimer <= 0f && anim != null && !combatActive)
        {
            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("jump") || stateInfo.IsName("Jump"))
            {
                if (isRunning)
                {
                    if (anim.HasState(0, Animator.StringToHash("Run"))) anim.Play("Run");
                    else if (anim.HasState(0, Animator.StringToHash("run"))) anim.Play("run");
                }
                else if (isWalking)
                {
                    if (anim.HasState(0, Animator.StringToHash("walk"))) anim.Play("walk");
                    else if (anim.HasState(0, Animator.StringToHash("Walk"))) anim.Play("Walk");
                }
                else
                {
                    if (anim.HasState(0, Animator.StringToHash("idle"))) anim.Play("idle");
                    else if (anim.HasState(0, Animator.StringToHash("Idle"))) anim.Play("Idle");
                }
            }
        }

        // Handle passing through enemies during dash or blob form
        bool shouldIgnoreEnemies = isDashing || isBlobForm;
        if (shouldIgnoreEnemies)
        {
            Collider2D[] playerColliders = GetComponents<Collider2D>();
            Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, 10f);
            foreach (var col in nearbyColliders)
            {
                if (col != null && !col.isTrigger && col.gameObject != gameObject && 
                    (col.CompareTag("enemy") || col.GetComponent<IDamageable>() != null || col.gameObject.name.Contains("Boss") || col.gameObject.name.Contains("Tsuchigumo")))
                {
                    if (!ignoredEnemyColliders.Contains(col))
                    {
                        foreach (var playerCol in playerColliders)
                        {
                            if (playerCol != null)
                            {
                                Physics2D.IgnoreCollision(playerCol, col, true);
                            }
                        }
                        ignoredEnemyColliders.Add(col);
                    }
                }
            }
        }
        else
        {
            if (ignoredEnemyColliders.Count > 0)
            {
                Collider2D[] playerColliders = GetComponents<Collider2D>();
                foreach (var col in ignoredEnemyColliders)
                {
                    if (col != null)
                    {
                        foreach (var playerCol in playerColliders)
                        {
                            if (playerCol != null)
                            {
                                Physics2D.IgnoreCollision(playerCol, col, false);
                            }
                        }
                    }
                }
                ignoredEnemyColliders.Clear();
            }
        }
    }

    public void ResetPlayerScaleToNormal()
    {
        if (initialAbsScale == Vector3.zero || initialAbsScale.x <= 0.01f)
        {
            initialAbsScale = new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(transform.localScale.x)),
                Mathf.Max(0.1f, Mathf.Abs(transform.localScale.y)),
                Mathf.Max(0.1f, Mathf.Abs(transform.localScale.z))
            );
        }
        float facing = (lastFacingSign < 0f) ? -1f : 1f;
        float absX = Mathf.Max(0.1f, initialAbsScale.x);
        float absY = Mathf.Max(0.1f, initialAbsScale.y);
        float absZ = Mathf.Max(0.1f, initialAbsScale.z);
        transform.localScale = new Vector3(facing * absX, absY, absZ);
    }

    public void FaceTarget(Vector3 targetPosition)
    {
        float dir = targetPosition.x - transform.position.x;
        if (dir != 0f)
        {
            lastFacingSign = Mathf.Sign(dir);
            float facing = lastFacingSign != 0 ? lastFacingSign : 1f;
            transform.localScale = new Vector3(facing * initialAbsScale.x, initialAbsScale.y, initialAbsScale.z);
        }
    }

    private bool IsMovementBlocked()
    {
        if (ExternalMovementLock) return true;
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return true;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return true;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return true;
        if (OrbInventoryUI.Instance != null && OrbInventoryUI.Instance.IsInventoryOpen) return true;
        if (NyxarisManager.IsChatActive || NyxarisManager.IsTyping) return true;

        // Block movement if any UI text input has active keyboard focus
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
        {
            var go = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (go.GetComponent<TMPro.TMP_InputField>() != null || go.GetComponent<UnityEngine.UI.InputField>() != null)
            {
                return true;
            }
        }

        return false;
    }

    [Header("Step-Over & Ground Seam Smoothing")]
    [Tooltip("Maximum height of uneven box collider bumps or ground seams the player can automatically step over without stopping.")]
    public float maxStepHeight = 0.35f;
    [Tooltip("Forward distance to scan for small ground seams.")]
    public float stepScanDistance = 0.45f;
    [Tooltip("Smooth lift speed when stepping over a low seam.")]
    public float stepSmoothSpeed = 14f;

    void FixedUpdate()
    {
        HandleStepSmoothing();
    }

    private void HandleStepSmoothing()
    {
        if (!isGrounded || isDashing) return;
        float hInput = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(hInput) < 0.1f) return;

        float dir = Mathf.Sign(hInput);
        Collider2D mainCol = null;
        foreach (var col in GetComponents<Collider2D>())
        {
            if (col != null && col.enabled && !col.isTrigger)
            {
                mainCol = col;
                break;
            }
        }
        if (mainCol == null) return;

        float bottomY = mainCol.bounds.min.y;
        float centerX = mainCol.bounds.center.x;
        float halfWidth = mainCol.bounds.extents.x;

        Vector2 footRayOrigin = new Vector2(centerX + dir * (halfWidth + 0.02f), bottomY + 0.05f);
        Vector2 waistRayOrigin = new Vector2(centerX + dir * (halfWidth + 0.02f), bottomY + maxStepHeight + 0.05f);

        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        RaycastHit2D footHit = Physics2D.Raycast(footRayOrigin, Vector2.right * dir, stepScanDistance, layerMask);
        RaycastHit2D waistHit = Physics2D.Raycast(waistRayOrigin, Vector2.right * dir, stepScanDistance + 0.1f, layerMask);

        // If foot hits a solid box/obstacle, but waist does NOT hit anything, it's a step/seam!
        if (footHit.collider != null && !footHit.collider.isTrigger && !footHit.collider.CompareTag("enemy") && footHit.collider.gameObject != gameObject)
        {
            if (waistHit.collider == null || waistHit.collider.isTrigger)
            {
                // Smooth step-up assist
                float stepTargetY = footHit.point.y + 0.08f;
                if (stepTargetY > bottomY)
                {
                    float liftAmount = Mathf.Min(stepTargetY - bottomY, maxStepHeight);
                    rb.position = Vector2.MoveTowards(rb.position, new Vector2(rb.position.x, rb.position.y + liftAmount), stepSmoothSpeed * Time.fixedDeltaTime);
                }
            }
        }
    }

    [Header("Ground Check Settings")]
    public Vector2 feetOffset = new Vector2(0f, -1.65f);
    public Vector2 feetBoxSize = new Vector2(0.7f, 0.4f);
    private float groundedGraceTimer = 0f;

    private bool CheckIsGrounded()
    {
        Vector2 checkPos;

        // Find the active solid (non-trigger) physics collider
        Collider2D mainCol = null;
        foreach (var col in GetComponents<Collider2D>())
        {
            if (col != null && col.enabled && !col.isTrigger)
            {
                mainCol = col;
                break;
            }
        }

        if (mainCol != null)
        {
            // Position the overlap box directly at the bottom-center edge of the physics collider in world space
            checkPos = new Vector2(mainCol.bounds.center.x, mainCol.bounds.min.y);
        }
        else
        {
            checkPos = (Vector2)transform.position + feetOffset;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, feetBoxSize, 0f);

        foreach (var col in hits)
        {
            if (col == null || col.isTrigger || col.gameObject == gameObject) continue;
            if (col.transform.IsChildOf(transform)) continue;

            // Ignore enemy colliders
            if (col.CompareTag("enemy") || col.GetComponent<IDamageable>() != null) continue;

            // Valid solid ground/platform surface found!
            return true;
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector2 checkPos;
        Collider2D mainCol = null;
        foreach (var col in GetComponents<Collider2D>())
        {
            if (col != null && col.enabled && !col.isTrigger)
            {
                mainCol = col;
                break;
            }
        }
        if (mainCol != null)
        {
            checkPos = new Vector2(mainCol.bounds.center.x, mainCol.bounds.min.y);
        }
        else
        {
            checkPos = (Vector2)transform.position + feetOffset;
        }
        Gizmos.DrawWireCube(checkPos, feetBoxSize);
    }

    public bool CheckIsGroundedPublic()
    {
        return CheckIsGrounded();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (jumpLockoutTimer > 0f) return;
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (jumpLockoutTimer > 0f) return;
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = CheckIsGrounded();
        }
    }

    private bool HasAnimatorParameter(Animator animator, string paramName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }
}
