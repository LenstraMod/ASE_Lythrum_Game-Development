"""
Authoring script for the three Lythrum maps (Desert, City, Dungeon).

    python Tools/MapGen/build_maps.py

writes Assets/Maps/Data/*.txt (consumed by the Unity menu
"Lythrum > Build Maps") and PNG previews into Tools/MapGen/preview/.
Needs Pillow (pip install pillow).
"""
import os
import sys

from mapkit import (ROOT, Map, empty_mask, mask_rect, mask_ellipse, mask_path)

DATA_DIR = os.path.join(ROOT, "Assets", "Maps", "Data")
PREVIEW_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview")


def reserve_mask(m, mask):
    """Keep scattered props off roads/plazas."""
    for y in range(m.h):
        for x in range(m.w):
            if mask[y][x]:
                m.busy[y][x] = True


# ---------------------------------------------------------------------------
# DESERT - "Sunscorch Dunes": a walled bazaar town, sunken ruins to the west,
# a nomad camp to the east, trails winding in from the south.
# ---------------------------------------------------------------------------
def build_desert():
    W, H = 64, 48
    m = Map("Desert", W, H, seed=7)
    m.fill_base("sand", detail=0.08)

    trail = empty_mask(W, H)
    # main trail from the south edge up to the town gate
    mask_path(trail, [(31, 47), (31, 40), (32, 34), (31, 26)], width=4)
    # west branch to the ruins' stairs, east branch to the camp
    mask_rect(trail, 5, 38, 31, 40)
    mask_rect(trail, 32, 37, 49, 39)
    mask_rect(trail, 47, 33, 49, 39)
    # town streets
    mask_rect(trail, 29, 13, 33, 25)          # high street
    mask_rect(trail, 18, 15, 45, 17)          # cross street
    mask_rect(trail, 25, 9, 37, 13)           # temple forecourt
    # camp clearing
    mask_ellipse(trail, 53, 31, 7.5, 5.5)
    # loose dunes
    for cx, cy, rx, ry in [(7, 8, 5, 3), (56, 8, 6, 3.5), (6, 44, 4.5, 2.5), (57, 43, 5, 3),
                           (21, 44, 4, 2.2), (45, 44, 4, 2.5), (54, 19, 3.5, 5), (8, 18, 4, 5)]:
        mask_ellipse(trail, cx, cy, rx, ry)
    trail = m.paint_patch("sand", trail)

    # the ruins stand on a raised sandstone plateau; its rim is a ledge you
    # can only climb by the stairs on the south side
    ruins_mask = empty_mask(W, H)
    mask_ellipse(ruins_mask, 9.5, 30, 7.5, 5.5)
    m.plateau("sand", ruins_mask, stairs=(8, "sand"))

    # --- walled bazaar ---------------------------------------------------
    m.wall_rect("sandstone", 16, 3, 47, 25, gaps=[("bottom", 29, 33)])
    reserve_mask(m, trail)

    # temple and its guardians
    for name, x, y in [("dome_temple", 30, 5), ("s_colossus", 26, 5), ("s_colossus", 35, 5),
                       ("s_pillar", 28, 9), ("s_pillar", 34, 9)]:
        m.stamp(name, x, y, force=True)
    # houses on the north side
    for x in (18, 22, 39, 43):
        m.stamp("house_sand", x, 5, force=True)
    m.stamp("kiln", 18, 10, force=True)
    m.stamp("hut_sand", 44, 10, force=True)
    m.stamp("hut_sand", 41, 10, force=True)
    # market row (south-west quarter)
    m.stamp("stall_orange", 19, 19, force=True)
    m.stamp("stall_cream", 23, 19, force=True)
    for name, x, y in [("sack_red", 19, 22), ("sack_cream", 20, 22), ("sack_green", 21, 22),
                       ("crate_fruit", 23, 22), ("crate_bread", 24, 22), ("barrel", 25, 22),
                       ("pot", 18, 21), ("pot_dark", 27, 19), ("sign_fruit", 27, 21),
                       ("bucket_water", 17, 24), ("barrel_shut", 18, 24)]:
        m.stamp(name, x, y, force=True)
    # tents (south-east quarter)
    m.stamp("tent", 36, 19, force=True)
    m.stamp("tent", 40, 19, force=True)
    for name, x, y in [("weapon_rack", 44, 19), ("barrel_open", 44, 21), ("firewood", 45, 23),
                       ("logs", 43, 23), ("lantern", 35, 22)]:
        m.stamp(name, x, y, force=True)
    # banners lining the high street
    for y in (19, 22):
        m.stamp("banner_red", 28, y, force=True)
        m.stamp("banner_red", 34, y, force=True)
    m.stamp("lantern", 28, 14, force=True)
    m.stamp("lantern", 34, 14, force=True)

    # --- west ruins --------------------------------------------------------
    ruins = [("s_pillar", 5, 27), ("s_pillar", 14, 27), ("s_pillar", 5, 31), ("s_pillar", 14, 31),
             ("s_orb_shrine", 9, 28), ("s_hound", 7, 31), ("s_hound", 12, 31), ("s_stub", 11, 26),
             ("s_block", 6, 34), ("s_block", 13, 34),
             ("s_knight", 18, 28), ("s_toad", 19, 32), ("s_stub", 1, 30)]
    for name, x, y in ruins:
        m.stamp(name, x, y, force=True)

    # --- east nomad camp ---------------------------------------------------
    camp = [("tent", 47, 26), ("tent", 56, 26), ("campfire", 53, 31), ("logs", 51, 33),
            ("logs", 53, 29), ("barrel", 49, 30), ("barrel_shut", 49, 31), ("sack_brown", 58, 31),
            ("sack_cream", 58, 32), ("weapon_rack", 55, 34), ("pot", 51, 29), ("lantern_low", 51, 35)]
    for name, x, y in camp:
        m.stamp(name, x, y, force=True)

    # --- desert outskirts --------------------------------------------------
    m.stamp("straw_hut", 3, 43, force=True)
    m.stamp("straw_hut", 12, 43, force=True)
    m.stamp("s_colossus", 55, 38, force=True)
    m.stamp("s_monk", 40, 42, force=True)
    # walk-over brush and pebbles (Decor) plus a few solid rocks
    m.scatter(["s_shrub", "s_shrub", "pebble"], 60, (0, 0, W - 1, H - 1), pad=1)
    m.scatter(["s_block", "s_stub"], 12, (0, 0, W - 1, H - 1), pad=1)

    m.spawn = (31, 45)
    return m


