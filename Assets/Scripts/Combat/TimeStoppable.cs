using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Add this to anything that should freeze during Time Stop (enemies, projectiles, the Shadow, ...).
// While frozen: animators pause, the Rigidbody2D is locked in place, particles pause,
// and the scripts listed in "Disable While Frozen" stop running (e.g. AI or ShadowPlayback).
public class TimeStoppable : MonoBehaviour
{
    [Tooltip("Scripts to turn off while frozen, e.g. enemy AI or ShadowPlayback.")]
    public Behaviour[] disableWhileFrozen;

    public UnityEvent onFrozen;
    public UnityEvent onResumed;

    public event Action Frozen;
    public event Action Resumed;

    public bool IsFrozen { get; private set; }

    private Rigidbody2D rb;
    private Animator[] animators;
    private ParticleSystem[] particles;

    private Vector2 savedVelocity;
    private float savedAngularVelocity;
    private RigidbodyConstraints2D savedConstraints;
    private readonly List<float> savedAnimatorSpeeds = new List<float>();
    private readonly List<bool> savedBehaviourStates = new List<bool>();
    private readonly List<ParticleSystem> pausedParticles = new List<ParticleSystem>();

    // Knockback received while frozen is stored and released when time resumes.
    private Vector2 queuedImpulse;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animators = GetComponentsInChildren<Animator>(true);
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    void OnEnable()
    {
        TimeStopManager.TimeStopChanged += OnTimeStopChanged;
        if (TimeStopManager.IsTimeStopped) Freeze();
    }

    void OnDisable()
    {
        TimeStopManager.TimeStopChanged -= OnTimeStopChanged;
        if (IsFrozen) Resume();
    }

    void OnTimeStopChanged(bool stopped)
    {
        if (stopped) Freeze();
        else Resume();
    }

    public void QueueImpulse(Vector2 impulse)
    {
        queuedImpulse += impulse;
    }

    void Freeze()
    {
        if (IsFrozen) return;
        IsFrozen = true;

        savedAnimatorSpeeds.Clear();
        foreach (Animator anim in animators)
        {
            savedAnimatorSpeeds.Add(anim.speed);
            anim.speed = 0f;
        }

        if (rb != null)
        {
            savedVelocity = rb.linearVelocity;
            savedAngularVelocity = rb.angularVelocity;
            savedConstraints = rb.constraints;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        pausedParticles.Clear();
        foreach (ParticleSystem ps in particles)
        {
            if (ps.isPlaying)
            {
                ps.Pause(false);
                pausedParticles.Add(ps);
            }
        }

        savedBehaviourStates.Clear();
        if (disableWhileFrozen != null)
        {
            foreach (Behaviour b in disableWhileFrozen)
            {
                savedBehaviourStates.Add(b != null && b.enabled);
                if (b != null) b.enabled = false;
            }
        }

        onFrozen?.Invoke();
        Frozen?.Invoke();
    }

    void Resume()
    {
        if (!IsFrozen) return;
        IsFrozen = false;

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null) animators[i].speed = savedAnimatorSpeeds[i];
        }

        if (rb != null)
        {
            rb.constraints = savedConstraints;
            rb.linearVelocity = savedVelocity;
            rb.angularVelocity = savedAngularVelocity;
            if (queuedImpulse != Vector2.zero) rb.AddForce(queuedImpulse, ForceMode2D.Impulse);
        }
        queuedImpulse = Vector2.zero;

        foreach (ParticleSystem ps in pausedParticles)
        {
            if (ps != null) ps.Play(false);
        }
        pausedParticles.Clear();

        if (disableWhileFrozen != null)
        {
            for (int i = 0; i < disableWhileFrozen.Length && i < savedBehaviourStates.Count; i++)
            {
                if (disableWhileFrozen[i] != null) disableWhileFrozen[i].enabled = savedBehaviourStates[i];
            }
        }

        onResumed?.Invoke();
        Resumed?.Invoke();
    }
}
