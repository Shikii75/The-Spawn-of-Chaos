using System.Collections;
using UnityEngine;

/// <summary>
/// Hazard spawned during Tsuchigumo's Ground Slam.
/// Displays a ground shadow telegraph before falling rapidly from the cave ceiling.
/// Deals damage and creates an impact dust shockwave on ground collision.
/// </summary>
public class FallingBoulder : MonoBehaviour
{
    [Header("Damage & Physics")]
    public int damage = 25;
    public float fallGravity = 4.5f;
    public float maxFallSpeed = 22f;
    public float impactRadius = 1.6f;
    public float lifeTime = 5f;

    [Header("Telegraph Indicator")]
    public float telegraphDuration = 0.65f;
    public Color shadowColor = new Color(0.35f, 0.1f, 0.5f, 0.65f);

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private GameObject groundShadow;
    private Vector2 impactPosition;
    private bool hasImpacted = false;

    public static void Spawn(Vector2 targetGroundPos, float spawnHeight = 12f, int boulderDamage = 25)
    {
        GameObject boulderObj = new GameObject("FallingBoulder_Amethyst");
        boulderObj.transform.position = new Vector3(targetGroundPos.x, targetGroundPos.y + spawnHeight, 0f);

        SpriteRenderer renderer = boulderObj.AddComponent<SpriteRenderer>();
        Sprite boulderSprite = Resources.Load<Sprite>("Hazards/shadow_boulder");
        if (boulderSprite == null)
        {
#if UNITY_EDITOR
            boulderSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Hazards/shadow_boulder.png");
#endif
        }
        renderer.sprite = boulderSprite;
        renderer.sortingOrder = 8;

        boulderObj.transform.localScale = Vector3.one * Random.Range(1.4f, 2.0f);

        FallingBoulder fb = boulderObj.AddComponent<FallingBoulder>();
        fb.damage = boulderDamage;
        fb.impactPosition = targetGroundPos;
    }

    private void Awake()
    {
        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic = true; // wait for telegraph
        rb.gravityScale = 0f;

        CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
        circle.radius = 0.45f;
        circle.isTrigger = true;
        col = circle;

        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        StartCoroutine(ExecuteFallingSequence());
        Destroy(gameObject, lifeTime);
    }

    private IEnumerator ExecuteFallingSequence()
    {
        // 1. Create ground shadow indicator
        CreateGroundShadow();

        // 2. Hide or suspend boulder during telegraph
        if (sr != null) sr.enabled = false;

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / telegraphDuration;
            if (groundShadow != null)
            {
                // Shadow expands and darkens as boulder nears drop time
                float scale = Mathf.Lerp(0.3f, 1.3f, t);
                groundShadow.transform.localScale = new Vector3(scale, scale * 0.4f, 1f);
                SpriteRenderer shadowSR = groundShadow.GetComponent<SpriteRenderer>();
                if (shadowSR != null)
                {
                    shadowSR.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, Mathf.Lerp(0.2f, shadowColor.a, t));
                }
            }
            yield return null;
        }

        // 3. Enable boulder and start dropping
        if (sr != null) sr.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.gravityScale = fallGravity;
            rb.linearVelocity = new Vector2(Random.Range(-0.5f, 0.5f), -8f);
        }

        // Add subtle rotation during fall
        float rotSpeed = Random.Range(-180f, 180f);
        while (!hasImpacted)
        {
            transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

            // Clamp max fall speed
            if (rb != null && rb.linearVelocity.y < -maxFallSpeed)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
            }

            // Fallback ground height check
            if (transform.position.y <= impactPosition.y + 0.2f)
            {
                TriggerImpact();
                yield break;
            }

            yield return null;
        }
    }

    private void CreateGroundShadow()
    {
        groundShadow = new GameObject("Boulder_GroundShadow");
        groundShadow.transform.position = new Vector3(impactPosition.x, impactPosition.y + 0.05f, 0f);

        SpriteRenderer shadowSR = groundShadow.AddComponent<SpriteRenderer>();
        shadowSR.sprite = Resources.Load<Sprite>("Hazards/shadow_boulder");
        if (shadowSR.sprite == null)
        {
#if UNITY_EDITOR
            shadowSR.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Hazards/shadow_boulder.png");
#endif
        }
        shadowSR.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0.2f);
        shadowSR.sortingOrder = 4;
        groundShadow.transform.localScale = new Vector3(0.3f, 0.12f, 1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasImpacted) return;

        if (other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
            TriggerImpact();
        }
        else if (!other.isTrigger && !other.CompareTag("enemy") && other.gameObject != gameObject)
        {
            TriggerImpact();
        }
    }

    private void TriggerImpact()
    {
        if (hasImpacted) return;
        hasImpacted = true;

        if (groundShadow != null)
        {
            Destroy(groundShadow);
        }

        // Damage any entities within impact radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, impactRadius);
        foreach (var h in hits)
        {
            if (h.CompareTag("Player"))
            {
                Health ph = h.GetComponent<Health>();
                if (ph != null) ph.TakeDamage(damage);
            }
        }

        // Camera impact shake
        CameraShakeManager.Shake(0.2f, 0.12f);

        // Create impact burst particles
        CreateImpactDebris();

        Destroy(gameObject);
    }

    private void CreateImpactDebris()
    {
        GameObject debrisGO = new GameObject("BoulderImpactFX");
        debrisGO.transform.position = transform.position;

        ParticleSystem ps = debrisGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.45f;
        main.startSpeed = 7f;
        main.startSize = 0.25f;
        main.startColor = new Color(0.7f, 0.35f, 0.95f, 0.9f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        ps.Play();
    }

    private void OnDestroy()
    {
        if (groundShadow != null)
        {
            Destroy(groundShadow);
        }
    }
}
