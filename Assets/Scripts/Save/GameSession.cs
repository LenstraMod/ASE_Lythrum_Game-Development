using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sesi permainan yang hidup lintas scene: slot aktif, waktu bermain, load/save, dan memunculkan menu pause di scene gameplay.
public class GameSession : MonoBehaviour
{
    const string PauseMenuResource = "UI/PauseMenu";

    public static GameSession Instance { get; private set; }

    public int ActiveSlot { get; private set; }
    public float PlayTime { get; private set; }

    private SaveData pendingLoad;
    private bool inGameplay;

    // Dibuat otomatis, jadi save & pause tetap jalan walau scene gameplay dibuka langsung dari editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance().OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    public static GameSession EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameSession");
            Instance = go.AddComponent<GameSession>();
            DontDestroyOnLoad(go);
        }
        return Instance;
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (inGameplay && !PauseMenu.IsPaused)
            PlayTime += Time.unscaledDeltaTime;
    }

    public void StartNewGame(int slot, string sceneName)
    {
        ActiveSlot = slot;
        PlayTime = 0f;
        pendingLoad = null;
        SaveSystem.Delete(slot);
        SceneManager.LoadSceneAsync(sceneName);
    }

    public bool LoadGame(int slot)
    {
        SaveData data = SaveSystem.Load(slot);
        if (data == null) return false;

        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogError("Save slot " + (slot + 1) + " menunjuk ke scene '" + data.sceneName + "' yang tidak ada di Build Settings.");
            return false;
        }

        ActiveSlot = slot;
        PlayTime = data.playTime;
        pendingLoad = data;
        SceneManager.LoadSceneAsync(data.sceneName);
        return true;
    }

    public bool SaveGame()
    {
        var player = FindAnyObjectByType<PlayerHealth>();
        if (player == null) return false;

        // Pakai scene tempat player berada, bukan active scene, dan tolak save yang nanti tidak bisa di-load.
        string sceneName = player.gameObject.scene.name;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Tidak bisa save: scene '" + sceneName + "' belum disimpan atau tidak ada di Build Settings.");
            return false;
        }

        var data = new SaveData
        {
            sceneName = sceneName,
            checkpoint = player.RespawnPoint,
            health = player.CurrentHealth,
            playTime = PlayTime,
            savedAtTicks = DateTime.Now.Ticks,
        };

        bool saved = SaveSystem.Write(ActiveSlot, data);
        if (saved) Debug.Log("Game saved to slot " + (ActiveSlot + 1));
        return saved;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        inGameplay = FindAnyObjectByType<PlayerHealth>() != null;
        if (!inGameplay) return;

        SpawnPauseMenu();

        if (pendingLoad != null && pendingLoad.sceneName == scene.name)
            StartCoroutine(ApplyLoad(pendingLoad));
        pendingLoad = null;
    }

    // Tunggu satu frame supaya PlayerHealth.Start (yang me-reset HP & respawn) sudah jalan lebih dulu.
    IEnumerator ApplyLoad(SaveData data)
    {
        yield return null;
        var player = FindAnyObjectByType<PlayerHealth>();
        if (player == null) yield break;

        Vector3 delta = data.checkpoint - player.transform.position;
        player.RestoreState(data.health, data.checkpoint);

        // Sama seperti SceneEntry: ikutkan Rigidbody2D dan beri tahu Cinemachine supaya kamera tidak menggeser dari spawn awal.
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = data.checkpoint;
            rb.linearVelocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();

        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>())
            cam.OnTargetObjectWarped(player.transform, delta);
    }

    void SpawnPauseMenu()
    {
        if (FindAnyObjectByType<PauseMenu>() != null) return;

        var prefab = Resources.Load<GameObject>(PauseMenuResource);
        if (prefab != null) Instantiate(prefab).name = prefab.name;
        else Debug.LogWarning("Prefab Resources/" + PauseMenuResource + " tidak ditemukan. Jalankan Lythrum/Build Pause Menu.");
    }
}
