using UnityEngine;

public class BoxPulling : MonoBehaviour
{
    [Header("Pull Settings")]
    public float grabDistance = 1.0f; // How close the player needs to be to grab the box
    public KeyCode grabKey = KeyCode.F; // The key to hold down

    private Rigidbody2D grabbedBox;
    private FixedJoint2D joint;

    void Update()
    {
        // When the player presses down the F key
        if (Input.GetKeyDown(grabKey))
        {
            TryGrab();
        }

        // When the player lets go of the F key
        if (Input.GetKeyUp(grabKey))
        {
            Release();
        }
    }

    void TryGrab()
    {
        // Draw an invisible circle around the player to look for objects
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, grabDistance);

        foreach (Collider2D collider in colliders)
        {
            // If we find an object with the "Box" tag
            if (collider.CompareTag("Box"))
            {
                grabbedBox = collider.GetComponent<Rigidbody2D>();

                if (grabbedBox != null)
                {
                    // Create a "glue" joint on the player and stick it to the box
                    joint = gameObject.AddComponent<FixedJoint2D>();
                    joint.connectedBody = grabbedBox;
                    
                    break; // Stop looking after we grab the first box we find
                }
            }
        }
    }

    void Release()
    {
        // If we have a joint, destroy it to let go of the box
        if (joint != null)
        {
            Destroy(joint);
            grabbedBox = null;
        }
    }

    // This draws a cyan circle in the Scene view so you can visually see your grab distance!
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, grabDistance);
    }
}