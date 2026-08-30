using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerCombatJuice - High-Impact Combat Feel & Animation Juice Engine for Lumi.
/// Provides dynamic attack lunges, squash and stretch scale physics, procedural slash arcs,
/// impact collision particle bursts, and seamless motion feedback.
/// </summary>
public class PlayerCombatJuice : MonoBehaviour
{
    public static PlayerCombatJuice Instance { get; private set; }

    [Header("Squash & Stretch Settings")]
    public Vector3 attack1Squash = new Vector3(1.25f, 0.8f, 1.0f);
    public Vector3 attack2Squash = new Vector3(1.35f, 0.72f, 1.0f);
    public Vector3 jumpStretch = new Vector3(0.75f, 1.35f, 1.0f);
    public Vector3 landSquash = new Vector3(1.3f, 0.75f, 1.0f);
    public Vector3 dashStretch = new Vector3(1.4f, 0.7f, 1.0f);
    public float defaultJuiceDuration = 0.15f;
    [Tooltip("Whether attack squash and stretch is enabled on player sprite.")]
    public bool enableAttackSquash = false;

    [Header("Attack Lunge / Movement Impulse")]
    public float attack1LungeForce = 4.5f;
    public float attack2LungeForce = 7.5f;
    public float airAttackForwardForce = 3.5f;

    private Transform visualTransform; // Child object or self with SpriteRenderer/Animator
    private Vector3 originalLocalScale = Vector3.one;
    private Coroutine squashCoroutine;
    private Rigidbody2D rb;

    private static Sprite circleParticleSprite;
    private static Sprite slashArcSprite;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        rb = GetComponent<Rigidbody2D>();