# ---------------------------------------------------------------------------
# CITY - "Lythrum Capital": a walled town with a central plaza, residential
# quarter, guild district, market square and farmland.
# ---------------------------------------------------------------------------
def build_city():
    W, H = 64, 48
    m = Map("City", W, H, seed=21)
    m.fill_base("grass", detail=0.10)

    roads = empty_mask(W, H)
    mask_rect(roads, 30, 0, 33, H - 1)        # King's Road (north-south, through both gates)
    mask_rect(roads, 2, 22, 61, 25)           # Market Road (east-west)
    mask_rect(roads, 3, 11, 60, 13)           # north lane
    mask_rect(roads, 3, 34, 60, 36)           # south lane
    mask_rect(roads, 14, 3, 16, 45)           # west lane
    mask_rect(roads, 47, 3, 49, 45)           # east lane
    mask_ellipse(roads, 31.5, 23.5, 8.5, 6.5)  # central plaza
    mask_rect(roads, 5, 38, 12, 44)           # market square (south-west)
    mask_rect(roads, 17, 38, 28, 44)
    roads = m.paint_patch("grass", roads)

    # farm soil (south-east) - separate patches inside rail fences
    soil = empty_mask(W, H)
    mask_rect(soil, 52, 39, 59, 44)
    mask_rect(soil, 36, 40, 44, 44)
    m.paint_patch("grass", soil, decor=0.08)

    # the guild hall sits on a raised terrace, reached by stairs from the north lane
    terrace = empty_mask(W, H)
    mask_rect(terrace, 34, 3, 45, 9)
    m.plateau("grass", terrace, stairs=(37, "sand"))

    # city wall with north and south gates
    m.wall_rect("sandstone", 1, 2, 62, 46, gaps=[("top", 30, 33), ("bottom", 30, 33)])
    reserve_mask(m, roads)
    reserve_mask(m, soil)

    S = lambda name, x, y: m.stamp(name, x, y, force=True)

    # --- plaza -------------------------------------------------------------
    S("g_orb_shrine", 31, 22)
    for x, y in [(27, 20), (36, 20), (27, 27), (36, 27)]:
        S("lantern", x, y)
    for x in (25, 38):
        S("banner_teal", x, 18)
        S("banner_teal", x, 28)

    # --- north-west: residential ---------------------------------------------
    S("house_orange", 3, 5)
    S("house_beige", 8, 5)
    S("house_red", 18, 5)
    S("house_green", 24, 5)
    S("house_orange2", 3, 15)
    S("house_beige", 8, 15)
    S("house_orange", 18, 15)
    S("house_red", 24, 15)
    for name, x, y in [("barrel", 12, 6), ("barrel_shut", 12, 7), ("stump", 7, 8), ("bucket_water", 22, 7),
                       ("crate_bin", 28, 7), ("stump", 12, 17), ("barrel_open", 22, 18), ("pot", 28, 17),
                       ("bucket_apples", 7, 19), ("firewood", 28, 19)]:
        S(name, x, y)

    # --- north-east: guild district ------------------------------------------
    S("guild_hall", 36, 4)
    S("house_wood", 41, 6)
    S("shop_fish", 51, 5)
    S("house_green", 56, 5)
    for x in (35, 40):
        S("banner_violet", x, 7)
    # training yard behind a rail fence
    m.wall_rect("rail", 51, 15, 60, 20, gaps=[("bottom", 55, 56)])
    for name, x, y in [("s_knight", 52, 16), ("s_knight", 58, 16), ("weapon_rack", 55, 16),
                       ("logs", 53, 19), ("banner_crimson", 50, 16)]:
        S(name, x, y)
    S("house_orange2", 36, 15)
    S("house_beige", 42, 15)
    S("stump", 41, 19)
    S("barrel", 46, 16)

    # --- south-west: market square -------------------------------------------
    for name, x, y in [("stall_orange", 5, 38), ("stall_cream", 9, 38), ("stall_orange", 18, 38),
                       ("stall_cream", 22, 38), ("stall_orange", 26, 38)]:
        S(name, x, y)
    goods = [("crate_fruit", 5, 41), ("crate_bread", 6, 41), ("sack_red", 9, 41), ("sack_green", 10, 41),
             ("bucket_apples", 18, 41), ("bucket_greens", 19, 41), ("crate_loaves", 22, 41),
             ("barrel", 23, 41), ("sack_cream", 26, 41), ("sack_brown", 27, 41), ("pot", 12, 43),
             ("barrel_shut", 5, 44), ("sign_fruit", 12, 39), ("sign_board", 20, 44), ("crate_bin", 28, 44)]
    for name, x, y in goods:
        S(name, x, y)
    for x, b in [(4, "banner_red"), (17, "banner_green"), (29, "banner_serpent")]:
        S(b, x, 38)
    S("house_orange", 3, 27)
    S("house_green", 8, 27)
    S("shop_fish", 18, 27)
    S("house_red", 22, 27)
    S("barrel", 26, 29)
    S("lantern_low", 13, 30)

    # --- south-east: farms and lumber yard -----------------------------------
    m.wall_rect("rail", 51, 38, 60, 45, gaps=[("top", 55, 56)])
    m.wall_rect("rail", 35, 39, 45, 45, gaps=[("top", 39, 41)])
    S("house_wood", 36, 28)
    S("straw_hut", 41, 29)
    S("house_beige", 52, 27)
    S("hut_sand", 57, 28)
    for name, x, y in [("logs", 36, 32), ("planks", 39, 32), ("firewood", 44, 32), ("bucket_greens", 51, 31),
                       ("bucket_water", 56, 31), ("stump", 59, 31)]:
        S(name, x, y)

    # walk-over shrubs and pebbles on the lawns, a few solid stumps
    m.scatter(["s_shrub", "g_shrub", "pebble"], 30, (2, 3, W - 3, H - 3), pad=1)
    m.scatter(["stump"], 6, (2, 3, W - 3, H - 3), pad=1)

    m.spawn = (31, 44)
    return m


