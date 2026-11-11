#!/usr/bin/env node
// 무기 70종의 전용 pixelfx 클립 표. 각 행이 CombatFX/W<Id><Role> 클립 하나가 된다.
//   node Tools/Art/weapons70-fx.mjs            전체 내보내기(이미 있으면 건너뜀)
//   node Tools/Art/weapons70-fx.mjs --force ember_claw frost_fang    지정 무기 다시 내보내기
//   node Tools/Art/weapons70-fx.mjs --list      클립 이름만 출력
import { spawnSync } from "node:child_process";
import { existsSync, rmSync, readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url)), ROOT = join(HERE, "..", "..");
const COMBATFX = join(ROOT, "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX");

const PAL = {
  silver: ["#141820", "#2a3240", "#4a5468", "#7a8698", "#b4bfcf", "#e4eaf2", "#ffffff"],
  steel: ["#14161c", "#2c3038", "#4a4f5a", "#70757f", "#9ea3ac", "#d0d4da", "#f6f7f8"],
  blood: ["#14040a", "#3a0a14", "#6a101c", "#a01824", "#d42a30", "#ff5a50", "#ffc0b0"],
  ice: ["#0c1430", "#1f4f94", "#2f7fcc", "#58b4ec", "#9bdcff", "#d8f4ff", "#ffffff"],
  poison: ["#0d1a0c", "#173a14", "#265e18", "#3f8a1e", "#6cb82a", "#a8e04a", "#e0ff8a"],
  ember: ["#2a0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"],
  lava: ["#14060a", "#380a10", "#6e1210", "#ac2410", "#e8501a", "#ff8c2a", "#ffd25a"],
  shadow: ["#0c0814", "#241638", "#3c2460", "#5e3290", "#8048b8", "#a970e0", "#e0c8ff"],
  gold: ["#2a1c08", "#5a3e10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6"],
  night: ["#0a0a24", "#191648", "#2b2478", "#4638b0", "#7a6ce0", "#b8aaff", "#f4eeff"],
  wood: ["#160c06", "#2e1a0c", "#4a2c14", "#6a421e", "#8e5c2c", "#b47c40", "#e0b478"],
  iron: ["#101218", "#262a34", "#3e4452", "#5e6674", "#88909c", "#bcc2ca", "#f0f2f4"],
  volt: ["#24160a", "#5e3a0c", "#a8680f", "#e8a218", "#ffd23c", "#fff08a", "#ffffff"],
  gravity: ["#0c0614", "#22103c", "#3e1a6c", "#6a2ca8", "#9a48e0", "#cc90ff", "#f2e0ff"],
  scarlet: ["#100406", "#30080c", "#5a1014", "#92181c", "#cc2c28", "#f06050", "#ffd0c0"],
  ash: ["#14141a", "#2a2a30", "#44444c", "#66666e", "#8c8c94", "#b8b8be", "#e8e8ea"],
  earth: ["#140e0a", "#2a1e14", "#47321e", "#69492a", "#8e6638", "#b88a52", "#e6c08a"],
  moon: ["#10182c", "#26365a", "#3e5a88", "#6a8cb8", "#a0c0e0", "#d8e8f6", "#ffffff"],
  wind: ["#1e2a2e", "#3e5258", "#6a8488", "#9ab4b6", "#c8dcdc", "#ecf6f6", "#ffffff"],
  sakura: ["#2c0e20", "#5e1c40", "#a03068", "#d85a90", "#ff90b8", "#ffc8dc", "#fff4f8"],
  ink: ["#060608", "#101014", "#1c1c22", "#32323a", "#5a5a64", "#a0a0aa", "#f0f0f4"],
  blueblack: ["#080e18", "#121e30", "#1e3450", "#305478", "#4f80a8", "#8cbcd8", "#dcf2ff"],
  water: ["#081830", "#0e2e5a", "#14508c", "#1c8ebc", "#36c2d0", "#88e8e0", "#e0fff8"],
  leaf: ["#0c1a0e", "#163418", "#225224", "#357530", "#4f9a3a", "#7cc048", "#d4f090"],
  war: ["#180a0a", "#3c1412", "#6c1e18", "#a03020", "#d0502c", "#f09050", "#ffe0b0"],
  bone: ["#1a1814", "#3a352c", "#5e5648", "#8a806c", "#b8ad96", "#e4dcc8", "#fffaf0"],
  polar: ["#060a20", "#10184a", "#1e2c80", "#3a54c0", "#7c9cf0", "#c4d8ff", "#ffffff"],
  arcblue: ["#0a0f2e", "#172a78", "#2550d0", "#3f8cff", "#7cc8ff", "#c8f0ff", "#ffffff"],
  stone: ["#0e0e12", "#1c1c24", "#2c2c38", "#42424f", "#6a6a78", "#9c9caa", "#d8d8e0"],
  storm: ["#0a0f2e", "#1a2050", "#2a3080", "#3f6cdf", "#7cc8ff", "#fff08a", "#ffffff"],
  soul: ["#0a1c20", "#123c40", "#1c6868", "#34a0a0", "#68d8d0", "#b0f4ec", "#f4fffc"],
  white: ["#2a2c36", "#50566a", "#8088a0", "#b0b8cc", "#d8dee8", "#f4f6fa", "#ffffff"],
  prismA: ["#2a1c08", "#6a4a10", "#b08018", "#e8b828", "#ffe050", "#fff4a0", "#ffffff"],
  prismB: ["#2c0820", "#6a1450", "#a82088", "#e040c0", "#ff80e0", "#ffc8f4", "#ffffff"],
  brass: ["#201406", "#4a300c", "#7e5414", "#b8821c", "#e4ae34", "#f8d878", "#fff6d0"],
  mono: ["#08080c", "#1a1a20", "#34343c", "#6c6c74", "#a8a8b0", "#dcdce0", "#ffffff"],
  teal: ["#06141a", "#0e2e36", "#165058", "#1e8080", "#b08a3c", "#e8c870", "#fff4c8"],
};
const SPARKS = { ember: ["#fff3c4", "#ffd166", "#ff8a2a", "#c42a1a"] };
const sparksOf = (p) => [PAL[p][6], PAL[p][5], PAL[p][4], PAL[p][3]];

