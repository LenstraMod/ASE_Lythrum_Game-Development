using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel pengaturan: master volume + fullscreen, disimpan di PlayerPrefs.
public class SettingsPanel : MonoBehaviour
{
    private const string VolumeKey = "Settings.MasterVolume";
    private const string FullscreenKey = "Settings.Fullscreen";

    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValue;
    [SerializeField] private Toggle fullscreenToggle;

    // Terapkan setting tersimpan saat game mulai, sebelum scene mana pun di-load.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        if (PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) == 1;
    }

    void Awake()
    {
        volumeSlider.onValueChanged.AddListener(SetVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    public void Open()
    {
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        UpdateVolumeLabel(AudioListener.volume);
        gameObject.SetActive(true);
    }

    public void Close()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        UpdateVolumeLabel(value);
    }

    void SetFullscreen(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    void UpdateVolumeLabel(float value)
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
