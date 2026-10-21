"""
Authoring script for the three Lythrum maps (Desert, City, Dungeon).

    python Tools/MapGen/build_maps.py

writes Assets/Maps/Data/*.txt (consumed by the Unity menu
"Lythrum > Build Maps") and PNG previews into Tools/MapGen/preview/.
Needs Pillow (pip install pillow).
"""
import os
import sys

from mapkit import (ROOT, Map, empty_mask, mask_rect, mask_ellipse, mask_path, ktile, _KFLOORS)

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
# a nomad camp to the east, two ring roads and short spurs from the southern spawn.
# ---------------------------------------------------------------------------
def spline(points, per=10):
    """Smooth curve through the waypoints (Catmull-Rom), so trails wind instead of running in straight lines."""
    if len(points) < 3:
        out = []
        (ax, ay), (bx, by) = points[0], points[-1]
        n = max(2, int(max(abs(bx - ax), abs(by - ay)) * 2))
        return [(ax + (bx - ax) * i / n, ay + (by - ay) * i / n) for i in range(n + 1)]
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        steps = max(per, int(max(abs(p2[0] - p1[0]), abs(p2[1] - p1[1])) * 2))
        for k in range(steps):
            t = k / steps
            out.append(tuple(0.5 * ((2 * p1[a]) + (-p0[a] + p2[a]) * t + (2 * p0[a] - 5 * p1[a] + 4 * p2[a] - p3[a]) * t * t
                                    + (-p0[a] + 3 * p1[a] - 3 * p2[a] + p3[a]) * t ** 3) for a in (0, 1)))
    out.append(tuple(points[-1]))
    return out


def stroke(mask, points, r=2.0):
    """Round-brushed trail along a smooth curve: 2*r+1 tiles wide, so r=2 gives roads you can walk side by side on."""
    h, w = len(mask), len(mask[0])
    for px, py in spline(points):
        for y in range(int(py - r) - 1, int(py + r) + 2):
            for x in range(int(px - r) - 1, int(px + r) + 2):
                if 0 <= x < w and 0 <= y < h and (x - px) ** 2 + (y - py) ** 2 <= r * r:
                    mask[y][x] = True


def octagon(mask, x0, y0, x1, y1, cut=3):
    """Rectangle with the corners cut off: the clean outline used for mesas."""
    mask_rect(mask, x0, y0, x1, y1)
    for k in range(cut):
        for j in range(cut - k):
            for cx, cy in ((x0 + j, y0 + k), (x1 - j, y0 + k), (x0 + j, y1 - k), (x1 - j, y1 - k)):
                mask[cy][cx] = False