// 작은 칼끝 반지름 표기를 그대로 두고 주인공 베기 크기로 키운다. 대검 내려치기처럼 이미 큰 것은 그대로다.
const arc = (p, o = {}) => {
  const R = o.R ?? 31, W = o.W ?? 8, big = R >= 60;
  return ["weapons/hero-slash", { palette: PAL[p], ...o, R: big ? R : Math.round(R * 1.7), W: big ? W : Math.round(W * 3.2) }];
};
const stab = (p, o = {}) => ["weapons/stab", { palette: PAL[p], sparks: sparksOf(p), ...o }];
const star = (p, o = {}) => ["weapons/hitstar", { palette: PAL[p], sparks: sparksOf(p), ...o }];
const burst = (p, kind, size = 1, o = {}) => ["weapons/burst", { palette: PAL[p], sparks: sparksOf(p), kind, size, ...o }];
const ring = (p, r, o = {}) => ["weapons/ring", { palette: PAL[p], r, ...o }];
const spike = (p, h, o = {}) => ["weapons/spike", { palette: PAL[p], h, ...o }];
const zap = (p, o = {}) => ["weapons/zap", { palette: PAL[p], sparks: sparksOf(p), ...o }];
const line = (p, style, W = 5, o = {}) => ["weapons/line", { palette: PAL[p], style, W, ...o }];
const tether = (p, style, W = 3, o = {}) => ["weapons/line-loop", { palette: PAL[p], style, W, ...o }];
const puff = (p, o = {}) => ["weapons/puff", { palette: PAL[p], ...o }];
const flash = (p, o = {}) => ["weapons/flash", { palette: PAL[p], sparks: sparksOf(p), ...o }];
const bolt = (p, kind, size = 1, o = {}) => ["weapons/bolt", { palette: PAL[p], kind, size, ...o }];
const mark = (p, shape, size = 1, o = {}) => ["weapons/mark", { palette: PAL[p], shape, size, ...o }];

