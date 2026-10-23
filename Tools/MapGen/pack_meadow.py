"""
Builds the art for the meadow and the kingdom (City map):

    python Tools/MapGen/pack_meadow.py

writes Assets/TileSets/TilesetMeadow.png, TilesetMeadow_cells.txt (same format as pack_desert.py),
TilesetMeadow_water.json (which tile to use for each shoreline shape) and TilesetMeadow_objects.json
(multi-tile props: where they are in the sheet and which tiles are solid).

Sources (see Tools/MapGen/source/):
  * forest_sample/  - "16x16 Pixel Forest Tileset" free sample by inkBubi, CC0: trees, bushes, rocks and the
                      grass-and-water autotile. Greens are shifted to the town's green.
  * 0x72_DungeonTilesetII_v1.7.png - CC0: brick wall pieces, banners and the big wooden door, turned from dark
                      dungeon brick into pale castle stone.
  * flowers, mushrooms and grass tufts are drawn here in code.
In Unity: Lythrum > Setup > Import Pack Tiles. Map tokens start with `m`.
"""
import colorsys
import json
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(HERE, "source")
FOREST = os.path.join(SRC, "forest_sample")
OUT_DIR = os.path.join(ROOT, "Assets", "TileSets")
COLS, ROWS = 16, 20
TILE = 16


# ------------------------------------------------------------------------------------------- recolouring
def shift_greens(img, to_hue=85.0, from_hue=108.0, sat_boost=0.12):
    """Turn the sample's cool green into the town's warmer green (hue ~85); blues stay water blue."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            deg = h * 360
            if 60 <= deg <= 215 and s > 0.15:                       # green, or the pine's blue-green
                deg = deg - (from_hue - to_hue) if deg <= 175 else deg - 70      # teal -> deep green
                s = min(1.0, s + sat_boost)
                r2, g2, b2 = colorsys.hsv_to_rgb(deg / 360, s, v)
                px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return out


def to_stone(img, hue=215.0):
    """Dark brown dungeon brick -> pale blue-grey castle stone. Strongly coloured pixels (wood, banners) stay."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.42:
                v2 = min(1.0, v * 1.75 + 0.12)
                r2, g2, b2 = colorsys.hsv_to_rgb(hue / 360, min(0.16, s * 0.5), v2)
                px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return out


