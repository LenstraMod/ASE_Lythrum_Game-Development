using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// The skeleton boss: a floating head and two floating hands. The head never moves; the hands fly anywhere in the arena
// to slam or grab the player. It rises, roars, then loops through its attacks (Slam, Fire Breath, Fear).
// Phase 1: only the hands take real damage. When both hands are destroyed it roars (invulnerable) and phase 2 starts:
// the head takes extra damage. Boss HP reaching 0 kills it and drops the last memory shard.
// Animator triggers are fired for the animation clips, but all timing comes from the fields below, so it works without clips.
public class SkeletonBoss : MonoBehaviour
{
    public enum State { Intro, Idle, Slam, FireBreath, Fear, PhaseTransition, Dead }

    [Header("Parts")]
    [SerializeField] BossPart leftHand;
    [SerializeField] BossPart rightHand;
    [SerializeField] BossPart head;
    [SerializeField] Transform visual;            // parent of all parts, slides up during the intro
    [SerializeField] Transform mouth;             // where the fire breath starts
    [SerializeField] Transform dropPoint;         // where the last shard appears when the boss dies

    [Header("Health")]
    [SerializeField] float handHealth = 90f;          // each hand (a normal slash does 10)
    [SerializeField] float bossHealth = 450f;         // the boss itself, only hurt through the head in the final phase (x3 damage)
    [SerializeField, Range(0f, 1f)] float phase1HeadDamage = 0.01f;   // the head in phase 1 takes this share of normal damage
    [SerializeField] float phase2HeadMultiplier = 3f;

    [Header("Intro / Death")]
    [SerializeField] float riseDuration = 2f;
    [SerializeField] float screamDuration = 3.4f;     // as long as the roar sound (Boss_roar is about 3.4 seconds)
    [SerializeField] float phase2RoarDuration = 2f;
    [SerializeField] float deathDuration = 2.5f;
    [SerializeField] bool codeDrivenMotion = true;   // slide/fade in and move the hands in code; turn off once the animation clips do it
    [SerializeField] float riseDepth = 2.5f;

    [Header("Attack Timing")]
    [SerializeField] float idleTime = 0.5f;
    [Tooltip("Slams come in bursts: this many in a row, hands taking turns, then the cooldown below.")]
    [SerializeField] int slamsPerBurst = 4;
    [Tooltip("Seconds of rest after a burst of slams (counted from when the last slam finishes).")]
    [SerializeField] float slamCooldown = 3f;
    [Tooltip("Seconds between one slam starting and the next inside a burst (min, max).")]
    [SerializeField] Vector2 slamInterval = new Vector2(1f, 1f);
    [SerializeField] float breathInterval = 8f;
    [SerializeField] float fearInterval = 25f;

    [Header("Ground Slam")]
    [SerializeField] float slamRadius = 3.2f;
    [SerializeField] float slamWarning = 0.7f;
    [SerializeField] float slamDamage = 1f;           // the player has 3 health
    [SerializeField] float slamRecovery = 0.3f;

    [Header("Fire Breath (a thick stream of fire from the mouth, see BossFlameStream)")]
    [SerializeField] float breathWindup = 0.8f;
    [SerializeField] float breathRecovery = 1f;
    [SerializeField] float breathShake = 0.12f;        // camera rumble while the boss breathes fire
    [SerializeField] BossFlameStream.Settings flame = new BossFlameStream.Settings();

    [Header("Fear")]
    [SerializeField] float fearRadius = 2f;
    [SerializeField] float fearWarning = 1.5f;
    [SerializeField] float fearLockTime = 5f;
    [SerializeField] float fearSlamInterval = 0.4f;   // fast slams on the caught player, the two hands taking turns
    [SerializeField] float fearSlamDamage = 0.15f;    // many slams, so each one is weak (12 slams = 1.8 of the player's 3)
    [SerializeField] float fearHandSpread = 1.6f;     // how far apart the two hands hover over the caught player
    [SerializeField] float creepyFadeTime = 0.6f;     // the screen turns grey and dark while the player is held
    [SerializeField] float fearHandHeight = 7f;
    [SerializeField] float fearHandScale = 0.35f;

