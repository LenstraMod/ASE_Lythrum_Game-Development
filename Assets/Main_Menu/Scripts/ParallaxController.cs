using UnityEngine;

// Membaca posisi mouse dan menyediakan nilai pointer yang dihaluskan (-1..1) untuk semua ParallaxLayer.
public class ParallaxController : MonoBehaviour
{
    public static ParallaxController Instance { get; private set; }

    [SerializeField] private float smoothTime = 0.35f;

    public Vector2 Pointer { get; private set; }

    private Vector2 velocity;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Vector2 target = Vector2.zero;
        Vector3 mouse = Input.mousePosition;
        bool insideWindow = mouse.x >= 0 && mouse.y >= 0 && mouse.x <= Screen.width && mouse.y <= Screen.height;

        if (Application.isFocused && insideWindow)
        {
            target.x = mouse.x / Screen.width * 2f - 1f;
            target.y = mouse.y / Screen.height * 2f - 1f;
        }

        Pointer = Vector2.SmoothDamp(Pointer, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }
}
