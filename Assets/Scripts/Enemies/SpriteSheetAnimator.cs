using System;
using UnityEngine;

// Plays frame-by-frame animation straight from sprite sheet textures, no Animator Controller needed.
// Each clip is a grid of equally sized cells read left-to-right, top-to-bottom.
// Listed in TimeStoppable.disableWhileFrozen so it pauses during Time Stop.
public class SpriteSheetAnimator : MonoBehaviour
{
    [Serializable]
    public class Clip
    {
        public string name = "Idle";
        public Texture2D sheet;
        public Vector2Int cellSize = new Vector2Int(64, 64);
        [Tooltip("How many cells actually hold a frame (the rest of the grid is empty).")]
        public int frameCount = 4;
        public float fps = 8f;
        public bool loop = true;

        [NonSerialized] public Sprite[] sprites;
    }

    public SpriteRenderer target;
    public float pixelsPerUnit = 100f;
    public Clip[] clips;

    // True once a non-looping clip has played its last frame (also true when nothing is playing).
    public bool Finished { get; private set; } = true;
    public string CurrentName => current != null ? current.name : null;

    private Clip current;
    private float time;
    private float forcedDuration;

    void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        foreach (Clip clip in clips) BuildSprites(clip);
    }

    void BuildSprites(Clip clip)
    {
        if (clip.sheet == null || clip.cellSize.x <= 0 || clip.cellSize.y <= 0)
        {
            clip.sprites = new Sprite[0];
            return;
        }

        int columns = Mathf.Max(1, clip.sheet.width / clip.cellSize.x);
        clip.sprites = new Sprite[clip.frameCount];
        for (int i = 0; i < clip.frameCount; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Rect rect = new Rect(column * clip.cellSize.x, clip.sheet.height - (row + 1) * clip.cellSize.y, clip.cellSize.x, clip.cellSize.y);
            Sprite sprite = Sprite.Create(clip.sheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = clip.name + "_" + i;
            clip.sprites[i] = sprite;
        }
    }

    Clip Find(string clipName)
    {
        foreach (Clip clip in clips)
        {
            if (clip.name != clipName) continue;
            if (clip.sprites == null) BuildSprites(clip);   // sprites aren't serialized: rebuild after a script reload
            return clip;
        }
        return null;
    }

    // duration > 0 stretches the clip to take exactly that long (used to time attack frames to the wind-up).
    public void Play(string clipName, float duration = 0f)
    {
        Clip clip = Find(clipName);
        if (clip == null || clip.sprites == null || clip.sprites.Length == 0) return;

        // Looping clips that are already playing keep going instead of restarting every frame.
        if (clip == current && clip.loop) return;

        current = clip;
        forcedDuration = duration;
        time = 0f;
        Finished = false;
        target.sprite = clip.sprites[0];
    }

    void Update()
    {
        if (current == null || Finished) return;
        if (current.sprites == null) BuildSprites(current);   // script reload while playing wipes the sprites
        if (current.sprites.Length == 0) return;

        float fps = forcedDuration > 0f ? current.sprites.Length / forcedDuration : current.fps;
        time += Time.deltaTime;
        int frame = Mathf.FloorToInt(time * fps);

        if (frame >= current.sprites.Length)
        {
            if (current.loop)
            {
                frame %= current.sprites.Length;
            }
            else
            {
                frame = current.sprites.Length - 1;
                Finished = true;
            }
        }
        target.sprite = current.sprites[frame];
    }
}
