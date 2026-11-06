#!/usr/bin/env node
// 무기 70종의 우클릭 스킬 연출. 범용 부품(weapons/*)이 아니라 pixelfx 완성 이펙트를 무기마다 고르고 칠해 내보낸다.
// Cast는 스킬을 쓰는 순간 재생되고, 그 밖의 역할은 weapons70-fx.mjs의 같은 이름 클립을 대신한다.
// style: aim 무기 끝에서 조준 방향으로 뻗음, front 그림이 주인공을 비켜날 만큼 앞의 바닥(오브는 구체 위치). 주인공 몸에서 시작하지 않는다.
//   none: 누르기만 했다고 그림을 띄우지 않는다. 그 무기의 스킬 코드가 판정 자리에 직접 재생한다(재사용 때 다시 나오지도 않는다).
//   node Tools/Art/weapons70-skillfx.mjs                전체 내보내기
//   node Tools/Art/weapons70-skillfx.mjs ember_claw     지정 무기만
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url)), ROOT = join(HERE, "..", "..");
const COMBATFX = join(ROOT, "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX");
const GENERATED = join(ROOT, "Assets/RuinRunner/Runtime/Data/SignatureCastTable.cs");

// [이펙트, 스타일, 팔레트, 추가 옵션, 내보내기 설정]. 팔레트가 null이면 이펙트 자체 색을 쓴다.
// 내보내기 설정: from 앞부분을 버릴 초, until 초(0이면 자르지 않음), center 피벗을 크롭 중심으로, fps 프레임레이트.
const C = (fx, style, p = null, o = {}, x = {}) => ({ fx, style, p, o, x });
// 그림 없이 스킬 코드만으로 보여 주는 무기. Cast 클립을 만들지 않는다.
const NONE = { fx: null, style: "none" };
export const SKILLS = {
  alley_awl: { Cast: NONE, A: C("impacts/crit-hit", "", "silver"), B: C("melee/dagger-flurry", "", "silver", {}, { center: true }) },
  saw_fang: { Cast: NONE, B: C("melee/claw-swipe", "", "blood", {}, { center: true }), C: C("explosions/small-explosion", "", "blood") },
  acrobat_knife: { Cast: NONE, B: C("impacts/parry", "", "silver") },
  frost_fang: { Cast: C("ice/snowflake-burst", "front", "ice"), B: C("ice/ice-shatter", "", "ice") },
  viper_tongue: { Cast: C("nature/acid-splash", "none", "poison"), B: C("nature/poison-cloud", "", "poison") },
  ember_claw: { Cast: C("melee/claw-swipe", "aim", "ember"), B: C("explosions/small-explosion", "", "ember") },
  shadow_suture: { Cast: C("shadow/shadow-tendrils", "none", "shadow", {}, { from: 0.32, until: 1.9 }) },
  blood_bank: { Cast: C("shadow/shadow-flames", "none", "blood"), B: C("explosions/big-explosion", "", "blood") },
  hour_minute: { Cast: C("arcane/time-stop", "none", "gold", {}, { center: true }) },
  night_thread: { Cast: C("arcane/star-fall", "none", "night") },

  merc_slab: { Cast: C("melee/hammer-smash", "front", "steel"), A: C("impacts/ground-slam", "", "stone") },
  lumber_blade: { Cast: C("melee/heavy-cleave", "front", "earth"), A: C("explosions/dirt-explosion", "", "earth") },
  warden_door: { Cast: NONE, A: C("impacts/parry", "", "steel") },
  cinder_cleaver: { Cast: C("fire/flame-pillar", "front", "lava"), B: C("fire/fire-ring", "", "ember") },
  glacier_tomb: { Cast: NONE, A: C("ice/ice-spikes", "", "ice", { reach: 44, height: 56 }, { until: 1.95, center: true }), B: C("ice/ice-spikes", "", "ice", { reach: 58, height: 84 }, { until: 1.95, center: true }), C: C("ice/ice-spikes", "", "ice", { reach: 76, height: 112 }, { until: 1.95, center: true }) },
  thunder_slug: { Cast: C("lightning/lightning-strike", "front", "volt"), B: C("lightning/zap-hit", "", "volt"), C: C("lightning/ball-lightning", "", "volt") },
  gravity_stele: { Cast: C("shadow/black-hole", "front", "gravity"), B: C("explosions/implosion", "", "gravity"), C: C("impacts/ground-slam", "", "gravity") },
  executioner_scale: { Cast: C("melee/heavy-cleave", "front", "scarlet"), C: C("spawn/poof", "", "ash") },
  dawn_gate: { Cast: C("holy/sunburst", "front", "gold") },
  continent_scar: { Cast: C("nature/quake", "front", "earth"), B: C("nature/earth-spike", "", "earth"), C: C("explosions/dirt-explosion", "", "earth") },

  trainee_blade: { Cast: C("melee/sword-slash", "aim", "silver"), B: C("impacts/hit-spark", "", "silver") },
  wanderer_blade: { Cast: C("melee/double-slash", "aim", "steel"), B: C("spawn/poof", "", "ash") },
  ironback_blade: { Cast: C("impacts/parry", "front", "iron"), A: C("impacts/shield-block", "", "iron") },
  moon_white: { Cast: C("melee/sword-slash", "aim", "moon"), B: C("impacts/crit-hit", "", "moon") },
  red_lotus: { Cast: NONE, B: C("explosions/small-explosion", "", "scarlet"), E: C("melee/flower-bloom", "", "scarlet", { R: 62, petals: 8, seed: 21 }, { until: 0 }) },
  wind_cutter: { Cast: NONE },
  falling_blossom: { Cast: NONE, A: C("nature/healing-bloom", "", "sakura"), B: C("melee/flower-bloom", "", "sakura", { R: 72 }, { until: 0 }), C: C("nature/healing-bloom", "", "sakura"), D: C("explosions/firework-burst", "", "sakura"), E: C("melee/flower-bloom", "", "sakura", { R: 44, petals: 7, seed: 12 }, { until: 0 }) },
  mu_eum: { Cast: C("melee/double-slash", "aim", "ink"), D: C("melee/spin-slash", "", "ink", {}, { center: true }) },
  instant_king: { Cast: NONE, A: C("impacts/parry", "", "gold"), C: C("impacts/crit-hit", "", "gold") },
  thousand_returns: { Cast: NONE },

  sentry_spear: { Cast: NONE, A: C("impacts/hit-spark", "", "white"), B: C("explosions/dirt-explosion", "", "earth"), C: C("holy/light-spear", "", "silver", { L: 128, W: 6 }, { until: 0 }) },
  hunt_harpoon: { Cast: NONE, C: C("impacts/crit-hit", "", "iron") },
  side_blade_pike: { Cast: NONE },
  thunder_needle: { Cast: NONE, C: C("lightning/lightning-strike", "", "volt", {}, { from: 0.77, until: 1.7 }) },
  tide_fang: { Cast: NONE, A: C("water/water-jet", "", "water", { length: 118 }, { until: 0.7 }), B: C("water/water-jet", "", "water", { length: 118 }, { until: 0.7 }), C: C("water/splash", "", "water"), D: C("water/whirlpool", "", "water", {}, { until: 1.1 }) },
  thorn_spear: { Cast: NONE, C: C("nature/thorns", "", "leaf") },
  chain_horizon: { Cast: NONE, B: C("impacts/bullet-impact", "", "iron"), E: C("impacts/crit-hit", "", "silver") },
  bannerless: { Cast: C("explosions/plasma-explosion", "none", "war"), A: C("weapons/zone", "", "war", { R: 83 }, { until: 0 }), B: C("holy/light-spear", "", "war", { L: 256, W: 7, stars: 10 }, { until: 0 }) },
  dragon_spine: { Cast: NONE },
  polaris: { Cast: NONE, B: C("arcane/mana-burst", "", "polar"), C: C("holy/light-spear", "", "polar", { L: 256 }, { until: 0 }) },

  novice_branch: { Cast: C("arcane/spell-charge", "none", "arcblue", {}, { until: 0, center: true }) },
  charcoal_staff: { Cast: C("fire/fire-breath", "aim", "ember"), B: C("explosions/small-explosion", "", "ember") },
  pebble_staff: { Cast: C("nature/boulder", "front", "stone"), B: C("explosions/dirt-explosion", "", "stone") },
  frost_branch: { Cast: C("ice/frost-breath", "aim", "ice"), C: C("ice/ice-shatter", "", "ice") },
  storm_cloud_staff: { Cast: NONE, A: C("lightning/storm-cloud", "", "storm", {}, { until: 0, center: true, fps: 15 }), B: C("lightning/lightning-strike", "", "storm", {}, { from: 0.77, until: 1.7 }) },
  swamp_lantern: { Cast: C("nature/poison-cloud", "front", "poison"), C: C("nature/toxic-puddle", "", "poison") },
  refractor_staff: { Cast: NONE, B: C("impacts/hit-spark", "", "prismA"), E: C("holy/light-spear", "", "prismA", { L: 256, W: 5 }, { until: 0 }), F: C("holy/light-spear", "", "prismB", { L: 256, W: 5 }, { until: 0 }), G: C("holy/light-spear", "", "white", { L: 256, W: 5 }, { until: 0 }) },
  undertaker_candle: { Cast: C("fire/spirit-flame", "front", "soul"), B: C("shadow/shadow-flames", "", "soul") },
  star_fold: { Cast: C("explosions/plasma-explosion", "none", "night"), D: C("arcane/mana-burst", "", "night"), E: C("arcane/mana-burst", "", "night"), F: C("holy/light-spear", "", "night", { L: 256, W: 8, stars: 16 }, { until: 0 }) },
  season_end: { Cast: NONE, A: C("nature/thorns", "", "leaf"), B: C("fire/fire-ring", "", "ember"), C: C("nature/leaf-storm", "", "earth"), G: C("nature/tornado", "", "earth"), D: C("ice/frost-nova", "", "ice", { reach: 61 }, { from: 0.3, until: 1.7 }), E: C("fire/flame-pillar", "", "ember", {}, { from: 0.45, until: 1.8 }), F: C("ice/ice-spikes", "", "ice", { reach: 26, height: 34 }, { until: 1.95, center: true }) },

  whetstone_orb: { Cast: C("arcane/mana-burst", "none", "stone"), A: C("arcane/mana-burst", "", "stone") },
  lantern_orb: { Cast: C("holy/holy-pillar", "front", "gold") },
  iron_core: { Cast: NONE, C: C("impacts/crit-hit", "", "iron") },
  frost_satellite: { Cast: NONE, A: C("ice/ice-spikes", "", "ice", { reach: 22, height: 24 }, { until: 1.95, center: true }) },
  ember_heart: { Cast: NONE, B: C("explosions/big-explosion", "", "ember"), C: C("fire/fire-ring", "", "ember") },
  storm_marble: { Cast: NONE, A: C("lightning/ball-lightning", "", "arcblue", {}, { until: 0, center: true }), C: C("lightning/ball-lightning", "", "volt", {}, { until: 0, center: true }), B: C("lightning/lightning-strike", "", "storm", {}, { from: 0.77, until: 1.7 }), E: C("lightning/zap-hit", "", "storm") },
  twin_star: { Cast: C("arcane/mana-burst", "none", "mono"), C: C("explosions/plasma-explosion", "", "mono") },
  mirror_eye: { Cast: C("arcane/mana-burst", "none", "moon"), C: C("explosions/firework-burst", "", "moon"), B: C("holy/light-spear", "", "moon", { L: 256, W: 4 }, { until: 0 }) },
  small_eclipse: { Cast: C("arcane/mana-burst", "none", "gold"), B: C("shadow/dark-explosion", "", "gold"), C: C("arcane/rune-circle", "", "gold"), D: C("shadow/eclipse", "", "gold", {}, { until: 0, center: true, fps: 15 }), E: C("weapons/zone", "", "gold", { R: 83 }, { until: 0 }), F: C("holy/sunburst", "", "gold") },
  world_pendulum: { Cast: NONE, C: C("weapons/zone", "", "teal", { R: 74, ticks: 8 }, { until: 0 }), D: C("impacts/parry", "", "teal") },

  scrap_revolver: { Cast: C("projectiles/muzzle-shotgun", "aim", "brass", {}, { until: 0.9 }), Hit: C("explosions/small-explosion", "", "brass") },
  gate_shotgun: { Cast: C("projectiles/muzzle-shotgun", "aim", "ember", {}, { until: 0.9 }) },
  scout_carbine: { Cast: NONE, B: C("impacts/bullet-impact", "", "white") },
  furnace_sprayer: { Cast: C("fire/flamethrower", "none", "ember", {}, { until: 0 }), B: C("spawn/poof", "", "ash") },
  frost_bolt_rifle: { Cast: C("holy/light-spear", "none", "ice", { L: 256, W: 6, stars: 12 }, { until: 0 }), C: C("explosions/plasma-explosion", "", "ice") },
  ricochet_pistol: { Cast: NONE, B: C("impacts/bullet-impact", "", "brass"), E: C("explosions/firework-burst", "", "brass") },
  lightning_carbine: { Cast: NONE, D: C("lightning/zap-hit", "", "volt"), E: C("lightning/lightning-strike", "", "volt", {}, { from: 0.77, until: 1.7 }) },
  siege_engineer: { Cast: C("impacts/ground-slam", "none", "brass", {}, { until: 0.8 }), D: C("spawn/poof", "", "ash") },
  last_bell: { Cast: NONE, B: C("holy/light-spear", "", "white", { L: 256, W: 14, stars: 12 }, { until: 0 }) },
  meteor_waybill: { Cast: C("fire/meteor", "none", "ember", {}, { until: 2.4 }), C: C("explosions/big-explosion", "", "ember") },
};

