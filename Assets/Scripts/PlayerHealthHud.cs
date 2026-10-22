using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Prototype player health bar in the top-left corner: a red bar that shrinks as health drops, with a pale "damage trail"
// that catches up a moment later. Plain colored rectangles for now, swap in the real art later.
// Creates itself in any scene that has a PlayerHealth, so no scene setup is needed.
public class PlayerHealthHud : MonoBehaviour
{
    const float BarWidth = 340f;        // reference pixels at 1080p
    const float BarHeight = 28f;
    const float Margin = 24f;
    const float Border = 3f;
    const float FillSpeed = 6f;         // how fast the red bar follows the real value
    const float TrailDelay = 0.4f;      // seconds before the pale trail starts catching up
    const float TrailSpeed = 0.8f;      // trail catch-up speed (fraction of the bar per second)
    const float FlashTime = 0.15f;

    static readonly Color BorderColor = new Color(0.05f, 0.03f, 0.04f, 0.95f);
    static readonly Color BackColor = new Color(0.2f, 0.1f, 0.12f, 0.9f);
    static readonly Color TrailColor = new Color(0.95f, 0.85f, 0.6f, 0.9f);
    static readonly Color FillColor = new Color(0.85f, 0.15f, 0.2f);

    PlayerHealth player;
    Image fillImage;
    RectTransform fill, trail;
    float shownFill = 1f, shownTrail = 1f, lastTarget = 1f, trailWaitUntil, flashUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Spawn();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn();

    static void Spawn()
    {
        if (FindAnyObjectByType<PlayerHealthHud>() != null) return;

        PlayerHealth health = FindAnyObjectByType<PlayerHealth>();
        if (health == null) return;

        GameObject canvasGo = new GameObject("Player Health HUD");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        PlayerHealthHud hud = canvasGo.AddComponent<PlayerHealthHud>();
        hud.player = health;
        hud.Build();
    }

    // Border, dark back, pale trail, red fill: stacked rectangles, the trail and fill are resized by Update.
    void Build()
    {
        RectTransform border = MakeRect("Border", transform, BorderColor, out _);
        border.anchorMin = border.anchorMax = border.pivot = new Vector2(0f, 1f);   // top-left
        border.sizeDelta = new Vector2(BarWidth + Border * 2f, BarHeight + Border * 2f);
        border.anchoredPosition = new Vector2(Margin, -Margin);

        RectTransform back = MakeRect("Back", border, BackColor, out _);
        Stretch(back, Border);

        trail = MakeRect("Trail", back, TrailColor, out _);
        Stretch(trail, 0f);

        fill = MakeRect("Fill", back, FillColor, out fillImage);
        Stretch(fill, 0f);
    }

    void Update()
    {
        if (player == null) return;

        float target = player.MaxHealth > 0f ? Mathf.Clamp01(player.CurrentHealth / player.MaxHealth) : 0f;

        if (target < lastTarget - 0.0001f)
        {
            flashUntil = Time.unscaledTime + FlashTime;      // white flash when hurt
            trailWaitUntil = Time.unscaledTime + TrailDelay;
        }
        lastTarget = target;

        shownFill = Mathf.Lerp(shownFill, target, 1f - Mathf.Exp(-FillSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(shownFill - target) < 0.002f) shownFill = target;

        // The trail stays at the old value for a moment, then slides down to the bar. Healing moves it up with the bar.
        if (target > shownTrail) shownTrail = target;
        else if (Time.unscaledTime >= trailWaitUntil) shownTrail = Mathf.MoveTowards(shownTrail, target, TrailSpeed * Time.unscaledDeltaTime);

        fill.anchorMax = new Vector2(shownFill, 1f);
        trail.anchorMax = new Vector2(shownTrail, 1f);
        fillImage.color = Time.unscaledTime < flashUntil ? Color.white : FillColor;
    }

    static RectTransform MakeRect(string rectName, Transform parent, Color color, out Image image)
    {
        GameObject go = new GameObject(rectName);
        go.transform.SetParent(parent, false);
        image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
