using SpawnOfChaos.Weapons;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpearState
{
    CarriedByLumi,
    ThrownFlight,
    Embedded,
    Recalling,
    MeleeThrusting,
    MeleeSlashing
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

    [Header("Void Shadow Colors")]
    public Color magicAuraColor = new Color(0.04f, 0.02f, 0.08f, 0.95f);
    public Color magicTrailColor = new Color(0.02f, 0.01f, 0.04f, 0.95f);
    public Color moteColor = new Color(0.02f, 0.01f, 0.04f, 0.85f);

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

        public bool IsSpearDisabledInCurrentScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return string.Equals(sceneName, "TutorialScene", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sceneName, "Tutorial", System.StringComparison.OrdinalIgnoreCase);
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

        if ((magicAuraColor.b > 0.4f && magicAuraColor.g > 0.4f) || (magicTrailColor.b > 0.4f && magicTrailColor.g > 0.4f))
        {
            magicAuraColor = new Color(0.04f, 0.02f, 0.08f, 0.95f);
            magicTrailColor = new Color(0.02f, 0.01f, 0.04f, 0.95f);
            moteColor = new Color(0.02f, 0.01f, 0.04f, 0.85f);
        }

        SetupComponents();
    }

    [Header("Weapon Arsenal Integration")]
    private WeaponID currentWeaponID = WeaponID.DarkSpear;
    private readonly List<GameObject> orbitingDaggerDuplicates = new List<GameObject>();
    private float orbitAngle = 0f;
    private bool isAutonomousSlashing = false;

    void Start()
    {
        FindReferences();
        WeaponManager.EnsureExists();
        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.OnWeaponEquipped += HandleWeaponEquipped;
            // Force equip DarkSpear (the base spear)
            WeaponManager.Instance.EquipWeapon(WeaponID.DarkSpear);
            ApplyWeaponConfiguration(WeaponManager.Instance.GetWeaponInfo(WeaponID.DarkSpear));
        }
    }

    void OnDestroy()
    {
        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.OnWeaponEquipped -= HandleWeaponEquipped;
        }
        ClearOrbitingDuplicates();
    }

    private void HandleWeaponEquipped(WeaponID newWeapon)
    {
        if (WeaponManager.Instance != null)
        {
            ApplyWeaponConfiguration(WeaponManager.Instance.GetWeaponInfo(newWeapon));
        }
    }

    public void ApplyWeaponConfiguration(WeaponInfo info)
    {
        if (info == null) return;
        currentWeaponID = info.id;

        int tier = WeaponManager.Instance != null ? WeaponManager.Instance.GetTier(info.id) : 1;
        pierceDamage = info.GetDamageForTier(tier);
        explosionDamage = info.GetExplosionDamageForTier(tier);
        throwSpeed = info.throwSpeed;
        recallSpeed = info.recallSpeed;

        magicAuraColor = info.auraColor;
        magicTrailColor = info.trailColor;

        Sprite s = info.GetSprite();
        if (spearRenderer != null && s != null)
        {
            spearRenderer.sprite = s;
        }

        if (trailRenderer != null)
        {
            trailRenderer.startColor = magicTrailColor;
            trailRenderer.endColor = new Color(magicTrailColor.r, magicTrailColor.g, magicTrailColor.b, 0f);
        }

        // Setup Duplication Orbiters if DarkDag
        if (currentWeaponID == WeaponID.DarkDag)
        {
            SetupOrbitingDuplicates(s, tier >= 3 ? 4 : 2);
        }
        else
        {
            ClearOrbitingDuplicates();
        }

        Debug.Log($"[LumiSpearWeapon] Applied Weapon '{info.displayName}' (Tier {tier}, Dmg {pierceDamage})");
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

        // 3. Trail Renderer (Pitch-Black Void Shadow Trail)
        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null) trailRenderer = gameObject.AddComponent<TrailRenderer>();
        trailRenderer.time = 0.32f;
        trailRenderer.startWidth = 0.55f;
        trailRenderer.endWidth = 0.02f;
        trailRenderer.material = PlayerShadowDashTrail.GetShadowMaterial() ?? CreateUnlitMaterial(magicTrailColor);
        trailRenderer.startColor = magicTrailColor;
        trailRenderer.endColor = new Color(magicTrailColor.r, magicTrailColor.g, magicTrailColor.b, 0f);
        Gradient trailGrad = new Gradient();
        trailGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.04f, 0.02f, 0.08f), 0f),
                new GradientColorKey(new Color(0.02f, 0.01f, 0.04f), 0.6f),
                new GradientColorKey(new Color(0.01f, 0.01f, 0.02f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.90f, 0f),
                new GradientAlphaKey(0.50f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trailRenderer.colorGradient = trailGrad;
        trailRenderer.emitting = false;

        // 4. Line Renderer for Arcane Drag Tether (Void Shadow Line)
        tetherLineRenderer = GetComponent<LineRenderer>();
        if (tetherLineRenderer == null) tetherLineRenderer = gameObject.AddComponent<LineRenderer>();
        tetherLineRenderer.startWidth = 0.22f;
        tetherLineRenderer.endWidth = 0.12f;
        tetherLineRenderer.material = PlayerShadowDashTrail.GetShadowMaterial() ?? CreateUnlitMaterial(magicAuraColor);
        tetherLineRenderer.startColor = new Color(0.04f, 0.02f, 0.08f, 0.95f);
        tetherLineRenderer.endColor = new Color(0.01f, 0.01f, 0.03f, 0.85f);
        tetherLineRenderer.positionCount = 2;
        tetherLineRenderer.enabled = false;

        // 5. Floating/Fading Dark Void Motes Particle System
        SetupMoteParticles();
    }

    public static bool IsEnemyTarget(Collider2D col)
    {
        if (col == null) return false;
        try
        {
            if (col.CompareTag("enemy")) return true;
        }
        catch {}
        if (string.Equals(col.tag, "enemy", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (col.GetComponent<UniversalEnemy>() != null || col.GetComponentInParent<UniversalEnemy>() != null) return true;
        return false;
    }

    void LoadSpearSprite()
    {
        if (spearRenderer.sprite != null) return;
        Sprite spearSprite = Resources.Load<Sprite>("Weapons/DarkSpear") ??
                             Resources.Load<Sprite>("DarkSpear") ??
                             Resources.Load<Sprite>("LumiSpear") ??
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

        ParticleSystemRenderer psRenderer = moteParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            LowResBlackOrb.ConfigureParticleRenderer(psRenderer, 19, spearRenderer != null ? spearRenderer.sortingLayerName : "Default");
        }

        var main = moteParticleSystem.main;
        // Pitch-black void particles with retro low-res orb texture
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.01f, 0.01f, 0.02f, 0.95f), new Color(0.04f, 0.04f, 0.06f, 0.85f));
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = moteParticleSystem.emission;
        emission.rateOverTime = 14f;

        var shape = moteParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.0f, 0.2f, 0.1f);

        var colOverLifetime = moteParticleSystem.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 0f), new GradientColorKey(new Color(0.01f, 0.01f, 0.01f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.90f, 0.25f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        var sol = moteParticleSystem.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

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

        // Completely hide and disable spear in Tutorial Scene
        if (IsSpearDisabledInCurrentScene())
        {
            if (spearRenderer != null && spearRenderer.enabled) spearRenderer.enabled = false;
            if (platformCollider != null && platformCollider.enabled) platformCollider.enabled = false;
            if (platformEffector != null && platformEffector.enabled) platformEffector.enabled = false;
            if (tetherLineRenderer != null && tetherLineRenderer.enabled) tetherLineRenderer.enabled = false;
            if (moteParticleSystem != null && moteParticleSystem.isPlaying) moteParticleSystem.Stop();
            return;
        }
        else
        {
            if (spearRenderer != null && !spearRenderer.enabled) spearRenderer.enabled = true;
        }

        if (spearRenderer != null && spearRenderer.sprite == null)
        {
            LoadSpearSprite();
        }

        HandleInput();

        UpdateOrbitingDuplicates();

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
        if (move.Instance != null && move.Instance.IsBlobForm) return;

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

        // 'X' Key or Spear Action:
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (CurrentState == SpearState.Embedded)
            {
                // DarkSpear Ground Anchor Emergency Dodge Teleport:
                // If the player is NOT standing on the spear, emergency-teleport directly back to it!
                bool isStandingOnSpear = (playerTransform != null && Vector2.Distance(playerTransform.position, transform.position) < 1.4f);
                if (currentWeaponID == WeaponID.DarkSpear && !isStandingOnSpear)
                {
                    ExecuteGroundAnchorTeleport();
                }
                else
                {
                    ExplodeAndSuperLaunch();
                }
            }
        }
    }

    // --- State 1: Independent Floating beside Lumi ---
    void UpdateIndependentFloating()
    {
        // Float comfortably in front of the player with dedicated space
        Vector3 basePos = (playerTransform != null) ? playerTransform.position : ((lumi != null) ? lumi.transform.position : Vector3.zero);
        float facing = (playerTransform != null && playerTransform.localScale.x < 0) ? -1f : 1f;

        // Independent hovering physics with sinusoidal wave
        float bobY = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        float bobX = Mathf.Cos(Time.time * (floatFrequency * 0.6f)) * (floatAmplitude * 0.5f);

        Vector3 targetPos = basePos + new Vector3((1.8f + bobX) * facing, 0.6f + bobY, 0f);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 12f);

        // Smoothly orient towards aim direction (safe from inf/NaN on touch/simulator)
        Vector2 aimDir = GetAimDirection();
        float targetAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        float tipOffset = SpearOrientationAnalyzer.GetTipAngleOffset(spearRenderer != null ? spearRenderer.sprite : null);

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, targetAngle - tipOffset), Time.deltaTime * 10f);

        if (spearRenderer != null && playerTransform != null)
        {
            SpriteRenderer pSr = playerTransform.GetComponentInChildren<SpriteRenderer>();
            if (pSr != null)
            {
                spearRenderer.sortingLayerID = pSr.sortingLayerID;
                spearRenderer.sortingOrder = pSr.sortingOrder + 2;
            }
        }

        // Harmonize trail with player running and dashing
        if (trailRenderer != null)
        {
            bool shouldSpearTrail = (playerMove != null && (playerMove.IsRunning || playerMove.IsDashing));
            if (trailRenderer.emitting != shouldSpearTrail)
            {
                trailRenderer.emitting = shouldSpearTrail;
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
            float tipOffset = SpearOrientationAnalyzer.GetTipAngleOffset(spearRenderer != null ? spearRenderer.sprite : null);
            transform.rotation = Quaternion.Euler(0, 0, angle - tipOffset);
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
        if (move.Instance != null && move.Instance.IsBlobForm) return;
        throwOrigin = transform.position;
        throwDirection = (direction.sqrMagnitude > 0.01f) ? direction.normalized : Vector2.right;
        CurrentState = SpearState.ThrownFlight;
        isAutoDragging = false;

        // Disable platform collider while in flight
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;

        float angle = Mathf.Atan2(throwDirection.y, throwDirection.x) * Mathf.Rad2Deg;
        float tipOffset = SpearOrientationAnalyzer.GetTipAngleOffset(spearRenderer != null ? spearRenderer.sprite : null);
        transform.rotation = Quaternion.Euler(0, 0, angle - tipOffset);

        // Jump voice used for spear travel
        move.Instance?.PlayRandomJumpVoice();

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
        if (currentWeaponID == WeaponID.DarkAxe) TriggerSeismicShockwave(transform.position);

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
        // Jump voice used for spear travel auto-drag
        move.Instance?.PlayRandomJumpVoice();

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
        if (move.Instance != null && move.Instance.IsBlobForm) return;
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

        // 2. Blast Player in Vertical Super Jump (22 units/s) ONLY IF MOUNTED on the spear
        bool isMounted = (playerTransform != null && Vector2.Distance(playerTransform.position, transform.position + Vector3.up * 0.3f) < 1.6f);
        if (isMounted && playerRb != null)
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
        if (move.Instance != null && move.Instance.IsBlobForm) return;
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
        SpawnVoidMagicBurst(position, particleCount, scale);
    }

    public void SpawnVoidMagicBurst(Vector3 position, int particleCount, float scale)
    {
        GameObject burstGO = new GameObject("Spear_MagicBurst");
        burstGO.transform.position = position;

        ParticleSystem ps = burstGO.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = burstGO.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            LowResBlackOrb.ConfigureParticleRenderer(psRenderer, 22, spearRenderer != null ? spearRenderer.sortingLayerName : "Default");
        }

        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.01f, 0.01f, 0.02f, 0.95f), new Color(0.04f, 0.02f, 0.07f, 0.85f));
        main.startSize = 0.28f * scale;
        main.startSpeed = 7f * scale;
        main.startLifetime = 0.5f;
        // Removed main.duration assignment to prevent Unity runtime error
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
    [Header("Dynamic Afterimage Ghost Trail Settings")]
    private Coroutine ghostTrailCoroutine;

    private void StartGhostTrail(float duration, Color ghostColor)
    {
        if (ghostTrailCoroutine != null) StopCoroutine(ghostTrailCoroutine);
        ghostTrailCoroutine = StartCoroutine(GhostTrailRoutine(duration, ghostColor));
    }

    private void StopGhostTrail()
    {
        if (ghostTrailCoroutine != null)
        {
            StopCoroutine(ghostTrailCoroutine);
            ghostTrailCoroutine = null;
        }
    }

    private IEnumerator GhostTrailRoutine(float duration, Color ghostColor)
    {
        float elapsed = 0f;
        float interval = 0.012f; // Silky 83Hz high-density sampling for majestic continuous afterimage ribbon
        float nextSpawn = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextSpawn && spearRenderer != null && spearRenderer.sprite != null)
            {
                nextSpawn = elapsed + interval;
                SpawnGhostEcho(ghostColor);
                SpawnTipGleam(ghostColor);
            }
            yield return null;
        }

        ghostTrailCoroutine = null;
    }

    private void SpawnGhostEcho(Color tint)
    {
        if (spearRenderer == null || spearRenderer.sprite == null) return;

        // 1. Luminous Outer Motion-Blur Bloom Echo (Behind)
        GameObject blurEcho = new GameObject("SpearBlurEcho");
        blurEcho.transform.position = transform.position;
        blurEcho.transform.rotation = transform.rotation;
        blurEcho.transform.localScale = transform.localScale * 1.25f;

        SpriteRenderer blurSr = blurEcho.AddComponent<SpriteRenderer>();
        blurSr.sprite = spearRenderer.sprite;
        blurSr.material = CreateUnlitMaterial(tint);
        blurSr.sortingLayerID = spearRenderer.sortingLayerID;
        blurSr.sortingOrder = Mathf.Max(0, spearRenderer.sortingOrder - 2);
        StartCoroutine(AnimateGhostEcho(blurEcho, blurSr, tint, 0.26f, 0.50f, 1.40f));

        // 2. Chromatic Prism Fringe Echo (Delicate prismatic color shift for stunning graphical depth)
        Color chromaTint = new Color(tint.b, tint.r, tint.g, tint.a * 0.6f);
        GameObject chromaEcho = new GameObject("SpearChromaEcho");
        chromaEcho.transform.position = transform.position + (transform.up * 0.04f);
        chromaEcho.transform.rotation = transform.rotation;
        chromaEcho.transform.localScale = transform.localScale * 1.08f;

        SpriteRenderer chromaSr = chromaEcho.AddComponent<SpriteRenderer>();
        chromaSr.sprite = spearRenderer.sprite;
        chromaSr.material = CreateUnlitMaterial(chromaTint);
        chromaSr.sortingLayerID = spearRenderer.sortingLayerID;
        chromaSr.sortingOrder = Mathf.Max(0, spearRenderer.sortingOrder - 1);
        StartCoroutine(AnimateGhostEcho(chromaEcho, chromaSr, chromaTint, 0.19f, 0.45f, 1.18f));

        // 3. Crisp Duplicate Spear Silhouette Echo
        GameObject echo = new GameObject("SpearGhostEcho");
        echo.transform.position = transform.position;
        echo.transform.rotation = transform.rotation;
        echo.transform.localScale = transform.localScale;

        SpriteRenderer sr = echo.AddComponent<SpriteRenderer>();
        sr.sprite = spearRenderer.sprite;
        sr.material = CreateUnlitMaterial(tint);
        sr.sortingLayerID = spearRenderer.sortingLayerID;
        sr.sortingOrder = spearRenderer.sortingOrder;
        StartCoroutine(AnimateGhostEcho(echo, sr, tint, 0.22f, 0.85f, 1.10f));
    }

    private void SpawnTipGleam(Color gleamColor)
    {
        if (spearRenderer == null || spearRenderer.sprite == null) return;

        Vector3 tipPos = SpearOrientationAnalyzer.GetTipWorldPosition(transform, spearRenderer);
        GameObject gleam = new GameObject("SpearTipGleam");
        gleam.transform.position = tipPos;

        SpriteRenderer gsr = gleam.AddComponent<SpriteRenderer>();
        Texture2D gleamTex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                float a = Mathf.Clamp01(1f - (dist / 7.5f));
                gleamTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        gleamTex.Apply();
        gsr.sprite = Sprite.Create(gleamTex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 32f);
        gsr.color = Color.Lerp(gleamColor, Color.white, 0.6f);
        gsr.sortingLayerID = spearRenderer.sortingLayerID;
        gsr.sortingOrder = spearRenderer.sortingOrder + 1;

        StartCoroutine(AnimateTipGleam(gleam, gsr));
    }

    private IEnumerator AnimateTipGleam(GameObject gleam, SpriteRenderer gsr)
    {
        float dur = 0.16f;
        float elapsed = 0f;
        Vector3 origScale = gleam.transform.localScale * 0.7f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (gleam != null && gsr != null)
            {
                gleam.transform.localScale = origScale * Mathf.Lerp(1.2f, 0.1f, t);
                Color c = gsr.color;
                c.a = Mathf.Lerp(1f, 0f, t * t);
                gsr.color = c;
            }
            yield return null;
        }
        if (gleam != null) Destroy(gleam);
    }

    private IEnumerator AnimateGhostEcho(GameObject echo, SpriteRenderer sr, Color tint, float lifetime, float initialAlpha, float scaleMultiplier)
    {
        float elapsed = 0f;
        Vector3 initialScale = echo.transform.localScale;
        Vector3 targetScale = initialScale * scaleMultiplier;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            if (echo != null && sr != null)
            {
                // Quadratic falloff: progressively softer & less visible the longer the trail is out
                float alpha = Mathf.Lerp(initialAlpha, 0f, t * t);
                Color c = tint;
                c.a = alpha;
                sr.color = c;

                // Dynamic subtle motion blur expansion
                echo.transform.localScale = Vector3.Lerp(initialScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            }
            yield return null;
        }

        if (echo != null) Destroy(echo);
    }

    // ══════════════════════════════════════════════════════════════
    // PURE BLACK SPEAR SILHOUETTE MULTIPLYING TRAIL
    // ══════════════════════════════════════════════════════════════
    private static Material blackSilhouetteMat;

    private Material GetBlackSilhouetteMaterial()
    {
        if (blackSilhouetteMat == null)
        {
            Shader s = Shader.Find("Sprites/Default") 
                    ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit") 
                    ?? Shader.Find("Unlit/Color");
            if (s != null)
            {
                blackSilhouetteMat = new Material(s) { hideFlags = HideFlags.DontSave };
                blackSilhouetteMat.color = Color.white;
            }
        }
        return blackSilhouetteMat;
    }

    private Transform darkTrailContainer;
    private Transform GetDarkTrailContainer()
    {
        if (darkTrailContainer == null)
        {
            GameObject go = new GameObject("Spear_DarkTrail_Container");
            darkTrailContainer = go.transform;
        }
        return darkTrailContainer;
    }

    private void EmitBlackSpearMotionTrail(ref Vector3 lastPos, ref Quaternion lastRot, ref bool hasLast, int step)
    {
        if (spearRenderer == null || spearRenderer.sprite == null) return;

        Vector3 curPos = transform.position;
        Quaternion curRot = transform.rotation;

        if (!hasLast)
        {
            hasLast = true;
            lastPos = curPos;
            lastRot = curRot;
            SpawnSingleBlackSpearEcho(curPos, curRot, step);
            return;
        }

        float dist = Vector3.Distance(lastPos, curPos);
        float angleDiff = Quaternion.Angle(lastRot, curRot);

        // Sub-sample density: spawn an echo every 0.055 units or 2.8 degrees (seamless dense overlap)
        int steps = Mathf.Clamp(Mathf.Max(Mathf.CeilToInt(dist / 0.055f), Mathf.CeilToInt(angleDiff / 2.8f)), 1, 10);

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 interpPos = Vector3.Lerp(lastPos, curPos, t);
            Quaternion interpRot = Quaternion.Slerp(lastRot, curRot, t);
            SpawnSingleBlackSpearEcho(interpPos, interpRot, step);

            // Scatter low-resolution black orbs along the spear's dynamic path
            if (i % 3 == 0)
            {
                Transform container = GetDarkTrailContainer();
                int order = spearRenderer != null ? spearRenderer.sortingOrder - 1 : 20;
                LowResBlackOrb.SpawnOrb(interpPos + (Vector3)(Random.insideUnitCircle * 0.14f), Random.Range(0.24f, 0.38f), 0.20f, container, order);
            }
        }

        lastPos = curPos;
        lastRot = curRot;
    }

    private void SpawnSingleBlackSpearEcho(Vector3 pos, Quaternion rot, int step)
    {
        if (spearRenderer == null || spearRenderer.sprite == null) return;
        Transform container = GetDarkTrailContainer();

        // 1. Core Pitch-Black Silhouette Clone
        GameObject echo = new GameObject("SpearDarkTrail");
        if (container != null) echo.transform.SetParent(container, false);
        echo.transform.position = pos;
        echo.transform.rotation = rot;
        echo.transform.localScale = transform.localScale;

        SpriteRenderer sr = echo.AddComponent<SpriteRenderer>();
        sr.sprite = spearRenderer.sprite;
        sr.flipX = spearRenderer.flipX;
        sr.flipY = spearRenderer.flipY;
        sr.material = GetBlackSilhouetteMaterial();
        sr.sortingLayerID = spearRenderer.sortingLayerID;
        sr.sortingLayerName = spearRenderer.sortingLayerName;
        sr.sortingOrder = Mathf.Max(0, spearRenderer.sortingOrder - 1);
        Color coreCol = (step == 2)
            ? new Color(0.04f, 0.005f, 0.015f, 0.88f) // Deep crimson void
            : (step == 3 ? new Color(0.01f, 0.01f, 0.02f, 0.95f) : new Color(0.015f, 0.008f, 0.035f, 0.88f)); // Pure abyssal shadow
        StartCoroutine(AnimateBlackSpearEcho(echo, sr, coreCol, 0.13f, 0.88f, 1.08f));

        // 2. Soft Outer Void Blur Mote (blends discrete spear edges into a continuous ribbon wake)
        GameObject blurEcho = new GameObject("SpearDarkBlur");
        if (container != null) blurEcho.transform.SetParent(container, false);
        blurEcho.transform.position = pos;
        blurEcho.transform.rotation = rot;
        blurEcho.transform.localScale = transform.localScale * 1.06f;

        SpriteRenderer blurSr = blurEcho.AddComponent<SpriteRenderer>();
        blurSr.sprite = spearRenderer.sprite;
        blurSr.flipX = spearRenderer.flipX;
        blurSr.flipY = spearRenderer.flipY;
        blurSr.material = GetBlackSilhouetteMaterial();
        blurSr.sortingLayerID = spearRenderer.sortingLayerID;
        blurSr.sortingLayerName = spearRenderer.sortingLayerName;
        blurSr.sortingOrder = Mathf.Max(0, spearRenderer.sortingOrder - 2);
        Color blurCol = (step == 2)
            ? new Color(0.03f, 0.01f, 0.02f, 0.35f)
            : (step == 3 ? new Color(0.015f, 0.015f, 0.03f, 0.40f) : new Color(0.02f, 0.02f, 0.03f, 0.35f));
        StartCoroutine(AnimateBlackSpearEcho(blurEcho, blurSr, blurCol, 0.15f, 0.35f, 1.20f));
    }

    private IEnumerator AnimateBlackSpearEcho(GameObject echo, SpriteRenderer sr, Color baseColor, float lifetime, float initialAlpha, float scaleMult)
    {
        float elapsed = 0f;
        Vector3 initialScale = (echo != null) ? echo.transform.localScale : Vector3.one;
        Vector3 targetScale = initialScale * scaleMult;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            if (echo != null && sr != null)
            {
                // Rapid cubic fade out: solid at head, dissolves quickly into mist
                float alpha = Mathf.Lerp(initialAlpha, 0f, t * t * t);
                Color c = baseColor;
                c.a = alpha;
                sr.color = c;

                // Gentle expansion that blurs into neighboring clones
                echo.transform.localScale = Vector3.Lerp(initialScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            }
            yield return null;
        }

        if (echo != null) Destroy(echo);
    }

    [Header("Spear Swordsmanship Melee Combos")]
    public int slash1Damage = 40;
    public int slash2Damage = 55;

    public void ExecuteMeleeSpearSlash1(float facingDirection)
    {
        if (move.Instance != null && move.Instance.IsBlobForm) return;
        FindReferences();
        PlaySpearAttackVoice();
        if (meleeThrustCoroutine != null) StopCoroutine(meleeThrustCoroutine);
        StopGhostTrail();
        meleeThrustCoroutine = StartCoroutine(MeleeSpearSlash1Routine(facingDirection));
    }


    private void PlaySpearAttackVoice()
    {
        if (MageCombat.Instance != null)
        {
            MageCombat.Instance.PlayRandomAttackVoice();
        }
        else if (playerTransform != null)
        {
            var vc = playerTransform.GetComponent<SpawnOfChaos.Entities.PlayerMageVoiceController>();
            if (vc != null) vc.PlayAttackVoice();
        }
    }

    /// <summary>
    /// Evaluates hit detection across the ENTIRE length and width of the spear (from pommel/handle to tip).
    /// Ensures that any enemy contacting any part of the blade, shaft, or head is hit cleanly.
    /// </summary>
    private Collider2D[] GetEntireSpearHits(float sampleRadius = 1.05f)
    {
        Vector3 tipPos = SpearOrientationAnalyzer.GetTipWorldPosition(transform, spearRenderer, 2.2f);
        Vector3 toTip = tipPos - transform.position;
        Vector3 buttPos = transform.position - toTip * 0.85f;

        List<Collider2D> uniqueHits = new List<Collider2D>();
        int sampleCount = 6;
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 samplePoint = Vector3.Lerp(buttPos, tipPos, t);
            Collider2D[] overlap = Physics2D.OverlapCircleAll(samplePoint, sampleRadius);
            for (int j = 0; j < overlap.Length; j++)
            {
                Collider2D col = overlap[j];
                if (col != null && !col.isTrigger && !col.CompareTag("Player") && !uniqueHits.Contains(col))
                {
                    uniqueHits.Add(col);
                }
            }
        }

        return uniqueHits.ToArray();
    }

    private IEnumerator MeleeSpearSlash1Routine(float facingDirection)
    {
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;
        attachedSurface = null;
        isAutoDragging = false;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;

        CurrentState = SpearState.MeleeSlashing;

        if (trailRenderer != null)
        {
            // Old line trail disabled during melee combos in favor of stunning Ghost Spear duplicate trail
            trailRenderer.emitting = false;
        }

        float dir = (facingDirection < 0f) ? -1f : 1f;
        Vector3 pPos = playerTransform != null ? playerTransform.position : transform.position;
        Vector3 slashCenter = pPos + new Vector3(dir * 0.85f, 0.15f, 0f);

        float startAngle = 65f;
        float endAngle = -38f;
        float slashDuration = 0.11f;
        float elapsed = 0f;
        SpearSlashVFX slashVfx = SpearSlashVFX.GetOrCreate(playerTransform);
        if (slashVfx != null) slashVfx.BeginComboSlash(1, dir, pPos);

        Vector3 lastEchoPos = transform.position;
        Quaternion lastEchoRot = transform.rotation;
        bool hasLastEcho = false;

        var hitTargets = new System.Collections.Generic.HashSet<GameObject>();

        while (elapsed < slashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slashDuration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);

            pPos = playerTransform != null ? playerTransform.position : transform.position;
            slashCenter = pPos + new Vector3(dir * 0.85f, 0.15f, 0f);

            float currentAngle = Mathf.Lerp(startAngle, endAngle, smoothT);
            float radius = 1.45f;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * radius * dir, Mathf.Sin(rad) * radius, 0f);
            transform.position = slashCenter + offset;

            Vector2 spearDir = ((Vector2)transform.position - (Vector2)slashCenter).normalized;
            float angle = Mathf.Atan2(spearDir.y, spearDir.x) * Mathf.Rad2Deg;
            float tipOffset = SpearOrientationAnalyzer.GetTipAngleOffset(spearRenderer != null ? spearRenderer.sprite : null);
            transform.rotation = Quaternion.Euler(0f, 0f, angle - tipOffset);

            // Emit multiplied black spear silhouette motion trail
            EmitBlackSpearMotionTrail(ref lastEchoPos, ref lastEchoRot, ref hasLastEcho, 1);

            if (slashVfx != null)
            {
                slashVfx.OnArcProgress(1, dir, SpearOrientationAnalyzer.GetTipWorldPosition(transform, spearRenderer, 2.2f), slashCenter, t, angle);
            }

            Collider2D[] hits = GetEntireSpearHits(1.05f);
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

                    if (damageable != null) damageable.TakeDamage(slash1Damage);
                    else if (health != null) health.TakeDamage(slash1Damage);

                    HitFeedbackManager.TriggerHitFeedback(col.transform, col.bounds.center, slash1Damage, false, EnemyHitType.PhysicalMelee);

                    if (PlayerCombatJuice.Instance != null)
                    {
                        PlayerCombatJuice.Instance.SpawnHitCollisionParticles(col.bounds.center, false);
                    }

                    if (SpearSlashVFX.Instance != null)
                    {
                        SpearSlashVFX.Instance.OnSpearHitEnemy(col.bounds.center, 1, dir);
                    }
                }
            }

            yield return null;
        }

        if (trailRenderer != null) trailRenderer.emitting = false;
        CurrentState = SpearState.CarriedByLumi;
        meleeThrustCoroutine = null;
    }

    public void ExecuteMeleeSpearSlash2(float facingDirection)
    {
        if (move.Instance != null && move.Instance.IsBlobForm) return;
        FindReferences();
        PlaySpearAttackVoice();
        if (meleeThrustCoroutine != null) StopCoroutine(meleeThrustCoroutine);
        StopGhostTrail();
        meleeThrustCoroutine = StartCoroutine(MeleeSpearSlash2Routine(facingDirection));
    }

    private IEnumerator MeleeSpearSlash2Routine(float facingDirection)
    {
        if (platformCollider != null) platformCollider.enabled = false;
        if (platformEffector != null) platformEffector.enabled = false;
        attachedSurface = null;
        isAutoDragging = false;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;

        CurrentState = SpearState.MeleeSlashing;

        if (trailRenderer != null)
        {
            // Old line trail disabled during melee combos in favor of stunning Ghost Spear duplicate trail
            trailRenderer.emitting = false;
        }

        float dir = (facingDirection < 0f) ? -1f : 1f;
        Vector3 pPos = playerTransform != null ? playerTransform.position : transform.position;
        Vector3 slashCenter = pPos + new Vector3(dir * 0.95f, 0.25f, 0f);

        float startAngle = -42f;
        float endAngle = 60f;
        float slashDuration = 0.11f;
        float elapsed = 0f;
        SpearSlashVFX slashVfx = SpearSlashVFX.GetOrCreate(playerTransform);
        if (slashVfx != null) slashVfx.BeginComboSlash(2, dir, pPos);

        Vector3 lastEchoPos = transform.position;
        Quaternion lastEchoRot = transform.rotation;
        bool hasLastEcho = false;

        var hitTargets = new System.Collections.Generic.HashSet<GameObject>();

        while (elapsed < slashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slashDuration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);

            pPos = playerTransform != null ? playerTransform.position : transform.position;
            slashCenter = pPos + new Vector3(dir * 0.95f, 0.25f, 0f);

            float currentAngle = Mathf.Lerp(startAngle, endAngle, smoothT);
            float radius = 1.55f;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * radius * dir, Mathf.Sin(rad) * radius, 0f);
            transform.position = slashCenter + offset;

            Vector2 spearDir = ((Vector2)transform.position - (Vector2)slashCenter).normalized;
            float angle = Mathf.Atan2(spearDir.y, spearDir.x) * Mathf.Rad2Deg;
            float tipOffset = SpearOrientationAnalyzer.GetTipAngleOffset(spearRenderer != null ? spearRenderer.sprite : null);
            transform.rotation = Quaternion.Euler(0f, 0f, angle - tipOffset);

            // Emit multiplied black spear silhouette motion trail
            EmitBlackSpearMotionTrail(ref lastEchoPos, ref lastEchoRot, ref hasLastEcho, 2);

            if (slashVfx != null)
            {
                slashVfx.OnArcProgress(2, dir, SpearOrientationAnalyzer.GetTipWorldPosition(transform, spearRenderer, 2.2f), slashCenter, t, angle);
            }

            Collider2D[] hits = GetEntireSpearHits(1.05f);
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

                    if (damageable != null) damageable.TakeDamage(slash2Damage);
                    else if (health != null) health.TakeDamage(slash2Damage);

                    SpawnOfChaos.Systems.ImpactFrameFX.Trigger(col.bounds.center, 0.05f, isNegativeInversion: false);

                    HitFeedbackManager.TriggerHitFeedback(col.transform, col.bounds.center, slash2Damage, true, EnemyHitType.PhysicalMelee);

                    if (PlayerCombatJuice.Instance != null)
                    {
                        PlayerCombatJuice.Instance.SpawnHitCollisionParticles(col.bounds.center, true);
                    }

                    if (SpearSlashVFX.Instance != null)
                    {
                        SpearSlashVFX.Instance.OnSpearHitEnemy(col.bounds.center, 2, dir);
                    }
                }
            }

            yield return null;
        }

        if (trailRenderer != null) trailRenderer.emitting = false;
        CurrentState = SpearState.CarriedByLumi;
        meleeThrustCoroutine = null;
    }

    public void ExecuteMeleeSpearThrust(float facingDirection)
    {
        if (move.Instance != null && move.Instance.IsBlobForm) return;
        FindReferences();
        PlaySpearAttackVoice();
        if (meleeThrustCoroutine != null) StopCoroutine(meleeThrustCoroutine);
        StopGhostTrail();
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

        float dir = (facingDirection < 0f) ? -1f : 1f;
        Vector3 pPos = playerTransform != null ? playerTransform.position : transform.position;
        Vector3 startPos = pPos + new Vector3(dir * 0.75f, 0.15f, 0f);
        startPos.z = 0f;
        transform.position = startPos;
        SpearSlashVFX slashVfx = SpearSlashVFX.GetOrCreate(playerTransform);
        if (slashVfx != null) slashVfx.BeginComboSlash(3, dir, startPos);

        // Align horizontally in the direction of the thrust with tip offset
        float targetAngle = (dir < 0f) ? 180f : 0f;
        float tipOffset = SpearOrientationAnalyzer.GetTipAngleOffset(spearRenderer != null ? spearRenderer.sprite : null);
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle - tipOffset);
        if (slashVfx != null) slashVfx.SpawnThrustFinisher(startPos, dir, meleeThrustDistance);

        if (trailRenderer != null)
        {
            // Old line trail disabled during melee combos in favor of stunning Ghost Spear duplicate trail
            trailRenderer.emitting = false;
        }

        SpawnSonicShockwave(startPos, dir);

        // 3. High-Velocity Piercing Thrust Phase
        Vector3 targetPos = startPos + new Vector3(dir * meleeThrustDistance, 0f, 0f);
        float thrustElapsed = 0f;
        var hitTargets = new System.Collections.Generic.HashSet<GameObject>();

        Vector3 lastEchoPos = startPos;
        Quaternion lastEchoRot = transform.rotation;
        bool hasLastEcho = false;

        while (thrustElapsed < meleeThrustDuration)
        {
            thrustElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(thrustElapsed / meleeThrustDuration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
            transform.position = Vector3.Lerp(startPos, targetPos, smoothT);

            // Emit multiplied black spear silhouette motion trail
            EmitBlackSpearMotionTrail(ref lastEchoPos, ref lastEchoRot, ref hasLastEcho, 3);

            if (slashVfx != null)
            {
                slashVfx.OnArcProgress(3, dir, SpearOrientationAnalyzer.GetTipWorldPosition(transform, spearRenderer, 2.2f), startPos, t, targetAngle);
            }

            Collider2D[] hits = GetEntireSpearHits(1.15f);
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

                    if (damageable != null) damageable.TakeDamage(meleeThrustDamage);
                    else if (health != null) health.TakeDamage(meleeThrustDamage);

                    SpawnOfChaos.Systems.ImpactFrameFX.Trigger(col.bounds.center, 0.07f, isNegativeInversion: false);

                    HitFeedbackManager.TriggerHitFeedback(col.transform, col.bounds.center, meleeThrustDamage, true, EnemyHitType.PhysicalMelee);

                    Rigidbody2D enemyRb = col.GetComponent<Rigidbody2D>() ?? col.GetComponentInParent<Rigidbody2D>();
                    if (enemyRb != null && enemyRb.bodyType == RigidbodyType2D.Dynamic)
                    {
                        enemyRb.linearVelocity = new Vector2(dir * 15f, 4.0f);
                    }

                    if (PlayerCombatJuice.Instance != null)
                    {
                        PlayerCombatJuice.Instance.SpawnHitCollisionParticles(col.bounds.center, true);
                    }

                    if (SpearSlashVFX.Instance != null)
                    {
                        SpearSlashVFX.Instance.OnSpearHitEnemy(col.bounds.center, 3, dir);
                    }
                }
            }

            yield return null;
        }

        // 4. Smooth Retraction back to companion float
        thrustElapsed = 0f;
        Vector3 apexPos = transform.position;
        while (thrustElapsed < meleeThrustReturnDuration)
        {
            thrustElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(thrustElapsed / meleeThrustReturnDuration);
            Vector3 returnTarget = (lumi != null) ? lumi.transform.position : (playerTransform != null ? playerTransform.position : startPos);
            transform.position = Vector3.Lerp(apexPos, returnTarget, t * t);
            yield return null;
        }

        if (trailRenderer != null) trailRenderer.emitting = false;
        CurrentState = SpearState.CarriedByLumi;
        meleeThrustCoroutine = null;
    }

    private void SpawnSonicShockwave(Vector3 origin, float facingDirection)
    {
        GameObject waveGO = new GameObject("Spear_SonicShockwave");
        waveGO.transform.position = origin;
        waveGO.transform.rotation = Quaternion.Euler(0f, 0f, (facingDirection < 0f) ? 180f : 0f);

        ParticleSystem ps = waveGO.AddComponent<ParticleSystem>();
        var psRenderer = waveGO.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            LowResBlackOrb.ConfigureParticleRenderer(psRenderer, 22, spearRenderer != null ? spearRenderer.sortingLayerName : "Default");
        }

        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.01f, 0.01f, 0.02f, 0.95f), new Color(0.05f, 0.05f, 0.08f, 0.85f));
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.startSpeed = 16f;
        main.startLifetime = 0.22f;
        main.loop = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(0f, 90f, 0f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        ps.Play();
        Destroy(waveGO, 0.5f);
    }

    /// <summary>
    /// Invoked by on-screen touch spear button. Throws spear, recalls, or super-launches.
    /// </summary>
    public void TriggerSpearActionFromTouch()
    {
        if (move.Instance != null && move.Instance.IsBlobForm) return;
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

        Camera cam = Camera.main;
        if (cam == null) return defaultDir;

        // 1. Mouse / Cursor Aiming (PC and Editor)
        Vector3 mPos = Input.mousePosition;
        if (!float.IsNaN(mPos.x) && !float.IsInfinity(mPos.x) && !float.IsNaN(mPos.y) && !float.IsInfinity(mPos.y))
        {
            try
            {
                Vector3 screenPoint = cam.orthographic ? new Vector3(mPos.x, mPos.y, 0f) : new Vector3(mPos.x, mPos.y, -cam.transform.position.z);
                Vector3 mouseWorld = cam.ScreenToWorldPoint(screenPoint);
                mouseWorld.z = 0f;
                Vector2 delta = (Vector2)mouseWorld - (Vector2)transform.position;
                if (delta.sqrMagnitude > 0.02f)
                {
                    return delta.normalized;
                }
            }
            catch (System.Exception) { }
        }

        // 2. Touch Aiming (if touching game area and not over UI buttons)
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    continue; // Skip touches on UI controls
                }

                try
                {
                    Vector3 screenPoint = cam.orthographic ? new Vector3(touch.position.x, touch.position.y, 0f) : new Vector3(touch.position.x, touch.position.y, -cam.transform.position.z);
                    Vector3 touchWorld = cam.ScreenToWorldPoint(screenPoint);
                    touchWorld.z = 0f;
                    Vector2 delta = (Vector2)touchWorld - (Vector2)transform.position;
                    if (delta.sqrMagnitude > 0.02f)
                    {
                        return delta.normalized;
                    }
                }
                catch (System.Exception) { }
            }
        }

        return defaultDir;
    }

    #region Specialized Weapon Abilities

    private void ExecuteGroundAnchorTeleport()
    {
        if (playerTransform == null) return;

        Vector3 targetBlinkPos = transform.position + Vector3.up * 0.9f;
        playerTransform.position = targetBlinkPos;

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }

        // Apply brief i-frames (0.5s) to guarantee dodge
        if (playerMove != null)
        {
            StartCoroutine(TemporaryDodgeInvulnerability(0.5f));
        }

        // Visual Dark Decoy & Smoke
        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, 0, false, EnemyHitType.PhysicalMelee);

        // Recall spear back to player hands immediately
        Recall();
        Debug.Log("[LumiSpearWeapon] GROUND ANCHOR EMERGENCY DODGE EXECUTED! Teleported to spear with i-frames.");
    }

    private IEnumerator TemporaryDodgeInvulnerability(float duration)
    {
        if (playerMove == null) yield break;
        SpriteRenderer sr = playerMove.GetComponent<SpriteRenderer>();
        Color orig = sr != null ? sr.color : Color.white;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (sr != null) sr.color = new Color(0.7f, 0.2f, 1f, 0.5f);
            yield return null;
        }

        if (sr != null) sr.color = orig;
    }

    // Blood Blade Autonomous Slashing (Zero Player Freeze)
    public void PerformBloodBladeAutonomousSlash(Vector2 aimDir)
    {
        if (isAutonomousSlashing || CurrentState != SpearState.CarriedByLumi) return;
        StartCoroutine(BloodBladeSlashRoutine(aimDir));
    }

    private IEnumerator BloodBladeSlashRoutine(Vector2 aimDir)
    {
        isAutonomousSlashing = true;
        Vector3 originPos = transform.position;
        Vector3 targetSlashPos = originPos + (Vector3)(aimDir.normalized * 4.2f);

        if (trailRenderer != null) trailRenderer.emitting = true;

        float t = 0f;
        float slashOutDuration = 0.12f;
        while (t < slashOutDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(originPos, targetSlashPos, t / slashOutDuration);
            yield return null;
        }

        // Damage enemies in slash arc
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(targetSlashPos, 2.5f);
        foreach (var col in hitEnemies)
        {
            if (IsEnemyTarget(col))
            {
                var health = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.TakeDamage(Mathf.RoundToInt(pierceDamage * 1.25f));
                    HitFeedbackManager.TriggerHitFeedback(col.transform, col.transform.position, pierceDamage, true, EnemyHitType.PhysicalMelee);
                }
            }
        }

        // Slash return
        t = 0f;
        float slashReturnDuration = 0.14f;
        while (t < slashReturnDuration)
        {
            t += Time.deltaTime;
            Vector3 currentOrigin = (lumi != null) ? lumi.transform.position : (playerTransform != null ? playerTransform.position : originPos);
            transform.position = Vector3.Lerp(targetSlashPos, currentOrigin, t / slashReturnDuration);
            yield return null;
        }

        if (trailRenderer != null) trailRenderer.emitting = false;
        isAutonomousSlashing = false;
    }

    // DarkDag Duplication Orbiters
    private void SetupOrbitingDuplicates(Sprite daggerSprite, int count)
    {
        ClearOrbitingDuplicates();
        for (int i = 0; i < count; i++)
        {
            GameObject dup = new GameObject($"DarkDag_Orbiter_{i}");
            dup.transform.SetParent(transform.parent, false);

            SpriteRenderer sr = dup.AddComponent<SpriteRenderer>();
            sr.sprite = daggerSprite;
            sr.sortingLayerName = spearRenderer.sortingLayerName;
            sr.sortingOrder = spearRenderer.sortingOrder;

            CircleCollider2D col = dup.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            orbitingDaggerDuplicates.Add(dup);
        }
    }

    private void ClearOrbitingDuplicates()
    {
        foreach (var dup in orbitingDaggerDuplicates)
        {
            if (dup != null) Destroy(dup);
        }
        orbitingDaggerDuplicates.Clear();
    }

    private void UpdateOrbitingDuplicates()
    {
        if (currentWeaponID != WeaponID.DarkDag || orbitingDaggerDuplicates.Count == 0 || playerTransform == null) return;

        orbitAngle += Time.deltaTime * 180f; // 180 deg/sec
        float radius = 1.4f;

        for (int i = 0; i < orbitingDaggerDuplicates.Count; i++)
        {
            GameObject dup = orbitingDaggerDuplicates[i];
            if (dup == null) continue;

            float angle = (orbitAngle + (i * (360f / (orbitingDaggerDuplicates.Count + 1)))) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + 0.6f, 0f);
            dup.transform.position = playerTransform.position + offset;
            dup.transform.rotation = Quaternion.Euler(0, 0, (orbitAngle + (i * (360f / (orbitingDaggerDuplicates.Count + 1)))) + 90f);

            // Orbit contact damage
            Collider2D[] hits = Physics2D.OverlapCircleAll(dup.transform.position, 0.6f);
            foreach (var hit in hits)
            {
                if (IsEnemyTarget(hit))
                {
                    var h = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
                    if (h != null)
                    {
                        h.TakeDamage(12);
                    }
                }
            }
        }
    }

    // Seismic Shockwave for DarkAxe
    private void TriggerSeismicShockwave(Vector3 impactPos)
    {
        if (currentWeaponID != WeaponID.DarkAxe) return;

        float radius = 4.5f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPos, radius);
        foreach (var hit in hits)
        {
            if (IsEnemyTarget(hit))
            {
                var h = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(Mathf.RoundToInt(explosionDamage * 0.8f));
                    HitFeedbackManager.TriggerHitFeedback(hit.transform, hit.transform.position, explosionDamage, true, EnemyHitType.PhysicalMelee);
                }
            }
        }

        Debug.Log("[LumiSpearWeapon] SEISMIC SHOCKWAVE DETONATED!");
    }

    #endregion
}