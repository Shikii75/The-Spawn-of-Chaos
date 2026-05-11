using UnityEngine;

public class move : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    public float gravityScale = 1f;

    private Rigidbody2D rb;
    private Animator anim;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        rb.gravityScale = gravityScale;
    }

    void Update()
    {
         float horizontalInput = Input.GetAxisRaw("Horizontal");

    rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

    anim.SetBool("isRunning", horizontalInput != 0);

    // Flip sprite
    if (horizontalInput > 0)
        transform.localScale = new Vector3(1, 1, 1);
    else if (horizontalInput < 0)
        transform.localScale = new Vector3(-1, 1, 1);

    // 👇 Jump logic
    if (Input.GetButtonDown("Jump") && isGrounded)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isGrounded = false;
    }

    // 👇 Animator jump bool
    anim.SetBool("isJumping", !isGrounded);
    }

    void FixedUpdate()
    {
        // Constant gravity
        rb.linearVelocity += Physics2D.gravity * Time.fixedDeltaTime;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}


