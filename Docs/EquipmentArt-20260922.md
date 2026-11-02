# Equipment art replacement — 2026-09-22

All 31 weapon IDs and 18 accessory IDs now use reviewed native pixel art. Inventory, held equipment and field loot share the same PNG per ID. Ten legacy passive aliases resolve to the current accessories. Previous 31 held PNGs and 61 generated icons were preserved with GUIDs under Assets/RuinRunner/ArtReview/RetiredGeneratedEquipment, outside Resources.

## Sources and presentation
- 7Soul, CC0: 15 melee weapons, 4 bows, most accessories and arrow projectile. Native 34px originals, transparent padding only.
- SnoopethDuckDuck Guns, CC0: 9 firearms, original two-pixel outline variants. Large pistol candidates obscured the hero's face and were replaced by compact originals in the same pack.
- CraftPix free Bow/Crossbow Icons: 3 native 32px crossbows. Freebie commercial game license; no standalone redistribution. Not used for image generation.
- Shikashi / Matt Firth / game-icons.net, CC BY4.0: dice, hearts, skull, lens. Initial map/anatomical-heart candidate mistakes corrected.
- Kyrise, CC BY4.0: original native16 crimson gem ring, replacing mismatched blue-gem candidate.

Originals/acquisition/license evidence: ArtDirection/ExternalCandidates/20260922. Distribution credits: Assets/StreamingAssets/ThirdPartyNotices. Exact sources/cells/hashes/landmarks: native-item-selection/all-weapons-import.json, accessory-import.json, projectile-import.json.

NativeWeaponArt supplies authored grip, muzzle and orientation. All individual PNGs were imported via Sprite Editor APIs: 32PPU, Point, uncompressed, explicit pivots. Emissions and visible weapons share the same landmarks. ItemIconLibrary resolves passive aliases and draws icons at integer physical-pixel enlargement inside existing HUD slots; small windows below native resolution retain a fit fallback. World pickups share the same textures. Oversized generated arrow replaced by native arrow; damage, movement and collision sizes unchanged.

## Evidence
- source-identity.txt: all49 equipment PNGs preserve reviewed source RGBA pixels after padding; no resampling.
- Logs/Validation/2026-09-22-native-weapons/020519: first19 group PASS764 sockets/25shots/error0.
- 021854: full31 weapons,3 directions each, PASS1233 sockets/107shots/error0,18accessories/10legacy aliases. all-weapons-1x.mp4 is124.705873seconds,1127 actual samples, real-time hold. Before final compact pistol substitutions.
- 022646: final four pistol IDs,8directions each, PASS442sockets/42shots/error0. No retired hero frames. This is the latest pistol source.
- Logs/Validation/2026-09-22-equipment-ui/022404: actual HUD, seeded31weapon/18accessory inventory pages and6FieldLootPickup objects. 04-world-drops-settled.png after introductory banner. Before final compact pistol substitutions.
- Earlier UI fixture022131 needed a stale one-frame fixture input cleared before its final capture. Harness now clears pulses; no production input workaround.
- X clean build succeeded,0errors/3existing warnings, but has rejected larger pistols. Y supersedes it; consult build/run evidence before claiming completion.

All fixtures are isolated/injected art/input checks, not normal drop or campaign evidence. Dedicated combat body poses, boss/later-stage art, stage composition, target run length, performance and overall goal acceptance remain incomplete.

## Rejected / historical material
Scrython: dark/military look. FartFish: tiny relative to other art. LimeZu: white borders. TheSquawkyRaven free pack: noncommercial-only. Anvil: incompatible restrictions, not adopted.
Generated Items-20260922/scrap-pistol-v1.png is NOT active: uneven logical pixel grid and noise; prompt/reference retained. Initial native-item-selection/manifest.json is a historical draft with rejected/misidentified cells; never import it wholesale. Use reviewed manifests above. Unconnected outline shader prototypes remain outside Assets.

## Bow adjustment and Windows follow-up
- User requested bows (not crossbows) be10% smaller. Bow-only renderer/muzzle scale is0.9. Hunter/composite/celestial native grip pivots corrected; great_bow grip unchanged. Source pixels preserved.
- 023308: all4bows at8directions,446socket samples,36shots,muzzle error0. bows-90percent-1x.mp4:45.139667seconds,418actual samples. Dedicated draw-string body/arm poses remain missing; this is not completed bow-animation evidence.
- RangedWeaponArt landmarks now recognize only16ranged IDs, never15melee IDs. Compiled Unity evaluation verified the split.
- Y normal Windows ranged campaign reached final ending in387.0seconds,60kills,3bosses,exit0. New isolated profile, normal input/rules, no forced weapons/invulnerability. Before latest bow adjustments; not evidence for AA/AB.
- AA build4528e21d90f9 completed with0errors/3existing warnings. Its Windows smoke failed the obsolete minimum32px accessory canvas assertion on native Kyrise16px art. Log retained at windows-AA-visual/smoke. The assertion now checks actual source identity, Point filtering, no mipmaps and nonempty alpha. AB clean build build_bb849ba6609e succeeded:0errors/3existing warnings. Windows smoke/map/1280x720/1920x1080/1460x900 all exit0,95nonblank screenshots. Inventory renders manually reviewed at all3sizes. Whole-UI visual acceptance and normal full campaign on AB are not claimed.

## Ranged stance continuity — 02:50
The old body selected movement direction whenever the short ranged-shot pose expired. Holding aim right while strafing/backpedaling therefore turned the body away between consecutive shots. Before-fix fixture024754 reproduced91wrong-facing observations out of147 across Bow/Crossbow/Pistol/Rifle/Shotgun/Cannon.
PlayerAnimationController now follows weapon aim throughout ranged locomotion, preserving dash direction and melee travel-facing behavior. Backpedal plays the existing native footfall cycle in reverse without resetting gait phase; physical speed, damage, fire delay and sprite pixels are unchanged. This is transition correction, not new authored arm/body poses.
First fixed fixture024959:150facing samples,0mismatches;286socket samples,38shots,muzzle error0;68reverse and70forward gait deltas,0wrong direction. Pause/dash/death retained. Extended025134 PASS:247facing samples/0mismatches,436socket samples/43shots/error0,161reverse+66forward gait deltas/0wrong direction. All8bow backpedal directions, melee swap, dash, pause, death. Actual-time43.513477second video,402samples. AC clean build build_829bc7d3fb73:0errors/3existing warnings. Windows120second normal ranged run reached stage2 and exited0 at the explicit diagnostic limit; not full completion. No forced loadout/health/damage. Default1080p focused normal combat measured20.01fps,p9551.25ms; performance goal FAIL. VSync0 comparison also completed120seconds with similar20fps. Concurrent Destiny2/Unity GPU activity and67percent total GPU utilization were observed; no other applications/settings were changed. These measurements do not establish isolated game cost or prove60fps. AC-performance-comparison.json records both runs and engine timing.
Generated bow-key-v4 was rejected: changed eye/hair, nonuniform pixel blocks, wrong canvas. Native193hero PNGs remain unchanged. Full prompt and rejected output retained in ArtDirection/Generated/SscaryMotion-20260922.
