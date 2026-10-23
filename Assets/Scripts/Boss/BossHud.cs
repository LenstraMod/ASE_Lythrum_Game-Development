using UnityEngine;
using UnityEngine.UI;

// Boss health bars at the top of the screen: a wide bar for the boss and, in phase 1, a small bar for each hand.
// Created by SkeletonBoss when the fight starts and removed when it ends. Plain colored rectangles, no art needed.
public class BossHud : MonoBehaviour
{
    SkeletonBoss boss;
    RectTransform bossFill, leftFill, rightFill;
    GameObject leftBar, rightBar;

    public static BossHud Create(SkeletonBoss boss)
    {
        GameObject canvasGo = new GameObject("Boss HUD");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BossHud hud = canvasGo.AddComponent<BossHud>();
        hud.boss = boss;

        Color back = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        Color bossColor = new Color(0.8f, 0.15f, 0.15f);
        Color handColor = new Color(0.95f, 0.6f, 0.15f);

        hud.MakeBar("Boss Bar", new Vector2(0f, -28f), new Vector2(800f, 22f), back, bossColor, out hud.bossFill);
        hud.leftBar = hud.MakeBar("Left Hand Bar", new Vector2(-210f, -60f), new Vector2(380f, 12f), back, handColor, out hud.leftFill);
        hud.rightBar = hud.MakeBar("Right Hand Bar", new Vector2(210f, -60f), new Vector2(380f, 12f), back, handColor, out hud.rightFill);
        return hud;
    }

    void Update()
    {
        if (boss == null) return;

        bossFill.anchorMax = new Vector2(boss.BossRatio, 1f);
        leftFill.anchorMax = new Vector2(boss.LeftHandRatio, 1f);
        rightFill.anchorMax = new Vector2(boss.RightHandRatio, 1f);

        // Hand bars only matter in phase 1, while that hand is still alive.
        leftBar.SetActive(!boss.InPhase2 && boss.LeftHandRatio > 0f);
        rightBar.SetActive(!boss.InPhase2 && boss.RightHandRatio > 0f);
    }

    // A dark background rectangle with a colored fill rectangle inside it (the fill is resized by Update).
    GameObject MakeBar(string barName, Vector2 position, Vector2 size, Color backColor, Color fillColor, out RectTransform fill)
    {
        GameObject bar = new GameObject(barName);
        bar.transform.SetParent(transform, false);
        Image back = bar.AddComponent<Image>();
        back.color = backColor;
        back.raycastTarget = false;

        RectTransform rect = back.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);   // top center
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bar.transform, false);
        Image image = fillGo.AddComponent<Image>();
        image.color = fillColor;
        image.raycastTarget = false;

        fill = image.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        return bar;
    }
}
