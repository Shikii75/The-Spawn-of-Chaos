using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AtmosphericVoidOrbSpawner - Renders the deep cosmic void backdrop and floating purple optical bokeh orbs
/// for the Tutorial Level (TutorialScene), matching the Title Screen and Opening Prologue aesthetic.
/// 
/// Guarantees 100% Physics Retention:
/// - Strictly visual SpriteRenderers without any Collider2D or Rigidbody2D.
/// - Background sorting orders (-100 to -90), ensuring zero interaction with platforms, triggers, or entities.
/// - World-scaled delicate orbs (0.35 to 1.4 units) that float gracefully around the camera.
/// - Zero per-frame memory allocations.
/// </summary>
[AddComponentMenu("Environment/Atmospheric Void Orb Spawner")]
public class AtmosphericVoidOrbSpawner : MonoBehaviour
{
    public static AtmosphericVoidOrbSpawner Instance { get; private set; }

    [Header("Camera & Frustum Tracking")]
    [Tooltip("Target camera to anchor the atmosphere to. Auto-detected if unassigned.")]
    public Camera targetCamera;
    [Tooltip("Set camera clear color to deep void pitch black on start.")]
    public bool overrideCameraClearColor = true;
    public Color voidClearColor = new Color(0.015f, 0.008f, 0.038f, 1f); // #04020a deep void black

    [Header("Orb Population Settings")]
    [Range(15, 60)]
    public int orbCount = 34;
    [Tooltip("Global scale multiplier for world-space orbs.")]
    [Range(0.2f, 2.5f)]
    public float orbScaleMultiplier = 1.0f;
    [Tooltip("Upward drift speed multiplier.")]
    [Range(0.2f, 3.0f)]
    public float driftSpeedMultiplier = 1.0f;

    [Header("Cosmic Nebula Glow Backdrop")]
    public bool enableNebulaGlow = true;
    [Range(0.05f, 0.5f)]
    public float nebulaAlpha = 0.16f;
    public Color nebulaColor = new Color(0.52f, 0.14f, 0.82f, 1f);

    [Header("Sorting Orders (Behind All Platforms)")]
    public string sortingLayerName = "Default";
    public int nebulaSortingOrder = -100;
    public int orbSortingOrder = -90;

    // Rich Purple/Amethyst/Magenta Palette matching Title Screen & Opening Prologue
    private static readonly Color[] OrbPalette = new Color[]
    {
        new Color(0.65f, 0.20f, 0.95f), // Radiant violet
        new Color(0.85f, 0.35f, 1.00f), // Neon purple
        new Color(0.92f, 0.50f, 0.98f), // Ethereal magenta
        new Color(0.48f, 0.15f, 0.85f), // Deep amethyst
        new Color(0.78f, 0.45f, 0.90f)  // Soft lilac
    };

    private class WorldOrb
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer spriteRenderer;

        // Position relative to camera view
        public Vector2 localPos;        // Position in camera frustum space
        public float worldZ;            // Fixed depth offset

        // Physics-free motion parameters
        public float speedY;            // Upward drift speed (world units/sec)
        public float swayAmp;           // Horizontal sway amplitude (world units)
        public float swayFreq;          // Horizontal sway frequency (rad/sec)
        public float swayPhase;         // Phase offset
        public float parallaxFactor;    // Parallax response to camera pan (0 = locked to cam, 0.2 = slight lag)

