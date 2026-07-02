using UnityEngine;

public class DojoGateController : MonoBehaviour
{
    [Header("Visual Settings")]
    public float slideSpeed = 5f;
    [Tooltip("Offset when the gate is fully closed (locked).")]
    public Vector3 closedOffset = Vector3.zero;
    [Tooltip("Offset when the gate is fully open (unlocked).")]
    public Vector3 openOffset = new Vector3(0f, 5f, 0f);

    private Vector3 startPosition;
    private Collider2D gateCollider;
    private bool isInitialized = false;

    void Awake()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized) return;
        startPosition = transform.position;
        gateCollider = GetComponent<Collider2D>();
        isInitialized = true;
    }

    public void CloseGate()
    {
        InitializeIfNeeded();
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(SlideTo(startPosition + closedOffset, true));
    }

    public void OpenGate()
    {
        InitializeIfNeeded();
        StopAllCoroutines();
        StartCoroutine(SlideTo(startPosition + openOffset, false));
    }

    private System.Collections.IEnumerator SlideTo(Vector3 targetPos, bool enableCollider)
    {
        if (gateCollider != null && !enableCollider)
        {
            gateCollider.enabled = false; // Turn off collision instantly when opening
        }

        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, Time.deltaTime * slideSpeed);
            yield return null;
        }

        transform.position = targetPos;

        if (gateCollider != null && enableCollider)
        {
            gateCollider.enabled = true;
        }

        if (!enableCollider)
        {
            // Fully open, deactivate visual object to save resources
            gameObject.SetActive(false);
        }
    }
}