const dagger = (p, s = {}, t = {}) => ({ Slash: arc(p, s), Thrust: stab(p, t), Hit: star(p, { len: 12 }) });
const great = (p, s = {}) => ({ Slash: arc(p, { R: 45, W: 11, ...s }), Cleave: arc(p, { R: 72, W: 46, a0: -170, a1: 100, over: 0, ...s }), Hit: star(p, { len: 17 }) });
const katana = (p, s = {}) => ({ Slash: arc(p, { R: 34, W: 7, ...s }), Cross: arc(p, { R: 34, W: 6, cross: true, ...s }), Hit: star(p, { len: 13 }) });
const spear = (p, t = {}) => ({ Thrust: stab(p, { LEN: 72, W: 7, ...t }), Slash: arc(p, { R: 46, W: 8 }), Hit: star(p, { len: 14 }) });
const staff = (p, kind = "magic", size = 1) => ({ Shot: bolt(p, kind, size), Muzzle: flash(p, { len: 9, wide: 4 }), Hit: star(p, { len: 11 }) });
const orbs = (p) => ({ Hit: star(p, { len: 13 }) });
const gun = (p, o = {}) => ({ Shot: bolt(p, "tracer", 1, { tail: 14 }), Muzzle: flash(p, { len: 13, wide: 4, ...o }), Hit: star(p, { len: 10 }) });

