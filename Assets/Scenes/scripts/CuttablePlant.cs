using System.Collections;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// CuttablePlant - Interactive foliage component for cuttable plants (Bamboo, Cherry Blossoms, Grass, Vines).
/// Implements IDamageable to react to sword attacks, projectiles, and mob hits.
/// Slices into upper and lower fragments with dynamic physics velocity, rotational torque, leaf bursts, and loot drops.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class CuttablePlant : MonoBehaviour, IDamageable
{
    public enum PlantType
    {
        CherryBlossomBush,
        Bamboo,
        TallGrass,
        MountainFlower,
        Vine,
        Custom
    }

    [Header("Plant Configuration")]
    public PlantType plantType = PlantType.CherryBlossomBush;
    public int health = 1;
    [Range(0.1f, 1f)]
    public float cutHeightRatio = 0.5f; // Fraction from bottom where plant is sliced
    public bool enableSwayOnTouch = true;
    public bool autoRegrow = false;
    public float regrowDelay = 10f;

    [Header("Physics & VFX")]
    public float cutEjectionForce = 4f;
    public float cutTorque = 220f;
    public Color leafParticleColor = new Color(1f, 0.55f, 0.75f, 1f); // Default Cherry Blossom pink
    public int leafParticleCount = 16;
    public int lootOrbCount = 2;

    [Header("Optional Overrides")]
    public Sprite cutStumpSprite;
    public Sprite upperFragmentSprite;
    public AudioClip cutSoundEffect;

    private SpriteRenderer sr;
    private Collider2D col;
    private Sprite originalSprite;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Color originalColor;
    private bool isCut = false;
    private int currentHealth;

    // Sway animation variables
    private float swayAngle = 0f;
    private float swayVelocity = 0f;
    private Coroutine swayCoroutine;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        originalSprite = sr.sprite;
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
        originalColor = sr.color;
        currentHealth = health;

        ApplyPresetColors();
    }

    public void ApplyPresetColors()
    {
        switch (plantType)
        {
            case PlantType.CherryBlossomBush:
                leafParticleColor = new Color(1f, 0.55f, 0.75f, 1f); // Pink Petals
                break;
            case PlantType.Bamboo:
                leafParticleColor = new Color(0.45f, 0.75f, 0.35f, 1f); // Bamboo Green
                break;
            case PlantType.TallGrass:
                leafParticleColor = new Color(0.35f, 0.85f, 0.35f, 1f); // Fresh Grass Green
                break;
            case PlantType.MountainFlower:
                leafParticleColor = new Color(0.85f, 0.45f, 0.95f, 1f); // Purple Petals
                break;
            case PlantType.Vine:
                leafParticleColor = new Color(0.25f, 0.65f, 0.3f, 1f); // Dark Vine Green
                break;
        }
    }

    // ── IDamageable Implementation ──────────────────────────────────────
    public void TakeDamage(int damage)
    {
        if (isCut) return;

        currentHealth -= damage;

        // Play subtle sway/shake on partial hit
        TriggerSway(12f);

        if (currentHealth <= 0)
        {
            PerformCut();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enableSwayOnTouch || isCut) return;

        if (other.CompareTag("Player") || other.GetComponent<IDamageable>() != null)
        {
            float hitDir = Mathf.Sign(transform.position.x - other.transform.position.x);
            if (hitDir == 0) hitDir = 1f;
            TriggerSway(hitDir * 15f);
        }
    }

    public void TriggerSway(float initialImpulse)
    {
        if (isCut) return;
        swayVelocity = initialImpulse;
        if (swayCoroutine == null)
        {
            swayCoroutine = StartCoroutine(SwayRoutine());
        }
    }

    private IEnumerator SwayRoutine()
    {
        float stiffness = 180f;
        float damping = 8f;

        while (Mathf.Abs(swayAngle) > 0.1f || Mathf.Abs(swayVelocity) > 0.1f)
        {
            float force = -stiffness * swayAngle;
            swayVelocity += force * Time.deltaTime;
            swayVelocity -= damping * swayVelocity * Time.deltaTime;
            swayAngle += swayVelocity * Time.deltaTime;

            transform.localRotation = originalRotation * Quaternion.Euler(0, 0, swayAngle);
            yield return null;
        }

        transform.localRotation = originalRotation;
        swayAngle = 0f;
        swayVelocity = 0f;
        swayCoroutine = null;
    }

    public void PerformCut()
    {
        if (isCut) return;
        isCut = true;

        if (swayCoroutine != null)
        {
            StopCoroutine(swayCoroutine);
            swayCoroutine = null;
        }
        transform.localRotation = originalRotation;

        // 1. Determine cut direction from player position if available
        Vector2 cutDir = Vector2.up + Vector2.right * (Random.value > 0.5f ? 1f : -1f);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float playerSide = Mathf.Sign(transform.position.x - player.transform.position.x);
            cutDir = new Vector2(playerSide * 0.8f, 1.2f).normalized;
        }

        // 2. Spawn Leaf / Petal Particle Burst
        SpawnLeafBurstParticleSystem(cutDir);

        // 3. Create Flying Upper Fragment
        CreateUpperCutFragment(cutDir);

        // 4. Update Main Plant Object to Cut Stump
        if (cutStumpSprite != null)
        {
            sr.sprite = cutStumpSprite;
        }
        else
        {
            // Scale main plant down vertically to represent cut base
            transform.localScale = new Vector3(originalScale.x, originalScale.y * cutHeightRatio, originalScale.z);
            sr.color = new Color(originalColor.r * 0.8f, originalColor.g * 0.85f, originalColor.b * 0.8f, originalColor.a);
        }

        if (col != null) col.enabled = false;

        // 5. Play Cut Audio
        if (cutSoundEffect != null)
        {
            AudioSource.PlayClipAtPoint(cutSoundEffect, transform.position);
        }

        // 6. Spawn Loot Orbs
        if (lootOrbCount > 0)
        {
            OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 0.5f, lootOrbCount);
        }

        // 7. Auto Regrow handling
        if (autoRegrow)
        {
            StartCoroutine(RegrowRoutine());
        }
    }

    private void CreateUpperCutFragment(Vector2 cutDir)
    {
        GameObject fragment = new GameObject($"{gameObject.name}_TopCut");
        fragment.transform.position = transform.position + Vector3.up * (sr.bounds.size.y * (1f - cutHeightRatio) * 0.5f);
        fragment.transform.rotation = transform.rotation;
        fragment.transform.localScale = originalScale;

        SpriteRenderer fragSr = fragment.AddComponent<SpriteRenderer>();
        fragSr.sprite = upperFragmentSprite != null ? upperFragmentSprite : sr.sprite;
        fragSr.color = originalColor;
        fragSr.sortingLayerID = sr.sortingLayerID;
        fragSr.sortingOrder = sr.sortingOrder + 1;

        Rigidbody2D rb = fragment.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1.8f;
        rb.linearVelocity = cutDir * cutEjectionForce;
        rb.angularVelocity = (cutDir.x > 0 ? -1f : 1f) * cutTorque;

        PolygonCollider2D fragCol = fragment.AddComponent<PolygonCollider2D>();
        fragCol.isTrigger = true;

        StartCoroutine(FadeAndDestroyFragment(fragment, fragSr, 1.4f));
    }

    private IEnumerator FadeAndDestroyFragment(GameObject fragObj, SpriteRenderer fragSr, float duration)
    {
        float elapsed = 0f;
        Color startCol = fragSr.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            if (fragSr != null)
            {
                fragSr.color = new Color(startCol.r, startCol.g, startCol.b, alpha);
            }
            yield return null;
        }

        if (fragObj != null)
        {
            Destroy(fragObj);
        }
    }

    private void SpawnLeafBurstParticleSystem(Vector2 cutDir)
    {
        GameObject burstObj = new GameObject($"{gameObject.name}_LeafBurst");
        burstObj.transform.position = transform.position + Vector3.up * (sr.bounds.size.y * cutHeightRatio);

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = burstObj.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = leafParticleColor;
        main.gravityModifier = 0.8f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, leafParticleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;

        // Align cone with cut direction
        float angleDeg = Mathf.Atan2(cutDir.y, cutDir.x) * Mathf.Rad2Deg;
        burstObj.transform.rotation = Quaternion.Euler(0, 0, angleDeg - 90f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Simple default sprite particle shader
        psr.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burstObj, 1.5f);
    }

    private IEnumerator RegrowRoutine()
    {
        yield return new WaitForSeconds(regrowDelay);

        float elapsed = 0f;
        float growTime = 1.2f;
        sr.sprite = originalSprite;
        sr.color = originalColor;

        while (elapsed < growTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / growTime;
            transform.localScale = Vector3.Lerp(new Vector3(originalScale.x, originalScale.y * cutHeightRatio, originalScale.z), originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
        currentHealth = health;
        isCut = false;
        if (col != null) col.enabled = true;
    }
}
