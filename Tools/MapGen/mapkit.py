"""
Tiny tilemap toolkit used to author the Lythrum maps.

It knows the layout of Assets/TileSets/TilesetFloor.png and TilesetHouse.png
(16px grid, sliced as TilesetFloor_N / TilesetHouse_N) and produces:
  * Assets/Maps/Data/<Map>.txt  -> read by Assets/Editor/LythrumMapBuilder.cs
  * Tools/MapGen/preview/<Map>.png (+ _layers.png) -> visual check outside Unity

Coordinates here are (x, y) with y growing DOWN (row 0 = top of the map).

Layers follow the Grid in SettingInput.unity:
  Ground        terrain
  Decor         walk-over details (pebbles, shrubs, small props)
  WalkInFront   walkable structures drawn under the player (stairs, bridges)
  Collision     anything solid: object bases, walls, ledges, invisible blockers
  WalkinBehind  the upper part of objects, drawn over the player
The player only collides with its feet, so it can step "behind" the upper
part of houses, statues and pillars while their base blocks it.
"""
import os
import re
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TILESETS = os.path.join(ROOT, "Assets", "TileSets")
FLOOR_PNG = os.path.join(TILESETS, "TilesetFloor.png")
HOUSE_PNG = os.path.join(TILESETS, "TilesetHouse.png")
DESERT_PNG = os.path.join(TILESETS, "TilesetDesert.png")          # made by pack_desert.py
DESERT_CELLS = os.path.join(TILESETS, "TilesetDesert_cells.txt")
DUNGEON_PNG = os.path.join(TILESETS, "TilesetDungeon.png")        # made by pack_dungeon.py
DUNGEON_CELLS = os.path.join(TILESETS, "TilesetDungeon_cells.txt")
MEADOW_PNG = os.path.join(TILESETS, "TilesetMeadow.png")          # made by pack_meadow.py
MEADOW_CELLS = os.path.join(TILESETS, "TilesetMeadow_cells.txt")

# Draw order (bottom -> top). The Unity builder maps each one to a sorting layer.
LAYERS = ["Ground", "Decor", "WalkInFront", "Collision", "WalkinBehind"]

INVISIBLE = "00000000"   # per-tile colour for invisible blockers


