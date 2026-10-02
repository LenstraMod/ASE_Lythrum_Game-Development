using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Normal slash (3-hit combo) and Area Slash (big arc in front of the player).
// Attacks aim in the direction the player last moved (PlayerMovement.FacingDirection).
[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    public class AttackData
    {
        public int damage = 10;
        public float range = 1.3f;
        [Range(10f, 360f)] public float arcAngle = 110f;
        public float knockback = 3f;
        [Tooltip("Delay between pressing the button and the hit landing.")]
        public float windup = 0.06f;
        [Tooltip("Time after the hit before the player can act again.")]
        public float recovery = 0.2f;
        public float screenShake = 0.1f;
    }

    [Header("Input (old Input Manager)")]
    public string slashButton = "Fire1";
    public KeyCode areaSlashKey = KeyCode.Q;
    [Tooltip("A press this long before the current attack ends still counts.")]
    public float inputBuffer = 0.25f;

    [Header("Normal Slash Combo")]
    public AttackData[] comboSteps =
    {
        new AttackData { damage = 10, range = 1.3f, arcAngle = 110f, knockback = 2.5f, windup = 0.05f, recovery = 0.18f, screenShake = 0.08f },
        new AttackData { damage = 10, range = 1.3f, arcAngle = 110f, knockback = 2.5f, windup = 0.05f, recovery = 0.18f, screenShake = 0.08f },
        new AttackData { damage = 18, range = 1.5f, arcAngle = 140f, knockback = 6f,   windup = 0.10f, recovery = 0.30f, screenShake = 0.2f },
    };
    [Tooltip("Time after a combo hit ends in which pressing again continues the combo.")]
    public float comboWindow = 0.35f;
    [Tooltip("Extra wait after the last combo hit before a new combo can start.")]
    public float comboEndCooldown = 0.25f;

    [Header("Area Slash")]
    public AttackData areaSlash = new AttackData
    {
        damage = 25, range = 2.6f, arcAngle = 170f, knockback = 8f, windup = 0.15f, recovery = 0.35f, screenShake = 0.35f
    };
    public float areaSlashCooldown = 3f;

    [Header("Movement While Attacking")]
    [Range(0f, 1f)] public float attackMoveSpeedMultiplier = 0.2f;

    [Header("Hit Detection")]
    public LayerMask hittableLayers = ~0;
    [Tooltip("Offset from the player's pivot where attacks start.")]
    public Vector2 attackOriginOffset = Vector2.zero;

    [Header("Game Feel")]
    public float hitStopDuration = 0.05f;
    public CinemachineImpulseSource impulseSource;

    [Header("Placeholder VFX")]
    public bool showPlaceholderVfx = true;
    public Material effectMaterial;
    public Color slashColor = new Color(1f, 1f, 1f, 0.9f);
    public Color areaSlashColor = new Color(0.55f, 0.85f, 1f, 0.9f);

    [Header("Animator Parameters (used only if they exist in the controller)")]
    public string attackTrigger = "Attack";
    public string comboStepInt = "ComboStep";
    public string areaSlashTrigger = "AreaSlash";

    public bool IsAttacking { get; private set; }
    public int ComboIndex { get; private set; }
    public float AreaSlashCooldownRemaining => Mathf.Max(0f, areaSlashReadyTime - Time.time);

    private PlayerMovement movement;
    private Animator animator;
    private SpriteRenderer playerSprite;

    private float slashPressedTime = -999f;
    private float areaPressedTime = -999f;
    private float lastSlashEndTime = -999f;
    private float nextSlashAllowedTime;
    private float areaSlashReadyTime;
    private bool hasAttackTrigger, hasComboStep, hasAreaTrigger;
    private bool hitStopping;

    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private readonly HashSet<Health> struckThisSwing = new HashSet<Health>();

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animator = GetComponent<Animator>();
        playerSprite = GetComponent<SpriteRenderer>();
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();

        hasAttackTrigger = animator.HasParameter(attackTrigger);
        hasComboStep = animator.HasParameter(comboStepInt);
        hasAreaTrigger = animator.HasParameter(areaSlashTrigger);
    }

    void OnDisable()
    {
        IsAttacking = false;
        if (movement != null) movement.speedMultiplier = 1f;
    }

    void Update()
    {
        if (Input.GetButtonDown(slashButton)) slashPressedTime = Time.time;
        if (Input.GetKeyDown(areaSlashKey)) areaPressedTime = Time.time;

        if (IsAttacking) return;

        if (Time.time - areaPressedTime <= inputBuffer && Time.time >= areaSlashReadyTime)
        {
            areaPressedTime = -999f;
            StartCoroutine(AreaSlashRoutine());
        }
        else if (Time.time - slashPressedTime <= inputBuffer && Time.time >= nextSlashAllowedTime)
        {
            slashPressedTime = -999f;
            StartCoroutine(SlashRoutine());
        }
    }

    IEnumerator SlashRoutine()
    {
        // Continue the combo if the last hit ended recently, otherwise start over.
        bool continuing = Time.time - lastSlashEndTime <= comboWindow && ComboIndex < comboSteps.Length - 1;
        ComboIndex = continuing ? ComboIndex + 1 : 0;

        AttackData attack = comboSteps[ComboIndex];
        Vector2 dir = movement.FacingDirection;

        BeginAttack();
        if (hasComboStep) animator.SetInteger(comboStepInt, ComboIndex);
        if (hasAttackTrigger) animator.SetTrigger(attackTrigger);

        yield return new WaitForSeconds(attack.windup);

        bool lastHit = ComboIndex == comboSteps.Length - 1;
        if (showPlaceholderVfx)
        {
            SpawnSlashVfx(dir, attack, slashColor, reverse: ComboIndex % 2 == 1, thicknessScale: lastHit ? 1.4f : 1f);
        }
        PerformHit(dir, attack);

        yield return new WaitForSeconds(attack.recovery);

        EndAttack();
        lastSlashEndTime = Time.time;
        if (lastHit)
        {
            nextSlashAllowedTime = Time.time + comboEndCooldown;
            lastSlashEndTime = -999f; // next press starts a fresh combo
        }
    }

    IEnumerator AreaSlashRoutine()
    {
        ComboIndex = 0;
        lastSlashEndTime = -999f;

        Vector2 dir = movement.FacingDirection;

        BeginAttack();
        if (hasAreaTrigger) animator.SetTrigger(areaSlashTrigger);

        yield return new WaitForSeconds(areaSlash.windup);

        if (showPlaceholderVfx)
        {
            SpawnSlashVfx(dir, areaSlash, areaSlashColor, reverse: false, thicknessScale: 1.6f);
        }
        PerformHit(dir, areaSlash);

        yield return new WaitForSeconds(areaSlash.recovery);

        EndAttack();
        areaSlashReadyTime = Time.time + areaSlashCooldown;
    }

    void BeginAttack()
    {
        IsAttacking = true;
        movement.speedMultiplier = attackMoveSpeedMultiplier;
    }

    void EndAttack()
    {
        IsAttacking = false;
        movement.speedMultiplier = 1f;
    }

    Vector2 AttackOrigin => (Vector2)transform.position + attackOriginOffset;

    void PerformHit(Vector2 dir, AttackData attack)
    {
        Vector2 origin = AttackOrigin;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hittableLayers);
        filter.useTriggers = true;

        overlapResults.Clear();
        struckThisSwing.Clear();
        Physics2D.OverlapCircle(origin, attack.range, filter, overlapResults);

        foreach (Collider2D col in overlapResults)
        {
            if (col.transform.IsChildOf(transform)) continue;

            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || struckThisSwing.Contains(target)) continue;

            // Only hit things inside the arc in front of the player.
            Vector2 toTarget = col.ClosestPoint(origin) - origin;
            if (toTarget.sqrMagnitude > 0.0001f && Vector2.Angle(dir, toTarget) > attack.arcAngle * 0.5f) continue;

            struckThisSwing.Add(target);

            Vector2 pushDir = (Vector2)target.transform.position - origin;
            pushDir = pushDir.sqrMagnitude > 0.0001f ? pushDir.normalized : dir;
            target.TakeDamage(attack.damage, pushDir * attack.knockback);
        }

        if (struckThisSwing.Count > 0)
        {
            if (impulseSource != null && attack.screenShake > 0f)
            {
                impulseSource.GenerateImpulseWithVelocity(dir * attack.screenShake);
            }
            if (hitStopDuration > 0f && !hitStopping) StartCoroutine(HitStopRoutine());
        }
    }

    // Freezes the whole game for a split second when a hit lands, which makes hits feel heavy.
    IEnumerator HitStopRoutine()
    {
        hitStopping = true;
        float previousScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = previousScale;
        hitStopping = false;
    }

    void SpawnSlashVfx(Vector2 dir, AttackData attack, Color color, bool reverse, float thicknessScale)
    {
        ArcEffect.Settings s = new ArcEffect.Settings
        {
            radius = attack.range * 0.9f,
            endRadius = attack.range,
            arcAngle = attack.arcAngle,
            thickness = 0.35f * thicknessScale,
            taperEnds = true,
            color = color,
            sweepTime = 0.08f,
            fadeTime = 0.15f,
            reverseSweep = reverse,
        };

        int layer = playerSprite != null ? playerSprite.sortingLayerID : 0;
        int order = playerSprite != null ? playerSprite.sortingOrder + 1 : 100;
        ArcEffect.Spawn(AttackOrigin, dir, s, effectMaterial, layer, order);
    }

    void OnDrawGizmosSelected()
    {
        Vector2 dir = Application.isPlaying && movement != null ? movement.FacingDirection : Vector2.down;
        DrawArcGizmo(dir, comboSteps != null && comboSteps.Length > 0 ? comboSteps[0] : null, Color.yellow);
        DrawArcGizmo(dir, areaSlash, Color.cyan);
    }

    void DrawArcGizmo(Vector2 dir, AttackData attack, Color color)
    {
        if (attack == null) return;
        Gizmos.color = color;

        Vector3 origin = AttackOrigin;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float half = attack.arcAngle * 0.5f;
        const int steps = 20;

        Vector3 prev = origin;
        for (int i = 0; i <= steps; i++)
        {
            float a = (baseAngle - half + attack.arcAngle * i / steps) * Mathf.Deg2Rad;
            Vector3 p = origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * attack.range;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.DrawLine(prev, origin);
    }
}
