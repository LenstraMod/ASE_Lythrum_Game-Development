using UnityEngine;

// Plays a list of sprites on a SpriteRenderer (loops, or plays once and destroys itself).
// Spawn() builds the whole object, which is how the boss makes its shockwaves, flames and fear hand.
public class SpriteFramePlayer : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 30f;
    public bool loop;

    SpriteRenderer sr;
    float time;

    public static GameObject Spawn(Sprite[] frames, Vector3 position, float scale, float fps, bool loop, string sortingLayer, int order)
    {
        GameObject go = new GameObject("Boss FX");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = order;
        renderer.sprite = frames[0];

        SpriteFramePlayer player = go.AddComponent<SpriteFramePlayer>();
        player.frames = frames;
        player.fps = fps;
        player.loop = loop;
        return go;
    }

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (TimeStopManager.IsTimeStopped) return;      // Time Stop freezes effects too

        if (frames == null || frames.Length == 0) return;

        time += Time.deltaTime;
        int index = (int)(time * fps);
        if (index >= frames.Length)
        {
            if (!loop) { Destroy(gameObject); return; }
            index %= frames.Length;
        }
        sr.sprite = frames[index];
    }
}
