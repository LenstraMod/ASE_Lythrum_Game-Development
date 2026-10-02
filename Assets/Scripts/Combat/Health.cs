using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Anything the player can hit needs this (enemies, breakable objects, the training dummy).
public class Health : MonoBehaviour
{
    public int maxHealth = 30;
    public bool destroyOnDeath = true;

    [Header("Hit Feedback")]
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f);
    public float hitFlashTime = 0.1f;

    public UnityEvent onDamaged;
    public UnityEvent onDied;

    // For scripts (health bars, AI stagger). Passes this Health.
    public event Action<Health> Damaged;
    public event Action<Health> Died;

    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    private Rigidbody2D rb;
    private TimeStoppable timeStoppable;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    void Awake()
    {
        CurrentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        timeStoppable = GetComponent<TimeStoppable>();
    }

    void Start()
    {
        // Done in Start so sprites created in other Awake methods (e.g. CombatDummy) are included.
        CacheSpriteColors();
    }

    public void CacheSpriteColors()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++) originalColors[i] = spriteRenderers[i].color;
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0);

        if (knockback != Vector2.zero)
        {
            // During Time Stop the hit lands now, but the knockback waits until time resumes.
            if (timeStoppable != null && timeStoppable.IsFrozen) timeStoppable.QueueImpulse(knockback);
            else if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.AddForce(knockback, ForceMode2D.Impulse);
        }

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());

        onDamaged?.Invoke();
        Damaged?.Invoke(this);

        if (IsDead) Die();
    }

    public void Revive()
    {
        CurrentHealth = maxHealth;
        Damaged?.Invoke(this);
    }

    void Die()
    {
        onDied?.Invoke();
        Died?.Invoke(this);

        if (destroyOnDeath) Destroy(gameObject);
    }

    IEnumerator FlashRoutine()
    {
        if (spriteRenderers == null) CacheSpriteColors();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null) spriteRenderers[i].color = originalColors[i] * hitFlashColor;
        }

        yield return new WaitForSeconds(hitFlashTime);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null) spriteRenderers[i].color = originalColors[i];
        }
        flashRoutine = null;
    }
}
