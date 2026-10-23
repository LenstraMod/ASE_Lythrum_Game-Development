using System;
using System.Collections.Generic;
using UnityEngine;

// Pengaturan pemain (volume per kanal, resolusi, fullscreen), disimpan di PlayerPrefs.
// Dipakai bersama oleh panel Settings di menu utama dan di menu pause.
public static class GameSettings
{
    public enum Channel { Master, Music, Ambience, Effects }

    const string FullscreenKey = "Settings.Fullscreen";
    const string ResolutionWidthKey = "Settings.ResolutionWidth";
    const string ResolutionHeightKey = "Settings.ResolutionHeight";

    // "Settings.MasterVolume" sama dengan kunci versi lama, jadi volume yang sudah tersimpan tetap terpakai.
    static string VolumeKey(Channel channel) => "Settings." + channel + "Volume";

    // Terapkan setting tersimpan saat game mulai, sebelum scene mana pun di-load.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        foreach (Channel channel in Enum.GetValues(typeof(Channel)))
            ApplyVolume(channel, GetVolume(channel));

        bool fullscreen = PlayerPrefs.HasKey(FullscreenKey) ? PlayerPrefs.GetInt(FullscreenKey) == 1 : Screen.fullScreen;
        if (PlayerPrefs.HasKey(ResolutionWidthKey))
            Screen.SetResolution(PlayerPrefs.GetInt(ResolutionWidthKey), PlayerPrefs.GetInt(ResolutionHeightKey), ModeFor(fullscreen));
        else if (PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreen = fullscreen;
    }

    public static float GetVolume(Channel channel) => PlayerPrefs.GetFloat(VolumeKey(channel), 1f);

    public static void SetVolume(Channel channel, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey(channel), value);
        ApplyVolume(channel, value);
    }

    static void ApplyVolume(Channel channel, float value)
    {
        switch (channel)
        {
            case Channel.Master: AudioListener.volume = value; break;
            case Channel.Music: MusicManager.MasterVolume = value; break;
            case Channel.Ambience: MusicManager.AmbienceVolume = value; break;
            case Channel.Effects: AudioManager.MasterVolume = value; break;
        }
    }

    public static void SetFullscreen(bool value)
    {
        Screen.fullScreenMode = ModeFor(value);
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    public static Vector2Int CurrentResolution => new Vector2Int(Screen.width, Screen.height);

    // Resolusi yang didukung monitor tanpa duplikat refresh rate, dari kecil ke besar (ukuran sekarang selalu ikut).
    public static List<Vector2Int> Resolutions()
    {
        var sizes = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (!sizes.Contains(size)) sizes.Add(size);
        }
        if (!sizes.Contains(CurrentResolution)) sizes.Add(CurrentResolution);

        sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return sizes;
    }

    public static void SetResolution(Vector2Int size)
    {
        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
        PlayerPrefs.SetInt(ResolutionWidthKey, size.x);
        PlayerPrefs.SetInt(ResolutionHeightKey, size.y);
    }

    static FullScreenMode ModeFor(bool fullscreen) => fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
}