    [Header("Polish")]
    [SerializeField] float bobHeight = 0.15f;                     // the floating boss gently bobs up and down
    [SerializeField] float bobSpeed = 1.5f;
    [SerializeField] float slamKnockback = 7f;                    // push on the player when a slam hits
    [SerializeField] float handBreakShake = 0.35f;                // camera shake when a hand is destroyed

    [Header("Last Hand Standing (rage when only one hand is left)")]
    [SerializeField] float lastHandSpeed = 2f;                    // the hand's slam moves this many times faster
    [SerializeField] float lastHandBreathSize = 1.6f;             // the fire stream is this many times bigger
    [SerializeField] float lastHandSlamCooldownCut = 2f;          // seconds taken off the rest after a burst of slams
    [SerializeField] float lastHandBreathCooldownCut = 2f;        // seconds taken off the wait between fire breaths

    [Header("Final Phase (only the head is left: no hands)")]
    [SerializeField] float finalBreathSize = 2.2f;            // the fire stream is this many times bigger (the last-hand rage is 1.6)
    [SerializeField] float finalAimTurnMultiplier = 2f;       // the fire follows the player this many times faster (30 x 2 = 60 degrees per second)
    [SerializeField] float finalFireSpeedMultiplier = 0.7f;   // the big fire flies at this share of the speed it would get from its size alone (lower = slower fire)
    [SerializeField] float finalBreathInterval = 4f;          // seconds between fire breaths (normally 8)
    [SerializeField] float finalFearInterval = 12f;           // seconds between Fear attacks (normally 25)
    [SerializeField] float finalFearSpeed = 1.6f;             // Fear is this many times faster: shorter warning, quicker slams (each hurts less, same total)

    [Header("Slam Impact: boom, then the ground shakes")]
    [SerializeField] float quakeDelay = 0.15f;       // seconds between the boom and the earthquake
    [SerializeField] float quakeDuration = 0.8f;
    [SerializeField] float quakeStrength = 0.6f;
    [SerializeField] float roarShake = 0.25f;        // camera shake while the boss roars

    [Header("Art (filled in by Lythrum > Setup > Shards + Boss)")]
    [SerializeField] Sprite circleSprite;              // plain circle, tinted for the slam and fear warnings
    [SerializeField] Sprite[] shockwaveFrames;
    [SerializeField] float shockwaveScale = 2.2f;
    [SerializeField] Sprite[] flameFrames;
    [SerializeField] Sprite fearHandSprite;
    [SerializeField] GameObject shardPrefab;
    [SerializeField] string dropShardId = "dungeon_boss_drop";

    static readonly Color SlamColor = new Color(1f, 0.5f, 0.1f, 0.4f);
    static readonly Color FearColor = new Color(1f, 0.05f, 0.05f, 0.5f);

    // What the camera looks at during the intro.
    public Transform CameraTarget => head.transform;

    // True while exactly one hand is still alive: the boss gets faster and bigger.
    bool LastHand => AliveHands().Count == 1;
    float SlamCooldown => Mathf.Max(0.5f, slamCooldown - (LastHand ? lastHandSlamCooldownCut : 0f));
    float BreathInterval => InPhase2 ? finalBreathInterval : Mathf.Max(2f, breathInterval - (LastHand ? lastHandBreathCooldownCut : 0f));
    float FearInterval => InPhase2 ? finalFearInterval : fearInterval;
    float FearSpeed => InPhase2 ? finalFearSpeed : 1f;
    bool HasHands => leftHandHp > 0f || rightHandHp > 0f;       // the ground slam is done by the hands, so no hands = no slams

    // Set by whichever arena trigger the player walks into first, so the other entrances don't start a second intro.
    public bool EncounterStarted { get; set; }

    public State CurrentState { get; private set; } = State.Intro;
    public bool InPhase2 { get; private set; }
    public float BossRatio => Mathf.Clamp01(bossHp / bossHealth);
    public float LeftHandRatio => Mathf.Clamp01(leftHandHp / handHealth);
    public float RightHandRatio => Mathf.Clamp01(rightHandHp / handHealth);

