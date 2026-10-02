using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    private bool isDashing;
    private bool canDash = true;

    private PlayerHealth playerHealth;

    public Rigidbody2D rb;
    private Vector2 movement;
    private Animator animator;

    // Set by other scripts (e.g. PlayerCombat slows the player while attacking). 1 = normal speed.
    [HideInInspector] public float speedMultiplier = 1f;

    // Normalized direction the player last moved in. Used to aim attacks.
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (isDashing) return;

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

        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && movement.sqrMagnitude > 0)
        {
            StartCoroutine(DashCoroutine());
        }
    }

    private void FixedUpdate()
    {   
        if (isDashing) return;

        rb.MovePosition(rb.position + movement.normalized * moveSpeed * speedMultiplier * Time.fixedDeltaTime);
    }

    private IEnumerator DashCoroutine()
    {
        canDash = false;
        isDashing = true;
        
        if (playerHealth != null)
        {
            playerHealth.isInvincible = true;
        }

        Vector2 dashDirection = movement.normalized;

        float startTime = Time.time;
        while (Time.time < startTime + dashDuration)
        {
            rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.fixedDeltaTime);

            yield return new WaitForFixedUpdate();
        }

        isDashing = false;

        if (playerHealth != null)
        {
            playerHealth.isInvincible = false;
        }

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
}