        // Find visual child or self
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            visualTransform = sr.transform;
            originalLocalScale = visualTransform.localScale;
        }
        else
        {
            visualTransform = transform;
            originalLocalScale = Vector3.one;
        }

        EnsureSpritesGenerated();
    }

    /// <summary>
    /// Executes forward lunge / movement impulse during attack swing.
    /// Allows Lumi to move and flow into her strikes.
    /// </summary>
    public void ApplyAttackLunge(float facingDirection, bool isHeavy, bool isGrounded)
    {
        if (rb == null) return;

        float lungeForce = isHeavy ? attack2LungeForce : attack1LungeForce;
        if (!isGrounded)
        {
            lungeForce = airAttackForwardForce;
            // Slight upward float on air attack
            rb.linearVelocity = new Vector2(facingDirection * lungeForce, Mathf.Max(rb.linearVelocity.y, 2.2f));
        }
        else
        {
            rb.linearVelocity = new Vector2(facingDirection * lungeForce, rb.linearVelocity.y);
        }
    }

    /// <summary>
    /// Triggers squash and stretch visual animation on Lumi's sprite.
    /// </summary>
    public void TriggerSquashAndStretch(Vector3 factor, float duration = -1f)
    {
        if (visualTransform == null) return;
        if (duration <= 0f) duration = defaultJuiceDuration;

        if (squashCoroutine != null) StopCoroutine(squashCoroutine);
        squashCoroutine = StartCoroutine(SquashRoutine(factor, duration));
    }

    private IEnumerator SquashRoutine(Vector3 targetFactor, float duration)
    {
        if (visualTransform == null) yield break;

        float rawX = visualTransform.localScale.x;
        float facingSign = (rawX < 0f) ? -1f : 1f;

        Vector3 baseAbsScale = new Vector3(
            Mathf.Max(0.1f, Mathf.Abs(originalLocalScale.x)),
            Mathf.Max(0.1f, Mathf.Abs(originalLocalScale.y)),
            Mathf.Max(0.1f, Mathf.Abs(originalLocalScale.z))
        );

        Vector3 currentStartScale = new Vector3(facingSign * baseAbsScale.x, baseAbsScale.y, baseAbsScale.z);
        Vector3 targetScale = new Vector3(facingSign * baseAbsScale.x * Mathf.Max(0.1f, targetFactor.x), baseAbsScale.y * Mathf.Max(0.1f, targetFactor.y), baseAbsScale.z * Mathf.Max(0.1f, targetFactor.z));
        float elapsed = 0f;

        // Phase 1: Rapid squash/stretch (35% duration)
        float phase1Dur = duration * 0.35f;
        while (elapsed < phase1Dur)
        {
            float t = elapsed / phase1Dur;
            rawX = visualTransform.localScale.x;
            facingSign = (rawX < 0f) ? -1f : 1f;

            currentStartScale.x = facingSign * baseAbsScale.x;
            targetScale.x = facingSign * baseAbsScale.x * Mathf.Max(0.1f, targetFactor.x);

            visualTransform.localScale = Vector3.Lerp(currentStartScale, targetScale, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Phase 2: Smooth spring recovery back to normal
        elapsed = 0f;
        float phase2Dur = duration * 0.65f;
        while (elapsed < phase2Dur)
        {
            float t = elapsed / phase2Dur;
            rawX = visualTransform.localScale.x;
            facingSign = (rawX < 0f) ? -1f : 1f;

            currentStartScale.x = facingSign * baseAbsScale.x;
            targetScale.x = facingSign * baseAbsScale.x * Mathf.Max(0.1f, targetFactor.x);

            float elasticT = Mathf.Sin(t * Mathf.PI * 0.5f);
            visualTransform.localScale = Vector3.Lerp(targetScale, currentStartScale, elasticT);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        rawX = visualTransform.localScale.x;
        facingSign = (rawX < 0f) ? -1f : 1f;
        visualTransform.localScale = new Vector3(facingSign * baseAbsScale.x, baseAbsScale.y, baseAbsScale.z);
        squashCoroutine = null;
    }

    public void TriggerJumpStretch()
    {
        TriggerSquashAndStretch(jumpStretch, 0.2f);
    }

    public void TriggerLandSquash()
    {
        TriggerSquashAndStretch(landSquash, 0.15f);
    }

    public void TriggerDashStretch()
    {
        TriggerSquashAndStretch(dashStretch, 0.18f);
    }

    /// <summary>
    /// Spawns a procedural slash arc at the attack position.
    /// </summary>
    public void SpawnSlashArc(Vector3 position, float facingDirection, bool isHeavy)
    {
        EnsureSpritesGenerated();

        GameObject arcObj = new GameObject(isHeavy ? "HeavySlashArc" : "SlashArc");
        arcObj.transform.position = position;

        float baseScale = isHeavy ? 1.6f : 1.15f;
        arcObj.transform.localScale = new Vector3(facingDirection * baseScale, baseScale, 1f);

        SpriteRenderer sr = arcObj.AddComponent<SpriteRenderer>();
        sr.sprite = slashArcSprite;
        sr.color = isHeavy
            ? new Color(1.0f, 0.55f, 0.15f, 0.95f)
            : new Color(1.0f, 0.85f, 0.35f, 0.9f);
        sr.sortingOrder = 20;

        StartCoroutine(AnimateShadowPunchArc(arcObj, sr, isHeavy));
    }

    /// <summary>
    /// <summary>
    /// Spawns dark arcane shadow VFX (expanding shadow shockwave arc + chaos ember particle burst)
    /// at the apex/extension point of each player punch.
    /// </summary>
    public void SpawnShadowPunchVFX(Vector3 fistPosition, float facingDirection, bool isHeavy)
    {
        EnsureSpritesGenerated();

        // 1. Procedural Shadow Shockwave Arc
        GameObject arcObj = new GameObject(isHeavy ? "HeavyShadowPunchArc" : "LightShadowPunchArc");
        arcObj.transform.position = fistPosition;

        float baseScale = isHeavy ? 1.6f : 1.15f;
        arcObj.transform.localScale = new Vector3(facingDirection * baseScale, baseScale, 1f);

        SpriteRenderer sr = arcObj.AddComponent<SpriteRenderer>();
        sr.sprite = slashArcSprite;
        // Deep Obsidian Shadow Core with Neon Violet / Chaos Magenta edge
        sr.color = isHeavy ? new Color(0.75f, 0.15f, 1.0f, 0.95f) : new Color(0.55f, 0.2f, 0.95f, 0.85f);
        sr.sortingOrder = 20;

        StartCoroutine(AnimateShadowPunchArc(arcObj, sr, isHeavy));

        // 2. High-Impact Shadow Chaos Particle Burst
        GameObject burstObj = new GameObject("ShadowPunchEmberBurst");
        burstObj.transform.position = fistPosition;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = burstObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = new Material(Shader.Find("Sprites/Default"));

        var main = ps.main;
        main.duration = 0.22f;
        main.loop = false;
        main.startLifetime = isHeavy ? 0.26f : 0.18f;
        main.startSpeed = isHeavy ? 9.5f : 6.0f;
        main.startSize = isHeavy ? 0.24f : 0.16f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;

        // Shadow color gradient: Deep Obsidian / Dark Purple -> Electric Chaos Violet
        Color deepShadow = new Color(0.12f, 0.02f, 0.24f, 1f);
        Color brightViolet = isHeavy ? new Color(0.95f, 0.35f, 1.0f, 1f) : new Color(0.7f, 0.25f, 1.0f, 1f);
        main.startColor = new ParticleSystem.MinMaxGradient(deepShadow, brightViolet);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(isHeavy ? 18 : 10)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(0f, (facingDirection < 0f) ? 180f : 0f, 0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.2f, 0f);

        ps.Play();
        Destroy(burstObj, 0.35f);
    }

    private IEnumerator AnimateShadowPunchArc(GameObject arcObj, SpriteRenderer sr, bool isHeavy)
    {
        float duration = isHeavy ? 0.18f : 0.13f;
        float elapsed = 0f;
        Vector3 startScale = arcObj.transform.localScale;
        Vector3 targetScale = startScale * (isHeavy ? 1.55f : 1.35f);
        Color startColor = sr.color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            if (arcObj != null)
            {
                arcObj.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t * t);
                sr.color = c;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (arcObj != null) Destroy(arcObj);
    }

    /// <summary>
    /// Spawns high-impact collision particle bursts at the contact point when an enemy is hit.
    /// </summary>
    public void SpawnHitCollisionParticles(Vector3 contactPoint, bool isHeavy)
    {
        GameObject burstObj = new GameObject("HitCollisionBurst");
        burstObj.transform.position = contactPoint;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = burstObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = new Material(Shader.Find("Sprites/Default"));

        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = isHeavy ? 0.28f : 0.18f;
        main.startSpeed = isHeavy ? 12f : 7f;
        main.startSize = isHeavy ? 0.22f : 0.14f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.8f;

        // Color gradient: Arcane Violet to Bright Spark White
        main.startColor = isHeavy
            ? new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.5f, 0.1f), new Color(0.9f, 0.2f, 1.0f))
            : new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.4f, 1.0f), new Color(1.0f, 1.0f, 0.9f));

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(isHeavy ? 20 : 12)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

        ps.Play();
        Destroy(burstObj, 0.4f);
    }

    private static void EnsureSpritesGenerated()
    {
        if (circleParticleSprite != null && slashArcSprite != null) return;

        // 1. Circle Particle Sprite (Soft Radial Gradient)
        int size = 32;
        Texture2D circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circleTex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = Mathf.SmoothStep(1f, 0f, dist / radius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        circleTex.SetPixels(pixels);
        circleTex.Apply();
        circleParticleSprite = Sprite.Create(circleTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);

        // 2. Crescent Slash Arc Sprite
        int arcW = 64;
        int arcH = 64;
        Texture2D arcTex = new Texture2D(arcW, arcH, TextureFormat.RGBA32, false);
        arcTex.filterMode = FilterMode.Bilinear;
        Color[] arcPixels = new Color[arcW * arcH];
        Vector2 arcCenter = new Vector2(arcW * 0.2f, arcH * 0.5f);

        for (int y = 0; y < arcH; y++)
        {
            for (int x = 0; x < arcW; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), arcCenter);
                // Crescent ring between radius 20 and 30
                if (dist >= 18f && dist <= 32f && x >= arcW * 0.2f)
                {
                    float ringCenter = 25f;
                    float ringDist = Mathf.Abs(dist - ringCenter);
                    float alpha = Mathf.SmoothStep(1f, 0f, ringDist / 7f);
                    arcPixels[y * arcW + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    arcPixels[y * arcW + x] = Color.clear;
                }
            }
        }
        arcTex.SetPixels(arcPixels);
        arcTex.Apply();
        slashArcSprite = Sprite.Create(arcTex, new Rect(0, 0, arcW, arcH), new Vector2(0.3f, 0.5f), 64f);
    }
}
