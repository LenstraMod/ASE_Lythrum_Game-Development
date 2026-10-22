using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// The trigger at the entrance of the boss room. When the player walks in, it freezes them, sends the camera to the boss
// while it rises and roars, then returns the camera and unfreezes the player.
// If the player dies, the boss is reset and hidden again, so the intro plays on the next visit.
// Regular enemies are removed from the boss room (clearArea) and kept out while the fight lasts.
// The room has several entrances: put one of these trigger boxes on each. They share the same boss, so only the first
// one the player touches starts the fight. A copy of a trigger finds the boss and the room area by itself.
[RequireComponent(typeof(Collider2D))]
public class BossArena : MonoBehaviour
{
    [SerializeField] SkeletonBoss boss;               // leave empty to find the boss in the scene automatically
    [SerializeField] float introZoom = 6.5f;          // camera orthographic size while it watches the boss (normal is 5)
    [SerializeField] float cameraTravelTime = 1f;     // seconds to move to the boss, and the same to come back
    [SerializeField] Bounds clearArea;                // the boss room: ordinary enemies inside it are removed (empty = copy it from another trigger)

    public Bounds ClearArea => clearArea;

    PlayerMovement player;
    PlayerHealth health;

    void Reset() => GetComponent<Collider2D>().isTrigger = true;

    void Awake()
    {
        if (boss == null) boss = FindAnyObjectByType<SkeletonBoss>(FindObjectsInactive.Include);

        if (clearArea.size == Vector3.zero)
        {
            foreach (BossArena other in FindObjectsByType<BossArena>(FindObjectsInactive.Include))
            {
                if (other.clearArea.size == Vector3.zero) continue;
                clearArea = other.clearArea;
                break;
            }
        }

        if (boss == null) Debug.LogWarning("[BossArena] " + name + ": no SkeletonBoss found in the scene.", this);
    }

    void Start()
    {
        if (boss == null) return;
        ClearEnemies();

        // Boss already beaten earlier in this session (scene was reloaded): no fight, but give the shard if it was missed.
        if (SkeletonBoss.Defeated)
        {
            boss.SpawnDropShard();
            gameObject.SetActive(false);
        }
    }

    // Stay (not Enter), so a player who was busy with a shard pickup when they stepped in still starts the fight afterwards.
    void OnTriggerStay2D(Collider2D other)
    {
        if (boss == null || boss.EncounterStarted || ShardManager.Busy || !other.CompareTag("Player")) return;

        player = other.GetComponent<PlayerMovement>();
        health = other.GetComponent<PlayerHealth>();
        if (player == null || health == null) return;

        boss.EncounterStarted = true;      // set before anything else, so a second trigger touched at the same moment does nothing
        StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        player.controlLocked = true;
        health.cutsceneInvulnerable = true;
        health.Respawned += ResetArena;
        StartCoroutine(KeepArenaClear());

        CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
        if (cam == null) Debug.LogWarning("[BossArena] No CinemachineCamera in the scene, the intro has no camera move.");
        Transform originalTarget = cam != null ? cam.Follow : null;
        float originalSize = cam != null ? cam.Lens.OrthographicSize : 0f;

        boss.gameObject.SetActive(true);
        boss.Begin();

        // Look at the boss while it rises and roars.
        if (cam != null)
        {
            cam.Follow = boss.CameraTarget;
            yield return CameraFocus.Zoom(cam, originalSize, introZoom, cameraTravelTime);
        }

        while (boss.CurrentState == SkeletonBoss.State.Intro) yield return null;

        if (cam != null)
        {
            cam.Follow = originalTarget;
            yield return CameraFocus.Zoom(cam, introZoom, originalSize, cameraTravelTime);
        }

        player.controlLocked = false;
        health.cutsceneInvulnerable = false;
    }

    // Enemies that chase the player into the room are removed too, until the boss is dead.
    IEnumerator KeepArenaClear()
    {
        while (boss.CurrentState != SkeletonBoss.State.Dead)
        {
            ClearEnemies();
            yield return new WaitForSeconds(0.5f);
        }
    }

    void ClearEnemies()
    {
        foreach (EnemyBrain enemy in FindObjectsByType<EnemyBrain>())
        {
            if (clearArea.Contains(enemy.transform.position)) Destroy(enemy.gameObject);
        }
    }

    void ResetArena()
    {
        health.Respawned -= ResetArena;
        if (SkeletonBoss.Defeated) return;     // died after winning: nothing to reset

        StopAllCoroutines();
        player.controlLocked = false;
        health.cutsceneInvulnerable = false;
        boss.ResetBoss();
    }

    void OnDestroy()
    {
        if (health != null) health.Respawned -= ResetArena;
    }
}
