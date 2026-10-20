using UnityEngine;

// Menskalakan root background agar art menutupi kamera di aspect ratio apa pun,
// dengan sisa margin yang cukup untuk offset parallax terbesar.
[ExecuteAlways]
public class BackgroundFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 artSize = new Vector2(38.4f, 21.6f);

    private Vector2Int lastScreen;
    private float lastOrthoSize;

    void LateUpdate()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        var screen = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
        if (screen == lastScreen && Mathf.Approximately(cam.orthographicSize, lastOrthoSize)) return;
        lastScreen = screen;
        lastOrthoSize = cam.orthographicSize;
        Fit();
    }

    public void Fit()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic) return;

        Vector2 margin = Vector2.zero;
        foreach (var layer in GetComponentsInChildren<ParallaxLayer>())
            margin = Vector2.Max(margin, layer.MaxOffset);

        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;

        // art * s >= cam + 2 * margin * s  =>  s >= cam / (art - 2 * margin)
        float scaleX = camWidth / Mathf.Max(artSize.x - 2f * margin.x, 0.01f);
        float scaleY = camHeight / Mathf.Max(artSize.y - 2f * margin.y, 0.01f);
        float scale = Mathf.Max(scaleX, scaleY);

        transform.localScale = new Vector3(scale, scale, 1f);
        transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, transform.position.z);
    }
}