export const TABLE = {
  alley_awl: { ...dagger("silver", { W: 5 }, { W: 5 }), C: line("silver", "slash", 9) },
  saw_fang: { ...dagger("blood", { serrate: 3.5, W: 9 }), A: mark("blood", "drop", 0.9) },
  acrobat_knife: { ...dagger("silver"), A: bolt("silver", "knife", 1.8, { tail: 12 }) },
  frost_fang: { ...dagger("ice", { W: 6 }), A: mark("ice", "chip"), B: burst("ice", "ice", 0.75, { scorch: false }) },
  viper_tongue: { ...dagger("poison"), Thrust: stab("poison", { fork: 8, W: 9 }), A: mark("poison", "drop"), B: burst("poison", "poison", 1.1, { scorch: false }), C: mark("poison", "drop", 0.5) },
  ember_claw: { ...dagger("ember", { curl: 15 }), A: mark("ember", "ember"), B: burst("ember", "fire") },
  shadow_suture: { ...dagger("shadow", { W: 6 }), A: tether("shadow", "beam", 1.5), B: line("shadow", "slash", 9) },
  blood_bank: { ...dagger("blood"), C: burst("blood", "blood", 1.1, { scorch: false }), A1: mark("blood", "gauge", 1, { fill: 0.2 }), A2: mark("blood", "gauge", 1, { fill: 0.4 }), A3: mark("blood", "gauge", 1, { fill: 0.6 }), A4: mark("blood", "gauge", 1, { fill: 0.8 }), A5: mark("blood", "gauge", 1, { fill: 1 }) },
  hour_minute: { ...dagger("gold", { W: 6 }), B: line("gold", "slash", 3), C: tether("gold", "dash", 1) },
  night_thread: { ...dagger("night"), A: mark("night", "star", 1.5), B: line("night", "thread", 2, { taper: false }), C: line("night", "beam", 4, { taper: false }) },

  merc_slab: { ...great("steel"), A: burst("steel", "rock", 0.9, { scorch: false }) },
  lumber_blade: { ...great("wood"), A: puff("wood", { dir: 1, n: 7, motes: 8 }) },
  warden_door: { ...great("iron"), A: star("volt", { len: 13, sparksN: 12 }), C: puff("earth", { dir: 1, n: 8 }) },
  cinder_cleaver: { ...great("lava"), A: line("lava", "crack", 5, { taper: true }), B: burst("lava", "fire", 0.7, { ring: false }) },
  glacier_tomb: { ...great("ice"), A: spike("ice", 22, { w: 8 }), B: spike("ice", 32, { w: 10 }), C: spike("ice", 46, { w: 13 }) },
  thunder_slug: { ...great("volt"), A: mark("volt", "nail"), B: zap("volt"), C: ring("volt", 34, { dotted: true, squash: 0.45 }) },
  gravity_stele: { ...great("gravity"), A: mark("gravity", "field", 1.1), B: burst("gravity", "void", 1.3, { scorch: false }), C: ring("gravity", 54, { inward: true, thick: 4 }) },
  executioner_scale: { ...great("scarlet"), A: mark("scarlet", "scale"), B: line("scarlet", "slash", 8), C: burst("ash", "soul", 0.8, { ring: false, scorch: false }) },
  dawn_gate: { ...great("gold"), A: ["weapons/gate-open", { palette: PAL.gold }], B: line("gold", "beam", 15, { taper: false }), C: line("gold", "beam", 8, { taper: false }) },
  continent_scar: { ...great("earth"), A: line("earth", "crack", 7), B: burst("earth", "rock", 0.8, { scorch: false, ring: false }), C: burst("earth", "rock", 1.8) },

  trainee_blade: { ...katana("steel", { W: 5 }), A: line("steel", "slash", 3), B: burst("silver", "shard", 0.4, { ring: false, scorch: false }) },
  wanderer_blade: { ...katana("ash"), A: line("ash", "slash", 6), B: puff("ash", { dir: -1, kind: "smoke", n: 4 }) },
  ironback_blade: { ...katana("iron", { W: 9 }), A: star("volt", { len: 14, sparksN: 12 }) },
  moon_white: { ...katana("moon"), A: bolt("moon", "crescent", 1, { tail: 8 }), B: star("moon", { len: 13 }) },
  red_lotus: { ...katana("scarlet"), A: line("scarlet", "slash", 5, { taper: false }) },
  wind_cutter: { ...katana("wind", { gaps: 0 }), A: mark("wind", "portal", 1.7, { spin: 3 }), B: bolt("wind", "crescent", 1.8, { tail: 30 }) },
  falling_blossom: { ...katana("sakura"), A: burst("sakura", "flower", 0.42, { ring: false, scorch: false }), B: burst("sakura", "flower", 1.45, { scorch: false }), C: burst("sakura", "flower", 0.55, { ring: false, scorch: false }), D: burst("sakura", "flower", 0.7, { ring: false, scorch: false }) },
  mu_eum: { ...katana("ink", { W: 3 }), A: mark("ink", "cross", 1.4), B: star("ink", { len: 14, sparksN: 8 }), C: line("ink", "slash", 10) },
  instant_king: { ...katana("gold"), A: star("gold", { len: 15 }), B: line("gold", "slash", 9), C: burst("gold", "shard", 0.5, { ring: false, scorch: false }), E: tether("gold", "beam", 2) },
  thousand_returns: { ...katana("blueblack"), A: mark("blueblack", "cross", 1.5), B: arc("blueblack", { R: 72, W: 46, a0: -170, a1: 100, over: 0 }) },

  sentry_spear: { ...spear("steel") },
  hunt_harpoon: { ...spear("iron"), A: bolt("iron", "shard", 2.8, { tail: 22 }), B: ["weapons/chain-long", { palette: PAL.wood }] },
  side_blade_pike: { ...spear("silver"), A: arc("silver", { R: 80, W: 44, a0: -180, a1: 180, over: 0 }), B: puff("earth", { dir: 1, n: 7 }) },
  thunder_needle: { ...spear("volt"), A: mark("volt", "electrode", 1.6), B: line("volt", "zap", 3, { taper: false }), D: burst("volt", "shock", 1.3, { scorch: false }) },
  tide_fang: { ...spear("water"), A: line("water", "water", 9), B: line("water", "water", 5) },
  thorn_spear: { ...spear("leaf"), A: line("leaf", "crack", 5), B: mark("leaf", "vine", 1.3) },
  chain_horizon: { ...spear("steel"), A: ["weapons/chain-long", { palette: PAL.steel }], B: flash("steel", { len: 16, wide: 5, sparksN: 2 }), D: bolt("steel", "shard", 2.6, { tail: 10 }), E: burst("silver", "shard", 1.1, { scorch: false }) },
  bannerless: { ...spear("war"), C: mark("war", "spear", 1.2) },
  dragon_spine: { ...spear("bone"), A: mark("bone", "node", 1.6), B: line("bone", "slash", 6), C: spike("bone", 44, { w: 9, curve: 9 }), D: burst("bone", "shard", 0.6, { ring: false, scorch: false }) },
  polaris: { ...spear("polar"), A: mark("polar", "star", 1.4), B: ring("polar", 14, { inward: true, thick: 2 }) },

  novice_branch: { ...staff("arcblue"), B: bolt("arcblue", "magic", 1.7) },
  charcoal_staff: { ...staff("ember", "fire", 0.8), A: bolt("ember", "fire", 0.45, { tail: 4 }), B: star("ember", { len: 7, sparksN: 4 }) },
  pebble_staff: { ...staff("stone", "shard"), A: spike("stone", 30, { w: 16 }), B: burst("stone", "rock", 0.75, { scorch: false }) },
  frost_branch: { ...staff("ice", "ice", 0.8), A: bolt("ice", "ice", 1.25), B: bolt("ice", "shard", 0.8), C: burst("ice", "ice", 0.7, { scorch: false }) },
  storm_cloud_staff: { ...staff("storm", "spark", 0.8) },
  swamp_lantern: { ...staff("poison", "poison", 0.8), A: bolt("poison", "poison", 1.25), B: mark("poison", "puddle", 1.05), C: burst("poison", "poison", 0.8, { scorch: false }) },
  refractor_staff: { ...staff("white"), A: mark("white", "prism", 1.6), C: bolt("prismA", "magic"), D: bolt("prismB", "magic") },
  undertaker_candle: { ...staff("soul", "soul", 0.7), A: bolt("soul", "soul"), B: burst("soul", "soul", 0.95, { scorch: false }) },
  star_fold: { ...staff("night"), A: mark("night", "portal", 1.7, { spin: 1 }) },
  season_end: { ...staff("leaf") },

  whetstone_orb: { ...orbs("stone"), D: arc("silver", { R: 83, W: 30, a0: -180, a1: 180, over: 0 }) },
  lantern_orb: { ...orbs("gold"), Shot: bolt("gold", "magic", 1.3, { tail: 14 }), A: mark("gold", "circle"), B: flash("gold", { len: 10, wide: 4 }) },
  iron_core: { ...orbs("iron"), A: bolt("iron", "shard", 1.7, { tail: 12 }), B: line("iron", "zap", 2, { taper: false }) },
  frost_satellite: { ...orbs("ice") },
  ember_heart: { ...orbs("lava"), A1: mark("lava", "coal", 1, { fill: 0.2 }), A2: mark("lava", "coal", 1, { fill: 0.4 }), A3: mark("lava", "coal", 1, { fill: 0.6 }), A4: mark("lava", "coal", 1, { fill: 0.8 }), A5: mark("lava", "coal", 1, { fill: 1 }) },
  storm_marble: { ...orbs("storm"), D: mark("arcblue", "cross", 1.6) },
  twin_star: { ...orbs("mono"), Shot: mark("white", "orbitdot", 1.6), A: mark("mono", "half", 1, { side: -1 }), B: mark("mono", "half", 1, { side: 1 }), D: tether("white", "beam", 7) },
  mirror_eye: { ...orbs("moon"), A: mark("moon", "mirror", 1.3), D: bolt("moon", "shard", 1, { tail: 12 }), F: flash("moon", { len: 12, wide: 6 }) },
  small_eclipse: { ...orbs("gold") },
  world_pendulum: { ...orbs("teal"), A: mark("teal", "orbitdot", 1.8), B: bolt("teal", "magic", 1.5, { tail: 24 }) },

  scrap_revolver: { ...gun("brass", { len: 22, wide: 7 }), Shot: bolt("brass", "tracer", 1.5, { tail: 22 }), A: mark("ash", "smoke") },
  gate_shotgun: { ...gun("ember", { len: 18, wide: 5 }), Shot: bolt("ember", "tracer", 1.3, { tail: 12 }), A: flash("ember", { len: 26, wide: 8, twin: 5, sparksN: 8 }), B: puff("earth", { dir: 1, n: 4 }) },
  scout_carbine: { ...gun("steel", { len: 9, wide: 3 }), A: mark("white", "reticle"), C: tether("white", "dash", 1) },
  furnace_sprayer: { ...gun("ember"), Shot: bolt("ember", "fire", 0.6, { tail: 4 }), A: bolt("ember", "fire", 1.2) },
  frost_bolt_rifle: { ...gun("ice", { len: 12, wide: 4 }), A: puff("ice", { kind: "smoke", dir: 1, n: 5 }), B: bolt("ice", "ice", 1.5, { tail: 22 }), D1: mark("ice", "chip", 0.7), D2: mark("ice", "chip", 1.1) },
  ricochet_pistol: { ...gun("brass"), A: bolt("brass", "tracer", 1.8, { tail: 34 }) },
  lightning_carbine: { ...gun("volt"), A: bolt("volt", "tracer", 1.6, { tail: 24 }), B: mark("volt", "electrode", 1.4), C: line("volt", "zap", 3, { taper: false }) },
  siege_engineer: { ...gun("brass", { len: 14, wide: 5 }), A: mark("brass", "node", 0.5), B: mark("ash", "smoke"), C: mark("brass", "deploy", 1.5), E: tether("brass", "dash", 1) },
  last_bell: { ...gun("white", { len: 12, wide: 3 }), A: mark("brass", "bell", 1, { closing: true }), B: line("white", "beam", 6, { taper: false }) },
  meteor_waybill: { ...gun("ember"), Shot: bolt("iron", "grenade", 1, { tail: 8 }), Hit: burst("ember", "fire", 0.95), A1: mark("ember", "coord", 1, { count: 1 }), A2: mark("ember", "coord", 1, { count: 2 }), A3: mark("ember", "coord", 1, { count: 3 }) },
};