def build_desert():
    """
    "Sunscorch Ruins" - 96 x 72, no towns, just wilderness and ruins to explore and fight in.

        spawn camp (south) ----- one winding spine ----- the Great Ruin (north-centre)

    A ring of trails loops around the Great Ruin: west through a canyon between two mesas, along
    the north, and back down the east side. Short spurs lead to the Hounds' Shrine (SW mesa) and to
    bug nests in the south. Mesas are raised sandstone plateaus with a solid rim and one staircase.
    Solid props never stand on a trail; walk-over details (shrubs, bones, tumbleweeds) come in clusters.
    """
    W, H = 96, 72
    m = Map("Desert", W, H, seed=19)
    m.fill_base("sand", detail=0.06)

    # ---- trails, clearings and the apron around the Great Ruin ----------------------------------
    road = empty_mask(W, H)
    stroke(road, [(48, 70), (48, 62), (46, 54), (50, 46), (48, 38), (48, 34)], r=2.5)        # spine
    stroke(road, [(46, 54), (38, 55), (30, 56), (26, 52), (22, 46), (20, 38), (20, 28), (22, 18), (30, 10), (40, 8)], r=2.2)  # west ring
    stroke(road, [(40, 8), (48, 7), (56, 8)], r=2.2)                                          # north ring
    stroke(road, [(56, 8), (68, 10), (76, 18), (78, 28), (78, 38), (76, 46), (70, 50), (60, 47), (50, 46)], r=2.2)  # east ring
    stroke(road, [(30, 56), (22, 63), (12, 66)], r=1.9)                                       # to the Hounds' Shrine
    stroke(road, [(48, 62), (58, 62), (68, 61), (80, 58)], r=1.9)                             # south-east spur
    stroke(road, [(48, 64), (40, 65), (34, 64)], r=1.9)                                       # south-west spur
    stroke(road, [(26, 47), (21, 47)], r=1.9)                                                 # canyon mesa stairs (east)
    stroke(road, [(7, 45), (13, 45), (20, 44)], r=1.9)                                        # canyon mesa stairs (west)
    stroke(road, [(69, 47), (69, 50)], r=1.9)                                                 # east mesa stairs
    stroke(road, [(88, 45), (80, 45)], r=1.9)                                                 # far-east mesa stairs
    mask_ellipse(road, 48, 66, 7.5, 4.5)                                                      # spawn camp
    mask_ellipse(road, 12, 66, 6.5, 3.5)                                                      # below the Hounds' Shrine
    mask_ellipse(road, 34, 64, 4.5, 3.2)                                                      # south-west nest
    mask_ellipse(road, 80, 58, 6.5, 4.5)                                                      # south-east nest
    mask_ellipse(road, 48, 22, 14, 13.5)                                                      # apron around the Great Ruin
    # open dunes between the trails
    for cx, cy, rx, ry in [(36, 38, 4.5, 3.5), (62, 38, 4.5, 3.5), (30, 3, 5, 2.5), (66, 3, 5, 2.5), (6, 4, 4, 2.5),
                           (90, 4, 4, 2.5), (60, 66, 5, 3), (20, 4, 3, 2)]:
        mask_ellipse(road, cx, cy, rx, ry)
    pale = m.paint_patch("sand", road, decor=0.015)

    # ---- brick courtyard of the Great Ruin -----------------------------------------------------------
    court = empty_mask(W, H)
    octagon(court, 41, 13, 55, 31, cut=2)
    m.paint_brick(court)

    # ---- mesas: raised sandstone plateaus, each with one staircase on its south side ---------------------
    def mesa(stairs_x, *parts):
        mk = empty_mask(W, H)
        for part in parts:
            octagon(mk, *part, cut=3)
        m.plateau("sand", mk, stairs=(stairs_x, "sand"))

    mesa(6, (3, 6, 11, 38))                                    # canyon, west wall
    mesa(25, (22, 20, 31, 42))                                 # canyon, east wall (shorter)
    mesa(68, (65, 14, 73, 42))                                 # east canyon, west wall
    mesa(87, (83, 10, 93, 40))                                 # east canyon, east wall
    mesa(11, (4, 47, 19, 60))                                  # the Hounds' Shrine

    reserve_mask(m, pale)

    # ---- the Great Ruin ----------------------------------------------------------------------------------
    m.wall_rect("sandstone", 38, 10, 58, 34, gaps=[("bottom", 46, 50), ("top", 46, 50)])
    for y in range(20, 25):                      # side gates
        m.clear("Collision", 38, y)
        m.clear("Collision", 58, y)
    for x, y in [(41, 10), (53, 10), (38, 14), (58, 29), (44, 34), (54, 34)]:    # a few chunks of wall have fallen
        m.clear("Collision", x, y)
        m.clear("Collision", x + 1, y)
    for name, x, y in [("stone_arch", 47, 12), ("d_torch_red", 46, 15), ("d_torch_red", 50, 15),
                       ("s_colossus", 43, 12), ("s_colossus", 52, 12),
                       ("s_orb_shrine", 47, 20),
                       ("d_pillar", 42, 16), ("d_pillar", 54, 16), ("d_pillar", 42, 22), ("d_pillar", 54, 22),
                       ("d_pillar", 42, 27), ("d_pillar", 54, 27),
                       ("d_statue", 42, 31), ("d_statue", 55, 31),
                       ("d_torch_red", 45, 21), ("d_torch_red", 51, 21), ("d_torch_blue", 45, 29), ("d_torch_blue", 51, 29),
                       ("d_pit", 40, 12), ("d_pit_deep", 57, 12), ("d_pot_brown", 40, 31), ("d_pot_gold", 57, 31),
                       ("d_bones", 41, 25), ("d_bones", 56, 18), ("d_pot_small", 40, 23)]:
        m.stamp(name, x, y, force=True)

    # ---- spawn camp ---------------------------------------------------------------------------------------
    for name, x, y in [("campfire", 48, 66), ("logs", 45, 67), ("logs", 51, 65), ("tent", 40, 64), ("tent", 54, 63),
                       ("barrel", 44, 62), ("barrel_shut", 53, 67), ("sack_brown", 43, 68), ("d_torch_red", 47, 62),
                       ("d_torch_red", 49, 62), ("d_sign", 50, 61), ("d_pot_brown", 56, 67), ("d_bones", 46, 69),
                       ("stone_arch", 47, 69)]:                                    # the south gate, back to the town
        m.stamp(name, x, y, force=True)

    # ---- Hounds' Shrine (south-west mesa) ---------------------------------------------------------------------
    for name, x, y in [("s_orb_shrine", 11, 50), ("s_hound", 8, 54), ("s_hound", 15, 54), ("s_pillar", 6, 49), ("s_pillar", 17, 49),
                       ("s_pillar", 6, 54), ("s_pillar", 17, 54), ("d_torch_blue", 10, 53), ("d_torch_blue", 13, 53),
                       ("d_bones", 12, 57)]:
        m.stamp(name, x, y, force=True)

    # ---- mesa tops: statues and scrub to find when you climb up ---------------------------------------------------
    for name, x, y in [("s_colossus", 6, 22), ("d_cactus", 5, 12), ("d_cactus", 9, 16), ("d_bones", 7, 30), ("d_cactus", 5, 34),
                       ("d_statue", 5, 25), ("d_pot_gold", 9, 36),                                     # west wall
                       ("s_monk", 25, 26), ("d_cactus", 24, 18), ("d_cactus", 28, 32), ("d_bones", 26, 38), ("d_pot_brown", 28, 22),   # east wall
                       ("s_knight", 68, 26), ("d_cactus", 67, 18), ("d_cactus", 71, 34), ("d_bones", 69, 38), ("d_pot_brown", 70, 22),
                       ("d_statue", 87, 24), ("s_colossus", 87, 14), ("d_cactus", 86, 32), ("d_cactus", 90, 20), ("d_pot_gold", 88, 36)]:
        m.stamp(name, x, y, force=True)

    # ---- landmarks beside the trails (never on them) --------------------------------------------------------------
    for name, x, y in [("s_pillar", 15, 30), ("s_pillar", 15, 22), ("s_colossus", 14, 14), ("s_hound", 17, 40),
                       ("s_pillar", 81, 30), ("s_pillar", 81, 22), ("s_colossus", 77, 12), ("s_hound", 80, 40),
                       ("s_colossus", 29, 3), ("s_colossus", 65, 3), ("d_statue", 44, 3), ("d_statue", 51, 3),
                       ("s_toad", 34, 40), ("s_knight", 60, 40), ("s_monk", 58, 56), ("s_toad", 36, 58)]:
        m.stamp(name, x, y, force=True)

    # ---- bug nests: bones, pots and pits in the open sand ----------------------------------------------------------
    for name, x, y in [("d_pit", 32, 63), ("d_pit_deep", 36, 66), ("d_bones", 34, 64), ("d_bones", 31, 66), ("d_pot_small", 37, 63),
                       ("d_pit", 78, 56), ("d_pit_deep", 82, 59), ("d_bones", 80, 58), ("d_bones", 77, 60), ("d_pot_brown", 83, 56),
                       ("d_pit", 10, 67), ("d_bones", 14, 66)]:
        m.stamp(name, x, y, force=True)

    # ---- rock outcrops (solid) well away from the trails -------------------------------------------------------------
    for cx, cy in [(34, 24), (62, 24), (34, 46), (60, 56), (12, 20), (86, 50), (24, 60), (44, 44), (70, 70), (88, 68), (4, 66), (30, 34)]:
        for name, dx, dy in [("s_block", 0, 0), ("s_stub", 1, 0), ("s_block", 0, 2), ("s_block", -1, 1)]:
            if m.fits(name, cx + dx, cy + dy, 1):
                m.stamp(name, cx + dx, cy + dy)

    # ---- desert life: cacti, tumbleweeds, bones, shrubs - in natural clusters -------------------------------------------
    cactus = ["d_cactus", "d_cactus", "d_cactus_small", "d_tumbleweed", "s_shrub"]
    for cx, cy, r, n in [(36, 30, 5, 5), (60, 30, 5, 5), (30, 44, 6, 6), (66, 54, 6, 6), (44, 56, 4, 4), (54, 54, 4, 4),
                         (14, 32, 4, 4), (82, 30, 4, 4), (16, 8, 4, 4), (80, 6, 4, 4), (40, 68, 5, 4), (60, 68, 5, 4),
                         (28, 68, 4, 3), (70, 66, 4, 3), (90, 56, 5, 4), (6, 56, 3, 3), (92, 28, 3, 3), (2, 40, 3, 3),
                         (46, 42, 3, 3), (36, 14, 4, 3), (60, 14, 4, 3), (52, 40, 3, 3)]:
        m.decor_cluster(cactus, cx, cy, r, n, avoid=pale)
    for cx, cy, r, n in [(24, 32, 4, 4), (73, 46, 3, 3), (50, 58, 4, 3), (44, 49, 3, 3), (56, 49, 3, 3), (12, 38, 3, 3),
                         (86, 44, 3, 3), (30, 18, 3, 3), (68, 20, 3, 3), (48, 3, 4, 3), (34, 52, 4, 3), (64, 62, 4, 3)]:
        m.decor_cluster(["d_bones", "d_tumbleweed", "pebble", "pebble"], cx, cy, r, n, avoid=pale)
    m.scatter(["s_shrub", "pebble", "d_tumbleweed"], 40, (0, 0, W - 1, H - 1), allowed=[[not c for c in row] for row in pale], pad=1)

    m.shade_ground(lo=0.88, cell=11, seed=5)
    m.spawn = (48, 68)
    # the arch in the temple court leads down into the dungeon; coming back up you stand in front of it
    m.portals.append(("ToDungeon", 48, 14, 1, 1, "Dungeon", "FromDesert"))
    m.entries.append(("FromDungeon", 48, 17))
    # the arch behind the camp (south edge) leads back to the town: walk south under the top of the arch into its gap
    m.portals.append(("ToCity", 48, 71, 1, 1, "City", "FromDesert"))
    m.entries.append(("FromCity", 48, 68))

    # ---- self-check: every important spot must be reachable on foot -----------------------------------------------------
    walk = m.reachable(m.spawn)
    goals = {"camp fire": (47, 68), "ruin south gate": (48, 33), "ruin court": (48, 26), "ruin temple front": (48, 17),
             "ruin west gate": (36, 22), "dungeon gate front": (48, 15), "west canyon": (19, 30), "north ring": (48, 8), "east canyon": (78, 30),
             "hounds shrine top": (11, 52), "west mesa top": (7, 20), "canyon mesa top": (26, 30), "east mesa top": (69, 30),
             "far-east mesa top": (88, 28), "SW nest": (34, 62), "SE nest": (80, 60), "spur end": (79, 58),
             "town gate front": (48, 68), "town gate": (48, 71)}
    bad = [name for name, p in goals.items() if p not in walk]
    if bad:
        print("WARNING: Desert - unreachable:", ", ".join(bad))
    open_cells = [(x, y) for y in range(H) for x in range(W) if not m.layers["Collision"][y][x]]
    cut_off = [c for c in open_cells if c not in walk]
    print("Desert: %d/%d walkable cells reachable from the spawn%s" % (len(walk), len(open_cells),
          " - cut off: %s" % cut_off[:12] if cut_off else ""))
    return m


