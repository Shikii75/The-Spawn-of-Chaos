using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DynamicPinkBridgeCluster - Master manager script for platform clusters (e.g. TutorialScene).
/// Automatically discovers all child platform objects and constructs a dynamic, glowing pink bridge 
/// that scales up in front of the player and retracts behind them as they walk across.
/// Features one-way effector colliders, spring-bounce scale assembly, neon pink color tinting, and star particle bursts.
/// </summary>
public class DynamicPinkBridgeCluster : MonoBehaviour
{
    [Header("Proximity & Range Settings")]
    [Tooltip("Distance from player at which child platforms begin assembling.")]
    public float buildDistance = 6.5f;

    [Tooltip("Distance from player at which child platforms begin retracting.")]
    public float retractDistance = 8.5f;

    [Header("Animation Easing")]
    public float assembleSpeed = 12f;
    public float retractSpeed = 8f;

    [Tooltip("Spring overshoot factor when platform scales up (1.15 = 15% bounce).")]
    public float scaleBounceFactor = 1.15f;

    [Header("Neon Pink Visuals")]
    public Color activePinkColor = new Color(1f, 0.25f, 0.75f, 1f); // Neon Pink #FF40BF
    public bool enableStarParticles = true;
    public int starParticleCount = 14;

    [Header("Player Target Override")]
    public Transform playerTransform;

    private class BridgePieceData
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer spriteRenderer;
        public Collider2D collider;
        public PlatformEffector2D effector;
        public Vector3 originalLocalScale;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
        public Color originalColor;

        public float currentProgress; // 0 = retracted, 1 = fully assembled
        public bool particleBurstFired;
    }

    private List<BridgePieceData> pieces = new List<BridgePieceData>();

    void Awake()
    {
        InitializeChildPlatforms();
    }

    void Start()
    {
        FindPlayer();
    }

    public void InitializeChildPlatforms()
    {
        pieces.Clear();

        foreach (Transform child in transform)
        {
            // Skip containers or non-renderable helper objects if any
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            Collider2D col = child.GetComponent<Collider2D>();

            if (sr == null && col == null) continue;

            BridgePieceData piece = new BridgePieceData
            {
                gameObject = child.gameObject,
                transform = child,
                spriteRenderer = sr,
                collider = col,
                originalLocalScale = child.localScale,
                originalLocalPos = child.localPosition,
                originalLocalRot = child.localRotation,
                originalColor = sr != null ? sr.color : Color.white,
                currentProgress = 0f,
                particleBurstFired = false
            };

            // Ensure Rigidbody2D is kinematic for safe platform movement/physics
            Rigidbody2D rb = child.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = child.gameObject.AddComponent<Rigidbody2D>();
            }
            rb.isKinematic = true;
            rb.simulated = true;

            // Configure One-Way Platform Effector if collider exists
            if (col != null)
            {
                col.usedByEffector = true;
                PlatformEffector2D effector = child.GetComponent<PlatformEffector2D>();
                if (effector == null)
                {
                    effector = child.gameObject.AddComponent<PlatformEffector2D>();
                }
                effector.useOneWay = true;
                effector.useSideFriction = false;
                effector.surfaceArc = 160f;
                piece.effector = effector;

                // Initially disable collider until platform scales up
                col.enabled = false;
            }

            // Set initial scale to zero (retracted state)
            child.localScale = Vector3.zero;
            if (sr != null)
            {
                sr.color = new Color(activePinkColor.r * 0.3f, activePinkColor.g * 0.3f, activePinkColor.b * 0.3f, 0f);
            }

            pieces.Add(piece);
        }

        Debug.Log($"[DynamicPinkBridgeCluster] Initialized {pieces.Count} child platforms on '{gameObject.name}'.");
    }

    private void FindPlayer()
    {
        if (playerTransform != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Fallback: search by name
            player = GameObject.Find("Player") ?? GameObject.Find("PlayerMage") ?? GameObject.Find("Mage");
        }

        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        Vector3 playerPos = playerTransform.position;

        foreach (var piece in pieces)
        {
            if (piece.transform == null) continue;

            // Calculate distance to player (prioritize horizontal & 2D distance)
            float distance = Vector2.Distance(piece.transform.position, playerPos);

            float targetProgress = 0f;
            if (distance <= buildDistance)
            {
                targetProgress = 1f;
            }
            else if (distance > retractDistance)
            {
                targetProgress = 0f;
            }
            else
            {
                // Smooth transition band between buildDistance and retractDistance
                targetProgress = Mathf.InverseLerp(retractDistance, buildDistance, distance);
            }

            // Interpolate progress smoothly
            float speed = (targetProgress > piece.currentProgress) ? assembleSpeed : retractSpeed;
            piece.currentProgress = Mathf.MoveTowards(piece.currentProgress, targetProgress, Time.deltaTime * speed);

            // 1. Calculate Spring Bounce Scale Curve
            float scaleMultiplier = EvaluateSpringScale(piece.currentProgress);
            piece.transform.localScale = piece.originalLocalScale * scaleMultiplier;

            // 2. Calculate Neon Pink Color & Alpha Fade
            if (piece.spriteRenderer != null)
            {
                Color darkDimPink = new Color(activePinkColor.r * 0.25f, activePinkColor.g * 0.1f, activePinkColor.b * 0.25f, 0f);
                Color currentPink = Color.Lerp(darkDimPink, activePinkColor, piece.currentProgress);
                piece.spriteRenderer.color = currentPink;
            }

            // 3. Collider Safety Threshold (Enable collider only when progress >= 0.75)
            if (piece.collider != null)
            {
                bool shouldBeSolid = (piece.currentProgress >= 0.75f);
                if (piece.collider.enabled != shouldBeSolid)
                {
                    piece.collider.enabled = shouldBeSolid;
                }
            }

            // 4. Trigger Neon Pink Star Particle Burst on Assembly
            if (piece.currentProgress >= 0.8f && !piece.particleBurstFired)
            {
                piece.particleBurstFired = true;
                if (enableStarParticles)
                {
                    SpawnPinkStarBurst(piece.transform.position + Vector3.up * 0.3f);
                }
            }
            else if (piece.currentProgress <= 0.15f)
            {
                piece.particleBurstFired = false;
            }
        }
    }

    /// <summary>
    /// Evaluates a spring overshoot curve for smooth scaling bounce.
    /// t=0 -> 0; t=0.8 -> scaleBounceFactor (e.g. 1.15); t=1 -> 1.0
    /// </summary>
    private float EvaluateSpringScale(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        // Custom spring bounce formula
        float bounce = Mathf.Sin(t * Mathf.PI * 1.15f) * scaleBounceFactor;
        return Mathf.Clamp(bounce, 0f, scaleBounceFactor);
    }

    private void SpawnPinkStarBurst(Vector3 position)
    {
        GameObject burstObj = new GameObject("PinkStarBurst");
        burstObj.transform.position = position;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = burstObj.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(activePinkColor, Color.white);
        main.gravityModifier = 0.5f;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, starParticleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.4f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        psr.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burstObj, 1.2f);
    }
}
