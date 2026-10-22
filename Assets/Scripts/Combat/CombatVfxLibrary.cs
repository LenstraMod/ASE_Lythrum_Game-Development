using System;
using UnityEngine;

// The pixel-art sprite sheets used by the combat effects (slash, fire). Built by Lythrum > Setup > Combat Effects
// (Assets/Resources/CombatVfxLibrary.asset). Frames are cut from the sheet at runtime, left-to-right, top-to-bottom.
[CreateAssetMenu(menuName = "Lythrum/Combat Vfx Library")]
public class CombatVfxLibrary : ScriptableObject
{
    [Serializable]
    public class Sheet
    {
        public Texture2D texture;
        public Vector2Int cellSize = new Vector2Int(64, 47);
        public int frameCount = 9;
        public float pixelsPerUnit = 16f;

        [NonSerialized] private Sprite[] frames;

        public Sprite[] Frames
        {
            get
            {
                // Runtime-made sprites are destroyed when Play mode ends, so check for that too.
                if (frames != null && frames.Length > 0 && frames[0] != null) return frames;
                if (texture == null) return new Sprite[0];

                int columns = Mathf.Max(1, texture.width / cellSize.x);
                frames = new Sprite[frameCount];
                for (int i = 0; i < frameCount; i++)
                {
                    Rect rect = new Rect((i % columns) * cellSize.x, texture.height - (i / columns + 1) * cellSize.y, cellSize.x, cellSize.y);
                    frames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
                return frames;
            }
        }
    }

    public Sheet slash = new Sheet { cellSize = new Vector2Int(64, 47), frameCount = 9 };
    public Sheet fire = new Sheet { cellSize = new Vector2Int(8, 10), frameCount = 6 };
}