def _load_grid(png, meta):
    """Map (gx, gy) grid cell (top-left origin) -> sprite index N."""
    height = Image.open(png).size[1]
    sec = open(meta, encoding="utf8").read().split("spriteSheet:")[1]
    inv = {}
    for m in re.finditer(r"name: \S+_(\d+)\s+rect:\s+serializedVersion: 2\s+x: (\d+)\s+y: (\d+)", sec):
        n, x, y = int(m.group(1)), int(m.group(2)), int(m.group(3))
        inv[(x // 16, (height - y - 16) // 16)] = n
    return inv


FLOOR = _load_grid(FLOOR_PNG, FLOOR_PNG + ".meta")
HOUSE = _load_grid(HOUSE_PNG, HOUSE_PNG + ".meta")


def _load_cells(path):
    """(col, row) -> sprite index for a repacked sheet (index = row * 16 + col)."""
    grid = {}
    if os.path.exists(path):
        for line in open(path, encoding="utf8"):
            if line.strip() and not line.startswith("#"):
                c, r, n = (int(v) for v in line.split())
                grid[(c, r)] = n
    return grid


DESERT = _load_cells(DESERT_CELLS)
DUNGEON = _load_cells(DUNGEON_CELLS)
MEADOW = _load_cells(MEADOW_CELLS)


def _load_json(name):
    path = os.path.join(TILESETS, name)
    if not os.path.exists(path):
        return {}
    import json
    with open(path, encoding="utf8") as f:
        return json.load(f)


MEADOW_WATER = _load_json("TilesetMeadow_water.json")      # "NSWE+NW NE SW SE" shoreline shape -> sprite index
MEADOW_WALLS = _load_json("TilesetMeadow_walls.json")      # castle stone pieces -> [col, row]
MEADOW_OBJECTS = _load_json("TilesetMeadow_objects.json")  # prop name -> [col, row, mask rows]

# ---------------------------------------------------------------------------
# Floor terrain blocks. Each block is an 11x7 region of TilesetFloor.png:
# a "base" ground with an autotiled "patch" terrain drawn on top of it.
# ---------------------------------------------------------------------------
BLOCKS = {
    "sand": (0, 0),     # base: deep orange sand   patch: pale sand (dunes / trails)
    "rose": (11, 0),    # base: rose sand          patch: pale sand
    "grass": (0, 7),    # base: green grass        patch: dirt road
    "forest": (11, 7),  # base: dark grass         patch: dirt road
    "snow": (0, 14),    # base: frost              patch: snow
    "dust": (11, 14),   # base: grey dust          patch: dark earth
}

# local (col,row) inside a block for each patch shape
_PATCH = {
    # (N, S, E, W) -> tile
    (False, True, True, False): (0, 0), (False, True, True, True): (1, 0), (False, True, False, True): (2, 0),
    (True, True, True, False): (0, 1), (True, True, False, True): (2, 1),
    (True, False, True, False): (0, 2), (True, False, True, True): (1, 2), (True, False, False, True): (2, 2),
}
_INNER = {"SE": (5, 1), "SW": (6, 1), "NE": (5, 2), "NW": (6, 2)}


def ftile(block, lx, ly):
    bx, by = BLOCKS[block]
    return "f%d" % FLOOR[(bx + lx, by + ly)]


def htile(gx, gy):
    n = HOUSE.get((gx, gy))
    return None if n is None else "h%d" % n


def dtile(gx, gy):
    n = DESERT.get((gx, gy))
    return None if n is None else "d%d" % n


def ktile(gx, gy):
    n = DUNGEON.get((gx, gy))
    return None if n is None else "k%d" % n


def mtile(gx, gy):
    n = MEADOW.get((gx, gy))
    return None if n is None else "m%d" % n


_SHEET_TILE = {"h": htile, "d": dtile, "k": ktile, "m": mtile}


# ---------------------------------------------------------------------------
# Multi-tile objects from TilesetHouse.png.
#   (gx, gy, mask) - mask rows top->bottom; one char per tile:
#   B = WalkinBehind (drawn over the player, walkable)
#   C = Collision    (solid - the footprint of the object)
#   F = WalkInFront  (walkable, drawn under the player)
#   D = Decor        (walkable detail)
#   . = skip that tile
# Rule of thumb: buildings collide everywhere except the roof ridge; statues,
# pillars and props collide only with their base row.
# ---------------------------------------------------------------------------
OBJECTS = {
    # buildings
    "house_orange": (0, 0, ["BBBB", "CCCC", "CCCC"]),
    "house_beige": (4, 0, ["BBBB", "CCCC", "CCCC"]),
    "house_orange2": (8, 0, ["BBBB", "CCCC", "CCCC"]),
    "house_red": (12, 0, ["BBBB", "CCCC", "CCCC"]),
    "shop_fish": (16, 0, ["BBB", "CCC", "CCC"]),
    "house_green": (19, 0, ["BBBB", "CCCC", "CCCC"]),
    "house_sand": (23, 0, ["BBB", "CCC", "CCC"]),
    "house_wood": (26, 0, ["BBB", "CCC", "CCC"]),
    "guild_hall": (25, 14, ["BBBB", "CCCC", "CCCC", "CCCC", "CCCC"]),
    "dome_temple": (29, 4, ["BBB", "CCC", "CCC", "CCC"]),
    "tent": (0, 7, ["BBB", "CCC", "CCC"]),
    "straw_hut": (3, 8, ["BBB", "CCC"]),
    "kiln": (31, 12, ["BB", "CC", "CC"]),
    "hut_sand": (29, 11, ["BB", "CC"]),
    "hut_stone": (29, 13, ["BB", "CC"]),
    "stone_arch": (29, 20, ["BBB", "BBB", "C.C"]),   # walk under the arch
    "iron_pen": (9, 10, ["CCCCC", "C.C.C", "CCCCC"]),
    # sandstone statues / ruins
    "s_pillar": (0, 15, ["B", "B", "C"]),
    "s_colossus": (1, 15, ["BB", "BB", "CC"]),
    "s_orb_shrine": (3, 15, ["BB", "CC"]),
    "s_monk": (5, 15, ["BB", "CC"]),
    "s_hound": (7, 15, ["B", "C"]),
    "s_toad": (3, 17, ["BB", "CC"]),
    "s_knight": (5, 17, ["BB", "CC"]),
    "s_stub": (7, 17, ["B", "C"]),
    "s_shrub": (8, 16, ["D"]),
    "s_block": (2, 18, ["C"]),
    "s_stairs": (10, 15, ["FFF", "FFF", "FFF"]),
    # mossy stone statues
    "g_pillar": (0, 19, ["B", "B", "C"]),
    "g_colossus": (1, 19, ["BB", "BB", "CC"]),
    "g_orb_shrine": (3, 19, ["BB", "CC"]),
    "g_monk": (5, 19, ["BB", "CC"]),
    "g_hound": (7, 19, ["B", "C"]),
    "g_toad": (3, 21, ["BB", "CC"]),
    "g_knight": (5, 21, ["BB", "CC"]),
    "g_stub": (7, 21, ["B", "C"]),
    "g_shrub": (8, 20, ["D"]),
    "g_block": (2, 22, ["C"]),
    "g_stairs": (10, 19, ["FFF", "FFF", "FFF"]),
    # small walk-over details
    "pebble": (1, 14, ["D"]),
    # props
    "barrel": (16, 15, ["C"]),
    "barrel_open": (17, 15, ["C"]),
    "barrel_shut": (18, 15, ["C"]),
    "bucket_water": (16, 13, ["C"]),
    "bucket_greens": (17, 13, ["C"]),
    "bucket_apples": (18, 13, ["C"]),
    "crate_fruit": (19, 14, ["C"]),
    "crate_bread": (20, 14, ["C"]),
    "crate_bin": (19, 15, ["C"]),
    "crate_loaves": (20, 15, ["C"]),
    "sack_red": (21, 12, ["C"]),
    "sack_cream": (21, 13, ["C"]),
    "sack_green": (21, 14, ["C"]),
    "sack_brown": (21, 15, ["C"]),
    "pot": (17, 20, ["C"]),
    "pot_dark": (18, 20, ["C"]),
    "lantern": (29, 16, ["C"]),
    "lantern_low": (29, 17, ["C"]),
    "campfire": (30, 15, ["C"]),
    "firewood": (31, 18, ["C"]),
    "logs": (29, 19, ["CC"]),
    "planks": (31, 19, ["CC"]),
    "stall_orange": (16, 16, ["BBB", "CCC"]),   # walk behind the awning
    "stall_cream": (19, 16, ["BBB", "CCC"]),
    "weapon_rack": (31, 15, ["CC"]),
    "sign_fruit": (20, 18, ["C"]),
    "sign_board": (21, 18, ["C"]),
    "stump": (21, 9, ["B", "C"]),
    "banner_red": (22, 20, ["B", "C"]),
    "banner_crimson": (23, 20, ["B", "C"]),
    "banner_teal": (24, 20, ["B", "C"]),
    "banner_green": (25, 20, ["B", "C"]),
    "banner_serpent": (26, 20, ["B", "C"]),
    "banner_violet": (27, 20, ["B", "C"]),
    "banner_skull": (28, 20, ["B", "C"]),
}

# Props from the Desert Ruins pack (TilesetDesert.png). Same mask rules as above; the 4th item says
# the cells come from the desert sheet.
OBJECTS.update({
    "d_pillar": (8, 0, ["BB", "BB", "CC"], "d"),
    "d_statue": (10, 0, ["BB", "CC"], "d"),
    "d_cactus": (4, 2, ["B", "C"], "d"),
    "d_cactus_small": (6, 1, ["C"], "d"),
    "d_tumbleweed": (5, 1, ["D"], "d"),
    "d_bones": (7, 3, ["D"], "d"),
    "d_pot_brown": (5, 3, ["C"], "d"),
    "d_pot_gold": (6, 3, ["C"], "d"),
    "d_pot_small": (5, 4, ["C"], "d"),
    "d_sign": (5, 2, ["DD"], "d"),
    "d_torch_red": (12, 1, ["C"], "d"),
    "d_torch_blue": (13, 1, ["C"], "d"),
    "d_torch_green": (14, 1, ["C"], "d"),
    "d_pit": (0, 3, ["C"], "d"),
    "d_pit_deep": (0, 4, ["C"], "d"),
})

# Props and walls from the 0x72 DungeonTileset II pack (TilesetDungeon.png). Wall banners, holes, goo and
# fountains already include their brick background, so they replace a wall tile (hence "C").
OBJECTS.update({
    "k_skull": (0, 0, ["D"], "k"), "k_chest": (1, 0, ["C"], "k"),
    "k_banner_blue": (2, 0, ["C"], "k"), "k_banner_red": (3, 0, ["C"], "k"),
    "k_banner_green": (4, 0, ["C"], "k"), "k_banner_yellow": (5, 0, ["C"], "k"),
    "k_flask_red": (6, 0, ["C"], "k"), "k_flask_blue": (7, 0, ["C"], "k"),
    "k_flask_green": (8, 0, ["C"], "k"), "k_flask_yellow": (9, 0, ["C"], "k"),
    "k_wall_hole_1": (10, 0, ["C"], "k"), "k_wall_hole_2": (11, 0, ["C"], "k"),
    "k_ladder": (12, 0, ["D"], "k"), "k_hole": (13, 0, ["C"], "k"), "k_stairs": (14, 0, ["D"], "k"),
    "k_crate": (0, 1, ["B", "C"], "k"),
    "k_column": (1, 1, ["B", "B", "C"], "k"), "k_column_wall": (2, 1, ["B", "B", "C"], "k"),
    "k_fountain_red": (3, 1, ["C", "C", "C"], "k"), "k_fountain_blue": (8, 1, ["C", "C", "C"], "k"),
    "k_goo": (9, 1, ["C", "C"], "k"),
    "k_door": (4, 1, [".BB.", "CCCC", "CCCC"], "k"),     # vault door, 4 wide: frame, leaf, frame
})

# Props from the meadow sheet (trees, bushes, rocks, flowers, castle door, banners, fountain)
for _name, (_gx, _gy, _mask) in MEADOW_OBJECTS.items():
    OBJECTS[_name] = (_gx, _gy, _mask, "m")

_KWALL = {"cap_l": (0, 4), "cap_m": (1, 4), "cap_r": (2, 4), "l": (3, 4), "m": (4, 4), "r": (5, 4)}
_KFLOORS = [(6, 4)] * 8 + [(7, 4), (8, 4), (9, 4), (10, 4), (11, 4), (12, 4), (13, 4)]   # plain stone most of the time

_MASK_LAYER = {"B": "WalkinBehind", "C": "Collision", "F": "WalkInFront", "D": "Decor"}

# Brick courtyard (desert pack) painted over pale sand: same idea as the sand patches.
# (N, S, E, W) neighbours inside the mask -> tile; _BRICK_INNER = concave corners.
_BRICK_PATCH = {
    (False, True, True, False): (3, 0), (False, True, True, True): (4, 0), (False, True, False, True): (1, 6),
    (True, True, True, False): (2, 0), (True, True, False, True): (0, 6),
    (True, False, True, False): (2, 1), (True, False, True, True): (3, 1), (True, False, False, True): (2, 6),
}
_BRICK_INNER = {"SE": (4, 6), "SW": (4, 1), "NE": (3, 6), "NW": (5, 0)}
_BRICK_FLOOR = (1, 0)
_BRICK_ORNAMENT = [(12, 0), (13, 0), (14, 0)]

# Autotiled wall/fence sets (same 7x2 layout, top-left grid cell given)
WALLSETS = {"sandstone": (8, 8), "palisade": (8, 4), "rail": (8, 6)}
# brick wall strip taken from the white brick house (tinted in the dungeon)
BRICK = {"top": (7, 11), "mid": (7, 12), "top_l": (6, 11), "mid_l": (6, 12), "top_r": (8, 11), "mid_r": (8, 12)}
# deck used for stairs and bridges: (top row, middle row, bottom row), 3 columns wide
DECKS = {"sand": (10, 15), "stone": (10, 19)}


class Map:
    def __init__(self, name, w, h, seed=1):
        self.name, self.w, self.h = name, w, h
        self.rng = random.Random(seed)
        self.layers = {l: [[None] * w for _ in range(h)] for l in LAYERS}
        self.busy = [[False] * w for _ in range(h)]  # tiles taken by objects / reserved
        self.tints = {}
        self.spawn = (w // 2, h // 2)
        self.background = None  # camera clear colour (r,g,b) or None
        # scene extras written to the data file and built by LythrumMapBuilder
        self.global_light = None    # (intensity, r, g, b) - makes the scene dark; None = unlit scene
        self.player_light = None    # (outer radius, inner radius, intensity, r, g, b)
        self.lights = []            # (x, y, radius, intensity, r, g, b, flicker)
        self.entries = []           # (name, x, y): where the player arrives from another scene
        self.portals = []           # (name, x, y, w, h, target scene, target entry)
        self.enemy_zones = []       # (x0, y0, x1, y1) tiles; if any, enemies are only placed inside them

    # -- basics -------------------------------------------------------------
    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def put(self, layer, x, y, tok, color=None):
        """color: optional per-tile RRGGBBAA multiplier."""
        if self.inside(x, y) and tok:
            self.layers[layer][y][x] = tok + ("@" + color if color else "")

    def get(self, layer, x, y):
        return self.layers[layer][y][x] if self.inside(x, y) else None

    def clear(self, layer, x, y):
        if self.inside(x, y):
            self.layers[layer][y][x] = None

    def reserve_rect(self, x0, y0, x1, y1):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if self.inside(x, y):
                    self.busy[y][x] = True

    # -- terrain ------------------------------------------------------------
    def fill_base(self, block, detail=0.06, cells=None):
        """Plain base ground, sprinkled with the block's 4 detail variants."""
        for y in range(self.h):
            for x in range(self.w):
                if cells is not None and not cells[y][x]:
                    continue
                lx = self.rng.randint(1, 4) if self.rng.random() < detail else 0
                self.put("Ground", x, y, ftile(block, lx, 5))

    def autotile(self, block, mask, oob_inside=True, decor=0.01):
        """Autotile the patch terrain of `block` over the (smoothed) mask.
        Returns (mask, {(x, y): (token, is_edge)})."""
        mask = open_mask(mask, self.w, self.h)

        def inm(x, y):
            if not self.inside(x, y):
                return oob_inside
            return mask[y][x]

        tiles = {}
        for y in range(self.h):
            for x in range(self.w):
                if not mask[y][x]:
                    continue
                n, s, e, w = inm(x, y - 1), inm(x, y + 1), inm(x + 1, y), inm(x - 1, y)
                edge = True
                if n and s and e and w:
                    tile = (1, 1)
                    for key, (dx, dy) in (("NE", (1, -1)), ("NW", (-1, -1)), ("SE", (1, 1)), ("SW", (-1, 1))):
                        if not inm(x + dx, y + dy):
                            tile = _INNER[key]
                            break
                    if tile == (1, 1):
                        edge = False
                        if self.rng.random() < decor:
                            tile = (self.rng.randint(0, 1), 4)
                else:
                    tile = _PATCH.get((n, s, e, w), (1, 1))
                tiles[(x, y)] = (ftile(block, *tile), edge)
        return mask, tiles

    def paint_patch(self, block, mask, oob_inside=True, decor=0.01):
        """Walkable patch (roads, dunes, mud) painted on the ground."""
        mask, tiles = self.autotile(block, mask, oob_inside, decor)
        for (x, y), (tok, _) in tiles.items():
            self.put("Ground", x, y, tok)
        return mask

    def plateau(self, block, mask, stairs=None, solid=False, oob_inside=False):
        """Raised ground / pit: the patch's border becomes a solid ledge on the
        Collision layer (like the platform in SettingInput).
        stairs=(x, deck_kind) opens the bottom ledge at columns x..x+2 and puts a
        3-row staircase below it. With solid=True the whole patch blocks (a pit)."""
        mask, tiles = self.autotile(block, mask, oob_inside, decor=0.0 if solid else 0.02)
        openings = set()
        if stairs:
            sx, kind = stairs
            bottom = max(y for x in range(sx, sx + 3) for y in range(self.h) if mask[y][x])
            for x in range(sx, sx + 3):
                openings.add((x, bottom))
            self.deck(kind, sx, bottom + 1, bottom + 3)
        for (x, y), (tok, edge) in tiles.items():
            if (x, y) in openings or not (edge or solid):
                self.put("Ground", x, y, tok)
            else:
                self.put("Collision", x, y, tok)
                self.busy[y][x] = True
        return mask

    def paint_brick(self, mask, ornament=0.04):
        """Temple brick floor with ragged edges (desert pack) on the Ground layer. Keep the mask
        inside pale sand so the sand on the edge tiles blends in. Returns the smoothed mask."""
        mask = open_mask(mask, self.w, self.h)

        def inm(x, y):
            return mask[y][x] if self.inside(x, y) else False

        for y in range(self.h):
            for x in range(self.w):
                if not mask[y][x]:
                    continue
                n, s, e, w = inm(x, y - 1), inm(x, y + 1), inm(x + 1, y), inm(x - 1, y)
                if n and s and e and w:
                    tile = _BRICK_FLOOR
                    for key, (dx, dy) in (("NE", (1, -1)), ("NW", (-1, -1)), ("SE", (1, 1)), ("SW", (-1, 1))):
                        if not inm(x + dx, y + dy):
                            tile = _BRICK_INNER[key]
                            break
                    if tile == _BRICK_FLOOR and self.rng.random() < ornament:
                        tile = self.rng.choice(_BRICK_ORNAMENT)
                else:
                    tile = _BRICK_PATCH.get((n, s, e, w), _BRICK_FLOOR)
                self.put("Ground", x, y, dtile(*tile))
        return mask

    def deck(self, kind, x, y0, y1):
        """3-wide walkable deck (stairs or a bridge) from row y0 to y1, drawn on
        WalkInFront. Any Collision under it is removed."""
        gx, gy = DECKS[kind]
        for y in range(y0, y1 + 1):
            row = 0 if y == y0 else (2 if y == y1 else 1)
            for dx in range(3):
                self.clear("Collision", x + dx, y)
                self.put("WalkInFront", x + dx, y, htile(gx + dx, gy + row))
                if self.inside(x + dx, y):
                    self.busy[y][x + dx] = True

    # -- objects ------------------------------------------------------------
    def fits(self, name, x, y, pad=0):
        gx, gy, rows = OBJECTS[name][:3]
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                if ch == ".":
                    continue
                for py in range(-pad, pad + 1):
                    for px in range(-pad, pad + 1):
                        tx, ty = x + dx + px, y + dy + py
                        if not self.inside(tx, ty):
                            return False
                        if self.busy[ty][tx]:
                            return False
        return True

    def stamp(self, name, x, y, force=False):
        gx, gy, rows = OBJECTS[name][:3]
        sheet_tile = _SHEET_TILE[OBJECTS[name][3] if len(OBJECTS[name]) > 3 else "h"]
        if not force and not self.fits(name, x, y):
            raise ValueError("%s: %s does not fit at %d,%d" % (self.name, name, x, y))
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                if ch == ".":
                    continue
                self.put(_MASK_LAYER[ch], x + dx, y + dy, sheet_tile(gx + dx, gy + dy))
                if self.inside(x + dx, y + dy):
                    self.busy[y + dy][x + dx] = True
        return (x, y, len(rows[0]), len(rows))

    def size(self, name):
        rows = OBJECTS[name][2]
        return len(rows[0]), len(rows)

    def scatter(self, names, count, region, allowed=None, pad=1, tries=4000):
        """Randomly drop objects inside region (x0,y0,x1,y1) where they fit."""
        x0, y0, x1, y1 = region
        placed = 0
        for _ in range(tries):
            if placed >= count:
                break
            name = self.rng.choice(names)
            w, h = self.size(name)
            x = self.rng.randint(x0, max(x0, x1 - w + 1))
            y = self.rng.randint(y0, max(y0, y1 - h + 1))
            if allowed is not None and not all(allowed[y + dy][x + dx] for dy in range(h) for dx in range(w)
                                               if self.inside(x + dx, y + dy)):
                continue
            if self.fits(name, x, y, pad):
                self.stamp(name, x, y)
                placed += 1
        return placed

    # -- walls --------------------------------------------------------------
    def wall_rect(self, wallset, x0, y0, x1, y1, gaps=()):
        """Closed rectangular wall; `gaps` = openings in the top/bottom rows
        given as ('top'|'bottom', xa, xb)."""
        bx, by = WALLSETS[wallset]
        T = lambda cx, cy: htile(bx + cx, by + cy)
        corner = {"TL": T(0, 0), "TR": T(1, 0), "BL": T(0, 1), "BR": T(1, 1)}
        hmid, vmid, hl, hr = T(3, 1), T(3, 0), T(2, 0), T(2, 1)
        post = T(4, 1)
        gapcells = set()
        for side, xa, xb in gaps:
            yy = y0 if side == "top" else y1
            for x in range(xa, xb + 1):
                gapcells.add((x, yy))

        def hrun(yy):
            for x in range(x0 + 1, x1):
                if (x, yy) in gapcells:
                    continue
                tok = hmid
                if (x - 1, yy) in gapcells:
                    tok = hl
                elif (x + 1, yy) in gapcells:
                    tok = hr
                elif (x - x0) % 6 == 0:
                    tok = post
                self._solid(x, yy, tok)

        self._solid(x0, y0, corner["TL"])
        self._solid(x1, y0, corner["TR"])
        self._solid(x0, y1, corner["BL"])
        self._solid(x1, y1, corner["BR"])
        hrun(y0)
        hrun(y1)
        for y in range(y0 + 1, y1):
            self._solid(x0, y, vmid)
            self._solid(x1, y, vmid)

    def _solid(self, x, y, tok, color=None):
        self.put("Collision", x, y, tok, color)
        if self.inside(x, y):
            self.busy[y][x] = True

    def brick_wall(self, x0, x1, y, color=None):
        """Two-tile high brick wall face occupying rows y-1 (top) and y (base)."""
        for x in range(x0, x1 + 1):
            if x == x0:
                top, mid = BRICK["top_l"], BRICK["mid_l"]
            elif x == x1:
                top, mid = BRICK["top_r"], BRICK["mid_r"]
            else:
                top, mid = BRICK["top"], BRICK["mid"]
            self._solid(x, y - 1, htile(*top), color)
            self._solid(x, y, htile(*mid), color)

    def block(self, x, y):
        """Invisible blocker on the Collision layer (only where nothing is solid yet)."""
        if self.inside(x, y) and not self.layers["Collision"][y][x]:
            self.put("Collision", x, y, ftile("sand", 0, 5), INVISIBLE)

    # -- dungeon (0x72 pack) ----------------------------------------------------------
    def fill_dungeon_floor(self, cells):
        """Stone floor with the occasional cracked tile, on every cell where `cells` is True."""
        for y in range(self.h):
            for x in range(self.w):
                if cells[y][x]:
                    self.put("Ground", x, y, ktile(*self.rng.choice(_KFLOORS)))

    def dungeon_wall(self, x0, x1, y, rows=2):
        """Brick wall across x0..x1 standing on the north side of floor row `y`: a thin cap row,
        then `rows` rows of brick. Solid (Collision)."""
        for x in range(x0, x1 + 1):
            end = "l" if x == x0 else ("r" if x == x1 else "m")
            cap = {"l": "cap_l", "m": "cap_m", "r": "cap_r"}[end]
            self._solid(x, y - rows - 1, ktile(*_KWALL[cap]))
            for r in range(rows):
                self._solid(x, y - rows + r, ktile(*_KWALL[end]))

    # -- meadow and castle (CC0 forest sample + recoloured 0x72 stone) ------------------------------------
    def paint_water(self, mask, layer="Collision"):
        """Lake / river with a grass bank. Water is solid, so it goes on the Collision layer. The shape of the
        bank comes from which neighbours are water, matched to the pack's tiles; returns the smoothed mask."""
        mask = open_mask(mask, self.w, self.h, k=3)

        def inm(x, y):
            return mask[y][x] if self.inside(x, y) else False

        shapes = {sig: int(idx) for sig, idx in MEADOW_WATER.items()}
        for y in range(self.h):
            for x in range(self.w):
                if not mask[y][x]:
                    continue
                n, s_, w, e = inm(x, y - 1), inm(x, y + 1), inm(x - 1, y), inm(x + 1, y)
                nw = n and w and inm(x - 1, y - 1)
                ne = n and e and inm(x + 1, y - 1)
                sw = s_ and w and inm(x - 1, y + 1)
                se = s_ and e and inm(x + 1, y + 1)
                want = "".join("1" if v else "0" for v in (n, s_, w, e, nw, ne, sw, se))
                idx = shapes.get(want)
                if idx is None:                                  # nearest shape if this exact one isn't drawn
                    idx = shapes[min(shapes, key=lambda k: sum(a != b for a, b in zip(k, want)))]
                self.put(layer, x, y, "m%d" % idx)
                self.busy[y][x] = True
        return mask

    def castle_block(self, x0, y0, x1, y1, darken=0.22):
        """A solid mass of pale castle stone: a cap row on top, brick below, shaded a little darker towards the
        bottom so tall walls and towers have depth. Seen from the south, so only the front shows."""
        pieces = {key: mtile(*cell) for key, cell in MEADOW_WALLS.items()}
        rows = max(1, y1 - y0)
        for x in range(x0, x1 + 1):
            end = "l" if x == x0 else ("r" if x == x1 else "m")
            self._solid(x, y0, pieces["cap_" + end])
            for y in range(y0 + 1, y1 + 1):
                f = 1.0 - darken * (y - y0) / rows
                v = "%02x" % int(255 * f)
                self._solid(x, y, pieces[end], v + v + v + "ff")

    # -- finishing touches ----------------------------------------------------
    def shade_ground(self, lo=0.9, cell=7, seed=0):
        """Soft large-scale light/dark variation over the Ground layer (dune shading),
        done with per-tile colour multipliers so flat sand stops looking tiled."""
        rng = random.Random(seed)

        def lattice(step):
            gw, gh = self.w // step + 3, self.h // step + 3
            return step, [[rng.random() for _ in range(gw)] for _ in range(gh)]

        def sample(lat, x, y):
            step, grid = lat
            fx, fy = x / step, y / step
            ix, iy = int(fx), int(fy)
            tx, ty = fx - ix, fy - iy
            tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
            a = grid[iy][ix] * (1 - tx) + grid[iy][ix + 1] * tx
            b = grid[iy + 1][ix] * (1 - tx) + grid[iy + 1][ix + 1] * tx
            return a * (1 - ty) + b * ty

        big, small = lattice(cell), lattice(max(2, cell // 2))
        for y in range(self.h):
            for x in range(self.w):
                tok = self.layers["Ground"][y][x]
                if not tok or "@" in tok:
                    continue
                n = 0.7 * sample(big, x, y) + 0.3 * sample(small, x, y)
                v = lo + (1.0 - lo) * n
                rgb = (v, v * 0.985, v * 0.955)
                self.layers["Ground"][y][x] = tok + "@" + "".join("%02x" % int(c * 255) for c in rgb) + "ff"

    def decor_cluster(self, names, cx, cy, radius, count, avoid=None, squash=0.75):
        """A natural-looking patch of walk-over details (shrubs, pebbles) around a centre,
        instead of uniform random noise. `avoid` is a mask (roads) to keep clear."""
        import math
        placed = 0
        for _ in range(count * 25):
            if placed >= count:
                break
            ang, rad = self.rng.random() * 6.2832, radius * math.sqrt(self.rng.random())
            x, y = int(round(cx + math.cos(ang) * rad)), int(round(cy + math.sin(ang) * rad * squash))
            if not self.inside(x, y) or (avoid is not None and avoid[y][x]):
                continue
            name = self.rng.choice(names)
            if self.fits(name, x, y, 0):
                self.stamp(name, x, y)
                placed += 1
        return placed

    def reachable(self, start):
        """Cells the player can walk to from `start`: everything not on the Collision layer."""
        sx, sy = start
        seen = {(sx, sy)}
        todo = [(sx, sy)]
        while todo:
            x, y = todo.pop()
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if (nx, ny) in seen or not self.inside(nx, ny) or self.layers["Collision"][ny][nx]:
                    continue
                seen.add((nx, ny))
                todo.append((nx, ny))
        return seen

    # -- output -------------------------------------------------------------
    def save(self, data_dir, preview_dir):
        os.makedirs(data_dir, exist_ok=True)
        os.makedirs(preview_dir, exist_ok=True)
        lines = ["# Generated by Tools/MapGen/build_maps.py - edit the script, not this file.",
                 "map %s %d %d" % (self.name, self.w, self.h),
                 "spawn %d %d" % self.spawn]
        if self.background:
            lines.append("background %.3f %.3f %.3f" % self.background)
        for layer, (r, g, b, a) in self.tints.items():
            lines.append("tint %s %.3f %.3f %.3f %.3f" % (layer, r, g, b, a))
        if self.global_light:
            lines.append("global_light %.3f %.3f %.3f %.3f" % self.global_light)
        if self.player_light:
            lines.append("player_light %.2f %.2f %.3f %.3f %.3f %.3f" % self.player_light)
        for light in self.lights:
            lines.append("light %.2f %.2f %.2f %.3f %.3f %.3f %.3f %.2f" % light)
        for name, x, y in self.entries:
            lines.append("entry %s %.2f %.2f" % (name, x, y))
        for zone in self.enemy_zones:
            lines.append("enemy_zone %d %d %d %d" % zone)
        for name, x, y, w, h, scene, entry in self.portals:
            lines.append("portal %s %.2f %.2f %d %d %s %s" % (name, x, y, w, h, scene, entry))
        for layer in LAYERS:
            grid = self.layers[layer]
            if not any(any(row) for row in grid):
                continue
            lines.append("layer %s" % layer)
            for row in grid:
                lines.append(" ".join(t or "." for t in row))
        path = os.path.join(data_dir, self.name + ".txt")
        with open(path, "w", encoding="utf8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        render(self, os.path.join(preview_dir, self.name + ".png"))
        render(self, os.path.join(preview_dir, self.name + "_layers.png"), debug=True)
        if self.global_light:
            render_lit(self, os.path.join(preview_dir, self.name + ".png"), os.path.join(preview_dir, self.name + "_lit.png"))
        return path


def open_mask(mask, w, h, k=3):
    """Morphological opening: keep only cells covered by some k*k all-True window,
    so patches never have 1-2 tile wide slivers the autotile can't draw."""
    out = [[False] * w for _ in range(h)]
    for y in range(-k + 1, h):
        for x in range(-k + 1, w):
            ok = True
            for dy in range(k):
                for dx in range(k):
                    tx, ty = x + dx, y + dy
                    inside = 0 <= tx < w and 0 <= ty < h
                    if inside and not mask[ty][tx]:
                        ok = False
                        break
                    if not inside and not _edge_ok(mask, tx, ty, w, h):
                        ok = False
                        break
                if not ok:
                    break
            if ok:
                for dy in range(k):
                    for dx in range(k):
                        tx, ty = x + dx, y + dy
                        if 0 <= tx < w and 0 <= ty < h:
                            out[ty][tx] = True
    return out


def _edge_ok(mask, tx, ty, w, h):
    # Out-of-map cells count as "inside" when the nearest map cell is inside.
    cx, cy = min(max(tx, 0), w - 1), min(max(ty, 0), h - 1)
    return mask[cy][cx]


def empty_mask(w, h, value=False):
    return [[value] * w for _ in range(h)]


def mask_rect(mask, x0, y0, x1, y1, value=True):
    h, w = len(mask), len(mask[0])
    for y in range(max(0, y0), min(h, y1 + 1)):
        for x in range(max(0, x0), min(w, x1 + 1)):
            mask[y][x] = value


def mask_ellipse(mask, cx, cy, rx, ry, value=True):
    h, w = len(mask), len(mask[0])
    for y in range(h):
        for x in range(w):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                mask[y][x] = value


def mask_path(mask, points, width=3):
    """Thick poly-line through the given points."""
    r = width / 2.0
    for (ax, ay), (bx, by) in zip(points, points[1:]):
        steps = max(abs(bx - ax), abs(by - ay)) * 2 + 1
        for i in range(steps + 1):
            t = i / steps
            px, py = ax + (bx - ax) * t, ay + (by - ay) * t
            for y in range(int(py - r - 1), int(py + r + 2)):
                for x in range(int(px - r - 1), int(px + r + 2)):
                    if abs(x - px) <= r - 0.5 + 0.01 and abs(y - py) <= r - 0.5 + 0.01:
                        if 0 <= y < len(mask) and 0 <= x < len(mask[0]):
                            mask[y][x] = True


# ---------------------------------------------------------------------------
# Preview renderer
# ---------------------------------------------------------------------------
_SHEETS = {}
_DEBUG_COLORS = {"Decor": (255, 230, 0, 120), "WalkInFront": (0, 255, 0, 90),
                 "Collision": (255, 0, 0, 100), "WalkinBehind": (0, 90, 255, 110)}


def _sprite(tok):
    if tok not in _SHEETS:
        kind, n = tok[0], int(tok[1:])
        grid, png = {"f": (FLOOR, FLOOR_PNG), "h": (HOUSE, HOUSE_PNG), "d": (DESERT, DESERT_PNG),
                     "k": (DUNGEON, DUNGEON_PNG), "m": (MEADOW, MEADOW_PNG)}[kind]
        sheet = _SHEETS.setdefault(png, Image.open(png).convert("RGBA"))
        gx, gy = next(k for k, v in grid.items() if v == n)
        _SHEETS[tok] = sheet.crop((gx * 16, gy * 16, gx * 16 + 16, gy * 16 + 16))
    return _SHEETS[tok]


def _mul(a, b):
    return tuple(x * y for x, y in zip(a, b))


def _tinted(img, tint):
    if not tint or tint == (1, 1, 1, 1):
        return img
    r, g, b, a = tint
    px = img.copy()
    px.putdata([(int(p[0] * r), int(p[1] * g), int(p[2] * b), int(p[3] * a)) for p in px.getdata()])
    return px


def render_lit(m, base_png, path):
    """Rough preview of the darkness: ambient light plus every point light (and the player's lantern at the
    spawn). Unity's Light2D falloff differs a little, but it shows where the pools of light fall."""
    from PIL import ImageChops, ImageDraw, ImageOps
    img = Image.open(base_png).convert("RGB")
    gi, gr, gg, gb = m.global_light
    light = Image.new("RGB", img.size, tuple(int(255 * min(1.0, gi * c)) for c in (gr, gg, gb)))

    def add(cx, cy, radius, intensity, colour):
        d = max(2, int(radius * 2 * 16))
        grad = ImageOps.invert(Image.radial_gradient("L").resize((d, d)))
        grad = grad.point(lambda v: int(255 * (v / 255.0) ** 1.4))
        disc = Image.new("L", (d, d), 0)
        ImageDraw.Draw(disc).ellipse((0, 0, d - 1, d - 1), fill=255)
        grad = ImageChops.multiply(grad, disc)
        tint = Image.new("RGB", (d, d), tuple(int(255 * min(1.0, intensity * c)) for c in colour))
        patch = ImageChops.multiply(tint, Image.merge("RGB", (grad, grad, grad)))
        x0, y0 = int((cx + 0.5) * 16 - d / 2), int((cy + 0.5) * 16 - d / 2)
        box = (max(0, x0), max(0, y0), min(light.width, x0 + d), min(light.height, y0 + d))
        if box[2] <= box[0] or box[3] <= box[1]:
            return
        part = patch.crop((box[0] - x0, box[1] - y0, box[2] - x0, box[3] - y0))
        light.paste(ImageChops.add(light.crop(box), part), box[:2])

    for x, y, radius, intensity, r, g, b, _ in m.lights:
        add(x, y, radius, intensity, (r, g, b))
    if m.player_light:
        outer, _, intensity, r, g, b = m.player_light
        add(m.spawn[0], m.spawn[1], outer, intensity, (r, g, b))
    ImageChops.multiply(img, light).save(path)


def _hex_color(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4, 6))


def render(m, path, debug=False):
    bg = tuple(int(c * 255) for c in (m.background or (0.19, 0.30, 0.47))) + (255,)
    img = Image.new("RGBA", (m.w * 16, m.h * 16), bg)
    cache = {}
    for layer in LAYERS:
        layer_tint = m.tints.get(layer, (1, 1, 1, 1))
        if layer == "WalkinBehind":
            # player marker at spawn, drawn between Collision and WalkinBehind
            sx, sy = m.spawn
            img.alpha_composite(Image.new("RGBA", (12, 12), (40, 120, 255, 255)), (sx * 16 + 2, sy * 16 + 2))
        for y in range(m.h):
            for x in range(m.w):
                cell = m.layers[layer][y][x]
                if not cell:
                    continue
                tok, _, color = cell.partition("@")
                tint = _mul(layer_tint, _hex_color(color)) if color else layer_tint
                if tint[3] > 0:
                    key = (tok, tint)
                    if key not in cache:
                        cache[key] = _tinted(_sprite(tok), tint)
                    img.alpha_composite(cache[key], (x * 16, y * 16))
                if debug and (layer != "Ground"):
                    img.alpha_composite(Image.new("RGBA", (16, 16), _DEBUG_COLORS[layer]), (x * 16, y * 16))
    img.save(path)
