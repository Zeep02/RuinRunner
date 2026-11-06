#!/usr/bin/env node
// 몬스터 공격 전용 pixelfx 클립 표. 각 행이 CombatFX/E<Name> 클립 하나가 된다.
// 호·원·띠 이펙트는 게임의 실제 피격 범위 크기(32 PPU)로 그려서, 눈에 보이는 공격이 판정과 같은 모양이 되게 한다.
//   node Tools/Art/enemy-attack-fx.mjs            전체 내보내기(이미 있으면 건너뜀)
//   node Tools/Art/enemy-attack-fx.mjs --force EShotRune ERingStone    지정 클립 다시 내보내기
//   node Tools/Art/enemy-attack-fx.mjs --diameters    NativeCombatEffect.diameters 항목 출력
import { spawnSync } from "node:child_process";
import { existsSync, rmSync, readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url)), ROOT = join(HERE, "..", "..");
const COMBATFX = join(ROOT, "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX");

const PAL = {
  earth: ["#140e0a", "#2a1e14", "#47321e", "#69492a", "#8e6638", "#b88a52", "#e6c08a"],
  clay: ["#1a0a06", "#3c1608", "#6e2a0e", "#a84616", "#dc6c22", "#f8a040", "#ffe0a0"],
  water: ["#081830", "#0e2e5a", "#14508c", "#1c8ebc", "#36c2d0", "#88e8e0", "#e0fff8"],
  rune: ["#2a1c08", "#5a3e10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6"],
  hex: ["#0c0814", "#241638", "#3c2460", "#5e3290", "#8048b8", "#a970e0", "#e0c8ff"],
  soul: ["#0a1c20", "#123c40", "#1c6868", "#34a0a0", "#68d8d0", "#b0f4ec", "#f4fffc"],
  spore: ["#061a14", "#0e3a2c", "#17634c", "#26967a", "#4cc8a4", "#94ecd0", "#e8fff4"],
  steel: ["#14161c", "#2c3038", "#4a4f5a", "#70757f", "#9ea3ac", "#d0d4da", "#f6f7f8"],
  brass: ["#201406", "#4a300c", "#7e5414", "#b8821c", "#e4ae34", "#f8d878", "#fff6d0"],
  amber: ["#241006", "#52240a", "#8e4210", "#cc7018", "#f4a42c", "#ffd878", "#fff6d8"],
  tusk: ["#1a1208", "#3e2c14", "#6c5024", "#a0803c", "#ccb060", "#ecdca0", "#fffcec"],
};
const EARTH_STONE = "#5a4632";
const sparksOf = (p) => [PAL[p][6], PAL[p][5], PAL[p][4], PAL[p][3]];
const row = (effect, options, canvas, more) => ({ effect, options, canvas, ...more });
const ring = (p, r, o = {}, canvas) => row("weapons/ring", { palette: PAL[p], r, squash: 1, ...o }, canvas);
const flash = (p, o = {}) => row("weapons/flash", { palette: PAL[p], sparks: sparksOf(p), ...o });
const bolt = (p, kind, size, o = {}) => row("weapons/bolt", { palette: PAL[p], kind, size, ...o });
const puff = (p, o = {}) => row("weapons/puff", { palette: PAL[p], ...o });
// 완성 이펙트를 색만 바꿔 쓴다. from/until 은 예비 동작과 오래 남는 잔해를 잘라 전투 박자에 맞춘다(끝은 서서히 사라진다).
const plasma = (p, size) => row("projectiles/plasma-shot", { palette: PAL[p], size });
const disc = (p, size) => row("projectiles/energy-disc", { palette: PAL[p], size });
const spark = (p) => row("impacts/hit-spark", { palette: PAL[p], height: 0 });
// 앵커는 빛과 조각의 무게 중심. 크롭 중심은 흩날리는 조각 때문에 치우친다. 앞의 흰 섬광은 잘라 낸다(주인공보다 화려하면 안 된다).
const poof = (p, until) => row("spawn/poof", { palette: PAL[p].slice(1), stars: sparksOf(p) }, null, { anchor: "0,-24", from: 0.25, until });
const sweep = (p, R, W, span, sparksN) => row("enemies/sweep", { palette: PAL[p], R, W, span, sparksN });
const cut = (p, L, W, sparksN = 4) => row("enemies/cut", { palette: PAL[p], L, W, sparksN });

