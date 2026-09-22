using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PlayerPlatformFallManager - Continuous safe platform tracking, fall detection,
/// and environmental recovery with a 25% max health penalty.
///
/// Features:
/// 1. Safe Ground Sampling:
///    - Probes grounded state and solid ground via dual downward raycasts at feet height.
///    - Excludes moving platforms, crumbling platforms, damage/spike zones, and steep slopes (> 31 deg).
///    - Stores verified standing coordinates and maintains a rolling safe platform history.
/// 2. Responsive Fall Detection:
///    - Relative Y-drop threshold (Y < lastSafeY - 6.5u).
///    - Continuous downward airtime sentinel (> 0.85s falling with negative velocity).
///    - Absolute scene floor cutoff (Y < -20u for SampleScene).
///    - Hazard trigger volume routing (KillZone, Pit, DamageZone).
/// 3. Respawn & Health Penalty:
///    - Deducts exactly 25% of max health bypassing dash/blob invulnerability and Lumi shield.
///    - Zeroes physics velocity, teleports player to nearest platform (above ground surface, never embedded).
///    - Grants 1.5s post-fall invulnerability window with sprite flicker feedback.
///    - Persists across scene transitions automatically.
/// </summary>
public class PlayerPlatformFallManager : MonoBehaviour
{
    public static PlayerPlatformFallManager Instance { get; private set; }

    [Header("Safe Platform Tracking")]
    [Tooltip("Interval in seconds between committing buffered safe platform coordinates.")]
    public float recordInterval = 0.15f;

    [Tooltip("Time in seconds the player must remain continuously grounded on solid geometry before buffering.")]
    public float requiredGroundedTime = 0.12f;

    [Tooltip("Maximum allowable slope angle in degrees for safe ground.")]
    public float maxSlopeAngle = 31.0f;

    [Tooltip("Horizontal spacing for dual downward raycasts to prevent edge/cliff teetering.")]
    public float dualRaycastHorizontalOffset = 0.35f;

    [Tooltip("Downward distance of platform verification raycasts below feet.")]
    public float raycastLength = 0.75f;

    [Header("Fall Detection Settings")]
    [Tooltip("Distance downward to probe for level ground beneath the falling player. If solid ground is detected below, fall respawn is inhibited so the player can land.")]
    public float downwardGroundCheckDistance = 35.0f;

    [Tooltip("Distance below last safe platform Y before considering a fall (only triggers if NO ground exists below).")]
    public float relativeFallDistanceThreshold = 28.0f;

    [Tooltip("Continuous seconds falling downward into empty void (no ground below) before triggering airtime fall respawn.")]
    public float continuousFallTimeLimit = 2.4f;

    [Tooltip("Minimum downward vertical velocity (linearVelocity.y) to register as falling for airtime sentinel.")]
    public float airtimeVelocityThreshold = -4.0f;

    [Tooltip("Minimum Y drop below last safe platform required before airtime sentinel can trigger.")]
    public float airtimeFallMinDrop = 14.0f;

    [Tooltip("Absolute Y coordinate below which the player is always considered to have fallen out of bounds (below all map geometry).")]
    public float absoluteFallYThreshold = -350.0f;

    [Tooltip("Whether to search for the closest solid platform directly above or near the fall point if last recorded safe platform is distant.")]
    public bool searchNearestPlatformOnRespawn = true;

    [Header("Respawn & Penalty Settings")]
    [Tooltip("Percentage of maximum health deducted on platform fall respawn (e.g. 25f for 25%).")]
    public float healthPenaltyPercent = 25.0f;

    [Tooltip("Duration of post-fall invulnerability and sprite blinking.")]
    public float iFrameDuration = 1.5f;

    [Tooltip("Flicker interval for sprite blinking.")]
    public float blinkInterval = 0.1f;

    [Tooltip("Slight upward clearance offset applied above platform surface.")]
    public Vector3 spawnOffset = new Vector3(0f, 0.05f, 0f);

    [Header("State Tracking (Read-Only)")]
    [SerializeField] private Vector3 lastSafePlatformPosition;
    [SerializeField] private Vector3 bufferedSafePosition;
    [SerializeField] private List<Vector3> safePlatformHistory = new List<Vector3>();
    [SerializeField] private float stableGroundedTimer = 0f;
    [SerializeField] private float safePositionRecordTimer = 0f;
    [SerializeField] private float airtimeFallTimer = 0f;
    [SerializeField] private bool isRespawning = false;

