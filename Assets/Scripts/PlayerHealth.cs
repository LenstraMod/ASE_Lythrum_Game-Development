using UnityEngine;


public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 3f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private Vector3 respawnPoint;

    public bool isInvincible = false;

    public Vector3 RespawnPoint => respawnPoint;

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

    public void Heal(float heal)
    {
        if (currentHealth + heal > maxHealth) {
            currentHealth = maxHealth;
        } else
        {
            currentHealth += heal;
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
    public void RestoreState(float health, Vector3 checkpoint)
    {
        respawnPoint = checkpoint;
        transform.position = checkpoint;
        currentHealth = Mathf.Clamp(health, 1f, maxHealth);
    }
}