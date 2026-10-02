using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Time Stop: freezes every object that has a TimeStoppable component for "duration" seconds.
// The player keeps moving and attacking. The screen turns grey while time is stopped.
public class TimeStopAbility : MonoBehaviour
{
    [Header("Input (old Input Manager)")]
    public KeyCode key = KeyCode.R;

    [Header("Timing")]
    public float duration = 5f;
    [Tooltip("Counted from the moment time resumes.")]
    public float cooldown = 10f;

    [Header("Screen Effect")]
    public bool greyscaleScreen = true;
    [Range(-100f, 0f)] public float saturation = -100f;
    public float screenFadeTime = 0.25f;

    [Header("Placeholder VFX")]
    public bool showPlaceholderVfx = true;
    public Material effectMaterial;
    public Color ringColor = new Color(0.8f, 0.7f, 1f, 0.8f);

    [Header("Animator Parameter (used only if it exists in the controller)")]
    public string timeStopTrigger = "TimeStop";

    public bool IsActive { get; private set; }
    public float TimeRemaining { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, readyTime - Time.time);

    private Animator animator;
    private SpriteRenderer playerSprite;
    private bool hasTrigger;
    private float readyTime;

    private Volume volume;
    private VolumeProfile volumeProfile;

    void Awake()
    {
        animator = GetComponent<Animator>();
        playerSprite = GetComponent<SpriteRenderer>();
        hasTrigger = animator.HasParameter(timeStopTrigger);

        if (greyscaleScreen) CreateVolume();
    }

    void Start()
    {
        // The grey screen is a post-processing effect, so the camera must have post-processing on.
        if (greyscaleScreen && Camera.main != null)
        {
            Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }

    void OnDisable()
    {
        if (IsActive)
        {
            StopAllCoroutines();
            IsActive = false;
            TimeStopManager.SetTimeStopped(false);
            if (volume != null) volume.weight = 0f;
        }
    }

    void OnDestroy()
    {
        if (volume != null) Destroy(volume.gameObject);
        if (volumeProfile != null) Destroy(volumeProfile);
    }

    void Update()
    {
        if (Input.GetKeyDown(key) && !IsActive && Time.time >= readyTime)
        {
            StartCoroutine(TimeStopRoutine());
        }
    }

    IEnumerator TimeStopRoutine()
    {
        IsActive = true;
        if (hasTrigger) animator.SetTrigger(timeStopTrigger);

        TimeStopManager.SetTimeStopped(true);
        if (showPlaceholderVfx) SpawnRing(0.3f, 9f);

        TimeRemaining = duration;
        float fade = 0f;
        while (TimeRemaining > 0f)
        {
            TimeRemaining -= Time.deltaTime;
            fade += Time.deltaTime;
            if (volume != null) volume.weight = screenFadeTime > 0f ? Mathf.Clamp01(fade / screenFadeTime) : 1f;
            yield return null;
        }
        TimeRemaining = 0f;

        TimeStopManager.SetTimeStopped(false);
        if (showPlaceholderVfx) SpawnRing(9f, 0.3f);

        fade = 0f;
        while (volume != null && fade < screenFadeTime)
        {
            fade += Time.deltaTime;
            volume.weight = 1f - Mathf.Clamp01(fade / screenFadeTime);
            yield return null;
        }
        if (volume != null) volume.weight = 0f;

        IsActive = false;
        readyTime = Time.time + cooldown;
    }

    void CreateVolume()
    {
        GameObject go = new GameObject("TimeStop Volume");
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;

        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments colorAdjustments = volumeProfile.Add<ColorAdjustments>(true);
        colorAdjustments.saturation.Override(saturation);
        colorAdjustments.contrast.Override(15f);

        Vignette vignette = volumeProfile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.color.Override(new Color(0.15f, 0.05f, 0.25f));

        volume.sharedProfile = volumeProfile;
    }

    void SpawnRing(float fromRadius, float toRadius)
    {
        ArcEffect.Settings s = new ArcEffect.Settings
        {
            radius = fromRadius,
            endRadius = toRadius,
            arcAngle = 360f,
            thickness = 0.4f,
            taperEnds = false,
            color = ringColor,
            sweepTime = 0f,
            fadeTime = 0.45f,
        };

        int layer = playerSprite != null ? playerSprite.sortingLayerID : 0;
        int order = playerSprite != null ? playerSprite.sortingOrder + 1 : 100;
        ArcEffect.Spawn(transform.position, Vector2.right, s, effectMaterial, layer, order);
    }
}
