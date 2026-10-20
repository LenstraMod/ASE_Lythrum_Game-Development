using UnityEngine;

// Menggeser satu layer berdasarkan pointer mouse + drift sinus pelan. Layer yang lebih dekat diberi nilai lebih besar.
public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Vector2 mouseStrength = new Vector2(0.3f, 0.15f);
    [SerializeField] private Vector2 driftAmplitude = new Vector2(0.1f, 0.05f);
    [SerializeField] private float driftSpeed = 0.25f;
    [SerializeField] private float phase;

    private Vector3 startPosition;

    // Offset maksimum (unit lokal) yang bisa dicapai layer ini, dipakai BackgroundFitter untuk margin.
    public Vector2 MaxOffset => new Vector2(
        Mathf.Abs(mouseStrength.x) + Mathf.Abs(driftAmplitude.x),
        Mathf.Abs(mouseStrength.y) + Mathf.Abs(driftAmplitude.y));

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        Vector2 pointer = ParallaxController.Instance != null ? ParallaxController.Instance.Pointer : Vector2.zero;

        float t = Time.unscaledTime * driftSpeed + phase;
        Vector2 drift = new Vector2(
            Mathf.Sin(t) * driftAmplitude.x,
            Mathf.Sin(t * 0.73f + 1.3f) * driftAmplitude.y);

        // Layer bergerak berlawanan arah mouse agar terasa seperti kamera yang menoleh.
        Vector2 offset = -Vector2.Scale(pointer, mouseStrength) + drift;
        transform.localPosition = startPosition + (Vector3)offset;
    }
}
