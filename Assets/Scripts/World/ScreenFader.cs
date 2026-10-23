using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Full-screen black fade used when changing scenes. Creates itself when first needed and survives scene loads;
// every time a scene finishes loading it fades back in.
public class ScreenFader : MonoBehaviour
{
    public float fadeInTime = 0.6f;

    private static ScreenFader instance;
    private CanvasGroup group;
    private Coroutine running;

    public static ScreenFader Get()
    {
        if (instance != null) return instance;

        GameObject go = new GameObject("Screen Fader");
        DontDestroyOnLoad(go);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        GameObject imageGo = new GameObject("Black");
        imageGo.transform.SetParent(go.transform, false);
        Image image = imageGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        instance = go.AddComponent<ScreenFader>();
        instance.group = go.AddComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;
        instance.group.interactable = false;

        SceneManager.sceneLoaded += instance.OnSceneLoaded;
        return instance;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (this == null || group == null) return;   // a fader from an earlier Play session must never throw here
        if (group.alpha > 0f) StartFade(0f, fadeInTime);
    }

    void StartFade(float target, float time)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Fade(target, time));
    }

    // Waits until the fade has finished.
    public IEnumerator FadeTo(float target, float time)
    {
        StartFade(target, time);
        while (running != null) yield return null;
    }

    IEnumerator Fade(float target, float time)
    {
        float start = group.alpha;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        group.alpha = target;
        running = null;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // otherwise the event keeps calling a destroyed object
        if (instance == this) instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;
}
