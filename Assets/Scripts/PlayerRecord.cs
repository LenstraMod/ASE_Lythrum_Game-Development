using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// E: record the next few seconds of your movement and attacks. When the recording ends a shadow clone appears
// where you started and replays everything (it can hold a pressure plate, or fight). Only one shadow exists at a time.
public class PlayerRecord : MonoBehaviour
{
    public GameObject shadowPrefab;
    public float recordDuration = 10f;
    public Animator playerAnimator;
    public KeyCode recordKey = KeyCode.E;

    [Header("Look")]
    [Tooltip("The shadow is drawn in this colour so it reads as a ghost of you.")]
    public Color shadowColor = new Color(0.55f, 0.45f, 0.95f, 0.75f);
    public Color recordingTint = new Color(0.55f, 0.8f, 1f, 1f);

    private List<PlayerSnapshot> recordedData = new List<PlayerSnapshot>();
    private bool isRecording = false;
    private float recordTimer = 0f;

    private SpriteRenderer playerSprite;
    private Color playerBaseColor = Color.white;
    private GameObject currentShadow;

    // little on-screen timer
    private GameObject hudRoot;
    private Image hudFill;
    private static Sprite whiteSprite;

    void Awake()
    {
        playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite != null) playerBaseColor = playerSprite.color;
        if (playerAnimator == null) playerAnimator = GetComponent<Animator>();
    }

    void OnDisable()
    {
        StopFeedback();
    }

    void Update()
    {
        if (Input.GetKeyDown(recordKey) && !isRecording)
        {
            StartRecording();
        }

        if (isRecording)
        {
            recordTimer += Time.deltaTime;

            // Capture the current frame's data
            PlayerSnapshot currentFrame = new PlayerSnapshot
            {
                // Primary Key
                timestamp = recordTimer,

                // Position
                position = transform.position,

                // Button Pressed
                isAttacking = Input.GetButtonDown("Fire1"), // Replace with your attack input
                isInteracting = Input.GetKeyDown(KeyCode.F), // Replace with your interact input

                // Animation parameters
                lastInputX = playerAnimator.GetFloat("LastInputX"),
                lastInputY = playerAnimator.GetFloat("LastInputY"),
                inputX = playerAnimator.GetFloat("InputX"),
                inputY = playerAnimator.GetFloat("InputY"),
                isWalking = playerAnimator.GetBool("isWalking")
            };

            recordedData.Add(currentFrame);
            UpdateFeedback();

            if (recordTimer >= recordDuration)
            {
                StopRecordingAndSpawnShadow();
            }
        }
    }

    void StartRecording()
    {
        isRecording = true;
        recordTimer = 0f;
        recordedData.Clear();
        AudioManager.Play(Sfx.RecordStart);
        BuildHud();
        Debug.Log("Recording started!");
    }

    void StopRecordingAndSpawnShadow()
    {
        isRecording = false;
        StopFeedback();
        Debug.Log("Recording stopped. Spawning shadow...");
        AudioManager.Play(Sfx.ShadowSpawn);

        if (shadowPrefab == null || recordedData.Count == 0)
        {
            Debug.LogWarning("[PlayerRecord] No shadow prefab assigned, nothing to spawn.");
            return;
        }

        // one shadow at a time
        if (currentShadow != null) Destroy(currentShadow);

        currentShadow = Instantiate(shadowPrefab, recordedData[0].position, Quaternion.identity);
        PrepareShadow(currentShadow);
        currentShadow.GetComponent<ShadowPlayback>().Initialize(new List<PlayerSnapshot>(recordedData));
    }

    // The prefab was made for a test scene: on the "Default" sorting layer it is drawn underneath the ground tiles of the
    // maps (so you never saw it), and a solid dynamic body would shove enemies and the player around.
    void PrepareShadow(GameObject shadow)
    {
        SpriteRenderer sr = shadow.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (playerSprite != null)
            {
                sr.sortingLayerID = playerSprite.sortingLayerID;
                sr.sortingOrder = playerSprite.sortingOrder - 1;
                sr.sharedMaterial = playerSprite.sharedMaterial;
            }
            sr.color = shadowColor;
        }

        Rigidbody2D rb = shadow.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;   // moved by the replay, still triggers pressure plates
            rb.gravityScale = 0f;
        }
        //foreach (Collider2D col in shadow.GetComponentsInChildren<Collider2D>()) col.isTrigger = true;

        // same feet-only collider the player has, so it presses plates the way the player does
        BoxCollider2D playerBox = GetComponent<BoxCollider2D>();
        BoxCollider2D shadowBox = shadow.GetComponent<BoxCollider2D>();
        if (playerBox != null && shadowBox != null)
        {
            shadowBox.size = playerBox.size;
            shadowBox.offset = playerBox.offset;
        }
    }

    // ---- feedback while recording ----------------------------------------------------------------------------------

    void UpdateFeedback()
    {
        if (playerSprite != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            playerSprite.color = Color.Lerp(playerBaseColor, recordingTint, 0.35f + 0.4f * pulse);
        }
        if (hudFill != null) hudFill.fillAmount = Mathf.Clamp01(1f - recordTimer / recordDuration);
    }

    void StopFeedback()
    {
        if (playerSprite != null) playerSprite.color = playerBaseColor;
        if (hudRoot != null) Destroy(hudRoot);
        hudRoot = null;
        hudFill = null;
    }

    void BuildHud()
    {
        if (hudRoot != null) return;

        hudRoot = new GameObject("Recording HUD");
        Canvas canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // dark track under the hearts, and a cyan bar that drains while the clone is being recorded
        Image track = NewBar("Track", hudRoot.transform, new Color(0.05f, 0.08f, 0.15f, 0.8f));
        hudFill = NewBar("Fill", track.transform, new Color(0.45f, 0.85f, 1f, 1f));
        hudFill.type = Image.Type.Filled;
        hudFill.fillMethod = Image.FillMethod.Horizontal;
        hudFill.fillAmount = 1f;
        RectTransform fillRect = hudFill.rectTransform;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;

        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(0f, 1f);
        trackRect.sizeDelta = new Vector2(240f, 22f);
        trackRect.anchoredPosition = new Vector2(24f, -96f);
    }

    static Image NewBar(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, name = "RecordBarWhite" };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        return whiteSprite;
    }
}
