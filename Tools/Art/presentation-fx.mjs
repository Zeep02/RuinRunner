#!/usr/bin/env node
// 환경·전투·UI 연출 pixelfx 클립 표. 각 행이 CombatFX/<이름> 클립 하나가 된다.
// 잉크 시퀀스와 해골 화로는 Tools/pixelfx/src/fx/ink, fire/skull-brazier 레시피를 쓴다. 기준은 연출용임시.gif.
// 반복 클립은 원본 길이 그대로 내보내 이음새 없이 돈다. 일회성 클립은 --until 로 게임 박자에 맞게 끊는다.
//   node Tools/Art/presentation-fx.mjs            전체 내보내기
//   node Tools/Art/presentation-fx.mjs AmbBrazier 지정 클립만
import { spawnSync } from "node:child_process";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SOUL = ["#0a1c20", "#123c40", "#1c6868", "#34a0a0", "#68d8d0", "#b0f4ec", "#f4fffc"];
const PALE = ["#141820", "#2a3240", "#4a5468", "#7a8698", "#b4bfcf", "#e4eaf2", "#ffffff"];
const GOLD = ["#241006", "#52240a", "#8e4210", "#cc7018", "#f4a42c", "#ffd878", "#fff6d8"];
const MOSS = ["#10180a", "#22331a", "#3c5a24", "#5a8434", "#84ac48", "#b8d870", "#eefcc0"];
const TEAL = ["#081830", "#0e2e5a", "#14508c", "#1c8ebc", "#36c2d0", "#88e8e0", "#e0fff8"];
const INK = ["#0c0814", "#241638", "#3c2460", "#5e3290", "#8048b8", "#a970e0", "#e0c8ff"];
const palette = (p) => (p ? { palette: p } : {});
const row = (fx, p, extra = []) => ({ fx, opts: palette(p), extra });

