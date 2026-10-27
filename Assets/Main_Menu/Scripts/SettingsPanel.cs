using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Panel pengaturan (dipakai Main Menu dan Pause Menu): volume master/musik/efek, resolusi, fullscreen, VSync.
// Semua disimpan di PlayerPrefs.
public class SettingsPanel : MonoBehaviour
{
    private const string MasterKey = "Settings.MasterVolume";
    private const string MusicKey = "Settings.MusicVolume";
    private const string SfxKey = "Settings.SfxVolume";
    private const string FullscreenKey = "Settings.Fullscreen";
    private const string WidthKey = "Settings.ResolutionWidth";
    private const string HeightKey = "Settings.ResolutionHeight";
    private const string VSyncKey = "Settings.VSync";

    [SerializeField] private Slider masterSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text sfxValue;
    [SerializeField] private Button resolutionPrevious;
    [SerializeField] private Button resolutionNext;
    [SerializeField] private TMP_Text resolutionValue;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vSyncToggle;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int resolutionIndex;

    // Terapkan setting tersimpan saat game mulai, sebelum scene mana pun di-load.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicManager.MasterVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
        AudioManager.MasterVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
        if (PlayerPrefs.HasKey(VSyncKey))
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey);

        bool fullscreen = PlayerPrefs.HasKey(FullscreenKey) ? PlayerPrefs.GetInt(FullscreenKey) == 1 : Screen.fullScreen;
        if (PlayerPrefs.HasKey(WidthKey))
            Screen.SetResolution(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey), ToMode(fullscreen));
        else if (PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreenMode = ToMode(fullscreen);
    }

    void Awake()
    {
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        resolutionPrevious.onClick.AddListener(() => StepResolution(-1));
        resolutionNext.onClick.AddListener(() => StepResolution(1));
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        vSyncToggle.onValueChanged.AddListener(SetVSync);
    }

    public void Open()
    {
        masterSlider.SetValueWithoutNotify(AudioListener.volume);
        musicSlider.SetValueWithoutNotify(MusicManager.MasterVolume);
        sfxSlider.SetValueWithoutNotify(AudioManager.MasterVolume);
        UpdatePercent(masterValue, AudioListener.volume);
        UpdatePercent(musicValue, MusicManager.MasterVolume);
        UpdatePercent(sfxValue, AudioManager.MasterVolume);

        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        vSyncToggle.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
        LoadResolutions();

        gameObject.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(masterSlider.gameObject);
    }

    public void Close()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterKey, value);
        UpdatePercent(masterValue, value);
    }

    void SetMusicVolume(float value)
    {
        MusicManager.MasterVolume = value;
        PlayerPrefs.SetFloat(MusicKey, value);
        UpdatePercent(musicValue, value);
    }

    void SetSfxVolume(float value)
    {
        AudioManager.MasterVolume = value;
        PlayerPrefs.SetFloat(SfxKey, value);
        UpdatePercent(sfxValue, value);
    }

    // Daftar resolusi monitor tanpa duplikat (Screen.resolutions memuat tiap refresh rate terpisah).
    void LoadResolutions()
    {
        resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }

        var current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current))
        {
            resolutions.Add(current);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }
        resolutionIndex = resolutions.IndexOf(current);
        UpdateResolutionLabel();
    }

    void StepResolution(int direction)
    {
        if (resolutions.Count == 0) return;
        resolutionIndex = Mathf.Clamp(resolutionIndex + direction, 0, resolutions.Count - 1);
        Vector2Int size = resolutions[resolutionIndex];
        Screen.SetResolution(size.x, size.y, ToMode(fullscreenToggle.isOn));
        PlayerPrefs.SetInt(WidthKey, size.x);
        PlayerPrefs.SetInt(HeightKey, size.y);
        UpdateResolutionLabel();
    }

    void SetFullscreen(bool value)
    {
        Screen.fullScreenMode = ToMode(value);
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    void SetVSync(bool value)
    {
        QualitySettings.vSyncCount = value ? 1 : 0;
        PlayerPrefs.SetInt(VSyncKey, QualitySettings.vSyncCount);
    }

    void UpdateResolutionLabel()
    {
        Vector2Int size = resolutions[resolutionIndex];
        resolutionValue.text = size.x + " x " + size.y;
        resolutionPrevious.interactable = resolutionIndex > 0;
        resolutionNext.interactable = resolutionIndex < resolutions.Count - 1;
    }

    static void UpdatePercent(TMP_Text label, float value)
    {
        if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    static FullScreenMode ToMode(bool fullscreen)
    {
        return fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
