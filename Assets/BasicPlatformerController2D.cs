using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BasicPlatformerController2D : MonoBehaviour
{
    private const string JumpFxName = "my_jump";
    private const string DashFxName = "my_dash";
    private const string LandFxName = "my_ground";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] private float shortJumpHeight = 1f;
    [SerializeField] private float longJumpHeight = 2f;
    [SerializeField] private int maxJumpCount = 1;
    [SerializeField] private int jumpBufferFrames = 6;
    [SerializeField] private int coyoteFrames = 6;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private float groundProbeDepth = 0.08f;
    [SerializeField] private bool onlyGroundCheck = true;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private string requiredGroundTag = "ground";
    [SerializeField] private bool showDebugInScene = true;
    [SerializeField] private bool useNoFrictionOnPlayer = true;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 2.5f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private bool enableDash = true;

    [Header("Action Record")]
    [SerializeField] private ActionMatchUIManager actionMatchUIManager;

    private Rigidbody2D rb;
    private CircleCollider2D circleCollider;
    private float moveInput;
    private float shortJumpSpeed;
    private float longJumpSpeed;
    private int jumpsRemaining;
    private bool wasGrounded;
    private bool debugGrounded;
    private int jumpBufferCounter = -1;
    private int coyoteCounter = -1;
    private readonly Collider2D[] groundHits = new Collider2D[8];
    private PhysicsMaterial2D runtimeNoFrictionMaterial;
    private Collider2D[] playerColliders;
    private int facingDirection = 1;
    private bool dashRequested;
    private bool isDashing;
    private float dashRemainingDistance;
    private float dashSpeed;
    private float dashLockY;
    private int dashDirection = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circleCollider = GetComponent<CircleCollider2D>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        ApplyNoFrictionMaterialIfNeeded();
        RecalculateJumpSpeeds();
        jumpsRemaining = Mathf.Max(0, maxJumpCount - 1);

        if (groundCheck == null)
        {
            var child = transform.Find("GroundCheck");
            if (child != null)
            {
                groundCheck = child;
            }
        }

        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
        }

        wasGrounded = IsGrounded();
        if (wasGrounded)
        {
            ResetAirJumpCount();
            coyoteCounter = coyoteFrames;
        }

        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(moveInput) > 0.001f)
        {
            facingDirection = moveInput > 0f ? 1 : -1;
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferFrames;
        }

        if (enableDash && (Input.GetKeyDown(KeyCode.LeftControl)
            || Input.GetKeyDown(KeyCode.RightControl)
            || Input.GetKeyDown(KeyCode.LeftShift)
            || Input.GetKeyDown(KeyCode.RightShift)))
        {
            dashRequested = true;
        }

        if (Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.Space))
        {
            // Short tap will clamp upward speed to the "1 unit jump" target.
            if (rb.velocity.y > shortJumpSpeed)
            {
                rb.velocity = new Vector2(rb.velocity.x, shortJumpSpeed);
            }
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            ContinueDash();
            return;
        }

        bool isGrounded = IsGrounded();
        debugGrounded = isGrounded;

        if (isGrounded)
        {
            coyoteCounter = coyoteFrames;
            if (!wasGrounded)
            {
                ResetAirJumpCount();
                NotifyAction(PlayerActionType.Land, true);
                PlayActionFx(LandFxName);
            }
        }

        if (dashRequested && enableDash)
        {
            StartDash();
            return;
        }

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        if (TryConsumeJump(isGrounded))
        {
            rb.velocity = new Vector2(rb.velocity.x, longJumpSpeed);
            NotifyAction(PlayerActionType.Jump, true);
            PlayActionFx(JumpFxName);
        }

        if (jumpBufferCounter >= 0)
        {
            jumpBufferCounter--;
        }

        if (coyoteCounter >= 0)
        {
            coyoteCounter--;
        }

        wasGrounded = isGrounded;
    }

    private void StartDash()
    {
        dashRequested = false;
        isDashing = true;
        dashDirection = facingDirection == 0 ? 1 : facingDirection;
        dashLockY = rb.position.y;
        dashRemainingDistance = Mathf.Max(0f, dashDistance);

        float safeDuration = Mathf.Max(0.01f, dashDuration);
        dashSpeed = dashRemainingDistance / safeDuration;

        // Dash is a real successful action, record it as C.
        NotifyAction(PlayerActionType.Dash, true);
        PlayActionFx(DashFxName);
    }

    private void ContinueDash()
    {
        if (dashRemainingDistance <= 0f)
        {
            EndDash();
            return;
        }

        float step = dashSpeed * Time.fixedDeltaTime;
        step = Mathf.Min(step, dashRemainingDistance);
        dashRemainingDistance -= step;

        Vector2 next = new Vector2(rb.position.x + dashDirection * step, dashLockY);
        rb.MovePosition(next);
        rb.velocity = new Vector2(0f, 0f);

        if (dashRemainingDistance <= 0f)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        isDashing = false;
        rb.velocity = new Vector2(0f, 0f);
    }

    private bool IsGrounded()
    {
        bool groundCheckHit = groundCheck != null && HasValidGroundHit(
            Physics2D.OverlapCircleNonAlloc(groundCheck.position, groundCheckRadius, groundHits, groundLayer));
        if (groundCheckHit)
        {
            return true;
        }

        if (onlyGroundCheck)
        {
            return false;
        }

        if (circleCollider == null)
        {
            return false;
        }

        Bounds bounds = circleCollider.bounds;
        Vector2 boxCenter = new Vector2(bounds.center.x, bounds.min.y - groundProbeDepth * 0.5f);
        Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, groundProbeDepth);
        return HasValidGroundHit(Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, 0f, groundHits, groundLayer));
    }

    private bool HasValidGroundHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = groundHits[i];
            if (hit == null)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(requiredGroundTag) && !hit.CompareTag(requiredGroundTag))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void ResetAirJumpCount()
    {
        jumpsRemaining = Mathf.Max(0, maxJumpCount - 1);
    }

    private void ApplyNoFrictionMaterialIfNeeded()
    {
        if (!useNoFrictionOnPlayer)
        {
            return;
        }

        if (runtimeNoFrictionMaterial == null)
        {
            runtimeNoFrictionMaterial = new PhysicsMaterial2D("Runtime_NoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D col = playerColliders[i];
            if (col == null)
            {
                continue;
            }

            col.sharedMaterial = runtimeNoFrictionMaterial;
        }
    }

    private void RecalculateJumpSpeeds()
    {
        shortJumpHeight = Mathf.Max(0.01f, shortJumpHeight);
        longJumpHeight = Mathf.Max(shortJumpHeight, longJumpHeight);

        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        gravity = Mathf.Max(0.01f, gravity);

        shortJumpSpeed = Mathf.Sqrt(2f * gravity * shortJumpHeight);
        longJumpSpeed = Mathf.Sqrt(2f * gravity * longJumpHeight);
    }

    private bool TryConsumeJump(bool isGrounded)
    {
        if (jumpBufferCounter < 0)
        {
            return false;
        }

        if (isGrounded || coyoteCounter >= 0)
        {
            jumpBufferCounter = -1;
            coyoteCounter = -1;
            return true;
        }

        if (jumpsRemaining > 0)
        {
            jumpsRemaining--;
            jumpBufferCounter = -1;
            return true;
        }

        return false;
    }

    private void NotifyAction(PlayerActionType action, bool actionSucceeded)
    {
        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }

        if (actionMatchUIManager != null)
        {
            actionMatchUIManager.TryRecordAction(action, actionSucceeded);
        }
    }

    private static void PlayActionFx(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return;
        }

        AudioManager.playFx(clipName);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (circleCollider != null)
        {
            Bounds bounds = circleCollider.bounds;
            Vector3 boxCenter = new Vector3(bounds.center.x, bounds.min.y - groundProbeDepth * 0.5f, 0f);
            Vector3 boxSize = new Vector3(bounds.size.x * 0.9f, groundProbeDepth, 0f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }

#if UNITY_EDITOR
        if (showDebugInScene)
        {
            string debugText = "Grounded: " + debugGrounded
                + " | Air Jumps Left: " + jumpsRemaining
                + " | Buffer: " + jumpBufferCounter
                + " | Coyote: " + coyoteCounter
                + " | ShortH: " + shortJumpHeight
                + " | LongH: " + longJumpHeight;
            Handles.color = Color.white;
            Handles.Label(transform.position + Vector3.up * 1.2f, debugText);
        }
#endif
    }

    private void OnValidate()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            RecalculateJumpSpeeds();
        }
    }
}
