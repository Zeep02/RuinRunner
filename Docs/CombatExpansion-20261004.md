# Combat expansion — 2026-10-04

This document describes implementation boundaries, not a completed campaign or visual-quality certification. Numerical item balance and drops are the pinned server catalog (`Backend/Content/catalog.campaign-v1.json`); the client action catalog owns the executable mechanic and formats its actual mana/cooldown into the displayed description.

## Input and cancellation contract

- LMB continues the equipped family's basic chain. Longsword is forward cut, opposite cut, wider finishing cut; the old final thrust is removed. Dagger, katana, mace and greatsword retain different chain length, rhythm, reach and finish motion.
- RMB is a finite active action. A press has the same 0.16-second intent buffer as the longsword rule. It can start once the pending hit has resolved and recovery permits it; it cannot replay an unresolved contact.
- Longsword catches briefly and automatically counters; a missed catch still completes an active cut. Katana catches into a crossing draw cut. Dagger lunges. Mace makes a forward impact. Greatsword press/hold/release prepares a forward cleave with a finite 0.6-second maximum preparation.
- Ordinary crossbow fires a penetrating bolt, pistol fires an aimable short burst, rifle takes a short aiming beat, shotgun spreads while stepping back, and cannon commits a visible mortar landing point.
- RMB re-input belongs to the currently stored unique action. It does not charge mana again or reset the paid cooldown. Re-input is not a way to stack multiple copies of a planted action.
- Dash, equip, pause, death and `CancelCombatForPresentation()` invalidate pending special contacts using an action epoch. Native melee bindings and transient item marks are cleared. Paid mana, action recovery, unique per-copy cooldown, ammo, reload and dash charges are not refunded.
- A normal bullet already fired can continue after equip/dash; its source owned identity is captured at launch. Secondary and pending special projectiles use the cancellation boundary. A previous copy's shot cannot trigger the currently equipped copy's awakening.
- `CancelCombatForPresentation()` is the boss-intro handoff. The caller supplies the actual phase/input lock. The API does not reset resources or create invulnerability.

## Unique ID actions

| ID | Distinct player operation |
| --- | --- |
| shadow_fang | Lunge leaves an origin, re-input within the window sweeps back to that origin. |
| blood_moon_katana | Draw cut marks at most three contacted enemies, re-input recalls those marks. |
| blood_reaver | Cleave plants three fixed fracture marks; the next basic melee contact joins the seam. |
| void_revolver | A contacted projectile embeds a charge; re-input detonates at the marked enemy. |
| dragon_cannon | Plants a powder beacon ahead; re-input explodes it and retreats safely. |
| sun_oath | Native catch/riposte banks only a real successful defense; another RMB releases the stored wave. |
| starbreaker | Plants a node; re-input traces player → node → current aim with one shared target ledger. |
| returning_edge (new) | Throws the actual native blade out and back. Moving changes its return route; each leg hits a target at most once. |
| gravity_maul (new) | Plants a well; a basic finisher activates a pull. Bosses receive damage without displacement. |
| tether_carbine (new) | Harpoon contact connects a target; three following basic ranged contacts pull it. Range or a wall breaks the connection. |
| bastion_shotgun (new) | A forward placed shield captures at most three hostile projectiles; re-input returns a bounded fan. |

All stored actions expire. Placement and secondary areas clip against ground collision. Area target counts and trail instance counts are bounded. Generic shape mutations affect basic attacks and do not replace an ID-specific special.

## New accessory rule coverage

The existing set includes automatic bleed, mana echo, lightning, shield and after-dash damage. The additions use different player sequences rather than duplicating those on-hit effects:

| ID | Trigger and limit |
| --- | --- |
| recall_spool | Store one basic-finisher path for two seconds; the next paid special recalls it, then a three-second cooldown. |
| cadence_clasp | Direct hit → dash → paid special within three seconds; a directional wind lane and capped mana recovery, four-second cooldown. |
| prism_cage | Paid special opens a 0.6-second frontal capture window; at most three bullets, capped total stored damage, next original attack releases once. |
| trail_spindle | Walking leaves at most three spaced marks; an original attack joins them once, shared target ledger and three-second cooldown. |
| focus_prism | Three distinct original actions focus one target; switching to another visible nearby target recalls the focus, three-second cooldown. |
| mana_aegis | On actual incoming damage, spend 12 mana to absorb at most 10 damage, five-second cooldown. This conversion does not recursively count as an active special. |

