using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(Animator), typeof(Collider2D))]
public class SpiderBoss : MonoBehaviour
{
    public float roamSpeed = 2.0f;
    public Transform[] roamPoints;
    public Transform webSpawnPoint;
    public GameObject webProjectilePrefab;
    public GameObject spiderlingPrefab;
    public int maxPhase = 3;
    public float stageTwoHealthThreshold = 0.65f;
    public float stageThreeHealthThreshold = 0.35f;
    public float attackDelay = 2.0f;
    public float stageChangeDelay = 1.5f;

    private Health health;
    private Animator animator;
    private int currentPhase = 1;
    private bool isAttacking;
    private bool isStunned;
    private int roamIndex;
    private float nextActionTime;

    void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        if (roamPoints == null || roamPoints.Length == 0)
        {
            Debug.LogWarning("SpiderBoss needs roam points assigned.");
        }
    }

    void OnEnable()
    {
        health.onDamageTaken += OnDamageTaken;
        health.onDeath += OnDeath;
    }

    void OnDisable()
    {
        health.onDamageTaken -= OnDamageTaken;
        health.onDeath -= OnDeath;
    }

    void Update()
    {
        UpdatePhase();
        if (isAttacking || isStunned)
            return;

        if (Time.time >= nextActionTime)
        {
            ChooseAttack();
        }
        else
        {
            Roam();
        }
    }

    private void UpdatePhase()
    {
        float ratio = health.CurrentHealth / health.MaxHealth;
        int phase = 1;
        if (ratio <= stageThreeHealthThreshold)
            phase = 3;
        else if (ratio <= stageTwoHealthThreshold)
            phase = 2;

        if (phase != currentPhase)
        {
            currentPhase = phase;
            animator.SetInteger("Phase", currentPhase);
            StartCoroutine(PhaseTransition());
        }
    }

    private IEnumerator PhaseTransition()
    {
        isStunned = true;
        animator.SetTrigger("PhaseShift");
        yield return new WaitForSeconds(stageChangeDelay);
        isStunned = false;
    }

    private void ChooseAttack()
    {
        isAttacking = true;
        nextActionTime = Time.time + attackDelay;

        int attackIndex = Random.Range(1, 5);
        switch (attackIndex)
        {
            case 1:
                StartCoroutine(ClawSwipe());
                break;
            case 2:
                StartCoroutine(WebSpray());
                break;
            case 3:
                StartCoroutine(SpiderlingSpawn());
                break;
            case 4:
                StartCoroutine(FloorStomp());
                break;
            default:
                StartCoroutine(ClawSwipe());
                break;
        }
    }

    private void Roam()
    {
        if (roamPoints == null || roamPoints.Length == 0)
            return;

        Transform target = roamPoints[roamIndex];
        transform.position = Vector3.MoveTowards(transform.position, target.position, roamSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < 0.2f)
        {
            roamIndex = (roamIndex + 1) % roamPoints.Length;
        }
    }

    private IEnumerator ClawSwipe()
    {
        animator.SetTrigger("ClawSwipe");
        yield return new WaitForSeconds(0.75f);
        isAttacking = false;
    }

    private IEnumerator WebSpray()
    {
        animator.SetTrigger("WebSpray");
        yield return new WaitForSeconds(0.5f);
        SpawnWebProjectile();
        yield return new WaitForSeconds(0.7f);
        isAttacking = false;
    }

    private IEnumerator SpiderlingSpawn()
    {
        animator.SetTrigger("Spawnlings");
        yield return new WaitForSeconds(0.8f);
        SpawnSpiderlings(currentPhase);
        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    private IEnumerator FloorStomp()
    {
        animator.SetTrigger("Stomp");
        yield return new WaitForSeconds(0.65f);
        // Optional: apply area-of-effect damage later via trigger.
        isAttacking = false;
    }

    private void SpawnWebProjectile()
    {
        if (webProjectilePrefab == null || webSpawnPoint == null)
            return;

        Instantiate(webProjectilePrefab, webSpawnPoint.position, Quaternion.identity);
    }

    private void SpawnSpiderlings(int count)
    {
        if (spiderlingPrefab == null || webSpawnPoint == null)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3((i - 1) * 0.8f, -0.5f, 0);
            Instantiate(spiderlingPrefab, webSpawnPoint.position + offset, Quaternion.identity);
        }
    }

    private void OnDamageTaken(int damage)
    {
        animator.SetTrigger("Hurt");
    }

    private void OnDeath()
    {
        animator.SetTrigger("Death");
        enabled = false;
    }
}
