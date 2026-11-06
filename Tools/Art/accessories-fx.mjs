#!/usr/bin/env node
// 패시브 장신구 20종의 연출. pixelfx 완성 이펙트를 장신구마다 고르고 칠해 WAcc<이름><역할> 클립으로 내보낸다.
// 쓰는 곳: Assets/RuinRunner/Runtime/CombatActions/ItemCombatEffects.Passives.cs
//   node Tools/Art/accessories-fx.mjs                전체 내보내기
//   node Tools/Art/accessories-fx.mjs thunder_charm  지정 장신구만
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, rmSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url)), ROOT = join(HERE, "..", "..");
const COMBATFX = join(ROOT, "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX");
// [이펙트, 팔레트, 추가 옵션, 시작 초]. 역할 ""가 장신구의 대표 클립이다. 패시브는 맞는 순간 터져야 하므로 긴 전조는 시작 초로 잘라 낸다.
const C = (fx, p = null, o = {}, from = 0) => ({ fx, p, o, from });
const STRIKE_FROM = 0.6;
export const PASSIVES = {
  retribution_thorn: { "": C("nature/earth-spike", "scarlet") },
  mirror_shard: { "": C("melee/thrust", "moon") },
  cinder_seed: { "": C("fire/flame-pillar", "ember") },
  thunder_charm: { "": C("lightning/lightning-strike", "volt", {}, STRIKE_FROM) },
  poison_censer: { "": C("nature/toxic-puddle", "poison") },
  chain_spark: { "": C("lightning/zap-hit", "volt"), Line: C("weapons/line", "volt", { style: "zap", W: 3 }) },
  soul_lantern: { "": C("fire/spirit-flame", "soul"), Bolt: C("projectiles/magic-missile", "soul"), Hit: C("fire/spirit-flame", "soul") },
  vine_snare: { "": C("nature/root-snare", "leaf") },
  shadow_twin_ring: { "": C("melee/double-slash", "shadow") },
  quake_bell: { "": C("impacts/ground-slam", "brass", { stone: "#6a4a2a" }) },
  frost_wave_necklace: { "": C("ice/frost-nova", "ice") },
  guardian_feather: { "": C("holy/feather-burst") },
  blood_pact: { "": C("explosions/implosion", "blood") },
  swap_flash: { "": C("arcane/mana-burst", "prismB") },
  meteor_compass: { "": C("fire/meteor", "ember") },
  gravity_pearl: { "": C("shadow/black-hole", "gravity"), Burst: C("explosions/implosion", "gravity") },
  time_hourglass: { "": C("arcane/time-stop", "arcblue") },
  holy_tithe: { "": C("holy/smite", "gold") },
  storm_core: { "": C("lightning/lightning-strike", "storm", {}, STRIKE_FROM) },
  phoenix_ash: { "": C("explosions/big-explosion", "ember"), Ring: C("fire/fire-ring", "lava") },
};

const pascal = (id) => id.split("_").map((s) => s[0].toUpperCase() + s.slice(1)).join("");
const palettes = readFileSync(join(HERE, "weapons70-fx.mjs"), "utf8");
const PAL = Object.fromEntries([...palettes.matchAll(/^\s+(\w+): (\[(?:"#[0-9a-f]+"(?:, )?)+\]),$/gm)].map((m) => [m[1], JSON.parse(m[2])]));
const only = process.argv.slice(2).filter((a) => !a.startsWith("--"));
const ids = only.length ? only : Object.keys(PASSIVES);
let done = 0; const failed = [];
for (const id of ids) {
  for (const [role, row] of Object.entries(PASSIVES[id])) {
    if (!existsSync(join(ROOT, "Tools/pixelfx/src/fx", row.fx + ".js"))) { failed.push(id + " " + role + " 없는 이펙트 " + row.fx); continue; }
    if (row.p && !PAL[row.p]) { failed.push(id + " " + role + " 없는 팔레트 " + row.p); continue; }
    const clip = "WAcc" + pascal(id) + role, dest = join(COMBATFX, clip);
    if (existsSync(dest)) { rmSync(dest, { recursive: true, force: true }); rmSync(dest + ".meta", { force: true }); }
    rmSync(join(HERE, "Staging/PixelFx", clip), { recursive: true, force: true });
    const opts = { ...(row.p ? { palette: PAL[row.p] } : {}), ...row.o };
    const r = spawnSync(process.execPath, [join(HERE, "pixelfx-to-unity.mjs"), row.fx, clip, "--opts", JSON.stringify(opts), "--seed", String((done * 13) % 97 + 1),
      "--fps", "30", ...(row.from ? ["--from", String(row.from)] : []), "--until", String(row.from + 1.25), "--install", "--replace"], { encoding: "utf8" });
    if (r.status !== 0) failed.push(clip + " " + (r.stderr || r.stdout).slice(0, 200)); else done++;
  }
}
console.log("exported", done, "failed", failed.length);
for (const f of failed) console.log(" FAIL " + f);
process.exit(failed.length ? 1 : 0);
