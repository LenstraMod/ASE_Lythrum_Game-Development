using UnityEngine;
using UnityEngine.Rendering.Universal;

// A one-shot pixel-art effect: plays a sprite sheet's frames, optionally drifts and fades, then removes itself.
// Created by CombatVfx; has no setup of its own.
public class SpriteEffect : MonoBehaviour
{
    private Sprite[] frames;
    private float fps;
    private float delay;
    private Vector3 drift;
    private float fadeTime;
    private SpriteRenderer sr;
    private Color baseColor;
    private Light2D glow;
    private float glowIntensity;
    private float time;
    private float duration;

    public static SpriteEffect Spawn(Sprite[] frames, Vector3 position, float angleDegrees, float scale, Color color, float fps,
                                     int sortingLayerId, int sortingOrder, Material material,
                                     bool flipY = false, bool flipX = false, int loops = 1, float delay = 0f,
                                     Vector3 drift = default, float fadeTime = 0.12f, Transform follow = null)
    {
        if (frames == null || frames.Length == 0) return null;

        GameObject go = new GameObject("Pixel Effect");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        if (follow != null) go.transform.SetParent(follow, true);

        SpriteEffect fx = go.AddComponent<SpriteEffect>();
        fx.frames = frames;
        fx.fps = fps;
        fx.delay = delay;
        fx.drift = drift;
        fx.fadeTime = fadeTime;
        fx.duration = frames.Length * Mathf.Max(1, loops) / fps;
        fx.baseColor = color;

        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = frames[0];
        fx.sr.color = color;
        fx.sr.flipY = flipY;
        fx.sr.flipX = flipX;
        fx.sr.sortingLayerID = sortingLayerId;
        fx.sr.sortingOrder = sortingOrder;
        if (material != null) fx.sr.sharedMaterial = material;
        fx.sr.enabled = delay <= 0f;
        return fx;
    }

    // A short-lived glow for effects in dark scenes (the dungeon). Never added to bright scenes: there a single
    // Light2D would switch off the default lighting for everything else.
    public void AddGlow(Color color, float radius, float intensity)
    {
        GameObject go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
        go.transform.localScale = new Vector3(1f / parentScale, 1f / parentScale, 1f);   // keep the light at world size

        glow = go.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.pointLightOuterRadius = radius;
        glow.pointLightInnerRadius = 0f;
        glow.color = color;
        glow.intensity = intensity;
        glow.shadowsEnabled = false;
        glowIntensity = intensity;
    }

    void Update()
    {
        time += Time.deltaTime;
        if (time < delay) return;
        if (!sr.enabled) sr.enabled = true;

        float t = time - delay;
        if (t >= duration)
        {
            Destroy(gameObject);
            return;
        }

        sr.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * fps) % frames.Length)];

        float alpha = fadeTime > 0f ? Mathf.Clamp01((duration - t) / fadeTime) : 1f;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        transform.position += drift * Time.deltaTime;
        if (glow != null) glow.intensity = glowIntensity * alpha;
    }
}
