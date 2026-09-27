using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 3;
    private int currentHealth;
    
    private Vector3 respawnPoint;

    public bool isInvincible = false;

    void Start()
    {
        currentHealth = maxHealth;
        respawnPoint = transform.position; 
    }

    public void TakeDamage(int damage)
    {

        if (isInvincible) 
        {
            Debug.Log("Damage dodged!");
            return; 
        }
        
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