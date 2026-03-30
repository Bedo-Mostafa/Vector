using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 500f;

    [Header("Auto-Run Settings")]
    [SerializeField] float autoRunSpeed = 1f;

    [Header("Jump Settings")]
    [SerializeField] float jumpHeight = 2.5f;
    [SerializeField] float jumpForwardBoost = 6f;
    [SerializeField] float gravity = -20f;

    [Header("Jump Prep (slide prevention)")]
    [Tooltip("Seconds to freeze root motion before launching — removes the sliding frame")]
    [SerializeField] float jumpPrepTime = 0.08f;

    [Header("Ground Check")]
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] Vector3 groundCheckOffset;
    [SerializeField] LayerMask groundLayer;

    [Header("Capsule During Roll (prevents sinking)")]
    [Tooltip("CharacterController height while the roll animation plays")]
    [SerializeField] float rollCapsuleHeight = 0.9f;
    [Tooltip("CharacterController center Y while rolling")]
    [SerializeField] float rollCapsuleCenterY = 0.45f;
    [Tooltip("Normal standing CharacterController height")]
    [SerializeField] float normalCapsuleHeight = 1.8f;
    [Tooltip("Normal standing CharacterController center Y")]
    [SerializeField] float normalCapsuleCenterY = 0.9f;

    // ── Public state ──────────────────────────────────────────────────────
    public bool IsGrounded { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsPreparingJump { get; private set; }   // brief freeze before launch
    public bool IsRolling { get; private set; }     // land-roll active

    // ── Internal ──────────────────────────────────────────────────────────
    bool hasControl = true;
    bool inParkourAction = false;

    float ySpeed;
    Vector3 jumpVelocity;
    float jumpPrepTimer;

    CameraController cameraController;
    Animator animator;
    CharacterController characterController;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        cameraController = Camera.main.GetComponent<CameraController>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (inParkourAction) return;
        if (!hasControl) return;

        GroundCheck();

        // ── Jump prep: freeze root motion briefly so no slide ─────────────
        if (IsPreparingJump)
        {
            jumpPrepTimer -= Time.deltaTime;
            // Apply tiny downward force to stay grounded during prep
            characterController.Move(new Vector3(0, -2f, 0) * Time.deltaTime);

            if (jumpPrepTimer <= 0f)
                LaunchJump();

            return;
        }

        // ── Airborne: projectile motion ───────────────────────────────────
        if (IsJumping)
        {
            jumpVelocity.y += gravity * Time.deltaTime;
            characterController.Move(jumpVelocity * Time.deltaTime);

            if (IsGrounded && jumpVelocity.y < 0f)
                Land();

            return;
        }

        // ── Grounded ──────────────────────────────────────────────────────
        if (IsGrounded)
        {
            ySpeed = -2f;
        }
        else
        {
            // Walked off an edge
            ySpeed += gravity * Time.deltaTime;
            characterController.Move(new Vector3(0, ySpeed, 0) * Time.deltaTime);

            if (!IsJumping)
            {
                IsJumping = true;
                jumpVelocity = transform.forward * jumpForwardBoost * 0.4f;
                jumpVelocity.y = ySpeed;
                animator.SetBool("isJumping", true);
                animator.SetBool("isGrounded", false);
            }
            return;
        }

        // Root motion handles XZ via OnAnimatorMove
        animator.SetFloat("moveAmount", autoRunSpeed, 0.1f, Time.deltaTime);
    }

    private void OnAnimatorMove()
    {
        if (inParkourAction)
        {
            characterController.Move(animator.deltaPosition);
            transform.rotation = animator.rootRotation;
            return;
        }

        if (!hasControl) return;
        if (IsJumping) return;   // physics owns XZ while airborne
        if (IsPreparingJump) return;   // freeze: no root motion during prep

        // Grounded normal movement
        Vector3 vel = animator.deltaPosition;
        vel.y = ySpeed * Time.deltaTime;
        characterController.Move(vel);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins jump prep (short freeze) then launches into projectile arc.
    /// </summary>
    public void DoJump(float forwardMultiplier = 1f)
    {
        if (!IsGrounded || IsPreparingJump || IsJumping) return;

        _pendingForwardMultiplier = forwardMultiplier;
        IsPreparingJump = true;
        jumpPrepTimer = jumpPrepTime;

        // Cross-fade to JumpStart immediately so animation starts snappy
        animator.CrossFade("JumpStart", 0.15f);
        animator.SetFloat("moveAmount", 0f);    // stop run blend during prep
    }

    /// <summary>Enable/disable rolling capsule to prevent sinking into floor.</summary>
    public void SetRolling(bool rolling)
    {
        IsRolling = rolling;

        if (rolling)
        {
            characterController.height = rollCapsuleHeight;
            characterController.center = new Vector3(0, rollCapsuleCenterY, 0);
        }
        else
        {
            characterController.height = normalCapsuleHeight;
            characterController.center = new Vector3(0, normalCapsuleCenterY, 0);
        }
    }

    public void SetControl(bool control)
    {
        hasControl = control;
        inParkourAction = !control;
        characterController.enabled = true;

        if (!control)
            animator.SetFloat("moveAmount", 0f);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────

    float _pendingForwardMultiplier = 1f;

    void LaunchJump()
    {
        IsPreparingJump = false;

        float launchSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        jumpVelocity = transform.forward * (jumpForwardBoost * _pendingForwardMultiplier);
        jumpVelocity.y = launchSpeed;

        IsJumping = true;
        animator.SetBool("isJumping", true);
        animator.SetBool("isGrounded", false);
    }

    void Land()
    {
        IsJumping = false;
        ySpeed = -2f;
        jumpVelocity = Vector3.zero;

        animator.SetBool("isJumping", false);
        animator.SetBool("isGrounded", true);
    }

    void GroundCheck()
    {
        IsGrounded = Physics.CheckSphere(
            transform.TransformPoint(groundCheckOffset),
            groundCheckRadius,
            groundLayer);

        animator.SetBool("isGrounded", IsGrounded);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawSphere(transform.TransformPoint(groundCheckOffset), groundCheckRadius);
    }

    public float RotationSpeed => rotationSpeed;
}