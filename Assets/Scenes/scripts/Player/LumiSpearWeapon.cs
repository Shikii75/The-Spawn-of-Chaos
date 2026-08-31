using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpearState
{
    CarriedByLumi,
    ThrownFlight,
    Embedded,
    Recalling,
    MeleeThrusting
}

/// <summary>
/// LumiSpearWeapon - Complete Physical Blue-Magic Spear Traversal & Combat System:
/// 1. Independent Floating: Floats a bit away from Lumi with ambient floating/fading blue magic motes.
/// 2. Direction-Based Physical Throw ('Q' / Right Click): Fires spear as physics projectile with blue trail.
/// 3. Wall Sticking & Platform Perch: Embeds horizontally into wall, creating a solid platform on top.
/// 4. Instant Auto-Drag: Upon hitting wall, Lumi immediately shoots tether & drags player on top of spear.
/// 5. Explode & Super-Launch ('X' Key): Detonates spear in blue AoE blast and launches player up at 22 units/s.
/// 6. Seamless Descent: Flows directly into MageFalling at apex.
/// </summary>
public class LumiSpearWeapon : MonoBehaviour
{
    public static LumiSpearWeapon Instance { get; private set; }

    [Header("Spear Physics Settings")]
    public float throwSpeed = 34f;
    public float maxThrowDistance = 35f;
    public int pierceDamage = 50;
    public int explosionDamage = 90;
    public float explosionRadius = 5.5f;
    public float explosionKnockback = 15f;
    public float superLaunchVelocity = 22f;
    public float recallSpeed = 38f;

    [Header("Independent Float Offset from Lumi")]
    public Vector3 independentOffset = new Vector3(0.9f, 0.5f, 0f);
    public float floatFrequency = 2.6f;
    public float floatAmplitude = 0.12f;

    [Header("Blue Magic Colors")]
    public Color magicAuraColor = new Color(0.15f, 0.88f, 1.0f, 1.0f);
    public Color magicTrailColor = new Color(0.35f, 0.95f, 1.0f, 0.95f);
    public Color moteColor = new Color(0.4f, 0.92f, 1.0f, 0.75f);

    public SpearState CurrentState { get; private set; } = SpearState.CarriedByLumi;
    public bool IsEmbedded => CurrentState == SpearState.Embedded;

    private SpriteRenderer spearRenderer;
    private TrailRenderer trailRenderer;
    private LineRenderer tetherLineRenderer;
    private ParticleSystem moteParticleSystem;
    private BoxCollider2D platformCollider;
    private PlatformEffector2D platformEffector;

    private Vector2 throwOrigin;
    private Vector2 throwDirection;
    private Vector3 embeddedPosition;
    private Quaternion embeddedRotation;
    private Transform attachedSurface;

    private LightOrbCompanion lumi;
    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private move playerMove;

    private bool isAutoDragging = false;
    private Vector3 dragTargetPosition;
    private float dragTimer = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitSpear()
    {
        if (Instance == null)
        {
            GameObject spearGO = new GameObject("LumiSpearWeapon");
            spearGO.AddComponent<LumiSpearWeapon>();
            DontDestroyOnLoad(spearGO);
            Debug.Log("[LumiSpearWeapon] System spawned into scene!");
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupComponents();
    }

    void Start()
    {
        FindReferences();
    }

    void FindReferences()
    {
        lumi = LightOrbCompanion.Instance ?? FindFirstObjectByType<LightOrbCompanion>();
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerRb = playerGO.GetComponent<Rigidbody2D>();
            playerMove = playerGO.GetComponent<move>();
        }
    }

