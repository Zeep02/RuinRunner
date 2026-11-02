# Individual runtime art

All active sheet elements are pre-exported PNGs. Runtime code loads a complete file by ID; it never guesses a sheet cell or removes a color background.

- Player/move, Player/attack: direction folders 00–07, frame 00–03. Pivots preserve verified feet/hand alignment.
- Enemies/<type>/<state>, Boss/<state>: intact isolated frames, retaining PPU and feet pivots.
- VFX/Slash/<family>: independent attack-effect frames.
- Terrain, Props, World: individual tiles, props and interaction sprites. Terrain borders use ordinary single-sprite 9-slice metadata, not an image sheet.
- HUD and UI: separate bars, panels, buttons and button states.
- ../Items: 49 independently saved weapon, passive and accessory icons already migrated earlier.

Each PNG's .meta defines its single Sprite, PPU, pivot and border. Keep both files together. Point filtering, no mipmaps, NPOT preservation and no texture compression are enforced. Do not centre animation frames independently: preserve their authored foot pivot.

Editor menu: Prototype > Art > Bake Missing Individual Art and Validate. Existing PNG replacements are never overwritten. Prototype > Art > Validate Individual Art Files detects incompatible dimensions, pivots, borders or missing files. Update metadata and the manifest deliberately when changing canvas dimensions.

Source sheets have been moved, with GUIDs preserved, to Assets/RuinRunner/ArtSource/Sheets. Old extraction logic exists only under Editor/ArtMigration/Legacy. These originals and extraction code are excluded from runtime Resources/player code. No original source image was deleted.
