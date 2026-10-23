# Map generator

The three maps (Desert, City, Dungeon) are built from `Assets/TileSets/TilesetFloor.png` and `TilesetHouse.png`.

1. `build_maps.py` holds the layouts. `mapkit.py` holds the tile catalogue and helpers (autotiled terrain patches, walls, and multi-tile objects such as houses and statues).
2. `python Tools/MapGen/build_maps.py` writes `Assets/Maps/Data/<Map>.txt` and PNG previews in `Tools/MapGen/preview/`. It needs Pillow (`pip install pillow`).
3. In Unity, choose **Lythrum > Build Maps > All Maps** (or pick one map). This runs `Assets/Editor/LythrumMapBuilder.cs` and creates `Assets/Scenes/Maps/<Map>.unity`.

Each scene has a `Grid` with the same tilemaps as `SettingInput.unity`:

| Tilemap      | Sorting layer | Purpose                                                               |
|--------------|---------------|-----------------------------------------------------------------------|
| Ground       | Ground        | terrain                                                               |
| WalkInFront  | WalkInFront   | walkable structures under the player (stairs, bridges)                |
| Collision    | Collision     | solid: building bodies, object bases, walls, ledges, invisible blockers |
| WalkinBehind | WalkBehind    | roof ridges and the upper part of statues and pillars, drawn over the player |
| Decor        | Decor         | walk-over details (shrubs, pebbles)                                   |

Objects only collide at their base, so the player can step behind the upper part of houses, statues and pillars. For this to work, the builder gives the copied `Player` a feet-only collider. It also moves the `Decor` sorting layer above `Ground`, because below `Ground` nothing on it would be visible.

Raised areas (the desert ruins, the city's guild terrace and the dungeon's throne dais) have a solid rim that you can only cross by the stairs. The dungeon pit is crossed by a bridge.

The builder also copies `Player`, `Main Camera` and `CmCam` from `SettingInput.unity`, then places them at the map's spawn point. It adds a `PlayerSpawn` marker there as well.

In the previews, `<Map>_layers.png` colours each layer: blue is WalkinBehind, red is Collision, green is WalkInFront and yellow is Decor.

Rebuilding a map overwrites its scene. If you hand-edit a scene, save it under a different name first.

## Extra art packs

Two free packs are re-packed onto a clean 16px grid so the map builder can use them like the original tilesets:

| Pack | Used for | Script | Map tokens |
|------|----------|--------|------------|
| "Desert Ruins 16x16" by Max_M42 (https://max-m42.itch.io/desert-ruins-16x16-pixel-art-asset) | Desert: cacti, brick temple floor, pillars, eagle statues, pots, bones, torches, pits | `pack_desert.py` -> `Assets/TileSets/TilesetDesert.png` | `d<n>` |
| "16x16 DungeonTileset II" by 0x72 (https://0x72.itch.io/dungeontileset-ii, CC0) | Dungeon: brick walls, stone floor, banners, fountains, vault door, crates, chests, skulls | `pack_dungeon.py` -> `Assets/TileSets/TilesetDungeon.png` | `k<n>` |

| CC0 forest sample by inkBubi (https://opengameart.org/content/free-sample-16x16-pixel-forest-tileset-%E2%80%93-top-down-rpg-style) + wall pieces, banners and door from 0x72 recoloured to pale castle stone + flowers/mushrooms drawn in code | City: trees, bushes, rocks, water with grass banks, castle walls/towers/keep, castle door, banners, windows, flowers | `pack_meadow.py` -> `Assets/TileSets/TilesetMeadow.png` (+ `_water.json`, `_walls.json`, `_objects.json`) | `m<n>` |

The originals and licences are in `Tools/MapGen/source/`. The Desert Ruins licence allows commercial use and modification, but not reselling or redistributing the files on their own.

In Unity, **Lythrum > Setup > Import Pack Tiles** slices the PNGs and creates the Tile assets (`Assets/TileSets/DesertPalette`, `DungeonPalette`). **Lythrum > Build Maps** does it automatically.

## Lights, gates and entry points in the map data

`mapkit.Map` can write extra lines that `LythrumMapBuilder` turns into scene objects (coordinates are tiles, y down, like the tile layers):

| Line | Result |
|------|--------|
| `global_light <intensity> <r> <g> <b>` | a dim Global Light 2D: makes the scene dark (everything not near a light is nearly black) |
| `player_light <outer> <inner> <intensity> <r> <g> <b>` | a Point Light 2D on the Player (their lantern) |
| `light <x> <y> <radius> <intensity> <r> <g> <b> <flicker>` | a Point Light 2D; `flicker > 0` adds `LightFlicker` (>= 0.3 also dips out now and then) |
| `entry <name> <x> <y>` | a `SceneEntryPoint`: where the player appears when arriving through a gate |
| `enemy_zone <x0> <y0> <x1> <y1>` | an `EnemyZone`: if a scene has any, enemies are only placed inside the zones (the City: slimes only in the meadow, the town stays safe) |
| `portal <name> <x> <y> <w> <h> <scene> <entry>` | a trigger area (top-left tile, size in tiles) that fades out and loads `<scene>`, placing the player at its `<entry>` |

The builder also adds every map scene it builds to the Build Settings, which `SceneManager.LoadScene` needs. In `build_maps.py`, the Desert gate is the stone arch in the temple court and the Dungeon's way back is the arch in its entrance hall. The previews include `<Map>_lit.png`, a rough picture of the darkness for maps that use `global_light`.

`build_desert()` and `build_dungeon()` finish with a walkability check and print a warning if the spawn can't reach an important spot.
