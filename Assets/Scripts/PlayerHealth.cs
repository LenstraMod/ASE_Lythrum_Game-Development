using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 3;
    private int currentHealth;
    
    private Vector3 respawnPoint;

    void Start()
    {
        currentHealth = maxHealth;
        respawnPoint = transform.position; 
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Player took damage! Current Health: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        Respawn();
    }

    private void Respawn()
    {
        transform.position = respawnPoint;
        currentHealth = maxHealth;
    }

    public void UpdateCheckpoint(Vector3 newCheckpointPosition)
    {
        respawnPoint = newCheckpointPosition;
        Debug.Log("Checkpoint updated to: " + respawnPoint);
    }
}