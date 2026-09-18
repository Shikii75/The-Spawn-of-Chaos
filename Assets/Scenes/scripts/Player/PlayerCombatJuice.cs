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
    public float attack1LungeForce = 0f;
    public float attack2LungeForce = 0f;
    public float airAttackForwardForce = 3.5f;

    private Transform visualTransform; // Child object or self with SpriteRenderer/Animator
    private Vector3 originalLocalScale = Vector3.one;
    private Coroutine squashCoroutine;
    private Rigidbody2D rb;

    private static Sprite circleParticleSprite;
    private static Sprite slashArcSprite;
    private static Sprite slashGlowSprite;
    private static Sprite sparkleShardSprite;
    private static Sprite pierceShockSprite;

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
        // Removed main.duration assignment to prevent Unity runtime error
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
        LowResBlackOrb.ConfigureParticleRenderer(psRenderer, 22);

        var main = ps.main;
        main.loop = false;
        main.startLifetime = isHeavy ? 0.28f : 0.18f;
        main.startSpeed = isHeavy ? 12f : 7f;
        main.startSize = isHeavy ? 0.26f : 0.18f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.4f;

        // Low-resolution black orbs with subtle dark glints
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.01f, 0.01f, 0.02f, 0.95f), new Color(0.06f, 0.06f, 0.09f, 0.85f));

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

    /// <summary>
    /// Spawns high-end GPU-style satisfying slash VFX for spear and swordmanship combos.
    /// comboStep 1: Sweeping horizontal crescent slash (Electric Cyan / Neon White core).
    /// comboStep 2: Reverse rising diagonal upper cut (Neon Magenta / Electric Violet with spark fountain).
    /// comboStep 3: Sonic Piercing Thrust & Finisher Shockwave (Massive piercing wedge + dual shockwave rings).
    /// </summary>
    public void SpawnGpuSlashVFX(Vector3 position, float facingDirection, int comboStep)
    {
        EnsureSpritesGenerated();

        float dir = (facingDirection < 0f) ? -1f : 1f;
        float yOffset = (comboStep == 2) ? 0.28f : ((comboStep == 1) ? 0.12f : 0.08f);
        Vector3 spawnPos = position + new Vector3(dir * 1.25f, yOffset, 0f);

        float rotZ;
        float baseScale;
        Color bladeColor;
        Color glowColor;
        Color sparkColor;
        Color shardColor;

        if (comboStep == 1)
        {
            // Step 1: Celestial Azure Blade + Warm Solar Amber Glow (Harmonious Complementary Duo)
            rotZ = -14f;
            baseScale = 2.1f;
            bladeColor = new Color(0.35f, 0.95f, 1.0f, 1.0f); // Bright Diamond Cyan
            glowColor  = new Color(1.0f, 0.72f, 0.15f, 0.75f); // Radiant Solar Gold
            sparkColor = new Color(0.2f, 0.9f, 1.0f, 1.0f);
            shardColor = new Color(1.0f, 0.92f, 0.5f, 0.95f);
        }
        else if (comboStep == 2)
        {
            // Step 2: Void Magenta Blade + Luminescent Emerald Mint Glow (Exotic Complementary Duo)
            rotZ = 34f;
            baseScale = 2.35f;
            bladeColor = new Color(1.0f, 0.25f, 0.75f, 1.0f); // Vivid Neon Magenta
            glowColor  = new Color(0.1f, 1.0f, 0.65f, 0.72f); // Luminous Emerald Mint
            sparkColor = new Color(0.85f, 0.2f, 1.0f, 1.0f);
            shardColor = new Color(0.4f, 1.0f, 0.8f, 0.95f);
        }
        else
        {
            // Step 3: Supernova Finisher (Prismatic Pure White + Cosmic Indigo & Solar Flare)
            rotZ = 0f;
            baseScale = 2.9f;
            bladeColor = new Color(1.0f, 1.0f, 1.0f, 1.0f); // Pure Prismatic White
            glowColor  = new Color(0.55f, 0.25f, 1.0f, 0.85f); // Deep Cosmic Indigo
            sparkColor = new Color(1.0f, 0.85f, 0.3f, 1.0f);
            shardColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        }

        // --- 1. Secondary Luminous Glow Envelope (Behind Sharp Arc) ---
        GameObject glowObj = new GameObject($"SlashGlow_Step{comboStep}");
        glowObj.transform.position = spawnPos;
        glowObj.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        glowObj.transform.localScale = new Vector3(dir * baseScale * 1.15f, baseScale * 1.15f, 1f);

        SpriteRenderer glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = (comboStep == 3) ? (pierceShockSprite ?? slashGlowSprite) : slashGlowSprite;
        glowSr.color = glowColor;
        glowSr.sortingOrder = 74;
        StartCoroutine(AnimateGpuSlashArc(glowObj, glowSr, baseScale * 1.15f, comboStep, 0.18f, true));

        // --- 2. Primary Razor Blade Arc (Foreground Sharp Core) ---
        GameObject arcObj = new GameObject($"SlashArc_Step{comboStep}");
        arcObj.transform.position = spawnPos;
        arcObj.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        arcObj.transform.localScale = new Vector3(dir * baseScale, baseScale, 1f);

        SpriteRenderer arcSr = arcObj.AddComponent<SpriteRenderer>();
        arcSr.sprite = (comboStep == 3) ? (pierceShockSprite ?? slashArcSprite) : slashArcSprite;
        arcSr.color = bladeColor;
        arcSr.sortingOrder = 76;
        StartCoroutine(AnimateGpuSlashArc(arcObj, arcSr, baseScale, comboStep, 0.15f, false));

        // --- 3. Floating Arcane Sparkle Shards / Star Runes ---
        SpawnArcShards(spawnPos, dir, rotZ, comboStep, shardColor);

        // --- 4. Directional High-Velocity Kinetic Spark Fountain ---
        SpawnGpuSparkSpray(spawnPos, dir, comboStep, sparkColor, rotZ);

        // --- 5. Expanding Distortion Shockwave Ring (Step 3 or Crits) ---
        if (comboStep == 3)
        {
            SpawnShockwaveRing(spawnPos, 1, new Color(1.0f, 0.85f, 0.3f, 0.9f));
            SpawnShockwaveRing(spawnPos + new Vector3(dir * 0.5f, 0f, 0f), 2, new Color(0.4f, 0.95f, 1.0f, 0.85f));
        }
        else
        {
            SpawnShockwaveRing(spawnPos, comboStep, glowColor);
        }
    }

    private void SpawnArcShards(Vector3 center, float facingDir, float rotZ, int comboStep, Color shardColor)
    {
        EnsureSpritesGenerated();
        int count = (comboStep == 3) ? 6 : 4;
        float radius = 1.1f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angleOffset = Mathf.Lerp(-40f, 40f, t);
            float rad = (rotZ + angleOffset) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * radius * facingDir, Mathf.Sin(rad) * radius, 0f);

            GameObject shard = new GameObject("ArcShard");
            shard.transform.position = center + offset;
            float initialScale = UnityEngine.Random.Range(0.45f, 0.75f);
            shard.transform.localScale = new Vector3(initialScale, initialScale, 1f);
            shard.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = sparkleShardSprite;
            sr.color = shardColor;
            sr.sortingOrder = 78;

            StartCoroutine(AnimateShardRoutine(shard, sr, initialScale));
        }
    }

    private IEnumerator AnimateShardRoutine(GameObject shard, SpriteRenderer sr, float initialScale)
    {
        float duration = 0.22f;
        float elapsed = 0f;
        Vector3 startPos = shard.transform.position;
        Vector3 drift = UnityEngine.Random.insideUnitCircle * 0.4f;
        Color startCol = sr.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            if (shard != null)
            {
                shard.transform.position = startPos + drift * t;
                float currentScale = Mathf.Lerp(initialScale, initialScale * 1.35f, t);
                shard.transform.localScale = new Vector3(currentScale, currentScale, 1f);
                shard.transform.Rotate(0f, 0f, 180f * Time.unscaledDeltaTime);

                Color c = startCol;
                c.a = Mathf.Lerp(startCol.a, 0f, t * t);
                sr.color = c;
            }
            yield return null;
        }

        if (shard != null) Destroy(shard);
    }

    private IEnumerator AnimateGpuSlashArc(GameObject arcObj, SpriteRenderer sr, float baseScale, int comboStep, float customDuration, bool isGlow)
    {
        float duration = customDuration;
        float elapsed = 0f;
        Vector3 initialScale = arcObj.transform.localScale;
        Vector3 targetScale = initialScale * (isGlow ? 1.45f : 1.25f);
        Color startColor = sr.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            if (arcObj != null)
            {
                float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);
                arcObj.transform.localScale = Vector3.Lerp(initialScale, targetScale, scaleT);

                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t * t);
                sr.color = c;
            }
            yield return null;
        }

        if (arcObj != null) Destroy(arcObj);
    }

    private void SpawnGpuSparkSpray(Vector3 position, float facingDirection, int comboStep, Color sparkColor, float rotZ)
    {
        GameObject burstObj = new GameObject($"GpuSparks_Step{comboStep}");
        burstObj.transform.position = position;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = burstObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = new Material(Shader.Find("Sprites/Default"));

        var main = ps.main;
        main.loop = false;
        main.startLifetime = (comboStep == 3) ? 0.32f : 0.22f;
        main.startSpeed = (comboStep == 3) ? 22f : 15f;
        main.startSize = (comboStep == 3) ? 0.22f : 0.16f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.0f;

        Color hotWhite = Color.white;
        main.startColor = new ParticleSystem.MinMaxGradient(sparkColor, hotWhite);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        int sparkCount = (comboStep == 3) ? 42 : (comboStep == 2 ? 30 : 25);
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)sparkCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = (comboStep == 3) ? 14f : 30f;
        shape.radius = 0.08f;
        float forwardRotY = (facingDirection < 0f) ? -90f : 90f;
        float forwardRotZ = (facingDirection < 0f) ? -rotZ : rotZ;
        shape.rotation = new Vector3(0f, forwardRotY, forwardRotZ);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.1f, 0f);

        ps.Play();
        Destroy(burstObj, 0.5f);
    }

    private void SpawnShockwaveRing(Vector3 position, int comboStep, Color ringColor)
    {
        EnsureSpritesGenerated();
        GameObject ringObj = new GameObject("ShockwaveRing");
        ringObj.transform.position = position;
        ringObj.transform.localScale = Vector3.zero;

        SpriteRenderer sr = ringObj.AddComponent<SpriteRenderer>();
        sr.sprite = circleParticleSprite;
        sr.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.75f);
        sr.sortingOrder = 72;

        float targetDiameter = (comboStep == 3) ? 3.4f : ((comboStep == 2) ? 2.4f : 1.9f);
        StartCoroutine(AnimateShockwaveRing(ringObj, sr, targetDiameter));
    }

    private IEnumerator AnimateShockwaveRing(GameObject ringObj, SpriteRenderer sr, float targetDiameter)
    {
        float duration = 0.16f;
        float elapsed = 0f;
        Color startCol = sr.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            if (ringObj != null)
            {
                float currentScale = Mathf.Lerp(0.2f, targetDiameter, Mathf.Sin(t * Mathf.PI * 0.5f));
                ringObj.transform.localScale = new Vector3(currentScale, currentScale, 1f);
                Color c = startCol;
                c.a = Mathf.Lerp(startCol.a, 0f, t * t);
                sr.color = c;
            }
            yield return null;
        }

        if (ringObj != null) Destroy(ringObj);
    }

    private static void EnsureSpritesGenerated()
    {
        if (circleParticleSprite != null && slashArcSprite != null && slashGlowSprite != null &&
            sparkleShardSprite != null && pierceShockSprite != null) return;

        // 1. Circle Particle Sprite (64x64 Soft Radial Glow)
        int cSize = 64;
        Texture2D circleTex = new Texture2D(cSize, cSize, TextureFormat.RGBA32, false);
        circleTex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(cSize * 0.5f, cSize * 0.5f);
        float radius = cSize * 0.48f;

        Color[] cPixels = new Color[cSize * cSize];
        for (int y = 0; y < cSize; y++)
        {
            for (int x = 0; x < cSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = Mathf.SmoothStep(1f, 0f, dist / radius);
                    cPixels[y * cSize + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    cPixels[y * cSize + x] = Color.clear;
                }
            }
        }
        circleTex.SetPixels(cPixels);
        circleTex.Apply();
        circleParticleSprite = Sprite.Create(circleTex, new Rect(0, 0, cSize, cSize), new Vector2(0.5f, 0.5f), 64f);

        // 2. High-Definition Razor Curved Crescent Slash Arc Sprite (128x128)
        int arcW = 128;
        int arcH = 128;
        Texture2D arcTex = new Texture2D(arcW, arcH, TextureFormat.RGBA32, false);
        arcTex.filterMode = FilterMode.Bilinear;
        Color[] arcPixels = new Color[arcW * arcH];
        Vector2 arcCenter = new Vector2(arcW * 0.12f, arcH * 0.5f);

        for (int y = 0; y < arcH; y++)
        {
            for (int x = 0; x < arcW; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), arcCenter);
                float angle = Mathf.Atan2(y - arcCenter.y, x - arcCenter.x) * Mathf.Rad2Deg;
                if (dist >= 32f && dist <= 76f && angle >= -76f && angle <= 76f)
                {
                    float ringCenter = 56f;
                    float ringDist = Mathf.Abs(dist - ringCenter);
                    float crossAlpha = Mathf.Pow(Mathf.Clamp01(1f - (ringDist / 18f)), 1.5f);
                    float angleAlpha = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(angle * Mathf.Deg2Rad * (90f / 76f))), 0.7f);
                    float alpha = Mathf.Clamp01(crossAlpha * angleAlpha);
                    float coreBoost = (ringDist <= 3.5f) ? 1.0f : 0.92f;
                    arcPixels[y * arcW + x] = new Color(coreBoost, coreBoost, coreBoost, alpha);
                }
                else
                {
                    arcPixels[y * arcW + x] = Color.clear;
                }
            }
        }
        arcTex.SetPixels(arcPixels);
        arcTex.Apply();
        slashArcSprite = Sprite.Create(arcTex, new Rect(0, 0, arcW, arcH), new Vector2(0.12f, 0.5f), 64f);

        // 3. High-Definition Soft Chromatic Bloom Envelope Sprite (128x128)
        Texture2D glowTex = new Texture2D(arcW, arcH, TextureFormat.RGBA32, false);
        glowTex.filterMode = FilterMode.Bilinear;
        Color[] glowPixels = new Color[arcW * arcH];

        for (int y = 0; y < arcH; y++)
        {
            for (int x = 0; x < arcW; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), arcCenter);
                float angle = Mathf.Atan2(y - arcCenter.y, x - arcCenter.x) * Mathf.Rad2Deg;
                if (dist >= 18f && dist <= 90f && angle >= -82f && angle <= 82f)
                {
                    float ringCenter = 56f;
                    float ringDist = Mathf.Abs(dist - ringCenter);
                    float crossAlpha = Mathf.Pow(Mathf.Clamp01(1f - (ringDist / 34f)), 2.0f);
                    float angleAlpha = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(angle * Mathf.Deg2Rad * (90f / 82f))), 1.1f);
                    float alpha = Mathf.Clamp01(crossAlpha * angleAlpha * 0.85f);
                    glowPixels[y * arcW + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    glowPixels[y * arcW + x] = Color.clear;
                }
            }
        }
        glowTex.SetPixels(glowPixels);
        glowTex.Apply();
        slashGlowSprite = Sprite.Create(glowTex, new Rect(0, 0, arcW, arcH), new Vector2(0.12f, 0.5f), 64f);

        // 4. Sparkling Diamond Star / Crystal Shard Sprite (64x64)
        int sSize = 64;
        Texture2D shardTex = new Texture2D(sSize, sSize, TextureFormat.RGBA32, false);
        shardTex.filterMode = FilterMode.Bilinear;
        Color[] sPixels = new Color[sSize * sSize];
        float halfS = sSize * 0.5f;

        for (int y = 0; y < sSize; y++)
        {
            for (int x = 0; x < sSize; x++)
            {
                float dx = Mathf.Abs(x - halfS) / (sSize * 0.44f);
                float dy = Mathf.Abs(y - halfS) / (sSize * 0.44f);
                float val = dx + dy;
                if (val <= 1.0f)
                {
                    float alpha = Mathf.Pow(1f - val, 1.4f);
                    float core = (val < 0.2f) ? 1.0f : 0.88f;
                    sPixels[y * sSize + x] = new Color(core, core, core, Mathf.Clamp01(alpha));
                }
                else
                {
                    sPixels[y * sSize + x] = Color.clear;
                }
            }
        }
        shardTex.SetPixels(sPixels);
        shardTex.Apply();
        sparkleShardSprite = Sprite.Create(shardTex, new Rect(0, 0, sSize, sSize), new Vector2(0.5f, 0.5f), 64f);

        // 5. High-Definition Sonic Piercing Shock Sprite (128x128)
        int pW = 128;
        int pH = 128;
        Texture2D pTex = new Texture2D(pW, pH, TextureFormat.RGBA32, false);
        pTex.filterMode = FilterMode.Bilinear;
        Color[] pPixels = new Color[pW * pH];
        Vector2 pCenter = new Vector2(pW * 0.1f, pH * 0.5f);

        for (int y = 0; y < pH; y++)
        {
            for (int x = 0; x < pW; x++)
            {
                float dx = x - pCenter.x;
                float dy = Mathf.Abs(y - pCenter.y);
                if (dx > 0f && dy <= (dx * 0.45f) && dx <= 112f)
                {
                    float t = dx / 112f;
                    float lateralDist = dy / (dx * 0.45f);
                    float alpha = (1f - lateralDist) * Mathf.Sin(t * Mathf.PI);
                    pPixels[y * pW + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
                else
                {
                    pPixels[y * pW + x] = Color.clear;
                }
            }
        }
        pTex.SetPixels(pPixels);
        pTex.Apply();
        pierceShockSprite = Sprite.Create(pTex, new Rect(0, 0, pW, pH), new Vector2(0.1f, 0.5f), 64f);
    }
}