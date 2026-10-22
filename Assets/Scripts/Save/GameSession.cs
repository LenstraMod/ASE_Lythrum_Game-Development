using System;
using System.Collections;
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

        var data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
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
        if (player != null) player.RestoreState(data.health, data.checkpoint);
    }

    void SpawnPauseMenu()
    {
        if (FindAnyObjectByType<PauseMenu>() != null) return;

        var prefab = Resources.Load<GameObject>(PauseMenuResource);
        if (prefab != null) Instantiate(prefab).name = prefab.name;
        else Debug.LogWarning("Prefab Resources/" + PauseMenuResource + " tidak ditemukan. Jalankan Lythrum/Build Pause Menu.");
    }
}
