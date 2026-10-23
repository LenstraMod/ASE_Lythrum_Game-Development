using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Menu pause di scene gameplay (Esc): Resume, Save Game, Settings, Main Menu, Quit.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject menuBox;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private ConfirmDialog confirmDialog;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private GameObject firstSelected;
    [SerializeField] private GameObject settingsButton;
    [SerializeField] private string mainMenuScene = "MainMenu";

    private float previousTimeScale = 1f;

    bool SettingsOpen => settingsPanel != null && settingsPanel.gameObject.activeSelf;

    void Start()
    {
        panel.SetActive(false);
        confirmDialog.gameObject.SetActive(false);
        if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

        if (EventSystem.current == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (confirmDialog.IsOpen) confirmDialog.Close();
        else if (SettingsOpen) CloseSettings();
        else if (IsPaused) Resume();
        else Pause();
    }

    void OnDestroy()
    {
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = previousTimeScale;
        }
    }

    public void Pause()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
        statusText.text = "";
        panel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    public void Resume()
    {
        confirmDialog.Close();
        if (SettingsOpen) settingsPanel.Close();
        if (menuBox != null) menuBox.SetActive(true);
        panel.SetActive(false);
        Time.timeScale = previousTimeScale;
        IsPaused = false;
    }

    public void OpenSettings()
    {
        menuBox.SetActive(false);
        settingsPanel.Open();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(settingsPanel.FirstSelected);
    }

    public void CloseSettings()
    {
        settingsPanel.Close();
        menuBox.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(settingsButton);
    }

    public void SaveGame()
    {
        var session = GameSession.EnsureInstance();
        statusText.text = session.SaveGame()
            ? "Game saved to Slot " + (session.ActiveSlot + 1)
            : "Save failed";
    }

    public void ReturnToMainMenu()
    {
        confirmDialog.Show("Return to main menu?\nUnsaved progress will be lost.", () =>
        {
            IsPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        });
    }

    public void Quit()
    {
        confirmDialog.Show("Quit the game?\nUnsaved progress will be lost.", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }
}
