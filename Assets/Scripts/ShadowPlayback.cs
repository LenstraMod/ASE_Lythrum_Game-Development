using System.Collections.Generic;
using UnityEngine;

// The clone made by PlayerRecord (E): replays the recorded movement, and strikes whenever you struck while recording.
public class ShadowPlayback : MonoBehaviour
{
    private List<PlayerSnapshot> playbackData;
    private bool isPlaying = false;
    private float playbackTimer = 0f;
    private int currentIndex = 0;

    public Animator shadowAnimator;

    [Header("Shadow attack (replays your Fire1 presses)")]
    public int damage = 10;
    public float attackRange = 1.3f;
    [Range(10f, 360f)] public float attackArc = 110f;
    public float knockback = 2.5f;
    public Color slashColor = new Color(0.7f, 0.55f, 1f, 0.95f);

    private SpriteRenderer sprite;
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private readonly HashSet<Health> struck = new HashSet<Health>();
    private int attackCount;

    public void Initialize(List<PlayerSnapshot> data)
    {
        playbackData = data;
        isPlaying = true;
        playbackTimer = 0f;
        currentIndex = 0;
        sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!isPlaying || playbackData == null || playbackData.Count == 0) return;

        playbackTimer += Time.deltaTime;

        // Fast-forward through the list until we match the current playback time
        while (currentIndex < playbackData.Count - 1 && playbackData[currentIndex + 1].timestamp <= playbackTimer)
        {
            currentIndex++;
            ExecuteActions(playbackData[currentIndex]);
        }

        // Smoothly interpolate position between the current snapshot and the next one
        if (currentIndex < playbackData.Count - 1)
        {
            PlayerSnapshot currentSnap = playbackData[currentIndex];
            PlayerSnapshot nextSnap = playbackData[currentIndex + 1];

            float timeBetween = nextSnap.timestamp - currentSnap.timestamp;
            float timePassed = playbackTimer - currentSnap.timestamp;
            float lerpPercent = timeBetween > 0f ? timePassed / timeBetween : 1f;

            transform.position = Vector3.Lerp(currentSnap.position, nextSnap.position, lerpPercent);

            shadowAnimator.SetFloat("LastInputX", currentSnap.lastInputX);
            shadowAnimator.SetFloat("LastInputY", currentSnap.lastInputY);
            shadowAnimator.SetFloat("InputX", currentSnap.inputX);
            shadowAnimator.SetFloat("InputY", currentSnap.inputY);
            shadowAnimator.SetBool("isWalking", currentSnap.isWalking);
        }
        else
        {
            // Playback finished
            AudioManager.Play(Sfx.ShadowEnd);
            Destroy(gameObject);
        }
    }

    void ExecuteActions(PlayerSnapshot snapshot)
    {
        if (snapshot.isAttacking) Attack(snapshot);

        if (snapshot.isInteracting)
        {
            Debug.Log("Shadow is interacting!");
            // Trigger shadow interact logic here
        }
    }

    // The same arc as the player's first combo hit, in the direction the player was facing.
    void Attack(PlayerSnapshot snapshot)
    {
        Vector2 dir = new Vector2(snapshot.lastInputX, snapshot.lastInputY);
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.down;

        AudioManager.Play(Sfx.PlayerSlash);
        int layer = sprite != null ? sprite.sortingLayerID : 0;
        int order = sprite != null ? sprite.sortingOrder + 2 : 100;
        bool mirrored = (attackCount++ % 2) == 1;
        if (!CombatVfx.Slash(transform.position, dir, attackRange, mirrored, false, slashColor, layer, order, null)) { /* no effect library: sound only */ }

        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        overlapResults.Clear();
        struck.Clear();
        Physics2D.OverlapCircle(transform.position, attackRange, filter, overlapResults);

        bool hitSomething = false;
        foreach (Collider2D col in overlapResults)
        {
            if (col.transform.IsChildOf(transform)) continue;

            Health target = col.GetComponentInParent<Health>();
            if (target == null || target.IsDead || struck.Contains(target)) continue;

            Vector2 toTarget = col.ClosestPoint(transform.position) - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f && Vector2.Angle(dir, toTarget) > attackArc * 0.5f) continue;

            struck.Add(target);
            hitSomething = true;
            Vector2 push = (Vector2)target.transform.position - (Vector2)transform.position;
            target.TakeDamage(damage, (push.sqrMagnitude > 0.0001f ? push.normalized : dir) * knockback);
        }
        if (hitSomething) AudioManager.Play(Sfx.HitImpact);
    }
}
