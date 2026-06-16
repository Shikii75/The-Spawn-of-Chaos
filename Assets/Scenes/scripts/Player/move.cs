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
                rb.gravityScale = gravityScale; // Restore gravity
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

        if (IsMovementBlocked())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (anim != null)
            {
                anim.SetBool("isRunning", false);
                anim.SetBool("isBlob", false);
                anim.SetBool("isJumping", !isGrounded);
            }
            return;
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
            rb.gravityScale = 0f; // Disable gravity during dash
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

    private bool IsMovementBlocked()
    {
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return true;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return true;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return true;
        if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf) return true;

        // Block movement if any UI text input has active keyboard focus
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
        {
            var go = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (go.GetComponent<TMPro.TMP_InputField>() != null || go.GetComponent<UnityEngine.UI.InputField>() != null)
            {
                return true;
            }
        }

        return false;
    }

    void FixedUpdate()
    {
        // Constant gravity is managed natively by Rigidbody2D, no manual velocity addition needed.
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Continuously verify ground contact so walking off a ledge resets isGrounded reliably
        if (collision.gameObject.CompareTag("Ground"))
        {
            bool touchingFromAbove = false;
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    touchingFromAbove = true;
                    break;
                }
            }
            isGrounded = touchingFromAbove;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
