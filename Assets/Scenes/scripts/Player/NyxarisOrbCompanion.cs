using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nyxaris Orb Companion - Faceless darkness void orb that floats and hovers alongside the player.
/// Fully compatible with URP 2D, automatic player re-binding, 2D sorting layer sync,
/// and rich purple/void particle mote aesthetics.
/// </summary>
[AddComponentMenu("Player/Nyxaris Orb Companion")]
public class NyxarisOrbCompanion : MonoBehaviour
{
    public static NyxarisOrbCompanion Instance { get; private set; }

    [Header("Follow & Hover Physics")]
    public Vector3 frontOffset = new Vector3(1.8f, 1.25f, 0f);
    public float smoothTime = 0.14f;
    public float hoverAmplitude = 0.18f;
    public float hoverFrequency = 2.8f;

    [Header("Scene Visibility")]
    [Tooltip("If true, only renders in TutorialScene. If false, persists and follows across all levels.")]
    public bool restrictToTutorialOnly = false;

    [Header("Nyxaris Palette")]
    public Color coreColor = new Color(0.02f, 0.005f, 0.05f, 1f);
    public Color neonPurple = new Color(0.78f, 0.05f, 1f, 0.9f);
    public Color deepPurple = new Color(0.28f, 0.02f, 0.45f, 0.6f);
    public Color starTrailColor = new Color(0.86f, 0.16f, 1f, 0.95f);
    [Range(3, 12)] public int starTrailCount = 7;

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int baseSortingOrder = 15;

