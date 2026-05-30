using UnityEngine;

public class Platform : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f;  // Speed at which the platform moves up and down
    public float patrolRange = 5f; // How far above and below its original position

    private float topBoundary;
    private float bottomBoundary;
    void Start()
    {
        // Set the boundaries based on where the enemy starts in the scene
        topBoundary = transform.position.y + patrolRange;
        bottomBoundary = transform.position.y - patrolRange;
    }

    void Update()
    {
        // Calculate the current vertical position based on time and speed
        float yPosition = Mathf.Sin(Time.time * moveSpeed) * patrolRange;
        
        // Apply the calculated Y-position to the platform's transform
        transform.position += new Vector3(0, yPosition, 0);
    }
}

