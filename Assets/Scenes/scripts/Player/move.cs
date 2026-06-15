using UnityEngine;

public class move : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    public float gravityScale = 1f;
    
    [Header("Blob Form Settings")]
    public float blobSpeedMultiplier = 1.5f; // Makes the blob dash faster than normal running
    private bool isBlobForm = false;

    [Header("Dash Settings")]
    public float dashSpeed = 16f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.8f;
    public bool invulnerableDuringDash = true;

    private bool isDashing = false;
    private float dashTimeLeft;
    private float dashCooldownTimer;
    private float lastFacingSign = 1f;

    public bool IsInvulnerable => isDashing && invulnerableDuringDash;
    public bool IsDashing => isDashing;

    [Header("Debuffs")]
    private float speedDebuffMultiplier = 1.0f;
    private float debuffTimer = 0f;

    private Rigidbody2D rb;
    private Animator anim;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        rb.gravityScale = gravityScale;
    }

    public void UpgradeDash()
    {
        dashSpeed *= 1.25f;
        dashCooldown *= 0.8f;
        Debug.Log("Player dash upgraded! New speed: " + dashSpeed + ", cooldown: " + dashCooldown);
    }

    public void ApplySlow(float duration, float multiplier)
    {
        speedDebuffMultiplier = multiplier;
        debuffTimer = duration;
        Debug.Log("Player slowed! Speed multiplier: " + multiplier + " for " + duration + "s");
    }

    void Update()
    {
        // Update debuff timer
        if (debuffTimer > 0f)
        {
            debuffTimer -= Time.deltaTime;
            if (debuffTimer <= 0f)
            {
                speedDebuffMultiplier = 1.0f;
                Debug.Log("Player slow debuff expired.");
            }
        }

        // Handle active dash
        if (isDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            if (dashTimeLeft <= 0f)
            {
                isDashing = false;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            else
            {
                rb.linearVelocity = new Vector2(lastFacingSign * dashSpeed, 0f); // zero gravity/vertical movement during dash
                return;
            }
        }

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput != 0)
        {
            lastFacingSign = Mathf.Sign(horizontalInput);
        }

        // 👇 Check if pressing M AND moving left or right
        if (Input.GetKey(KeyCode.M) && horizontalInput != 0)
        {
            isBlobForm = true;
        }
        else
        {
            isBlobForm = false;
        }

        // Trigger Dash
        if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownTimer <= 0f && !isBlobForm)
        {
            isDashing = true;
            dashTimeLeft = dashDuration;
            dashCooldownTimer = dashCooldown;
            rb.linearVelocity = new Vector2(lastFacingSign * dashSpeed, 0f);
            if (anim != null)
            {
                anim.SetTrigger("dash");
            }
            return;
        }

        // Apply movement speed (faster if in blob form, affected by slow debuff)
        float currentSpeed = isBlobForm ? (moveSpeed * blobSpeedMultiplier) : moveSpeed;
        currentSpeed *= speedDebuffMultiplier;
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
