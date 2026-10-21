using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Remembers which memory shards were collected (the list lives in a static, so it survives scene loads)
// and plays the pickup sequence: lock the player, move and zoom the camera to the shard, hold, go back.
// Each scene has one of these; if a scene has none, one is created on the first pickup.
public class ShardManager : MonoBehaviour
{
    [SerializeField] float zoomSize = 2.5f;       // camera orthographic size while looking at the shard
    [SerializeField] float travelTime = 1.1f;     // seconds to ease the zoom in on the shard
    [SerializeField] float holdTime = 1f;         // seconds to stay on the shard once the camera has arrived
    [SerializeField] float returnTime = 0.8f;     // seconds to ease back out to the player (a little quicker than going in)
    [SerializeField] float maxSettleWait = 0.7f;  // longest extra wait for the camera to catch up (it lags behind its target)

    static readonly HashSet<string> collected = new HashSet<string>();
    static ShardManager instance;

    // The Dungeon gate stays shut until all of these are collected (the shards in the Town and the Desert).
    // Keep in sync with the shard placements in Assets/Editor/LythrumBossSetup.cs.
    public static readonly string[] DungeonKeyShards = { "town_01", "town_02", "desert_01", "desert_02" };

    public static bool Busy { get; private set; }
    public static bool IsCollected(string id) => collected.Contains(id);

    // With "Reload Domain" off in the editor, statics survive Play sessions: start every session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        collected.Clear();
        instance = null;
        Busy = false;
    }

    void Awake() => instance = this;

    // Called by a shard when the player touches it. Ignored while another pickup is running.
    public static void Pickup(MemoryShard shard)
    {
        if (Busy || shard == null || IsCollected(shard.Id)) return;

        if (instance == null) instance = new GameObject("Shard Manager").AddComponent<ShardManager>();
        instance.StartCoroutine(instance.Sequence(shard));
    }

    IEnumerator Sequence(MemoryShard shard)
    {
        Busy = true;
        collected.Add(shard.Id);          // counted right away, so it can never be picked up twice
        shard.DisableTrigger();

        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
        CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
        if (cam == null) Debug.LogWarning("[ShardManager] No CinemachineCamera in the scene, skipping the camera zoom.");

        if (player != null) player.controlLocked = true;
        if (health != null) health.cutsceneInvulnerable = true;

        Transform originalTarget = cam != null ? cam.Follow : null;
        float originalSize = cam != null ? cam.Lens.OrthographicSize : 0f;

        if (cam != null)
        {
            cam.Follow = shard.transform;
            yield return CameraFocus.Zoom(cam, originalSize, zoomSize, travelTime);
            yield return CameraFocus.WaitUntilSettled(cam, maxSettleWait);
        }

        yield return new WaitForSeconds(holdTime);

        if (cam != null) cam.Follow = originalTarget;    // release the shard before it is destroyed
        shard.Consume();

        if (cam != null)
        {
            yield return CameraFocus.Zoom(cam, zoomSize, originalSize, returnTime);
            yield return CameraFocus.WaitUntilSettled(cam, maxSettleWait);   // the player only gets control back once the camera is on them
        }

        if (player != null) player.controlLocked = false;
        if (health != null) health.cutsceneInvulnerable = false;
        Busy = false;
    }
}
