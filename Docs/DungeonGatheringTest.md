# Dungeon Gathering — isolated map test

## Run

Open `Builds/DungeonGatheringTest/DungeonGatheringTest.exe`. This is a separate
playground: `Builds/Windows/RuinRunnerPrototype.exe`, the meadow demo and save
slots are unchanged. It has no monster waves or stage progression.

- WASD: move; Space: dash; left click: existing sword attack.
- E near a vase: remove it; walk over the four animated coins to collect them.
- Tab: toggle the layout map; Esc: close the map, or exit the test.
- The test starts fresh on every launch and writes no run/save-slot data.

## Contents

A stair entrance connects through a passage to the cistern hall, a side alcove,
and a raised-looking sanctum. Walls are generated from the union of the floor
areas, so modules share continuous openings. Water and masonry block movement
and dashes. Torches, water ripples, coins and vases animate using original pack
frames. The hero and sword are reused from the current demo, not from this pack.

## Edit / rebuild

- Scene: `Assets/RuinRunner/Scenes/DungeonGatheringTest.unity`.
- Layout: `Assets/RuinRunner/Resources/Maps/dungeon_gathering_test.json`.
- Editor menu: `Prototype > Dungeon Gathering` (open scene, bake, build).
- Runtime images: `Assets/RuinRunner/Resources/Art/Pixel/Separated/DungeonGathering`.
- 77 single-sprite PNG files; 16×16, 16 PPU, point filtering, no compression.
- Original tile palettes/alpha are preserved. No runtime sheet slicing and no
  generated art. The bottom water lip is a whole-tile vertical flip of the top.

The standalone build entry is `RuinRunner.EditorTools.DungeonGatheringTestBuild.Build`.
Its output never targets the normal demo's build folder.

## Validation

Launch the **visible** executable with `--dungeon-gathering-validation
--automated-stage-validation -screen-fullscreen 0`. It checks sprite imports,
floor connectivity, the actual player route, wall/dash/water collision, all
animated props, vase interaction and the map toggle. Screenshots are written
under the test build's `Validation` directory. This is map validation, not a
full-stage combat test. Source-pixel audit: `python Tools/inspect-dungeon-gathering.py`.

## Asset credit

Dungeon Gathering / Under The Castle by Jose Javier (SnowHex).
User-provided source: `Dungeon Gathering Free Version/`.
The supplied license permits use and modification inside games; it prohibits
redistribution/resale as standalone asset packs. See `Docs/DungeonGatheringLicense.txt`.
