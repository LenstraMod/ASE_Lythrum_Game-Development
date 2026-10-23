using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Pixel-art slash and fire effects for the combat. Reads the sheets from Resources/CombatVfxLibrary and returns
// false when they aren't there, so the caller can fall back to the old procedural arc.
public static class CombatVfx
{
    const string LibraryName = "CombatVfxLibrary";

    static CombatVfxLibrary library;
    static Material fallbackMaterial;
    static string darkCheckedScene;
    static bool darkScene;

    static CombatVfxLibrary Library
    {
        get
        {
            if (library == null) library = Resources.Load<CombatVfxLibrary>(LibraryName);
            return library;
        }
    }

    // True if the scene has a Global Light 2D, i.e. it is deliberately dark and effects should glow.
    static bool IsDarkScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (darkCheckedScene != scene.name)
        {
            darkCheckedScene = scene.name;
            darkScene = false;
            foreach (Light2D light in Object.FindObjectsByType<Light2D>())
            {
                if (light.lightType == Light2D.LightType.Global) { darkScene = true; break; }
            }
        }
        return darkScene;
    }

    // Unlit, so effects glow in dark scenes instead of being lit like the ground.
    static Material Unlit(Material wanted)
    {
        if (wanted != null) return wanted;
        if (fallbackMaterial == null) fallbackMaterial = new Material(Shader.Find("Sprites/Default")) { name = "PixelFxFallback" };
        return fallbackMaterial;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        library = null;
        fallbackMaterial = null;
        darkCheckedScene = null;
    }

    // The sword slash: the crescent sweeps across the attack arc. `mirrored` flips the sweep (every other combo hit).
    public static bool Slash(Vector2 origin, Vector2 dir, float range, bool mirrored, bool heavy, Color tint, int layerId, int order, Material material)
    {
        CombatVfxLibrary lib = Library;
        if (lib == null) return false;
        Sprite[] frames = lib.slash.Frames;
        if (frames.Length == 0) return false;
        material = Unlit(material);

        float scale = range / 1.9f;                                   // the crescent is ~2.2 units tall at scale 1
        Vector2 centre = origin + dir.normalized * range * 0.4f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        SpriteEffect.Spawn(frames, centre, angle, scale, tint, heavy ? 44f : 58f, layerId, order, material, flipY: mirrored, fadeTime: 0.06f);
        return true;
    }

    // Q: a big slash in fire colours, flames left burning along the arc, and a glow in dark scenes.
    public static bool AreaSlash(Vector2 origin, Vector2 dir, float range, float arcAngle, int layerId, int order, Material material)
    {
        CombatVfxLibrary lib = Library;
        if (lib == null) return false;
        Sprite[] slash = lib.slash.Frames;
        Sprite[] fire = lib.fire.Frames;
        if (slash.Length == 0) return false;
        material = Unlit(material);

        Vector2 d = dir.normalized;
        float baseAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        // two crescents, the second a beat later and mirrored, make a wide fiery sweep
        float scale = range / 1.7f;
        Color hot = new Color(1f, 0.62f, 0.2f, 1f);
        SpriteEffect.Spawn(slash, origin + d * range * 0.4f, baseAngle, scale, hot, 40f, layerId, order, material, fadeTime: 0.08f);
        SpriteEffect.Spawn(slash, origin + d * range * 0.4f, baseAngle, scale * 0.85f, new Color(1f, 0.9f, 0.5f, 0.9f), 40f, layerId, order + 1, material,
                           flipY: true, delay: 0.05f, fadeTime: 0.08f);

        if (fire.Length > 0)
        {
            // lots of small flames across the whole arc, popping up in a wave from one side to the other
            float half = arcAngle * 0.5f;
            int count = 26;
            for (int i = 0; i < count; i++)
            {
                float along = i / (count - 1f);
                float a = baseAngle + Mathf.Lerp(-half, half, along) + Random.Range(-8f, 8f);
                float radius = range * Random.Range(0.3f, 1.0f);
                Vector2 pos = origin + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * radius;
                SpriteEffect flame = SpriteEffect.Spawn(fire, pos, 0f, Random.Range(0.9f, 1.35f), Color.white, Random.Range(13f, 18f), layerId, order + 2, material,
                                                        flipX: Random.value < 0.5f, loops: Random.Range(1, 3), delay: 0.12f * along + Random.Range(0f, 0.05f),
                                                        drift: new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.25f, 0.5f), 0f), fadeTime: 0.22f);
                if (flame != null && i == count / 2 && IsDarkScene()) flame.AddGlow(new Color(1f, 0.55f, 0.2f), range * 2.2f, 1.3f);
            }

            // embers flying outwards
            Sprite ember = EmberSprite();
            for (int i = 0; i < 18; i++)
            {
                float a = (baseAngle + Random.Range(-half, half)) * Mathf.Deg2Rad;
                Vector2 outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float life = Random.Range(0.35f, 0.75f);
                Color c = Random.value < 0.5f ? new Color(1f, 0.75f, 0.15f) : new Color(1f, 0.3f, 0.05f);
                SpriteEffect.Spawn(new[] { ember }, origin + outward * range * Random.Range(0.4f, 0.9f), 0f, 1f, c, 1f / life, layerId, order + 3, material,
                                   delay: Random.Range(0f, 0.15f), drift: (Vector3)(outward * Random.Range(0.8f, 2.2f)) + new Vector3(0f, Random.Range(0.3f, 1.0f), 0f),
                                   fadeTime: life * 0.7f);
            }
        }
        return true;
    }

    static Sprite emberSprite;

    // A 2x2 pixel spark, two game pixels wide.
    static Sprite EmberSprite()
    {
        if (emberSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Ember" };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            emberSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f);
        }
        return emberSprite;
    }

    // A flame that sticks to something that was just set alight.
    public static void Burn(Transform target, int layerId, int order, Material material)
    {
        CombatVfxLibrary lib = Library;
        if (lib == null || target == null) return;
        Sprite[] fire = lib.fire.Frames;
        if (fire.Length == 0) return;
        material = Unlit(material);

        SpriteEffect flame = SpriteEffect.Spawn(fire, target.position + new Vector3(0f, 0.1f, 0f), 0f, 1.15f, Color.white, 12f, layerId, order + 2, material,
                                                flipX: Random.value < 0.5f, loops: 3, drift: new Vector3(0f, 0.2f, 0f), fadeTime: 0.3f, follow: target);
        if (flame != null && IsDarkScene()) flame.AddGlow(new Color(1f, 0.55f, 0.2f), 3f, 0.9f);
    }
}
