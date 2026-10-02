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
