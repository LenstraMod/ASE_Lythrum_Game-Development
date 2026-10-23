using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Moves the player to the requested entry point right after a scene loads (before any Start() runs, so
// PlayerHealth's respawn point is the entry too) and tells Cinemachine about the jump so the camera doesn't pan across.
public static class SceneEntry
{
    public static string Pending;

    // With "Reload Domain" off in the editor, statics survive Play sessions: start every session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Pending = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    static void Apply()
    {
        string wanted = Pending;
        Pending = null;
        if (string.IsNullOrEmpty(wanted)) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        foreach (SceneEntryPoint point in Object.FindObjectsByType<SceneEntryPoint>())
        {
            if (point.entryName != wanted) continue;

            Vector3 delta = point.transform.position - player.transform.position;
            delta.z = 0f;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            player.transform.position += delta;
            if (rb != null) rb.position = player.transform.position;
            Physics2D.SyncTransforms();

            foreach (CinemachineCamera cam in Object.FindObjectsByType<CinemachineCamera>())
            {
                cam.OnTargetObjectWarped(player.transform, delta);
            }
            return;
        }
        Debug.LogWarning("[SceneEntry] No entry point named '" + wanted + "' in " + SceneManager.GetActiveScene().name + ", the player stays at the default spawn.");
    }
}
