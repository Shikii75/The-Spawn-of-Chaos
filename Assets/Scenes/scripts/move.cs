using UnityEngine;

public class move : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    public float gravityScale = 1f;
    
    [Header("Blob Form Settings")]
    public float blobSpeedMultiplier = 1.5f; // Makes the blob dash faster than normal running
    private bool isBlobForm = false;

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

        // 👇 Check if pressing M AND moving left or right
        if (Input.GetKey(KeyCode.M) && horizontalInput != 0)
        {
            isBlobForm = true;
        }
        else
        {
            isBlobForm = false;
        }

        // Apply movement speed (faster if in blob form)
        float currentSpeed = isBlobForm ? (moveSpeed * blobSpeedMultiplier) : moveSpeed;
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);

        // 👇 Update Animator states
        anim.SetBool("isRunning", horizontalInput != 0 && !isBlobForm);
        anim.SetBool("isBlob", isBlobForm);

        // Flip sprite based on movement direction (preserving your exact inspector scales)
        if (horizontalInput > 0)
            transform.localScale = new Vector3(1.145f, 1.1842f, 1.1042f);
        else if (horizontalInput < 0)
            transform.localScale = new Vector3(-1.145f, 1.1842f, 1.1042f);

        // 👇 Jump logic (disable jumping while in blob form)
        if (Input.GetButtonDown("Jump") && isGrounded && !isBlobForm)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }

        anim.SetBool("isJumping", !isGrounded);
    }

    void FixedUpdate()
    {
        // Constant gravity
        rb.linearVelocity += Physics2D.gravity * gravityScale * Time.fixedDeltaTime;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}
