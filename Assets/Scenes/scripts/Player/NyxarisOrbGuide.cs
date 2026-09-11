using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Nyxaris Orb Guide - Celestial Dark Tutorial Guide Orb with purple star trails,
/// camera boundary tethering, generous personal space, and [ExecuteAlways] Editor Preview.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("Tutorial/Nyxaris Orb Guide")]
public class NyxarisOrbGuide : MonoBehaviour
{
    public static NyxarisOrbGuide Instance { get; private set; }

    public enum GuideState
    {
        FollowPlayer,
        LeadingWaypoint,
        ReturningToPlayer,
        IdleLeadAhead,
        IdleImpatience
    }

    [Header("Current State")]
    public GuideState currentState = GuideState.FollowPlayer;

    [Header("Waypoint Location Hierarchy")]
    [Tooltip("The parent GameObject containing your tutorial location child objects. Auto-finds 'Nyxaris Guide Locations' if unassigned.")]
    public GameObject guideLocationsParent;

    [Tooltip("If true, activates waypoints strictly in order. If false, activates any waypoint when entering its radius.")]
    public bool enforceSequentialOrder = false;

    [Header("Generous Follow & Personal Space")]
    [Tooltip("Offset (X = 2.5, Y = 2.2) to keep the orb comfortably floating to the side and above the player.")]
    public Vector3 followOffset = new Vector3(3.4f, 2.4f, 0f);

    [Tooltip("Minimum distance from player. If player gets closer, the orb repels away to maintain personal space.")]
    public float minPersonalSpace = 2.6f;

    public float smoothSpeed = 4.5f;
    public float returnRushSpeed = 9.0f;
    public float hoverFrequency = 2.8f;
    public float hoverAmplitude = 0.16f;

    [Header("Camera Leash & Viewport Bounds")]
    [Tooltip("Margin from screen edges (0.0 to 0.5). If the orb approaches the camera screen edge, it quickly returns to the player.")]
    [Range(0.05f, 0.35f)]
    public float screenEdgeMargin = 0.12f;

    [Tooltip("Max allowed distance from player before the orb rushes back to the player's side.")]
    public float maxDistanceFromPlayer = 12.0f;

    [Header("Core Artwork Sprite")]
    [Tooltip("Custom artwork sprite for the core. Automatically loads 'Nyxaris_CoreArtwork' from Resources if unassigned.")]
    public Sprite customCoreArtworkSprite;

    [Header("Orb Size Scale")]
    [Tooltip("Overall visual scale of the orb, aura, and motes (default 0.55 for a compact companion size).")]
    [Range(0.2f, 1.5f)]
    public float orbScale = 0.55f;

    [Header("Visual Palette")]
    public Color coreColor = new Color(0.01f, 0.005f, 0.02f, 1f);
    public Color outlineGlowColor = new Color(0.92f, 0.15f, 1f, 0.98f);
    public Color coronaAuraColor = new Color(0.72f, 0.06f, 1f, 0.55f);
    public Color innerCoronaColor = new Color(0.82f, 0.18f, 1f, 0.38f);
    public Color starTrailColor = new Color(0.95f, 0.45f, 1f, 0.95f);
    public Color starTrailColorAlt = new Color(0.70f, 0.20f, 0.98f, 0.90f);
    public Color moteColor = new Color(0.85f, 0.25f, 1f, 0.85f);

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int baseSortingOrder = 50;

    [Header("Idle Impatience Timers")]
    public float idleLeadThreshold = 4.0f;
    public float idleNagThreshold = 8.0f;

    [Header("Sassy Idle Quips")]
    public string[] idleQuips = new string[]
    {
        "Are you taking a nap, mortal?",
        "We don't have all day...",
        "Do I need to carry you through this?",
        "The realm won't save itself while you stand there."
    };

    [Header("Registered Waypoints (Auto-Populated)")]
    public List<NyxarisTutorialWaypoint> waypoints = new List<NyxarisTutorialWaypoint>();
    private NyxarisTutorialWaypoint activeWaypoint = null;
    private int currentSequentialIndex = 0;

    // References
    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private move playerMove;
    private SpriteRenderer playerSpriteRenderer;
    private Camera mainCam;

    // Floating Orb Entity Visuals
    private GameObject orbRootGO;
    public Transform orbTransform;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer outlineRenderer;
    private SpriteRenderer coronaRenderer;
    private SpriteRenderer innerCoronaRenderer;
    private SpriteRenderer innerRaysRenderer;
    private readonly List<Transform> motes = new List<Transform>();
    private readonly List<SpriteRenderer> moteRenderers = new List<SpriteRenderer>();

