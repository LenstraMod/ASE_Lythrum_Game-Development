using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the background music of the current scene and crossfades when you change scenes.
// Creates itself when the game starts (from Resources/MusicLibrary) and survives scene loads.
// A layer that is in both the old and the new scene just keeps playing. During Time Stop the music ducks.
// PlayOverride("BossFight") swaps to another entry of the library (the boss song) until ClearOverride() brings the scene music back.
public class MusicManager : MonoBehaviour
{
    const string LibraryResourceName = "MusicLibrary";

    // 0-1, multiplies all music. Hook an options slider to this later.
    public static float MasterVolume = 1f;

    [Range(0f, 1f)] public float timeStopVolume = 0.35f;

    class Voice
    {
        public AudioSource source;
        public AudioClip clip;
        public float target;      // volume this layer is fading towards (0 = fading out)
        public float level;       // current layer volume before master / ducking
        public float fadeSeconds;
    }

    static MusicManager instance;

    private MusicLibrary library;
    private readonly List<Voice> voices = new List<Voice>();
    private float duck = 1f;
    private string overrideName;      // library entry that replaces the scene music (null = normal scene music)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        instance = null;

        MusicLibrary library = Resources.Load<MusicLibrary>(LibraryResourceName);
        if (library == null)
        {
            Debug.LogWarning("[MusicManager] No MusicLibrary found in a Resources folder. Run Lythrum > Setup > Audio to build it. Music is off until then.");
            return;
        }

        GameObject go = new GameObject("MusicManager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MusicManager>();
        instance.library = library;
        instance.PlayFor(SceneManager.GetActiveScene().name);
    }

    // Subscribed here (not in Bootstrap) so a stale handler can never outlive this object between Play sessions.
    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        overrideName = null;       // a new scene always starts with its own music
        PlayFor(scene.name);
    }

    // Crossfades to the library entry with this name (e.g. "BossFight"). Does nothing if the entry does not exist.
    public static void PlayOverride(string entryName)
    {
        if (instance == null || !instance.HasEntry(entryName)) return;
        instance.overrideName = entryName;
        instance.PlayFor(entryName);
    }

    // Back to the music of the current scene.
    public static void ClearOverride()
    {
        if (instance == null || instance.overrideName == null) return;
        instance.overrideName = null;
        instance.PlayFor(SceneManager.GetActiveScene().name);
    }

    bool HasEntry(string entryName)
    {
        foreach (MusicLibrary.SceneMusic candidate in library.scenes)
        {
            if (candidate.sceneName == entryName) return true;
        }
        return false;
    }

    void PlayFor(string sceneName)
    {
        MusicLibrary.SceneMusic music = null;
        foreach (MusicLibrary.SceneMusic candidate in library.scenes)
        {
            if (candidate.sceneName == sceneName) { music = candidate; break; }
        }

        float fade = music != null ? Mathf.Max(0.1f, music.fadeSeconds) : 1.5f;
        foreach (Voice voice in voices) voice.fadeSeconds = fade;

        // Keep layers that continue into the new scene, fade out the rest.
        HashSet<Voice> kept = new HashSet<Voice>();
        if (music != null && music.layers != null)
        {
            foreach (MusicLibrary.Layer layer in music.layers)
            {
                if (layer.clip == null) continue;

                Voice voice = voices.Find(v => v.clip == layer.clip && !kept.Contains(v));
                if (voice == null)
                {
                    voice = NewVoice(layer.clip, fade);
                    voices.Add(voice);
                }
                voice.target = layer.volume;
                kept.Add(voice);
            }
        }
        foreach (Voice voice in voices)
        {
            if (!kept.Contains(voice)) voice.target = 0f;
        }
    }

    Voice NewVoice(AudioClip clip, float fade)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();
        return new Voice { source = source, clip = clip, level = 0f, fadeSeconds = fade };
    }

    void Update()
    {
        // Real time: the game freezes for a split second on every hit (hit-stop), the music must not.
        float dt = Time.unscaledDeltaTime;

        float wanted = TimeStopManager.IsTimeStopped ? timeStopVolume : 1f;
        duck = Mathf.MoveTowards(duck, wanted, dt * 2f);

        for (int i = voices.Count - 1; i >= 0; i--)
        {
            Voice voice = voices[i];
            voice.level = Mathf.MoveTowards(voice.level, voice.target, dt / voice.fadeSeconds);
            voice.source.volume = voice.level * MasterVolume * duck;

            if (voice.target <= 0f && voice.level <= 0f)
            {
                Destroy(voice.source);
                voices.RemoveAt(i);
            }
        }
    }
}