    // Stays true after the boss is killed, so it doesn't come back when a scene is reloaded.
    public static bool Defeated { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Defeated = false;

    float bossHp, leftHandHp, rightHandHp;
    Animator animator;
    PlayerMovement player;
    PlayerHealth playerHealth;
    BossHud hud;
    CreepyScreen creepy;
    AudioSource fireSound, fearSound;      // long sounds that are cut off when the attack ends (or the boss is reset)
    float bobWeight;
    SpriteRenderer[] renderers;
    Vector3 leftHandRest, rightHandRest;
    int activeSlams, lastSlammerIndex = -1;                       // slams in progress; which hand slammed last
    readonly HashSet<Transform> busySlammers = new HashSet<Transform>();   // hands in the middle of a slam
    readonly List<GameObject> spawned = new List<GameObject>();   // warnings, flames, fear hand: removed on reset/death

    // Time Stop: while time is stopped the boss and everything it spawned stand still (the player can still hit it).
    // The boss keeps its own clock that skips stopped time, so cooldowns, waits and movements all simply pause.
    float clock;
    bool Frozen => TimeStopManager.IsTimeStopped;
    float Dt => Frozen ? 0f : Time.deltaTime;

    // Like WaitForSeconds, but time stopped by Time Stop does not count.
    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt) yield return null;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        leftHandRest = leftHand.transform.localPosition;
        rightHandRest = rightHand.transform.localPosition;
    }

    void Update()
    {
        if (animator != null) animator.speed = Frozen ? 0f : 1f;     // animation clips pause too
        if (Frozen) return;
        clock += Time.deltaTime;

        bool bobbing = codeDrivenMotion && visual != null && CurrentState != State.Intro && CurrentState != State.Dead;
        if (!bobbing) { bobWeight = 0f; return; }

        bobWeight = Mathf.MoveTowards(bobWeight, 1f, Time.deltaTime);   // fades the bob in after the intro
        visual.localPosition = Vector3.up * (Mathf.Sin(clock * bobSpeed) * bobHeight * bobWeight);
    }

    // ------------------------------------------------------------------ encounter control (used by BossArena)

    // Starts the intro. The arena keeps the player frozen until CurrentState leaves Intro.
    public void Begin()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;

        bossHp = bossHealth;
        leftHandHp = rightHandHp = handHealth;
        InPhase2 = false;
        CurrentState = State.Intro;
        MusicManager.PlayOverride("BossFight");     // the boss song takes over from the dungeon music
        StartCoroutine(IntroRoutine());
    }

    // Puts everything back as it was before the fight and hides the boss (the arena calls this when the player dies).
    public void ResetBoss()
    {
        StopAllCoroutines();
        CleanUp();
        if (hud != null) Destroy(hud.gameObject);

        leftHand.Restore();
        rightHand.Restore();
        leftHand.transform.localPosition = leftHandRest;
        rightHand.transform.localPosition = rightHandRest;

        InPhase2 = false;
        CurrentState = State.Intro;
        EncounterStarted = false;
        MusicManager.ClearOverride();               // the player died: back to the dungeon music
        gameObject.SetActive(false);
    }

    // Used by the arena when the boss was already beaten but its shard was never picked up.
    public void SpawnDropShard()
    {
        if (shardPrefab == null) { Debug.LogWarning("[SkeletonBoss] No shard prefab assigned, the boss drop can't spawn."); return; }
        if (ShardManager.IsCollected(dropShardId)) return;

        Vector3 position = dropPoint != null ? dropPoint.position : transform.position;
        GameObject shard = Instantiate(shardPrefab, position, Quaternion.identity);
        shard.GetComponent<MemoryShard>().SetId(dropShardId);
    }

    // ------------------------------------------------------------------ damage

    // Called by BossPart. Returns false if the hit did nothing.
    public bool ReceiveHit(BossPartType part, float damage)
    {
        if (CurrentState == State.Intro || CurrentState == State.PhaseTransition || CurrentState == State.Dead) return false;

        if (!InPhase2)
        {
            if (part == BossPartType.LeftHand) { leftHandHp -= damage; if (leftHandHp <= 0f) HandDestroyed(leftHand); }
            else if (part == BossPartType.RightHand) { rightHandHp -= damage; if (rightHandHp <= 0f) HandDestroyed(rightHand); }
            else bossHp -= damage * phase1HeadDamage;
        }
        else if (part == BossPartType.Head) bossHp -= damage * phase2HeadMultiplier;
        else return false;   // hands can't be hit in phase 2

        if (bossHp <= 0f && CurrentState != State.Dead) Die();
        return true;
    }

    void HandDestroyed(BossPart hand)
    {
        hand.Break();                                   // fades out instead of vanishing
        bool bothGone = leftHandHp <= 0f && rightHandHp <= 0f;
        if (bothGone)
        {
            StopAllCoroutines();                        // cancels whatever attack was running
            CleanUp();
        }

        AudioManager.Play(Sfx.BossSlam);
        StartCoroutine(ShakeCamera(0.4f, handBreakShake));
        if (bothGone) StartCoroutine(PhaseTwoRoutine());
    }

    void Die()
    {
        StopAllCoroutines();
        CleanUp();
        CurrentState = State.Dead;
        Defeated = true;
        MusicManager.ClearOverride();               // the boss is dead: the dungeon music fades back in
        Trigger("Die");
        StartCoroutine(DeathRoutine());
    }

    // ------------------------------------------------------------------ states

    IEnumerator IntroRoutine()
    {
        Trigger("Rise");
        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            if (codeDrivenMotion)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / riseDuration);
                visual.localPosition = Vector3.down * (riseDepth * (1f - t));
                SetAlpha(t);
            }
            yield return null;
        }
        if (codeDrivenMotion) { visual.localPosition = Vector3.zero; SetAlpha(1f); }

        Trigger("Scream");
        Roar(screamDuration);
        yield return new WaitForSeconds(screamDuration);

        hud = BossHud.Create(this);
        StartCoroutine(FightLoop());
    }

    IEnumerator PhaseTwoRoutine()
    {
        CurrentState = State.PhaseTransition;
        Trigger("Phase2Roar");
        Roar(phase2RoarDuration);
        yield return new WaitForSeconds(phase2RoarDuration);
        InPhase2 = true;
        StartCoroutine(FightLoop());
    }

    IEnumerator DeathRoutine()
    {
        Roar(deathDuration);
        for (float t = 0f; t < deathDuration; t += Time.deltaTime)
        {
            if (codeDrivenMotion)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / deathDuration);
                visual.localPosition = Vector3.down * (riseDepth * k);      // sinks back into the ground, the opposite of the rise
                SetAlpha(1f - k);
            }
            yield return null;
        }

        SpawnDropShard();
        if (hud != null) Destroy(hud.gameObject);
        gameObject.SetActive(false);
    }

    // Slams come in bursts (slamsPerBurst in a row, the hands taking turns and overlapping), then slamCooldown seconds of rest.
    // Fire Breath and Fear wait for the running slams to finish first, so only one of those attacks is ever active.
    // Fear first, then Fire Breath, then Slam.
    IEnumerator FightLoop()
    {
        float nextSlam = clock + Random.Range(slamInterval.x, slamInterval.y);
        float nextBreath = clock + BreathInterval;
        float nextFear = clock + FearInterval;
        int slamsFired = 0;
        bool burstFinished = false;      // the last slam of the burst has started, waiting for it to end before the cooldown

        while (true)
        {
            if (activeSlams == 0) CurrentState = State.Idle;
            yield return null;

            bool fearDue = clock >= nextFear;
            bool breathDue = clock >= nextBreath;

            if (fearDue || breathDue)
            {
                if (activeSlams > 0) continue;       // no new slams, let the running ones finish

                if (fearDue) { yield return FearRoutine(); nextFear = clock + FearInterval; }
                else { yield return BreathRoutine(); nextBreath = clock + BreathInterval; }

                CurrentState = State.Idle;
                yield return Wait(idleTime);
                slamsFired = 0;                      // a fresh burst after the big attack
                burstFinished = false;
                nextSlam = clock;
            }
            else if (burstFinished)
            {
                if (activeSlams == 0)
                {
                    burstFinished = false;
                    nextSlam = clock + SlamCooldown;
                }
            }
            else if (HasHands && clock >= nextSlam)
            {
                // If every hand is still busy, just try again next frame (the slam is not counted).
                Transform slammer = NextSlammer();
                if (slammer != null)
                {
                    StartCoroutine(SlamRoutine(slammer));
                    slamsFired++;
                    if (slamsFired >= slamsPerBurst) { slamsFired = 0; burstFinished = true; }
                    else nextSlam = clock + Random.Range(slamInterval.x, slamInterval.y) / (LastHand ? lastHandSpeed : 1f);
                }
            }
        }
    }

    // ------------------------------------------------------------------ attacks

    IEnumerator SlamRoutine(Transform slammer)
    {
        CurrentState = State.Slam;
        activeSlams++;
        busySlammers.Add(slammer);
        float speed = LastHand ? lastHandSpeed : 1f;     // the last hand moves much faster
        Vector3 target = player.transform.position;
        target.z = 0f;

        GameObject warning = SpawnCircle(target, slamRadius, SlamColor, slamWarning / speed, 2f);   // stays on the ground where the player was
        Trigger("Slam");

        if (codeDrivenMotion) yield return MoveTo(slammer, target + Vector3.up * 2.5f, slamWarning / speed);   // hand rises over the spot
        else yield return Wait(slamWarning / speed);

        if (codeDrivenMotion) yield return MoveTo(slammer, target, 0.12f / speed);                              // and comes down

        if (playerHealth != null && Vector2.Distance(player.transform.position, target) <= slamRadius)
        {
            playerHealth.TakeDamage(slamDamage);
            Vector2 away = player.transform.position - target;
            player.Knockback((away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitCircle.normalized) * slamKnockback);
        }
        if (!isActiveAndEnabled) yield break;   // the hit killed the player and the arena reset the boss
        Impact(target);
        if (warning != null) Destroy(warning);

        if (codeDrivenMotion)
        {
            yield return Wait(0.25f / speed);
            yield return MoveToRest(slammer, 0.4f / speed);
        }
        yield return Wait(slamRecovery / speed);

        busySlammers.Remove(slammer);
        activeSlams--;
    }

    IEnumerator BreathRoutine()
    {
        CurrentState = State.FireBreath;
        Trigger("Breath");
        StartCoroutine(ShakeCamera(breathWindup, 0.06f));      // a low rumble while the boss draws breath
        yield return Wait(breathWindup);

        if (flameFrames == null || flameFrames.Length == 0)
        {
            Debug.LogWarning("[SkeletonBoss] No flame frames assigned, the fire breath is skipped.");
            yield break;
        }

        // The stream aims itself at the player; this object just lives as long as the stream does.
        float sizeFactor = InPhase2 ? finalBreathSize : (LastHand ? lastHandBreathSize : 1f);
        BossFlameStream.Settings settings = sizeFactor > 1f ? flame.Scaled(sizeFactor) : flame;
        if (InPhase2)
        {
            settings.aimTurnRate *= finalAimTurnMultiplier;       // the fire keeps up with the player much better
            settings.puffSpeed *= finalFireSpeedMultiplier;       // but is not too fast
        }
        GameObject stream = new GameObject("Boss Fire Breath");
        stream.AddComponent<BossFlameStream>().Init(mouth != null ? mouth : head.transform, player.transform, playerHealth, flameFrames, settings);
        spawned.Add(stream);
        fireSound = AudioManager.PlayFor(Sfx.BossFire, settings.duration, true, 0.6f);     // cut off with a fade when the stream ends
        StartCoroutine(ShakeCamera(settings.duration, breathShake * sizeFactor));

        yield return Wait(settings.duration + breathRecovery);
    }

    IEnumerator FearRoutine()
    {
        CurrentState = State.Fear;
        Vector3 zoneCenter = player.transform.position;
        zoneCenter.z = 0f;

        float fearSpeed = FearSpeed;
        float warningTime = fearWarning / fearSpeed;
        float slamEvery = fearSlamInterval / fearSpeed;
        float slamHurt = fearSlamDamage / fearSpeed;       // quicker slams hurt less each, so the total stays the same

        GameObject zone = SpawnCircle(zoneCenter, fearRadius, FearColor, warningTime, 5f);   // pulses faster as the hands come down

        // Every living hand floats over to the player (left hand on the left, right on the right). With no hands left
        // (phase 2), two spare hands reach down from above instead.
        List<BossPart> hands = AliveHands();
        bool spare = hands.Count == 0;
        List<Transform> catchers = new List<Transform>();
        List<Vector3> spots = new List<Vector3>();
        int count = spare ? 2 : hands.Count;
        for (int i = 0; i < count; i++)
        {
            bool onLeft = spare ? i == 0 : hands[i].Type == BossPartType.LeftHand;
            float x = count == 1 ? 0f : (onLeft ? -fearHandSpread : fearHandSpread);
            Vector3 spot = zoneCenter + new Vector3(x, 0.8f, 0f);

            Transform catcher;
            if (spare)
            {
                GameObject spareHand = SpawnFearHand(spot + Vector3.up * fearHandHeight);
                if (spareHand == null) continue;
                catcher = spareHand.transform;
            }
            else
            {
                catcher = hands[i].transform;
            }
            catchers.Add(catcher);
            spots.Add(spot);
        }

        for (int i = 0; i < catchers.Count; i++) StartCoroutine(MoveTo(catchers[i], spots[i], warningTime));
        yield return Wait(warningTime);

        bool caught = Vector2.Distance(player.transform.position, zoneCenter) <= fearRadius;
        if (!caught)
        {
            yield return ReleaseFear(zone, catchers, spare);
            yield break;
        }

        // Caught: frozen for fearLockTime while the hands take turns slamming, fast. Only the player is hit.
        Trigger("FearCatch");
        player.controlLocked = true;
        SetCreepy(true);
        fearSound = AudioManager.PlayFor(Sfx.BossFearCatch, fearLockTime, false, 0.5f);    // the clip is as long as the grab
        if (playerHealth != null) playerHealth.HitFlash();     // caught: flash, boom and shake right as the hands close in
        Impact(zoneCenter);
        float end = clock + fearLockTime;
        float nextSlam = clock + slamEvery;
        int turn = 0;
        while (clock < end)
        {
            if (clock >= nextSlam)
            {
                nextSlam += slamEvery;
                Trigger("Slam");
                if (playerHealth != null) playerHealth.TakeDamage(slamHurt, false);   // false: every slam lands
                if (!isActiveAndEnabled) yield break;   // the hit killed the player and the arena reset the boss
                Impact(player.transform.position);
                if (catchers.Count > 0) StartCoroutine(Bounce(catchers[turn++ % catchers.Count]));
            }
            yield return null;
        }

        player.controlLocked = false;
        SetCreepy(false);
        yield return ReleaseFear(zone, catchers, spare);
    }

    // Removes the red zone, then sends real hands back to where they were (spare hands just disappear).
    IEnumerator ReleaseFear(GameObject zone, List<Transform> catchers, bool spare)
    {
        if (zone != null) Destroy(zone);
        if (catchers.Count == 0) yield break;

        if (spare)
        {
            foreach (Transform hand in catchers) if (hand != null) Destroy(hand.gameObject);
            yield break;
        }

        for (int i = 0; i < catchers.Count; i++) StartCoroutine(MoveToRest(catchers[i], 0.5f));
        yield return Wait(0.5f);
    }

    // ------------------------------------------------------------------ helpers

    List<BossPart> AliveHands()
    {
        List<BossPart> alive = new List<BossPart>();
        if (leftHandHp > 0f) alive.Add(leftHand);
        if (rightHandHp > 0f) alive.Add(rightHand);
        return alive;
    }

    // The living hand that did not slam last, so the hands take turns. A hand that is still busy is skipped (returns null
    // if everyone is busy or there are no hands: the head never slams).
    Transform NextSlammer()
    {
        List<BossPart> alive = AliveHands();
        if (alive.Count == 0) return null;

        for (int i = 1; i <= alive.Count; i++)
        {
            Transform candidate = alive[(lastSlammerIndex + i) % alive.Count].transform;
            if (busySlammers.Contains(candidate)) continue;
            lastSlammerIndex = alive.IndexOf(candidate.GetComponent<BossPart>());
            return candidate;
        }
        return null;
    }

    IEnumerator MoveTo(Transform t, Vector3 destination, float time)
    {
        Vector3 start = t.position;
        for (float elapsed = 0f; elapsed < time; elapsed += Dt)
        {
            t.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, elapsed / time));
            yield return null;
        }
        t.position = destination;
    }

    // Moves a hand back to its resting spot on the boss. The spot is looked up every frame, so the bobbing of the whole boss
    // never makes the hand end up in the wrong place.
    IEnumerator MoveToRest(Transform hand, float time)
    {
        Vector3 restLocal = hand == leftHand.transform ? leftHandRest : rightHandRest;
        Vector3 start = hand.position;
        for (float elapsed = 0f; elapsed < time; elapsed += Dt)
        {
            Vector3 rest = hand.parent.TransformPoint(restLocal);
            hand.position = Vector3.Lerp(start, rest, Mathf.SmoothStep(0f, 1f, elapsed / time));
            yield return null;
        }
        hand.localPosition = restLocal;
    }

    IEnumerator Bounce(Transform t)
    {
        Vector3 down = t.position;
        yield return MoveTo(t, down + Vector3.up * 1.2f, 0.12f);
        yield return MoveTo(t, down, 0.07f);
    }

    GameObject SpawnCircle(Vector3 position, float radius, Color color, float warningTime, float pulses)
    {
        if (circleSprite == null)
        {
            Debug.LogWarning("[SkeletonBoss] No circle sprite assigned, warning zones are invisible.");
            return null;
        }

        GameObject go = new GameObject("Boss Warning");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * (radius * 2f / circleSprite.bounds.size.x);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = color;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = -10;           // above the ground, below the player
        WarningPulse.Begin(go, warningTime, pulses);
        spawned.Add(go);
        return go;
    }

    GameObject SpawnFearHand(Vector3 position)
    {
        if (fearHandSprite == null)
        {
            Debug.LogWarning("[SkeletonBoss] No fear hand sprite assigned.");
            return null;
        }

        GameObject go = new GameObject("Boss Fear Hand");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, 180f);   // the hand art points up, this one reaches down
        go.transform.localScale = Vector3.one * fearHandScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = fearHandSprite;
        sr.sortingLayerName = "WalkBehind";
        sr.sortingOrder = 5;
        spawned.Add(go);
        return go;
    }

    // A slam lands: boom and shockwave now, then the earthquake (rumble + camera shake) a moment later.
    void Impact(Vector3 position)
    {
        AudioManager.Play(Sfx.BossSlam);
        SpawnShockwave(position);
        StartCoroutine(QuakeRoutine());
    }

    void SpawnShockwave(Vector3 position)
    {
        if (shockwaveFrames == null || shockwaveFrames.Length == 0)
        {
            Debug.LogWarning("[SkeletonBoss] No shockwave frames assigned.");
            return;
        }
        spawned.RemoveAll(go => go == null);      // forget the ones that already finished
        spawned.Add(SpriteFramePlayer.Spawn(shockwaveFrames, position, shockwaveScale, 60f, false, "Player", 4));
    }

    IEnumerator QuakeRoutine()
    {
        yield return new WaitForSeconds(quakeDelay);
        AudioManager.Play(Sfx.BossQuake);
        yield return ShakeCamera(quakeDuration, quakeStrength);
    }

    void Roar(float duration)
    {
        AudioManager.Play(Sfx.BossRoar);
        StartCoroutine(ShakeCamera(duration, roarShake));
    }

    // Random camera kicks that fade out, using the player's Cinemachine impulse source (the same one the sword uses).
    IEnumerator ShakeCamera(float duration, float strength)
    {
        CinemachineImpulseSource source = player != null ? player.GetComponent<CinemachineImpulseSource>() : null;
        if (source == null) yield break;

        const float step = 0.04f;
        for (float t = 0f; t < duration; t += step)
        {
            source.GenerateImpulseWithVelocity(Random.insideUnitCircle.normalized * (strength * (1f - t / duration)));
            yield return new WaitForSeconds(step);
        }
    }

    void SetAlpha(float alpha)
    {
        foreach (SpriteRenderer sr in renderers)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    // Removes everything the attacks created and frees the player if they were being held.
    void CleanUp()
    {
        foreach (GameObject go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
        activeSlams = 0;
        busySlammers.Clear();
        if (creepy != null) creepy.SetInstant(0f);
        AudioManager.Stop(fireSound);
        AudioManager.Stop(fearSound);
        if (player != null) player.controlLocked = false;
    }

    // The grey, dark Fear atmosphere.
    void SetCreepy(bool on)
    {
        if (creepy == null)
        {
            if (!on) return;
            creepy = CreepyScreen.Create();
        }
        creepy.FadeTo(on ? 1f : 0f, creepyFadeTime);
    }

    void Trigger(string triggerName)
    {
        if (animator != null && animator.HasParameter(triggerName)) animator.SetTrigger(triggerName);
    }
}
