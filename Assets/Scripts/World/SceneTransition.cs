using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// A doorway between scenes: walk into the trigger and the screen fades out, the target scene loads and
// the player appears at the SceneEntryPoint called "entryName" in that scene.
// The map builder creates these from the "portal" lines in Assets/Maps/Data/*.txt.
// A gate can be locked until some memory shards are collected: it then pushes the player back and shows a message.
// The gate to the Dungeon is locked by default (it needs ShardManager.DungeonKeyShards), no scene setup needed.
[RequireComponent(typeof(Collider2D))]
public class SceneTransition : MonoBehaviour
{
    public string targetScene = "Dungeon";
    [Tooltip("Name of the SceneEntryPoint in the target scene where the player arrives.")]
    public string entryName = "";
    public float fadeOutTime = 0.4f;

    [Header("Locked gate (optional)")]
    [Tooltip("Memory shard ids that must ALL be collected before this gate opens. Leave empty to use the default: the Dungeon gate needs the 4 Town and Desert shards, every other gate is always open.")]
    public string[] requiredShardIds;
    [Tooltip("Tick to open this gate no matter what (for testing).")]
    public bool unlockForTesting;
    public string lockedMessage = "You are not Ready";
    [Tooltip("Adds a line under the message, e.g. \"Memories found: 2 / 4\".")]
    public bool showProgress = true;
    [Tooltip("How hard a locked gate pushes the player back (the run speed is 8, keep this above it).")]
    public float lockedPush = 10f;

    private static bool busy;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (busy || !other.CompareTag("Player")) return;

        if (!IsOpen(out int found, out int needed))
        {
            AudioManager.Play(Sfx.DoorClose);
            ScreenMessage.Show(lockedMessage, showProgress ? "Memories found: " + found + " / " + needed : null);
            PushBack(other);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError("[SceneTransition] Scene '" + targetScene + "' is not in the Build Settings. Rebuild the maps (Lythrum > Build Maps) or add it in File > Build Profiles.");
            return;
        }
        StartCoroutine(Go());
    }

    // While the gate is locked and the player stands in it, keep pushing them out (so running into it can't get through).
    void OnTriggerStay2D(Collider2D other)
    {
        if (busy || !other.CompareTag("Player") || IsOpen(out _, out _)) return;
        PushBack(other);
    }

    // The shards this gate needs: the list set in the Inspector, or for the Dungeon gate the default list.
    string[] RequiredShards()
    {
        if (requiredShardIds != null && requiredShardIds.Length > 0) return requiredShardIds;
        return targetScene == "Dungeon" ? ShardManager.DungeonKeyShards : null;
    }

    bool IsOpen(out int found, out int needed)
    {
        string[] required = RequiredShards();
        found = 0;
        needed = required != null ? required.Length : 0;
        if (unlockForTesting) return true;

        for (int i = 0; i < needed; i++)
        {
            if (ShardManager.IsCollected(required[i])) found++;
        }
        return found >= needed;
    }

    // Pushes the player back the way they came (opposite of the direction they were walking).
    void PushBack(Collider2D player)
    {
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null) movement.Knockback(-movement.FacingDirection * lockedPush);
    }

    IEnumerator Go()
    {
        busy = true;
        SceneEntry.Pending = entryName;
        yield return ScreenFader.Get().FadeTo(1f, fadeOutTime);
        SceneManager.LoadScene(targetScene);   // the fader fades back in once the new scene is up
        busy = false;
    }

    // Domain reload can be off in the editor; make sure a stale flag never blocks the first gate.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => busy = false;
}
