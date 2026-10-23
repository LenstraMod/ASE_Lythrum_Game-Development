using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The grey, dark, creepy look of the boss's Fear attack: a full-screen post-processing volume that fades in and out.
// Same idea as the Time Stop screen. Needs post-processing on the camera (switched on here).
public class CreepyScreen : MonoBehaviour
{
    Volume volume;
    VolumeProfile profile;
    Coroutine fade;

    public static CreepyScreen Create()
    {
        GameObject go = new GameObject("Creepy Screen");
        CreepyScreen screen = go.AddComponent<CreepyScreen>();

        screen.volume = go.AddComponent<Volume>();
        screen.volume.isGlobal = true;
        screen.volume.priority = 90f;
        screen.volume.weight = 0f;

        screen.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments color = screen.profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-100f);       // fully grey
        color.contrast.Override(25f);
        color.postExposure.Override(-0.7f);     // and darker

        Vignette vignette = screen.profile.Add<Vignette>(true);
        vignette.intensity.Override(0.55f);
        vignette.color.Override(new Color(0.08f, 0f, 0.12f));

        screen.volume.sharedProfile = screen.profile;

        if (Camera.main != null) Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        return screen;
    }

    // weight 1 = fully creepy, 0 = normal.
    public void FadeTo(float weight, float time)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(weight, time));
    }

    public void SetInstant(float weight)
    {
        if (fade != null) StopCoroutine(fade);
        volume.weight = weight;
    }

    IEnumerator FadeRoutine(float weight, float time)
    {
        float start = volume.weight;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            volume.weight = Mathf.Lerp(start, weight, t / time);
            yield return null;
        }
        volume.weight = weight;
    }

    void OnDestroy()
    {
        if (profile != null) Destroy(profile);
    }
}
