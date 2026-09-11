using System.Collections;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// NyxarisShrineCage - Component for the Divine Crystal Shrine at the High Mountain Peak of TutorialScene.
/// Holds the goddess Nyxaris captive inside a glowing crystal structure.
/// Implements IDamageable: attacking the shrine plays hit flashes, and at 0 HP triggers explosive plasma shattering,
/// freeing Nyxaris to initiate her investigation arc dialogue and open the portal to the Cherry Blossom Forest (MountainPathScene).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class NyxarisShrineCage : MonoBehaviour, IDamageable
{
    [Header("Shrine Health & State")]
    public int health = 50;
    public bool isBroken = false;

    [Header("Nyxaris & Exit Portal")]
    public GameObject nyxarisNPCObject;
    public GameObject exitPortalObject; // entersign leading to MountainPathScene

    [Header("Visual Effects & Audio")]
    public Color crystalGlowColor = new Color(0.9f, 0.2f, 0.95f, 1f); // Neon Magenta/Pink
    public AudioClip shatterSound;
    public AudioClip freedomSound;

    private SpriteRenderer sr;
    private Collider2D col;
    private Color originalColor;
    private int currentHealth;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        originalColor = sr.color;
        currentHealth = health;

        if (exitPortalObject != null)
        {
            exitPortalObject.SetActive(false); // Locked until Nyxaris is freed
        }
    }

    // ── IDamageable Implementation ──────────────────────────────────────
    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        currentHealth -= damage;

        // Flash White/Magenta Highlight
        StartCoroutine(HitFlashRoutine());

        // Show Hit Reaction
        HitFeedbackManager.TriggerHitFeedback(transform, transform.position + Vector3.up * 1f, damage, false, EnemyHitType.MagicSpell);

        if (currentHealth <= 0)
        {
            ShatterShrineAndFreeNyxaris();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (sr == null) yield break;
        sr.color = Color.white * 1.8f;
        yield return new WaitForSeconds(0.08f);
        sr.color = originalColor;
    }

    public void ShatterShrineAndFreeNyxaris()
    {
        if (isBroken) return;
        isBroken = true;

        if (col != null) col.enabled = false;

        // 1. Play Shatter Audio
        if (shatterSound != null)
        {
            AudioSource.PlayClipAtPoint(shatterSound, transform.position);
        }

        // 2. Spawn Explosive Plasma Shatter FX
        SpawnPlasmaShatterFX();

        // 3. Spawn Loot Orbs
        OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 1f, 12);

        // 4. Hide Shrine Cage Mesh/Sprite
        if (sr != null) sr.enabled = false;

        // 5. Trigger Nyxaris Freedom & Narrative Dialogue
        StartCoroutine(NyxarisFreedomSequence());
    }

    private IEnumerator NyxarisFreedomSequence()
    {
        yield return new WaitForSeconds(0.4f);

        if (nyxarisNPCObject != null)
        {
            nyxarisNPCObject.SetActive(true);

            // Float Nyxaris down smoothly to ground level
            Vector3 startPos = nyxarisNPCObject.transform.position;
            Vector3 targetPos = new Vector3(startPos.x, transform.position.y - 0.5f, startPos.z);

            float elapsed = 0f;
            float duration = 1.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                nyxarisNPCObject.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            nyxarisNPCObject.transform.position = targetPos;
        }

        // Trigger Nyxaris Lore Dialogue Banner
        string dialogue = "NYXARIS: 'Thank you, mortal... My followers were mass-murdered across this realm in this timeline. " +
                          "Clues point to a killer hidden in the Cherry Blossom Forest mountain dōjōs—or something far darker. " +
                          "Enter the portal and begin the investigation!'";

        Debug.Log($"[NyxarisShrineCage] {dialogue}");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 pPos = player.transform.position;
            FloatingDamageNumber.SpawnText(pPos + Vector3.up * 2.5f, dialogue, new Color(0.95f, 0.4f, 1f, 1f));
        }

        yield return new WaitForSeconds(2.0f);

        // Unlock Exit Portal to MountainPathScene (Cherry Blossom Forest)
        if (exitPortalObject != null)
        {
            exitPortalObject.SetActive(true);

            // Configure entersign portal component if attached
            entersign sign = exitPortalObject.GetComponent<entersign>();
            if (sign != null)
            {
                sign.targetSceneName = "MountainPathScene";
                sign.targetSpawnPointName = "MountainPath_Entrance";
            }
        }
    }

    private void SpawnPlasmaShatterFX()
    {
        GameObject burstObj = new GameObject("Shrine_PlasmaShatter");
        burstObj.transform.position = transform.position + Vector3.up * 1f;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = burstObj.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        // Removed main.duration assignment to prevent Unity runtime error
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(crystalGlowColor, Color.white);
        main.gravityModifier = 0.8f;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;

        psr.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        Destroy(burstObj, 2f);
    }
}