    // Purple Star Trails
    private struct StarParticle
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector3 position;
        public Vector3 velocity;
        public float lifeTime;
        public float maxLifeTime;
        public float initialScale;
        public float rotationSpeed;
        public float shimmerPhase;
        public int colorVariant;
    }
    private List<StarParticle> starParticles = new List<StarParticle>();
    private const int MAX_STAR_TRAIL_PARTICLES = 35;
    private float starSpawnTimer = 0f;
    private Vector3 lastOrbPos;

    // Speech UI Objects
    private GameObject speechCanvasGO;
    private CanvasGroup speechCanvasGroup;
    private TextMeshProUGUI dialogueTextComp;
    private TextMeshProUGUI promptBadgeComp;
    private Image bubbleBgImage;

    // Internal State
    private float playerIdleTimer = 0f;
    private bool isSpeaking = false;
    private Coroutine typewriterCoroutine;
    private float coronaSpeechFlicker = 0f;
    private AudioSource audioSource;
    private AudioClip speechChirpClip;
    private AudioClip completeChimeClip;

    void OnEnable()
    {
        Instance = this;
        EnsureVisualsExist();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        Instance = this;
        EnsureVisualsExist();

        if (Application.isPlaying)
        {
            BuildStarTrailPool();
            SetupWorldSpaceSpeechBubble();
            SynthesizeAudioClips();
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            FindPlayer();
            mainCam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            FindAndRegisterGuideLocations();

            if (playerTransform != null && orbTransform != null)
            {
                orbTransform.position = playerTransform.position + followOffset;
                lastOrbPos = orbTransform.position;
            }
        }
        else
        {
            FindAndRegisterGuideLocations();
        }
    }

    public void RebuildVisualsInEditMode()
    {
        if (orbRootGO != null)
        {
            if (Application.isPlaying) Destroy(orbRootGO);
            else DestroyImmediate(orbRootGO);
        }
        BuildFloatingOrbVisuals();
        SetupWorldSpaceSpeechBubble();
    }

    private void EnsureVisualsExist()
    {
        if (orbTransform == null)
        {
            Transform existing = transform.Find("Nyxaris_FloatingOrbEntity");
            if (existing != null)
            {
                orbRootGO = existing.gameObject;
                orbTransform = existing;
                LinkExistingVisualComponents();
            }
            else
            {
                BuildFloatingOrbVisuals();
            }
        }
    }

    private void LinkExistingVisualComponents()
    {
        coronaRenderer = orbTransform.Find("CoronaAura")?.GetComponent<SpriteRenderer>();
        innerCoronaRenderer = orbTransform.Find("InnerCoronaRing")?.GetComponent<SpriteRenderer>();
        innerRaysRenderer = orbTransform.Find("InnerStarRays")?.GetComponent<SpriteRenderer>();
        outlineRenderer = orbTransform.Find("PurpleOutline")?.GetComponent<SpriteRenderer>();
        coreRenderer = orbTransform.Find("BlackCore")?.GetComponent<SpriteRenderer>();
        if (coreRenderer != null)
        {
            Sprite coreArt = customCoreArtworkSprite != null ? customCoreArtworkSprite : Resources.Load<Sprite>("Sprites/Nyxaris/Nyxaris_CoreArtwork");
            if (coreArt != null && coreRenderer.sprite != coreArt)
            {
                coreRenderer.sprite = coreArt;
                coreRenderer.color = Color.white;
                coreRenderer.transform.localScale = Vector3.one * 1.0f;
            }
        }

        motes.Clear();
        moteRenderers.Clear();
        for (int i = 0; i < 8; i++)
        {
            Transform m = orbTransform.Find($"Mote_{i}");
            if (m != null)
            {
                motes.Add(m);
                moteRenderers.Add(m.GetComponent<SpriteRenderer>());
            }
        }
    }

    public void FindAndRegisterGuideLocations()
    {
        waypoints.Clear();

        if (guideLocationsParent == null)
        {
            var rootObjs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjs)
            {
                if (IsMatchingHierarchyName(root.name))
                {
                    guideLocationsParent = root;
                    break;
                }
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (IsMatchingHierarchyName(child.name))
                    {
                        guideLocationsParent = child.gameObject;
                        break;
                    }
                }
                if (guideLocationsParent != null) break;
            }
        }

        if (guideLocationsParent == null)
        {
            guideLocationsParent = gameObject;
        }

        if (guideLocationsParent != null)
        {
            Transform p = guideLocationsParent.transform;
            for (int i = 0; i < p.childCount; i++)
            {
                Transform child = p.GetChild(i);
                if (child.name.StartsWith("Nyxaris_FloatingOrb")) continue;

                NyxarisTutorialWaypoint wp = child.GetComponent<NyxarisTutorialWaypoint>();
                if (wp == null)
                {
                    wp = child.gameObject.AddComponent<NyxarisTutorialWaypoint>();
                }
                wp.InferDefaultsFromName();
                waypoints.Add(wp);
            }
        }
    }

    private bool IsMatchingHierarchyName(string name)
    {
        string n = name.Replace(" ", "").Replace("_", "").ToLower();
        return n.Contains("nyxarisguidelocation") || n.Contains("guidelocation") || n == "nyxarisguide";
    }

    void FindPlayer()
    {
        if (move.Instance != null)
        {
            playerTransform = move.Instance.transform;
            playerRb = move.Instance.GetComponent<Rigidbody2D>();
            playerMove = move.Instance;
            playerSpriteRenderer = move.Instance.GetComponentInChildren<SpriteRenderer>();
        }
        else
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null)
            {
                playerTransform = pObj.transform;
                playerRb = pObj.GetComponent<Rigidbody2D>();
                playerMove = pObj.GetComponent<move>();
                playerSpriteRenderer = pObj.GetComponentInChildren<SpriteRenderer>();
            }
        }
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            // Edit-Mode Animation & Preview
            if (orbTransform != null)
            {
                orbTransform.position = transform.position + followOffset + new Vector3(0f, Mathf.Sin(Time.realtimeSinceStartup * hoverFrequency) * hoverAmplitude, 0f);
            }
            UpdateVisualPulsation();
            return;
        }

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        if (mainCam == null)
        {
            mainCam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        }

        // Dismiss dialogue on any player button press
        if (speechCanvasGroup != null && speechCanvasGroup.alpha > 0.05f)
        {
            if (Input.anyKeyDown)
            {
                HideSpeechBubble();
            }
        }

        SyncSortingLayers();
        CheckCameraBoundsAndTether();
        UpdateWaypointChecks();
        UpdateIdleDetection();
        UpdateOrbMovement();
        UpdateVisualPulsation();
        UpdateStarTrails();
    }

    #region Camera Boundary Tethering

    private void CheckCameraBoundsAndTether()
    {
        if (orbTransform == null || playerTransform == null) return;

        float distFromPlayer = Vector2.Distance(orbTransform.position, playerTransform.position);
        bool shouldReturn = distFromPlayer > maxDistanceFromPlayer;

        if (!shouldReturn && mainCam != null)
        {
            Vector3 viewportPos = mainCam.WorldToViewportPoint(orbTransform.position);
            if (viewportPos.z > 0f)
            {
                if (viewportPos.x < screenEdgeMargin || viewportPos.x > (1f - screenEdgeMargin) ||
                    viewportPos.y < screenEdgeMargin || viewportPos.y > (1f - screenEdgeMargin))
                {
                    shouldReturn = true;
                }
            }
        }

        if (shouldReturn && currentState != GuideState.ReturningToPlayer)
        {
            currentState = GuideState.ReturningToPlayer;
            if (orbTransform != null) BurstSpawnStarRing(orbTransform.position, 8);
            if (activeWaypoint != null && !activeWaypoint.isCompleted)
            {
                activeWaypoint = null;
                HideSpeechBubble();
            }
        }
        else if (currentState == GuideState.ReturningToPlayer)
        {
            if (distFromPlayer < 3.2f)
            {
                currentState = GuideState.FollowPlayer;
            }
        }
    }

    #endregion

    #region Waypoint Checks & Activation

    private void UpdateWaypointChecks()
    {
        if (playerTransform == null || waypoints.Count == 0 || currentState == GuideState.ReturningToPlayer) return;

        if (enforceSequentialOrder)
        {
            if (currentSequentialIndex < waypoints.Count)
            {
                var wp = waypoints[currentSequentialIndex];
                if (wp != null && !wp.isCompleted)
                {
                    if (wp.IsPlayerInRange(playerTransform.position))
                    {
                        if (activeWaypoint != wp)
                        {
                            ActivateWaypoint(wp);
                        }
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                if (wp == null || wp.isCompleted) continue;

                if (wp.IsPlayerInRange(playerTransform.position))
                {
                    if (activeWaypoint != wp)
                    {
                        ActivateWaypoint(wp);
                    }
                    break;
                }
            }
        }

        if (activeWaypoint != null)
        {
            if (activeWaypoint.isCompleted || activeWaypoint.CheckActionCompleted(playerRb, playerMove))
            {
                CompleteActiveWaypoint();
            }
        }
    }

    private void ActivateWaypoint(NyxarisTutorialWaypoint wp)
    {
        activeWaypoint = wp;
        wp.hasTriggered = true;
        wp.onWaypointActivated?.Invoke();
        currentState = GuideState.LeadingWaypoint;

        string expr = !string.IsNullOrEmpty(wp.expressionAnimationKey) ? wp.expressionAnimationKey : "explaining";
        ShowDialogue(wp.dialogueText, wp.promptBadgeText, expr);
    }

    private void CompleteActiveWaypoint()
    {
        if (activeWaypoint == null) return;
        activeWaypoint.MarkCompleted();

        if (completeChimeClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(completeChimeClip, 0.75f);
        }

        if (promptBadgeComp != null)
        {
            promptBadgeComp.text = "<color=#55FF88>COMPLETED!</color>";
        }

        StartCoroutine(DelayedFadeOutSpeech(1.2f));
        activeWaypoint = null;
        currentSequentialIndex++;
        currentState = GuideState.FollowPlayer;
    }

    #endregion

    #region Movement, Personal Space & States

    private void UpdateIdleDetection()
    {
        float hInput = Mathf.Abs(Input.GetAxisRaw("Horizontal"));
        float vInput = Mathf.Abs(Input.GetAxisRaw("Vertical"));
        float hVel = playerRb != null ? Mathf.Abs(playerRb.linearVelocity.x) : 0f;

        bool playerIsMoving = hInput > 0.1f || vInput > 0.1f || hVel > 0.3f;

        if (playerIsMoving)
        {
            playerIdleTimer = 0f;
            if (currentState == GuideState.IdleLeadAhead || currentState == GuideState.IdleImpatience)
            {
                currentState = (activeWaypoint != null && !activeWaypoint.isCompleted) ? GuideState.LeadingWaypoint : GuideState.FollowPlayer;
                if (!isSpeaking) HideSpeechBubble();
            }
        }
        else
        {
            playerIdleTimer += Time.deltaTime;

            if (playerIdleTimer >= idleNagThreshold && currentState != GuideState.IdleImpatience)
            {
                currentState = GuideState.IdleImpatience;
                TriggerSassyIdleNag();
            }
            else if (playerIdleTimer >= idleLeadThreshold && currentState == GuideState.FollowPlayer)
            {
                currentState = GuideState.IdleLeadAhead;
            }
        }
    }

    private void TriggerSassyIdleNag()
    {
        if (idleQuips == null || idleQuips.Length == 0) return;
        string quip = idleQuips[Random.Range(0, idleQuips.Length)];
        ShowDialogue(quip, "Press [A] / [D] to move", "cutely_annoyed");
    }

    private void UpdateOrbMovement()
    {
        if (orbTransform == null) return;

        float facing = 1f;
        if (playerTransform.localScale.x < 0f || (playerMove != null && playerMove.LastFacingSign < 0f))
        {
            facing = -1f;
        }

        Vector3 targetPos;
        float currentLerpSpeed = smoothSpeed;

        switch (currentState)
        {
            case GuideState.LeadingWaypoint:
                if (activeWaypoint != null)
                {
                    targetPos = activeWaypoint.TargetLeadPosition;
                }
                else
                {
                    targetPos = playerTransform.position + new Vector3(followOffset.x * facing, followOffset.y, 0f);
                }
                break;

            case GuideState.ReturningToPlayer:
                targetPos = playerTransform.position + new Vector3(followOffset.x * facing, followOffset.y, 0f);
                currentLerpSpeed = returnRushSpeed;
                break;

            case GuideState.IdleLeadAhead:
                targetPos = playerTransform.position + new Vector3(3.2f * facing, followOffset.y + 0.4f, 0f);
                break;

            case GuideState.IdleImpatience:
                targetPos = playerTransform.position + new Vector3(1.8f * facing, followOffset.y, 0f);
                break;

            case GuideState.FollowPlayer:
            default:
                targetPos = playerTransform.position + new Vector3(followOffset.x * facing, followOffset.y, 0f);
                break;
        }

        if (currentState != GuideState.ReturningToPlayer)
        {
            Vector2 deltaToPlayer = (Vector2)targetPos - (Vector2)playerTransform.position;
            float currentDist = deltaToPlayer.magnitude;
            if (currentDist < minPersonalSpace && currentDist > 0.01f)
            {
                Vector2 pushDir = deltaToPlayer.normalized;
                targetPos = playerTransform.position + (Vector3)(pushDir * minPersonalSpace);
                targetPos.y = Mathf.Max(targetPos.y, playerTransform.position.y + followOffset.y * 0.85f);
            }
        }

        float freq = (currentState == GuideState.IdleImpatience) ? hoverFrequency * 2.2f : hoverFrequency;
        float amp = (currentState == GuideState.IdleImpatience) ? hoverAmplitude * 1.5f : hoverAmplitude;
        targetPos.y += Mathf.Sin(Time.time * freq) * amp;
        targetPos.z = 0f;

        orbTransform.position = Vector3.Lerp(orbTransform.position, targetPos, currentLerpSpeed * Time.deltaTime);

        if (speechCanvasGO != null)
        {
            speechCanvasGO.transform.position = orbTransform.position + new Vector3(0f, 0.75f + orbScale * 0.9f, 0f);
        }
    }

    private void UpdateVisualPulsation()
    {
        float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

        float speechBonus = 0f;
        if (isSpeaking)
        {
            coronaSpeechFlicker = Mathf.PingPong(t * 24f, 0.55f) + Random.Range(0f, 0.25f);
            speechBonus = coronaSpeechFlicker;
        }

        float pulse = 1f + Mathf.Sin(t * 3.8f) * 0.06f;
        if (coreRenderer != null) coreRenderer.transform.localScale = Vector3.one * (0.85f * pulse * orbScale);

        if (outlineRenderer != null)
        {
            float outlineScale = (1.22f + speechBonus * 0.45f) * pulse * orbScale;
            outlineRenderer.transform.localScale = Vector3.one * outlineScale;
            outlineRenderer.color = Color.Lerp(outlineGlowColor, Color.white, speechBonus * 0.35f);
        }

        if (innerCoronaRenderer != null)
        {
            float innerPulse = (1.55f + Mathf.Sin(t * 4.2f + 1.1f) * 0.12f + speechBonus * 0.35f) * orbScale;
            innerCoronaRenderer.transform.localScale = Vector3.one * innerPulse;
            innerCoronaRenderer.transform.Rotate(0f, 0f, 12f * dt);
            Color ic = innerCoronaColor;
            ic.a = 0.35f + Mathf.Sin(t * 5.5f) * 0.1f + speechBonus * 0.2f;
            innerCoronaRenderer.color = ic;
        }

        if (innerRaysRenderer != null)
        {
            innerRaysRenderer.transform.localScale = Vector3.one * ((1.6f + speechBonus * 0.6f) * orbScale);
            innerRaysRenderer.transform.Rotate(0f, 0f, -45f * dt);
        }

        if (coronaRenderer != null)
        {
            float baseScale = 2.4f + speechBonus * 0.8f;
            coronaRenderer.transform.localScale = Vector3.one * ((baseScale + Mathf.Sin(t * 2.5f) * 0.18f) * orbScale);
            coronaRenderer.transform.Rotate(0f, 0f, (isSpeaking ? 90f : 24f) * dt);
        }

        for (int i = 0; i < motes.Count; i++)
        {
            if (motes[i] == null) continue;
            float angle = t * (1.4f + i * 0.16f) + (i * Mathf.PI * 2f / motes.Count);
            float radius = (0.68f + Mathf.Sin(t * 2.8f + i) * 0.12f) * orbScale;
            motes[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            motes[i].localScale = Vector3.one * ((0.14f + (i % 4) * 0.035f) * orbScale);

            if (i < moteRenderers.Count && moteRenderers[i] != null)
            {
                Color col = moteRenderers[i].color;
                col.a = 0.45f + Mathf.Sin(t * 3.5f + i) * 0.25f + (isSpeaking ? 0.4f : 0f);
                moteRenderers[i].color = col;
            }
        }
    }

    #endregion

    #region Purple Star Trails System

    private Vector3 orbVelocitySmoothed = Vector3.zero;

    private void BuildStarTrailPool()
    {
        Sprite starSprite = CreateStarSprite(48);

        for (int i = 0; i < MAX_STAR_TRAIL_PARTICLES; i++)
        {
            GameObject starGO = new GameObject($"PurpleStar_{i}");
            starGO.transform.SetParent(transform, false);
            SpriteRenderer sr = starGO.AddComponent<SpriteRenderer>();
            sr.sprite = starSprite;
            sr.color = starTrailColor;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder - 1;
            starGO.SetActive(false);

            StarParticle p = new StarParticle
            {
                go = starGO,
                sr = sr,
                position = Vector3.zero,
                velocity = Vector3.zero,
                lifeTime = 0f,
                maxLifeTime = 1.0f,
                initialScale = 0.4f,
                rotationSpeed = 120f,
                shimmerPhase = Random.Range(0f, Mathf.PI * 2f),
                colorVariant = (i % 3 == 0) ? 1 : 0
            };
            starParticles.Add(p);
        }
    }

    private void UpdateStarTrails()
    {
        if (orbTransform == null) return;

        Vector3 currentPos = orbTransform.position;
        Vector3 frameDelta = currentPos - lastOrbPos;
        float moveDist = frameDelta.magnitude;

        orbVelocitySmoothed = Vector3.Lerp(orbVelocitySmoothed, frameDelta / Mathf.Max(Time.deltaTime, 0.001f), 6f * Time.deltaTime);
        lastOrbPos = currentPos;

        starSpawnTimer -= Time.deltaTime;
        bool isReturning = currentState == GuideState.ReturningToPlayer;
        bool shouldSpawn = moveDist > 0.02f || isReturning || isSpeaking;

        if (shouldSpawn && starSpawnTimer <= 0f)
        {
            int spawnCount = isReturning ? 3 : 1;
            for (int s = 0; s < spawnCount; s++)
            {
                SpawnStarParticle(currentPos, isReturning);
            }
            starSpawnTimer = isReturning ? 0.018f : (moveDist > 0.06f ? 0.04f : 0.07f);
        }

        if (!shouldSpawn && Random.value < 0.012f)
        {
            SpawnStarParticle(currentPos, false);
        }

        for (int i = 0; i < starParticles.Count; i++)
        {
            StarParticle p = starParticles[i];
            if (!p.go.activeSelf) continue;

            p.lifeTime += Time.deltaTime;
            if (p.lifeTime >= p.maxLifeTime)
            {
                p.go.SetActive(false);
                starParticles[i] = p;
                continue;
            }

            float t = p.lifeTime / p.maxLifeTime;
            p.velocity *= (1f - 1.8f * Time.deltaTime);
            p.position += p.velocity * Time.deltaTime;
            p.go.transform.position = p.position;
            p.go.transform.Rotate(0f, 0f, p.rotationSpeed * Time.deltaTime);

            float scaleCurve = Mathf.Sin(t * Mathf.PI);
            float scale = p.initialScale * Mathf.Lerp(0.6f, 1f, scaleCurve) * (1f - Mathf.Pow(t, 2.2f));
            p.go.transform.localScale = Vector3.one * Mathf.Max(scale, 0f);

            Color baseCol = (p.colorVariant == 1) ? starTrailColorAlt : starTrailColor;
            float shimmer = Mathf.Sin(Time.time * 8f + p.shimmerPhase) * 0.5f + 0.5f;
            Color c = Color.Lerp(baseCol, Color.white, shimmer * 0.25f);
            c.a = Mathf.Lerp(0.95f, 0f, Mathf.Pow(t, 1.6f));
            p.sr.color = c;

            starParticles[i] = p;
        }
    }

    private void SpawnStarParticle(Vector3 spawnOrigin, bool isBurst)
    {
        for (int i = 0; i < starParticles.Count; i++)
        {
            StarParticle p = starParticles[i];
            if (!p.go.activeSelf)
            {
                float spread = isBurst ? 0.7f : 0.35f;
                Vector2 randomDrift = Random.insideUnitCircle * spread;
                p.position = spawnOrigin + (Vector3)randomDrift;

                Vector3 inherited = -orbVelocitySmoothed * Random.Range(0.15f, 0.35f);
                Vector3 scatter = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.1f, 0.6f), 0f);
                p.velocity = inherited + scatter;

                if (isBurst)
                {
                    p.velocity += (Vector3)(Random.insideUnitCircle * 1.8f);
                }

                p.lifeTime = 0f;
                p.maxLifeTime = isBurst ? Random.Range(0.55f, 1.1f) : Random.Range(0.6f, 1.2f);
                p.initialScale = (isBurst ? Random.Range(0.32f, 0.55f) : Random.Range(0.22f, 0.42f)) * (0.6f + orbScale * 0.4f);
                p.rotationSpeed = Random.Range(-200f, 200f);
                p.shimmerPhase = Random.Range(0f, Mathf.PI * 2f);
                p.colorVariant = Random.value < 0.35f ? 1 : 0;

                p.go.transform.position = p.position;
                p.go.transform.localScale = Vector3.one * p.initialScale;
                p.go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                p.sr.color = (p.colorVariant == 1) ? starTrailColorAlt : starTrailColor;
                p.go.SetActive(true);

                starParticles[i] = p;
                return;
            }
        }
    }

    public void BurstSpawnStarRing(Vector3 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnStarParticle(center, true);
        }
    }

    #endregion

    #region Speech Bubble & Typewriter UI

    public void ShowDialogue(string text, string promptBadge, string expressionKey = "")
    {
        if (speechCanvasGroup == null) return;

        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterRoutine(text, promptBadge));
    }

    public void HideSpeechBubble()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        isSpeaking = false;
        StartCoroutine(FadeCanvasGroup(speechCanvasGroup, 0f, 0.25f));
    }

    private IEnumerator TypewriterRoutine(string text, string promptBadge)
    {
        isSpeaking = true;
        speechCanvasGroup.alpha = 1.0f;
        speechCanvasGO.SetActive(true);

        dialogueTextComp.text = "";
        if (promptBadgeComp != null)
        {
            promptBadgeComp.text = !string.IsNullOrEmpty(promptBadge) ? $"<color=#D47BFF>► {promptBadge}</color>" : "";
            promptBadgeComp.gameObject.SetActive(!string.IsNullOrEmpty(promptBadge));
        }

        for (int i = 0; i < text.Length; i++)
        {
            dialogueTextComp.text += text[i];

            if (char.IsLetterOrDigit(text[i]) && audioSource != null && speechChirpClip != null && (i % 2 == 0))
            {
                audioSource.pitch = Random.Range(1.35f, 1.75f);
                audioSource.PlayOneShot(speechChirpClip, 0.5f);
            }

            yield return new WaitForSecondsRealtime(0.028f);
        }

        isSpeaking = false;
    }

    private IEnumerator DelayedFadeOutSpeech(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        yield return FadeCanvasGroup(speechCanvasGroup, 0f, 0.35f);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        if (cg == null) yield break;
        float startAlpha = cg.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
    }

    #endregion

    #region Visual & UI Setup

    private void SyncSortingLayers()
    {
        if (playerSpriteRenderer != null)
        {
            sortingLayerName = playerSpriteRenderer.sortingLayerName;
            int order = playerSpriteRenderer.sortingOrder + 10;

            if (coronaRenderer != null) { coronaRenderer.sortingLayerName = sortingLayerName; coronaRenderer.sortingOrder = order; }
            if (innerCoronaRenderer != null) { innerCoronaRenderer.sortingLayerName = sortingLayerName; innerCoronaRenderer.sortingOrder = order + 1; }
            if (innerRaysRenderer != null) { innerRaysRenderer.sortingLayerName = sortingLayerName; innerRaysRenderer.sortingOrder = order + 2; }
            if (outlineRenderer != null) { outlineRenderer.sortingLayerName = sortingLayerName; outlineRenderer.sortingOrder = order + 3; }
            if (coreRenderer != null) { coreRenderer.sortingLayerName = sortingLayerName; coreRenderer.sortingOrder = order + 4; }
            for (int i = 0; i < moteRenderers.Count; i++)
            {
                if (moteRenderers[i] != null) { moteRenderers[i].sortingLayerName = sortingLayerName; moteRenderers[i].sortingOrder = order + 5; }
            }
            for (int i = 0; i < starParticles.Count; i++)
            {
                if (starParticles[i].sr != null) { starParticles[i].sr.sortingLayerName = sortingLayerName; starParticles[i].sr.sortingOrder = order - 1; }
            }
        }
    }

    private void BuildFloatingOrbVisuals()
    {
        orbRootGO = new GameObject("Nyxaris_FloatingOrbEntity");
        orbTransform = orbRootGO.transform;
        orbTransform.SetParent(transform, false);
        orbTransform.localPosition = followOffset;

        Sprite circleSprite = CreateCircleSprite(128);
        Sprite softAuraSprite = CreateAuraSprite(128);
        Sprite starRaysSprite = CreateStarRaysSprite(128);

        // 1. Corona (Wide soft purple aura)
        GameObject corona = new GameObject("CoronaAura");
        corona.transform.SetParent(orbTransform, false);
        coronaRenderer = corona.AddComponent<SpriteRenderer>();
        coronaRenderer.sprite = softAuraSprite;
        coronaRenderer.color = coronaAuraColor;
        coronaRenderer.sortingLayerName = sortingLayerName;
        coronaRenderer.sortingOrder = baseSortingOrder;
        corona.transform.localScale = Vector3.one * 2.4f;

        // 2. Inner Corona Ring (Layered purple depth)
        GameObject innerCorona = new GameObject("InnerCoronaRing");
        innerCorona.transform.SetParent(orbTransform, false);
        innerCoronaRenderer = innerCorona.AddComponent<SpriteRenderer>();
        innerCoronaRenderer.sprite = softAuraSprite;
        innerCoronaRenderer.color = innerCoronaColor;
        innerCoronaRenderer.sortingLayerName = sortingLayerName;
        innerCoronaRenderer.sortingOrder = baseSortingOrder + 1;
        innerCorona.transform.localScale = Vector3.one * 1.55f;

        // 3. Inner Star Rays (Cosmic rotating diamond flare)
        GameObject rays = new GameObject("InnerStarRays");
        rays.transform.SetParent(orbTransform, false);
        innerRaysRenderer = rays.AddComponent<SpriteRenderer>();
        innerRaysRenderer.sprite = starRaysSprite;
        innerRaysRenderer.color = new Color(outlineGlowColor.r, outlineGlowColor.g, outlineGlowColor.b, 0.45f);
        innerRaysRenderer.sortingLayerName = sortingLayerName;
        innerRaysRenderer.sortingOrder = baseSortingOrder + 2;
        innerRaysRenderer.transform.localScale = Vector3.one * 1.6f;

        // 4. Outline (Sharp vibrant neon purple rim)
        GameObject outline = new GameObject("PurpleOutline");
        outline.transform.SetParent(orbTransform, false);
        outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = circleSprite;
        outlineRenderer.color = outlineGlowColor;
        outlineRenderer.sortingLayerName = sortingLayerName;
        outlineRenderer.sortingOrder = baseSortingOrder + 3;
        outline.transform.localScale = Vector3.one * 1.22f;

        // 5. Core (Custom Artwork or Black Void Core)
        GameObject core = new GameObject("BlackCore");
        core.transform.SetParent(orbTransform, false);
        coreRenderer = core.AddComponent<SpriteRenderer>();

        Sprite coreArt = customCoreArtworkSprite != null ? customCoreArtworkSprite : Resources.Load<Sprite>("Sprites/Nyxaris/Nyxaris_CoreArtwork");
        if (coreArt != null)
        {
            coreRenderer.sprite = coreArt;
            coreRenderer.color = Color.white; // Preserve full vibrant artwork colors
            core.transform.localScale = Vector3.one * 1.0f;
        }
        else
        {
            coreRenderer.sprite = circleSprite;
            coreRenderer.color = coreColor;
            core.transform.localScale = Vector3.one * 0.85f;
        }
        coreRenderer.sortingLayerName = sortingLayerName;
        coreRenderer.sortingOrder = baseSortingOrder + 4;

        // 6. Orbiting Void Motes (8 for denser cosmic feel)
        for (int i = 0; i < 8; i++)
        {
            GameObject mote = new GameObject($"Mote_{i}");
            mote.transform.SetParent(orbTransform, false);
            SpriteRenderer r = mote.AddComponent<SpriteRenderer>();
            r.sprite = softAuraSprite;
            r.color = (i % 2 == 0) ? moteColor : new Color(starTrailColorAlt.r, starTrailColorAlt.g, starTrailColorAlt.b, 0.7f);
            r.sortingLayerName = sortingLayerName;
            r.sortingOrder = baseSortingOrder + 5;
            mote.transform.localScale = Vector3.one * (0.14f + (i % 4) * 0.035f);

            motes.Add(mote.transform);
            moteRenderers.Add(r);
        }
    }

    private void SetupWorldSpaceSpeechBubble()
    {
        if (speechCanvasGO != null) return;

        speechCanvasGO = new GameObject("Nyxaris_SpeechCanvas");
        if (orbTransform != null) speechCanvasGO.transform.SetParent(orbTransform, false);

        Canvas canvas = speechCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 250;

        speechCanvasGO.AddComponent<CanvasScaler>();
        speechCanvasGroup = speechCanvasGO.AddComponent<CanvasGroup>();
        speechCanvasGroup.alpha = 0f;

        RectTransform canvasRT = speechCanvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(6.5f, 3.5f);
        canvasRT.localScale = Vector3.one * 0.022f;

        // Bubble Background Box
        GameObject bgGO = new GameObject("BubbleBackground");
        bgGO.transform.SetParent(speechCanvasGO.transform, false);
        bubbleBgImage = bgGO.AddComponent<Image>();
        bubbleBgImage.color = new Color(0.06f, 0.015f, 0.12f, 0.88f); // Rich glassmorphic obsidian
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(400f, 150f);

        Outline outline = bgGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.25f, 1.0f, 0.9f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        // Dialogue Text
        GameObject textGO = new GameObject("DialogueText");
        textGO.transform.SetParent(bgGO.transform, false);
        dialogueTextComp = textGO.AddComponent<TextMeshProUGUI>();
        dialogueTextComp.fontSize = 24f;
        dialogueTextComp.color = Color.white;
        dialogueTextComp.alignment = TextAlignmentOptions.TopLeft;
        dialogueTextComp.textWrappingMode = TextWrappingModes.Normal;
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.sizeDelta = new Vector2(370f, 85f);
        textRT.anchoredPosition = new Vector2(0f, 18f);

        // Prompt Badge Text
        GameObject promptGO = new GameObject("PromptBadge");
        promptGO.transform.SetParent(bgGO.transform, false);
        promptBadgeComp = promptGO.AddComponent<TextMeshProUGUI>();
        promptBadgeComp.fontSize = 21f;
        promptBadgeComp.fontStyle = FontStyles.Bold;
        promptBadgeComp.color = new Color(0.92f, 0.65f, 1f, 1f);
        promptBadgeComp.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.sizeDelta = new Vector2(370f, 34f);
        promptRT.anchoredPosition = new Vector2(0f, -46f);
    }

    private void SynthesizeAudioClips()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        int rate = 44100;
        int chirpSamples = Mathf.FloorToInt(rate * 0.045f);
        float[] cSamples = new float[chirpSamples];
        for (int i = 0; i < chirpSamples; i++)
        {
            float t = (float)i / chirpSamples;
            float freq = Mathf.Lerp(540f, 390f, t);
            float env = Mathf.Sin(t * Mathf.PI);
            cSamples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)rate)) * env * 0.4f;
        }
        speechChirpClip = AudioClip.Create("Nyxaris_VoiceChirp", chirpSamples, 1, rate, false);
        speechChirpClip.SetData(cSamples, 0);

        int chimeSamples = Mathf.FloorToInt(rate * 0.35f);
        float[] chSamples = new float[chimeSamples];
        for (int i = 0; i < chimeSamples; i++)
        {
            float t = (float)i / chimeSamples;
            float env = Mathf.Exp(-t * 6f);
            float wave1 = Mathf.Sin(2f * Mathf.PI * 659.25f * (i / (float)rate));
            float wave2 = Mathf.Sin(2f * Mathf.PI * 987.77f * (i / (float)rate));
            float wave3 = Mathf.Sin(2f * Mathf.PI * 1318.5f * (i / (float)rate));
            chSamples[i] = (wave1 * 0.4f + wave2 * 0.35f + wave3 * 0.25f) * env * 0.6f;
        }
        completeChimeClip = AudioClip.Create("Nyxaris_CompleteChime", chimeSamples, 1, rate, false);
        completeChimeClip.SetData(chSamples, 0);
    }

    #endregion

    #region Procedural Sprite Generators

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - d) / 1.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateAuraSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - (d / radius)), 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateStarRaysSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 diff = new Vector2(x, y) - center;
                float d = diff.magnitude / radius;
                if (d >= 1f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                float angle = Mathf.Atan2(diff.y, diff.x);
                float rayCross = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 2f)), 6f);
                float rayDiagonal = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 14f) * 0.6f;
                float alpha = Mathf.Clamp01((rayCross + rayDiagonal) * (1f - d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateStarSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = (new Vector2(x, y) - center) / radius;
                float dist = p.magnitude;

                float starShape = Mathf.Sqrt(Mathf.Abs(p.x)) + Mathf.Sqrt(Mathf.Abs(p.y));
                float starAlpha = Mathf.Clamp01(1f - starShape * 0.88f);
                starAlpha = Mathf.Pow(starAlpha, 1.5f);

                float haloAlpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 3.0f) * 0.45f;

                float alpha = Mathf.Clamp01(starAlpha + haloAlpha);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    #endregion

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 previewPos = orbTransform != null ? orbTransform.position : (transform.position + followOffset);

        Gizmos.color = new Color(0.85f, 0.15f, 1.0f, 0.65f);
        Gizmos.DrawWireSphere(previewPos, 0.65f);

        Gizmos.color = new Color(0.1f, 0.05f, 0.2f, 0.9f);
        Gizmos.DrawSphere(previewPos, 0.35f);

        UnityEditor.Handles.Label(previewPos + Vector3.up * 0.85f, "★ Nyxaris Guide Orb");

        if (guideLocationsParent != null)
        {
            Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.35f);
            foreach (var wp in waypoints)
            {
                if (wp != null)
                {
                    Gizmos.DrawLine(previewPos, wp.transform.position);
                }
            }
        }
    }
