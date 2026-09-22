using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Minigames;

public enum LightOrbState
{
    IdleFollow,
    ShieldActive,
    ManaDrainHeal,
    LightSpearParkour,
    PlayerHitPanic,
    CaveGuide,
    StorageOpen,
    GrappleActive,
    BodyTraceAttack
}

/// <summary>
/// "Lumi" - Light Companion Spirit with Grapple Hook Mechanic ('E' Key / Right Click):
/// 1. Lumi Grapple Hook ('E' / Right Click): Shoots a glowing cyan energy tether to Lumi, pulling the player upward/forward over gaps and ledges!
/// 2. Aerial Fling Release: Releasing 'E' or pressing Space flings the player into an aerial jump boost!
/// 3. Dynamic 2D Sorting Layer Sync: Continuously matches Player's exact sortingLayerID and sortingOrder in LateUpdate().
/// 4. Pure 100% Solid Black Eyes with specular highlights and dynamic eye tracking + emotion system.
/// 5. Spring-Mass Physics: Velocity squash & stretch math with spring recoil.
/// 6. HLSL Liquid Caustics & Wide Corona Halo: Custom/LumiGlowCore shader v2 with Voronoi caustics & iridescence.
/// 7. Floating particle motes, inner nebula layer, corona rotation, state transition VFX.
/// </summary>
public class LightOrbCompanion : MonoBehaviour
{
    public static LightOrbCompanion Instance { get; private set; }

    [Header("Bouncy Follow Physics")]
    public Vector3 followOffset = new Vector3(-2.4f, 2.2f, 0f);
    public float smoothTime = 0.16f;
    public float hoverFrequency1 = 3.8f;
    public float hoverFrequency2 = 7.2f;
    public float hoverAmplitude = 0.18f;

    [Header("Grapple Hook Settings")]
    public KeyCode grappleKey = KeyCode.E;
    public float grappleSpeed = 20f;
    public float grappleFlingForce = 14f;
    public float maxGrappleDistance = 15f;
    public bool IsGrappling { get; private set; } = false;

    [Header("Spring Squash & Stretch")]
    public float maxVelocityStretch = 0.35f;
    public float springStiffness = 180f;
    public float springDamping = 12f;

    [Header("Cave Guidance")]
    public bool isCaveMode = false;
    public Vector3 caveGuideOffset = new Vector3(2.5f, 0.8f, 0f);

    [Header("Combat & Shield")]
    public bool isShieldActive = false;
    public float shieldDamageReduction = 0.75f;

    [Header("Orb Body Trace Attack ('X' Key)")]
    public KeyCode orbAttackKey = KeyCode.V;
    public int orbAttackDamage = 35;
    public float orbAttackSpeed = 28f;
    public float orbAttackMaxDistance = 14f;
    public float orbAttackCooldown = 0.45f;
    private float nextOrbAttackTime = 0f;
    private bool isBodyAttacking = false;

    [Header("Core Alpha / Transparency")]
    [Range(0.1f, 1.0f)]
    public float coreAlpha = 0.78f; // Make white core nucleus slightly transparent

    [Header("Rich Solid Colors")]
    public Color colorIdle = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    public Color colorGlowIdle = new Color(0.4f, 0.90f, 1.0f, 1.0f);
    public Color colorShield = new Color(0.0f, 0.45f, 1.0f, 1.0f);
    public Color colorHeal = new Color(0.68f, 0.15f, 0.95f, 1.0f); // Dark Abyssal Void Violet
    public Color colorSpear = new Color(1.0f, 0.78f, 0.0f, 1.0f);
    public Color colorGrapple = new Color(0.1f, 1.0f, 0.85f, 1.0f);  // Radiant Cyan Grapple
    public Color colorPanic = new Color(1.0f, 0.05f, 0.2f, 1.0f);
    public Color colorAttack = new Color(1.0f, 0.92f, 0.35f, 1.0f);  // Radiant Energetic Gold Body Trace
    public Color colorGlowAttack = new Color(1.0f, 0.55f, 0.1f, 1.0f);

    private Transform playerTransform;
    private Health playerHealth;
    private MageCombat playerCombat;
    private move playerMove;
    private SpriteRenderer playerSpriteRenderer;
    private Rigidbody2D playerRb;

    // Procedural Render Layers & Materials
    private SpriteRenderer orbCoreRenderer;
    private SpriteRenderer orbCoronaRenderer;
    private SpriteRenderer orbNebulaRenderer;  // NEW: Inner nebula layer
    private Material coreShaderMaterial;
    private LineRenderer tetherLineRenderer;
    private LineRenderer attackTraceLineRenderer;

    // Layer 3: Solid Black Eyes with specular highlights
    private SpriteRenderer eyeLeftRenderer;
    private SpriteRenderer eyeRightRenderer;
    private SpriteRenderer eyeLeftSpecular;  // NEW: white specular dot
    private SpriteRenderer eyeRightSpecular; // NEW: white specular dot
    private Transform eyeContainer;

    private Light pointLight;

    // Forcefield Shield
    private GameObject forcefieldShieldGO;
    private SpriteRenderer forcefieldRenderer;
    private Light forcefieldLight;

    // Floating particle motes
    private List<Transform> floatingMotes = new List<Transform>();
    private List<SpriteRenderer> moteRenderers = new List<SpriteRenderer>();
    private const int MOTE_COUNT = 6;

    // Ambient trail particles
    private List<TrailMote> trailMotes = new List<TrailMote>();
    private float trailSpawnTimer = 0f;

    // State transition VFX
    private LightOrbState previousState = LightOrbState.IdleFollow;

    // Spring Mass Physics State
    private Vector3 currentVelocity;
    private Vector3 scaleSpringOffset = Vector3.zero;
    private Vector3 scaleSpringVelocity = Vector3.zero;

    // Blink & Pop State
    private float blinkTimer = 3.0f;
    private bool isBlinking = false;
    private float popBounceScale = 1.0f;

    private LightOrbState currentState = LightOrbState.IdleFollow;
    private float panicTimer = 0f;
    private Color currentColor;
    private Color currentGlowColor;
    private Color displayColor;      // Smoothly lerped display color
    private Color displayGlowColor;  // Smoothly lerped glow color

    // Corona rotation state
    private float coronaRotation = 0f;

    // Eye emotion target scales
    private Vector3 eyeTargetScale = Vector3.one;