    void SetupComponents()
    {
        // 1. Sprite Renderer
        spearRenderer = GetComponent<SpriteRenderer>();
        if (spearRenderer == null) spearRenderer = gameObject.AddComponent<SpriteRenderer>();
        LoadSpearSprite();
        spearRenderer.sortingLayerName = "Default";
        spearRenderer.sortingOrder = 20;

        // 2. Platform Collider (Solid perch top when embedded)
        platformCollider = GetComponent<BoxCollider2D>();
        if (platformCollider == null) platformCollider = gameObject.AddComponent<BoxCollider2D>();
        platformCollider.size = new Vector2(2.4f, 0.35f);
        platformCollider.offset = new Vector2(0f, 0.1f);
        platformCollider.usedByEffector = true;
        platformCollider.enabled = false; // Only enabled when embedded

        platformEffector = GetComponent<PlatformEffector2D>();
        if (platformEffector == null) platformEffector = gameObject.AddComponent<PlatformEffector2D>();
        platformEffector.useOneWay = true;
        platformEffector.surfaceArc = 160f;
        platformEffector.enabled = false;

        // 3. Trail Renderer (Radiant Blue Energy Trail)
        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null) trailRenderer = gameObject.AddComponent<TrailRenderer>();
        trailRenderer.time = 0.32f;
        trailRenderer.startWidth = 0.55f;
        trailRenderer.endWidth = 0.02f;
        trailRenderer.material = CreateUnlitMaterial(magicTrailColor);
        trailRenderer.startColor = magicTrailColor;
        trailRenderer.endColor = new Color(magicTrailColor.r, magicTrailColor.g, magicTrailColor.b, 0f);
        trailRenderer.emitting = false;

        // 4. Line Renderer for Arcane Drag Tether
        tetherLineRenderer = GetComponent<LineRenderer>();
        if (tetherLineRenderer == null) tetherLineRenderer = gameObject.AddComponent<LineRenderer>();
        tetherLineRenderer.startWidth = 0.22f;
        tetherLineRenderer.endWidth = 0.12f;
        tetherLineRenderer.material = CreateUnlitMaterial(magicAuraColor);
        tetherLineRenderer.startColor = magicAuraColor;
        tetherLineRenderer.endColor = new Color(0.4f, 0.95f, 1.0f, 0.85f);
        tetherLineRenderer.positionCount = 2;
        tetherLineRenderer.enabled = false;

