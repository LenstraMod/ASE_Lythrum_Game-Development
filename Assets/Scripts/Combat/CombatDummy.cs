using UnityEngine;

// Placeholder enemy for testing combat. Walks left/right, shows a health bar,
// gets knocked back when hit, falls over when killed and stands back up after a delay.
// Needs no art: it draws its own sprite if the SpriteRenderer is empty.
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class CombatDummy : MonoBehaviour
{
    [Header("Patrol")]
    public bool patrol = true;
    public float patrolDistance = 2f;
    public float patrolSpeed = 1.5f;

    [Header("Reactions")]
    public float staggerTime = 0.4f;
    public float reviveDelay = 2f;

    [Header("Placeholder Look")]
    public Color bodyColor = new Color(0.85f, 0.55f, 0.3f);
    public Vector2 bodySize = new Vector2(0.8f, 1f);

    private Health health;
    private Rigidbody2D rb;
    private TimeStoppable timeStoppable;
    private Transform body;
    private Transform barFill;

    private Vector2 startPosition;
    private float patrolDirection = 1f;
    private float lastX;
    private float blockedTime;
    private float staggerUntil;
    private float reviveAt = -1f;
    private bool hitWhileFrozen;

    private static Sprite whiteSprite;

    void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        timeStoppable = GetComponent<TimeStoppable>();

        BuildPlaceholderVisuals();

        health.Damaged += OnDamaged;
        health.Died += OnDied;
        if (timeStoppable != null) timeStoppable.Resumed += OnTimeResumed;
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (timeStoppable != null) timeStoppable.Resumed -= OnTimeResumed;
    }

    void Start()
    {
        startPosition = rb.position;
        lastX = rb.position.x;
        UpdateHealthBar();
    }

    void Update()
    {
        if (reviveAt > 0f && Time.time >= reviveAt && !IsFrozen)
        {
            reviveAt = -1f;
            body.localRotation = Quaternion.identity;
            barFill.parent.gameObject.SetActive(true);
            health.Revive();
        }
    }

    void FixedUpdate()
    {
        if (IsFrozen || health.IsDead || Time.time < staggerUntil) return;

        if (!patrol)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, 20f * Time.fixedDeltaTime);
            return;
        }

        if (rb.position.x > startPosition.x + patrolDistance) patrolDirection = -1f;
        else if (rb.position.x < startPosition.x - patrolDistance) patrolDirection = 1f;

        // Turn around if something (a wall, the player) blocks the way.
        blockedTime = Mathf.Abs(rb.position.x - lastX) < 0.001f ? blockedTime + Time.fixedDeltaTime : 0f;
        lastX = rb.position.x;
        if (blockedTime > 0.3f)
        {
            patrolDirection = -patrolDirection;
            blockedTime = 0f;
        }

        Vector2 target = new Vector2(patrolDirection * patrolSpeed, 0f);
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, 20f * Time.fixedDeltaTime);
    }

    bool IsFrozen => timeStoppable != null && timeStoppable.IsFrozen;

    void OnDamaged(Health h)
    {
        staggerUntil = Time.time + staggerTime;
        if (IsFrozen) hitWhileFrozen = true;
        UpdateHealthBar();
    }

    void OnTimeResumed()
    {
        // Let the stored knockback play out before walking again.
        if (hitWhileFrozen) staggerUntil = Time.time + staggerTime;
        hitWhileFrozen = false;
    }

    void OnDied(Health h)
    {
        body.localRotation = Quaternion.Euler(0f, 0f, 90f);
        barFill.parent.gameObject.SetActive(false);
        reviveAt = Time.time + reviveDelay;
    }

    void UpdateHealthBar()
    {
        if (barFill == null) return;
        float pct = health.maxHealth > 0 ? (float)health.CurrentHealth / health.maxHealth : 0f;
        barFill.localScale = new Vector3(pct, 1f, 1f);
        barFill.localPosition = new Vector3(-0.5f + pct * 0.5f, 0f, 0f);
    }

    void BuildPlaceholderVisuals()
    {
        int sortingLayer = SortingLayer.NameToID("Player");
        if (!SortingLayer.IsValid(sortingLayer)) sortingLayer = 0;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
        {
            GameObject bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localScale = new Vector3(bodySize.x, bodySize.y, 1f);
            sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = bodyColor;
            sr.sortingLayerID = sortingLayer;
        }
        body = sr.transform;

        // Health bar: dark background + green fill, above the body.
        GameObject barGo = new GameObject("HealthBar");
        barGo.transform.SetParent(transform, false);
        barGo.transform.localPosition = new Vector3(0f, bodySize.y * 0.5f + 0.25f, 0f);
        barGo.transform.localScale = new Vector3(1f, 0.12f, 1f);
        SpriteRenderer bg = barGo.AddComponent<SpriteRenderer>();
        bg.sprite = GetWhiteSprite();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        bg.sortingLayerID = sortingLayer;
        bg.sortingOrder = 50;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barGo.transform, false);
        SpriteRenderer fill = fillGo.AddComponent<SpriteRenderer>();
        fill.sprite = GetWhiteSprite();
        fill.color = new Color(0.3f, 0.9f, 0.3f);
        fill.sortingLayerID = sortingLayer;
        fill.sortingOrder = 51;
        barFill = fillGo.transform;
    }

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, name = "DummyWhite" };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        return whiteSprite;
    }
}