def components(img, min_pixels=6):
    """Bounding boxes (x0, y0, x1, y1) of the separate sprites in an image with transparency."""
    w, h = img.size
    px = img.load()
    seen = set()
    boxes = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 0 and (x, y) not in seen:
                stack, xs, ys = [(x, y)], [], []
                seen.add((x, y))
                while stack:
                    cx, cy = stack.pop()
                    xs.append(cx)
                    ys.append(cy)
                    for dx in (-1, 0, 1):
                        for dy in (-1, 0, 1):
                            nx, ny = cx + dx, cy + dy
                            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and px[nx, ny][3] > 0:
                                seen.add((nx, ny))
                                stack.append((nx, ny))
                if len(xs) >= min_pixels:
                    boxes.append((min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    return sorted(boxes, key=lambda b: (b[1] // 40, b[0]))


# ------------------------------------------------------------------------------------------- drawn decor
def draw(rows, palette):
    img = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in palette:
                px[x, y] = palette[ch]
    return img


STEM, LEAF, DARK = (70, 130, 40, 255), (96, 158, 52, 255), (52, 104, 36, 255)


def flower(petal, centre=(250, 214, 70, 255)):
    rows = ["................", "................", "................", "......ppp.......",
            ".....ppcpp......", "......ppp.......", ".......s........", ".......s.l......",
            ".......sll......", "......ls........", ".......s........", "................",
            "................", "................", "................", "................"]
    return draw(rows, {"p": petal, "c": centre, "s": STEM, "l": LEAF})


def tuft():
    rows = ["................", "................", "................", "................",
            "....d.....l.....", "....d..l..l.....", "...dl..l.ll.....", "...dl.ll.l......",
            "...dllll.l......", "....dlll.l......", "....ddll.d......", "................",
            "................", "................", "................", "................"]
    return draw(rows, {"d": DARK, "l": LEAF})


def mushroom():
    rows = ["................", "................", "................", "................",
            "................", ".......rrr......", "......rwrrr.....", ".....rrrrwrr....",
            ".....rrrrrrr....", "......ccc.......", "......ccc.......", ".....ccccc......",
            "................", "................", "................", "................"]
    return draw(rows, {"r": (200, 50, 50, 255), "w": (250, 240, 230, 255), "c": (232, 214, 176, 255)})


def clover():
    rows = ["................"] * 16
    rows = list(rows)
    rows[6] = "......ll.ll....."
    rows[7] = ".....llllllll...."
    rows[8] = "......ll.ll....."
    rows[9] = "....ll..l..ll..."
    rows[10] = "...llll.l.llll.."
    rows[11] = "....ll.....ll..."
    return draw(rows, {"l": LEAF})


# ------------------------------------------------------------------------------------------- packing
class Atlas:
    def __init__(self):
        self.img = Image.new("RGBA", (COLS * TILE, ROWS * TILE), (0, 0, 0, 0))
        self.cursor = [0, 0]      # next free (col, row) for 1x1 tiles

    def tile(self, piece):
        c, r = self.cursor
        self.img.alpha_composite(piece, (c * TILE, r * TILE))
        self.cursor[0] += 1
        if self.cursor[0] >= COLS:
            self.cursor = [0, self.cursor[1] + 1]
        return r * COLS + c

    def newline(self):
        if self.cursor[0] != 0:
            self.cursor = [0, self.cursor[1] + 1]

    def block(self, piece, col, row, cols, rows_):
        """Bottom-centre `piece` in a block of whole cells."""
        x = col * TILE + (cols * TILE - piece.width) // 2
        y = row * TILE + (rows_ * TILE - piece.height)
        self.img.alpha_composite(piece, (x, y))


def mask_for(atlas, col, row, cols, rows_):
    """B above the base, C on the bottom row's non-empty cells (the trunk / base), '.' for empty cells."""
    lines = []
    for r in range(rows_):
        line = ""
        for c in range(cols):
            cell = atlas.img.crop(((col + c) * TILE, (row + r) * TILE, (col + c + 1) * TILE, (row + r + 1) * TILE))
            if cell.getbbox() is None:
                line += "."
            else:
                line += "C" if r == rows_ - 1 else "B"
        lines.append(line)
    return lines


def main():
    atlas = Atlas()
    objects = {}

    # ---- water: one tile for every shoreline shape ----------------------------------------------------------------
    water_src = shift_greens(Image.open(os.path.join(FOREST, "grass_deep_water.png")).convert("RGBA"))
    raw = Image.open(os.path.join(FOREST, "grass_deep_water.png")).convert("RGBA")

    def is_water(p):
        return p[3] >= 128 and p[2] > p[0] + 30 and p[2] > p[1] + 10

    def signature(c, r):
        px = raw.crop((c * TILE, r * TILE, c * TILE + TILE, r * TILE + TILE)).load()
        w = lambda x, y: is_water(px[x, y])
        mids = (sum(w(x, 0) for x in range(6, 10)), sum(w(x, 15) for x in range(6, 10)),
                sum(w(0, y) for y in range(6, 10)), sum(w(15, y) for y in range(6, 10)))
        corner = lambda xs, ys: sum(w(x, y) for x in xs for y in ys)
        cs = (corner((0, 1), (0, 1)), corner((14, 15), (0, 1)), corner((0, 1), (14, 15)), corner((14, 15), (14, 15)))
        return "".join("1" if v >= 2 else "0" for v in mids + cs)       # N S W E NW NE SW SE

    water = {}
    for r in range(4):                      # the first 4 rows hold one full set; the rest are variants
        for c in range(12):
            tile = raw.crop((c * TILE, r * TILE, (c + 1) * TILE, (r + 1) * TILE))
            if tile.getbbox() is None:
                continue
            sig = signature(c, r)
            if sig not in water and sig != "00000000":
                water[sig] = atlas.tile(water_src.crop((c * TILE, r * TILE, (c + 1) * TILE, (r + 1) * TILE)))
    full = water.get("11111111")
    if full is None:
        full = atlas.tile(water_src.crop((12 * TILE, 0, 13 * TILE, TILE)))
        water["11111111"] = full
    with open(os.path.join(OUT_DIR, "TilesetMeadow_water.json"), "w", newline="\n") as f:
        json.dump(water, f, indent=1, sort_keys=True)
    atlas.newline()

    # ---- drawn decor (walk-over) ----------------------------------------------------------------------------------
    decor = {"m_flower_red": flower((226, 64, 64, 255)), "m_flower_yellow": flower((250, 214, 70, 255), (230, 140, 40, 255)),
             "m_flower_white": flower((250, 250, 245, 255)), "m_flower_blue": flower((96, 140, 240, 255)),
             "m_flower_pink": flower((240, 130, 190, 255)), "m_tuft": tuft(), "m_mushroom": mushroom(), "m_clover": clover()}
    for name, piece in decor.items():
        index = atlas.tile(piece)
        objects[name] = [index % COLS, index // COLS, ["D"]]
    atlas.newline()

    # ---- trees, bushes, rocks (solid at the base only) ----------------------------------------------------------------
    trees = shift_greens(Image.open(os.path.join(FOREST, "trees.png")).convert("RGBA"))
    bushes = shift_greens(Image.open(os.path.join(FOREST, "bushes.png")).convert("RGBA"))
    stones = Image.open(os.path.join(FOREST, "stones.png")).convert("RGBA")

    def place_object(name, piece, col, row):
        cols, rows_ = -(-piece.width // TILE), -(-piece.height // TILE)
        atlas.block(piece, col, row, cols, rows_)
        objects[name] = [col, row, mask_for(atlas, col, row, cols, rows_)]
        return cols, rows_

    tree_boxes = components(trees)
    row = atlas.cursor[1] + 1 if atlas.cursor[0] else atlas.cursor[1]
    col = 0
    for name, box in zip(("m_oak", "m_pine"), tree_boxes[:2]):
        cols, rows_ = place_object(name, trees.crop(box), col, row)
        col += cols
    row += 6
    col = 0
    for i, box in enumerate(components(bushes)[:4]):
        cols, rows_ = place_object("m_bush_%d" % (i + 1), bushes.crop(box), col, row)
        col += cols
    col2 = col
    for i, box in enumerate(components(stones)[:3]):
        cols, rows_ = place_object("m_rock_%d" % (i + 1), stones.crop(box), col2, row)
        col2 += cols
    row += 3

    # ---- castle stone: walls, door, banners (from the dungeon pack, recoloured) ---------------------------------------
    dungeon = Image.open(os.path.join(SRC, "0x72_DungeonTilesetII_v1.7.png")).convert("RGBA")

    def d(x, y, w=16, h=16):
        return to_stone(dungeon.crop((x, y, x + w, y + h)))

    walls = {"cap_l": d(16, 0), "cap_m": d(32, 0), "cap_r": d(48, 0), "l": d(16, 16), "m": d(32, 16), "r": d(48, 16)}
    wall_cells = {}
    for key, piece in walls.items():
        atlas.img.alpha_composite(piece, (len(wall_cells) * TILE, row * TILE))
        wall_cells[key] = [len(wall_cells), row]
    with open(os.path.join(OUT_DIR, "TilesetMeadow_walls.json"), "w", newline="\n") as f:
        json.dump(wall_cells, f, indent=1, sort_keys=True)

    # banners keep their colours but sit on stone; the wooden door keeps its wood
    for i, (name, (sx, sy)) in enumerate([("m_banner_red", (16, 32)), ("m_banner_blue", (32, 32)),
                                          ("m_banner_green", (16, 48)), ("m_banner_yellow", (32, 48))]):
        atlas.img.alpha_composite(d(sx, sy), ((6 + i) * TILE, row * TILE))
        objects[name] = [6 + i, row, ["C"]]
    for i, (name, (sx, sy)) in enumerate([("m_window_1", (48, 32)), ("m_window_2", (48, 48))]):
        atlas.img.alpha_composite(d(sx, sy), ((10 + i) * TILE, row * TILE))
        objects[name] = [10 + i, row, ["C"]]
    row += 1
    top, left, leaf, right = d(32, 224, 32, 16), d(16, 240, 16, 32), d(32, 240, 32, 32), d(64, 240, 16, 32)
    atlas.img.alpha_composite(top, (1 * TILE, row * TILE))
    atlas.img.alpha_composite(left, (0 * TILE, (row + 1) * TILE))
    atlas.img.alpha_composite(leaf, (1 * TILE, (row + 1) * TILE))
    atlas.img.alpha_composite(right, (3 * TILE, (row + 1) * TILE))
    objects["m_castle_door"] = [0, row, [".BB.", "CCCC", "CCCC"]]
    # round fountain pieces from the dungeon pack are reused as castle fountains
    fountain = [d(64, 0), d(64, 16), d(64, 32)]
    for i, piece in enumerate(fountain):
        atlas.img.alpha_composite(piece, ((5 + 0) * TILE, (row + i) * TILE))
    objects["m_fountain"] = [5, row, ["C", "C", "C"]]

    # ---- write ---------------------------------------------------------------------------------------------------------------
    atlas.img.save(os.path.join(OUT_DIR, "TilesetMeadow.png"))
    lines = []
    for r in range(ROWS):
        for c in range(COLS):
            if atlas.img.crop((c * TILE, r * TILE, (c + 1) * TILE, (r + 1) * TILE)).getbbox():
                lines.append("%d %d %d" % (c, r, r * COLS + c))
    with open(os.path.join(OUT_DIR, "TilesetMeadow_cells.txt"), "w", newline="\n") as f:
        f.write("# col row index - generated by Tools/MapGen/pack_meadow.py\n" + "\n".join(lines) + "\n")
    with open(os.path.join(OUT_DIR, "TilesetMeadow_objects.json"), "w", newline="\n") as f:
        json.dump(objects, f, indent=1, sort_keys=True)
    print("wrote TilesetMeadow.png with", len(lines), "tiles,", len(water), "water shapes,", len(objects), "objects")


if __name__ == "__main__":
    main()
