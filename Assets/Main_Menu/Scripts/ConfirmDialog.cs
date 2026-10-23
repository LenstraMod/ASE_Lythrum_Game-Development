using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Dialog Yes/No sederhana. Dipakai Main Menu (timpa slot) dan Pause Menu (keluar tanpa save).
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action onConfirm;
    private GameObject previousSelection;

    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        yesButton.onClick.AddListener(Confirm);
        noButton.onClick.AddListener(Close);
    }

    public void Show(string text, Action confirm)
    {
        message.text = text;
        onConfirm = confirm;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        gameObject.SetActive(true);
        // Default ke "No" supaya tidak tidak sengaja menimpa/keluar.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    public void Close()
    {
        if (!IsOpen) return;
        onConfirm = null;
        gameObject.SetActive(false);
        if (EventSystem.current != null && previousSelection != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
    }

    void Confirm()
    {
        Action action = onConfirm;
        Close();
        action?.Invoke();
    }
}