# ---------------------------------------------------------------------------
# DUNGEON - "Forgotten Crypt": stone rooms carved out of darkness. The tileset
# has no dungeon walls, so walls are the brick facade tinted to dark stone.
# ---------------------------------------------------------------------------
DUNGEON_ROOMS = {
    "entrance": (25, 36, 38, 45),
    "great_hall": (20, 19, 43, 29),
    "prison": (3, 20, 16, 32),
    "gallery": (48, 18, 60, 30),
    "throne": (22, 4, 41, 13),
    "treasury": (4, 5, 14, 14),
    "crypt": (48, 5, 59, 12),
    "storage": (4, 39, 17, 45),
    "pit": (44, 38, 58, 45),
}
DUNGEON_HALLS = [
    (30, 46, 33, 47),   # way in (south edge)
    (30, 30, 33, 35),   # entrance -> great hall
    (30, 14, 33, 18),   # great hall -> throne room
    (17, 24, 19, 26),   # great hall -> prison
    (44, 23, 47, 25),   # great hall -> gallery
    (15, 8, 21, 10),    # throne -> treasury
    (52, 13, 54, 17),   # gallery -> crypt
    (8, 33, 10, 38),    # prison -> storage
    (18, 41, 24, 43),   # entrance -> storage
    (39, 40, 43, 42),   # entrance -> pit
]


