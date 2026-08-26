using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// Fully Code-Generated Tutorial Mob: Chaos Shade / Void Stalker.
/// 100% procedural sprites, animations, glowing crimson eyes, shadow tendrils,
/// attack telegraphing, hit feedback, floating health bar, and death particle burst.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class TutorialShadowMob : MonoBehaviour, IDamageable
{
    public enum State
    {
        Spawning,
        Patrolling,
        Chasing,
        Telegraphing,
        Attacking,
        HitStun,
        Dead
    }

    [Header("State")]
    public State currentState = State.Spawning;

    [Header("Stats")]
    public int maxHealth = 40;
    public int currentHealth = 40;
    public int attackDamage = 8;
    public float moveSpeed = 2.5f;
    public float chaseSpeed = 4.0f;
    public float detectionRange = 8.0f;
    public float attackRange = 1.9f;
    public float attackCooldown = 2.0f;
    public float telegraphDuration = 0.75f;

    [Header("Visual Palette")]
    public Color coreColor = new Color(0.08f, 0.02f, 0.14f, 1.0f);
    public Color auraColor = new Color(0.75f, 0.08f, 0.95f, 0.85f);
    public Color eyeColor = new Color(1.0f, 0.15f, 0.35f, 1.0f);
    public Color eyeTelegraphColor = new Color(1.0f, 0.95f, 0.1f, 1.0f);
    public Color clawColor = new Color(0.9f, 0.2f, 1.0f, 0.9f);

    // References
    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Transform playerTransform;
    private TutorialCombatArea parentArea;

    // Procedural Visual Transforms & Renderers
    private Transform visualRoot;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer auraRenderer;
    private SpriteRenderer eyeLeftRenderer;
    private SpriteRenderer eyeRightRenderer;
    private readonly List<Transform> tendrils = new List<Transform>();
    private readonly List<SpriteRenderer> tendrilRenderers = new List<SpriteRenderer>();

    // Floating HP Bar
    private Transform hpBarRoot;
    private SpriteRenderer hpBarBg;
    private SpriteRenderer hpBarFill;

    // Timers & Combat
    private Vector3 spawnOrigin;
    private float patrolTimer = 0f;
    private int patrolDirection = 1;
    private float attackTimer = 0f;
    private float stateTimer = 0f;
    private bool isFacingRight = true;
    private float floatPhase = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();

        gameObject.tag = "enemy";
        rb.gravityScale = 2.0f;
        rb.freezeRotation = true;

        col.size = new Vector2(1.1f, 1.8f);
        col.offset = new Vector2(0f, 0.9f);

        currentHealth = maxHealth;
        spawnOrigin = transform.position;
        floatPhase = Random.Range(0f, Mathf.PI * 2f);

        BuildProceduralMobVisuals();
        BuildFloatingHealthBar();
    }

    void Start()
    {
        FindPlayer();
        StartCoroutine(SpawnPortalRoutine());
    }

    public void Setup(TutorialCombatArea area, Vector3 spawnPos)
    {
        parentArea = area;
        spawnOrigin = spawnPos;
        transform.position = spawnPos;
    }

    void FindPlayer()
    {
        if (move.Instance != null) playerTransform = move.Instance.transform;
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    void Update()
    {
        AnimateProceduralVisuals();

        if (currentState == State.Dead || currentState == State.Spawning) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        attackTimer -= Time.deltaTime;
        stateTimer -= Time.deltaTime;

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        float xDiffToPlayer = playerTransform.position.x - transform.position.x;

        switch (currentState)
        {
            case State.Patrolling:
                UpdatePatrolMovement();
                if (distToPlayer <= detectionRange)
                {
                    currentState = State.Chasing;
                }
                break;

            case State.Chasing:
                SetFacing(xDiffToPlayer > 0);

                if (distToPlayer <= attackRange && attackTimer <= 0f)
                {
                    StartTelegraph();
                }
                else if (distToPlayer > detectionRange * 1.5f)
                {
                    currentState = State.Patrolling;
                }
                else
                {
                    float dir = Mathf.Sign(xDiffToPlayer);
                    rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
                }
                break;

            case State.Telegraphing:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                float p = Mathf.PingPong(Time.time * 16f, 1f);
                if (eyeLeftRenderer != null) eyeLeftRenderer.color = Color.Lerp(eyeColor, eyeTelegraphColor, p);
                if (eyeRightRenderer != null) eyeRightRenderer.color = Color.Lerp(eyeColor, eyeTelegraphColor, p);

                if (stateTimer <= 0f)
                {
                    ExecuteShadowStrike();
                }
                break;

            case State.Attacking:
                if (stateTimer <= 0f)
                {
                    currentState = State.Chasing;
                    attackTimer = attackCooldown;
                }
                break;

            case State.HitStun:
                if (stateTimer <= 0f)
                {
                    currentState = State.Chasing;
                }
                break;
        }

        UpdateHealthBarVisual();
    }

    private void UpdatePatrolMovement()
    {
        patrolTimer += Time.deltaTime;
        if (patrolTimer >= 2.2f)
        {
            patrolTimer = 0f;
            patrolDirection = (transform.position.x > spawnOrigin.x) ? -1 : 1;
        }

        SetFacing(patrolDirection > 0);
        rb.linearVelocity = new Vector2(patrolDirection * moveSpeed, rb.linearVelocity.y);
    }

    private void SetFacing(bool right)
    {
        isFacingRight = right;
        if (visualRoot != null)
        {
            Vector3 s = visualRoot.localScale;
            s.x = right ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            visualRoot.localScale = s;
        }
    }

    private void StartTelegraph()
    {
        currentState = State.Telegraphing;
        stateTimer = telegraphDuration;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void ExecuteShadowStrike()
    {
        currentState = State.Attacking;
        stateTimer = 0.35f;

        float dir = isFacingRight ? 1f : -1f;
        Vector2 slashOrigin = (Vector2)transform.position + new Vector2(dir * 1.3f, 0.8f);

        // Lunge forward slightly
        rb.linearVelocity = new Vector2(dir * 5.5f, 1.5f);

        // Check player hit
        Collider2D[] hits = Physics2D.OverlapCircleAll(slashOrigin, 1.2f);
        foreach (var h in hits)
        {
            if (h.CompareTag("Player") || h.GetComponent<Health>() != null)
            {
                var pH = h.GetComponent<Health>() ?? h.GetComponentInParent<Health>();
                if (pH != null)
                {
                    pH.TakeDamage(attackDamage);
                }
            }
        }

        StartCoroutine(SpawnSlashArc(slashOrigin, dir));
    }

    private IEnumerator SpawnSlashArc(Vector3 pos, float dir)
    {
        GameObject arcGO = new GameObject("ChaosSlashArc");
        arcGO.transform.position = pos;
        arcGO.transform.localScale = new Vector3(dir, 1f, 1f);
        SpriteRenderer sr = arcGO.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSlashSprite(64);
        sr.color = clawColor;
        sr.sortingOrder = 45;

        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float p = t / 0.22f;
            arcGO.transform.localScale = new Vector3(dir * Mathf.Lerp(0.5f, 1.8f, p), Mathf.Lerp(0.5f, 1.8f, p), 1f);
            sr.color = new Color(clawColor.r, clawColor.g, clawColor.b, 1f - p);
            yield return null;
        }
        Destroy(arcGO);
    }

    #region IDamageable

    public void TakeDamage(int damage)
    {
        if (currentState == State.Dead || currentState == State.Spawning) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        StartCoroutine(HitFlash());
        HitFeedbackManager.TriggerHitFeedback(transform, transform.position + Vector3.up * 0.8f, damage, false, EnemyHitType.PhysicalMelee);

        // Knockback away from attacker
        if (playerTransform != null && rb != null)
        {
            float kDir = Mathf.Sign(transform.position.x - playerTransform.position.x);
            rb.linearVelocity = new Vector2(kDir * 5.0f, 3.5f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            currentState = State.HitStun;
            stateTimer = 0.25f;
        }
    }

    private IEnumerator HitFlash()
    {
        if (coreRenderer != null) coreRenderer.color = Color.white;
        if (auraRenderer != null) auraRenderer.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        if (coreRenderer != null) coreRenderer.color = coreColor;
        if (auraRenderer != null) auraRenderer.color = auraColor;
    }

    private void Die()
    {
        currentState = State.Dead;
        rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;

        // Spawn loot drops
        OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 0.8f, 3);

        if (parentArea != null)
        {
            parentArea.OnMobDefeated(this);
        }

        StartCoroutine(DeathBurstRoutine());
    }

    private IEnumerator DeathBurstRoutine()
    {
        float t = 0f;
        Vector3 initialScale = visualRoot.localScale;

        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float p = t / 0.4f;

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.Lerp(initialScale, Vector3.zero, p);
                visualRoot.Rotate(0f, 0f, 540f * Time.deltaTime);
            }

            if (auraRenderer != null)
            {
                Color c = auraColor;
                c.a = 1f - p;
                auraRenderer.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    #endregion

    #region Procedural Visuals Generation

    private void BuildProceduralMobVisuals()
    {
        GameObject vRoot = new GameObject("VisualRoot");
        vRoot.transform.SetParent(transform, false);
        visualRoot = vRoot.transform;

        Sprite capsuleSprite = CreateCapsuleSprite(64, 96);
        Sprite circleSprite = CreateCircleSprite(32);
        Sprite tendrilSprite = CreateTendrilSprite(32, 64);

        // 1. Purple Outer Aura
        GameObject auraGO = new GameObject("Aura");
        auraGO.transform.SetParent(visualRoot, false);
        auraGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        auraRenderer = auraGO.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = capsuleSprite;
        auraRenderer.color = auraColor;
        auraRenderer.sortingOrder = 30;
        auraGO.transform.localScale = new Vector3(1.3f, 1.2f, 1f);

        // 2. Black Void Core
        GameObject coreGO = new GameObject("Core");
        coreGO.transform.SetParent(visualRoot, false);
        coreGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        coreRenderer = coreGO.AddComponent<SpriteRenderer>();
        coreRenderer.sprite = capsuleSprite;
        coreRenderer.color = coreColor;
        coreRenderer.sortingOrder = 31;
        coreGO.transform.localScale = Vector3.one;

        // 3. Glowing Crimson Eyes
        GameObject eyeL = new GameObject("EyeL");
        eyeL.transform.SetParent(visualRoot, false);
        eyeL.transform.localPosition = new Vector3(0.18f, 1.25f, 0f);
        eyeL.transform.localScale = new Vector3(0.24f, 0.12f, 1f);
        eyeLeftRenderer = eyeL.AddComponent<SpriteRenderer>();
        eyeLeftRenderer.sprite = circleSprite;
        eyeLeftRenderer.color = eyeColor;
        eyeLeftRenderer.sortingOrder = 33;

        GameObject eyeR = new GameObject("EyeR");
        eyeR.transform.SetParent(visualRoot, false);
        eyeR.transform.localPosition = new Vector3(0.38f, 1.25f, 0f);
        eyeR.transform.localScale = new Vector3(0.18f, 0.10f, 1f);
        eyeRightRenderer = eyeR.AddComponent<SpriteRenderer>();
        eyeRightRenderer.sprite = circleSprite;
        eyeRightRenderer.color = eyeColor;
        eyeRightRenderer.sortingOrder = 33;

        // 4. Orbiting Shadow Tendrils (4 Claws)
        for (int i = 0; i < 4; i++)
        {
            GameObject tGO = new GameObject($"Tendril_{i}");
            tGO.transform.SetParent(visualRoot, false);
            tGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            SpriteRenderer tr = tGO.AddComponent<SpriteRenderer>();
            tr.sprite = tendrilSprite;
            tr.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.75f);
            tr.sortingOrder = 29;
            tGO.transform.localScale = Vector3.one * 0.45f;

            tendrils.Add(tGO.transform);
            tendrilRenderers.Add(tr);
        }
    }

    private void AnimateProceduralVisuals()
    {
        if (visualRoot == null) return;

        // Subtle hover/breath
        float b = Mathf.Sin(Time.time * 3.5f + floatPhase) * 0.05f;
        if (coreRenderer != null) coreRenderer.transform.localScale = new Vector3(1f + b, 1f - b, 1f);
        if (auraRenderer != null) auraRenderer.transform.localScale = new Vector3(1.3f - b, 1.2f + b, 1f);

        // Tendril wave animation
        for (int i = 0; i < tendrils.Count; i++)
        {
            if (tendrils[i] == null) continue;
            float angle = Time.time * 2.5f + (i * Mathf.PI * 0.5f) + floatPhase;
            float radX = 0.55f + Mathf.Sin(Time.time * 4f + i) * 0.1f;
            float radY = 0.45f + Mathf.Cos(Time.time * 3.5f + i) * 0.1f;

            tendrils[i].localPosition = new Vector3(Mathf.Cos(angle) * radX, 0.9f + Mathf.Sin(angle) * radY, 0f);
            tendrils[i].localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
        }
    }

    private void BuildFloatingHealthBar()
    {
        GameObject hpGO = new GameObject("FloatingHPBar");
        hpGO.transform.SetParent(transform, false);
        hpGO.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        hpBarRoot = hpGO.transform;

        Sprite boxSprite = CreateBoxSprite(32, 8);

        GameObject bg = new GameObject("Bg");
        bg.transform.SetParent(hpBarRoot, false);
        hpBarBg = bg.AddComponent<SpriteRenderer>();
        hpBarBg.sprite = boxSprite;
        hpBarBg.color = new Color(0.06f, 0.02f, 0.12f, 0.9f);
        hpBarBg.sortingOrder = 40;
        bg.transform.localScale = new Vector3(1.2f, 0.32f, 1f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(hpBarRoot, false);
        hpBarFill = fill.AddComponent<SpriteRenderer>();
        hpBarFill.sprite = boxSprite;
        hpBarFill.color = new Color(0.95f, 0.15f, 0.45f, 0.95f);
        hpBarFill.sortingOrder = 41;
        fill.transform.localScale = new Vector3(1.15f, 0.24f, 1f);

        hpBarRoot.gameObject.SetActive(false);
    }

    private void UpdateHealthBarVisual()
    {
        if (hpBarFill == null) return;
        float pct = Mathf.Clamp01((float)currentHealth / maxHealth);
        hpBarFill.transform.localScale = new Vector3(1.15f * pct, 0.24f, 1f);
        hpBarFill.transform.localPosition = new Vector3(-0.575f * (1f - pct), 0f, 0f);

        if (hpBarRoot != null)
        {
            hpBarRoot.gameObject.SetActive(currentHealth < maxHealth && currentHealth > 0);
        }
    }

    private IEnumerator SpawnPortalRoutine()
    {
        currentState = State.Spawning;
        visualRoot.localScale = Vector3.zero;

        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float p = t / 0.5f;
            visualRoot.localScale = Vector3.one * Mathf.Lerp(0f, 1f, p);
            yield return null;
        }

        currentState = State.Patrolling;
    }

    #endregion

    #region Procedural Sprite Generators

    private Sprite CreateCapsuleSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        float rx = w * 0.5f;
        float ry = h * 0.5f;
        Vector2 c = new Vector2(rx, ry);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - c.x) / rx;
                float dy = (y - c.y) / ry;
                float d = dx * dx + dy * dy;
                float a = Mathf.Clamp01((1f - d) * 3.5f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 48f);
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        Vector2 c = Vector2.one * (size * 0.5f);
        float r = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01((r - d) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 48f);
    }

    private Sprite CreateTendrilSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / h;
            float widthAtY = (1f - t) * (w * 0.5f);
            for (int x = 0; x < w; x++)
            {
                float distFromCenter = Mathf.Abs(x - (w * 0.5f));
                float a = Mathf.Clamp01((widthAtY - distFromCenter) / 1.5f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.1f), 48f);
    }

    private Sprite CreateBoxSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 48f);
    }

    private Sprite CreateSlashSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        Vector2 c = Vector2.one * (size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float angle = Mathf.Atan2(y - c.y, x - c.x);
                float arc = Mathf.Clamp01(Mathf.Sin(angle * 1.5f));
                float a = Mathf.Clamp01((1f - Mathf.Abs(d - 0.7f) * 4f)) * arc;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 48f);
    }

    #endregion
}
