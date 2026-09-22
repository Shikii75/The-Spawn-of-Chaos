using UnityEngine;

public class move : MonoBehaviour
{
    [Header("Mage Voice Settings")]
    [Tooltip("Audio clips for Mage jumping.")]
    public AudioClip[] jumpVoiceClips;
    [Tooltip("Audio clips for Mage talking during NPC dialogue.")]
    public AudioClip[] talkVoiceClips;
    private AudioSource voiceAudioSource;

    [Header("Virtual Touch Controls")]
    public float virtualHorizontalInput = 0f;
    public bool virtualJumpPressed = false;
    public bool virtualJumpHeld = false;
    public bool virtualDashPressed = false;
    public bool virtualBlobPressed = false;
   public static bool forceBlobIntro = false;
    public bool virtualLeftDown = false;
    public bool virtualRightDown = false;
    public float moveSpeed = 6f;
    public float jumpForce = 15.5f;
    public float gravityScale = 2.8f;
    public float fallGravityMultiplier = 1.6f;
    public float lowJumpMultiplier = 2.0f;
    
    [Header("Teleport Jump Settings")]
    [Tooltip("If true, jumping teleports the player up a set distance with pixelated dissolve/rebuild FX instead of using standard jump animations.")]
    public bool useTeleportJump = false;
    public float teleportJumpDistance = 7.35f;

    [Header("Blob Form Settings")]
    public float blobSpeedMultiplier = 1.5f; // Makes the blob dash faster than normal running
    public Vector2 blobColliderSize = new Vector2(1.2f, 0.45f); // Dedicated miniature collider matching blob sprite bounds
    private bool isBlobForm = false;

    [Header("Dash Settings")]
    [Tooltip("Target distance covered by the instantaneous shadow dash.")]
    public float dashDistance = 11.2f;
    public float dashSpeed = 48f;
    [Tooltip("Duration of the instantaneous shadow warp window in seconds.")]
    public float dashDuration = 0.075f;
    [Tooltip("Cooldown between consecutive dashes.")]
    public float dashCooldown = 0.35f;
    public bool invulnerableDuringDash = true;

    private bool isDashing = false;
    private float dashTimeLeft;
    private float dashCooldownTimer;
    public float lastFacingSign = 1f;
    private Vector3 dashStartPos;
    private Vector3 dashTargetPos;
    private PlayerShadowDashTrail shadowTrail;

    [Header("Post-Dash Attack Window")]
    public const float POST_DASH_WINDOW = 0.35f;
    private float postDashTimer = 0f;
    public bool IsInPostDashWindow => postDashTimer > 0f;

    public bool TryConsumePostDashStrike()
    {
        if (postDashTimer > 0f)
        {
            postDashTimer = 0f;
            return true;
        }
        return false;
    }

    public float LastFacingSign => lastFacingSign;
    public bool IsInvulnerable => (isDashing && invulnerableDuringDash) || isBlobForm;
    public bool IsBlobForm => isBlobForm;
    public bool IsDashing => isDashing;
    public bool IsGrounded => isGrounded;
    public bool IsRunning => isDoubleTapRunning && isGrounded && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f;

    [Header("Debuffs")]
    private float speedDebuffMultiplier = 1.0f;
    private float debuffTimer = 0f;

    private System.Collections.Generic.HashSet<Collider2D> ignoredEnemyColliders = new System.Collections.Generic.HashSet<Collider2D>();

    [Header("Run & Double Tap Settings")]
    [Tooltip("Movement speed multiplier when running (triggered by double-tapping horizontal directional input).")]
    public float runSpeedMultiplier = 1.6f;
    [Tooltip("Time window in seconds to register a double tap for running.")]
    public float doubleTapThreshold = 0.35f;

    [Header("Jump Lockout Settings")]
    [Tooltip("Duration in seconds after launching a jump where ground checks are temporarily locked out so the jump animation plays full multi-frame flight.")]
    public float jumpLockoutDuration = 0.25f;
    private float jumpLockoutTimer = 0f;

    [Header("Extended Fall & Proximity Landing Settings")]
    [Tooltip("Minimum airtime in seconds before switching from standard jump to the extended falling loop.")]
    public float fallThresholdTime = 0.35f;
    [Tooltip("Downward distance to scan for ground while falling to trigger the 4 mid-air landing anticipation frames.")]
    public float landingProximityDistance = 1.8f;
    private float airTimeCounter = 0f;
    private bool hasTriggeredStartLanding = false;
    private bool isFallingState = false;

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
    private Collider2D standingCollider;
    private BoxCollider2D blobCollider;
    private Vector2 standingColSize;
    private Vector2 standingColOffset;
    private System.Collections.Generic.List<Collider2D> disabledCollidersInBlob = new System.Collections.Generic.List<Collider2D>();
    private System.Collections.Generic.List<MonoBehaviour> disabledComponentsInBlob = new System.Collections.Generic.List<MonoBehaviour>();
    private bool wasBlobFormLastFrame = false;

    public static move Instance { get; set; }

