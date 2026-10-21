using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

// Keeps the camera inside the map so it never shows the empty space past the edge.
// Goes on the Cinemachine camera (CmCam). It measures every Tilemap in the scene, builds an
// invisible rectangle from that and hands it to a CinemachineConfiner2D.
[RequireComponent(typeof(CinemachineCamera))]
public class CameraMapBounds : MonoBehaviour
{
    [Tooltip("Grow (+) or shrink (-) the map rectangle, in world units. The map builder surrounds every map with an invisible wall ring one tile thick, so -1 keeps the camera off it.")]
    public float padding = -1f;

    [Tooltip("Leave empty to use every Tilemap in the scene.")]
    public Tilemap[] tilemaps;

    private PolygonCollider2D shape;

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (!TryMeasureMap(out Bounds bounds))
        {
            Debug.LogWarning("[CameraMapBounds] No tiles found, camera is not limited.", this);
            return;
        }

        bounds.Expand(padding * 2f);
        if (bounds.size.x <= 0f || bounds.size.y <= 0f) return;

        if (shape == null)
        {
            GameObject go = new GameObject("CameraBounds");
            go.layer = 2; // Ignore Raycast, so combat and line-of-sight checks never see it
            shape = go.AddComponent<PolygonCollider2D>();
            shape.isTrigger = true;
        }

        shape.transform.position = bounds.center;
        Vector2 e = bounds.extents;
        shape.SetPath(0, new[] { new Vector2(-e.x, -e.y), new Vector2(-e.x, e.y), new Vector2(e.x, e.y), new Vector2(e.x, -e.y) });

        CinemachineConfiner2D confiner = GetComponent<CinemachineConfiner2D>();
        if (confiner == null) confiner = gameObject.AddComponent<CinemachineConfiner2D>();
        confiner.BoundingShape2D = shape;
        confiner.InvalidateBoundingShapeCache();
    }

    bool TryMeasureMap(out Bounds result)
    {
        Tilemap[] maps = tilemaps != null && tilemaps.Length > 0
            ? tilemaps
            : FindObjectsByType<Tilemap>();

        bool found = false;
        result = new Bounds();

        foreach (Tilemap map in maps)
        {
            if (map == null) continue;
            map.CompressBounds();
            BoundsInt cells = map.cellBounds;
            if (cells.size.x <= 0 || cells.size.y <= 0) continue;

            Vector3 min = map.CellToWorld(cells.min);
            Vector3 max = map.CellToWorld(cells.max);
            Bounds b = new Bounds((min + max) * 0.5f, Vector3.zero);
            b.Encapsulate(min);
            b.Encapsulate(max);

            if (!found) result = b;
            else result.Encapsulate(b);
            found = true;
        }
        return found;
    }

    void OnDestroy()
    {
        if (shape != null) Destroy(shape.gameObject);
    }
}
