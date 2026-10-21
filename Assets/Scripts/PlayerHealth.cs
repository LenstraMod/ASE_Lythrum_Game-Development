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

    // Set by cutscenes (shard pickup, boss intro) so enemies can't hit a player who can't move.
    [HideInInspector] public bool cutsceneInvulnerable;

    // Fired after the player died and was put back at the respawn point (the boss arena resets on this).
    public event System.Action Respawned;

    [Tooltip("After taking a hit the player can't be hit again for this long.")]
    public float hitInvincibleTime = 0.6f;
    private float invincibleUntil;

    private SpriteRenderer sprite;
    private Color spriteColor = Color.white;
    private Coroutine flash;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) spriteColor = sprite.color;
    }

    // A short red flash on the player sprite.
    public void HitFlash()
    {
        if (sprite == null) return;
        if (flash != null) StopCoroutine(flash);
        flash = StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        sprite.color = new Color(1f, 0.35f, 0.35f, spriteColor.a);
        yield return new WaitForSeconds(0.12f);
        sprite.color = spriteColor;
        flash = null;
    }

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
}