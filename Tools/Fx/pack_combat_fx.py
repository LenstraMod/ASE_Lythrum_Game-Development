"""
Prepares the pixel-art combat effects:

    python Tools/Fx/pack_combat_fx.py

  Assets/Sprites/Effects/slash_pixel.png  - the sword slash, 3x3 frames of 64x47 (as drawn)
  Assets/Sprites/Effects/fire_pixel.png   - the flame, 3x2 frames of 8x10: the original is drawn at ~7x pixel size,
                                            so it is sampled back down to real pixels (they then match the game's)
In Unity: Lythrum > Setup > Combat Effects builds the library the game reads.
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Sprites", "Effects")


def main():
    os.makedirs(OUT, exist_ok=True)
    Image.open(os.path.join(HERE, "source", "slash_tbbk.png")).convert("RGBA").save(os.path.join(OUT, "slash_pixel.png"))

    fire = Image.open(os.path.join(HERE, "source", "fire_kertex.png")).convert("RGBA")
    native = fire.resize((24, 20), Image.NEAREST)           # 3 x 2 frames of 8 x 10 real pixels
    # the original has an opaque grey (165,165,165) background baked in - that was the grey box around every flame
    px = native.load()
    for y in range(native.height):
        for x in range(native.width):
            if px[x, y][:3] == (165, 165, 165):
                px[x, y] = (0, 0, 0, 0)
    native.save(os.path.join(OUT, "fire_pixel.png"))
    print("wrote slash_pixel.png (9 frames, 64x47) and fire_pixel.png (6 frames, 8x10)")


if __name__ == "__main__":
    main()
