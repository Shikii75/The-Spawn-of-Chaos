using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpringPlatform : PlatformBase
{
    public float bounceForce = 16f;
    public string playerTag = "Player";

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag))
            return;

        // Verify the player landed on the top of the spring
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                Rigidbody2D playerRb = collision.collider.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, bounceForce);
                    break;
                }
            }
        }
    }
}

