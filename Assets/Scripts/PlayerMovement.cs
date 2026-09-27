using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
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
    }

    private void FixedUpdate()
    {
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

        rb.MovePosition(rb.position + movement.normalized * moveSpeed * speedMultiplier * Time.fixedDeltaTime);
    }


}
