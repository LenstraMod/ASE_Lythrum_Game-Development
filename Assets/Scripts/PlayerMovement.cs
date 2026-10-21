using UnityEngine;

// Walking, running (hold the run key) and the facing direction. The controlLocked flag switches player input off
// (used by shard pickups and the skeleton boss intro and grab).
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Header("Run (hold to run, replaces the old dash)")]
    public KeyCode runKey = KeyCode.LeftShift;
    [Tooltip("Run speed = move speed x this.")]
    public float runSpeedMultiplier = 1.6f;
    [Tooltip("The walk animation plays this much faster while running (1 = same speed).")]
    public float runAnimationSpeed = 1.3f;

    [Tooltip("Seconds between footstep sounds while walking (they come faster while running).")]
    public float footstepInterval = 0.35f;
    private float nextFootstepTime;

    public Rigidbody2D rb;
    private Vector2 movement;
    private Animator animator;

    // Set by other scripts (e.g. PlayerCombat slows the player while attacking). 1 = normal speed.
    [HideInInspector] public float speedMultiplier = 1f;

    // A short push (a boss slam) that fades out within a fraction of a second. Added on top of normal walking.
    private Vector2 knockVelocity;
    public void Knockback(Vector2 velocity) => knockVelocity = velocity;

    // True while a cutscene (shard pickup, boss intro, boss grab) owns the player: no walking, running or attacking.
    [HideInInspector] public bool controlLocked;

    // True while the run key is held and the player is actually moving.
    public bool IsRunning { get; private set; }

    // Normalized direction the player last moved in. Used to aim attacks.
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (controlLocked)
        {
            movement = Vector2.zero;
            IsRunning = false;
            animator.speed = 1f;
            animator.SetFloat("InputX", 0f);
            animator.SetFloat("InputY", 0f);
            animator.SetBool("isWalking", false);
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        animator.SetFloat("InputX", movement.x);
        animator.SetFloat("InputY", movement.y);

        if (movement.sqrMagnitude > 0)
        {
            animator.SetFloat("LastInputX", movement.x);
            animator.SetFloat("LastInputY", movement.y);
            FacingDirection = movement.normalized;
        }

        bool walking = movement.sqrMagnitude > 0 && speedMultiplier > 0;
        animator.SetBool("isWalking", walking);

        // No running while attacking (PlayerCombat slows the player down then), and only while really moving.
        IsRunning = Input.GetKey(runKey) && walking && speedMultiplier >= 1f;
        animator.speed = IsRunning ? runAnimationSpeed : 1f;

        if (walking && Time.time >= nextFootstepTime)
        {
            AudioManager.Play(Sfx.PlayerFootstep);
            float stepInterval = footstepInterval / Mathf.Max(0.25f, speedMultiplier) / (IsRunning ? runSpeedMultiplier : 1f);
            nextFootstepTime = Time.time + stepInterval;
        }
    }

    private void FixedUpdate()
    {
        float speed = moveSpeed * speedMultiplier * (IsRunning ? runSpeedMultiplier : 1f);

        Vector2 push = knockVelocity;
        knockVelocity *= Mathf.Exp(-12f * Time.fixedDeltaTime);    // dies away in about a quarter of a second
        rb.MovePosition(rb.position + (movement.normalized * speed + push) * Time.fixedDeltaTime);
    }

    void OnDisable()
    {
        if (animator != null) animator.speed = 1f;
    }
}
