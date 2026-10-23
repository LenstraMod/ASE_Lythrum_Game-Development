using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string newGameScene = "SettingInput";
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private GameObject continueButton;
    [SerializeField] private GameObject newGameButton;
    [SerializeField] private GameObject loadGameButton;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private SaveSlotPanel slotPanel;
    [SerializeField] private ConfirmDialog confirmDialog;
    [SerializeField] private UIFader fadeOverlay;
    [SerializeField] private float fadeOutDuration = 0.6f;

    private bool loading;

    void Start()
    {
        Time.timeScale = 1f;
        settingsPanel.gameObject.SetActive(false);
        slotPanel.gameObject.SetActive(false);
        confirmDialog.gameObject.SetActive(false);

        // Continue & Load Game baru muncul setelah pemain punya save pertama.
        bool hasSave = SaveSystem.HasAnySave();
        continueButton.SetActive(hasSave);
        loadGameButton.SetActive(hasSave);
        SelectDefault();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (confirmDialog.IsOpen) confirmDialog.Close();
        else if (slotPanel.gameObject.activeSelf) CloseSlots();
        else if (settingsPanel.gameObject.activeSelf) CloseSettings();
    }

    public void Continue()
    {
        int slot = SaveSystem.LatestSlot();
        if (slot >= 0) StartTransition(() => GameSession.EnsureInstance().LoadGame(slot));
    }

    public void NewGame()
    {
        // Pemain pertama kali langsung masuk ke Slot 1 tanpa harus memilih slot.
        if (SaveSystem.HasAnySave()) OpenSlots(SaveSlotPanel.Mode.NewGame);
        else StartNewGame(0);
    }

    public void LoadGame()
    {
        OpenSlots(SaveSlotPanel.Mode.LoadGame);
    }

    public void OnSlotChosen(int slot, SaveSlotPanel.Mode mode)
    {
        if (mode == SaveSlotPanel.Mode.LoadGame)
            StartTransition(() => GameSession.EnsureInstance().LoadGame(slot));
        else if (SaveSystem.Exists(slot))
            confirmDialog.Show("Overwrite Slot " + (slot + 1) + "?\nThe old save will be deleted.", () => StartNewGame(slot));
        else
            StartNewGame(slot);
    }

    void StartNewGame(int slot)
    {
        StartTransition(() =>
        {
            GameSession.EnsureInstance().StartNewGame(slot, newGameScene);
            return true;
        });
    }

    // loadScene mengembalikan false kalau scene tidak jadi dibuka (mis. save rusak), menu lalu dibuka lagi.
    void StartTransition(Func<bool> loadScene)
    {
        if (loading) return;
        loading = true;
        StartCoroutine(FadeAndLoad(loadScene));
    }

    IEnumerator FadeAndLoad(Func<bool> loadScene)
    {
        menuGroup.interactable = false;
        CanvasGroup overlay = fadeOverlay.GetComponent<CanvasGroup>();
        overlay.blocksRaycasts = true;
        yield return fadeOverlay.FadeTo(1f, fadeOutDuration);
        if (loadScene()) yield break;

        yield return fadeOverlay.FadeTo(0f, fadeOutDuration);
        overlay.blocksRaycasts = false;
        loading = false;
        if (slotPanel.gameObject.activeSelf) CloseSlots();
        else SetMenuActive(true);
    }

    public void OpenSettings()
    {
        SetMenuActive(false);
        settingsPanel.Open();
    }

    public void CloseSettings()
    {
        settingsPanel.Close();
        SetMenuActive(true);
    }

    void OpenSlots(SaveSlotPanel.Mode mode)
    {
        SetMenuActive(false);
        slotPanel.Open(mode);
    }

    public void CloseSlots()
    {
        slotPanel.Close();
        SetMenuActive(true);
    }

    void SetMenuActive(bool active)
    {
        menuGroup.interactable = active;
        menuGroup.blocksRaycasts = active;
        menuGroup.alpha = active ? 1f : 0.35f;
        if (active) SelectDefault();
    }

    void SelectDefault()
    {
        if (EventSystem.current == null) return;
        GameObject target = continueButton.activeSelf ? continueButton : newGameButton;
        EventSystem.current.SetSelectedGameObject(target);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