    public static bool ExternalMovementLock = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
        ExternalMovementLock = false;
    }

    void Awake()
    {
        SpawnOfChaos.Entities.PlayerMageVoiceController.EnsureAttached(gameObject);
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ExternalMovementLock = false;
        InitVoiceAudio();

        // DontDestroyOnLoad only works on root GameObjects.
        // The Player may be parented under a holder object (e.g. "playerholder") in the scene.
        // Detach first so the player persists across scene transitions.
        if (transform.parent != null)
        {
            Debug.Log($"[move] Detaching Player from parent '{transform.parent.name}' to enable DontDestroyOnLoad.");
            transform.SetParent(null, true);
        }
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        dissolveFX = GetComponent<PlayerPixelDissolveFX>();
        if (dissolveFX == null) dissolveFX = gameObject.AddComponent<PlayerPixelDissolveFX>();

        shadowTrail = GetComponent<PlayerShadowDashTrail>();
        if (shadowTrail == null) shadowTrail = gameObject.AddComponent<PlayerShadowDashTrail>();

        PlayerPlatformFallManager.EnsureAttached(gameObject);

        // Layer both players at layer 1
        gameObject.layer = 1;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 1;
        }
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingOrder = 1;
        }

        // Auto-check Teleport Jump when using the Mage (BasePlayer uses standard jump)
        if (!gameObject.name.Contains("BasePlayer"))
        {
            useTeleportJump = true;
        }
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

        // Instantaneous shadow teleport dash parameters
        if (dashDuration > 0.15f || dashDuration <= 0.01f)
        {
            dashDuration = 0.075f;
        }
        if (dashSpeed < 30f)
        {
            dashSpeed = 48f;
        }
        if (dashDistance < 10.0f)
        {
            dashDistance = 11.2f;
        }
        if (initialAbsScale == Vector3.zero) initialAbsScale = Vector3.one;

        // Setup Dual-Collider System: Standing Collider (Capsule/Box) & 4x Smaller Dedicated Blob Collider
        // 1. Locate primary standing collider on player
        standingCollider = null;
        var rootCols = GetComponents<Collider2D>();
        foreach (var c in rootCols)
        {
            if (c != null && c.enabled && !c.isTrigger)
            {
                standingCollider = c;
                break;
            }
        }
        if (standingCollider == null && rootCols.Length > 0)
        {
            standingCollider = rootCols[0];
        }

        if (standingCollider != null)
        {
            standingColOffset = standingCollider.offset;
            if (standingCollider is CapsuleCollider2D cc)
                standingColSize = cc.size;
            else if (standingCollider is BoxCollider2D bc)
                standingColSize = bc.size;
            else
                standingColSize = standingCollider.bounds.size;
        }
        else
        {
            standingColSize = new Vector2(1.1f, 2.6f);
            standingColOffset = Vector2.zero;
        }

        // 2. Compute Blob Collider: strictly 4 times smaller in height than the standing collider (and 50% width)
        float blobH = Mathf.Max(0.12f, standingColSize.y * 0.25f);
        float blobW = Mathf.Max(0.35f, standingColSize.x * 0.50f);
        blobColliderSize = new Vector2(blobW, blobH);

        // Find or create dedicated BoxCollider2D for blob mode
        blobCollider = null;
        foreach (var c in rootCols)
        {
            if (c is BoxCollider2D bc && c != standingCollider && (!bc.enabled || bc.size.y <= blobH + 0.1f))
            {
                blobCollider = bc;
                break;
            }
        }
        if (blobCollider == null)
        {
            blobCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        float feetY = standingColOffset.y - (standingColSize.y * 0.5f);
        blobCollider.size = blobColliderSize;
        blobCollider.offset = new Vector2(standingColOffset.x, feetY + (blobH * 0.5f));
        blobCollider.isTrigger = false; // Solid physics collider
        blobCollider.enabled = false;   // Start disabled until entering Blob Form

        EnsureLumiCompanionInitialized();
    }

    [Header("Orb Companion Toggle")]
    [Tooltip("If true, initializes Lumi companion orb. Set to false for BasePlayer tutorial skin.")]
    public bool enableOrbCompanion = true;

    [Tooltip("If true, initializes the Nyxaris Tutorial Guide Orb during the tutorial.")]
    public bool enableNyxarisTutorialOrb = true;

    public void EnsureLumiCompanionInitialized()
    {
        bool isTutorial = string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "TutorialScene", System.StringComparison.OrdinalIgnoreCase);

        // 1. Initialize Nyxaris Tutorial Guide in TutorialScene (Strict single instance check)
        if (isTutorial)
        {
            NyxarisSealSequence.EnsureInstanceInScene();
        }

        if (isTutorial && enableNyxarisTutorialOrb)
        {
            if (NyxarisOrbGuide.Instance == null && Object.FindFirstObjectByType<NyxarisOrbGuide>() == null)
            {
                GameObject existingLocations = GameObject.Find("Nyxaris Guide Locations");
                if (existingLocations == null) existingLocations = GameObject.Find("Nyxaris Guide locations");
                if (existingLocations != null)
                {
                    if (existingLocations.GetComponent<NyxarisOrbGuide>() == null)
                        existingLocations.AddComponent<NyxarisOrbGuide>();
                    Debug.Log("[move] Bound Nyxaris Orb Guide to existing 'Nyxaris Guide Locations' hierarchy.");
                }
                else
                {
                    GameObject nyxarisGO = new GameObject("Nyxaris_Guide_Master");
                    nyxarisGO.AddComponent<NyxarisOrbGuide>();
                    Debug.Log("[move] Initialized Nyxaris Orb Guide Master for Tutorial.");
                }
            }
        }

        // 2. In non-tutorial scenes, initialize Lumi companion if enabled (Inventory removed)
        if (isTutorial)
        {
            // Lumi must NEVER exist in TutorialScene
            if (LightOrbCompanion.Instance != null)
            {
                Destroy(LightOrbCompanion.Instance.gameObject);
            }
            var existingLumi = Object.FindObjectsByType<LightOrbCompanion>(FindObjectsSortMode.None);
            foreach (var l in existingLumi)
            {
                if (l != null) Destroy(l.gameObject);
            }
            return;
        }

        if (!enableOrbCompanion) return;

        if (LightOrbCompanion.Instance == null && Object.FindFirstObjectByType<LightOrbCompanion>() == null)
        {
            GameObject orbGO = new GameObject("Lumi_LightOrbCompanion");
            orbGO.transform.position = transform.position + new Vector3(-2.4f, 2.2f, 0f);
            orbGO.AddComponent<LightOrbCompanion>();
        }
    }

    public void UpgradeDash()
    {
        dashSpeed *= 1.25f;
        dashDistance *= 1.15f;
        dashCooldown = Mathf.Max(0.25f, dashCooldown * 0.85f);
        Debug.Log("Player dash upgraded! New speed: " + dashSpeed + ", distance: " + dashDistance + ", cooldown: " + dashCooldown);
    }

    private bool IsObstacleIgnored(Collider2D col)
    {
        if (col == null || col.isTrigger) return true;
        if (col.gameObject == gameObject || col.transform.IsChildOf(transform)) return true;
        try
        {
            if (col.CompareTag("enemy")) return true;
        }
        catch { }
        if (string.Equals(col.tag, "enemy", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (col.GetComponent<UniversalEnemy>() != null || col.GetComponentInParent<UniversalEnemy>() != null) return true;
        if (col.GetComponent<IDamageable>() != null || LumiSpearWeapon.IsEnemyTarget(col)) return true;
        return false;
    }

    /// <summary>
    /// Executes an instantaneous, addictive shadow warp/teleport dash.
    /// Safely boxcasts ahead for solid geometry and triggers shadow visuals and audio.
    /// </summary>
    public void ExecuteInstantShadowDash(float direction)
    {
        if (isBlobForm || dashCooldownTimer > 0f) return;

        postDashTimer = 0f;
        if (direction != 0f) lastFacingSign = Mathf.Sign(direction);
        float dir = lastFacingSign != 0f ? lastFacingSign : 1f;

        // 1. BoxCastAll check ahead for solid walls/geometry to ensure safe teleport destination
        float targetDist = dashDistance;
        int obstacleMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
        Vector2 castOrigin = (Vector2)transform.position + standingColOffset;
        Vector2 castDir = new Vector2(dir, 0f);
        Vector2 boxSize = (standingCollider != null) 
            ? new Vector2(standingColSize.x * 0.90f, standingColSize.y * 0.95f) 
            : new Vector2(0.9f, 2.4f);

        RaycastHit2D[] hits = Physics2D.BoxCastAll(castOrigin, boxSize, 0f, castDir, targetDist, obstacleMask);
        foreach (var h in hits)
        {
            if (IsObstacleIgnored(h.collider)) continue;

            // Valid solid environment / wall obstacle found!
            // Stop safely 0.35 units before the wall
            targetDist = Mathf.Max(0.1f, h.distance - 0.35f);
            break; // BoxCastAll returns hits sorted by distance; first valid solid obstacle is closest
        }

        // Secondary Overlap Verification at Target Position:
        Vector3 potentialTargetPos = transform.position + new Vector3(dir * targetDist, 0f, 0f);
        Collider2D[] initialOverlaps = Physics2D.OverlapBoxAll(
            (Vector2)potentialTargetPos + standingColOffset,
            boxSize * 0.95f,
            0f,
            obstacleMask
        );
        bool hasSolidOverlap = false;
        foreach (var col in initialOverlaps)
        {
            if (!IsObstacleIgnored(col))
            {
                hasSolidOverlap = true;
                break;
            }
        }

        if (hasSolidOverlap)
        {
            // Step backward along direction until fully clear of solid geometry
            for (float stepBack = 0.2f; stepBack <= targetDist; stepBack += 0.2f)
            {
                Vector3 testPos = transform.position + new Vector3(dir * (targetDist - stepBack), 0f, 0f);
                Collider2D[] testOverlaps = Physics2D.OverlapBoxAll((Vector2)testPos + standingColOffset, boxSize * 0.95f, 0f, obstacleMask);
                bool solidFound = false;
                foreach (var to in testOverlaps)
                {
                    if (!IsObstacleIgnored(to))
                    {
                        solidFound = true;
                        break;
                    }
                }
                if (!solidFound)
                {
                    targetDist = Mathf.Max(0.1f, targetDist - stepBack);
                    break;
                }
            }
        }

        dashStartPos = transform.position;
        dashTargetPos = dashStartPos + new Vector3(dir * targetDist, 0f, 0f);

        isDashing = true;
        dashTimeLeft = dashDuration;
        dashCooldownTimer = dashCooldown;
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(dir * (targetDist / Mathf.Max(0.005f, dashDuration)), 0f);
        }

        PlayRandomJumpVoice();

        if (shadowTrail != null)
        {
            shadowTrail.OnDashStart(dashStartPos, dashTargetPos, dir, dashDuration);
        }

        if (anim != null)
        {
            try
            {
                anim.SetTrigger("dash");
                anim.Play("Dash", 0, 0f);
            }
            catch (System.Exception) { }
        }
    }

    public void ApplySlow(float duration, float multiplier)
    {
        speedDebuffMultiplier = multiplier;
        debuffTimer = duration;
        Debug.Log("Player slowed! Speed multiplier: " + multiplier + " for " + duration + "s");
    }

    void Update()
    {
        if (HUDManager.IsInMainMenu())
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

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
        if (Mathf.Abs(virtualHorizontalInput) > 0.05f)
        {
            horizontalInput = virtualHorizontalInput;
        }
        if (horizontalInput != 0 && !isDashing)
        {
            lastFacingSign = Mathf.Sign(horizontalInput);
        }

        // Handle active instantaneous shadow dash: ultra-snappy teleport phase
        if (isDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            float progress = Mathf.Clamp01(1f - (dashTimeLeft / Mathf.Max(0.001f, dashDuration)));
            transform.position = Vector3.Lerp(dashStartPos, dashTargetPos, progress);

            if (dashTimeLeft <= 0f)
            {
                isDashing = false;
                postDashTimer = POST_DASH_WINDOW;
                transform.position = dashTargetPos;
                if (rb != null) rb.gravityScale = gravityScale; // Restore gravity

                if (shadowTrail != null)
                {
                    shadowTrail.OnDashEnd(dashTargetPos);
                }

                // Use a fresh physics check instead of the stale isGrounded flag
                bool actuallyGrounded = CheckIsGrounded();

                if (!actuallyGrounded)
                {
                    // Air dash completed: Force into falling animation immediately!
                    isGrounded = false;
                    groundedGraceTimer = 0f; // Clear stale grace timer
                    isFallingState = true;
                    hasTriggeredStartLanding = false;
                    airTimeCounter = 0.25f; // Ready for proximity landing detection
                    isDoubleTapRunning = false;
                    if (rb != null) rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, -1.0f);
                    if (anim != null)
                    {
                        try
                        {
                            anim.SetBool("isJumping", false);
                            anim.SetBool("isWalking", false);
                            anim.SetBool("isRunning", false);
                            anim.SetBool("isFalling", true);
                            if (anim.HasState(0, Animator.StringToHash("falling"))) anim.Play("falling", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("Falling"))) anim.Play("Falling", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("magefalling"))) anim.Play("magefalling", 0, 0f);
                        }
                        catch (System.Exception) { }
                    }
                }
                else if (isDoubleTapRunning && horizontalInput != 0)
                {
                    // Ground dash completed with movement held: flow directly into continuous Run!
                    if (rb != null) rb.linearVelocity = new Vector2(horizontalInput * moveSpeed * runSpeedMultiplier, rb.linearVelocity.y);
                    if (anim != null)
                    {
                        try
                        {
                            anim.SetBool("isRunning", true);
                            if (anim.HasState(0, Animator.StringToHash("Run"))) anim.Play("Run", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("run"))) anim.Play("run", 0, 0f);
                        }
                        catch (System.Exception) { }
                    }
                }
                else
                {
                    // Ground dash completed without holding direction: halt in idle
                    isDoubleTapRunning = false;
                    if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    if (anim != null)
                    {
                        try
                        {
                            if (horizontalInput != 0)
                            {
                                if (anim.HasState(0, Animator.StringToHash("walk"))) anim.Play("walk", 0, 0f);
                            }
                            else
                            {
                                if (anim.HasState(0, Animator.StringToHash("idle"))) anim.Play("idle", 0, 0f);
                            }
                        }
                        catch (System.Exception) { }
                    }
                }
            }
            else
            {
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

        if (postDashTimer > 0f)
        {
            postDashTimer -= Time.deltaTime;
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
                try
                {
                    anim.SetBool("isWalking", false);
                    anim.SetBool("isRunning", false);
                    anim.SetBool("isBlob", false);
                    anim.SetBool("isJumping", !isGrounded);
                }
                catch (System.Exception) { }
            }
            return;
        }

                // Handle double-tap detection: Double-tap initiates explosive Start Dash Run; holding sustains continuous Run
        bool leftKeyDown = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || virtualLeftDown;
        bool rightKeyDown = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || virtualRightDown;
        virtualLeftDown = false;
        virtualRightDown = false;

        if (leftKeyDown)
        {
            float timeSinceLastLeft = Time.time - lastLeftTapTime;
            if (timeSinceLastLeft <= doubleTapThreshold && lastTapDirection == -1 && dashCooldownTimer <= 0f && !isBlobForm)
            {
                isDoubleTapRunning = true;
                ExecuteInstantShadowDash(-1f);
                lastLeftTapTime = -10f; // Reset to prevent triple-tap re-trigger
            }
            else
            {
                lastLeftTapTime = Time.time;
                lastTapDirection = -1;
            }
        }

        if (rightKeyDown)
        {
            float timeSinceLastRight = Time.time - lastRightTapTime;
            if (timeSinceLastRight <= doubleTapThreshold && lastTapDirection == 1 && dashCooldownTimer <= 0f && !isBlobForm)
            {
                isDoubleTapRunning = true;
                ExecuteInstantShadowDash(1f);
                lastRightTapTime = -10f; // Reset to prevent triple-tap re-trigger
            }
            else
            {
                lastRightTapTime = Time.time;
                lastTapDirection = 1;
            }
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

        // 👇 Check if pressing M or S/DownArrow: morphs into flat puddle blob
        bool blobInputHeld = forceBlobIntro || (Input.GetKey(KeyCode.M) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || virtualBlobPressed);
        
        // Auto-sustain blob form if inside a low ceiling / crawlspace (Strict Hold & Crawlspace Lock)
        bool hasLowCeilingAbove = false;
        if (isBlobForm)
        {
            int ceilingMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            float feetY = standingColOffset.y - (standingColSize.y * 0.5f);
            float fullHeight = (standingColSize.y > 0.5f) ? standingColSize.y : 2.6f;
            float blobH = (blobColliderSize.y > 0.05f) ? blobColliderSize.y : 0.35f;
            float checkHeight = fullHeight - blobH;
            float width = Mathf.Max(0.5f, standingColSize.x > 0.1f ? standingColSize.x * 0.85f : 0.8f);
            float halfW = width * 0.5f;

            Vector2 baseCenter = (Vector2)transform.position + new Vector2(standingColOffset.x, feetY + blobH + 0.04f);

            // 1. BoxCast covering the standing body clearance width
            RaycastHit2D boxHit = Physics2D.BoxCast(baseCenter, new Vector2(width, 0.08f), 0f, Vector2.up, checkHeight, ceilingMask);
            if (IsValidCeilingObstacle(boxHit))
            {
                hasLowCeilingAbove = true;
            }
            else
            {
                // 2. Triple-raycast fallback (Left, Center, Right) across the crawlspace
                RaycastHit2D leftHit = Physics2D.Raycast(baseCenter + new Vector2(-halfW, 0f), Vector2.up, checkHeight, ceilingMask);
                RaycastHit2D midHit = Physics2D.Raycast(baseCenter, Vector2.up, checkHeight, ceilingMask);
                RaycastHit2D rightHit = Physics2D.Raycast(baseCenter + new Vector2(halfW, 0f), Vector2.up, checkHeight, ceilingMask);

                if (IsValidCeilingObstacle(leftHit) || IsValidCeilingObstacle(midHit) || IsValidCeilingObstacle(rightHit))
                {
                    hasLowCeilingAbove = true;
                }
            }
        }

        if ((blobInputHeld || hasLowCeilingAbove) && !isDashing)
        {
            isBlobForm = true;
        }
        else
        {
            isBlobForm = false;
        }

        // Trigger Instant Shadow Dash
        bool dashTriggered = (Input.GetKeyDown(KeyCode.LeftShift) || virtualDashPressed) && dashCooldownTimer <= 0f && !isBlobForm;
        virtualDashPressed = false;
        if (dashTriggered)
        {
            float dashDir = (horizontalInput != 0f) ? Mathf.Sign(horizontalInput) : lastFacingSign;
            ExecuteInstantShadowDash(dashDir);
            return;
        }

        // Apply movement speed (faster if running or in blob form, affected by slow debuff)
        float currentSpeed = moveSpeed;
        if (isBlobForm)
        {
            currentSpeed *= blobSpeedMultiplier;
            currentSpeed *= speedDebuffMultiplier;
            if (!isDashing && rb != null) rb.gravityScale = gravityScale;
            if (rb != null) rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);
        }
        else
        {
            if (!isDashing && rb != null) rb.gravityScale = gravityScale;
            if (isRunning)
            {
                currentSpeed *= runSpeedMultiplier;
            }
            currentSpeed *= speedDebuffMultiplier;
            if (rb != null) rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);
        }

        // 👇 Update Animator states — but SUPPRESS during active attack animations
        //    so walk/run/idle bools don't fight with the attack state machine transitions.
        bool combatActive = MageCombat.Instance != null && MageCombat.Instance.IsAttacking;
        if (anim != null && !combatActive)
        {
            try
            {
                bool isDescending = !isGrounded && !isDashing && (rb != null && rb.linearVelocity.y <= 0.05f);
                bool isAscendingJump = !isGrounded && !isDashing && (rb != null && rb.linearVelocity.y > 0.05f);

                // Set all core locomotion and airborne bools every frame
                anim.SetBool("isWalking", isGrounded && isWalking);
                anim.SetBool("isRunning", isGrounded && isRunning);
                anim.SetBool("isBlob", isBlobForm);
                anim.SetBool("isJumping", isAscendingJump);
                anim.SetBool("isFalling", isDescending);

                if (isDescending && !hasTriggeredStartLanding)
                {
                    var curState = anim.GetCurrentAnimatorStateInfo(0);
                    if (!curState.IsName("falling") && !curState.IsName("Falling") && !curState.IsName("magefalling") && !curState.IsName("startLanding") && !curState.IsName("Dash"))
                    {
                        if (anim.HasState(0, Animator.StringToHash("falling"))) anim.Play("falling", 0, 0f);
                    }
                }

                if (isBlobForm)
                {
                    var curState = anim.GetCurrentAnimatorStateInfo(0);
                    if (!curState.IsName("blob") && !curState.IsName("Blob") && !curState.IsName("mageblob"))
                    {
                        if (anim.HasState(0, Animator.StringToHash("Blob")))
                        {
                            anim.Play("Blob", 0, 0f);
                        }
                        else if (anim.HasState(0, Animator.StringToHash("blob")))
                        {
                            anim.Play("blob", 0, 0f);
                        }
                        else if (anim.HasState(0, Animator.StringToHash("mageblob")))
                        {
                            anim.Play("mageblob", 0, 0f);
                        }
                    }
                }
            }
            catch (System.Exception) { }
        }

        // Flip sprite based on movement direction (preserving exact initial inspector scales)
        float facing = (lastFacingSign < 0f) ? -1f : 1f;
        float scaleX = Mathf.Max(0.1f, initialAbsScale.x);
        float scaleY = Mathf.Max(0.1f, initialAbsScale.y);
        float scaleZ = Mathf.Max(0.1f, initialAbsScale.z);
        transform.localScale = new Vector3(facing * scaleX, scaleY, scaleZ);

        // Dual-Collider State Switch: Disable ALL old colliders/components that could cause obstruction during blob
        if (isBlobForm && !wasBlobFormLastFrame)
        {
            wasBlobFormLastFrame = true;
            disabledCollidersInBlob.Clear();
            disabledComponentsInBlob.Clear();

            // 1. Disable all colliders on player AND all child GameObjects (hitbox, Hurtbox, attack, weapon, polygon colliders)
            Collider2D[] allPlayerColliders = GetComponentsInChildren<Collider2D>(true);
            foreach (var col in allPlayerColliders)
            {
                if (col != null && col != blobCollider && col.enabled)
                {
                    disabledCollidersInBlob.Add(col);
                    col.enabled = false;
                }
            }

            // 2. Disable any polygon hurtbox animators or attack components during blob form
            var polyAnimators = GetComponentsInChildren<PlayerPolygonColliderAnimator>(true);
            foreach (var pa in polyAnimators)
            {
                if (pa != null && pa.enabled)
                {
                    disabledComponentsInBlob.Add(pa);
                    pa.enabled = false;
                }
            }

            var attackScripts = GetComponentsInChildren<Attack>(true);
            foreach (var att in attackScripts)
            {
                if (att != null && att.enabled)
                {
                    disabledComponentsInBlob.Add(att);
                    att.enabled = false;
                }
            }

            // 3. Activate 4x smaller dedicated solid blob collider
            if (blobCollider != null)
            {
                float feetY = standingColOffset.y - (standingColSize.y * 0.5f);
                float bH = Mathf.Max(0.12f, standingColSize.y * 0.25f);
                float bW = Mathf.Max(0.35f, standingColSize.x * 0.50f);
                blobCollider.size = new Vector2(bW, bH);
                blobCollider.offset = new Vector2(standingColOffset.x, feetY + (bH * 0.5f));
                blobCollider.isTrigger = false; // Solid physics collider
                blobCollider.enabled = true;
            }
        }
        else if (!isBlobForm && wasBlobFormLastFrame)
        {
            wasBlobFormLastFrame = false;

            // 1. Disable blob collider
            if (blobCollider != null)
            {
                blobCollider.enabled = false;
            }

            // 2. Restore all previously disabled colliders
            foreach (var col in disabledCollidersInBlob)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }
            disabledCollidersInBlob.Clear();

            // 3. Restore all disabled components (PlayerPolygonColliderAnimator, Attack)
            foreach (var comp in disabledComponentsInBlob)
            {
                if (comp != null)
                {
                    comp.enabled = true;
                }
            }
            disabledComponentsInBlob.Clear();

            if (!isDashing) rb.gravityScale = gravityScale;
        }

        // 👇 Jump logic (supports both standard jumps and bouncy Blob Leaps)
        bool jumpTriggered = (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space) || virtualJumpPressed) && isGrounded && !isDashing;
        virtualJumpPressed = false;
        if (jumpTriggered)
        {
            PlayRandomJumpVoice();
            jumpLockoutTimer = jumpLockoutDuration; // Lockout ground checks for initial launch phase so full animation plays
            groundedGraceTimer = 0f; // Reset grace timer on jump
            isGrounded = false;

            if (isBlobForm)
            {
                // Bouncy Blob Leap: keep low-profile puddle and bounce into the air
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * 0.95f);
                if (PlayerCombatJuice.Instance != null)
                {
                    PlayerCombatJuice.Instance.TriggerJumpStretch();
                }
            }
            else if (useTeleportJump)
            {
                // Calculate target position in air with ceiling raycast check
                Vector2 startPos = transform.position;
                float maxDist = teleportJumpDistance;
                RaycastHit2D hit = Physics2D.Raycast(startPos, Vector2.up, maxDist, ~0);
                float actualDist = (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject) ? Mathf.Max(0.5f, hit.distance - 0.5f) : maxDist;
                Vector2 targetPos = startPos + new Vector2(0f, actualDist);

                // Execute Pixelated Dissolve & Rebuild Visual FX -> instantly enter 'falling' state on arrival
                if (dissolveFX != null)
                {
                    dissolveFX.PlayDissolveTeleport(startPos, targetPos, () => {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0.5f); // Smooth downward descent transition
                        isFallingState = true;
                        hasTriggeredStartLanding = false;
                        if (anim != null)
                        {
                            try
                            {
                                anim.SetBool("isJumping", false);
                                anim.SetBool("isFalling", true);
                                if (anim.HasState(0, Animator.StringToHash("falling")))
                                    anim.Play("falling", 0, 0f);
                                else if (anim.HasState(0, Animator.StringToHash("Falling")))
                                    anim.Play("Falling", 0, 0f);
                                else if (anim.HasState(0, Animator.StringToHash("magefalling")))
                                    anim.Play("magefalling", 0, 0f);
                            }
                            catch (System.Exception) { }
                        }
                    });
                }
                else
                {
                    transform.position = targetPos;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0.5f);
                    isFallingState = true;
                    hasTriggeredStartLanding = false;
                    if (anim != null)
                    {
                        try
                        {
                            anim.SetBool("isJumping", false);
                            anim.SetBool("isFalling", true);
                            if (anim.HasState(0, Animator.StringToHash("falling")))
                                anim.Play("falling", 0, 0f);
                        }
                        catch (System.Exception) { }
                    }
                }
            }
            else
            {
                // Standard physics jump: Ascend with physics and play jump liftoff
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

                if (PlayerCombatJuice.Instance != null)
                {
                    PlayerCombatJuice.Instance.TriggerJumpStretch();
                }

                if (anim != null)
                {
                    try
                    {
                        anim.SetBool("isJumping", true);
                        anim.SetBool("isFalling", false);
                        anim.SetTrigger("jump");
                        if (anim.HasState(0, Animator.StringToHash("jump")))
                            anim.Play("jump", 0, 0f);
                        else if (anim.HasState(0, Animator.StringToHash("Jump")))
                            anim.Play("Jump", 0, 0f);
                        else if (anim.HasState(0, Animator.StringToHash("air")))
                            anim.Play("air", 0, 0f);
                    }
                    catch (System.Exception) { }
                }
            }
        }

        // isJumping is managed strictly by ascending phase / apex transitions
        // to allow smooth transition into falling and startLanding without AnyState interruptions.

        // --- Apex Fall & Pre-Landing Evaluation ---
        // Any form of descending (walked off ledge, post-dash, post-jump apex) = falling animation
        if (!isGrounded && !isDashing)
        {
            airTimeCounter += Time.deltaTime;

            // 1. Universal Descent Rule: Any downward velocity or zero-gravity apex = immediately enter 'falling'
            if (rb != null && rb.linearVelocity.y <= 0.05f && !hasTriggeredStartLanding)
            {
                if (!isFallingState)
                {
                    isFallingState = true;
                    if (anim != null && !combatActive)
                    {
                        try
                        {
                            anim.SetBool("isJumping", false);
                            anim.SetBool("isFalling", true);
                            if (anim.HasState(0, Animator.StringToHash("falling"))) anim.Play("falling", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("Falling"))) anim.Play("Falling", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("magefalling"))) anim.Play("magefalling", 0, 0f);
                        }
                        catch (System.Exception) { }
                    }
                }
            }

            // 2. Pre-Landing for Drops: Anticipate ground contact while airborne so Frames 1-4 play in air and Frame 5 touches down
            if (rb != null && rb.linearVelocity.y < -1.0f && airTimeCounter >= 0.2f && !hasTriggeredStartLanding)
            {
                int groundMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
                Vector2 rayOrigin = (Vector2)transform.position + feetOffset;
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, landingProximityDistance, groundMask);
                if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.CompareTag("enemy") && hit.collider.gameObject != gameObject)
                {
                    hasTriggeredStartLanding = true;
                    if (anim != null && !combatActive)
                    {
                        try
                        {
                            anim.SetBool("isJumping", false);
                            anim.SetBool("isFalling", false);
                            anim.SetTrigger("startLanding");
                            anim.SetTrigger("StartLanding");
                            if (anim.HasState(0, Animator.StringToHash("startLanding"))) anim.Play("startLanding", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("StartLanding"))) anim.Play("StartLanding", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("magestartlanding"))) anim.Play("magestartlanding", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("land"))) anim.Play("land", 0, 0f);
                            else if (anim.HasState(0, Animator.StringToHash("Land"))) anim.Play("Land", 0, 0f);
                        }
                        catch (System.Exception) { }
                    }
                }
            }
        }
        else
        {
            airTimeCounter = 0f;
            hasTriggeredStartLanding = false;
            if (isFallingState)
            {
                isFallingState = false;
                if (anim != null)
                {
                    try
                    {
                        anim.SetBool("isJumping", false);
                        anim.SetBool("isFalling", false);
                    }
                    catch (System.Exception) { }
                }
            }
        }

        // Dynamic Gravity Modifiers for Snappy 2D Jump Physics
        if (!isDashing && rb != null)
        {
            if (rb.linearVelocity.y < 0f)
            {
                rb.gravityScale = gravityScale * fallGravityMultiplier;
            }
            else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump") && !Input.GetKey(KeyCode.Space) && !virtualJumpHeld)
            {
                rb.gravityScale = gravityScale * lowJumpMultiplier;
            }
            else
            {
                rb.gravityScale = gravityScale;
            }
        }

        // Safeguard & Fluid cancel: Instantly transition from jump/landing states when grounded
        // BUT skip during active attacks so we don't rip the player out of attack animations
        if (isGrounded && jumpLockoutTimer <= 0f && anim != null && !combatActive)
        {
            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool inAirOrLandingState = stateInfo.IsName("jump") || stateInfo.IsName("Jump") ||
                                       stateInfo.IsName("falling") || stateInfo.IsName("Falling") || stateInfo.IsName("magefalling") ||
                                       stateInfo.IsName("startLanding") || stateInfo.IsName("StartLanding") || stateInfo.IsName("magestartlanding") ||
                                       stateInfo.IsName("land") || stateInfo.IsName("Land");

            if (inAirOrLandingState)
            {
                // If holding horizontal movement on touchdown (Frame 5), seamlessly cancel into run/walk immediately!
                if (isRunning && horizontalInput != 0)
                {
                    if (anim.HasState(0, Animator.StringToHash("Run"))) anim.Play("Run");
                    else if (anim.HasState(0, Animator.StringToHash("run"))) anim.Play("run");
                }
                else if (isWalking && horizontalInput != 0)
                {
                    if (anim.HasState(0, Animator.StringToHash("walk"))) anim.Play("walk");
                    else if (anim.HasState(0, Animator.StringToHash("Walk"))) anim.Play("Walk");
                }
                else if (stateInfo.IsName("startLanding") || stateInfo.IsName("StartLanding") || stateInfo.IsName("magestartlanding") || stateInfo.IsName("land") || stateInfo.IsName("Land"))
                {
                    // Allow full 7-frame landing cushion to finish before returning to idle
                    if (stateInfo.normalizedTime >= 0.9f)
                    {
                        if (anim.HasState(0, Animator.StringToHash("idle"))) anim.Play("idle");
                        else if (anim.HasState(0, Animator.StringToHash("Idle"))) anim.Play("Idle");
                    }
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
        if (isBlobForm && blobCollider != null && blobCollider.enabled)
        {
            mainCol = blobCollider;
        }
        else if (standingCollider != null && standingCollider.enabled && !standingCollider.isTrigger)
        {
            mainCol = standingCollider;
        }
        else
        {
            foreach (var col in GetComponents<Collider2D>())
            {
                if (col != null && col.enabled && !col.isTrigger)
                {
                    mainCol = col;
                    break;
                }
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

        Vector2 boxSize = isBlobForm ? new Vector2(Mathf.Min(feetBoxSize.x, blobColliderSize.x * 0.9f), 0.2f) : feetBoxSize;
        Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, boxSize, 0f);

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

    private bool IsValidCeilingObstacle(RaycastHit2D hit)
    {
        if (hit.collider == null) return false;
        if (hit.collider.isTrigger) return false;
        if (hit.collider.gameObject == gameObject) return false;
        if (hit.transform.IsChildOf(transform)) return false;
        if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("enemy")) return false;
        if (hit.collider.GetComponent<IDamageable>() != null) return false;
        return true;
    }


    private void InitVoiceAudio()
    {
        voiceAudioSource = gameObject.GetComponent<AudioSource>();
        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
        }
        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.spatialBlend = 0f; // 2D Audio

        if (jumpVoiceClips == null || jumpVoiceClips.Length == 0)
        {
            jumpVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/jump");
        }
        if (talkVoiceClips == null || talkVoiceClips.Length == 0)
        {
            talkVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/talk");
        }
    }

    public void PlayRandomJumpVoice()
    {
        var vc = GetComponent<SpawnOfChaos.Entities.PlayerMageVoiceController>() ?? GetComponentInParent<SpawnOfChaos.Entities.PlayerMageVoiceController>();
        if (vc != null) { vc.PlayJumpVoice(); return; }
        if (jumpVoiceClips == null || jumpVoiceClips.Length == 0) return;
        if (voiceAudioSource == null) InitVoiceAudio();
        AudioClip clip = jumpVoiceClips[UnityEngine.Random.Range(0, jumpVoiceClips.Length)];
        if (clip != null)
        {
            float sfxVol = AudioManager.Instance != null ? AudioManager.Instance.GetRealSFXVolume() : 1.0f;
            voiceAudioSource.PlayOneShot(clip, sfxVol);
        }
    }

    public void PlayRandomTalkVoice()
    {
        var vc = GetComponent<SpawnOfChaos.Entities.PlayerMageVoiceController>() ?? GetComponentInParent<SpawnOfChaos.Entities.PlayerMageVoiceController>();
        if (vc != null) { vc.PlayTalkVoice(); return; }
        if (talkVoiceClips == null || talkVoiceClips.Length == 0) return;
        if (voiceAudioSource == null) InitVoiceAudio();
        AudioClip clip = talkVoiceClips[UnityEngine.Random.Range(0, talkVoiceClips.Length)];
        if (clip != null)
        {
            float sfxVol = AudioManager.Instance != null ? AudioManager.Instance.GetRealSFXVolume() : 1.0f;
            voiceAudioSource.PlayOneShot(clip, sfxVol);
        }
    }

}