Arcane Lens is also changed: spending mana arms a short-lived echo at the original point; a following direct attack chooses its aim and releases it. It no longer fires automatically on every mana spend. Pending marks, packets and echoes are reconciled when the owned item is removed. Readiness uses native animated markers; all secondary damage enters `TakeItemDamage` and does not emit another original-hit/growth event.

## Growth integration

- `ConfigureCombatGrowth` receives the active talent query and the game-owned progression callback. Training state is explicit.
- Six combat/movement talent hooks: ComboMemory preserves a hit-then-dash chain; StoredGuard stores one frontal projectile; CrossChamber feeds one usable shot during the previous ranged copy's still-running reload; TurningDash bends once without adding distance/time; BrakeDash stops travel without refund; ReturnDash spends a second charge to retrace the origin.
- LongFinisher and WideFinisher trade basic final reach against breadth. Ricochet reflects a normal projectile once at a wall. AnchorRound places a contact mark for the following original attack.
- CounterDiscipline adds one narrow counter continuation, FinisherHunt recalls one finishing path, CrossWeaponChain releases the previous family's echo on the switched copy's first special, and SteadyVolley readies one mark round after an empty reload. The latter readiness follows the owned identity even while another slot is active.
- `CombatGrowthEvent.IsMagazineHit` is true only for original ammo-using projectiles. Free-ammo RMB contacts do not satisfy the three-in-one-magazine oath. Every reload emits a clearing event, and `ReloadStartedEmpty` uses the stored start state, including when CrossChamber feeds a round mid-reload.
- Stable weak target identities, original action identities and source owned identities permit deduplication without retaining destroyed targets. Training and recursive events do not progress the oath tracker.

## Server content boundary

`BindPinnedCatalog` validates complete known-mechanic lists and installs server numbers and item weights. It does not invent executable mechanics from remote strings. Once pinned, legacy GameBalance weapon overrides are bypassed. Runtime rolls use eligible individual weights directly; the old rarity roll is not applied on top. `SetDropStage(0..2)` enforces published stage intervals; bounded luck weighting increases higher-tier relative weight. `RollOfRarity` preserves tier when the chest intent talent changes item category.

The initial migration was exported once from client definitions, 46 weapons and 30 accessories. It is historical migration input, not a second live source. Backend publication and persistence verification are owned by the backend workstream.

## Art and verification

Four weapon PNGs and six accessory PNGs are unchanged native originals from Snoopeth's CC0 packs. Exact source paths, dimensions, source SHA256 and grip coordinates are in `ArtDirection/Review-20261004/combat-expansion/native-selection.json`; deployment attribution is in `Assets/StreamingAssets/ThirdPartyNotices/Snoopeth-CombatExpansion-20261004.txt`. No generated or rescaled image is adopted in this slice.

- Full Runtime offline compile: successful after the action/growth boundary integration; four existing warnings, no errors. `ArtDirection/Review-20261004/combat-expansion/compile.txt`.
- Native import: parent reported all ten originals imported and SHA/geometry checks passed. This alone does not verify the actual held pose.
- Functional fixture: `Tools/Validation/CombatExpansionRegression20261004.cs`, `Main()`. Requires Play at main menu, supplies a temporary isolated scene and fixed high-health targets. It uses actual GameAction inputs for the ten family actions, eleven ID paths, cancellation, projectile mutation and talent dash cases; accessory and awakening unit cases explicitly supply callbacks/state. It restores roots, active scene, input, audio settings, clocks, AutoTick and removes its temporary scene. Actual Unity execution PASS: `Logs/Validation/2026-10-04-combat-expansion/143431/result.txt`, 131 explicit checks including restoration and temporary-scene removal. This is the first real execution of this integrated action fixture, not an offline simulation.
- New weapon socket review: `WeaponRedesignReview20261004.NewUnique()`, 4 × 8 standing directions. Uses a transient supplied run; it is not evidence of a natural reward or campaign flow.
- These fixtures do not certify animation smoothness, listening quality, four real builds, natural reward acquisition or a full three-stage campaign. Those remain integration and observation work for the parent.
