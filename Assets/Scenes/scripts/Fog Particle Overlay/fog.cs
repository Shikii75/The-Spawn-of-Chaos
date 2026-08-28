using UnityEngine;

public class Fog : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 0.5f;
    [SerializeField] private float wrapDistance = 60f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        transform.Translate(Vector3.right * moveSpeed * Time.deltaTime);

        // Wrap around when drifted beyond wrapDistance to avoid runaway coordinates / Invalid AABB
        if (wrapDistance > 0f && Mathf.Abs(transform.position.x - startPosition.x) > wrapDistance)
        {
            transform.position = startPosition;
        }
    }
}