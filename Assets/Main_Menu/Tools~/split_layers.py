"""
Pecah Main_Menus.jpg (ilustrasi flat) menjadi layer parallax PNG transparan.

    python split_layers.py <Main_Menus.jpg> <folder_output> [folder_preview]

Output:
    L0_Sky.png          warna langit flat (tekstur kecil, di-stretch di Unity)
    L1_Cloud_XX.png     tiap awan terpisah (sisi yang terpotong di-fade agar aman di-scroll)
    L2_Ruins.png        reruntuhan + bukit tengah + dune jauh (bagian bawah diperpanjang)
    L3_Foreground.png   dune depan + karakter
    layers.json         posisi tiap layer/awan dalam piksel (dipakai MainMenuSceneBuilder)

Gambar dikerjakan di resolusi 3840x2160. Semua threshold di bawah dituning untuk art ini.
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

Image.MAX_IMAGE_PIXELS = None

WIDTH, HEIGHT = 3840, 2160

# Warna kunci (RGB) hasil analisis histogram gambar.
DUNE_SHADOW = np.array([148, 100, 60])   # pita gelap dune depan
DUNE_LIGHT = np.array([228, 172, 116])   # lereng terang dune depan / rim ridge

CLOUD_ALPHA_RANGE = (14.0, 40.0)    # jarak warna dari langit -> alpha awan 0..1
CLOUD_MIN_AREA = 1500               # komponen awan lebih kecil dari ini dibuang
CLOUD_CUT_FADE = 160                # px fade pada sisi awan yang terpotong
CLOUD_PAD = 4


def load(path):
    im = Image.open(path).convert("RGB")
    if im.size != (WIDTH, HEIGHT):
        im = im.resize((WIDTH, HEIGHT), Image.LANCZOS)
    return np.asarray(im).astype(np.float32)


def sky_color(img):
    top = img[:40].reshape(-1, 3)
    return np.median(top, axis=0)


def pinkish_mask(img):
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    pink = (r >= 165) & (b >= 105) & (b > g + 15)
    highlight = (r > 200) & (g > 170) & (b > 190)   # garis highlight putih pada awan
    return pink | highlight


def color_near(img, color, tol):
    return np.linalg.norm(img - color, axis=-1) < tol


def foreground_mask(img, pink):
    """Mask dune depan: pita gelap + lereng terang yang terhubung ke bawah, plus karakter."""
    h, w = pink.shape
    fg_colors = color_near(img, DUNE_SHADOW, 26) | color_near(img, DUNE_LIGHT, 20)
    labels, n = ndimage.label(fg_colors)
    sizes = ndimage.sum(fg_colors, labels, range(1, n + 1))
    keep = np.zeros(n + 1, bool)
    keep[1:] = sizes > 50000                      # hanya area besar (dune), bukan aksen kecil
    fg_seed = keep[labels]

    # Tepi atas per kolom, dihaluskan agar tidak bergerigi.
    has = fg_seed.any(axis=0)
    top = np.where(has, fg_seed.argmax(axis=0), h).astype(np.float32)
    top = ndimage.median_filter(top, size=31, mode="nearest")
    top = ndimage.minimum_filter1d(top, size=7) - 2   # sedikit naik untuk menutupi garis anti-alias

    rows = np.arange(h)[:, None]
    fg = rows >= top[None, :]

    # Rambut merah karakter yang mencuat di atas tepi dune (+ outline-nya lewat dilasi).
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    hair = (r > 150) & (g < 80) & (b < 100) & ~pink
    hair_lab, _ = ndimage.label(hair)
    touching = np.unique(hair_lab[fg & (hair_lab > 0)])
    hair_keep = np.isin(hair_lab, touching[touching > 0])
    fg |= ndimage.binary_dilation(hair_keep, iterations=4) & ~pink
    fg = ndimage.binary_fill_holes(fg)
    return fg, top


def soft(mask, radius=1.0):
    return ndimage.gaussian_filter(mask.astype(np.float32), radius)


def rgba(rgb, alpha):
    out = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha, 0, 1) * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def build_clouds(img, sky, pink, fg):
    h, w = pink.shape
    d = np.linalg.norm(img - sky, axis=-1)
    lo, hi = CLOUD_ALPHA_RANGE
    alpha = np.clip((d - lo) / (hi - lo), 0, 1)
    # Erode supaya ring anti-alias di tepi reruntuhan tidak ikut terbaca sebagai awan.
    region = ndimage.binary_erosion(pink, iterations=2, border_value=1) & ~fg
    alpha *= region

    # Unblend warna langit dari tepi awan: C = (P - (1-a) S) / a
    a3 = np.maximum(alpha, 1e-3)[..., None]
    color = (img - (1 - alpha[..., None]) * sky) / a3

    solid = ndimage.binary_closing(alpha > 0.5, iterations=3)
    labels, n = ndimage.label(ndimage.binary_dilation(solid, iterations=6))

    # Batas "terpotong": piksel non-langit (reruntuhan) dan tepi kiri/kanan gambar.
    blocker = ~pink | fg
    blab, bn = ndimage.label(blocker)
    bsizes = ndimage.sum(blocker, blab, range(1, bn + 1))
    tiny = np.zeros(bn + 1, bool)
    tiny[1:] = bsizes < 2000                      # garis/bintik di dalam awan bukan pemotong
    blocker &= ~tiny[blab]
    blocker[:, :2] = True
    blocker[:, -2:] = True
    cut_dist = ndimage.distance_transform_edt(~blocker)

    clouds = []
    for i in range(1, n + 1):
        comp = labels == i
        if (alpha * comp).sum() < CLOUD_MIN_AREA:
            continue
        ys, xs = np.nonzero(comp & (alpha > 0.02))
        if len(xs) == 0:
            continue
        x0, x1 = max(xs.min() - CLOUD_PAD, 0), min(xs.max() + CLOUD_PAD + 1, w)
        y0, y1 = max(ys.min() - CLOUD_PAD, 0), min(ys.max() + CLOUD_PAD + 1, h)
        a = (alpha * comp)[y0:y1, x0:x1]
        touches_cut = (cut_dist[y0:y1, x0:x1][a > 0.5] < 4).any()
        if touches_cut:
            t = np.clip(cut_dist[y0:y1, x0:x1] / CLOUD_CUT_FADE, 0, 1)
            a = a * (t * t * (3 - 2 * t))
        clouds.append(((x0, y0, x1, y1), color[y0:y1, x0:x1], a))
    # Urutkan kiri ke kanan supaya penomoran stabil.
    clouds.sort(key=lambda c: c[0][0])
    return clouds


def build_ruins(img, pink, fg, top):
    h, w = pink.shape
    solid = ~pink & ~fg
    # Buang serpihan kecil yang melayang di langit (sisa outline awan).
    lab, n = ndimage.label(solid)
    sizes = ndimage.sum(solid, lab, range(1, n + 1))
    small = np.zeros(n + 1, bool)
    small[1:] = sizes < 2000
    solid &= ~small[lab]
    alpha = soft(solid | fg, 0.8)
    color = img.copy()

    # Perpanjang warna tepat di atas tepi dune ke bawah (area yang tertutup foreground),
    # supaya tidak ada celah saat layer depan bergeser.
    cols = np.arange(w)
    src_y = np.clip(top.astype(int) - 4, 0, h - 1)
    fill = color[src_y, cols]                       # (w, 3)
    fill_a = np.where(solid[src_y, cols], 1.0, 0.0)  # kolom yang di belakangnya langit tetap transparan
    color = np.where(fg[..., None], fill[None, :, :], color)
    alpha = np.where(fg, fill_a[None, :], alpha)
    return color, alpha


def main():
    src, out = sys.argv[1], sys.argv[2]
    preview = sys.argv[3] if len(sys.argv) > 3 else None
    os.makedirs(out, exist_ok=True)

    img = load(src)
    sky = sky_color(img)
    pink = pinkish_mask(img)
    fg, top = foreground_mask(img, pink)
    pink &= ~fg

    meta = {"width": WIDTH, "height": HEIGHT, "sky": [int(c) for c in sky], "layers": {}, "clouds": []}

    sky_img = np.tile(sky, (36, 64, 1))
    rgba(sky_img, np.ones((36, 64))).save(os.path.join(out, "L0_Sky.png"))
    meta["layers"]["L0_Sky"] = [0, 0, WIDTH, HEIGHT]

    clouds = build_clouds(img, sky, pink, fg)
    for i, ((x0, y0, x1, y1), col, a) in enumerate(clouds, 1):
        name = f"L1_Cloud_{i:02d}"
        rgba(col, a).save(os.path.join(out, name + ".png"))
        meta["clouds"].append({"name": name, "rect": [int(x0), int(y0), int(x1), int(y1)]})

    ruins_col, ruins_a = build_ruins(img, pink, fg, top)
    rgba(ruins_col, ruins_a).save(os.path.join(out, "L2_Ruins.png"))
    meta["layers"]["L2_Ruins"] = [0, 0, WIDTH, HEIGHT]

    fg_a = soft(fg, 0.7)
    rgba(img, fg_a).save(os.path.join(out, "L3_Foreground.png"))
    meta["layers"]["L3_Foreground"] = [0, 0, WIDTH, HEIGHT]

    with open(os.path.join(out, "layers.json"), "w") as f:
        json.dump(meta, f, indent=2)

    print(f"sky={meta['sky']} clouds={len(clouds)}")

    if preview:
        os.makedirs(preview, exist_ok=True)
        save_previews(out, meta, preview)


def save_previews(out, meta, preview):
    def compose(offsets):
        canvas = Image.new("RGBA", (WIDTH, HEIGHT), tuple(meta["sky"]) + (255,))
        for c in meta["clouds"]:
            im = Image.open(os.path.join(out, c["name"] + ".png"))
            dx, dy = offsets.get("clouds", (0, 0))
            canvas.alpha_composite(im, (c["rect"][0] + dx, c["rect"][1] + dy))
        for name in ("L2_Ruins", "L3_Foreground"):
            im = Image.open(os.path.join(out, name + ".png"))
            dx, dy = offsets.get(name, (0, 0))
            layer = Image.new("RGBA", (WIDTH, HEIGHT))
            layer.paste(im, (dx, dy))
            canvas.alpha_composite(layer)
        return canvas.convert("RGB").resize((1920, 1080), Image.LANCZOS)

    compose({}).save(os.path.join(preview, "composite.png"))
    compose({"clouds": (-300, 0), "L2_Ruins": (-40, -15), "L3_Foreground": (-110, -40)}).save(
        os.path.join(preview, "composite_shift_a.png"))
    compose({"clouds": (500, 0), "L2_Ruins": (40, 15), "L3_Foreground": (110, 40)}).save(
        os.path.join(preview, "composite_shift_b.png"))
    for name in ("L2_Ruins", "L3_Foreground"):
        im = Image.open(os.path.join(out, name + ".png"))
        bg = Image.new("RGBA", im.size, (40, 200, 60, 255))
        bg.alpha_composite(im)
        bg.convert("RGB").resize((1920, 1080)).save(os.path.join(preview, name + "_check.png"))
    clouds_bg = Image.new("RGBA", (WIDTH, HEIGHT), (30, 30, 30, 255))
    for c in meta["clouds"]:
        clouds_bg.alpha_composite(Image.open(os.path.join(out, c["name"] + ".png")), tuple(c["rect"][:2]))
    clouds_bg.convert("RGB").resize((1920, 1080)).save(os.path.join(preview, "clouds_check.png"))


if __name__ == "__main__":
    main()