export const TABLE = {
  // 날아가는 탄: 머리가 피벗, 진행 방향은 +x. 몸이 작은 일반 적에 맞는 크기다.
  EShotEmber: plasma("clay", 4), EShotEmberHit: spark("clay"), EFlashEmber: flash("clay", { len: 11, wide: 5 }),
  EShotWater: plasma("water", 4), EShotWaterHit: spark("water"), EFlashWater: flash("water", { len: 10, wide: 5 }),
  EShotBubble: plasma("water", 3), EShotBubbleHit: spark("water"),
  EShotRune: row("projectiles/laser-bolt", { palette: PAL.rune, length: 20 }), EShotRuneHit: spark("rune"), EFlashRune: flash("rune", { len: 10, wide: 5 }),
  EShotSoul: plasma("soul", 3), EShotSoulHit: spark("soul"), EFlashSoul: flash("soul", { len: 10, wide: 5 }),
  EShotHex: plasma("hex", 4), EShotHexHit: spark("hex"),
  EShotStone: bolt("earth", "shard", 0.9, { tail: 8 }), EShotStoneHit: spark("earth"),
  EShotTide: disc("water", 9), EShotTideHit: spark("water"),
  EShotSeal: disc("soul", 9), EShotSealHit: spark("soul"),
  EShotBoulder: bolt("earth", "shard", 1.5, { tail: 12 }), EShotBoulderHit: spark("earth"),
  EFlashHex: flash("hex", { len: 10, wide: 5 }),

  // 몬스터 공격은 주인공 베기보다 수수하게 만든다. 흰 섬광, 겹치는 잔상, 강한 빛을 넣지 않는다.
  // 멧돼지는 베지 않고 엄니로 앞을 들이받는다.
  EChargerTusk: row("enemies/gore", { palette: PAL.tusk, reach: 66 }),
  // 부채꼴: 호의 반지름과 각도가 ArenaHazard 부채꼴과 같다. 적을 중심으로 돌고 게임이 조준 방향으로 돌린다.
  ECrabSweep: sweep("water", 93, 11, 200, 4),
  EWardenSwipe: sweep("amber", 83, 13, 120, 5),

  // 직선 베기: 길이가 판정 상자와 같고 늘리지 않는다. 피벗이 시작점이다.
  ESentinelCut: cut("steel", 122, 9),
  ESentinelCross: cut("steel", 134, 8),
  EKnightCut: cut("brass", 122, 10),
  EKnightHeavy: cut("brass", 160, 14, 6),
  // 토템을 가운데 두고 양쪽으로 18칸.
  ETotemBeam: row("enemies/beam", { palette: PAL.rune, L: 576, W: 8 }, "620x90"),

  // 충격: 판정 범위는 ERing* 이 정확히 보여 주고, 이쪽은 먼지와 돌조각이다. 큰 충격도 Fit 으로 키운 같은 그림이다.
  EBurstStone: row("impacts/heavy-landing", { stone: EARTH_STONE }, null, { until: 1 }),
  EBurstHex: poof("hex", 1.2),
  EBurstSpore: poof("spore", 1.3),
  ERingStone: ring("earth", 104, { thick: 5 }, "280x280"),
  ERingMid: ring("earth", 72, { thick: 4 }, "200x200"),
  ERingSmall: ring("earth", 38, { thick: 3 }, "120x120"),
  ERingSpore: ring("spore", 61, { thick: 3 }, "170x170"),
  ERingHex: ring("hex", 40, { thick: 3 }, "120x120"),

  // 탄막 직전 기 모으기: 안쪽으로 닫히는 고리.
  ECastEmber: ring("clay", 22, { inward: true, thick: 2 }, "80x80"),
  ECastWater: ring("water", 26, { inward: true, thick: 2 }, "90x90"),
  ECastRune: ring("rune", 30, { inward: true, thick: 2 }, "100x100"),
  ECastSoul: ring("soul", 22, { inward: true, thick: 2 }, "80x80"),
  ECastHex: ring("hex", 34, { inward: true, thick: 3 }, "110x110"),
  ECastSeal: ring("soul", 56, { inward: true, thick: 3 }, "160x160"),
  ECastStone: ring("amber", 60, { inward: true, thick: 3 }, "170x170"),

  // 방이 잠길 때 기둥을 잇는 룬 띠. 길이 64px을 기둥 간격에 맞춰 늘린다.
  ESealLinkOuter: row("weapons/line-loop", { palette: PAL.amber, style: "thread", W: 3, taper: false }, "90x50"),
  ESealLinkFlooded: row("weapons/line-loop", { palette: PAL.water, style: "thread", W: 3, taper: false }, "90x50"),
  ESealLinkCore: row("weapons/line-loop", { palette: PAL.rune, style: "thread", W: 3, taper: false }, "90x50"),

  // 돌진 먼지.
  ERushDust: puff("earth", { dir: -1, n: 5, rise: 6 }),

  // 보스 대기술. 보스만큼은 화려해도 된다. 판정 도형을 그대로 채우도록 게임이 Fit 한다.
  EShotRockBig: bolt("earth", "shard", 2.6, { tail: 16 }), EShotRockBigHit: spark("earth"),
  EQuakeRift: row("nature/quake", {}, null, { until: 1.5 }),
  EWhirlpool: row("water/whirlpool", { palette: PAL.water }),
  EWaveCrash: row("water/wave-crash", {}, null, { until: 2 }),
  EGeyserBlast: row("water/geyser", {}, null, { from: 1.9, until: 3.3 }),
  EShotWave: disc("water", 15), EShotWaveHit: spark("water"),
  ESealBeamLoop: row("weapons/line-loop", { palette: PAL.rune, style: "beam", W: 9, taper: false }, "90x60"),
  ESmite: row("holy/smite", { palette: PAL.rune }, null, { until: 1.7 }),
  ESealCircle: row("arcane/magic-circle-cast", { palette: PAL.rune }, null, { until: 3 }),
  EManaBurst: row("arcane/mana-burst", { palette: PAL.rune }),
  EShotOrbit: disc("rune", 8), EShotOrbitHit: spark("rune"),
};

