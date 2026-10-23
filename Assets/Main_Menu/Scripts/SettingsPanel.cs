using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel pengaturan: volume per kanal, resolusi dan fullscreen (lihat GameSettings).
// Dipakai di menu utama dan di menu pause.
public class SettingsPanel : MonoBehaviour
{
    [Serializable]
    class VolumeRow
    {
        public GameSettings.Channel channel;
        public Slider slider;
        public TMP_Text value;
    }

    [SerializeField] private VolumeRow[] volumes;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private List<Vector2Int> resolutions;

    // Kontrol pertama, untuk dipilih saat panel dibuka dengan keyboard/gamepad.
    public GameObject FirstSelected => volumes.Length > 0 ? volumes[0].slider.gameObject : null;

    void Awake()
    {
        foreach (VolumeRow row in volumes)
        {
            VolumeRow current = row;
            current.slider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetVolume(current.channel, value);
                UpdateLabel(current, value);
            });
        }
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
    }

    public void Open()
    {
        foreach (VolumeRow row in volumes)
        {
            float value = GameSettings.GetVolume(row.channel);
            row.slider.SetValueWithoutNotify(value);
            UpdateLabel(row, value);
        }
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        RefreshResolutions();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        resolutionDropdown.Hide();
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    void RefreshResolutions()
    {
        resolutions = GameSettings.Resolutions();
        Vector2Int current = GameSettings.CurrentResolution;

        var options = new List<string>();
        foreach (Vector2Int size in resolutions) options.Add(size.x + " x " + size.y);

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(resolutions.IndexOf(current));
        resolutionDropdown.RefreshShownValue();
    }

    void SetResolution(int index)
    {
        GameSettings.SetResolution(resolutions[index]);
    }

    static void UpdateLabel(VolumeRow row, float value)
    {
        if (row.value != null) row.value.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
