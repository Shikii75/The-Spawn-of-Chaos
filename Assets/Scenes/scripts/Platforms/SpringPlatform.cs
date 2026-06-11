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

        Rigidbody2D playerRb = collision.collider.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, bounceForce);
        }
    }
}