export const CLIPS = {
  // 해골 화로(연출용임시.gif의 화로). 캐릭터 크기라 그대로 쓴다.
  AmbSkullPale: row("fire/skull-brazier", null),
  AmbSkullGold: { fx: "fire/skull-brazier", opts: { palette: ["#3a1408", "#6e2a0c", "#a84a10", "#d9741a", "#f49c2c", "#ffc35a", "#ffe08c", "#fff2c0", "#ffffff"] }, extra: [] },
  AmbSkullTeal: { fx: "fire/skull-brazier", opts: { palette: ["#0a2a34", "#124a5a", "#1a7084", "#2a9cae", "#4cc6d0", "#84e4e2", "#b8f4ee", "#e0fcf8", "#ffffff"] }, extra: [] },
  AmbLampTeal: { fx: "fire/lamp-glow", opts: { palette: ["#0e4a58", "#1c8ebc", "#36c2d0", "#b8f4ee"] }, extra: [] },
  AmbLampEmber: row("fire/lamp-glow", null),
  // 움직이는 소품. 전부 원본이 이음새 없는 반복이다.
  AmbDrip: row("water/drip", null),
  AmbRipples: row("water/ripples", TEAL),
  AmbButterfly: row("critters/butterfly", null),
  AmbBat: row("critters/bat", INK),
  AmbSpirit: row("fire/spirit-flame", SOUL),
  AmbPortal: row("arcane/portal", GOLD),
  // 소품 위에 얹는 효과. 소품 자체의 움직임은 PixelLab 프레임(CombatFX/Prop*)이다.
  FxPropEmbers: row("fire/prop-embers", null),
  FxPropEmbersTeal: row("fire/prop-embers", ["#0e4a58", "#1c8ebc", "#36c2d0", "#b8f4ee"]),
  FxPropCrystalGlints: row("arcane/crystal-glints", null),
  FxPropSpores: row("nature/prop-spores", null),
  FxPropPollen: { fx: "nature/prop-spores", opts: { palette: ["#3c5a24", "#5a8434", "#a8cc58", "#e4f4a0"], motes: 4, spread: 7, drift: 3.5, glow: 0.06 }, extra: [] },
  FxPropBubbles: row("water/prop-bubbles", null),
  FxPropPipeDrip: row("water/pipe-drip", null),
  // 소품 파괴
  BreakPot: row("explosions/dirt-explosion", null, ["--until", "0.9"]),
  BreakStone: row("impacts/ground-slam", null, ["--until", "0.9"]),
  BreakBarrel: row("explosions/small-explosion", null, ["--until", "1"]),
  BreakPlant: row("spawn/poof", MOSS, ["--until", "0.8"]),
  // 등장·사망
  SpawnOuter: row("spawn/summon-rise", MOSS, ["--until", "1.1"]),
  SpawnFlooded: row("spawn/summon-rise", TEAL, ["--until", "1.1"]),
  SpawnCore: row("spawn/summon-rise", INK, ["--until", "1.1"]),
  DeathPuffOuter: row("spawn/poof", MOSS, ["--until", "0.8"]),
  DeathPuffFlooded: row("spawn/poof", TEAL, ["--until", "0.8"]),
  DeathPuffCore: row("spawn/poof", INK, ["--until", "0.8"]),
  // 글자 받침 붓자국, 바닥 얼룩, 발걸음
  BannerIn: { fx: "ink/banner", opts: { phase: "in" }, extra: [] },
  BannerOut: { fx: "ink/banner", opts: { phase: "out" }, extra: [] },
  StainOuter: { fx: "ink/stain", opts: { palette: ["#141c0c", "#22331a", "#3c5a24"] }, extra: [] },
  StainFlooded: { fx: "ink/stain", opts: { palette: ["#0a2230", "#123a4a", "#1f6a78"] }, extra: [] },
  StainCore: { fx: "ink/stain", opts: { palette: ["#120a1e", "#241638", "#44286e"] }, extra: [] },
  StainScorch: { fx: "ink/stain", opts: { palette: ["#0c0a0a", "#1c1614", "#3a2a22"], seed: 21 }, extra: [] },
  StepOuter: { fx: "ink/step", opts: { kind: "dust" }, extra: [] },
  StepFlooded: { fx: "ink/step", opts: { kind: "ripple", palette: ["#1c6a7c", "#48aab8", "#a8e8e8"] }, extra: [] },
  StepCore: { fx: "ink/step", opts: { kind: "ash", palette: ["#3c2460", "#8048b8", "#e0c8ff"] }, extra: [] },
  // 순간 연출
  BossFall: row("explosions/big-explosion", GOLD, ["--until", "1.2"]),
  UiLevelUp: row("arcane/mana-burst", GOLD, ["--until", "0.9"]),
  UiPick: row("pickups/collect-burst", GOLD),
  UiConfetti: row("ui/confetti", null, ["--until", "1.8"]),
  AmbForge: row("fire/prop-embers", null),
  // 보상·UI
  LootBeam: row("pickups/loot-beam", GOLD, ["--until", "1.2"]),
  LevelUpBurst: row("arcane/mana-burst", GOLD, ["--until", "1"]),
  RoomClearRing: row("impacts/ground-slam", GOLD, ["--until", "0.9"]),
  // 잉크 시퀀스(연출용임시.gif 기준): 덮고, 걷히고, 웅덩이에서 솟는다
  InkCover: { fx: "ink/cover", opts: {}, extra: [] },
  InkRecede: { fx: "ink/recede", opts: {}, extra: [] },
  InkPuddle: { fx: "ink/rise", opts: { layer: "puddle" }, extra: [] },
  InkMound: { fx: "ink/rise", opts: { layer: "mound" }, extra: [] },
};

const only = process.argv.slice(2);
let failed = 0;
for (const [clip, c] of Object.entries(CLIPS)) {
  if (only.length && !only.includes(clip)) continue;
  const args = [join(HERE, "pixelfx-to-unity.mjs"), c.fx, clip, "--opts", JSON.stringify(c.opts), "--install", "--replace", ...c.extra];
  const r = spawnSync(process.execPath, args, { encoding: "utf8" });
  if (r.status !== 0) { failed++; console.log("FAIL", clip, (r.stderr || r.stdout).slice(0, 200)); }
  else console.log(clip, (r.stdout.match(/\{"[^}]+\}/) || [""])[0]);
}
process.exit(failed ? 1 : 0);