const pascal = (id) => id.split("_").map((s) => s[0].toUpperCase() + s.slice(1)).join("");
const args = process.argv.slice(2), force = args.includes("--force"), only = args.filter((a) => !a.startsWith("--") && !a.includes(":"));
// --force roles:Slash,Cleave,Cross 지정한 동작만 다시 내보낸다.
const roles = args.find((a) => a.startsWith("roles:"))?.slice(6).split(",");
let done = 0, skipped = 0, failed = [];
const ids = Object.keys(TABLE);
if (args.includes("--list")) { for (const id of ids) for (const r of Object.keys(TABLE[id])) console.log("W" + pascal(id) + r); process.exit(0); }
for (const id of ids) {
  if (only.length && !only.includes(id)) continue;
  for (const [role, [effect, opts]] of Object.entries(TABLE[id])) {
    if (roles && !roles.includes(role)) continue;
    const clip = "W" + pascal(id) + role, dest = join(COMBATFX, clip);
    if (existsSync(dest) && !force) { skipped++; continue; }
    if (existsSync(dest)) { rmSync(dest, { recursive: true, force: true }); rmSync(dest + ".meta", { force: true }); }
    rmSync(join(HERE, "Staging/PixelFx", clip), { recursive: true, force: true });
    const r = spawnSync(process.execPath, [join(HERE, "pixelfx-to-unity.mjs"), effect, clip, "--opts", JSON.stringify(opts), "--seed", String((done * 7) % 97 + 1), "--install"], { cwd: ROOT, encoding: "utf8" });
    if (r.status !== 0) { failed.push(clip + ": " + (r.stderr || r.stdout).split("\n").slice(0, 3).join(" ")); continue; }
    done++;
  }
}
console.log(`exported ${done}, skipped ${skipped}, failed ${failed.length}`);
for (const f of failed) console.log(" FAIL " + f);