def build_dungeon():
    W, H = 64, 48
    m = Map("Dungeon", W, H, seed=13)
    m.background = (0.05, 0.04, 0.07)
    props = (0.80, 0.80, 0.90, 1.0)
    m.tints = {"Ground": (0.62, 0.60, 0.70, 1.0), "Decor": props, "WalkInFront": props,
               "Collision": props, "WalkinBehind": props}
    wall_color = "80869AFF"   # x Collision tint -> dark slate brick

    floor = empty_mask(W, H)
    for r in list(DUNGEON_ROOMS.values()) + DUNGEON_HALLS:
        mask_rect(floor, *r)
    m.fill_base("dust", detail=0.10, cells=floor)

    def inner(mask):
        # keep a clean 1-tile margin of plain floor along the room edges
        for y in range(H):
            for x in range(W):
                if any(not (0 <= x + dx < W and 0 <= y + dy < H and floor[y + dy][x + dx])
                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                    mask[y][x] = False
        return mask

    # dark earth / rubble patches inside some rooms (walkable)
    mud = empty_mask(W, H)
    for cx, cy, rx, ry in [(9, 27, 4, 3), (11, 42, 4, 2.2), (54, 26, 3, 2.5),
                           (26, 25, 3, 2.2), (38, 42, 2.5, 2.2), (9, 9, 3, 2.5)]:
        mask_ellipse(mud, cx, cy, rx, ry)
    m.paint_patch("dust", inner(mud), oob_inside=False)

    # raised dais in the throne room, stairs down to the great hall corridor
    dais = empty_mask(W, H)
    mask_rect(dais, 25, 4, 38, 9)
    m.plateau("dust", dais, stairs=(30, "stone"))

    # a sunken pit in the south-east room, crossed by a stone bridge
    pit = empty_mask(W, H)
    mask_ellipse(pit, 51, 41.5, 6, 3)
    pit = m.plateau("dust", inner(pit), solid=True)
    rows = [y for y in range(H) if any(pit[y][x] for x in range(50, 53))]
    m.deck("stone", 50, min(rows) - 1, max(rows) + 1)

    # walls: a 2-high brick face above every floor cell that has void to the north
    for y in range(H):
        x = 0
        while x < W:
            if floor[y][x] and y > 0 and not floor[y - 1][x]:
                x0 = x
                while x < W and floor[y][x] and y > 0 and not floor[y - 1][x]:
                    x += 1
                m.brick_wall(x0, x - 1, y - 1, wall_color)
            else:
                x += 1
    # invisible blockers all around the walkable floor
    for y in range(H):
        for x in range(W):
            if floor[y][x]:
                continue
            if any(0 <= x + dx < W and 0 <= y + dy < H and floor[y + dy][x + dx]
                   for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                m.block(x, y)
    # nothing gets scattered into the void
    for y in range(H):
        for x in range(W):
            if not floor[y][x]:
                m.busy[y][x] = True
    # keep doorways clear
    for x0, y0, x1, y1 in DUNGEON_HALLS:
        m.reserve_rect(x0 - 1, y0 - 1, x1 + 1, y1 + 1)

    S = lambda name, x, y: m.stamp(name, x, y, force=True)

    # --- entrance hall --------------------------------------------------------
    for y in (37, 41):
        S("g_pillar", 27, y)
        S("g_pillar", 36, y)
    S("g_hound", 29, 43)
    S("g_hound", 34, 43)
    S("lantern", 28, 45)
    S("lantern", 35, 45)
    S("g_block", 37, 45)
    S("g_block", 25, 36)

    # --- great hall -----------------------------------------------------------
    for x in (22, 26, 37, 41):
        S("g_pillar", x, 20)
        S("g_pillar", x, 26)
    S("g_colossus", 31, 19)
    for x in (29, 34):
        S("banner_skull", x, 19)
    S("lantern", 28, 23)
    S("lantern", 35, 23)
    S("g_knight", 23, 23)
    S("g_knight", 39, 23)

    # --- prison (west) ----------------------------------------------------------
    S("iron_pen", 4, 20)
    S("iron_pen", 10, 20)
    S("iron_pen", 4, 29)
    for name, x, y in [("pot_dark", 5, 21), ("sack_brown", 11, 21), ("g_block", 15, 29),
                       ("barrel_open", 15, 20), ("lantern_low", 11, 30), ("g_stub", 15, 31)]:
        S(name, x, y)

    # --- statue gallery (east) --------------------------------------------------
    for name, x, y in [("g_monk", 49, 18), ("g_monk", 58, 18), ("g_toad", 49, 22), ("g_toad", 58, 22),
                       ("g_knight", 49, 27), ("g_knight", 58, 27), ("g_orb_shrine", 53, 20),
                       ("lantern", 52, 29), ("lantern", 56, 29), ("g_hound", 53, 28), ("g_hound", 55, 28)]:
        S(name, x, y)

    # --- throne room (north) ----------------------------------------------------
    S("stone_arch", 30, 4)
    S("g_colossus", 26, 5)
    S("g_colossus", 36, 5)
    for x in (23, 40):
        S("g_pillar", x, 4)
        S("g_pillar", x, 11)
    for x in (29, 33):
        S("banner_skull", x, 7)
    S("g_orb_shrine", 22, 8)
    S("g_orb_shrine", 40, 8)
    S("campfire", 28, 11)
    S("campfire", 34, 11)

    # --- treasury (north-west) ----------------------------------------------------
    for name, x, y in [("s_orb_shrine", 8, 5), ("weapon_rack", 5, 5), ("weapon_rack", 12, 5),
                       ("barrel", 4, 8), ("barrel_shut", 4, 9), ("crate_bin", 4, 12), ("crate_loaves", 5, 13),
                       ("sack_red", 13, 12), ("sack_cream", 13, 13), ("pot", 12, 13), ("banner_violet", 7, 12),
                       ("banner_violet", 11, 12)]:
        S(name, x, y)

    # --- crypt (north-east) -------------------------------------------------------
    for name, x, y in [("hut_stone", 49, 5), ("hut_stone", 57, 5), ("g_stub", 52, 6), ("g_stub", 55, 6),
                       ("g_block", 49, 10), ("g_block", 58, 10), ("g_shrub", 51, 9), ("g_shrub", 56, 9),
                       ("pot_dark", 53, 11)]:
        S(name, x, y)

    # --- storage (south-west) -----------------------------------------------------
    for name, x, y in [("logs", 4, 39), ("planks", 4, 40), ("firewood", 6, 39), ("barrel", 13, 39),
                       ("barrel_open", 14, 39), ("barrel_shut", 15, 39), ("crate_bin", 16, 41),
                       ("crate_fruit", 4, 44), ("sack_brown", 5, 44), ("pot_dark", 16, 45)]:
        S(name, x, y)

    # --- flooded pit (south-east) -------------------------------------------------
    for name, x, y in [("g_toad", 44, 38), ("g_toad", 57, 38), ("g_shrub", 45, 45), ("g_shrub", 58, 45),
                       ("g_stub", 44, 43), ("g_block", 58, 41), ("lantern_low", 48, 38), ("lantern_low", 54, 38)]:
        S(name, x, y)
    # rubble and pebbles on the floors (walk-over Decor)
    m.scatter(["pebble", "g_shrub"], 25, (0, 0, W - 1, H - 1), pad=1)

    m.spawn = (31, 44)
    return m


BUILDERS = {"Desert": build_desert, "City": build_city, "Dungeon": build_dungeon}


def main(names):
    for name in names or BUILDERS:
        m = BUILDERS[name]()
        print("wrote", m.save(DATA_DIR, PREVIEW_DIR))


if __name__ == "__main__":
    main(sys.argv[1:])
