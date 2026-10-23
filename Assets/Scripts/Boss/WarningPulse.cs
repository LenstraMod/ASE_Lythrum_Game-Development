using UnityEngine;

// Goes on the ground warning circles (slam and fear zone). The circle fades in and grows into place, then pulses faster
// and faster until the hit lands, so it is easy to read how soon it will go off.
public class WarningPulse : MonoBehaviour
{
    SpriteRenderer sr;
    Vector3 fullScale;
    float baseAlpha, duration, pulses, age;

    // duration = seconds until the hit, pulses = how many pulses it fits in (they get quicker towards the end).
    public static void Begin(GameObject circle, float duration, float pulses)
    {
        WarningPulse pulse = circle.AddComponent<WarningPulse>();
        pulse.sr = circle.GetComponent<SpriteRenderer>();
        pulse.fullScale = circle.transform.localScale;
        pulse.baseAlpha = pulse.sr.color.a;
        pulse.duration = Mathf.Max(0.01f, duration);
        pulse.pulses = pulses;
        pulse.Apply();
    }

    void Update()
    {
        if (TimeStopManager.IsTimeStopped) return;      // the telegraph freezes with the boss

        age += Time.deltaTime;
        Apply();
    }

    void Apply()
    {
        float t = Mathf.Clamp01(age / duration);
        transform.localScale = fullScale * Mathf.SmoothStep(0.6f, 1f, Mathf.Min(1f, t * 3f));   // grows in during the first third

        float fadeIn = Mathf.Clamp01(t * 4f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(t * t * pulses * Mathf.PI * 2f);                // t*t: the pulses speed up
        Color c = sr.color;
        c.a = baseAlpha * fadeIn * pulse * (1f + 0.4f * t);                                      // and it gets a bit stronger
        sr.color = c;
    }
}