const args = process.argv.slice(2), force = args.includes("--force"), only = args.filter((a) => !a.startsWith("--"));
if (args.includes("--diameters")) {
  const parts = Object.keys(TABLE).map((clip) => {
    const meta = JSON.parse(readFileSync(join(COMBATFX, clip, "_pixelfx.json"), "utf8"));
    return `{"${clip}",${Math.max(1, meta.visibleDiameter)}}`;
  });
  console.log(parts.join(","));
  process.exit(0);
}
let done = 0, skipped = 0, failed = [];
for (const [clip, spec] of Object.entries(TABLE)) {
  if (only.length && !only.includes(clip)) continue;
  const dest = join(COMBATFX, clip);
  if (existsSync(dest) && !force) { skipped++; continue; }
  if (existsSync(dest)) { rmSync(dest, { recursive: true, force: true }); rmSync(dest + ".meta", { force: true }); }
  rmSync(join(HERE, "Staging/PixelFx", clip), { recursive: true, force: true });
  const run = [join(HERE, "pixelfx-to-unity.mjs"), spec.effect, clip, "--opts", JSON.stringify(spec.options), "--seed", String(done % 90 + 3), "--install"];
  if (spec.canvas) run.push("--canvas", spec.canvas);
  if (spec.center) run.push("--center");
  if (spec.anchor) run.push("--anchor", spec.anchor);
  if (spec.from) run.push("--from", String(spec.from));
  if (spec.until) run.push("--until", String(spec.until));
  const r = spawnSync(process.execPath, run, { encoding: "utf8" });
  if (r.status !== 0) { failed.push(clip + ": " + (r.stderr || r.stdout).split("\n").slice(0, 3).join(" ")); continue; }
  done++;
}
console.log(`exported ${done}, skipped ${skipped}, failed ${failed.length}`);
for (const f of failed) console.log(" FAIL " + f);
