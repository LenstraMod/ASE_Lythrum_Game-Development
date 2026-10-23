using UnityEngine;

// Melee enemy: stands around and wanders, notices the player, chases, winds up an attack
// (a visible warning the player can dodge or dash through), strikes, then recovers.
// Uses Health for hit points and TimeStoppable so Time Stop freezes it.
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class EnemyBrain : MonoBehaviour
{
    public enum State { Idle, Wander, Chase, Windup, Recover, Hurt, Dead }

    [Header("Senses")]
    public float detectionRadius = 5f;
    [Tooltip("Once chasing, the enemy gives up when the player gets this far away.")]
    public float loseInterestRadius = 9f;
    public bool needLineOfSight = true;

    [Header("Movement")]
    public float wanderSpeed = 0.8f;
    public float chaseSpeed = 2.2f;
    public float wanderRadius = 2.5f;
    public Vector2 idleTime = new Vector2(1f, 3f);
    public float maxWanderTime = 4f;

    [Header("Attack")]
    public int damage = 1;
    [Tooltip("Distance to the player at which the enemy starts an attack.")]
    public float attackRange = 0.9f;
    [Tooltip("The player must still be this close when the wind-up ends, otherwise the attack misses.")]
    public float hitRange = 1.2f;
    public float windupTime = 0.5f;
    public float recoverTime = 1f;
    public float lungeSpeed = 4f;
    public float lungeTime = 0.12f;
    public Color telegraphColor = new Color(1f, 0.45f, 0.45f);

    [Header("Reactions")]
    public float staggerTime = 0.3f;
    [Tooltip("Off = hits don't interrupt the wind-up (tough enemies).")]
    public bool interruptWindup = true;
    public float corpseTime = 0.8f;
    const float FadeTime = 0.5f;

    [Header("Visuals")]
    public SpriteRenderer body;
    public SpriteSheetAnimator animator;
    public bool spritesFaceRight = true;
    public float healthBarWidth = 0.8f;

    public State Current { get; private set; } = State.Idle;

    private Rigidbody2D rb;
    private Health health;
    private TimeStoppable stoppable;

    private Transform player;
    private PlayerHealth playerHealth;

    private Vector2 anchor;
    private Vector2 wanderTarget;
    private Vector2 desiredVelocity;
    private Vector2 lungeDirection;
    private float stateTimer;
    private float lungeTimer;
    private float corpseTimer;
    private bool aggro;
    private bool hurtWhileFrozen;

    private Transform barRoot;
    private Transform barFill;
    private static Sprite whiteSprite;
    private static readonly RaycastHit2D[] sightHits = new RaycastHit2D[8];

    bool Frozen => stoppable != null && stoppable.IsFrozen;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        stoppable = GetComponent<TimeStoppable>();

        health.Damaged += OnDamaged;
        health.Died += OnDied;
        if (stoppable != null) stoppable.Resumed += OnTimeResumed;
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (stoppable != null) stoppable.Resumed -= OnTimeResumed;
    }

    void Start()
    {
        anchor = rb.position;
        EnterIdle();
    }

    void Update()
    {
        if (Frozen) return;

        if (Current == State.Dead)
        {
            UpdateCorpse();
            return;
        }
        if (!FindPlayer()) return;

        stateTimer -= Time.deltaTime;
        float distance = Vector2.Distance(rb.position, player.position);
        UpdateAggro(distance);

        desiredVelocity = Vector2.zero;

        switch (Current)
        {
            case State.Idle:
                if (aggro) EnterChase();
                else if (stateTimer <= 0f) EnterWander();
                break;

            case State.Wander:
                if (aggro) { EnterChase(); break; }
                if (stateTimer <= 0f || Vector2.Distance(rb.position, wanderTarget) < 0.15f) { EnterIdle(); break; }
                MoveToward(wanderTarget, wanderSpeed);
                break;

            case State.Chase:
                if (!aggro) { EnterIdle(); break; }
                if (distance <= attackRange && (!needLineOfSight || HasLineOfSight())) { EnterWindup(); break; }
                MoveToward(player.position, chaseSpeed);
                break;

            case State.Windup:
                // Glow toward the warning colour so the player can read the attack.
                body.color = Color.Lerp(Color.white, telegraphColor, 1f - Mathf.Clamp01(stateTimer / windupTime));
                if (stateTimer <= 0f) Strike(distance);
                break;

            case State.Recover:
                if (stateTimer <= 0f) EnterChase();
                break;

            case State.Hurt:
                if (stateTimer <= 0f) EnterChase();
                break;
        }

        if (lungeTimer > 0f)
        {
            lungeTimer -= Time.deltaTime;
            desiredVelocity = lungeDirection * lungeSpeed;
        }
    }

    void FixedUpdate()
    {
        if (Frozen || Current == State.Dead || Current == State.Hurt) return; // Hurt: let the knockback play out
        rb.linearVelocity = desiredVelocity;
    }

    // ---- states ----

    void EnterIdle()
    {
        Current = State.Idle;
        stateTimer = Random.Range(idleTime.x, idleTime.y);
        PlayAnim("Idle");
    }

    void EnterWander()
    {
        Current = State.Wander;
        stateTimer = maxWanderTime;
        wanderTarget = anchor + Random.insideUnitCircle * wanderRadius;
        PlayAnim("Walk");
    }

    void EnterChase()
    {
        Current = State.Chase;
        body.color = Color.white;
        PlayAnim("Walk");
    }

    void EnterWindup()
    {
        Current = State.Windup;
        stateTimer = windupTime;
        FaceToward(player.position);
        // Stretch the attack clip so its last frame lands right as the hit does.
        animator.Play("Attack", windupTime + 0.15f);
    }

    void Strike(float distance)
    {
        body.color = Color.white;
        AudioManager.Play(Sfx.EnemyAttack);

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        lungeDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
        lungeTimer = lungeTime;

        if (distance <= hitRange && playerHealth != null) playerHealth.TakeDamage(damage);

        Current = State.Recover;
        stateTimer = recoverTime;
    }

    void EnterHurt()
    {
        Current = State.Hurt;
        stateTimer = staggerTime;
        lungeTimer = 0f;
        body.color = Color.white;
        PlayAnim("Idle");
    }

    // ---- reactions ----

    void OnDamaged(Health h)
    {
        if (Current == State.Dead) return;

        aggro = true;
        UpdateHealthBar();

        if (Current == State.Windup && !interruptWindup) return;
        if (Frozen) { hurtWhileFrozen = true; return; } // the stagger starts when time resumes
        EnterHurt();
    }

    void OnTimeResumed()
    {
        if (hurtWhileFrozen && Current != State.Dead && !(Current == State.Windup && !interruptWindup)) EnterHurt();
        hurtWhileFrozen = false;
    }

    void OnDied(Health h)
    {
        Current = State.Dead;
        corpseTimer = 0f;
        lungeTimer = 0f;
        body.color = Color.white;
        if (barRoot != null) barRoot.gameObject.SetActive(false);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
        rb.linearVelocity = Vector2.zero;

        AudioManager.Play(Sfx.EnemyDeath);
        animator.Play("Death");
    }

    void UpdateCorpse()
    {
        if (!animator.Finished) return;

        corpseTimer += Time.deltaTime;
        if (corpseTimer > corpseTime)
        {
            float t = (corpseTimer - corpseTime) / FadeTime;
            Color c = body.color;
            c.a = 1f - Mathf.Clamp01(t);
            body.color = c;
            if (t >= 1f) Destroy(gameObject);
        }
    }

    // ---- helpers ----

    bool FindPlayer()
    {
        if (player != null) return true;

        foreach (GameObject candidate in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (candidate.GetComponent<ShadowPlayback>() != null) continue;   // the E clone has the Player tag too

            player = candidate.transform;
            playerHealth = candidate.GetComponent<PlayerHealth>();
            return true;
        }
        return false;
    }

    void UpdateAggro(float distance)
    {
        if (!aggro)
        {
            if (distance <= detectionRadius && (!needLineOfSight || HasLineOfSight())) aggro = true;
        }
        else if (distance > loseInterestRadius)
        {
            aggro = false;
            anchor = rb.position; // wander around the new spot instead of walking back
        }
    }

    // Walls and closed doors block sight; the player and other enemies don't.
    bool HasLineOfSight()
    {
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        int count = Physics2D.Linecast(rb.position, player.position, filter, sightHits);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = sightHits[i].collider;
            if (col.transform.IsChildOf(transform) || col.transform.IsChildOf(player)) continue;
            if (col.GetComponentInParent<Health>() != null) continue;
            return false;
        }
        return true;
    }

    void MoveToward(Vector2 target, float speed)
    {
        Vector2 direction = target - rb.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        desiredVelocity = direction.normalized * speed;
        FaceToward(target);
    }

    void FaceToward(Vector2 target)
    {
        float dx = target.x - rb.position.x;
        if (Mathf.Abs(dx) < 0.05f) return;
        body.flipX = spritesFaceRight ? dx < 0f : dx > 0f;
    }

    void PlayAnim(string clip)
    {
        if (animator != null) animator.Play(clip);
    }

    // ---- health bar (appears after the first hit) ----

    void UpdateHealthBar()
    {
        if (barRoot == null) CreateHealthBar();

        float percent = health.maxHealth > 0 ? (float)health.CurrentHealth / health.maxHealth : 0f;
        barFill.localScale = new Vector3(percent, 1f, 1f);
        barFill.localPosition = new Vector3(-0.5f + percent * 0.5f, 0f, 0f);
    }

    void CreateHealthBar()
    {
        GameObject root = new GameObject("HealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, body.bounds.max.y - transform.position.y + 0.15f, 0f);
        root.transform.localScale = new Vector3(healthBarWidth, 0.1f, 1f);
        barRoot = root.transform;

        SpriteRenderer bg = root.AddComponent<SpriteRenderer>();
        bg.sprite = GetWhiteSprite();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        bg.sortingLayerID = body.sortingLayerID;
        bg.sortingOrder = body.sortingOrder + 50;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        SpriteRenderer fill = fillGo.AddComponent<SpriteRenderer>();
        fill.sprite = GetWhiteSprite();
        fill.color = new Color(0.9f, 0.25f, 0.25f);
        fill.sortingLayerID = body.sortingLayerID;
        fill.sortingOrder = body.sortingOrder + 51;
        barFill = fillGo.transform;
    }

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, name = "EnemyBarWhite" };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        return whiteSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}
