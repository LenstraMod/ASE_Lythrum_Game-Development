# Combat effects and sounds

| Script | Writes | Notes |
|--------|--------|-------|
| `pack_combat_fx.py` | `Assets/Sprites/Effects/slash_pixel.png`, `fire_pixel.png` | The sword slash (tbbk, CC0) and the flame (KerteX_, CC0); originals + licences in `source/`. The flame is drawn at about 7x pixel size, so it is sampled down to real pixels. |
| `make_pixel_sfx.py` | `Assets/Audio/Combat/pixel_*.wav` | 8-bit slash / fire slash / hit sounds, synthesised (square sweeps, noise, bit-crush). No samples, no licence. |

In Unity, **Lythrum > Setup > Audio (build SFX library)** (also part of *Setup > Everything*) maps the sounds, builds the music library and the combat effect library (`Assets/Resources/CombatVfxLibrary.asset`).
**Setup > Combat Effects** rebuilds just the effect library.

`PlayerCombat` plays the pixel slash for the normal combo (mirrored on every other hit, warmer on the third) and, for Q, a fiery double slash with flames along the arc. The enemies it hits get a flame that sticks to them. In dark scenes (the dungeon) the fire also glows. If the library is missing, the old procedural arc is used.
