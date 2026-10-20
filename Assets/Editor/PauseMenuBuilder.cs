using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

// Membuat prefab menu pause di Resources/UI, yang dimunculkan otomatis oleh GameSession di scene gameplay.
public static class PauseMenuBuilder
{
    const string PrefabFolder = "Assets/Resources/UI";
    const string PrefabPath = PrefabFolder + "/PauseMenu.prefab";

    [MenuItem("Lythrum/Build Pause Menu")]
    public static void Build()
    {
        Directory.CreateDirectory(PrefabFolder);
        TMP_FontAsset font = MainMenuSceneBuilder.GetOrCreateFontAsset();

        var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        // Layar diredupkan selama pause.
        var panel = MainMenuSceneBuilder.CreateRect(root.transform, "Panel");
        MainMenuSceneBuilder.Place(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform box = MainMenuSceneBuilder.CreatePanel(panel, "Box", new Vector2(560f, 560f));
        var top = new Vector2(0.5f, 1f);

        var header = MainMenuSceneBuilder.CreateText(box, "Header", "Paused", font, null, 64f, TextAlignmentOptions.Center);
        MainMenuSceneBuilder.Place(header.rectTransform, top, top, new Vector2(0f, -35f), new Vector2(480f, 90f));

        string[] labels = { "Resume", "Save Game", "Main Menu", "Quit" };
        var buttons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = MainMenuSceneBuilder.CreateButton(box, labels[i].Replace(" ", "") + "Button", labels[i], font, null, 0);
            MainMenuSceneBuilder.Place((RectTransform)buttons[i].transform, top, top, new Vector2(0f, -150f - i * 76f), new Vector2(400f, 64f));
            buttons[i].GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        }

        var status = MainMenuSceneBuilder.CreateText(box, "Status", "", font, null, 30f, TextAlignmentOptions.Center);
        MainMenuSceneBuilder.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(480f, 50f));
        status.color = new Color(1f, 0.8f, 0.5f);

        ConfirmDialog confirm = MainMenuSceneBuilder.BuildConfirmDialog(root.transform, font);

        var pause = root.AddComponent<PauseMenu>();
        MainMenuSceneBuilder.SetField(pause, "panel", panel.gameObject);
        MainMenuSceneBuilder.SetField(pause, "statusText", status);
        MainMenuSceneBuilder.SetField(pause, "confirmDialog", confirm);
        MainMenuSceneBuilder.SetField(pause, "firstSelected", buttons[0].gameObject);

        UnityEventTools.AddPersistentListener(buttons[0].onClick, pause.Resume);
        UnityEventTools.AddPersistentListener(buttons[1].onClick, pause.SaveGame);
        UnityEventTools.AddPersistentListener(buttons[2].onClick, pause.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(buttons[3].onClick, pause.Quit);

        panel.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[PauseMenu] Prefab dibuat: " + PrefabPath);
    }
}