    private Transform playerTransform;
    private SpriteRenderer playerSpriteRenderer;
    private Vector3 velocity;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer coronaRenderer;
    private readonly List<Transform> motes = new List<Transform>();
    private readonly List<SpriteRenderer> moteRenderers = new List<SpriteRenderer>();
    private readonly List<Transform> starTrails = new List<Transform>();
    private readonly List<SpriteRenderer> starTrailRenderers = new List<SpriteRenderer>();
    private Vector3[] starTrailPositions;
    private Vector3 previousOrbPosition;
    private bool hasTrailPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupVisuals();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        CheckVisibility();
    }

    private void Start()
    {
        FindPlayer();
        UpdateTargetPosition(true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckVisibility();
        FindPlayer();
    }

    private void CheckVisibility()
    {
        bool isVisible = true;
        if (restrictToTutorialOnly)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            isVisible = string.Equals(currentScene, "TutorialScene", System.StringComparison.OrdinalIgnoreCase);
        }
        SetOrbVisibility(isVisible);
    }

    private void SetOrbVisibility(bool visible)
    {
        if (coreRenderer != null) coreRenderer.enabled = visible;
        if (coronaRenderer != null) coronaRenderer.enabled = visible;
        for (int i = 0; i < moteRenderers.Count; i++)
        {
            if (moteRenderers[i] != null) moteRenderers[i].enabled = visible;
        }
        for (int i = 0; i < starTrailRenderers.Count; i++)
        {
            if (starTrailRenderers[i] != null) starTrailRenderers[i].enabled = visible;
        }
    }

    private void UpdateStarTrails(Vector3 positionBeforeMovement)
    {
        if (starTrailPositions == null || starTrails.Count == 0) return;

        if (!hasTrailPosition || Vector3.Distance(transform.position, positionBeforeMovement) > 4f)
        {
            for (int i = 0; i < starTrailPositions.Length; i++)
            {
                starTrailPositions[i] = transform.position;
            }
            hasTrailPosition = true;
        }

        for (int i = 0; i < starTrails.Count; i++)
        {
            Vector3 target = i == 0 ? positionBeforeMovement : starTrailPositions[i - 1];
            starTrailPositions[i] = Vector3.Lerp(starTrailPositions[i], target, 10f * Time.deltaTime);
            starTrails[i].position = starTrailPositions[i];

            float twinkle = 0.82f + Mathf.Sin(Time.time * 7f + i * 1.7f) * 0.18f;
            float size = (0.13f - i * 0.009f) * twinkle;
            starTrails[i].localScale = Vector3.one * size;
            Color color = starTrailRenderers[i].color;
            color.a = (1f - (float)i / starTrails.Count) * twinkle;
            starTrailRenderers[i].color = color;
        }
    }

    private void FindPlayer()
    {
        if (move.Instance != null)
        {
            playerTransform = move.Instance.transform;
            playerSpriteRenderer = move.Instance.GetComponentInChildren<SpriteRenderer>();
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerSpriteRenderer = playerObj.GetComponentInChildren<SpriteRenderer>();
            }
        }
    }

    private void LateUpdate()
    {
        // 1. Dynamic continuous re-bind if player was spawned late
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // 2. Sorting layer synchronization with player
        SyncSortingLayers();

        // 3. Hovering & target calculation
        float facing = 1f;
        if (playerTransform.localScale.x < 0f || (move.Instance != null && move.Instance.LastFacingSign < 0f))
        {
            facing = -1f;
        }

        Vector3 target = playerTransform.position + new Vector3(frontOffset.x * facing, frontOffset.y, frontOffset.z);
        target.y += Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;

        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);

        // 4. Subtle pulsation & rotational flare
        float pulse = 1f + Mathf.Sin(Time.time * 3.5f) * 0.08f;
        if (coreRenderer != null)
        {
            coreRenderer.transform.localScale = Vector3.one * pulse;
        }

        if (coronaRenderer != null)
        {
            coronaRenderer.transform.localScale = Vector3.one * (2.4f + Mathf.Sin(Time.time * 2.2f) * 0.2f);
            coronaRenderer.transform.Rotate(0f, 0f, 25f * Time.deltaTime);
        }

        // 5. Orbiting void motes
        for (int i = 0; i < motes.Count; i++)
        {
            if (motes[i] == null) continue;

            float angle = Time.time * (1.2f + i * 0.15f) + (i * Mathf.PI * 2f / motes.Count);
            float radius = 0.65f + Mathf.Sin(Time.time * 2f + i) * 0.1f;
            motes[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            if (i < moteRenderers.Count && moteRenderers[i] != null)
            {
                Color color = moteRenderers[i].color;
                color.a = 0.35f + Mathf.Sin(Time.time * 3f + i) * 0.25f;
                moteRenderers[i].color = color;
            }
        }

        UpdateStarTrails(previousOrbPosition);
        previousOrbPosition = transform.position;
    }

    private void SyncSortingLayers()
    {
        if (playerSpriteRenderer != null)
        {
            sortingLayerName = playerSpriteRenderer.sortingLayerName;
            int playerOrder = playerSpriteRenderer.sortingOrder;

            if (coronaRenderer != null)
            {
                coronaRenderer.sortingLayerName = sortingLayerName;
                coronaRenderer.sortingOrder = playerOrder + 2;
            }
            if (coreRenderer != null)
            {
                coreRenderer.sortingLayerName = sortingLayerName;
                coreRenderer.sortingOrder = playerOrder + 3;
            }
            for (int i = 0; i < moteRenderers.Count; i++)
            {
                if (moteRenderers[i] != null)
                {
                    moteRenderers[i].sortingLayerName = sortingLayerName;
                    moteRenderers[i].sortingOrder = playerOrder + 4;
                }
            }
            for (int i = 0; i < starTrailRenderers.Count; i++)
            {
                if (starTrailRenderers[i] != null)
                {
                    starTrailRenderers[i].sortingLayerName = sortingLayerName;
                    starTrailRenderers[i].sortingOrder = playerOrder + 1;
                }
            }
        }
    }

    private void UpdateTargetPosition(bool snap)
    {
        if (playerTransform == null) return;
        float facing = playerTransform.localScale.x < 0f ? -1f : 1f;
        Vector3 target = playerTransform.position + new Vector3(frontOffset.x * facing, frontOffset.y, frontOffset.z);
        if (snap) transform.position = target;
    }

    private void SetupVisuals()
    {
        // Use standard universal sprite shader for 100% URP 2D compatibility
        Material spriteMat = CreateSpriteMaterial();

        // 1. Neon Purple Corona (Aura)
        GameObject corona = new GameObject("Nyxaris_NeonPurpleCorona");
        corona.transform.SetParent(transform, false);
        coronaRenderer = corona.AddComponent<SpriteRenderer>();
        coronaRenderer.sprite = CreateRadialSprite(128, false);
        coronaRenderer.sharedMaterial = spriteMat;
        coronaRenderer.color = neonPurple;
        coronaRenderer.sortingLayerName = sortingLayerName;
        coronaRenderer.sortingOrder = baseSortingOrder;
        corona.transform.localScale = Vector3.one * 2.4f;

        // Small bright stars preserve the orb's movement as a fading purple tail.
        starTrailPositions = new Vector3[starTrailCount];
        for (int i = 0; i < starTrailCount; i++)
        {
            GameObject star = new GameObject($"Nyxaris_PurpleStarTrail_{i}");
            star.transform.SetParent(transform, true);
            SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateStarSprite(32);
            renderer.sharedMaterial = spriteMat;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = baseSortingOrder - 1;
            renderer.color = starTrailColor;
            star.transform.position = transform.position;
            star.transform.localScale = Vector3.one * (0.13f - i * 0.009f);

            starTrails.Add(star.transform);
            starTrailRenderers.Add(renderer);
            starTrailPositions[i] = transform.position;
        }

        // 2. Deep Black Void Core
        GameObject core = new GameObject("Nyxaris_BlackCore");
        core.transform.SetParent(transform, false);
        coreRenderer = core.AddComponent<SpriteRenderer>();
        coreRenderer.sprite = CreateRadialSprite(128, true);
        coreRenderer.sharedMaterial = spriteMat;
        coreRenderer.color = coreColor;
        coreRenderer.sortingLayerName = sortingLayerName;
        coreRenderer.sortingOrder = baseSortingOrder + 1;

        // 3. Orbiting Void Motes
        for (int i = 0; i < 8; i++)
        {
            GameObject mote = new GameObject($"Nyxaris_VoidMote_{i}");
            mote.transform.SetParent(transform, false);
            SpriteRenderer renderer = mote.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRadialSprite(32, false);
            renderer.sharedMaterial = spriteMat;
            renderer.color = deepPurple;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = baseSortingOrder + 2;
            mote.transform.localScale = Vector3.one * (0.12f + (i % 3) * 0.03f);

            motes.Add(mote.transform);
            moteRenderers.Add(renderer);
        }
    }

    private Material CreateSpriteMaterial()
    {
        // Try finding URP 2D Unlit, Sprite-Default, or Sprites/Default
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");

        Material mat = new Material(shader);
        return mat;
    }

    private Sprite CreateRadialSprite(int size, bool solidCenter)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);

                if (solidCenter)
                {
                    // Punchy sharp spherical falloff
                    alpha = Mathf.Clamp01(1f - Mathf.Pow(distance, 3f));
                }
                else
                {
                    // Soft glowing aura falloff
                    alpha = Mathf.Pow(alpha, 1.8f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateStarSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float distance = offset.magnitude / (size * 0.5f);
                float angle = Mathf.Atan2(offset.y, offset.x);
                float ray = Mathf.Abs(Mathf.Cos(angle * 2f));
                float starRadius = Mathf.Lerp(0.18f, 0.95f, Mathf.Pow(ray, 7f));
                float alpha = Mathf.Clamp01((starRadius - distance) * 7f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