    // Trail mote helper struct
    private struct TrailMote
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float lifetime;
        public float maxLifetime;
        public Vector3 velocity;
    }

    private static bool IsTutorialSceneActive()
    {
        string s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return string.Equals(s, "TutorialScene", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(s, "Tutorial", System.StringComparison.OrdinalIgnoreCase);
    }

    private void Awake()
    {
        // Lumi must NEVER exist in the TutorialScene under any circumstance
        if (IsTutorialSceneActive())
        {
            Debug.Log("[LightOrbCompanion] TutorialScene detected in Awake: Lumi must NEVER exist in TutorialScene. Destroying.");
            if (Instance == this) Instance = null;
            Destroy(gameObject);
            return;
        }

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            SetupComponents();
        }
        else
        {
            Destroy(gameObject);
        }
    }



    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (string.Equals(scene.name, "TutorialScene", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scene.name, "Tutorial", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("[LightOrbCompanion] SceneLoaded event to TutorialScene: Lumi must NEVER exist in TutorialScene. Destroying.");
            if (Instance == this) Instance = null;
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (IsTutorialSceneActive())
        {
            if (Instance == this) Instance = null;
            Destroy(gameObject);
            return;
        }

        FindPlayerReferences();
        currentColor = colorIdle;
        currentGlowColor = colorGlowIdle;
        displayColor = currentColor;
        displayGlowColor = currentGlowColor;
    }

    private Material CreateAdditiveMaterial()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        return mat;
    }

    private void SetupComponents()
    {
        Material additiveMat = CreateAdditiveMaterial();
        Material alphaMat = new Material(Shader.Find("Sprites/Default"));

        Shader customShader = Shader.Find("Custom/LumiGlowCore");
        if (customShader != null)
        {
            coreShaderMaterial = new Material(customShader);
            coreShaderMaterial.SetFloat("_HDRMultiplier", 1.8f);
            coreShaderMaterial.SetColor("_GlowColor", colorGlowIdle);
        }
        else
        {
            coreShaderMaterial = alphaMat;
        }

        // Layer 0: Wide Additive Corona Halo (with organic wisps)
        GameObject coronaGO = new GameObject("OrbCoronaHalo");
        coronaGO.transform.SetParent(transform, false);
        orbCoronaRenderer = coronaGO.AddComponent<SpriteRenderer>();
        orbCoronaRenderer.sharedMaterial = additiveMat;
        orbCoronaRenderer.sprite = CreateCoronaHaloSprite(256);
        orbCoronaRenderer.color = new Color(0.6f, 0.94f, 1.0f, 0.75f);
        orbCoronaRenderer.sortingOrder = 0;
        coronaGO.transform.localScale = Vector3.one * 3.0f;

        // Layer 0.5: Inner Nebula swirl layer
        GameObject nebulaGO = new GameObject("OrbInnerNebula");
        nebulaGO.transform.SetParent(transform, false);
        orbNebulaRenderer = nebulaGO.AddComponent<SpriteRenderer>();
        orbNebulaRenderer.sharedMaterial = additiveMat;
        orbNebulaRenderer.sprite = CreateNebulaSprite(256);
        orbNebulaRenderer.color = new Color(0.5f, 0.85f, 1.0f, 0.4f);
        orbNebulaRenderer.sortingOrder = 0;
        nebulaGO.transform.localScale = Vector3.one * 1.6f;

        // Layer 1: Solid Core Nucleus
        orbCoreRenderer = gameObject.AddComponent<SpriteRenderer>();
        orbCoreRenderer.sharedMaterial = coreShaderMaterial;
        orbCoreRenderer.sprite = CreateSolidCoreSprite(256);
        orbCoreRenderer.color = Color.white;
        orbCoreRenderer.sortingOrder = 0;

        // Layer 3: Pure 100% Solid Black Eye Sprites with specular highlights
        GameObject eyeGroup = new GameObject("OrbEyeGroup");
        eyeGroup.transform.SetParent(transform, false);
        eyeContainer = eyeGroup.transform;
        eyeContainer.localPosition = new Vector3(0, 0.04f, -0.1f);

        Sprite circleSprite = CreateSolidCoreSprite(64);
        Sprite tinyCircle = CreateSolidCoreSprite(32);
        Color solidBlack = Color.black;

        // Left Eye
        GameObject eyeL = new GameObject("EyeLeft");
        eyeL.transform.SetParent(eyeContainer, false);
        eyeL.transform.localPosition = new Vector3(-0.16f, 0f, 0f);
        eyeL.transform.localScale = new Vector3(0.17f, 0.17f, 1f);
        eyeLeftRenderer = eyeL.AddComponent<SpriteRenderer>();
        eyeLeftRenderer.sharedMaterial = alphaMat;
        eyeLeftRenderer.sprite = circleSprite;
        eyeLeftRenderer.color = solidBlack;
        eyeLeftRenderer.sortingOrder = 1;

        // Left Eye Specular Highlight
        GameObject eyeLSpec = new GameObject("EyeLeftSpecular");
        eyeLSpec.transform.SetParent(eyeL.transform, false);
        eyeLSpec.transform.localPosition = new Vector3(0.22f, 0.25f, -0.01f);
        eyeLSpec.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        eyeLeftSpecular = eyeLSpec.AddComponent<SpriteRenderer>();
        eyeLeftSpecular.sharedMaterial = alphaMat;
        eyeLeftSpecular.sprite = tinyCircle;
        eyeLeftSpecular.color = new Color(1f, 1f, 1f, 0.92f);
        eyeLeftSpecular.sortingOrder = 2;

        // Right Eye
        GameObject eyeR = new GameObject("EyeRight");
        eyeR.transform.SetParent(eyeContainer, false);
        eyeR.transform.localPosition = new Vector3(0.16f, 0f, 0f);
        eyeR.transform.localScale = new Vector3(0.17f, 0.17f, 1f);
        eyeRightRenderer = eyeR.AddComponent<SpriteRenderer>();
        eyeRightRenderer.sharedMaterial = alphaMat;
        eyeRightRenderer.sprite = circleSprite;
        eyeRightRenderer.color = solidBlack;
        eyeRightRenderer.sortingOrder = 1;

        // Right Eye Specular Highlight
        GameObject eyeRSpec = new GameObject("EyeRightSpecular");
        eyeRSpec.transform.SetParent(eyeR.transform, false);
        eyeRSpec.transform.localPosition = new Vector3(0.22f, 0.25f, -0.01f);
        eyeRSpec.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        eyeRightSpecular = eyeRSpec.AddComponent<SpriteRenderer>();
        eyeRightSpecular.sharedMaterial = alphaMat;
        eyeRightSpecular.sprite = tinyCircle;
        eyeRightSpecular.color = new Color(1f, 1f, 1f, 0.92f);
        eyeRightSpecular.sortingOrder = 2;

        // Grapple Beam LineRenderer
        GameObject lineGO = new GameObject("LumiGrappleTether");
        lineGO.transform.SetParent(transform, false);
        tetherLineRenderer = lineGO.AddComponent<LineRenderer>();
        tetherLineRenderer.sharedMaterial = additiveMat;
        tetherLineRenderer.startWidth = 0.18f;
        tetherLineRenderer.endWidth = 0.08f;
        tetherLineRenderer.positionCount = 2;
        tetherLineRenderer.enabled = false;

        // Orb Body Trace Attack LineRenderer
        GameObject attackLineGO = new GameObject("LumiAttackTraceLine");
        attackLineGO.transform.SetParent(transform, false);
        attackTraceLineRenderer = attackLineGO.AddComponent<LineRenderer>();
        attackTraceLineRenderer.sharedMaterial = additiveMat;
        attackTraceLineRenderer.startWidth = 0.28f;
        attackTraceLineRenderer.endWidth = 0.06f;
        attackTraceLineRenderer.positionCount = 2;
        attackTraceLineRenderer.enabled = false;

        // Point Light Component
        pointLight = gameObject.AddComponent<Light>();
        if (pointLight != null)
        {
            pointLight.type = LightType.Point;
            pointLight.range = 9.5f;
            pointLight.color = colorIdle;
            pointLight.intensity = 2.5f;
        }

        // Floating Particle Motes
        SetupFloatingMotes(additiveMat);
    }

    private void SetupFloatingMotes(Material additiveMat)
    {
        Sprite moteSprite = CreateMoteSprite(32);
        for (int i = 0; i < MOTE_COUNT; i++)
        {
            GameObject moteGO = new GameObject("FloatingMote_" + i);
            moteGO.transform.SetParent(transform, false);
            SpriteRenderer moteSR = moteGO.AddComponent<SpriteRenderer>();
            moteSR.sharedMaterial = additiveMat;
            moteSR.sprite = moteSprite;
            moteSR.color = new Color(0.7f, 0.95f, 1.0f, 0.6f);
            moteSR.sortingOrder = 0;
            float scale = Random.Range(0.08f, 0.16f);
            moteGO.transform.localScale = Vector3.one * scale;

            // Random initial orbit position
            float angle = (i / (float)MOTE_COUNT) * Mathf.PI * 2f + Random.Range(0f, 0.5f);
            float radius = Random.Range(0.4f, 0.9f);
            moteGO.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );

            floatingMotes.Add(moteGO.transform);
            moteRenderers.Add(moteSR);
        }
    }

    private void FindPlayerReferences()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerHealth = playerGO.GetComponent<Health>();
            playerCombat = playerGO.GetComponent<MageCombat>();
            playerMove = playerGO.GetComponent<move>();
            playerRb = playerGO.GetComponent<Rigidbody2D>();
            playerSpriteRenderer = playerGO.GetComponentInChildren<SpriteRenderer>();

            if (playerHealth != null)
            {
                playerHealth.onDamageTaken += OnPlayerTookDamage;
            }

            SyncSortingLayersWithPlayer();
            SetupForcefieldShield(playerGO);
        }
    }

    private void SyncSortingLayersWithPlayer()
    {
        if (playerSpriteRenderer == null)
        {
            if (playerTransform != null) playerSpriteRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();
            if (playerSpriteRenderer == null) return;
        }

        int playerLayerID = playerSpriteRenderer.sortingLayerID;
        int playerOrder = playerSpriteRenderer.sortingOrder;

        if (orbCoronaRenderer != null)
        {
            orbCoronaRenderer.sortingLayerID = playerLayerID;
            orbCoronaRenderer.sortingOrder = playerOrder;
        }
        if (orbNebulaRenderer != null)
        {
            orbNebulaRenderer.sortingLayerID = playerLayerID;
            orbNebulaRenderer.sortingOrder = playerOrder;
        }
        if (orbCoreRenderer != null)
        {
            orbCoreRenderer.sortingLayerID = playerLayerID;
            orbCoreRenderer.sortingOrder = playerOrder;
        }
        if (eyeLeftRenderer != null)
        {
            eyeLeftRenderer.sortingLayerID = playerLayerID;
            eyeLeftRenderer.sortingOrder = playerOrder + 1;
        }
        if (eyeRightRenderer != null)
        {
            eyeRightRenderer.sortingLayerID = playerLayerID;
            eyeRightRenderer.sortingOrder = playerOrder + 1;
        }
        if (eyeLeftSpecular != null)
        {
            eyeLeftSpecular.sortingLayerID = playerLayerID;
            eyeLeftSpecular.sortingOrder = playerOrder + 2;
        }
        if (eyeRightSpecular != null)
        {
            eyeRightSpecular.sortingLayerID = playerLayerID;
            eyeRightSpecular.sortingOrder = playerOrder + 2;
        }
        if (tetherLineRenderer != null)
        {
            tetherLineRenderer.sortingLayerID = playerLayerID;
            tetherLineRenderer.sortingOrder = playerOrder + 1;
        }
        if (attackTraceLineRenderer != null)
        {
            attackTraceLineRenderer.sortingLayerID = playerLayerID;
            attackTraceLineRenderer.sortingOrder = playerOrder + 1;
        }

        // Sync floating motes
        for (int i = 0; i < moteRenderers.Count; i++)
        {
            if (moteRenderers[i] != null)
            {
                moteRenderers[i].sortingLayerID = playerLayerID;
                moteRenderers[i].sortingOrder = playerOrder;
            }
        }
    }

    private void SetupForcefieldShield(GameObject playerGO)
    {
        if (forcefieldShieldGO != null) return;

        forcefieldShieldGO = new GameObject("LumiForcefieldShield");
        forcefieldShieldGO.transform.SetParent(playerGO.transform, false);
        forcefieldShieldGO.transform.localPosition = new Vector3(0f, 0.2f, -0.1f);
        forcefieldShieldGO.transform.localScale = Vector3.zero;

        forcefieldRenderer = forcefieldShieldGO.AddComponent<SpriteRenderer>();
        forcefieldRenderer.sharedMaterial = CreateAdditiveMaterial();
        forcefieldRenderer.sprite = CreateForcefieldDomeSprite(256);
        forcefieldRenderer.color = new Color(colorShield.r, colorShield.g, colorShield.b, 0.85f);
        forcefieldRenderer.sortingOrder = 25;

        forcefieldLight = forcefieldShieldGO.AddComponent<Light>();
        if (forcefieldLight != null)
        {
            forcefieldLight.type = LightType.Point;
            forcefieldLight.range = 8f;
            forcefieldLight.color = colorShield;
            forcefieldLight.intensity = 3.0f;
        }

        forcefieldShieldGO.SetActive(false);
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;

        if (playerHealth != null)
        {
            playerHealth.onDamageTaken -= OnPlayerTookDamage;
        }

        // Clean up trail motes
        foreach (var tm in trailMotes)
        {
            if (tm.go != null) Destroy(tm.go);
        }
        trailMotes.Clear();
    }

    private void Update()
    {
        if (IsTutorialSceneActive())
        {
            if (Instance == this) Instance = null;
            Destroy(gameObject);
            return;
        }


        if (playerTransform == null)
        {
            FindPlayerReferences();
            if (playerTransform == null) return;
        }

        HandleInput();
        UpdateStateAndVisuals();
        UpdateBouncyMovementAndSquash();
        UpdateGrappleMechanic();
        UpdateEyeLookAtTracking();
        UpdateEyeEmotion();
        UpdateBlinkAnimation();
        UpdateForcefieldShield();
        UpdateFloatingMotes();
        UpdateCoronaRotation();
        UpdateNebulaSwirl();
        UpdateAmbientTrail();
        UpdateGrappleTetherPulse();
    }

    private void LateUpdate()
    {
        SyncSortingLayersWithPlayer();
    }

    private void HandleInput()
    {
        if (OrbInventoryUI.Instance != null && OrbInventoryUI.Instance.IsInventoryOpen)
        {
            currentState = LightOrbState.StorageOpen;
            return;
        }

        if (panicTimer > 0f)
        {
            panicTimer -= Time.deltaTime;
            currentState = LightOrbState.PlayerHitPanic;
            return;
        }

        // Grapple Hook permanently disabled per player mobility balance
        bool isBlocked = IsPlayerInteractingOrNearInteractable();
        if (IsGrappling)
        {
            ReleaseGrapple(false);
        }

        // Orb Body Trace Attack ('X' Key Press)
        if (Input.GetKeyDown(orbAttackKey) && !isBodyAttacking && Time.time >= nextOrbAttackTime && !isBlocked)
        {
            StartBodyTraceAttack();
            return;
        }

        // Shield Defense ('H' Key Hold)
        if (Input.GetKey(KeyCode.H))
        {
            currentState = LightOrbState.ShieldActive;
            isShieldActive = true;
        }
        else
        {
            isShieldActive = false;

            // Mana-Drain Heal ('B' Key Press)
            if (Input.GetKeyDown(KeyCode.B))
            {
                TriggerPopBounce(1.35f);
                PerformManaDrainHeal();
            }

            else if (isCaveMode)
            {
                currentState = LightOrbState.CaveGuide;
            }
            else
            {
                currentState = LightOrbState.IdleFollow;
            }
        }
    }

    private void StartGrapple()
    {
        IsGrappling = true;
        TriggerPopBounce(1.5f);
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = true;
        Debug.Log("Lumi: Grapple Hook Activated!");
    }

    private void ReleaseGrapple(bool shouldFling)
    {
        IsGrappling = false;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = false;

        if (shouldFling && playerRb != null)
        {
            Vector2 flingDir = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
            playerRb.linearVelocity = (flingDir + Vector2.up * 0.4f).normalized * grappleFlingForce;
            TriggerPopBounce(1.4f);
            Debug.Log("Lumi: Released Grapple with Aerial Fling Boost!");
        }
    }

    private void UpdateGrappleMechanic()
    {
        if (!IsGrappling || playerTransform == null || playerRb == null)
        {
            if (tetherLineRenderer != null && tetherLineRenderer.enabled && !IsGrappling)
            {
                tetherLineRenderer.enabled = false;
            }
            return;
        }

        Vector3 playerPos = playerTransform.position + Vector3.up * 0.3f;
        Vector3 lumiPos = transform.position;
        Vector3 grappleDir = (lumiPos - playerPos).normalized;
        float distToLumi = Vector3.Distance(playerPos, lumiPos);

        // Pull player smoothly toward Lumi
        playerRb.linearVelocity = grappleDir * grappleSpeed;

        // Draw Cyan-White Energy Beam
        if (tetherLineRenderer != null)
        {
            tetherLineRenderer.SetPosition(0, playerPos);
            tetherLineRenderer.SetPosition(1, lumiPos);
            tetherLineRenderer.startColor = displayGlowColor;
            tetherLineRenderer.endColor = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.4f);
        }

        // Auto-release if arrived close to Lumi
        if (distToLumi <= 1.2f)
        {
            ReleaseGrapple(true);
        }
    }

    /// <summary>
    /// Animated grapple tether width pulsing for a more energetic look.
    /// </summary>
    private void UpdateGrappleTetherPulse()
    {
        if (tetherLineRenderer == null || !tetherLineRenderer.enabled) return;

        float pulse = 0.14f + Mathf.Sin(Time.time * 18f) * 0.06f;
        float pulseEnd = 0.06f + Mathf.Sin(Time.time * 18f + 1f) * 0.03f;
        tetherLineRenderer.startWidth = pulse;
        tetherLineRenderer.endWidth = pulseEnd;
    }

    /// <summary>
    /// Checks if player is currently interacting, entering a scene transition, or near an interactable object/NPC/door/UI.
    /// </summary>
    private bool IsPlayerInteractingOrNearInteractable()
    {
        // 1. Active UI/Dialogue checks
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return true;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return true;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return true;
        if (NyxarisManager.IsChatActive) return true;
        if (OrbInventoryUI.Instance != null && OrbInventoryUI.Instance.IsInventoryOpen) return true;

        // 2. Check for nearby interactables, enter signs, doors, or NPCs
        if (playerTransform != null)
        {
            Collider2D[] hitCols = Physics2D.OverlapCircleAll(playerTransform.position, 1.8f);
            foreach (var col in hitCols)
            {
                if (col == null) continue;

                if (col.GetComponent<entersign>() != null ||
                    col.GetComponentInParent<entersign>() != null ||
                    col.GetComponent<DojoDoorTransition>() != null ||
                    col.GetComponentInParent<DojoDoorTransition>() != null ||
                    col.GetComponent<NPCInteractable>() != null ||
                    col.GetComponentInParent<NPCInteractable>() != null ||
                    col.GetComponent<InspectableDojoObject>() != null ||
                    col.GetComponentInParent<InspectableDojoObject>() != null ||
                    col.GetComponent<SpeechBubbleDialogue>() != null ||
                    col.GetComponentInParent<SpeechBubbleDialogue>() != null ||
                    (col.gameObject.name != null && (col.gameObject.name.Contains("Sign") || col.gameObject.name.Contains("Door") || col.gameObject.name.Contains("NPC"))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void UpdateBouncyMovementAndSquash()
    {
        if (playerTransform == null) return;

        float facingDirection = (playerTransform.localScale.x < 0) ? -1.0f : 1.0f;

        Vector3 targetOffset = followOffset;
        targetOffset.x *= facingDirection;

        if (currentState == LightOrbState.CaveGuide)
        {
            targetOffset = caveGuideOffset;
            targetOffset.x *= facingDirection;
        }

        Vector3 targetPos = playerTransform.position + targetOffset;

        float hover = (Mathf.Sin(Time.time * hoverFrequency1) * 0.7f + Mathf.Sin(Time.time * hoverFrequency2) * 0.3f) * hoverAmplitude;
        targetPos.y += hover;

        // Move Lumi to hover position (unless grappling or body attacking)
        if (!IsGrappling && !isBodyAttacking)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);
        }

        // Velocity-based squash and stretch target calculation
        float speed = currentVelocity.magnitude;
        Vector3 targetScaleOffset = Vector3.zero;

        if (speed > 0.4f)
        {
            float stretchFactor = Mathf.Clamp(speed * 0.04f, 0f, 0.15f);
            targetScaleOffset = new Vector3(-stretchFactor * 0.5f, stretchFactor, 0f);
        }

        // Robust, unconditionally stable smooth damping for spring squash & stretch
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        scaleSpringOffset = Vector3.SmoothDamp(scaleSpringOffset, targetScaleOffset, ref scaleSpringVelocity, 0.08f, 10f, dt);
        
        // Safety clamp on spring offset
        scaleSpringOffset.x = Mathf.Clamp(scaleSpringOffset.x, -0.2f, 0.2f);
        scaleSpringOffset.y = Mathf.Clamp(scaleSpringOffset.y, -0.2f, 0.2f);
        scaleSpringOffset.z = 0f;

        // Enhanced breathing animation - subtle and bounded
        float breathe = 1.0f + Mathf.Sin(Time.time * 1.5f) * 0.04f + Mathf.Sin(Time.time * 2.7f) * 0.02f;
        float corePulse = Mathf.Clamp(breathe * popBounceScale, 0.8f, 1.25f);
        Vector3 finalScale = Vector3.one * corePulse + scaleSpringOffset;

        // Clamp final scale within strictly safe visual boundaries
        finalScale.x = Mathf.Clamp(finalScale.x, 0.7f, 1.3f);
        finalScale.y = Mathf.Clamp(finalScale.y, 0.7f, 1.3f);
        finalScale.z = 1f;

        // Apply scale to root transform
        transform.localScale = finalScale;

        // Child renderers maintain stable local scale proportions
        if (orbCoronaRenderer != null)
        {
            float coronaPulse = 3.0f * (1.0f + Mathf.Sin(Time.time * 2.5f) * 0.04f);
            coronaPulse = Mathf.Clamp(coronaPulse, 2.7f, 3.3f);
            orbCoronaRenderer.transform.localScale = Vector3.one * coronaPulse;
        }
        if (orbNebulaRenderer != null)
        {
            float nebulaPulse = 1.6f * (1.0f + Mathf.Sin(Time.time * 1.8f) * 0.03f);
            nebulaPulse = Mathf.Clamp(nebulaPulse, 1.4f, 1.8f);
            orbNebulaRenderer.transform.localScale = Vector3.one * nebulaPulse;
        }

        popBounceScale = Mathf.Lerp(popBounceScale, 1.0f, Time.deltaTime * 6f);
    }

    private void UpdateEyeLookAtTracking()
    {
        if (eyeContainer == null || playerTransform == null) return;

        Vector3 lookTarget = playerTransform.position + Vector3.up * 0.6f;
        Vector3 dir = (lookTarget - transform.position).normalized;

        Vector3 eyeTargetLocal = new Vector3(dir.x * 0.06f, dir.y * 0.06f + 0.04f, -0.1f);
        eyeContainer.localPosition = Vector3.Lerp(eyeContainer.localPosition, eyeTargetLocal, Time.deltaTime * 10f);
    }

    /// <summary>
    /// Emotion-driven eye scale changes based on current state.
    /// </summary>
    private void UpdateEyeEmotion()
    {
        switch (currentState)
        {
            case LightOrbState.PlayerHitPanic:
                // Eyes widen in panic
                eyeTargetScale = new Vector3(1.3f, 1.35f, 1f);
                break;
            case LightOrbState.ShieldActive:
                // Eyes squint in concentration
                eyeTargetScale = new Vector3(1.1f, 0.6f, 1f);
                break;
            case LightOrbState.ManaDrainHeal:
                // Eyes half-close, peaceful
                eyeTargetScale = new Vector3(0.95f, 0.55f, 1f);
                break;
            case LightOrbState.GrappleActive:
                // Eyes sparkle wide, excited
                eyeTargetScale = new Vector3(1.2f, 1.2f, 1f);
                break;
            case LightOrbState.StorageOpen:
                // Eyes look curious, slightly wider
                eyeTargetScale = new Vector3(1.1f, 1.15f, 1f);
                break;
            default:
                // Normal relaxed eyes
                eyeTargetScale = Vector3.one;
                break;
        }

        if (eyeLeftRenderer != null && !isBlinking)
        {
            eyeLeftRenderer.transform.localScale = Vector3.Lerp(
                eyeLeftRenderer.transform.localScale,
                new Vector3(0.17f, 0.17f, 1f) * new Vector3(eyeTargetScale.x, eyeTargetScale.y, 1f).x,
                Time.deltaTime * 8f
            );
            // Apply Y scale separately for proper squash
            Vector3 lScale = eyeLeftRenderer.transform.localScale;
            float targetY = 0.17f * eyeTargetScale.y;
            lScale.y = Mathf.Lerp(lScale.y, targetY, Time.deltaTime * 8f);
            eyeLeftRenderer.transform.localScale = lScale;
        }
        if (eyeRightRenderer != null && !isBlinking)
        {
            eyeRightRenderer.transform.localScale = Vector3.Lerp(
                eyeRightRenderer.transform.localScale,
                new Vector3(0.17f, 0.17f, 1f) * new Vector3(eyeTargetScale.x, eyeTargetScale.y, 1f).x,
                Time.deltaTime * 8f
            );
            Vector3 rScale = eyeRightRenderer.transform.localScale;
            float targetY = 0.17f * eyeTargetScale.y;
            rScale.y = Mathf.Lerp(rScale.y, targetY, Time.deltaTime * 8f);
            eyeRightRenderer.transform.localScale = rScale;
        }
    }

    public void TriggerPopBounce(float scaleMultiplier)
    {
        popBounceScale = Mathf.Clamp(scaleMultiplier, 0.85f, 1.25f);
        scaleSpringVelocity = new Vector3(0.15f, -0.2f, 0f) * popBounceScale;
    }

    private void UpdateBlinkAnimation()
    {
        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            blinkTimer = Random.Range(2.5f, 4.5f);
            StartCoroutine(BlinkRoutine());
        }
    }

    private IEnumerator BlinkRoutine()
    {
        isBlinking = true;
        if (eyeContainer != null)
        {
            eyeContainer.localScale = new Vector3(1f, 0.12f, 1f);
        }
        yield return new WaitForSeconds(0.12f);
        if (eyeContainer != null)
        {
            eyeContainer.localScale = Vector3.one;
        }
        isBlinking = false;
    }

    private void UpdateStateAndVisuals()
    {
        // Check for state transition to trigger VFX
        if (currentState != previousState)
        {
            SpawnStateTransitionRing();
            previousState = currentState;
        }

        switch (currentState)
        {
            case LightOrbState.IdleFollow:
                currentColor = colorIdle;
                currentGlowColor = colorGlowIdle;
                SetLightRadius(9.5f, 2.4f);
                break;

            case LightOrbState.GrappleActive:
                currentColor = colorGrapple;
                currentGlowColor = colorGrapple;
                SetLightRadius(13.0f, 3.0f);
                break;

            case LightOrbState.ShieldActive:
                currentColor = colorShield;
                currentGlowColor = new Color(0.0f, 0.8f, 1.0f, 1f);
                SetLightRadius(11.5f, 2.8f);
                break;

            case LightOrbState.ManaDrainHeal:
                currentColor = colorHeal;
                currentGlowColor = new Color(0.4f, 1.0f, 0.7f, 1f);
                SetLightRadius(14.0f, 3.2f);
                break;

            case LightOrbState.LightSpearParkour:
                currentColor = colorSpear;
                currentGlowColor = new Color(1.0f, 0.9f, 0.4f, 1f);
                SetLightRadius(11.5f, 2.6f);
                break;

            case LightOrbState.PlayerHitPanic:
                currentColor = colorPanic;
                currentGlowColor = new Color(1.0f, 0.4f, 0.5f, 1f);
                SetLightRadius(10.5f, 2.7f);
                break;

            case LightOrbState.CaveGuide:
                currentColor = colorIdle;
                currentGlowColor = colorGlowIdle;
                SetLightRadius(16.5f, 3.0f);
                break;

            case LightOrbState.StorageOpen:
                currentColor = colorSpear;
                currentGlowColor = new Color(1.0f, 0.9f, 0.4f, 1f);
                SetLightRadius(11.5f, 2.4f);
                break;

            case LightOrbState.BodyTraceAttack:
                currentColor = colorAttack;
                currentGlowColor = colorGlowAttack;
                SetLightRadius(15.0f, 3.5f);
                break;
        }

        // Smooth color transitions instead of instant
        float colorLerpSpeed = 6f;
        displayColor = Color.Lerp(displayColor, currentColor, Time.deltaTime * colorLerpSpeed);
        displayGlowColor = Color.Lerp(displayGlowColor, currentGlowColor, Time.deltaTime * colorLerpSpeed);

        if (orbCoreRenderer != null) orbCoreRenderer.color = new Color(displayColor.r, displayColor.g, displayColor.b, coreAlpha);
        if (coreShaderMaterial != null && coreShaderMaterial.HasProperty("_GlowColor"))
        {
            coreShaderMaterial.SetColor("_GlowColor", displayGlowColor);
            coreShaderMaterial.SetColor("_Color", displayColor);
        }

        if (orbCoronaRenderer != null) orbCoronaRenderer.color = Color.Lerp(orbCoronaRenderer.color, new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.65f), Time.deltaTime * 8f);
        if (orbNebulaRenderer != null) orbNebulaRenderer.color = Color.Lerp(orbNebulaRenderer.color, new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.35f), Time.deltaTime * 8f);
        if (pointLight != null) pointLight.color = Color.Lerp(pointLight.color, displayGlowColor, Time.deltaTime * 8f);

        // Update floating mote colors
        for (int i = 0; i < moteRenderers.Count; i++)
        {
            if (moteRenderers[i] != null)
            {
                moteRenderers[i].color = Color.Lerp(moteRenderers[i].color,
                    new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.5f),
                    Time.deltaTime * 4f);
            }
        }
    }

    /// <summary>
    /// Spawn an expanding ring VFX when Lumi changes state.
    /// </summary>
    private void SpawnStateTransitionRing()
    {
        StartCoroutine(StateTransitionRingRoutine());
    }

    private IEnumerator StateTransitionRingRoutine()
    {
        GameObject ringGO = new GameObject("LumiStateRing");
        ringGO.transform.position = transform.position;

        SpriteRenderer ringSR = ringGO.AddComponent<SpriteRenderer>();
        ringSR.sharedMaterial = CreateAdditiveMaterial();
        ringSR.sprite = CreateRingSprite(128);
        ringSR.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.9f);
        ringSR.sortingOrder = 30;

        if (playerSpriteRenderer != null)
        {
            ringSR.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        }

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scale = Mathf.Lerp(0.3f, 2.5f, t);
            ringGO.transform.localScale = Vector3.one * scale;

            float alpha = (1f - t) * 0.9f;
            ringSR.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, alpha);

            ringGO.transform.position = transform.position;

            yield return null;
        }

        Destroy(ringGO);
    }

    /// <summary>
    /// Floating particle motes that slowly orbit and drift around Lumi.
    /// </summary>
    private void UpdateFloatingMotes()
    {
        float time = Time.time;

        for (int i = 0; i < floatingMotes.Count; i++)
        {
            if (floatingMotes[i] == null) continue;

            float baseAngle = (i / (float)MOTE_COUNT) * Mathf.PI * 2f;
            float orbitSpeed = 0.4f + i * 0.08f;
            float angle = baseAngle + time * orbitSpeed;

            // Elliptical orbit with slight vertical wobble
            float radiusX = 0.5f + Mathf.Sin(time * 0.7f + i) * 0.15f;
            float radiusY = 0.4f + Mathf.Cos(time * 0.5f + i * 0.8f) * 0.12f;

            Vector3 targetLocal = new Vector3(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY + Mathf.Sin(time * 1.5f + i * 2f) * 0.1f,
                0f
            );

            floatingMotes[i].localPosition = Vector3.Lerp(floatingMotes[i].localPosition, targetLocal, Time.deltaTime * 3f);

            // Pulsing alpha for fade in/out effect
            if (moteRenderers[i] != null)
            {
                float alphaBase = 0.35f + Mathf.Sin(time * 2f + i * 1.2f) * 0.25f;
                Color c = moteRenderers[i].color;
                c.a = Mathf.Lerp(c.a, alphaBase, Time.deltaTime * 4f);
                moteRenderers[i].color = c;

                // Gentle scale breathing
                float s = (0.1f + Mathf.Sin(time * 1.8f + i) * 0.04f);
                floatingMotes[i].localScale = Vector3.one * s;
            }
        }
    }

    /// <summary>
    /// Slowly rotate the corona halo for organic, living-energy feel.
    /// </summary>
    private void UpdateCoronaRotation()
    {
        if (orbCoronaRenderer == null) return;

        coronaRotation += Time.deltaTime * 15f; // 15 degrees per second
        orbCoronaRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, coronaRotation);
    }

    /// <summary>
    /// Slowly counter-rotate the inner nebula for a swirling plasma effect.
    /// </summary>
    private void UpdateNebulaSwirl()
    {
        if (orbNebulaRenderer == null) return;

        orbNebulaRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -coronaRotation * 0.7f + 45f);
    }

    /// <summary>
    /// Spawn short-lived sparkle trail particles as Lumi moves.
    /// </summary>
    private void UpdateAmbientTrail()
    {
        // Spawn new motes based on velocity
        float speed = currentVelocity.magnitude;
        trailSpawnTimer -= Time.deltaTime;

        if (speed > 0.5f && trailSpawnTimer <= 0f && trailMotes.Count < 20)
        {
            trailSpawnTimer = 0.06f;
            SpawnTrailMote();
        }
        else if (speed <= 0.5f && trailSpawnTimer <= 0f && trailMotes.Count < 20)
        {
            // Occasional idle sparkle
            trailSpawnTimer = Random.Range(0.3f, 0.6f);
            if (Random.value < 0.4f) SpawnTrailMote();
        }

        // Update existing trail motes
        for (int i = trailMotes.Count - 1; i >= 0; i--)
        {
            TrailMote tm = trailMotes[i];
            if (tm.go == null)
            {
                trailMotes.RemoveAt(i);
                continue;
            }

            tm.lifetime += Time.deltaTime;
            if (tm.lifetime >= tm.maxLifetime)
            {
                Destroy(tm.go);
                trailMotes.RemoveAt(i);
                continue;
            }

            // Drift upward and fade
            tm.go.transform.position += tm.velocity * Time.deltaTime;
            tm.velocity *= 0.97f; // gentle drag

            float t = tm.lifetime / tm.maxLifetime;
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.7f; // fade in then out
            float scale = Mathf.Lerp(0.06f, 0.02f, t);
            tm.go.transform.localScale = Vector3.one * scale;

            if (tm.sr != null)
            {
                tm.sr.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, alpha);
            }

            trailMotes[i] = tm;
        }
    }

    private void SpawnTrailMote()
    {
        GameObject moteGO = new GameObject("TrailMote");
        moteGO.transform.position = transform.position + new Vector3(
            Random.Range(-0.2f, 0.2f),
            Random.Range(-0.2f, 0.2f),
            0f
        );
        moteGO.transform.localScale = Vector3.one * 0.06f;

        SpriteRenderer sr = moteGO.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = CreateAdditiveMaterial();
        sr.sprite = CreateMoteSprite(16);
        sr.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.0f);
        sr.sortingOrder = 0;

        if (playerSpriteRenderer != null)
        {
            sr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            sr.sortingOrder = playerSpriteRenderer.sortingOrder;
        }

        TrailMote tm = new TrailMote
        {
            go = moteGO,
            sr = sr,
            lifetime = 0f,
            maxLifetime = Random.Range(0.3f, 0.7f),
            velocity = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0.3f, 1.0f),
                0f
            )
        };

        trailMotes.Add(tm);
    }

    private void UpdateForcefieldShield()
    {
        if (forcefieldShieldGO == null) return;

        if (isShieldActive)
        {
            if (!forcefieldShieldGO.activeSelf) forcefieldShieldGO.SetActive(true);

            forcefieldShieldGO.transform.localScale = Vector3.Lerp(forcefieldShieldGO.transform.localScale, Vector3.one * 2.2f, Time.deltaTime * 14f);
            forcefieldShieldGO.transform.Rotate(Vector3.forward, -40f * Time.deltaTime);

            if (forcefieldRenderer != null)
            {
                float pulse = 0.75f + Mathf.Sin(Time.time * 9f) * 0.15f;
                forcefieldRenderer.color = new Color(colorShield.r, colorShield.g, colorShield.b, pulse);
            }
        }
        else
        {
            if (forcefieldShieldGO.activeSelf)
            {
                forcefieldShieldGO.transform.localScale = Vector3.Lerp(forcefieldShieldGO.transform.localScale, Vector3.zero, Time.deltaTime * 16f);
                if (forcefieldShieldGO.transform.localScale.magnitude < 0.1f)
                {
                    forcefieldShieldGO.SetActive(false);
                }
            }
        }
    }

    private void SetLightRadius(float range, float intensity)
    {
        if (pointLight != null)
        {
            pointLight.range = Mathf.Lerp(pointLight.range, range, Time.deltaTime * 6f);
            pointLight.intensity = Mathf.Lerp(pointLight.intensity, intensity, Time.deltaTime * 6f);
        }
    }

    private void PerformManaDrainHeal()
    {
        if (playerCombat == null || playerHealth == null) return;

        OrbHealMode mode = (OrbInventorySystem.Instance != null) ? OrbInventorySystem.Instance.activeHealMode : OrbHealMode.FullHealAllMP;

        if (playerCombat.currentMana < 20f && mode == OrbHealMode.FullHealAllMP)
        {
            Debug.LogWarning("Lumi: Mana is too low (<20 MP) to perform Full Mana-Drain Heal!");
            return;
        }

        currentState = LightOrbState.ManaDrainHeal;

        int healAmount = 0;
        if (mode == OrbHealMode.FullHealAllMP)
        {
            healAmount = playerHealth.MaxHealth - playerHealth.CurrentHealth;
        }
        else if (mode == OrbHealMode.ProportionalMP)
        {
            healAmount = Mathf.RoundToInt((playerCombat.currentMana / playerCombat.maxMana) * playerHealth.MaxHealth);
        }

        if (healAmount > 0)
        {
            playerHealth.Heal(healAmount);
            playerCombat.currentMana = 0f;
            Debug.Log($"Lumi: Healed Player by {healAmount} HP and drained MP to 0!");

            // Trigger dark orbs gathering at the player
            DarkHealingVFX.TriggerGather(playerTransform, 14);

            if (HUDOrbPanel.Instance != null)
            {
                HUDOrbPanel.Instance.TriggerSplash(OrbType.Health, 1.0f);
                HUDOrbPanel.Instance.TriggerSplash(OrbType.Mana, 1.0f);
            }
        }
    }

    private IEnumerator PlayerHealingGlowRoutine(float duration)
    {
        if (playerTransform == null) yield break;
        DarkHealingVFX.TriggerGather(playerTransform, 14);
        yield return null;
    }

    // Legacy light spear purged

    private void StartBodyTraceAttack()
    {
        nextOrbAttackTime = Time.time + orbAttackCooldown;
        Transform enemyTarget = FindNearestEnemyTarget();
        StartCoroutine(OrbBodyTraceAttackRoutine(enemyTarget));
    }

    private Transform FindNearestEnemyTarget()
    {
        if (playerTransform == null) return null;

        Collider2D[] hitCols = Physics2D.OverlapCircleAll(playerTransform.position, orbAttackMaxDistance);
        Transform closestEnemy = null;
        float minDistance = float.MaxValue;
        Health playerH = (playerHealth != null) ? playerHealth : playerTransform.GetComponent<Health>();

        foreach (var col in hitCols)
        {
            if (col == null) continue;

            // 1. Strict Player exclusion: Skip player gameobject, root, tag, and all child transforms
            if (col.gameObject == playerTransform.gameObject ||
                col.transform.IsChildOf(playerTransform) ||
                col.transform.root == playerTransform.root ||
                col.CompareTag("Player"))
            {
                continue;
            }

            // 2. Must have an IDamageable component (on self or parent)
            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable == null) damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            // 3. Strict check: Skip if the IDamageable target is the player's Health component
            if (playerH != null && damageable is Health h && h == playerH) continue;

            float dist = Vector3.Distance(playerTransform.position, col.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestEnemy = col.transform;
            }
        }

        return closestEnemy;
    }

    private IEnumerator OrbBodyTraceAttackRoutine(Transform enemyTarget)
    {
        isBodyAttacking = true;
        currentState = LightOrbState.BodyTraceAttack;
        TriggerPopBounce(1.6f);

        Vector3 startPos = transform.position;
        Vector3 targetPos;

        if (enemyTarget != null)
        {
            targetPos = enemyTarget.position;
        }
        else
        {
            float facingDir = (playerTransform != null && playerTransform.localScale.x < 0) ? -1.0f : 1.0f;
            targetPos = playerTransform.position + new Vector3(facingDir * 7f, 0.4f, 0f);
        }

        // Enable Trace Line Renderer
        if (attackTraceLineRenderer != null)
        {
            attackTraceLineRenderer.enabled = true;
            attackTraceLineRenderer.SetPosition(0, startPos);
            attackTraceLineRenderer.SetPosition(1, startPos);
            attackTraceLineRenderer.startColor = displayGlowColor;
            attackTraceLineRenderer.endColor = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.1f);
        }

        float distance = Vector3.Distance(startPos, targetPos);
        float duration = Mathf.Clamp(distance / orbAttackSpeed, 0.12f, 0.35f);
        float elapsed = 0f;

        // Phase 1: Fast Body Trace Dash to Target
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float tEase = Mathf.Sin(t * Mathf.PI * 0.5f);

            if (enemyTarget != null)
            {
                targetPos = enemyTarget.position;
            }

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, tEase);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.4f;

            transform.position = currentPos;

            if (attackTraceLineRenderer != null)
            {
                attackTraceLineRenderer.SetPosition(0, startPos);
                attackTraceLineRenderer.SetPosition(1, currentPos);
                attackTraceLineRenderer.startColor = displayGlowColor;
                attackTraceLineRenderer.endColor = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 0.1f);
            }

            if (Random.value < 0.6f) SpawnTrailMote();

            yield return null;
        }

        transform.position = targetPos;

        // Phase 2: Impact & Hit Detection
        IDamageable target = null;
        Health playerH = (playerHealth != null) ? playerHealth : playerTransform.GetComponent<Health>();

        if (enemyTarget != null)
        {
            if (!enemyTarget.IsChildOf(playerTransform) && enemyTarget.root != playerTransform.root && !enemyTarget.CompareTag("Player"))
            {
                target = enemyTarget.GetComponent<IDamageable>();
                if (target == null) target = enemyTarget.GetComponentInParent<IDamageable>();
                if (playerH != null && target is Health hp && hp == playerH) target = null;
            }
        }

        // If no direct target, check overlap at arrival position for any enemy mob
        if (target == null)
        {
            Collider2D[] impactCols = Physics2D.OverlapCircleAll(targetPos, 1.4f);
            foreach (var col in impactCols)
            {
                if (col == null || col.CompareTag("Player") || col.transform.IsChildOf(playerTransform) || col.transform.root == playerTransform.root)
                    continue;

                IDamageable d = col.GetComponent<IDamageable>();
                if (d == null) d = col.GetComponentInParent<IDamageable>();
                if (d != null && (playerH == null || !(d is Health hp && hp == playerH)))
                {
                    target = d;
                    enemyTarget = col.transform;
                    break;
                }
            }
        }

        if (target != null)
        {
            target.TakeDamage(orbAttackDamage);
            Vector3 contactPoint = enemyTarget != null ? enemyTarget.position : targetPos;
            HitFeedbackManager.TriggerHitFeedback(enemyTarget != null ? enemyTarget : transform, contactPoint, orbAttackDamage, true, EnemyHitType.MagicSpell);

            if (enemyTarget != null)
            {
                Rigidbody2D enemyRb = enemyTarget.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    Vector2 knockDir = ((Vector2)targetPos - (Vector2)startPos).normalized;
                    enemyRb.AddForce(knockDir * 8f, ForceMode2D.Impulse);
                }
            }
        }

        // Spawn Impact VFX
        SpawnImpactVFX(targetPos);
        TriggerPopBounce(1.7f);

        yield return new WaitForSeconds(0.04f);

        if (attackTraceLineRenderer != null)
        {
            attackTraceLineRenderer.enabled = false;
        }

        // Phase 3: Smooth return to hover position
        Vector3 returnStartPos = transform.position;
        float returnDuration = 0.18f;
        float returnElapsed = 0f;

        while (returnElapsed < returnDuration)
        {
            returnElapsed += Time.deltaTime;
            float t = returnElapsed / returnDuration;
            float facingDir = (playerTransform != null && playerTransform.localScale.x < 0) ? -1.0f : 1.0f;
            Vector3 currentHoverPos = playerTransform.position + new Vector3(followOffset.x * facingDir, followOffset.y, 0f);

            transform.position = Vector3.Lerp(returnStartPos, currentHoverPos, t * t);
            yield return null;
        }

        isBodyAttacking = false;
        currentState = LightOrbState.IdleFollow;
    }

    private void SpawnImpactVFX(Vector3 hitPosition)
    {
        StartCoroutine(ImpactVFXRoutine(hitPosition));
    }

    private IEnumerator ImpactVFXRoutine(Vector3 hitPosition)
    {
        GameObject ringGO = new GameObject("LumiHitRing");
        ringGO.transform.position = hitPosition;

        SpriteRenderer ringSR = ringGO.AddComponent<SpriteRenderer>();
        ringSR.sharedMaterial = CreateAdditiveMaterial();
        ringSR.sprite = CreateRingSprite(128);
        ringSR.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 1.0f);
        ringSR.sortingOrder = 35;
        if (playerSpriteRenderer != null) ringSR.sortingLayerID = playerSpriteRenderer.sortingLayerID;

        Light flashLight = ringGO.AddComponent<Light>();
        if (flashLight != null)
        {
            flashLight.type = LightType.Point;
            flashLight.range = 7f;
            flashLight.color = displayGlowColor;
            flashLight.intensity = 4.5f;
        }

        int sparkCount = 10;
        List<GameObject> sparks = new List<GameObject>();
        List<Vector3> sparkVelocities = new List<Vector3>();

        Sprite sparkSprite = CreateMoteSprite(32);
        Material additiveMat = CreateAdditiveMaterial();

        for (int i = 0; i < sparkCount; i++)
        {
            GameObject sparkGO = new GameObject("HitSpark_" + i);
            sparkGO.transform.position = hitPosition;
            sparkGO.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);

            SpriteRenderer sparkSR = sparkGO.AddComponent<SpriteRenderer>();
            sparkSR.sharedMaterial = additiveMat;
            sparkSR.sprite = sparkSprite;
            sparkSR.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 1.0f);
            sparkSR.sortingOrder = 36;
            if (playerSpriteRenderer != null) sparkSR.sortingLayerID = playerSpriteRenderer.sortingLayerID;

            float angle = (i / (float)sparkCount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            float speed = Random.Range(4f, 9f);
            Vector3 vel = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);

            sparks.Add(sparkGO);
            sparkVelocities.Add(vel);
        }

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float ringScale = Mathf.Lerp(0.2f, 3.2f, t);
            ringGO.transform.localScale = Vector3.one * ringScale;
            float ringAlpha = (1f - t);
            ringSR.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, ringAlpha);
            if (flashLight != null) flashLight.intensity = (1f - t) * 4.5f;

            for (int i = 0; i < sparks.Count; i++)
            {
                if (sparks[i] == null) continue;
                sparks[i].transform.position += sparkVelocities[i] * Time.deltaTime;
                sparkVelocities[i] *= 0.92f;

                SpriteRenderer sr = sparks[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(displayGlowColor.r, displayGlowColor.g, displayGlowColor.b, 1f - t);
                }
            }

            yield return null;
        }

        Destroy(ringGO);
        foreach (var s in sparks)
        {
            if (s != null) Destroy(s);
        }
    }

    private void OnPlayerTookDamage(int damage)
    {
        if (damage > 0)
        {
            TriggerPopBounce(1.5f);
            panicTimer = 0.5f;
            currentState = LightOrbState.PlayerHitPanic;
        }
    }

    // ========================
    // PROCEDURAL TEXTURE GENERATION
    // ========================