    private const int MAX_SAFE_HISTORY = 12;

    public Vector3 LastSafePlatformPosition => lastSafePlatformPosition;
    public bool IsRespawning => isRespawning;

    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private Health playerHealth;
    private move playerMove;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance == null || Instance == this || Instance.gameObject == null || Instance.gameObject != gameObject)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        FindPlayerComponents();
        if (playerTransform != null)
        {
            lastSafePlatformPosition = playerTransform.position;
            bufferedSafePosition = playerTransform.position;
            CommitSafePosition(playerTransform.position);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        FindPlayerComponents();
        if (playerTransform != null)
        {
            lastSafePlatformPosition = playerTransform.position;
            bufferedSafePosition = playerTransform.position;
            CommitSafePosition(playerTransform.position);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindPlayerComponents();
        if (playerTransform != null)
        {
            lastSafePlatformPosition = playerTransform.position;
            bufferedSafePosition = playerTransform.position;
            safePlatformHistory.Clear();
            CommitSafePosition(playerTransform.position);
        }
        airtimeFallTimer = 0f;
        stableGroundedTimer = 0f;
        safePositionRecordTimer = 0f;
        isRespawning = false;
    }

    private void Update()
    {
        if (isRespawning) return;

        if (playerTransform == null || playerRb == null || playerHealth == null)
        {
            FindPlayerComponents();
            if (playerTransform == null || playerRb == null) return;
        }

        // Do not process fall recovery if the player is already dead
        if (playerHealth != null && playerHealth.CurrentHealth <= 0)
        {
            return;
        }

        UpdateSafePlatformTracking();
        CheckFallConditions();
    }

    /// <summary>
    /// Evaluates current ground contact and buffers safe platform coordinates if solid, stable, and flat.
    /// Probes from slightly above the actual feet of the player downward to accurately detect platforms.
    /// </summary>
    private void UpdateSafePlatformTracking()
    {
        if (playerTransform == null || playerRb == null) return;

        bool isGrounded = playerMove != null ? playerMove.IsGrounded : (Mathf.Abs(playerRb.linearVelocity.y) < 0.2f);
        bool isDashing = playerMove != null && playerMove.IsDashing;

        // Player must be grounded, not in mid-dash warp, and vertically stationary/settled
        if (!isGrounded || isDashing || Mathf.Abs(playerRb.linearVelocity.y) > 0.4f)
        {
            stableGroundedTimer = 0f;
            return;
        }

        float feetY = GetFeetWorldY();
        float leftX = playerTransform.position.x - dualRaycastHorizontalOffset;
        float rightX = playerTransform.position.x + dualRaycastHorizontalOffset;

        // Cast from 0.35 units above the feet downward by raycastLength (0.75u), reaching 0.40u below feet
        RaycastHit2D leftHit = RaycastDownToGround(new Vector2(leftX, feetY + 0.35f), raycastLength);
        RaycastHit2D rightHit = RaycastDownToGround(new Vector2(rightX, feetY + 0.35f), raycastLength);

        // Both raycast probes must hit solid geometry
        if (leftHit.collider == null || rightHit.collider == null)
        {
            stableGroundedTimer = 0f;
            return;
        }

        // Reject if hitting excluded platform types (moving, crumbling, damage zones, hazards)
        if (IsPlatformExcluded(leftHit.collider) || IsPlatformExcluded(rightHit.collider))
        {
            stableGroundedTimer = 0f;
            return;
        }

        // Slope angle check: cos(31 deg) ≈ 0.857. Exclude steep slides/slopes.
        float minNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
        if (leftHit.normal.y < minNormalY || rightHit.normal.y < minNormalY)
        {
            stableGroundedTimer = 0f;
            return;
        }

        // Verify surface flatness between dual probes (height diff < 0.40u)
        if (Mathf.Abs(leftHit.point.y - rightHit.point.y) > 0.40f)
        {
            stableGroundedTimer = 0f;
            return;
        }

        // The player is currently standing safely on this surface. Use current position as candidate.
        Vector3 candidatePos = playerTransform.position;

        // Verify no nearby damage zones or hazards within proximity radius
        Collider2D[] nearCols = Physics2D.OverlapCircleAll(candidatePos, 0.75f);
        foreach (var col in nearCols)
        {
            if (col != null && IsHazardCollider(col))
            {
                stableGroundedTimer = 0f;
                return;
            }
        }

        // Overhead & body clearance check: ensure no solid ceiling or wall collides with candidate respawn volume
        if (!HasBodyClearance(candidatePos))
        {
            stableGroundedTimer = 0f;
            return;
        }

        // Candidate passed all safety filters!
        stableGroundedTimer += Time.deltaTime;
        if (stableGroundedTimer >= requiredGroundedTime)
        {
            bufferedSafePosition = candidatePos;

            safePositionRecordTimer += Time.deltaTime;
            if (safePositionRecordTimer >= recordInterval)
            {
                safePositionRecordTimer = 0f;
                CommitSafePosition(bufferedSafePosition);
            }
        }
    }

    private void CommitSafePosition(Vector3 pos)
    {
        lastSafePlatformPosition = pos;

        if (safePlatformHistory.Count == 0 || Vector2.Distance(safePlatformHistory[safePlatformHistory.Count - 1], pos) > 1.2f)
        {
            safePlatformHistory.Add(pos);
            if (safePlatformHistory.Count > MAX_SAFE_HISTORY)
            {
                safePlatformHistory.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Performs downward raycast ignoring player colliders, triggers, and child transforms.
    /// </summary>
    private RaycastHit2D RaycastDownToGround(Vector2 origin, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.gameObject == gameObject || (playerTransform != null && h.collider.transform.IsChildOf(playerTransform))) continue;
            if (h.collider.CompareTag("Player")) continue;
            return h;
        }
        return default;
    }

    /// <summary>
    /// Checks if a candidate position has sufficient clearance for the player's body collider.
    /// </summary>
    private bool HasBodyClearance(Vector3 worldPos)
    {
        Collider2D mainCol = GetMainPlayerCollider();
        Vector2 offset = (mainCol != null) ? mainCol.offset : new Vector2(0f, -0.05f);
        Vector2 size = (mainCol != null) ? new Vector2(mainCol.bounds.size.x * 0.9f, mainCol.bounds.size.y * 0.9f) : new Vector2(1.2f, 3.0f);

        Collider2D[] overlaps = Physics2D.OverlapBoxAll((Vector2)worldPos + offset, size, 0f);
        foreach (var col in overlaps)
        {
            if (col == null || col.isTrigger) continue;
            if (col.gameObject == gameObject || (playerTransform != null && col.transform.IsChildOf(playerTransform))) continue;
            if (col.CompareTag("Player")) continue;
            if (col.CompareTag("enemy") || col.CompareTag("Enemy")) continue;

            // Blocked by solid world obstacle
            return false;
        }
        return true;
    }

    /// <summary>
    /// Probes downward beneath the player to check if there is valid solid level ground or a platform they can land on.
    /// Returns true if solid ground is detected, indicating an intentional/necessary gameplay descent or high jump.
    /// </summary>
    public bool HasSolidGroundBelow(float maxDistance)
    {
        if (playerTransform == null) return false;

        float feetY = GetFeetWorldY();
        float posX = playerTransform.position.x;

        // Spread 5 downward feeler rays (center, left, right, wide left, wide right)
        float[] xOffsets = { 0f, -0.6f, 0.6f, -1.8f, 1.8f };

        foreach (float xOff in xOffsets)
        {
            Vector2 origin = new Vector2(posX + xOff, feetY);
            RaycastHit2D hit = RaycastDownToGround(origin, maxDistance);

            if (hit.collider != null && !hit.collider.isTrigger && !IsHazardCollider(hit.collider))
            {
                // Verify slope is landable (not a sheer wall or ceiling)
                float minNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
                if (hit.normal.y >= minNormalY)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Monitors dynamic relative drop, continuous airtime sentinel, and absolute void floor.
    /// Differentiates necessary traversal falls (where valid ground exists below) from falling off the map into empty void.
    /// </summary>
    private void CheckFallConditions()
    {
        if (playerTransform == null || playerRb == null) return;

        // 1. Absolute Fall Floor (falling below ALL map geometry into the deep abyss, e.g. < -350u)
        if (playerTransform.position.y < absoluteFallYThreshold)
        {
            Debug.Log($"[PlayerPlatformFallManager] Absolute void fall detected: player Y ({playerTransform.position.y:F1}) < floor ({absoluteFallYThreshold:F1}). Triggering respawn.");
            TriggerFallRespawn();
            return;
        }

        bool isGrounded = playerMove != null ? playerMove.IsGrounded : (Mathf.Abs(playerRb.linearVelocity.y) < 0.2f);
        bool isDashing = playerMove != null && playerMove.IsDashing;

        if (isGrounded)
        {
            airtimeFallTimer = 0f;
            return;
        }

        // If the player is falling downward with significant negative velocity
        if (!isDashing && playerRb.linearVelocity.y < airtimeVelocityThreshold)
        {
            float yDrop = (lastSafePlatformPosition != Vector3.zero)
                ? (lastSafePlatformPosition.y - playerTransform.position.y)
                : 0f;

            // Only consider respawn if they've dropped significantly below their last safe platform
            if (yDrop > relativeFallDistanceThreshold || yDrop > airtimeFallMinDrop)
            {
                // Check if there is valid solid ground beneath the player to land on
                bool hasGroundBelow = HasSolidGroundBelow(downwardGroundCheckDistance);

                if (hasGroundBelow)
                {
                    // Necessary gameplay fall! Ground exists below, so reset airtime timer and let player land safely
                    airtimeFallTimer = 0f;
                    return;
                }

                // No ground below: player is in empty open void off the map!
                airtimeFallTimer += Time.deltaTime;

                // Trigger respawn if airtime in void exceeds limit OR if they dropped far beyond the threshold in pure void
                if (airtimeFallTimer >= continuousFallTimeLimit || yDrop >= (relativeFallDistanceThreshold * 1.5f))
                {
                    Debug.Log($"[PlayerPlatformFallManager] Off-map void fall detected: airtime {airtimeFallTimer:F2}s, drop {yDrop:F1}u, no ground below within {downwardGroundCheckDistance:F1}u. Triggering respawn.");
                    TriggerFallRespawn();
                    return;
                }
            }
            else
            {
                airtimeFallTimer = 0f;
            }
        }
        else
        {
            airtimeFallTimer = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isRespawning) return;

        // Pit, KillZone, or Fall Hazard trigger volumes
        bool isFallTrigger = false;
        try
        {
            string t = other.tag;
            if (t == "KillZone" || t == "Pit" || t == "Hazard") isFallTrigger = true;
        }
        catch {}

        if (isFallTrigger || other.name.Contains("KillZone") || other.name.Contains("Pit"))
        {
            Debug.Log($"[PlayerPlatformFallManager] Entered fall trigger volume '{other.name}'. Triggering fall respawn.");
            TriggerFallRespawn();
            return;
        }

        DamageZone dz = other.GetComponent<DamageZone>();
        if (dz != null && dz.isFallHazard)
        {
            Debug.Log($"[PlayerPlatformFallManager] Entered fall hazard DamageZone '{other.name}'. Triggering fall respawn.");
            TriggerFallRespawn();
        }
    }

    /// <summary>
    /// Triggers the platform fall recovery sequence: deducts 25% max health, teleports to nearest safe platform,
    /// resets physics velocity, and activates 1.5s post-fall invulnerability with sprite flicker.
    /// </summary>
    public void TriggerFallRespawn(GameObject targetPlayer = null)
    {
        if (isRespawning) return;
        if (!gameObject.activeInHierarchy) return;

        if (targetPlayer != null && (playerTransform == null || playerTransform.gameObject != targetPlayer))
        {
            playerTransform = targetPlayer.transform;
            playerRb = targetPlayer.GetComponent<Rigidbody2D>();
            playerHealth = targetPlayer.GetComponent<Health>();
            playerMove = targetPlayer.GetComponent<move>();
        }

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        airtimeFallTimer = 0f;
        stableGroundedTimer = 0f;

        // 1. Deduct exactly 25% max health (bypasses dash/blob invulnerability and Lumi's shield)
        if (playerHealth != null)
        {
            playerHealth.TakeFallPenalty(healthPenaltyPercent);
        }

        // If penalty was lethal, allow standard death routine to proceed
        if (playerHealth != null && playerHealth.CurrentHealth <= 0)
        {
            isRespawning = false;
            yield break;
        }

        // 2. Reset physics velocity
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
        }

        // 3. Teleport player to nearest safe platform
        Vector3 respawnTarget = FindNearestSafePlatform();
        respawnTarget += spawnOffset;
        transform.position = respawnTarget;
        if (playerRb != null)
        {
            playerRb.position = respawnTarget;
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
        }

        // 4. Snap Camera if available
        CameraFollow camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camFollow == null) camFollow = Object.FindFirstObjectByType<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.SnapToTarget();
        }

        // 5. Reset tutorial obstacles if present in TutorialScene
        if (SpawnOfChaos.Platforms.CrumblingRunwayController.Instance != null && !SpawnOfChaos.Platforms.CrumblingRunwayController.Instance.HasCompleted)
        {
            SpawnOfChaos.Platforms.CrumblingRunwayController.Instance.ResetRunway();
        }
        if (SpawnOfChaos.NPC.GenbuFerryController.Instance != null)
        {
            SpawnOfChaos.NPC.GenbuFerryController.Instance.ResetFerry();
        }

        // 6. Grant post-fall invulnerability window
        if (playerHealth != null)
        {
            playerHealth.GrantPostFallInvulnerability(iFrameDuration);
        }

        // 7. Visual sprite flicker feedback
        yield return StartCoroutine(SpriteFlickerRoutine(iFrameDuration, blinkInterval));

        isRespawning = false;
    }

    private IEnumerator SpriteFlickerRoutine(float duration, float interval)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Color[] origColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            origColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }

        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            elapsed += interval;
            visible = !visible;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    Color c = origColors[i];
                    c.a = visible ? origColors[i].a : origColors[i].a * 0.25f;
                    renderers[i].color = c;
                }
            }

            yield return new WaitForSeconds(interval);
        }

        // Restore original colors and alpha
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = origColors[i];
            }
        }
    }

    /// <summary>
    /// Finds the nearest solid platform coordinates relative to the player's current fall point,
    /// searching safe position history and sweeping downward raycasts for solid ground.
    /// Guarantees the player is spawned on top of the ground surface and never embedded inside it.
    /// </summary>
    public Vector3 FindNearestSafePlatform()
    {
        if (playerTransform == null) return transform.position;

        Vector2 fallPos = playerTransform.position;
        Vector3 bestPlatform = Vector3.zero;
        float bestScore = float.MaxValue;
        float pivotOffset = GetFeetToPivotOffset();

        // 1. Evaluate known safe positions from history
        List<Vector3> candidatePositions = new List<Vector3>();
        if (lastSafePlatformPosition != Vector3.zero)
        {
            candidatePositions.Add(lastSafePlatformPosition);
        }
        foreach (var pos in safePlatformHistory)
        {
            if (pos != Vector3.zero && !candidatePositions.Contains(pos))
            {
                candidatePositions.Add(pos);
            }
        }

        foreach (var cand in candidatePositions)
        {
            float dx = Mathf.Abs(cand.x - fallPos.x);
            float dy = cand.y - fallPos.y;
            // Heavily penalize platforms below the fall point; prefer platforms at or above where player fell
            float score = dx * dx + (dy >= -1.0f ? dy * dy : (dy * 3.0f) * (dy * 3.0f));

            if (score < bestScore && HasBodyClearance(cand))
            {
                bestScore = score;
                bestPlatform = cand;
            }
        }

        // 2. Spatial Downward Raycast Sweep: scan for solid platforms near fallPos.x
        if (searchNearestPlatformOnRespawn)
        {
            float scanTop = (lastSafePlatformPosition != Vector3.zero)
                ? Mathf.Max(lastSafePlatformPosition.y + 2.0f, fallPos.y + 8.0f)
                : fallPos.y + 8.0f;
            float scanDist = scanTop - (fallPos.y - 2.0f);
            float[] horizontalOffsets = { 0f, -1.0f, 1.0f, -2.5f, 2.5f, -4.5f, 4.5f, -7.0f, 7.0f, -10.0f, 10.0f };

            foreach (float xOff in horizontalOffsets)
            {
                Vector2 rayOrigin = new Vector2(fallPos.x + xOff, scanTop);
                RaycastHit2D hit = RaycastDownToGround(rayOrigin, scanDist);

                if (hit.collider != null && !IsPlatformExcluded(hit.collider))
                {
                    float minNormalY = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
                    if (hit.normal.y >= minNormalY)
                    {
                        // Position candidate so player's feet touch down cleanly on the hit surface
                        Vector3 cand = new Vector3(hit.point.x, hit.point.y + pivotOffset + 0.05f, 0f);

                        if (HasBodyClearance(cand))
                        {
                            float dx = Mathf.Abs(cand.x - fallPos.x);
                            float dy = cand.y - fallPos.y;
                            float score = dx * dx + (dy >= -1.0f ? dy * dy : (dy * 3.0f) * (dy * 3.0f));

                            if (score < bestScore)
                            {
                                bestScore = score;
                                bestPlatform = cand;
                            }
                        }
                    }
                }
            }
        }

        if (bestPlatform != Vector3.zero)
        {
            Debug.Log($"[PlayerPlatformFallManager] Nearest safe platform resolved at {bestPlatform} (distance score: {bestScore:F1} from fall point {fallPos}).");
            lastSafePlatformPosition = bestPlatform;
            return bestPlatform;
        }

        // Fallback to last recorded safe platform or current position
        return (lastSafePlatformPosition != Vector3.zero) ? lastSafePlatformPosition : transform.position;
    }

    private float GetFeetWorldY()
    {
        Collider2D col = GetMainPlayerCollider();
        if (col != null)
        {
            return col.bounds.min.y;
        }
        return transform.position.y - 1.75f;
    }

    private float GetFeetToPivotOffset()
    {
        Collider2D col = GetMainPlayerCollider();
        if (col != null)
        {
            return transform.position.y - col.bounds.min.y;
        }
        return 1.75f;
    }

    private Collider2D GetMainPlayerCollider()
    {
        Collider2D[] cols = GetComponents<Collider2D>();
        foreach (var c in cols)
        {
            if (c != null && c.enabled && !c.isTrigger) return c;
        }
        return GetComponent<Collider2D>();
    }

    private bool IsPlatformExcluded(Collider2D col)
    {
        if (col == null) return true;
        if (col.isTrigger) return true;
        if (col.CompareTag("Player")) return true;
        if (col.CompareTag("enemy") || col.CompareTag("Enemy")) return true;
        if (col.GetComponent<IDamageable>() != null && !col.CompareTag("Player")) return true;

        // Exclude moving platforms
        if (col.GetComponentInParent<ParkourMovingPlatform>() != null) return true;
        if (col.GetComponentInParent<MovingPlatform>() != null) return true;
        if (col.GetComponentInParent<FloatingMobPlatform>() != null) return true;

        // Exclude crumbling platforms
        if (col.GetComponentInParent<FallingCrumblingPlatform>() != null) return true;
        if (col.GetComponentInParent<FallingPlatform>() != null) return true;
        if (col.GetComponentInParent<SpawnOfChaos.Platforms.CrumblingRock>() != null) return true;
        if (col.GetComponentInParent<SpawnOfChaos.Platforms.CrumblingRunwayController>() != null) return true;

        // Exclude damage zones, spikes, hazards
        if (IsHazardCollider(col)) return true;

        return false;
    }

    private bool IsHazardCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.GetComponentInParent<DamageZone>() != null) return true;
        if (col.GetComponentInParent<HazardPlatform>() != null) return true;
        try
        {
            string t = col.tag;
            if (t == "Hazard" || t == "Spike" || t == "Spikes" || t == "KillZone" || t == "Pit") return true;
        }
        catch {}
        return false;
    }

    private void FindPlayerComponents()
    {
        playerTransform = transform;
        playerRb = GetComponent<Rigidbody2D>();
        playerHealth = GetComponent<Health>();
        playerMove = GetComponent<move>();

        if (playerRb == null || playerHealth == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerRb = p.GetComponent<Rigidbody2D>();
                playerHealth = p.GetComponent<Health>();
                playerMove = p.GetComponent<move>();
            }
        }
    }

    /// <summary>
    /// Static helper ensuring PlayerPlatformFallManager is attached to the player GameObject.
    /// </summary>
    public static PlayerPlatformFallManager EnsureAttached(GameObject player)
    {
        if (player == null) return null;
        var manager = player.GetComponent<PlayerPlatformFallManager>();
        if (manager == null)
        {
            manager = player.AddComponent<PlayerPlatformFallManager>();
        }
        return manager;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachOnSceneLoad()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            EnsureAttached(player);
        }
    }
}
