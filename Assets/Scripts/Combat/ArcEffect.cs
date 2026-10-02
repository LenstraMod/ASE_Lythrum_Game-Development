using UnityEngine;

// Placeholder VFX: a crescent "slash" or an expanding ring, drawn as a generated mesh.
// Replace with real sprite animations later — nothing else depends on this.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ArcEffect : MonoBehaviour
{
    [System.Serializable]
    public struct Settings
    {
        public float radius;          // start radius
        public float endRadius;       // radius at the end of the effect (same as radius = no expand)
        [Range(1f, 360f)] public float arcAngle;
        public float thickness;
        public bool taperEnds;        // thin at both ends like a sword trail
        public Color color;
        public float sweepTime;       // time to draw the arc from one end to the other (0 = instant)
        public float fadeTime;        // time to fade out after the sweep
        public bool reverseSweep;     // sweep clockwise instead of counter-clockwise
    }

    const int Segments = 32;

    private static Material defaultMaterial;

    private Settings settings;
    private Mesh mesh;
    private float facingAngle;
    private float elapsed;

    private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
    private readonly Color[] colors = new Color[(Segments + 1) * 2];
    private readonly int[] triangles = new int[Segments * 6];

    public static ArcEffect Spawn(Vector3 position, Vector2 direction, Settings settings,
                                  Material material, int sortingLayerId, int sortingOrder)
    {
        GameObject go = new GameObject("ArcEffect");
        go.transform.position = position;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material != null ? material : GetDefaultMaterial();
        mr.sortingLayerID = sortingLayerId;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        ArcEffect effect = go.AddComponent<ArcEffect>();
        effect.Init(direction, settings);
        return effect;
    }

    static Material GetDefaultMaterial()
    {
        if (defaultMaterial == null)
        {
            defaultMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        return defaultMaterial;
    }

    void Init(Vector2 direction, Settings s)
    {
        settings = s;
        facingAngle = direction.sqrMagnitude > 0f ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : -90f;

        mesh = new Mesh { name = "ArcEffect" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        for (int i = 0; i < Segments; i++)
        {
            int v = i * 2;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }

        Rebuild();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= settings.sweepTime + settings.fadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Rebuild();
    }

    void Rebuild()
    {
        float total = Mathf.Max(settings.sweepTime + settings.fadeTime, 0.0001f);
        float life = Mathf.Clamp01(elapsed / total);

        float reveal = settings.sweepTime > 0f ? Mathf.Clamp01(elapsed / settings.sweepTime) : 1f;
        float fade = settings.fadeTime > 0f ? 1f - Mathf.Clamp01((elapsed - settings.sweepTime) / settings.fadeTime) : 1f;

        // Ease-out expansion for rings.
        float grow = 1f - (1f - life) * (1f - life);
        float radius = Mathf.Lerp(settings.radius, settings.endRadius, grow);

        float halfArc = settings.arcAngle * 0.5f;

        for (int i = 0; i <= Segments; i++)
        {
            float t = (float)i / Segments;
            // Only the revealed part of the arc is visible; the rest collapses to the head.
            float drawT = Mathf.Min(t, reveal);
            float arcT = settings.reverseSweep ? 1f - drawT : drawT;

            float angle = (facingAngle - halfArc + settings.arcAngle * arcT) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            float thick = settings.thickness;
            if (settings.taperEnds) thick *= Mathf.Sin(arcT * Mathf.PI);

            vertices[i * 2] = dir * radius;
            vertices[i * 2 + 1] = dir * Mathf.Max(radius - thick, 0f);

            // Older part of the trail is dimmer than the leading edge.
            float trail = settings.sweepTime > 0f && reveal > 0f ? Mathf.Lerp(0.35f, 1f, drawT / reveal) : 1f;
            Color c = settings.color;
            c.a *= trail * fade;
            colors[i * 2] = c;
            colors[i * 2 + 1] = c;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