        // Visuals
        public float baseSize;          // Diameter in world units
        public float baseAlpha;         // Peak transparency
        public float pulseSpeed;        // Breathing frequency
        public float pulsePhase;        // Breathing offset
        public Color color;             // Tint
    }

    private readonly List<WorldOrb> activeOrbs = new List<WorldOrb>();
    private GameObject nebulaGlowObj;
    private SpriteRenderer nebulaRenderer;

    private static Sprite s_opticalBokehSprite;
    private static Sprite s_nebulaGlowSprite;

    private Vector3 lastCamPosition;
    private bool isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSetupInTutorialScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "TutorialScene")
        {
            EnsureInScene();
        }
    }

    /// <summary>
    /// Ensures an AtmosphericVoidOrbSpawner is present in the current scene.
    /// </summary>
    public static AtmosphericVoidOrbSpawner EnsureInScene()
    {
        if (Instance != null) return Instance;

        AtmosphericVoidOrbSpawner existing = FindFirstObjectByType<AtmosphericVoidOrbSpawner>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Camera mainCam = Camera.main;
        GameObject host;
        if (mainCam != null)
        {
            host = mainCam.gameObject;
        }
        else
        {
            host = new GameObject("[AtmosphericVoidOrbSpawner]");
        }

        AtmosphericVoidOrbSpawner spawner = host.AddComponent<AtmosphericVoidOrbSpawner>();
        Instance = spawner;
        Debug.Log("<color=#D47BFF>[AtmosphericVoidOrbSpawner] Automatically initialized for scene.</color>");
        return spawner;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>() ?? Camera.main;
        }

        EnsureTextures();
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (overrideCameraClearColor && targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = voidClearColor;
        }

        InitializeAtmosphere();
    }

    private void OnEnable()
    {
        if (isInitialized && activeOrbs.Count == 0)
        {
            InitializeAtmosphere();
        }
    }

    private void OnDisable()
    {
        ClearAtmosphere();
    }

    private void OnDestroy()
    {
        ClearAtmosphere();
        if (Instance == this) Instance = null;
    }

    #region Procedural High-Res Texture Generation

    private static void EnsureTextures()
    {
        if (s_opticalBokehSprite == null)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.49f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    if (dist >= 1.0f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Pure out-of-focus optical bokeh Gaussian blur with smooth zero-falloff edge
                    float edgeFactor = Mathf.Clamp01(1.0f - dist);
                    float smoothEdge = edgeFactor * edgeFactor * (3.0f - 2.0f * edgeFactor);
                    // Ultra-diffuse wide Gaussian curve - soft, misty, ethereal glow
                    float alpha = Mathf.Exp(-1.9f * dist * dist) * smoothEdge;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            s_opticalBokehSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        if (s_nebulaGlowSprite == null)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.49f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    if (dist >= 1.0f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Ultra-wide cosmic nebula falloff curve
                    float alpha = Mathf.Exp(-2.8f * dist * dist) * (1.0f - dist * dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }
            tex.Apply();
            s_nebulaGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    #endregion

    #region Atmosphere Setup

    public void InitializeAtmosphere()
    {
        ClearAtmosphere();
        EnsureTextures();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        lastCamPosition = targetCamera.transform.position;

        // 1. Cosmic Nebula Glow Billboard
        if (enableNebulaGlow)
        {
            nebulaGlowObj = new GameObject("CosmicNebulaGlow_Background");
            nebulaGlowObj.transform.SetParent(transform, false);
            nebulaRenderer = nebulaGlowObj.AddComponent<SpriteRenderer>();
            nebulaRenderer.sprite = s_nebulaGlowSprite;
            nebulaRenderer.sortingLayerName = sortingLayerName;
            nebulaRenderer.sortingOrder = nebulaSortingOrder;
            nebulaRenderer.color = new Color(nebulaColor.r, nebulaColor.g, nebulaColor.b, nebulaAlpha);

            float camH = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 10f;
            float camW = camH * targetCamera.aspect;
            nebulaGlowObj.transform.localScale = new Vector3(camW * 1.8f, camH * 1.8f, 1f);
        }

        // 2. Spawn Floating Purple Bokeh Orbs
        float orthoH = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 10f;
        float orthoW = orthoH * targetCamera.aspect;

        for (int i = 0; i < orbCount; i++)
        {
            WorldOrb orb = CreateWorldOrb(orthoW, orthoH, randomY: true);
            activeOrbs.Add(orb);
        }

        isInitialized = true;
    }

    private WorldOrb CreateWorldOrb(float frustumWidth, float frustumHeight, bool randomY)
    {
        GameObject orbGO = new GameObject("AtmosphericVoidOrb");
        orbGO.transform.SetParent(transform, false);

        SpriteRenderer sr = orbGO.AddComponent<SpriteRenderer>();
        sr.sprite = s_opticalBokehSprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = orbSortingOrder;

        WorldOrb orb = new WorldOrb
        {
            gameObject = orbGO,
            transform = orbGO.transform,
            spriteRenderer = sr,
            color = OrbPalette[Random.Range(0, OrbPalette.Length)],
            swayFreq = Random.Range(1.0f, 2.2f),
            swayPhase = Random.Range(0f, Mathf.PI * 2f),
            pulseSpeed = Random.Range(1.2f, 2.5f),
            pulsePhase = Random.Range(0f, Mathf.PI * 2f)
        };

        // Multi-depth layer distribution:
        // Layer 2: Deep background haze (40%) - small, slow drift, subtle parallax lag
        // Layer 1: Midground ambient bokeh (40%) - balanced size and drift
        // Layer 0: Foreground dreamy soft bokeh (20%) - large, gentle majestic float
        float layerRoll = Random.value;
        if (layerRoll < 0.40f)
        {
            // Deep Background Ambient Haze
            orb.baseSize = Random.Range(0.35f, 0.65f) * orbScaleMultiplier;
            orb.baseAlpha = Random.Range(0.18f, 0.35f);
            orb.speedY = Random.Range(0.25f, 0.55f) * driftSpeedMultiplier;
            orb.swayAmp = Random.Range(0.25f, 0.55f);
            orb.parallaxFactor = 0.15f; // Slower than camera
            orb.worldZ = 5f;
            sr.sortingOrder = orbSortingOrder - 2;
        }
        else if (layerRoll < 0.80f)
        {
            // Midground Dreamy Bokeh
            orb.baseSize = Random.Range(0.70f, 1.10f) * orbScaleMultiplier;
            orb.baseAlpha = Random.Range(0.28f, 0.48f);
            orb.speedY = Random.Range(0.45f, 0.85f) * driftSpeedMultiplier;
            orb.swayAmp = Random.Range(0.45f, 0.90f);
            orb.parallaxFactor = 0.06f;
            orb.worldZ = 2f;
            sr.sortingOrder = orbSortingOrder - 1;
        }
        else
        {
            // Foreground Large Out-of-Focus Bokeh
            orb.baseSize = Random.Range(1.15f, 1.55f) * orbScaleMultiplier;
            orb.baseAlpha = Random.Range(0.35f, 0.55f);
            orb.speedY = Random.Range(0.60f, 1.10f) * driftSpeedMultiplier;
            orb.swayAmp = Random.Range(0.60f, 1.20f);
            orb.parallaxFactor = 0.0f; // Track with camera field
            orb.worldZ = 0f;
            sr.sortingOrder = orbSortingOrder;
        }

        // Horizontal distribution within camera visible area with buffer
        float marginX = frustumWidth * 0.15f;
        orb.localPos.x = Random.Range(-frustumWidth * 0.5f - marginX, frustumWidth * 0.5f + marginX);

        float marginY = frustumHeight * 0.15f;
        if (randomY)
        {
            orb.localPos.y = Random.Range(-frustumHeight * 0.5f - marginY, frustumHeight * 0.5f + marginY);
        }
        else
        {
            orb.localPos.y = -frustumHeight * 0.5f - marginY - Random.Range(0f, 1.5f);
        }

        orb.transform.localScale = Vector3.one * orb.baseSize;
        sr.color = new Color(orb.color.r, orb.color.g, orb.color.b, orb.baseAlpha);

        return orb;
    }

    public void ClearAtmosphere()
    {
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i] != null && activeOrbs[i].gameObject != null)
            {
                Destroy(activeOrbs[i].gameObject);
            }
        }
        activeOrbs.Clear();

        if (nebulaGlowObj != null)
        {
            Destroy(nebulaGlowObj);
            nebulaGlowObj = null;
        }

        isInitialized = false;
    }

    #endregion

    #region Frame-by-Frame Update & Animation

    private void LateUpdate()
    {
        if (!isInitialized || targetCamera == null)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null && !isInitialized) InitializeAtmosphere();
            return;
        }

        Vector3 camPos = targetCamera.transform.position;
        Vector3 camDelta = camPos - lastCamPosition;
        lastCamPosition = camPos;

        float dt = Time.deltaTime;
        float time = Time.time;

        float frustumH = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 10f;
        float frustumW = frustumH * targetCamera.aspect;
        float halfW = frustumW * 0.5f;
        float halfH = frustumH * 0.5f;
        float bufferX = frustumW * 0.20f;
        float bufferY = frustumH * 0.20f;

        // 1. Update Nebula Glow Billboard to follow camera smoothly with breathing
        if (nebulaGlowObj != null)
        {
            nebulaGlowObj.transform.position = new Vector3(camPos.x, camPos.y, camPos.z + 15f);
            float nebulaPulse = (Mathf.Sin(time * 0.75f) + 1f) * 0.5f;
            float currentAlpha = Mathf.Lerp(nebulaAlpha * 0.7f, nebulaAlpha * 1.3f, nebulaPulse);
            if (nebulaRenderer != null)
            {
                nebulaRenderer.color = new Color(nebulaColor.r, nebulaColor.g, nebulaColor.b, currentAlpha);
            }
        }

        // 2. Update each Floating World Orb
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            WorldOrb orb = activeOrbs[i];
            if (orb == null || orb.transform == null) continue;

            // Apply camera parallax lag
            orb.localPos.x -= camDelta.x * orb.parallaxFactor;
            orb.localPos.y -= camDelta.y * orb.parallaxFactor;

            // Upward drift
            orb.localPos.y += orb.speedY * dt;

            // Horizontal sinusoidal sway
            float swayOffset = Mathf.Sin(time * orb.swayFreq + orb.swayPhase) * orb.swayAmp;

            // Wrap vertical bounds when drifting past top of camera
            if (orb.localPos.y > halfH + bufferY)
            {
                orb.localPos.y = -halfH - bufferY - Random.Range(0f, 1.2f);
                orb.localPos.x = Random.Range(-halfW - bufferX, halfW + bufferX);
                orb.color = OrbPalette[Random.Range(0, OrbPalette.Length)];
            }
            // If camera fell quickly downward, wrap orbs downward
            else if (orb.localPos.y < -halfH - bufferY * 2.5f)
            {
                orb.localPos.y = halfH + bufferY;
            }

            // Wrap horizontal bounds if camera moved horizontally
            if (orb.localPos.x > halfW + bufferX * 1.5f)
            {
                orb.localPos.x = -halfW - bufferX;
            }
            else if (orb.localPos.x < -halfW - bufferX * 1.5f)
            {
                orb.localPos.x = halfW + bufferX;
            }

            // Calculate final world position
            Vector3 worldPos = new Vector3(
                camPos.x + orb.localPos.x + swayOffset,
                camPos.y + orb.localPos.y,
                camPos.z + 10f + orb.worldZ
            );
            orb.transform.position = worldPos;

            // Gentle breathing alpha pulse
            float pulse = 0.75f + 0.25f * Mathf.Sin(time * orb.pulseSpeed + orb.pulsePhase);
            Color c = orb.color;
            c.a = orb.baseAlpha * pulse;
            orb.spriteRenderer.color = c;
        }
    }

    #endregion
}
