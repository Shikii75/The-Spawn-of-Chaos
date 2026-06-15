using UnityEngine;

public class NPCFacePlayer : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Distance at which the NPC will start facing the player.")]
    public float faceRange = 5f;

    [Header("Flipping Method")]
    [Tooltip("If checked, it flips the SpriteRenderer.flipX. If unchecked, it flips the Transform's localScale.x (warning: might mirror child UI text).")]
    public bool useSpriteRendererFlip = true;

    [Tooltip("Does the NPC sprite face right by default in the original asset?")]
    public bool spriteFacesRightByDefault = true;

    [Tooltip("Check this if the NPC turns away from the player instead of facing them.")]
    public bool invertFlipping = false;

    private Transform playerTransform;
    private SpriteRenderer spriteRenderer;
    private bool facingRight;

    void Start()
    {
        // Find the player automatically in the scene
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null && useSpriteRendererFlip)
        {
            // If sprite renderer is missing, fallback to localScale flip
            useSpriteRendererFlip = false;
            Debug.LogWarning($"NPCFacePlayer on {gameObject.name}: No SpriteRenderer found, falling back to Transform local scale flip.");
        }

        // Initialize starting direction
        facingRight = spriteFacesRightByDefault;
    }

    void Update()
    {
        if (playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);

        if (distance <= faceRange)
        {
            // Check if player is to the right or left of the NPC
            float direction = playerTransform.position.x - transform.position.x;

            if (direction > 0.1f && !facingRight)
            {
                SetFacing(true);
            }
            else if (direction < -0.1f && facingRight)
            {
                SetFacing(false);
            }
        }
    }

    private void SetFacing(bool faceRight)
    {
        if (invertFlipping)
        {
            faceRight = !faceRight;
        }

        facingRight = faceRight;

        if (useSpriteRendererFlip && spriteRenderer != null)
        {
            // If the sprite faces right by default, we flipX when we want to face left
            spriteRenderer.flipX = spriteFacesRightByDefault ? !faceRight : faceRight;
        }
        else
        {
            // Flip using Transform local scale
            Vector3 scale = transform.localScale;
            // Force the sign of local scale to match the direction
            float absoluteScaleX = Mathf.Abs(scale.x);
            
            if (spriteFacesRightByDefault)
            {
                scale.x = faceRight ? absoluteScaleX : -absoluteScaleX;
            }
            else
            {
                scale.x = faceRight ? -absoluteScaleX : absoluteScaleX;
            }
            
            transform.localScale = scale;
        }
    }
}
