using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Daftar slot save untuk New Game (pilih slot tujuan) dan Load Game (pilih save yang ada).
public class SaveSlotPanel : MonoBehaviour
{
    public enum Mode { NewGame, LoadGame }

    [SerializeField] private MainMenuController controller;
    [SerializeField] private TMP_Text header;
    [SerializeField] private Button[] slotButtons;

    private Mode mode;

    void Awake()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i;
            slotButtons[i].onClick.AddListener(() => controller.OnSlotChosen(slot, mode));
        }
    }

    public void Open(Mode openMode)
    {
        mode = openMode;
        header.text = mode == Mode.NewGame ? "New Game" : "Load Game";

        GameObject firstAvailable = null;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            SaveData data = SaveSystem.Load(i);
            slotButtons[i].GetComponentInChildren<TMP_Text>().text = Describe(i, data);
            // Load Game hanya bisa memilih slot yang berisi save.
            slotButtons[i].interactable = mode == Mode.NewGame || data != null;
            if (firstAvailable == null && slotButtons[i].interactable) firstAvailable = slotButtons[i].gameObject;
        }

        gameObject.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstAvailable);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    static string Describe(int slot, SaveData data)
    {
        string title = "Slot " + (slot + 1);
        if (data == null) return title + "   <alpha=#80>Empty";

        string savedAt = data.SavedAt.ToString("dd MMM yyyy  HH:mm", CultureInfo.InvariantCulture);
        return title + "   " + data.sceneName + "\n<size=70%><alpha=#B0>" + savedAt + "   |   " + FormatPlayTime(data.playTime);
    }

    static string FormatPlayTime(float seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
    }
}