#endif

    /// <summary>
    /// Smoothly morphs the Dark Orb into Fox Nyxaris with an explosive purple shadow burst.
    /// </summary>
    private bool isMorphingIntoFox = false;

    public void MorphIntoFoxForm()
    {
        if (isMorphingIntoFox) return;
        isMorphingIntoFox = true;
        StartCoroutine(MorphIntoFoxRoutine());
    }

    private IEnumerator MorphIntoFoxRoutine()
    {
        ShowDialogue("Impressive blade work, mortal. Now follow my lead on four paws!", "");

        // 1. Descend toward ground
        float t = 0f;
        Vector3 startP = orbTransform != null ? orbTransform.position : transform.position;
        Vector3 groundTarget = startP - new Vector3(0f, 1.2f, 0f);

        while (t < 0.6f)
        {
            t += Time.deltaTime;
            if (orbTransform != null)
            {
                orbTransform.position = Vector3.Lerp(startP, groundTarget, t / 0.6f);
            }
            yield return null;
        }

        // 2. Spawn Purple Shadow Burst FX
        BurstSpawnStarRing(groundTarget, 16);

        // 3. Completely shut down Orb entity & guide while Fox is active
        if (orbRootGO != null) orbRootGO.SetActive(false);
        HideSpeechBubble();
        var guideRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in guideRenderers) if (r != null) r.enabled = false;
        if (speechCanvasGO != null) speechCanvasGO.SetActive(false);
        enabled = false; // Disable update/waypoint processing completely

        // 4. Instantiate Fox Form Companion (from Prefab or procedural)
        GameObject foxPrefab = Resources.Load<GameObject>("Prefabs/FoxNyxaris_Companion");
        GameObject foxGO;
        FoxNyxarisController fox;

        if (foxPrefab != null)
        {
            foxGO = Object.Instantiate(foxPrefab, groundTarget, Quaternion.identity);
            foxGO.name = "FoxNyxaris_Companion";
            fox = foxGO.GetComponent<FoxNyxarisController>();
        }
        else
        {
            foxGO = new GameObject("FoxNyxaris_Companion");
            foxGO.transform.position = groundTarget;
            SpriteRenderer foxSr = foxGO.AddComponent<SpriteRenderer>();
            Rigidbody2D foxRb = foxGO.AddComponent<Rigidbody2D>();
            CapsuleCollider2D foxCol = foxGO.AddComponent<CapsuleCollider2D>();
            fox = foxGO.AddComponent<FoxNyxarisController>();
        }

        yield return new WaitForSeconds(0.4f);
        if (fox != null)
        {
            fox.ShowDialogue("Much better. Keep up, mortal!", "");
        }
    }


    public void ActivateAirborneOrb(Vector3 startPos)
    {
        if (orbTransform != null)
        {
            orbTransform.position = startPos;
            if (orbRootGO != null) orbRootGO.SetActive(true);
        }
        BurstSpawnStarRing(startPos, 8);
    }

    public void DeactivateAirborneOrb()
    {
        if (orbTransform != null)
        {
            BurstSpawnStarRing(orbTransform.position, 8);
            if (orbRootGO != null) orbRootGO.SetActive(false);
        }
    }

}
