using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Minigames;

/// <summary>
/// DarkHealingVFX - Handles dark abyssal void orbs gathering and converging into the player during healing.
/// 
/// Visual Design:
/// 1. Dynamic Perimeter Spawn:
///    - Clusters of dark void orbs materialize in a 360-degree perimeter around the player.
/// 2. Spiraling Inward Convergence:
///    - Each orb spirals inward with magnetic gravitational acceleration towards the player's core.
///    - Orbs leave trailing abyssal shadow particles as they fly.
/// 3. Core Absorption & Feedback:
///    - Upon reaching the player, each orb collapses with an energy ripple and dark spark burst.
///    - Pulses subtle dark-violet resonance through the player's sprite.
///    - Triggers fluid liquid splash on the Health HUD Orb container.
/// </summary>
public class DarkHealingVFX : MonoBehaviour
{
    private static DarkHealingVFX instance;
    public static DarkHealingVFX Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<DarkHealingVFX>();
                if (instance == null)
                {
                    GameObject go = new GameObject("DarkHealingVFX_System");
                    instance = go.AddComponent<DarkHealingVFX>();
                }
            }
            return instance;
        }
    }

    [Header("Visual Colors")]
    public Color darkCoreColor = new Color(0.02f, 0.01f, 0.035f, 1.0f);
    public Color darkGlowColor = new Color(0.72f, 0.18f, 0.95f, 0.95f);
    public Color darkRimColor = new Color(0.95f, 0.15f, 0.45f, 1.0f);

    [Header("Gathering Parameters")]
    public float minSpawnRadius = 2.4f;
    public float maxSpawnRadius = 4.2f;
    public float baseOrbScale = 0.38f;
    public float maxFlightDuration = 0.85f;

    // Procedural Sprites Cache
    private static Sprite darkOrbSprite;
    private static Sprite sparkSprite;
    private static Sprite groundVortexSprite;
    private static Material defaultSpriteMat;

    private float lastGatherTime = -999f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureAssets();
    }

    /// <summary>
    /// Static entry point to trigger the dark orbs gathering healing effect at the target transform.
    /// </summary>
    public static void TriggerGather(Transform target, int orbCount = 10)
    {
        if (target == null) return;
        Instance.StartGatherSequence(target, orbCount);
    }

    public void StartGatherSequence(Transform target, int orbCount)
    {
        // Debounce within 0.15s to prevent overlapping double-bursts on identical frames
        if (Time.time < lastGatherTime + 0.15f) return;
        lastGatherTime = Time.time;

        EnsureAssets();
        StartCoroutine(GatherRoutine(target, Mathf.Clamp(orbCount, 6, 16)));
    }

    private IEnumerator GatherRoutine(Transform target, int count)
    {
        if (target == null) yield break;

        // 1. Spawn Ground Abyssal Swirl Decal beneath player
        StartCoroutine(AnimateGroundVortex(target));

        // 2. Spawn Spiraling Dark Orbs in a scattered perimeter
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            if (target == null) yield break;

            float angle = baseAngle + ((float)i / count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            float dist = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * (dist * 0.65f), 0f);
            Vector3 spawnPos = target.position + Vector3.up * 0.6f + offset;

            float staggerDelay = (float)i * 0.045f;
            StartCoroutine(AnimateSingleOrb(spawnPos, target, staggerDelay));
        }

        yield return null;
    }

    private IEnumerator AnimateSingleOrb(Vector3 spawnPos, Transform target, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (target == null) yield break;

        GameObject orbGO = new GameObject("DarkHealingOrb");
        orbGO.transform.position = spawnPos;
        orbGO.transform.localScale = Vector3.zero;

        SpriteRenderer sr = orbGO.AddComponent<SpriteRenderer>();
        sr.sprite = darkOrbSprite;
        sr.sortingOrder = 35;
        if (defaultSpriteMat != null) sr.material = defaultSpriteMat;

        Vector3 currentPos = spawnPos;
        float targetYOffset = 0.65f;
        float currentScale = 0f;
        float targetScale = baseOrbScale * Random.Range(0.85f, 1.25f);

        // Initial burst outward
        Vector3 playerCenter = target.position + Vector3.up * targetYOffset;
        Vector3 outwardDir = (spawnPos - playerCenter).normalized;
        Vector3 velocity = outwardDir * Random.Range(1.8f, 3.2f);

        float elapsed = 0f;
        float trailTimer = 0f;
        float angularSpeed = (Random.value > 0.5f ? 1f : -1f) * Random.Range(4.5f, 7.5f);

        while (elapsed < maxFlightDuration && target != null)
        {
            elapsed += Time.deltaTime;
            float dt = Time.deltaTime;

            playerCenter = target.position + Vector3.up * targetYOffset;
            Vector3 toTarget = playerCenter - currentPos;
            float dist = toTarget.magnitude;

            // Grow orb on spawn
            if (currentScale < targetScale)
            {
                currentScale = Mathf.Min(targetScale, currentScale + dt * 4.5f);
                orbGO.transform.localScale = Vector3.one * currentScale;
            }

            // Magnetic gravitational pull toward player core + tangential swirl
            Vector3 pullDir = toTarget.normalized;
            Vector3 tangentDir = new Vector3(-pullDir.y, pullDir.x, 0f) * angularSpeed;

            // Acceleration increases as orb gets closer (gravitational snap)
            float pullStrength = Mathf.Lerp(14f, 45f, 1f - Mathf.Clamp01(dist / maxSpawnRadius));
            velocity = Vector3.Lerp(velocity, pullDir * pullStrength + tangentDir, dt * 8.5f);

            currentPos += velocity * dt;
            orbGO.transform.position = currentPos;

            // Emit trailing dark motes
            trailTimer += dt;
            if (trailTimer >= 0.035f)
            {
                trailTimer = 0f;
                SpawnTrailMote(currentPos, darkGlowColor, currentScale * 0.45f);
            }

            // Impact check
            if (dist < 0.35f)
            {
                break;
            }

            yield return null;
        }

        // On absorption into player core:
        if (target != null)
        {
            Vector3 impactPos = target.position + Vector3.up * targetYOffset;
            SpawnImpactBurst(impactPos);

            // Pulse player sprite with subtle dark glow
            SpriteRenderer targetSr = target.GetComponentInChildren<SpriteRenderer>();
            if (targetSr != null)
            {
                StartCoroutine(PlayerSpriteDarkPulse(targetSr));
            }

            // Liquid splash on HUD Health Orb
            if (HUDOrbPanel.Instance != null)
            {
                HUDOrbPanel.Instance.TriggerSplash(OrbType.Health, 0.45f);
            }
        }

        if (orbGO != null) Destroy(orbGO);
    }

    private IEnumerator AnimateGroundVortex(Transform target)
    {
        if (target == null) yield break;

        GameObject vortexGO = new GameObject("DarkHealingVortex");
        vortexGO.transform.position = target.position - Vector3.up * 0.4f;
        vortexGO.transform.localScale = new Vector3(0.5f, 0.2f, 1f);

        SpriteRenderer sr = vortexGO.AddComponent<SpriteRenderer>();
        sr.sprite = groundVortexSprite;
        sr.color = new Color(darkGlowColor.r, darkGlowColor.g, darkGlowColor.b, 0f);
        sr.sortingOrder = 3;

        float duration = 1.1f;
        float elapsed = 0f;
        Vector3 maxScale = new Vector3(2.6f, 0.85f, 1f);

        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            vortexGO.transform.position = target.position - Vector3.up * 0.4f;
            vortexGO.transform.Rotate(0f, 0f, 120f * Time.deltaTime);

            float alpha = Mathf.Sin(t * Mathf.PI);
            sr.color = new Color(darkGlowColor.r, darkGlowColor.g, darkGlowColor.b, alpha * 0.75f);
            vortexGO.transform.localScale = Vector3.Lerp(new Vector3(0.5f, 0.2f, 1f), maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));

            yield return null;
        }

        if (vortexGO != null) Destroy(vortexGO);
    }

    private void SpawnTrailMote(Vector3 pos, Color col, float scale)
    {
        GameObject mote = new GameObject("DarkTrailMote");
        mote.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.08f);
        mote.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = mote.AddComponent<SpriteRenderer>();
        sr.sprite = sparkSprite;
        sr.color = new Color(col.r, col.g, col.b, 0.8f);
        sr.sortingOrder = 34;

        StartCoroutine(FadeAndDestroy(mote, 0.22f));
    }

    private void SpawnImpactBurst(Vector3 pos)
    {
        // Spawn 4-5 radial dark sparks
        for (int i = 0; i < 5; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.8f, 3.8f);

            GameObject spark = new GameObject("DarkImpactSpark");
            spark.transform.position = pos;
            spark.transform.localScale = Vector3.one * Random.Range(0.18f, 0.32f);

            SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = sparkSprite;
            sr.color = Color.Lerp(darkGlowColor, darkRimColor, Random.value);
            sr.sortingOrder = 36;

            StartCoroutine(AnimateSpark(spark, vel, 0.25f));
        }
    }

    private IEnumerator AnimateSpark(GameObject spark, Vector2 vel, float lifetime)
    {
        float elapsed = 0f;
        SpriteRenderer sr = spark.GetComponent<SpriteRenderer>();
        Color startCol = sr != null ? sr.color : Color.white;

        while (elapsed < lifetime && spark != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            spark.transform.position += (Vector3)vel * Time.deltaTime;
            vel *= 0.92f;

            if (sr != null)
            {
                sr.color = new Color(startCol.r, startCol.g, startCol.b, (1f - t) * startCol.a);
            }
            yield return null;
        }

        if (spark != null) Destroy(spark);
    }

    private IEnumerator PlayerSpriteDarkPulse(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color baseCol = sr.color;
        Color pulseCol = Color.Lerp(baseCol, darkGlowColor, 0.45f);

        sr.color = pulseCol;
        float elapsed = 0f;
        float dur = 0.12f;
        while (elapsed < dur && sr != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            sr.color = Color.Lerp(pulseCol, baseCol, t);
            yield return null;
        }
    }

    private IEnumerator FadeAndDestroy(GameObject go, float lifetime)
    {
        float elapsed = 0f;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        Color startCol = sr != null ? sr.color : Color.white;

        while (elapsed < lifetime && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            if (sr != null)
            {
                sr.color = new Color(startCol.r, startCol.g, startCol.b, (1f - t) * startCol.a);
            }
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PROCEDURAL SPRITE GENERATION
    // ══════════════════════════════════════════════════════════════════

    private static void EnsureAssets()
    {
        if (darkOrbSprite == null) darkOrbSprite = GenerateDarkOrbSprite(64);
        if (sparkSprite == null) sparkSprite = GenerateSparkSprite(32);
        if (groundVortexSprite == null) groundVortexSprite = GenerateGroundVortexSprite(128);

        if (defaultSpriteMat == null)
        {
            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
            if (s != null)
            {
                defaultSpriteMat = new Material(s) { hideFlags = HideFlags.DontSave };
            }
        }
    }

    private static Sprite GenerateDarkOrbSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float center = size * 0.5f;
        float radius = size * 0.45f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float normD = dist / radius;

                if (normD <= 1.0f)
                {
                    // Obsidian pitch core inside
                    if (normD < 0.65f)
                    {
                        float innerT = normD / 0.65f;
                        Color core = Color.Lerp(new Color(0.01f, 0.005f, 0.02f, 1.0f), new Color(0.12f, 0.02f, 0.22f, 1.0f), innerT);
                        pixels[y * size + x] = core;
                    }
                    else
                    {
                        // Searing dark-violet & magenta coronal rim
                        float rimT = (normD - 0.65f) / 0.35f;
                        float rimGlow = Mathf.Sin(rimT * Mathf.PI);
                        Color rim = Color.Lerp(new Color(0.85f, 0.15f, 0.98f, 1.0f), new Color(0.95f, 0.10f, 0.45f, 0.9f), rimT);
                        rim.a = Mathf.SmoothStep(1.0f, 0.0f, rimT * rimT);
                        pixels[y * size + x] = rim;
                    }
                }
                else
                {
                    // Atmospheric aura falloff outside
                    float extraD = (dist - radius) / (size * 0.15f);
                    if (extraD <= 1.0f)
                    {
                        float aura = Mathf.Exp(-extraD * extraD * 4.0f) * 0.4f;
                        pixels[y * size + x] = new Color(0.72f, 0.15f, 0.95f, aura);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite GenerateSparkSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float center = size * 0.5f;
        float radius = size * 0.45f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.Exp(-t * t * 4.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite GenerateGroundVortexSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float center = size * 0.5f;
        float radius = size * 0.42f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                float spiral = Mathf.Sin(angle * 3f + (dist / radius) * Mathf.PI * 2f);
                float distFactor = Mathf.Exp(-Mathf.Pow(dist - radius * 0.7f, 2f) / (radius * radius * 0.15f));
                float alpha = Mathf.Clamp01(distFactor * (0.6f + spiral * 0.4f));

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
