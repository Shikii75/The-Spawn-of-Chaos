using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinItem : MonoBehaviour
{
    [Header("Coin Properties")]
    public int coinValue = 10;
    
    [Header("Magnetism Settings")]
    public float magnetRadius = 4.0f;
    public float magnetSpeed = 8.0f;

    private Transform playerTransform;
    private bool isMagnetizing = false;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform != null)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            
            if (distance <= magnetRadius)
            {
                isMagnetizing = true;
            }

            if (isMagnetizing)
            {
                // Disable gravity if rigid body is present to allow smooth float towards player
                if (rb != null)
                {
                    rb.gravityScale = 0f;
                    rb.linearVelocity = Vector2.zero;
                }

                // Move towards player
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, magnetSpeed * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerCurrency pc = other.GetComponent<PlayerCurrency>();
            if (pc == null)
            {
                pc = other.GetComponentInParent<PlayerCurrency>();
            }

            if (pc != null)
            {
                pc.AddCoins(coinValue);
                // Optional: Spawn coin pickup particle or sound here
                Destroy(gameObject);
            }
        }
    }
}