private Sprite CreateSolidCoreSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(1, 1, 1, 0);
        float center = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= 0.85f)
                {
                    float brightness = Mathf.Lerp(1.0f, 0.92f, dist / 0.85f);
                    float angle = Mathf.Atan2(dy, dx);
                    float wave1 = Mathf.Sin(dx * 10f) * Mathf.Cos(dy * 10f);
                    float wave2 = Mathf.Cos(dx * 16f) * Mathf.Sin(dy * 16f);
                    float caustic = (wave1 + wave2) * 0.5f + 0.5f;
                    caustic = Mathf.Pow(caustic, 3f) * 0.12f;

                    float finalB = Mathf.Clamp01(brightness + caustic);
                    tex.SetPixel(x, y, new Color(finalB, finalB, finalB, 1f));
                }
                else if (dist <= 1.0f)
                {
                    float alpha = Mathf.Clamp01((1.0f - dist) / 0.15f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private Sprite CreateCoronaHaloSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(1, 1, 1, 0);
        float center = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= 1.0f)
                {
                    // Base exponential falloff
                    float alpha = Mathf.Clamp01(Mathf.Exp(-dist * 2.5f) * (1.0f - dist));

                    // Add organic radial wisps
                    float angle = Mathf.Atan2(dy, dx);
                    float wisp1 = Mathf.Sin(angle * 5f) * 0.5f + 0.5f;
                    float wisp2 = Mathf.Sin(angle * 8f + 2.3f) * 0.5f + 0.5f;
                    float wisp3 = Mathf.Sin(angle * 13f + 5.1f) * 0.5f + 0.5f;
                    float wispMask = (wisp1 * 0.5f + wisp2 * 0.3f + wisp3 * 0.2f);

                    // Wisps are more visible at mid-range distances
                    float wispRadial = Mathf.Sin(dist * Mathf.PI) * 0.35f;
                    alpha += wispRadial * wispMask;

                    alpha = Mathf.Clamp01(alpha);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    /// <summary>
    /// Creates a swirling nebula texture for the inner plasma layer.
    /// Uses overlapping angular gradient bands to simulate swirling gas.
    /// </summary>
    private Sprite CreateNebulaSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(1, 1, 1, 0);
        float center = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= 1.0f)
                {
                    float angle = Mathf.Atan2(dy, dx);

                    // Swirling bands
                    float swirl1 = Mathf.Sin(angle * 3f + dist * 6f) * 0.5f + 0.5f;
                    float swirl2 = Mathf.Cos(angle * 5f - dist * 4f + 1.7f) * 0.5f + 0.5f;
                    float swirl3 = Mathf.Sin(angle * 7f + dist * 8f + 3.2f) * 0.5f + 0.5f;

                    float nebula = swirl1 * 0.4f + swirl2 * 0.35f + swirl3 * 0.25f;

                    // Radial falloff — strongest at mid-range, fades at center and edge
                    float radialMask = Mathf.Sin(dist * Mathf.PI) * 0.8f;
                    float alpha = nebula * radialMask;

                    // Slight color variation: warmer toward center, cooler toward edges
                    float warmth = Mathf.Lerp(1.0f, 0.85f, dist);

                    alpha = Mathf.Clamp01(alpha);
                    tex.SetPixel(x, y, new Color(warmth, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    /// <summary>
    /// Creates a small soft-glow sprite for floating motes and trail particles.
    /// </summary>
    private Sprite CreateMoteSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1.0f - dist * dist); // Quadratic falloff for soft glow
                alpha *= alpha; // Extra softness
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    /// <summary>
    /// Creates a thin ring sprite for state transition VFX.
    /// </summary>
    private Sprite CreateRingSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(0, 0, 0, 0);
        float center = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;

                // Thin ring between 0.8 and 1.0
                if (dist >= 0.75f && dist <= 1.0f)
                {
                    float ringDist = Mathf.Abs(dist - 0.875f) / 0.125f;
                    float alpha = Mathf.Clamp01(1.0f - ringDist * ringDist);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private Sprite CreateForcefieldDomeSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(0, 0, 0, 0);
        float center = resolution * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                
                if (dist <= 1.0f && dist >= 0.75f)
                {
                    float alpha = Mathf.Sin((dist - 0.75f) / 0.25f * Mathf.PI);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else if (dist < 0.75f)
                {
                    tex.SetPixel(x, y, new Color(1, 1, 1, 0.18f));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }
}
