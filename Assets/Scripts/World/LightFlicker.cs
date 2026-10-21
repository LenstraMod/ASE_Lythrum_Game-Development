using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes a Light2D flicker like a flame. `strength` is how much the brightness wobbles (0.2 = +-20%).
// With `dropouts` on, the light now and then dips almost out for a moment, which is what makes a
// dark room feel unsafe.
[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    [Range(0f, 1f)] public float strength = 0.2f;
    public float speed = 6f;
    public bool dropouts = false;
    public Vector2 dropoutEvery = new Vector2(6f, 16f);
    public float dropoutLength = 0.25f;

    private Light2D lamp;
    private float baseIntensity;
    private float baseRadius;
    private float seed;
    private float nextDropout;
    private float dropoutUntil;

    void Awake()
    {
        lamp = GetComponent<Light2D>();
        baseIntensity = lamp.intensity;
        baseRadius = lamp.pointLightOuterRadius;
        seed = Random.value * 100f;
        nextDropout = Time.time + Random.Range(dropoutEvery.x, dropoutEvery.y);
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;   // -1..1
        float factor = 1f + n * strength;

        if (dropouts)
        {
            if (Time.time >= nextDropout)
            {
                dropoutUntil = Time.time + dropoutLength;
                nextDropout = dropoutUntil + Random.Range(dropoutEvery.x, dropoutEvery.y);
            }
            if (Time.time < dropoutUntil) factor *= 0.15f;
        }

        lamp.intensity = baseIntensity * factor;
        lamp.pointLightOuterRadius = baseRadius * (0.92f + 0.08f * factor);
    }
}