# ---------------------------------------------------------------------------
# CITY - "Lythrum Capital": a walled kingdom with a castle in the north-centre and
# a wide meadow (lake, farm, groves, slimes) around it.
# ---------------------------------------------------------------------------
def dilate(mask, r):
    """Cells within r tiles (square) of a True cell."""
    h, w = len(mask), len(mask[0])
    out = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if mask[y][x]:
                for yy in range(max(0, y - r), min(h, y + r + 1)):
                    for xx in range(max(0, x - r), min(w, x + r + 1)):
                        out[yy][xx] = True
    return out


def build_city():
    """
    "Lythrum Capital" - 128 x 80.

        north centre : the walled kingdom - castle keep with two turrets, four towers, two gatehouses, a wide gate
        inside       : castle plaza, two cross streets, lanes, a market, houses; quarters for the guild and the guard
        south        : a big meadow with a lake, a pond, a farm, groves, flower fields and a camp - the slimes live here

    The town is safe (the map defines an enemy zone covering only the meadow, see "enemy_zone").
    """
    W, H = 128, 80
    m = Map("City", W, H, seed=41)
    m.fill_base("forest", detail=0.08)                       # meadow green everywhere

    town = empty_mask(W, H)
    mask_rect(town, 31, 8, 96, 34)
    m.fill_base("grass", detail=0.10, cells=town)            # the town's own lighter grass

    # ---- streets inside the walls (terracotta) ----------------------------------------------------------------------
    streets = empty_mask(W, H)
    mask_rect(streets, 60, 18, 67, 38)                       # avenue from the plaza through the gate
    mask_rect(streets, 46, 18, 81, 20)                       # castle plaza
    mask_rect(streets, 33, 24, 94, 26)                       # street 1
    mask_rect(streets, 33, 30, 94, 32)                       # street 2
    mask_rect(streets, 44, 21, 46, 34)                       # west lane
    mask_rect(streets, 81, 21, 83, 34)                       # east lane
    m.paint_patch("grass", streets, decor=0.01)

    # ---- meadow trails (dirt) and clearings ---------------------------------------------------------------------------
    trails = empty_mask(W, H)
    stroke(trails, [(64, 39), (64, 48), (59, 58), (64, 68), (64, 78)], r=2.5)                 # main road south
    stroke(trails, [(62, 54), (46, 58), (32, 66), (20, 72), (10, 74)], r=2.0)                 # to the farm
    stroke(trails, [(66, 56), (80, 54), (94, 56), (101, 60)], r=2.0)                          # to the lake
    stroke(trails, [(62, 68), (44, 73), (28, 72)], r=1.9)                                     # south-west spur
    stroke(trails, [(66, 68), (84, 73), (100, 70)], r=1.9)                                    # south-east spur
    stroke(trails, [(32, 66), (24, 54), (18, 44), (15, 32), (16, 20), (22, 10)], r=1.9)       # west margin, up to the hunters' camp
    stroke(trails, [(94, 56), (104, 46), (110, 34), (111, 22), (106, 12)], r=1.9)             # east margin, up to the old arch
    mask_ellipse(trails, 16, 24, 6.5, 4.5)                                                    # hunters' camp
    mask_ellipse(trails, 108, 16, 6.5, 4.5)                                                   # old arch clearing
    mask_ellipse(trails, 100, 71, 5.5, 3.5)                                                   # stone circle
    mask_ellipse(trails, 28, 71, 4.5, 3)                                                      # shrine of the field
    m.paint_patch("forest", trails, decor=0.02)

    # ---- water: a lake in the east, a pond in the west -----------------------------------------------------------------------
    water = empty_mask(W, H)
    mask_ellipse(water, 117, 60, 8.5, 8)
    mask_ellipse(water, 111, 66, 6, 5)
    mask_ellipse(water, 6, 42, 5.5, 3.8)
    water = m.paint_water(water)

    # ---- the kingdom: walls, towers, gatehouses, castle ------------------------------------------------------------------------
    B = m.castle_block
    B(28, 4, 43, 7)                       # north wall (the castle itself closes the middle)
    B(84, 4, 99, 7)
    B(28, 8, 30, 34)                      # west wall
    B(97, 8, 99, 34)                      # east wall
    B(34, 35, 53, 38)                     # south wall, west of the gate
    B(74, 35, 93, 38)                     # south wall, east of the gate
    for x0, y0 in [(28, 4), (92, 4), (28, 31), (92, 31)]:
        B(x0, y0, x0 + 7, y0 + 7)         # corner towers
    B(54, 31, 60, 38)                     # gatehouses either side of the 6-wide gate
    B(67, 31, 73, 38)
    B(51, 4, 76, 6)                       # castle: back wall, then the keep one step lower, between two tall turrets
    B(51, 7, 76, 17)
    B(44, 3, 50, 17)
    B(77, 3, 83, 17)

    S = lambda name, x, y: m.stamp(name, x, y, force=True)
    S("m_castle_door", 62, 15)
    for name, x, y in [("m_banner_red", 54, 11), ("m_banner_red", 73, 11), ("m_banner_blue", 58, 11), ("m_banner_blue", 69, 11),
                       ("m_banner_yellow", 46, 9), ("m_banner_yellow", 81, 9), ("m_banner_red", 62, 12), ("m_banner_red", 65, 12), ("m_fountain", 59, 14), ("m_fountain", 68, 14),
                       ("m_banner_red", 57, 33), ("m_banner_red", 70, 33), ("m_banner_green", 31, 34), ("m_banner_green", 96, 34),
                       ("m_banner_blue", 31, 8), ("m_banner_blue", 96, 8), ("m_banner_red", 40, 36), ("m_banner_red", 87, 36),
                       ("m_banner_blue", 46, 36), ("m_banner_blue", 81, 36)]:
        S(name, x, y)
    for x, y in [(59, 40), (68, 40), (59, 34), (68, 34)]:
        S("lantern", x, y)
    for i, (x, y) in enumerate([(53, 9), (57, 9), (61, 9), (66, 9), (70, 9), (74, 9), (52, 13), (56, 13), (72, 13), (76, 13),
                                (46, 6), (46, 12), (80, 6), (80, 12), (63, 5), (66, 5)]):
        S("m_window_%d" % (1 + i % 2), x, y)

    # ---- inside: houses on both sides of the two streets (doors face the street) ------------------------------------------------
    row1 = [("house_orange", 32), ("house_beige", 36), ("house_orange2", 40), ("house_red", 47), ("shop_fish", 52), ("house_green", 56),
            ("house_orange", 68), ("house_sand", 72), ("house_orange2", 76), ("house_beige", 84), ("house_red", 88), ("house_orange", 92)]
    for name, x in row1:
        S(name, x, 21)
    row2 = [("house_green", 32), ("house_orange2", 36), ("house_beige", 40), ("house_sand", 68), ("house_red", 72), ("house_orange", 76),
            ("house_orange2", 84), ("house_beige", 88), ("house_green", 92)]
    for name, x in row2:
        S(name, x, 27)
    # market (west of the avenue, between the two streets)
    for name, x, y in [("stall_orange", 48, 27), ("stall_cream", 52, 27), ("sack_red", 48, 29), ("sack_cream", 49, 29), ("crate_fruit", 52, 29),
                       ("crate_bread", 53, 29), ("barrel", 56, 29), ("sign_fruit", 57, 28), ("pot", 47, 29), ("bucket_water", 58, 29)]:
        S(name, x, y)
    # castle plaza: the crystal monument and banners
    S("s_orb_shrine", 63, 19)
    for x in (48, 55, 70, 77):
        S("banner_crimson", x, 18)
    # guild quarter (north-west) and guard quarter (north-east)
    S("guild_hall", 36, 11)
    for name, x, y in [("barrel", 33, 10), ("barrel_open", 34, 10), ("crate_bin", 42, 12), ("sack_brown", 42, 13), ("banner_violet", 34, 15), ("banner_violet", 41, 15),
                       ("m_oak", 31, 16), ("m_bush_1", 33, 18), ("m_bush_3", 41, 18), ("m_flower_red", 32, 19), ("m_flower_white", 43, 19)]:
        S(name, x, y)
    for name, x, y in [("tent", 86, 10), ("tent", 91, 10), ("weapon_rack", 86, 14), ("weapon_rack", 91, 14), ("campfire", 89, 15), ("logs", 87, 16),
                       ("barrel", 94, 13), ("barrel_shut", 94, 14), ("m_oak", 93, 16), ("m_bush_2", 85, 18), ("lantern", 84, 12)]:
        S(name, x, y)
    # planters and lanterns along the inner side of the south wall
    for x in (35, 39, 43, 48, 52, 56, 71, 75, 79, 86, 90, 94):
        S("m_flower_" + ["red", "yellow", "white", "pink", "blue"][x % 5], x, 33)
    for x in (37, 49, 73, 88):
        S("lantern", x, 34)

    # ---- the meadow: farm, camp, ruins ----------------------------------------------------------------------------------------------
    m.wall_rect("palisade", 4, 59, 19, 70, gaps=[("bottom", 9, 12)])                         # the farm's fence
    soil = empty_mask(W, H)
    mask_rect(soil, 7, 62, 16, 68)
    m.paint_patch("forest", soil, decor=0.0)
    for name, x, y in [("straw_hut", 6, 60), ("bucket_water", 13, 61), ("logs", 15, 61), ("barrel", 17, 61), ("sack_brown", 6, 67), ("sack_cream", 7, 67),
                       ("planks", 13, 67), ("m_tuft", 9, 64), ("m_tuft", 11, 64), ("m_tuft", 13, 64), ("m_tuft", 9, 66), ("m_tuft", 11, 66), ("m_tuft", 13, 66)]:
        S(name, x, y)
    for name, x, y in [("tent", 12, 22), ("tent", 19, 22), ("campfire", 16, 25), ("logs", 13, 26), ("logs", 18, 27), ("barrel", 21, 24), ("weapon_rack", 10, 25),
                       ("m_rock_2", 22, 27), ("lantern", 16, 21)]:
        S(name, x, y)
    S("stone_arch", 107, 11)                                                                   # an old arch in the east
    for name, x, y in [("s_colossus", 103, 12), ("s_colossus", 112, 12), ("s_pillar", 105, 16), ("s_pillar", 110, 16), ("s_stub", 104, 19), ("s_block", 112, 18)]:
        S(name, x, y)
    for name, x, y in [("m_rock_1", 97, 68), ("m_rock_1", 103, 68), ("m_rock_1", 97, 73), ("m_rock_1", 103, 73), ("s_orb_shrine", 100, 70),
                       ("m_rock_2", 100, 66), ("m_rock_3", 95, 70), ("m_rock_3", 106, 71)]:
        S(name, x, y)
    for name, x, y in [("s_monk", 27, 69), ("lantern_low", 25, 72), ("lantern_low", 31, 72), ("m_flower_white", 26, 74), ("m_flower_white", 29, 74)]:
        S(name, x, y)

    # ---- trees, bushes, rocks, flowers: never on a street, trail or in the water ------------------------------------------------
    walkable = [[streets[y][x] or trails[y][x] for x in range(W)] for y in range(H)]
    for rx0, ry0, rx1, ry1 in [(3, 58, 21, 72), (8, 19, 25, 30), (101, 8, 114, 22), (94, 66, 108, 76), (22, 66, 34, 77)]:
        mask_rect(walkable, rx0, ry0, rx1, ry1)                  # farm, hunters' camp, old arch, stone circle, field shrine
    near_walk = dilate(walkable, 2)
    in_town = empty_mask(W, H)
    mask_rect(in_town, 26, 2, 101, 40)
    free_meadow = [[not near_walk[y][x] and not in_town[y][x] and not water[y][x] for x in range(W)] for y in range(H)]
    outside_trails = [[not near_walk[y][x] and not water[y][x] for x in range(W)] for y in range(H)]

    # a ring of tall trees along the map edge so the world doesn't just stop
    for x in range(0, W, 4):
        for y in (0, 76):
            name = "m_pine" if (x // 4) % 3 else "m_oak"
            if m.fits(name, x, y, 0) and free_meadow[min(H - 1, y + 2)][x]:
                m.stamp(name, x, y)
    for y in range(4, H - 4, 4):
        for x in (0, 124):
            name = "m_pine" if (y // 4) % 3 else "m_oak"
            if m.fits(name, x, y, 0) and free_meadow[y + 2][x]:
                m.stamp(name, x, y)
    # groves in the west and east margins and the far south corners
    m.scatter(["m_oak", "m_pine", "m_pine"], 70, (0, 2, 26, 56), allowed=free_meadow, pad=0)
    m.scatter(["m_oak", "m_pine", "m_pine"], 70, (102, 2, 126, 44), allowed=free_meadow, pad=0)
    m.scatter(["m_oak", "m_pine"], 22, (0, 56, 26, 78), allowed=free_meadow, pad=0)
    m.scatter(["m_oak", "m_pine"], 22, (102, 74, 126, 78), allowed=free_meadow, pad=0)
    # open meadow: lone oaks, bushes, rocks
    m.scatter(["m_oak", "m_oak", "m_pine"], 36, (26, 40, 102, 78), allowed=free_meadow, pad=1)
    m.scatter(["m_bush_1", "m_bush_2", "m_bush_3"], 70, (0, 40, 127, 79), allowed=outside_trails, pad=0)
    m.scatter(["m_bush_1", "m_bush_2", "m_bush_3"], 26, (0, 2, 127, 38), allowed=free_meadow, pad=0)
    m.scatter(["m_rock_1", "m_rock_2", "m_rock_3"], 34, (0, 2, 127, 79), allowed=free_meadow, pad=1)
    # flower fields, tufts and mushrooms in natural clusters
    flowers = ["m_flower_red", "m_flower_yellow", "m_flower_white", "m_flower_blue", "m_flower_pink"]
    field_centres = [(50, 46), (76, 46), (40, 62), (88, 64), (52, 72), (76, 74), (30, 48), (96, 50), (46, 66), (82, 60), (36, 74),
                     (60, 62), (70, 62), (20, 60), (110, 40), (14, 40), (118, 28), (8, 50), (92, 74), (26, 62)]
    for i, (cx, cy) in enumerate(field_centres):
        pick = [flowers[(i + k) % 5] for k in range(3)]
        m.decor_cluster(pick + ["m_tuft"], cx, cy, 6, 26, avoid=None)
    m.decor_cluster(["m_tuft", "m_clover", "m_tuft"], 64, 60, 18, 40)
    m.decor_cluster(["m_mushroom", "m_tuft"], 12, 12, 8, 8)
    m.decor_cluster(["m_mushroom", "m_tuft"], 118, 8, 8, 8)
    m.decor_cluster(["m_mushroom", "m_tuft"], 8, 30, 6, 6)
    m.scatter(["m_tuft", "m_clover", "m_flower_white", "m_flower_yellow"], 160, (0, 39, 127, 79), allowed=[[not w for w in row] for row in walkable], pad=0)
    m.scatter(["m_tuft", "m_flower_pink", "m_flower_blue"], 60, (0, 2, 127, 38), allowed=free_meadow, pad=0)
    # town garden touches
    m.scatter(["m_flower_red", "m_flower_yellow", "m_flower_white", "m_tuft"], 36, (32, 9, 95, 34), allowed=[[not streets[y][x] for x in range(W)] for y in range(H)], pad=0)

    m.shade_ground(lo=0.92, cell=13, seed=9)
    m.spawn = (63, 29)                                          # inside the walls, on the avenue between the two cross streets
    m.enemy_zones.append((0, 41, 127, 79))                      # slimes only live in the meadow
    # the old arch in the east is the gate to the desert: walk north into its gap; coming back you stand in front of it
    m.portals.append(("ToDesert", 108, 13, 1, 1, "Desert", "FromCity"))
    m.entries.append(("FromDesert", 108, 16))

    # ---- self-check -----------------------------------------------------------------------------------------------------------------
    walk = m.reachable(m.spawn)
    goals = {"spawn": (63, 29), "meadow road": (64, 76), "desert gate": (108, 13), "city gate": (64, 36), "plaza": (50, 19), "castle door front": (63, 18), "street 1": (40, 25), "market": (50, 31),
             "guild yard": (38, 19), "guard yard": (90, 19), "farm gate": (10, 71), "farm field": (11, 63), "hunters camp": (16, 28), "old arch": (108, 20),
             "stone circle": (100, 72), "lake shore": (101, 60), "field shrine": (28, 74)}
    bad = [name for name, p in goals.items() if p not in walk]
    if bad:
        print("WARNING: City - unreachable:", ", ".join(bad))
    open_cells = [(x, y) for y in range(H) for x in range(W) if not m.layers["Collision"][y][x]]
    cut_off = [c for c in open_cells if c not in walk]
    print("City: %d/%d walkable cells reachable from the spawn%s" % (len(walk), len(open_cells), " - cut off: %s" % cut_off[:10] if cut_off else ""))
    return m


# ---------------------------------------------------------------------------
# DUNGEON - "The Hollow Crypt": nine chambers around a great hall, joined into a loop, almost no light.
# The scene is nearly black; the player carries a small lantern and the braziers, fountains and
# shrines glow in their own colours. Walls, floor, banners, fountains and the vault door come from
# the 0x72 DungeonTileset II pack; statues, pillars and furniture from the town tileset.
# ---------------------------------------------------------------------------
TORCH = (1.0, 0.62, 0.28)
COLD = (0.40, 0.60, 1.0)
EERIE = (0.45, 1.0, 0.55)
BLOOD = (1.0, 0.18, 0.15)
ARCANE = (0.70, 0.40, 1.0)


def build_dungeon():
    W, H = 96, 72
    m = Map("Dungeon", W, H, seed=31)
    m.background = (0.0, 0.0, 0.0)
    m.global_light = (0.10, 0.45, 0.50, 0.90)           # almost black, a hint of cold blue
    m.player_light = (7.5, 1.4, 1.15, 1.0, 0.88, 0.68)   # the player's lantern: outer, inner, intensity, colour

    # ---- chambers (x0, y0, x1, y1, corner cut) and the passages between them -----------------------------
    rooms = {
        "entrance": (38, 58, 57, 68, 0), "hub": (26, 30, 69, 47, 3), "throne": (30, 4, 65, 19, 3),
        "library": (4, 6, 20, 16, 0), "prison": (4, 27, 15, 46, 0), "storage": (4, 52, 28, 67, 0),
        "crypt": (78, 27, 92, 46, 0), "shrine": (73, 6, 92, 16, 2), "ritual": (66, 52, 92, 67, 0),
    }
    halls = [
        (46, 49, 49, 57),    # entrance <-> hub
        (46, 20, 49, 29),    # hub <-> throne
        (16, 36, 25, 39),    # hub <-> prison
        (8, 17, 11, 26),     # prison <-> library
        (21, 8, 29, 11),     # library <-> throne
        (8, 47, 11, 51),     # prison <-> storage
        (29, 61, 37, 64),    # storage <-> entrance
        (70, 36, 77, 39),    # hub <-> crypt
        (83, 17, 86, 26),    # crypt <-> shrine
        (66, 9, 72, 12),     # throne <-> shrine
        (83, 47, 86, 51),    # crypt <-> ritual
        (58, 61, 65, 64),    # entrance <-> ritual
    ]

    floor = empty_mask(W, H)
    for x0, y0, x1, y1, cut in rooms.values():
        octagon(floor, x0, y0, x1, y1, cut)
    for r in halls:
        mask_rect(floor, *r)
    m.fill_dungeon_floor(floor)

    # a brick wall on the north side of every floor cell that has void above it
    for y in range(H):
        x = 0
        while x < W:
            if floor[y][x] and y > 3 and not floor[y - 1][x]:
                x0 = x
                while x < W and floor[y][x] and y > 3 and not floor[y - 1][x]:
                    x += 1
                m.dungeon_wall(x0, x - 1, y)
            else:
                x += 1
    # invisible blockers along the other edges, and nothing is ever scattered into the void
    for y in range(H):
        for x in range(W):
            if floor[y][x]:
                continue
            if any(0 <= x + dx < W and 0 <= y + dy < H and floor[y + dy][x + dx]
                   for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                m.block(x, y)
            m.busy[y][x] = True
    for x0, y0, x1, y1 in halls:                          # keep the passages clear of props
        m.reserve_rect(x0 - 1, y0 - 1, x1 + 1, y1 + 1)

    S = lambda name, x, y: m.stamp(name, x, y, force=True)

    def light(x, y, radius, intensity, colour, flicker=0.18):
        m.lights.append((x, y, radius, intensity, colour[0], colour[1], colour[2], flicker))

    def torch(x, y, colour=TORCH, radius=6.0, intensity=1.0, flicker=0.2):
        S("lantern", x, y)
        light(x, y + 0.5, radius, intensity, colour, flicker)

    # ============================================================================ entrance
    # The way back to the desert is an arch on the SOUTH edge of the hall, so the path north to the great hall
    # stays open. You walk south, under the top of the arch, into its gap (bottom row, middle tile).
    S("stone_arch", 46, 66)
    m.portals.append(("ToDesert", 47, 68, 1, 1, "Desert", "FromDungeon"))
    m.entries.append(("FromDesert", 47, 64))
    m.spawn = (47, 64)
    light(47, 66.5, 6.5, 1.0, COLD, 0.1)                 # daylight leaking down the stairs
    for x in (41, 53):
        S("k_banner_blue", x, 56)
    for x, y in [(41, 59), (54, 59), (41, 65), (54, 65)]:
        S("g_pillar", x, y)
    torch(44, 67)
    torch(50, 67)
    torch(44, 60)
    torch(50, 60)
    S("g_hound", 43, 63)
    S("g_hound", 52, 63)
    S("k_crate", 39, 59)
    S("k_crate", 56, 59)
    S("k_skull", 48, 61)
    light(47, 61, 5.5, 0.7, TORCH, 0.25)

    # ============================================================================ great hall
    for x in (33, 39, 55, 61):
        for y in (33, 41):
            S("g_pillar", x, y)
    S("g_colossus", 47, 37)
    for x, y in [(44, 38), (51, 38), (44, 44), (51, 44)]:
        torch(x, y, TORCH, 6.5, 1.0, 0.22)
    for x in (33, 38, 56, 61):
        S("k_banner_red", x, 28)
    S("k_fountain_red", 42, 28)
    light(42, 30, 6, 1.1, BLOOD, 0.3)
    S("k_fountain_blue", 53, 28)
    light(53, 30, 6, 1.0, COLD, 0.1)
    for name, x, y in [("g_knight", 30, 43), ("g_knight", 65, 43), ("g_monk", 30, 33), ("g_monk", 64, 33),
                       ("k_skull", 36, 36), ("k_skull", 59, 44), ("g_block", 31, 44), ("g_stub", 64, 45)]:
        S(name, x, y)
    light(47, 33, 5, 0.6, ARCANE, 0.15)

    # ============================================================================ throne room
    S("k_door", 46, 2)                                   # sealed vault door in the north wall
    light(47.5, 5, 6.5, 1.2, BLOOD, 0.35)
    for x in (44, 51):
        torch(x, 6, BLOOD, 6.0, 1.1, 0.3)
    for x in (34, 40, 55, 61):
        for y in (8, 14):
            S("g_pillar", x, y)
    S("g_colossus", 40, 5)
    S("g_colossus", 54, 5)
    for x in (36, 59):
        S("k_banner_red", x, 2)
    S("g_orb_shrine", 46, 11)
    S("g_orb_shrine", 49, 11)
    light(48, 12, 6, 1.0, ARCANE, 0.2)
    for name, x, y in [("k_skull", 38, 16), ("k_skull", 57, 12), ("k_chest", 33, 12), ("k_chest", 62, 12),
                       ("g_knight", 32, 16), ("g_knight", 63, 16), ("campfire", 36, 12), ("campfire", 59, 15)]:
        S(name, x, y)
    light(36, 12, 5, 0.8, TORCH, 0.3)
    light(59, 15, 5, 0.8, TORCH, 0.3)

    # ============================================================================ library / alchemy lab
    for i, x in enumerate(range(6, 19, 2)):               # shelves of potions along the wall
        S(["k_flask_red", "k_flask_blue", "k_flask_green", "k_flask_yellow"][i % 4], x, 4)
    S("k_wall_hole_1", 12, 5)
    S("g_orb_shrine", 11, 9)
    light(12, 10, 6.5, 1.0, ARCANE, 0.15)
    for name, x, y in [("barrel", 5, 7), ("barrel_open", 5, 9), ("crate_bin", 6, 14), ("sack_cream", 18, 8),
                       ("sack_red", 18, 9), ("pot", 17, 14), ("pot_dark", 8, 13), ("bucket_water", 14, 14),
                       ("k_crate", 16, 12), ("k_skull", 9, 8), ("banner_violet", 7, 11), ("banner_violet", 15, 11)]:
        S(name, x, y)
    torch(5, 12, EERIE, 5.5, 0.9, 0.2)
    torch(19, 7, TORCH, 5.5, 0.9, 0.2)

    # ============================================================================ prison
    for x, y in [(4, 30), (10, 30), (4, 38), (10, 38)]:
        S("iron_pen", x, y)
    for name, x, y in [("k_skull", 7, 33), ("k_skull", 12, 41), ("barrel_open", 14, 28), ("pot_dark", 5, 28),
                       ("g_block", 14, 44), ("k_crate", 5, 43), ("sack_brown", 8, 44), ("k_chest", 12, 34)]:
        S(name, x, y)
    S("k_wall_hole_2", 13, 25)
    torch(9, 29, TORCH, 5.5, 0.9, 0.35)
    torch(9, 37, TORCH, 5.5, 0.8, 0.4)
    torch(9, 45, EERIE, 5.0, 0.8, 0.3)

    # ============================================================================ storage
    for name, x, y in [("logs", 5, 54), ("planks", 5, 56), ("firewood", 8, 54), ("barrel", 14, 54), ("barrel_open", 15, 54),
                       ("barrel_shut", 16, 54), ("crate_bin", 18, 57), ("crate_fruit", 5, 62), ("sack_brown", 6, 62),
                       ("pot_dark", 24, 55), ("k_crate", 22, 54), ("k_crate", 23, 54), ("k_chest", 25, 64),
                       ("k_skull", 12, 60), ("g_pillar", 12, 56), ("g_pillar", 20, 60), ("g_pillar", 8, 60),
                       ("g_hound", 26, 58), ("g_block", 15, 64), ("lantern_low", 9, 65)]:
        S(name, x, y)
    S("k_banner_green", 18, 50)
    S("k_goo", 25, 50)
    torch(14, 59, TORCH, 6.0, 1.0, 0.25)
    torch(24, 62, EERIE, 6.0, 0.9, 0.3)
    light(7, 64, 4.5, 0.6, TORCH, 0.3)

    # ============================================================================ crypt
    for name, x, y in [("hut_stone", 80, 31), ("hut_stone", 85, 31), ("hut_stone", 90, 31), ("hut_stone", 80, 38),
                       ("hut_stone", 85, 38), ("hut_stone", 90, 38), ("g_stub", 82, 35), ("g_stub", 88, 35),
                       ("g_monk", 79, 42), ("g_monk", 91, 42), ("g_shrub", 83, 43), ("g_shrub", 87, 43),
                       ("k_skull", 84, 34), ("k_skull", 86, 41), ("g_block", 82, 44), ("g_block", 89, 44)]:
        S(name, x, y)
    S("k_fountain_blue", 85, 25)
    light(85, 27, 5.5, 0.9, COLD, 0.1)
    for x, y in [(82, 29), (88, 29), (82, 45), (88, 45)]:
        torch(x, y, EERIE, 6.0, 0.9, 0.35)

    # ============================================================================ shrine
    S("g_orb_shrine", 82, 10)
    light(83, 11, 7, 1.2, ARCANE, 0.12)
    for x, y in [(77, 8), (89, 8), (77, 13), (89, 13)]:
        S("g_pillar", x, y)
    for x in (76, 80, 86, 90):
        S("k_banner_green", x, 4)
    S("k_fountain_red", 83, 4)
    light(83, 6, 5.5, 1.0, BLOOD, 0.3)
    for name, x, y in [("g_knight", 79, 12), ("g_knight", 87, 12), ("k_skull", 75, 14), ("k_chest", 91, 13),
                       ("g_hound", 81, 14), ("g_hound", 88, 14)]:
        S(name, x, y)

    # ============================================================================ ritual chamber (flooded pit room)
    for x, y in [(75, 57), (79, 57), (83, 57), (75, 62), (79, 62), (83, 62)]:
        S("k_hole", x, y)                                   # the pits in a ring around the shrine
    S("g_orb_shrine", 79, 59)
    light(80, 60, 7, 1.1, BLOOD, 0.4)
    for name, x, y in [("g_toad", 70, 55), ("g_toad", 88, 55), ("g_colossus", 69, 65), ("g_colossus", 89, 65),
                       ("k_skull", 72, 60), ("k_skull", 86, 58), ("g_pillar", 70, 58), ("g_pillar", 90, 58),
                       ("k_crate", 91, 65), ("g_block", 67, 66)]:
        S(name, x, y)
    S("k_banner_red", 72, 50)
    S("k_banner_red", 86, 50)
    torch(72, 54, BLOOD, 6.0, 1.0, 0.35)
    torch(87, 54, BLOOD, 6.0, 1.0, 0.35)

    # ============================================================================ passages: a little light now and then
    for x, y in [(47, 52), (21, 37), (73, 37), (9, 21), (25, 9), (84, 22), (69, 10), (33, 62), (62, 62), (9, 49), (84, 49)]:
        light(x, y, 4.5, 0.55, TORCH, 0.3)
    # loose bones and rubble
    m.decor_cluster(["k_skull", "pebble", "pebble", "g_shrub"], 47, 45, 12, 6)
    m.scatter(["pebble", "pebble", "k_skull"], 40, (0, 0, W - 1, H - 1), pad=1)

    # ---- self-check -------------------------------------------------------------------------------------------------------
    walk = m.reachable(m.spawn)
    goals = {"entrance": (47, 62), "way north": (47, 56), "hub": (36, 38), "throne": (48, 16), "library": (12, 12), "prison": (8, 35),
             "storage": (22, 62), "crypt": (84, 36), "shrine": (84, 14), "ritual": (80, 65), "arch front": (47, 66)}
    bad = [name for name, p in goals.items() if p not in walk]
    if bad:
        print("WARNING: Dungeon - unreachable:", ", ".join(bad))
    open_cells = [(x, y) for y in range(H) for x in range(W) if floor[y][x] and not m.layers["Collision"][y][x]]
    cut_off = [c for c in open_cells if c not in walk]
    print("Dungeon: %d/%d floor cells reachable from the entrance%s" % (len(walk & set(open_cells)), len(open_cells),
          " - cut off: %s" % cut_off[:12] if cut_off else ""))
    return m


BUILDERS = {"Desert": build_desert, "City": build_city, "Dungeon": build_dungeon}


def main(names):
    for name in names or BUILDERS:
        m = BUILDERS[name]()
        print("wrote", m.save(DATA_DIR, PREVIEW_DIR))


if __name__ == "__main__":
    main(sys.argv[1:])
