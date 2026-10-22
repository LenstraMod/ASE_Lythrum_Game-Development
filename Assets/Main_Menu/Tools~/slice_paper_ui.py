"""
Potong Paper_UI_SpriteSheet.png menjadi daftar sprite (rect + nama + border 9-slice).

    python slice_paper_ui.py <Paper_UI_SpriteSheet.png> <output.json> [preview.png]

Bagian-bagian yang berdekatan (mis. ikon yang terdiri dari beberapa potongan) digabung
dengan dilasi kecil. Sprite yang dipakai UI diberi nama tetap lewat NAMED; sisanya diberi
nama Paper_UI_XX berurutan (baris lalu kolom). Output dibaca oleh MainMenuSceneBuilder.
"""
import json
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

Image.MAX_IMAGE_PIXELS = None

GROUP_GAP = 6          # px: potongan yang lebih dekat dari ini dianggap satu sprite
MIN_AREA = 64          # buang noise

# (x, y, w, h) bounding box di sheet (origin kiri-atas) -> nama + border 9-slice (L, B, R, T).
NAMED = {
    # Border kiri lebar supaya lipatan sudut kiri-atas (~110 px) tidak ikut meregang.
    (49, 780, 621, 503): ("Paper_Large", (116, 64, 96, 64)),
    (66, 70, 380, 413): ("Paper_Medium", (40, 40, 40, 40)),
    (60, 544, 391, 175): ("Paper_Card", (40, 40, 40, 40)),
    (1243, 82, 633, 187): ("Plate_Wide", (48, 40, 48, 40)),
    (574, 464, 563, 111): ("Plate_Long", (40, 30, 40, 30)),
    (669, 96, 389, 111): ("Plate_Small", (40, 30, 40, 30)),
    (784, 656, 576, 144): ("Banner_Arc", (0, 0, 0, 0)),
    (848, 835, 416, 189): ("Banner_Wave", (0, 0, 0, 0)),
    (848, 1056, 416, 144): ("Banner_Wave_Small", (0, 0, 0, 0)),
    (64, 1664, 768, 560): ("Board_Stone_Ring", (64, 64, 64, 64)),
    (880, 1664, 768, 560): ("Board_Wood_Ring", (64, 64, 64, 64)),
    (1696, 1664, 768, 560): ("Board_Wood_Ring_Alt", (64, 64, 64, 64)),
    (64, 2288, 768, 560): ("Board_Wood", (64, 64, 64, 64)),
    (880, 2288, 768, 560): ("Board_Wood_Dark", (64, 64, 64, 64)),
    (64, 2912, 768, 560): ("Board_Sand", (64, 64, 64, 64)),
    (880, 2912, 768, 560): ("Board_Blue", (64, 64, 64, 64)),
}


def main():
    src, out = sys.argv[1], sys.argv[2]
    preview = sys.argv[3] if len(sys.argv) > 3 else None

    img = Image.open(src).convert("RGBA")
    alpha = np.asarray(img)[..., 3] > 0
    h, w = alpha.shape

    grouped = ndimage.binary_dilation(alpha, iterations=GROUP_GAP)
    labels, _ = ndimage.label(grouped)
    labels = labels * alpha                      # bounding box dari piksel asli, bukan hasil dilasi

    sprites = []
    for i, sl in enumerate(ndimage.find_objects(labels), 1):
        if sl is None:
            continue
        area = int((labels[sl] == i).sum())
        if area < MIN_AREA:
            continue
        x, y = sl[1].start, sl[0].start
        rw, rh = sl[1].stop - x, sl[0].stop - y
        sprites.append({"rect": [x, y, rw, rh]})

    sprites.sort(key=lambda s: (s["rect"][1] // 160, s["rect"][0]))

    counter = 1
    for s in sprites:
        key = tuple(s["rect"])
        if key in NAMED:
            s["name"], s["border"] = NAMED[key][0], list(NAMED[key][1])
        else:
            s["name"], s["border"] = "Paper_UI_%03d" % counter, [0, 0, 0, 0]
            counter += 1

    missing = set(n for n, _ in NAMED.values()) - set(s["name"] for s in sprites)
    if missing:
        print("PERINGATAN: sprite bernama tidak ditemukan:", sorted(missing))

    with open(out, "w") as f:
        json.dump({"width": w, "height": h, "sprites": sprites}, f, indent=1)
    print("sprites:", len(sprites), "named:", len(NAMED) - len(missing))

    if preview:
        bg = Image.new("RGBA", img.size, (60, 90, 60, 255))
        bg.alpha_composite(img)
        d = ImageDraw.Draw(bg)
        for s in sprites:
            x, y, rw, rh = s["rect"]
            named = not s["name"].startswith("Paper_UI_")
            d.rectangle([x, y, x + rw - 1, y + rh - 1], outline=(255, 60, 60) if named else (255, 255, 0), width=4 if named else 2)
        bg.convert("RGB").resize((w // 3, h // 3), Image.LANCZOS).save(preview)


if __name__ == "__main__":
    main()
