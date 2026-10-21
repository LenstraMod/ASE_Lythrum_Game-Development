using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private bool isPlayerNear = false;
    private PlayerHealth playerHealth;

    void Update()
    {
        if (isPlayerNear && playerHealth != null && Input.GetKeyDown(KeyCode.F))
        {
            playerHealth.UpdateCheckpoint(transform.position);
            AudioManager.Play(Sfx.Checkpoint);
            Debug.Log("Respawn Set");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = true;
            playerHealth = collision.GetComponent<PlayerHealth>();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = false;
            playerHealth = null;
        }
    }
}