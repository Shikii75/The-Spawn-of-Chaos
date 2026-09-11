using System.Collections;
using UnityEngine;

[AddComponentMenu("Combat/Enemy Spawn FX")]
public class EnemySpawnFX : MonoBehaviour
{
    public enum SpawnStyle
    {
        NinjaSmokeDrop,
        PhantomDissolve,
        GroundRise,
        PhantomDash
    }

    [Header("Spawn Settings")]
    [Tooltip("Visual entry style for this enemy.")]
    public SpawnStyle spawnStyle = SpawnStyle.NinjaSmokeDrop;

    [Tooltip("Duration of the spawn entry animation in seconds.")]
    public float spawnDuration = 0.35f;

    [Tooltip("Height offset for NinjaSmokeDrop style.")]
    public float dropHeight = 3.5f;

    [Tooltip("Distance offset for PhantomDash style.")]
    public float dashDistance = 4f;

    [Header("Landing Juiciness")]
    [Tooltip("Squash and stretch scale magnitude on landing.")]
    public float squashMagnitude = 0.3f;

    [Tooltip("Spawn smoke/dust particles on entry/landing.")]
    public bool spawnParticles = true;

    private SpriteRenderer sr;
    private Collider2D col;
    private Rigidbody2D rb;
    private Vector3 targetLocalScale = Vector3.one;
    private Vector3 targetLocalPosition = Vector3.zero;
    private Color originalColor = Color.white;
    private bool isSpawning = false;

