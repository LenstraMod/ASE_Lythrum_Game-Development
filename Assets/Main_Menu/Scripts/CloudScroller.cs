using UnityEngine;

// Menggulirkan semua awan (child dengan SpriteRenderer) ke kiri tanpa henti, lalu memindahkannya kembali ke kanan.
public class CloudScroller : MonoBehaviour
{
    [SerializeField] private float speed = 0.35f;
    [SerializeField] private float speedVariation = 0.4f;
    [SerializeField] private float artWidth = 38.4f;

    private Transform[] clouds;
    private float[] speeds;
    private float[] halfWidths;

    void Start()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        clouds = new Transform[renderers.Length];
        speeds = new float[renderers.Length];
        halfWidths = new float[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            clouds[i] = renderers[i].transform;
            halfWidths[i] = renderers[i].sprite.bounds.extents.x * clouds[i].localScale.x;
            // Variasi kecepatan yang stabil per awan supaya tidak bergerak seragam.
            float t = renderers.Length > 1 ? i / (float)(renderers.Length - 1) : 0.5f;
            speeds[i] = speed * (1f + Mathf.Lerp(-speedVariation, speedVariation, Mathf.Repeat(t * 2.618f, 1f)));
        }
    }

    void Update()
    {
        float halfArt = artWidth * 0.5f;
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < clouds.Length; i++)
        {
            Vector3 pos = clouds[i].localPosition;
            pos.x -= speeds[i] * dt;

            if (pos.x + halfWidths[i] < -halfArt)
                pos.x = halfArt + halfWidths[i];

            clouds[i].localPosition = pos;
        }
    }
}
