using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays sounds by name: AudioManager.Play(Sfx.PlayerSlash).
// Creates itself when the game starts (from Resources/SfxLibrary) and survives scene changes.
// If the library is missing the game still runs, just silently.
public class AudioManager : MonoBehaviour
{
    const string LibraryResourceName = "SfxLibrary";
    const int VoiceCount = 12;

    // 0-1, multiplies every sound. Set by the "Effects" slider (GameSettings).
    public static float MasterVolume = 1f;

    static AudioManager instance;

    readonly Dictionary<Sfx, SfxLibrary.Entry> lookup = new Dictionary<Sfx, SfxLibrary.Entry>();
    AudioSource[] voices;
    int nextVoice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        instance = null;

        SfxLibrary library = Resources.Load<SfxLibrary>(LibraryResourceName);
        if (library == null)
        {
            Debug.LogWarning("[AudioManager] No SfxLibrary found in a Resources folder. Run Lythrum > Setup > Audio to build it. Sound is off until then.");
            return;
        }

        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioManager>();
        instance.Init(library);
    }

    void Init(SfxLibrary library)
    {
        foreach (SfxLibrary.Entry entry in library.entries)
        {
            if (entry != null && entry.clips != null && entry.clips.Length > 0) lookup[entry.id] = entry;
        }

        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D: the game is top-down, no need for positional audio
            voices[i] = source;
        }
    }

    public static void Play(Sfx id, float volumeScale = 1f)
    {
        if (instance == null) return;
        instance.PlayInternal(id, volumeScale);
    }

    // Plays a sound for a set time: a long clip is cut off with a fade-out, a short one can repeat (loop).
    // Returns the AudioSource, so Stop() can end it early. Used for the boss's fire breath and fear grab.
    public static AudioSource PlayFor(Sfx id, float seconds, bool loop = false, float fadeOut = 0.4f)
    {
        return instance != null ? instance.PlayForInternal(id, seconds, loop, fadeOut) : null;
    }

    public static void Stop(AudioSource source, float fadeOut = 0.3f)
    {
        if (instance != null && source != null) instance.StartCoroutine(instance.FadeAndRemove(source, fadeOut));
    }

    AudioSource PlayForInternal(Sfx id, float seconds, bool loop, float fadeOut)
    {
        if (!lookup.TryGetValue(id, out SfxLibrary.Entry entry)) return null;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null) return null;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.pitch = Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        source.volume = entry.volume * MasterVolume;
        source.Play();

        StartCoroutine(StopAfter(source, seconds, fadeOut));
        return source;
    }

    IEnumerator StopAfter(AudioSource source, float seconds, float fadeOut)
    {
        for (float t = 0f; source != null && source.isPlaying && t < seconds; t += Time.unscaledDeltaTime) yield return null;
        yield return FadeAndRemove(source, fadeOut);
    }

    IEnumerator FadeAndRemove(AudioSource source, float fadeOut)
    {
        if (source == null) yield break;

        float start = source.volume;
        for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
        {
            if (source == null) yield break;
            source.volume = start * (1f - t / fadeOut);
            yield return null;
        }
        if (source != null) Destroy(source);
    }

    void PlayInternal(Sfx id, float volumeScale)
    {
        if (!lookup.TryGetValue(id, out SfxLibrary.Entry entry)) return;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null) return;

        AudioSource source = GetVoice();
        source.pitch = Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        source.PlayOneShot(clip, entry.volume * volumeScale * MasterVolume);
    }

    // A free voice if there is one, otherwise the oldest one gets cut off.
    AudioSource GetVoice()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].isPlaying) return voices[i];
        }
        AudioSource stolen = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        return stolen;
    }
}
