using UnityEngine;

public class Key : MonoBehaviour
{
    [Header("Follow Settings")]
    public float followSpeed = 5f;
    public Vector3 offset = new Vector3(-0.5f, 0.5f, 0f); // Floats slightly up and to the left of the player

    [HideInInspector] 
    public bool isCollected = false; // Hidden in inspector, but used by the door to check status
    private Transform playerTransform;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If the player touches the key, it becomes collected and starts following
        if (!isCollected && other.CompareTag("Player"))
        {
            isCollected = true;
            playerTransform = other.transform;
        }
    }

    private void Update()
    {
        // If collected, smoothly float towards the player's position + offset
        if (isCollected && playerTransform != null)
        {
            transform.position = Vector3.Lerp(transform.position, playerTransform.position + offset, followSpeed * Time.deltaTime);
        }
    }
}