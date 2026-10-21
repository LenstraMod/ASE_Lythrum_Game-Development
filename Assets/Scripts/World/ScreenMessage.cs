using UnityEngine;
using UnityEngine.UI;

// A short message in the upper middle of the screen that fades in and out, e.g.
// ScreenMessage.Show("You are not Ready", "Memories found: 2 / 4"). Builds its own canvas on first use. Plain text for now.
public class ScreenMessage : MonoBehaviour
{
    const float FadeIn = 0.15f;
    const float FadeOut = 0.5f;

    static ScreenMessage instance;

    CanvasGroup group;
    Text mainText, subText;
    float shownAt, duration;

    public static void Show(string text, string smallText = null, float seconds = 2.2f)
    {
        if (instance == null) instance = Create();
        instance.Display(text, smallText, seconds);
    }

    static ScreenMessage Create()
    {
        GameObject canvasGo = new GameObject("Screen Message");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        ScreenMessage message = canvasGo.AddComponent<ScreenMessage>();
        message.group = canvasGo.AddComponent<CanvasGroup>();
        message.group.alpha = 0f;
        message.group.blocksRaycasts = false;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        message.mainText = MakeText(canvasGo.transform, "Main", font, 64, FontStyle.Bold, new Vector2(0f, 220f));
        message.subText = MakeText(canvasGo.transform, "Small", font, 30, FontStyle.Normal, new Vector2(0f, 140f));
        return message;
    }

    static Text MakeText(Transform parent, string objectName, Font font, int size, FontStyle style, Vector2 position)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        Outline outline = go.AddComponent<Outline>();     // dark edge so it reads on any background
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, 100f);
        rect.anchoredPosition = position;
        return text;
    }

    void Display(string text, string smallText, float seconds)
    {
        mainText.text = text;
        subText.text = smallText ?? "";
        shownAt = Time.unscaledTime;
        duration = seconds;
    }

    void Update()
    {
        float age = Time.unscaledTime - shownAt;
        if (age < FadeIn) group.alpha = age / FadeIn;
        else if (age < duration) group.alpha = 1f;
        else group.alpha = Mathf.Clamp01(1f - (age - duration) / FadeOut);
    }
}