const pascal = (id) => id.split("_").map((s) => s[0].toUpperCase() + s.slice(1)).join("");
const palettes = readFileSync(join(HERE, "weapons70-fx.mjs"), "utf8");
const PAL = Object.fromEntries([...palettes.matchAll(/^\s+(\w+): (\[(?:"#[0-9a-f]+"(?:, )?)+\]),$/gm)].map((m) => [m[1], JSON.parse(m[2])]));
const LIMIT_SECONDS = 1.25;
const only = process.argv.slice(2).filter((a) => !a.startsWith("--"));
const ids = only.length ? only : Object.keys(SKILLS);
let done = 0; const failed = [];
for (const id of ids) {
  for (const [role, row] of Object.entries(SKILLS[id])) {
    if (!row.fx) {
      const gone = join(COMBATFX, "W" + pascal(id) + role);
      rmSync(gone, { recursive: true, force: true }); rmSync(gone + ".meta", { force: true });
      continue;
    }
    if (!existsSync(join(ROOT, "Tools/pixelfx/src/fx", row.fx + ".js"))) { failed.push(id + " " + role + " 없는 이펙트 " + row.fx); continue; }
    if (row.p && !PAL[row.p]) { failed.push(id + " " + role + " 없는 팔레트 " + row.p); continue; }
    const clip = "W" + pascal(id) + role, dest = join(COMBATFX, clip);
    if (existsSync(dest)) { rmSync(dest, { recursive: true, force: true }); rmSync(dest + ".meta", { force: true }); }
    rmSync(join(HERE, "Staging/PixelFx", clip), { recursive: true, force: true });
    const opts = { ...(row.p ? { palette: PAL[row.p] } : {}), ...row.o };
    const until = row.x?.until ?? LIMIT_SECONDS;
    const r = spawnSync(process.execPath, [join(HERE, "pixelfx-to-unity.mjs"), row.fx, clip, "--opts", JSON.stringify(opts), "--seed", String((done * 11) % 97 + 1),
      "--fps", String(row.x?.fps ?? 30), ...(until ? ["--until", String(until)] : []), ...(row.x?.from ? ["--from", String(row.x.from)] : []), ...(row.x?.center ? ["--center"] : []), "--install", "--replace"], { encoding: "utf8" });
    if (r.status !== 0) failed.push(clip + " " + (r.stderr || r.stdout).slice(0, 200)); else done++;
  }
}
{
  const rows = Object.entries(SKILLS).map(([id, s]) => `            {"${id}",SigCastStyle.${s.Cast.style[0].toUpperCase() + s.Cast.style.slice(1)}},`).join("\n");
  writeFileSync(GENERATED, `using System.Collections.Generic;

namespace RuinRunner
{
    public enum SigCastStyle { Aim, Front, None }

    /// <summary>How each weapon's W&lt;Id&gt;Cast clip is placed. Generated by Tools/Art/weapons70-skillfx.mjs; edit that.</summary>
    public static class SignatureCastTable
    {
        public static readonly Dictionary<string,SigCastStyle> Styles=new Dictionary<string,SigCastStyle>
        {
${rows}
        };
    }
}
`);
}
console.log("exported", done, "failed", failed.length);
for (const f of failed) console.log(" FAIL " + f);
process.exit(failed.length ? 1 : 0);