        // 5. Floating/Fading Blue Magic Motes Particle System
        SetupMoteParticles();
    }

    void LoadSpearSprite()
    {
        if (spearRenderer.sprite != null) return;
        Sprite spearSprite = Resources.Load<Sprite>("LumiSpear") ??
                             Resources.Load<Sprite>("items/LumiSpear") ??
                             Resources.Load<Sprite>("Art/Items/LumiSpear");
        if (spearSprite != null)
        {
            spearRenderer.sprite = spearSprite;
        }
    }

    void SetupMoteParticles()
    {
        moteParticleSystem = GetComponent<ParticleSystem>();
        if (moteParticleSystem == null) moteParticleSystem = gameObject.AddComponent<ParticleSystem>();

        var main = moteParticleSystem.main;
        main.startColor = moteColor;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = moteParticleSystem.emission;
        emission.rateOverTime = 12f;

        var shape = moteParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.0f, 0.2f, 0.1f);

        var colOverLifetime = moteParticleSystem.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(moteColor, 0f), new GradientColorKey(new Color(0.7f, 1f, 1f), 0.5f), new GradientColorKey(moteColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        moteParticleSystem.Play();
    }

    Material CreateUnlitMaterial(Color col)
    {
        Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Material m = new Material(s);
        m.color = col;
        return m;
    }

    void Update()
    {
        if (lumi == null || playerTransform == null)
        {
            FindReferences();
        }

        if (spearRenderer != null && spearRenderer.sprite == null)
        {
            LoadSpearSprite();
        }

        HandleInput();

        switch (CurrentState)
        {
            case SpearState.CarriedByLumi:
                UpdateIndependentFloating();
                break;

            case SpearState.ThrownFlight:
                UpdateFlight();
                break;

            case SpearState.Embedded:
                UpdateEmbedded();
                break;

            case SpearState.Recalling:
                UpdateRecalling();
                break;
        }

        if (isAutoDragging)
        {
            UpdateAutoDrag();
        }
    }

    void HandleInput()
    {
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return;

        // 'Q' Key or Right-Click: Throw Spear
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1))
        {
            if (CurrentState == SpearState.CarriedByLumi)
            {
                Throw(GetAimDirection());
            }
            else if (CurrentState == SpearState.ThrownFlight)
            {
                Recall();
            }
        }

        // 'X' Key: Explode Spear & Super Launch Player
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (CurrentState == SpearState.Embedded)
            {
                ExplodeAndSuperLaunch();
            }
        }
    }

    // --- State 1: Independent Floating beside Lumi ---
    void UpdateIndependentFloating()
    {
        Vector3 basePos = (lumi != null) ? lumi.transform.position : (playerTransform != null ? playerTransform.position : Vector3.zero);
        float facing = (playerTransform != null && playerTransform.localScale.x < 0) ? -1f : 1f;

        // Independent hovering physics with sinusoidal wave
        float bobY = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        float bobX = Mathf.Cos(Time.time * (floatFrequency * 0.6f)) * (floatAmplitude * 0.5f);

        Vector3 targetPos = basePos + new Vector3(independentOffset.x * facing + bobX, independentOffset.y + bobY, 0f);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 12f);

        // Smoothly orient towards aim direction (safe from inf/NaN on touch/simulator)
        Vector2 aimDir = GetAimDirection();
        float targetAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, targetAngle), Time.deltaTime * 10f);

        if (spearRenderer != null && playerTransform != null)
        {
            SpriteRenderer pSr = playerTransform.GetComponentInChildren<SpriteRenderer>();
            if (pSr != null)
            {
                spearRenderer.sortingLayerID = pSr.sortingLayerID;
                spearRenderer.sortingOrder = pSr.sortingOrder + 2;
            }
        }
    }

    // --- State 2: Physical Thrown Flight ---
    void UpdateFlight()
    {
        transform.position += (Vector3)(throwDirection * throwSpeed * Time.deltaTime);

        if (Vector2.Distance(throwOrigin, transform.position) >= maxThrowDistance)
        {
            Recall();
            return;
        }

        // Raycast precision collision
        RaycastHit2D hit = Physics2D.Raycast(transform.position, throwDirection, throwSpeed * Time.deltaTime * 1.6f, ~LayerMask.GetMask("Player", "Ignore Raycast"));
        if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.CompareTag("Player"))
        {
            if (hit.collider.CompareTag("enemy") || hit.collider.GetComponent<Health>() != null || hit.collider.GetComponentInParent<Health>() != null)
            {
                DamageTarget(hit.collider.gameObject, pierceDamage);
            }
            else
            {
                EmbedInWall(hit);
            }
        }
    }

    // --- State 3: Embedded in Wall / Platform Perch ---
    void UpdateEmbedded()
    {
        if (attachedSurface != null)
        {
            transform.position = attachedSurface.TransformPoint(embeddedPosition);
            transform.rotation = attachedSurface.rotation * embeddedRotation;
        }
    }

    // --- State 4: Recalling ---
    void UpdateRecalling()
    {
        Vector3 returnTarget = (lumi != null) ? lumi.transform.position : (playerTransform != null ? playerTransform.position : Vector3.zero);
        transform.position = Vector3.MoveTowards(transform.position, returnTarget, recallSpeed * Time.deltaTime);

        Vector2 dir = (returnTarget - transform.position).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        if (Vector2.Distance(transform.position, returnTarget) < 1.0f)
        {
            CurrentState = SpearState.CarriedByLumi;
            if (trailRenderer != null) trailRenderer.emitting = false;
        }
    }

    // --- Actions ---

    public void Throw(Vector2 direction)
    {
        throwOrigin = transform.position;
        throwDirection = (direction.sqrMagnitude > 0.01f) ? direction.normalized : Vector2.right;
        CurrentState = SpearState.ThrownFlight;
        isAutoDragging = false;

        // Disable platform collider while in flight
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;

        float angle = Mathf.Atan2(throwDirection.y, throwDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }

        SpawnBlueMagicBurst(transform.position, 16, 0.6f);
        Debug.Log("[LumiSpear] Physically thrown towards: " + throwDirection);
    }

    void EmbedInWall(RaycastHit2D hit)
    {
        CurrentState = SpearState.Embedded;

        // Embed horizontally or along normal
        transform.position = hit.point + (throwDirection * 0.3f);
        attachedSurface = hit.collider.transform;
        embeddedPosition = attachedSurface.InverseTransformPoint(transform.position);
        embeddedRotation = Quaternion.Inverse(attachedSurface.rotation) * transform.rotation;

        if (trailRenderer != null) trailRenderer.emitting = false;

        // Enable solid one-way perch platform collider on top of spear
        if (platformCollider != null) platformCollider.enabled = true;
        if (platformEffector != null) platformEffector.enabled = true;

        SpawnBlueMagicBurst(transform.position, 24, 0.9f);
        Debug.Log("[LumiSpear] Embedded in wall! Triggering instant auto-drag...");

        // Instant Auto-Drag player on top of spear
        StartAutoDrag();
    }

    void StartAutoDrag()
    {
        if (playerTransform == null) return;

        isAutoDragging = true;
        dragTimer = 0f;
        // Target is directly standing on top of spear
        dragTargetPosition = transform.position + Vector3.up * 0.85f;

        if (tetherLineRenderer != null)
        {
            tetherLineRenderer.enabled = true;
            tetherLineRenderer.SetPosition(0, playerTransform.position + Vector3.up * 0.5f);
            tetherLineRenderer.SetPosition(1, transform.position);
        }
    }

    void UpdateAutoDrag()
    {
        if (playerTransform == null || playerRb == null)
        {
            isAutoDragging = false;
            if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;
            return;
        }

        dragTimer += Time.deltaTime;

        // Update glowing blue tether line
        if (tetherLineRenderer != null)
        {
            tetherLineRenderer.enabled = true;
            tetherLineRenderer.SetPosition(0, playerTransform.position + Vector3.up * 0.5f);
            tetherLineRenderer.SetPosition(1, transform.position);
        }

        // Swiftly drag player on top of spear
        playerTransform.position = Vector3.Lerp(playerTransform.position, dragTargetPosition, Time.deltaTime * 24f);
        playerRb.linearVelocity = Vector2.zero;

        if (Vector2.Distance(playerTransform.position, dragTargetPosition) < 0.25f || dragTimer > 0.45f)
        {
            // Arrived securely on top of spear platform!
            playerTransform.position = dragTargetPosition;
            playerRb.linearVelocity = Vector2.zero;
            isAutoDragging = false;
            if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;
            SpawnBlueMagicBurst(playerTransform.position, 12, 0.4f);
            Debug.Log("[LumiSpear] Player successfully perched on top of spear!");
        }
    }

    /// <summary>
    /// Explodes the embedded spear in a blue shockwave and blasts the player into a 22 unit/s Vertical Super Jump!
    /// </summary>
    public void ExplodeAndSuperLaunch()
    {
        if (CurrentState != SpearState.Embedded) return;

        Vector3 explosionCenter = transform.position;

        // 1. Damage all nearby enemies and breakables
        Collider2D[] colliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius);
        foreach (var col in colliders)
        {
            if (col.CompareTag("Player") || col.isTrigger) continue;

            Health h = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();
            if (h != null)
            {
                h.TakeDamage(explosionDamage);
            }

            BreakableObject b = col.GetComponent<BreakableObject>() ?? col.GetComponentInParent<BreakableObject>();
            if (b != null)
            {
                b.TakeDamage(100);
            }

            Rigidbody2D rb = col.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 knockDir = ((Vector2)col.transform.position - (Vector2)explosionCenter).normalized;
                rb.AddForce(knockDir * explosionKnockback, ForceMode2D.Impulse);
            }
        }

        // 2. Blast Player in Vertical Super Jump (22 units/s)!
        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x * 0.5f, superLaunchVelocity);

            // Trigger player jump animation
            if (playerTransform != null)
            {
                Animator pAnim = playerTransform.GetComponentInChildren<Animator>();
                if (pAnim != null)
                {
                    try
                    {
                        pAnim.SetBool("isJumping", true);
                        pAnim.SetBool("isFalling", false);
                        pAnim.SetBool("isWalking", false);
                        pAnim.SetBool("isRunning", false);
                        pAnim.Play("jump", 0, 0f);
                    }
                    catch (System.Exception) { }
                }
            }
        }

        // 3. Shockwave FX & Screen Juice
        SpawnBlueMagicBurst(explosionCenter, 42, 1.8f);
        if (PlayerCombatJuice.Instance != null)
        {
            PlayerCombatJuice.Instance.ApplyAttackLunge(0, true, false);
        }

        // 4. Disable platform collider and recall spear
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;
        isAutoDragging = false;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;

        Recall();
        Debug.Log("[LumiSpear] Exploded spear and launched player at 22 units/s!");
    }

    public void Recall()
    {
        isAutoDragging = false;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;
        CurrentState = SpearState.Recalling;
    }

    void DamageTarget(GameObject target, int damage)
    {
        Health h = target.GetComponent<Health>() ?? target.GetComponentInParent<Health>();
        if (h != null)
        {
            h.TakeDamage(damage);
            SpawnBlueMagicBurst(target.transform.position, 14, 0.5f);
        }

        BreakableObject b = target.GetComponent<BreakableObject>() ?? target.GetComponentInParent<BreakableObject>();
        if (b != null)
        {
            b.TakeDamage(100);
        }
    }

    void SpawnBlueMagicBurst(Vector3 position, int particleCount, float scale)
    {
        GameObject burstGO = new GameObject("Spear_MagicBurst");
        burstGO.transform.position = position;

        ParticleSystem ps = burstGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = magicAuraColor;
        main.startSize = 0.28f * scale;
        main.startSpeed = 7f * scale;
        main.startLifetime = 0.5f;
        main.duration = 0.5f;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, particleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f * scale;

        ps.Play();
        Destroy(burstGO, 0.8f);
    }

    [Header("Melee Combo Finisher")]
    public int meleeThrustDamage = 75;
    public float meleeThrustDistance = 6.5f;
    public float meleeThrustDuration = 0.12f;
    public float meleeThrustReturnDuration = 0.15f;
    private Coroutine meleeThrustCoroutine;

    /// <summary>
    /// Executes the high-impact Sonic Piercing Thrust combo finisher when player double-taps J.
    /// Auto-recalls spear if currently away, aligns horizontally, pierces forward dealing 75 damage
    /// with pure white impact flash and screen shake, and returns smoothly to Lumi.
    /// </summary>
    public void ExecuteMeleeSpearThrust(float facingDirection)
    {
        FindReferences();
        if (meleeThrustCoroutine != null) StopCoroutine(meleeThrustCoroutine);
        meleeThrustCoroutine = StartCoroutine(MeleeSpearThrustRoutine(facingDirection));
    }

    private IEnumerator MeleeSpearThrustRoutine(float facingDirection)
    {
        // 1. If currently embedded or dragging, cancel attachment immediately
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;
        attachedSurface = null;
        isAutoDragging = false;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;

        CurrentState = SpearState.MeleeThrusting;

        // 2. Snap to launch position slightly in front of Lumi/Player
        Vector3 startPos;
        if (lumi != null)
        {
            startPos = lumi.transform.position + new Vector3(facingDirection * 0.4f, 0f, 0f);
        }
        else if (playerTransform != null)
        {
            startPos = playerTransform.position + new Vector3(facingDirection * 0.8f, 0.2f, 0f);
        }
        else
        {
            startPos = transform.position;
        }
        startPos.z = 0f;
        transform.position = startPos;

        // Align horizontally
        float targetAngle = (facingDirection < 0f) ? 180f : 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);

        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
            trailRenderer.Clear();
        }

        SpawnSonicShockwave(startPos, facingDirection);

        // 3. High-Velocity Piercing Thrust Phase
        Vector3 targetPos = startPos + new Vector3(facingDirection * meleeThrustDistance, 0f, 0f);
        float elapsed = 0f;
        var hitTargets = new System.Collections.Generic.HashSet<GameObject>();

        while (elapsed < meleeThrustDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / meleeThrustDuration);
            // Snap forward with high acceleration curve
            float curveT = Mathf.Sin(t * Mathf.PI * 0.5f);
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, curveT);
            transform.position = currentPos;

            // Detect and pierce enemies along the thrust path
            Collider2D[] hits = Physics2D.OverlapCircleAll(currentPos, 1.35f);
            foreach (var col in hits)
            {
                if (col == null || col.isTrigger || col.CompareTag("Player")) continue;
                GameObject rootTarget = col.transform.root.gameObject;
                if (hitTargets.Contains(rootTarget) || hitTargets.Contains(col.gameObject)) continue;

                var damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                var health = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();

                if (damageable != null || health != null || col.CompareTag("enemy"))
                {
                    hitTargets.Add(rootTarget);
                    hitTargets.Add(col.gameObject);

                    // Deal 75 Heavy Damage!
                    if (damageable != null) damageable.TakeDamage(meleeThrustDamage);
                    else if (health != null) health.TakeDamage(meleeThrustDamage);

                    // Trigger Pure White Impact Flash!
                    SpawnOfChaos.Systems.ImpactFrameFX.Trigger(col.bounds.center, 0.07f, isNegativeInversion: false);

                    // Directional Screen Shake & Hit Feedback
                    HitFeedbackManager.TriggerHitFeedback(col.transform, col.bounds.center, meleeThrustDamage, true, EnemyHitType.PhysicalMelee);

                    // Directional Knockback
                    Rigidbody2D enemyRb = col.GetComponent<Rigidbody2D>() ?? col.GetComponentInParent<Rigidbody2D>();
                    if (enemyRb != null && enemyRb.bodyType == RigidbodyType2D.Dynamic)
                    {
                        enemyRb.linearVelocity = new Vector2(facingDirection * 15f, 4.0f);
                    }

                    // Impact Magic Burst VFX
                    SpawnBlueMagicBurst(col.bounds.center, 22, 1.2f);
                }
            }

            yield return null;
        }

        // 4. Brief Apex Linger
        yield return new WaitForSeconds(0.04f);

        // 5. Smooth Retraction back to Lumi
        elapsed = 0f;
        Vector3 apexPos = transform.position;
        while (elapsed < meleeThrustReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / meleeThrustReturnDuration);
            Vector3 returnTarget = (lumi != null) ? lumi.transform.position : (playerTransform != null ? playerTransform.position : startPos);
            transform.position = Vector3.Lerp(apexPos, returnTarget, t * t);
            yield return null;
        }

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }

        CurrentState = SpearState.CarriedByLumi;
        meleeThrustCoroutine = null;
    }

    private void SpawnSonicShockwave(Vector3 origin, float facingDirection)
    {
        GameObject waveGO = new GameObject("Spear_SonicShockwave");
        waveGO.transform.position = origin;
        waveGO.transform.rotation = Quaternion.Euler(0f, 0f, (facingDirection < 0f) ? 180f : 0f);

        ParticleSystem ps = waveGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.95f, 1.0f, 0.95f), Color.white);
        main.startSize = 0.45f;
        main.startSpeed = 16f;
        main.startLifetime = 0.22f;
        main.duration = 0.22f;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 26) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.15f;

        ps.Play();
        Destroy(waveGO, 0.5f);
    }

    /// <summary>
    /// Invoked by on-screen touch spear button. Throws spear, recalls, or super-launches.
    /// </summary>
    public void TriggerSpearActionFromTouch()
    {
        if (CurrentState == SpearState.CarriedByLumi)
        {
            float facing = (playerTransform != null && playerTransform.localScale.x < 0) ? -1f : 1f;
            Throw(new Vector2(facing, 0f));
        }
        else if (CurrentState == SpearState.Embedded)
        {
            ExplodeAndSuperLaunch();
        }
        else if (CurrentState == SpearState.ThrownFlight)
        {
            Recall();
        }
    }

    private Vector2 GetAimDirection()
    {
        float defaultFacing = (playerTransform != null && playerTransform.localScale.x < 0f) ? -1f : 1f;
        Vector2 defaultDir = new Vector2(defaultFacing, 0f);

        if (Application.isMobilePlatform)
        {
            return defaultDir;
        }

        Vector3 mPos = Input.mousePosition;
        if (float.IsInfinity(mPos.x) || float.IsInfinity(mPos.y) || float.IsNaN(mPos.x) || float.IsNaN(mPos.y))
        {
            return defaultDir;
        }

        if (mPos.x < 0f || mPos.x > Screen.width || mPos.y < 0f || mPos.y > Screen.height)
        {
            return defaultDir;
        }

        Camera cam = Camera.main;
        if (cam == null) return defaultDir;

        try
        {
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mPos.x, mPos.y, -cam.transform.position.z));
            if (float.IsNaN(worldPos.x) || float.IsInfinity(worldPos.x))
            {
                return defaultDir;
            }
            worldPos.z = 0f;
            Vector2 delta = (Vector2)worldPos - (Vector2)transform.position;
            if (delta.sqrMagnitude > 0.01f)
            {
                return delta.normalized;
            }
        }
        catch (System.Exception)
        {
            return defaultDir;
        }

        return defaultDir;
    }
}
