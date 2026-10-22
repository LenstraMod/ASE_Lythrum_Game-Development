using UnityEngine;

// Player hit points (a continuous amount, so attacks can take a fraction of the bar), damage with short invincibility,
// respawn at the last checkpoint.
// Cutscenes can block damage (cutsceneInvulnerable); the Respawned event lets the boss arena reset.
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 3f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    
    private Vector3 respawnPoint;

    public bool isInvincible = false;

    public int CurrentHealth => currentHealth;
    public Vector3 RespawnPoint => respawnPoint;

    void Start()
    {
        currentHealth = maxHealth;
        respawnPoint = transform.position; 
    }

    // respectHitCooldown = false lets very rapid hits (the boss's Fear slams) all land instead of being blocked
    // by the short invincibility after each hit. Cutscenes (and isInvincible) still block them.
    public void TakeDamage(float damage, bool respectHitCooldown = true)
    {

        if (isInvincible || cutsceneInvulnerable || (respectHitCooldown && Time.time < invincibleUntil))
        {
            return; 
        }
        
        if (respectHitCooldown) invincibleUntil = Time.time + hitInvincibleTime;
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            AudioManager.Play(Sfx.PlayerHurt);
            HitFlash();
        }
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        AudioManager.Play(Sfx.PlayerDeath);
        Respawn();
    }

    private void Respawn()
    {
        transform.position = respawnPoint;
        currentHealth = maxHealth;
        Respawned?.Invoke();
    }

    public void UpdateCheckpoint(Vector3 newCheckpointPosition)
    {
        respawnPoint = newCheckpointPosition;
        Debug.Log("Checkpoint updated to: " + respawnPoint);
    }

    // Dipanggil saat Load Game: kembali ke checkpoint tersimpan dengan HP tersimpan.
    public void RestoreState(int health, Vector3 checkpoint)
    {
        respawnPoint = checkpoint;
        transform.position = checkpoint;
        currentHealth = Mathf.Clamp(health, 1, maxHealth);
    }
}