    public bool IsSpawning => isSpawning;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        if (sr != null)
        {
            originalColor = sr.color;
            targetLocalScale = sr.transform.localScale;
            targetLocalPosition = sr.transform.localPosition;
        }
    }

    void OnEnable()
    {
        StartCoroutine(PlaySpawnSequence());
    }

    public IEnumerator PlaySpawnSequence()
    {
        isSpawning = true;

        // 1. Temporarily disable collider & AI scripts during appearance
        if (col != null) col.enabled = false;
        SetAIScriptsActive(false);

        Vector3 finalPos = transform.position;

        // Execute selected procedural style
        switch (spawnStyle)
        {
            case SpawnStyle.NinjaSmokeDrop:
                yield return StartCoroutine(RoutineNinjaSmokeDrop(finalPos));
                break;
            case SpawnStyle.PhantomDissolve:
                yield return StartCoroutine(RoutinePhantomDissolve(finalPos));
                break;
            case SpawnStyle.GroundRise:
                yield return StartCoroutine(RoutineGroundRise(finalPos));
                break;
            case SpawnStyle.PhantomDash:
                yield return StartCoroutine(RoutinePhantomDash(finalPos));
                break;
        }

        // 2. Perform squash-and-stretch landing impact
        yield return StartCoroutine(RoutineLandingImpact());

        // 3. Restore collision and enable AI
        if (col != null) col.enabled = true;
        SetAIScriptsActive(true);

        isSpawning = false;
    }

    private IEnumerator RoutineNinjaSmokeDrop(Vector3 finalPos)
    {
        SpawnSmokeParticles(finalPos + Vector3.up * (dropHeight * 0.5f), new Color(0.35f, 0.35f, 0.4f, 0.85f));

        transform.position = finalPos + Vector3.up * dropHeight;
        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnDuration;
            // Ease in quad (gravity fall)
            float easeT = t * t;
            transform.position = Vector3.Lerp(finalPos + Vector3.up * dropHeight, finalPos, easeT);
            yield return null;
        }

        transform.position = finalPos;
        SpawnSmokeParticles(finalPos, new Color(0.6f, 0.55f, 0.5f, 0.9f));
    }

    private IEnumerator RoutinePhantomDissolve(Vector3 finalPos)
    {
        transform.position = finalPos;
        float elapsed = 0f;

        if (sr != null)
        {
            sr.transform.localScale = Vector3.zero;
            sr.color = new Color(0.15f, 0.1f, 0.25f, 0f);
        }

        SpawnSmokeParticles(finalPos, new Color(0.55f, 0.25f, 0.85f, 0.8f));

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnDuration;
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease out sine

            if (sr != null)
            {
                sr.transform.localScale = Vector3.Lerp(Vector3.zero, targetLocalScale, easeT);
                sr.color = Color.Lerp(new Color(0.15f, 0.1f, 0.25f, 0f), originalColor, easeT);
            }
            yield return null;
        }

        if (sr != null)
        {
            sr.transform.localScale = targetLocalScale;
            sr.color = originalColor;
        }
    }

    private IEnumerator RoutineGroundRise(Vector3 finalPos)
    {
        Vector3 startBelow = finalPos + Vector3.down * 1.5f;
        transform.position = startBelow;
        float elapsed = 0f;

        if (sr != null)
        {
            sr.color = new Color(originalColor.r * 0.3f, originalColor.g * 0.3f, originalColor.b * 0.3f, 0.2f);
        }

        SpawnSmokeParticles(finalPos + Vector3.down * 0.2f, new Color(0.45f, 0.4f, 0.35f, 0.85f));

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnDuration;
            float easeT = 1f - Mathf.Cos(t * Mathf.PI * 0.5f); // Ease out sine

            transform.position = Vector3.Lerp(startBelow, finalPos, easeT);
            if (sr != null)
            {
                sr.color = Color.Lerp(new Color(originalColor.r * 0.3f, originalColor.g * 0.3f, originalColor.b * 0.3f, 0.2f), originalColor, easeT);
            }
            yield return null;
        }

        transform.position = finalPos;
        if (sr != null) sr.color = originalColor;
    }

    private IEnumerator RoutinePhantomDash(Vector3 finalPos)
    {
        float dir = Random.value > 0.5f ? 1f : -1f;
        Vector3 startOffset = finalPos + Vector3.right * (dashDistance * dir);
        transform.position = startOffset;
        float elapsed = 0f;

        SpawnSmokeParticles(startOffset, new Color(0.2f, 0.75f, 1f, 0.8f));

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnDuration;
            float easeT = t * (2f - t); // Ease out quad

            transform.position = Vector3.Lerp(startOffset, finalPos, easeT);
            if (sr != null)
            {
                Color c = originalColor;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                sr.color = c;
            }
            yield return null;
        }

        transform.position = finalPos;
        if (sr != null) sr.color = originalColor;
    }

    private IEnumerator RoutineLandingImpact()
    {
        if (sr == null) yield break;

        // Squash & Stretch
        float squashTime = 0.08f;
        float elapsed = 0f;

        Vector3 squashedScale = new Vector3(targetLocalScale.x * (1f + squashMagnitude), targetLocalScale.y * (1f - squashMagnitude), targetLocalScale.z);

        while (elapsed < squashTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / squashTime;
            sr.transform.localScale = Vector3.Lerp(targetLocalScale, squashedScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < squashTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / squashTime;
            sr.transform.localScale = Vector3.Lerp(squashedScale, targetLocalScale, t);
            yield return null;
        }

        sr.transform.localScale = targetLocalScale;
        sr.transform.localPosition = targetLocalPosition;
    }

    private void SetAIScriptsActive(bool active)
    {
        // Disable/Enable AI behaviors during spawn
        UniversalEnemy universal = GetComponent<UniversalEnemy>();
        if (universal != null) universal.enabled = active;

        FatStrawhatAI fat = GetComponent<FatStrawhatAI>();
        if (fat != null) fat.enabled = active;

        FemaleStrawhatAI female = GetComponent<FemaleStrawhatAI>();
        if (female != null) female.enabled = active;

        EnemyPatrol2D patrol = GetComponent<EnemyPatrol2D>();
        if (patrol != null) patrol.enabled = active;

        BaseMob baseMob = GetComponent<BaseMob>();
        if (baseMob != null) baseMob.enabled = active;
    }

    private void SpawnSmokeParticles(Vector3 position, Color color)
    {
        if (!spawnParticles) return;

        GameObject pObj = new GameObject("SpawnSmokeFX");
        pObj.transform.position = position;

        ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        // Removed main.duration assignment to prevent Unity runtime error
        main.loop = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 2.5f;
        main.startSize = 0.4f;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        ParticleSystemRenderer psr = pObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null && sr != null)
        {
            psr.sortingLayerID = sr.sortingLayerID;
            psr.sortingOrder = sr.sortingOrder + 1;
        }

        ps.Play();
    }
}
