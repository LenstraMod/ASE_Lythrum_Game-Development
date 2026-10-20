using System.Collections;
using UnityEngine;

// Fade CanvasGroup (opsional dengan slide kecil). Dipakai untuk fade-in judul/tombol dan overlay hitam.
[RequireComponent(typeof(CanvasGroup))]
public class UIFader : MonoBehaviour
{
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float startAlpha = 0f;
    [SerializeField] private float endAlpha = 1f;
    [SerializeField] private float delay;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Vector2 slideFrom = Vector2.zero;

    private CanvasGroup group;
    private RectTransform rect;
    private Vector2 restPosition;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = transform as RectTransform;
        if (rect != null) restPosition = rect.anchoredPosition;

        if (playOnStart)
        {
            group.alpha = startAlpha;
            if (rect != null) rect.anchoredPosition = restPosition + slideFrom;
        }
    }

    void Start()
    {
        if (playOnStart)
            StartCoroutine(Fade(startAlpha, endAlpha, delay, duration, slideFrom));
    }

    public Coroutine FadeTo(float alpha, float time)
    {
        StopAllCoroutines();
        return StartCoroutine(Fade(group.alpha, alpha, 0f, time, Vector2.zero));
    }

    IEnumerator Fade(float from, float to, float wait, float time, Vector2 slide)
    {
        if (wait > 0f) yield return new WaitForSecondsRealtime(wait);

        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            group.alpha = Mathf.Lerp(from, to, k);
            if (rect != null) rect.anchoredPosition = restPosition + slide * (1f - k);
            yield return null;
        }

        group.alpha = to;
        if (rect != null) rect.anchoredPosition = restPosition;
    }
}
