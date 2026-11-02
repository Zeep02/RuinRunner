// Ruin Runner 이미지 테스트 초안: 이동, 칼 휘두르기, 스킬 하나. 이펙트는 pixelfx 코드가 실시간으로 그린다.
import { createFrame, clearFrame, createFx, composite } from "../pixelfx/src/core/index.js";
import * as swordSlash from "../pixelfx/src/fx/melee/sword-slash.js";
import * as frostNova from "../pixelfx/src/fx/ice/frost-nova.js";
import * as hitSpark from "../pixelfx/src/fx/impacts/hit-spark.js";
import * as poof from "../pixelfx/src/fx/spawn/poof.js";

const W = 384, H = 216, TAU = Math.PI * 2;
const $ = (id) => document.getElementById(id);
const screen = $("screen"), sctx = screen.getContext("2d");
screen.width = W; screen.height = H;
sctx.imageSmoothingEnabled = false;
const fxCanvas = document.createElement("canvas");
fxCanvas.width = W; fxCanvas.height = H;
const fxCtx = fxCanvas.getContext("2d");
const frame = createFrame(W, H), fxImg = fxCtx.createImageData(W, H);

// ---------- 설정 ----------
const WHITE = ["#9fb0b4", "#cfdadb", "#eef4f3", "#ffffff", "#ffffff", "#ffffff", "#ffffff"];
const cfg = { style: "white", light: 0.15, hitstop: true, shake: true };
function slashOpts() {
  return cfg.style === "white"
    ? { palette: WHITE, sparks: ["#ffffff", "#eef4f3", "#cfdadb", "#9fb0b4"], radius: 24, light: cfg.light }
    : { radius: 24, light: Math.max(cfg.light, 0.6) };
}
function sparkOpts() {
  return cfg.style === "white" ? { palette: WHITE, height: 6, light: cfg.light } : { height: 6, light: Math.max(cfg.light, 0.6) };
}
function poofOpts() {
  return cfg.style === "white" ? { palette: ["#7d8e92", "#b9c6c9", "#dfe7e8", "#f4f8f8", "#ffffff", "#ffffff"], stars: ["#ffffff", "#ffffff", "#eef4f3"], light: cfg.light }
    : { light: Math.max(cfg.light, 0.6) };
}
function novaOpts() { return { reach: 52, light: cfg.style === "white" ? Math.max(cfg.light, 0.25) : 0.8 }; }

// ---------- 효과 목록 ----------
let effects = [];
function spawnFx(mod, x, y, opts = {}, layer = "front", speed = 1) {
  const fx = createFx(mod, { x: Math.round(x), y: Math.round(y), ...opts });
  effects.push({ fx, layer, speed });
  return fx;
}

// 옆에서 본 참격의 무게중심을 한 번 재 두고, 탑다운 조준 방향 앞에 오도록 회전·배치한다.
function centroid(mod, opts, t) {
  const S = 256, f = createFrame(S, S), fx = createFx(mod, { x: S / 2, y: S / 2, ...opts });
  for (let i = 0; i < Math.round(t * 120); i++) fx.update(1 / 120);
  fx.draw(f);
  let sx = 0, sy = 0, n = 0;
  for (let i = 0; i < S * S; i++) if (f.color[i] >>> 24) { sx += i % S; sy += (i / S) | 0; n++; }
  return n ? [sx / n - S / 2, sy / n - S / 2] : [20, -20];
}
let slashC = null;
function measureSlash() { slashC = centroid(swordSlash, slashOpts(), 0.08); }

// ---------- 픽셀 스프라이트 ----------
const PAL = {
  o: "#22161f", h: "#2f2333", H: "#4a3850", s: "#f0c49a", S: "#c98f6a", e: "#22161f",
  r: "#d4483a", R: "#9a2c2b", t: "#7a5a3c", T: "#5a4130", b: "#3b2a24", g: "#8e9aa0",
};
const HERO = [
  "....oooo....",
  "...ohhhHo...",
  "..ohhhhhHo..",
  "..ohsshhho..",
  "..osssssho..",
  "..oseseso...",
  "..osssssSo..",
  "...orrrro...",
  "..orrRrrro..",
  ".otrRtttTto.",
  ".ostttttTso.",
  "..otttTtto..",
  "..obbbbbbo..",
];
const LEGS = [
  ["..oTo.oTo...", "..obo.obo...", "..oo..oo...."],
  ["..oTo..oTo..", "..obo..obo..", "..oo...oo..."],
  ["...oTooTo...", "...obo.obo..", "...oo..oo..."],
];
const SLIME = [
  "....oooo....",
  "..oo3333oo..",
  ".o33333332o.",
  ".o3e33e332o.",
  "o3333333322o",
  "o3333333222o",
  ".o22222222o.",
  "..oooooooo..",
];
const SLIME_PAL = { o: "#1d2433", "3": "#8f6fc4", "2": "#6a4f9e", e: "#1d2433" };
function makeSprite(rows, pal, flash) {
  const c = document.createElement("canvas");
  c.width = Math.max(...rows.map((r) => r.length)); c.height = rows.length;
  const x = c.getContext("2d");
  rows.forEach((row, y) => [...row].forEach((ch, i) => {
    if (ch === ".") return;
    x.fillStyle = flash ? "#ffffff" : pal[ch] ?? "#ff00ff";
    x.fillRect(i, y, 1, 1);
  }));
  return c;
}
const heroFrames = LEGS.map((l) => makeSprite([...HERO, ...l], PAL));
const heroFlash = makeSprite([...HERO, ...LEGS[0]], PAL, true);
const slimeSprite = makeSprite(SLIME, SLIME_PAL), slimeFlash = makeSprite(SLIME, SLIME_PAL, true);

// 사용자 이미지 (원본 크기 그대로, 발밑 중앙 기준)
const custom = { hero: null, heroFrames: 1, enemy: null, tile: null };

// ---------- 바닥 ----------
let ground = null;
const TILE = 16;
function rand(seed) { let s = seed >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }
function buildGround() {
  const g = document.createElement("canvas"); g.width = W; g.height = H;
  const x = g.getContext("2d"), R = rand(7);
  if (custom.tile) {
    for (let ty = 0; ty < H; ty += custom.tile.height) for (let tx = 0; tx < W; tx += custom.tile.width) x.drawImage(custom.tile, tx, ty);
  } else {
    x.fillStyle = "#4f9a52"; x.fillRect(0, 0, W, H);
    for (let i = 0; i < 900; i++) { x.fillStyle = R() < 0.5 ? "#5aa75b" : "#458a49"; x.fillRect((R() * W) | 0, (R() * H) | 0, 1 + (R() * 2 | 0), 1); }
    // 흙길
    x.fillStyle = "#b98b56";
    for (let i = 0; i < 46; i++) { const px = 40 + i * 7 + Math.sin(i * 0.5) * 6, py = 150 + Math.sin(i * 0.33) * 18; x.fillRect(px | 0, py | 0, 12, 9); }
    x.fillStyle = "#a57a48";
    for (let i = 0; i < 120; i++) { const px = 40 + R() * 320, py = 150 + Math.sin((px - 40) / 7 * 0.33) * 18 + R() * 8; x.fillRect(px | 0, py | 0, 2, 1); }
    // 언덕 가장자리
    x.fillStyle = "#a6c96a"; x.fillRect(0, 0, W, 22);
    x.fillStyle = "#8a6a44"; for (let tx = 0; tx < W; tx += 8) x.fillRect(tx, 22, 8, 6 + ((tx / 8) % 3));
    x.fillStyle = "#6b5034"; for (let tx = 0; tx < W; tx += 8) x.fillRect(tx, 27 + ((tx / 8) % 3), 8, 2);
  }
  // 바위
  [[60, 64], [318, 96], [250, 40]].forEach(([bx, by]) => {
    x.fillStyle = "#22161f"; x.fillRect(bx - 1, by - 1, 14, 10);
    x.fillStyle = "#8f9aa0"; x.fillRect(bx, by, 12, 8);
    x.fillStyle = "#b9c3c7"; x.fillRect(bx + 1, by + 1, 7, 3);
    x.fillStyle = "#66717a"; x.fillRect(bx, by + 6, 12, 2);
  });
  ground = g;
}

// ---------- 월드 ----------
const keys = new Set();
const player = { x: 192, y: 120, vx: 0, vy: 0, face: 1, aim: 0, walk: 0, hp: 5, hurt: 0, swing: 0, swingDir: 1, swingAim: 0, cd: 0, skillCd: 0, combo: 0 };
let enemies = [], grass = [], bits = [], kills = 0, hitstop = 0, shake = 0, time = 0, wave = 0;
const SKILL_CD = 3.5;

function spawnGrass() {
  const R = rand(11); grass = [];
  for (let i = 0; i < 26; i++) {
    const x = 24 + R() * (W - 48), y = 44 + R() * (H - 64);
    if (Math.hypot(x - 192, y - 120) < 30) continue;
    grass.push({ x, y, h: 7 + (R() * 5 | 0), alive: true, sway: R() * TAU });
  }
}
function spawnWave() {
  wave++;
  const n = 3 + Math.min(wave, 5);
  for (let i = 0; i < n; i++) {
    const side = i % 4, t = Math.random();
    const x = side === 0 ? 10 : side === 1 ? W - 10 : 20 + t * (W - 40);
    const y = side === 2 ? 40 : side === 3 ? H - 10 : 40 + t * (H - 60);
    enemies.push({ x, y, vx: 0, vy: 0, hp: 3, flash: 0, frozen: 0, hop: Math.random() * TAU });
  }
}

function leafBurst(x, y, n, colors = ["#2f6b3a", "#3f8a45", "#6fbf5f", "#a6d66a"]) {
  for (let i = 0; i < n; i++) {
    const a = Math.random() * TAU, s = 30 + Math.random() * 70;
    bits.push({ x, y, z: 2 + Math.random() * 6, vx: Math.cos(a) * s, vy: Math.sin(a) * s * 0.6, vz: 40 + Math.random() * 60, life: 0.6 + Math.random() * 0.6, c: colors[(Math.random() * colors.length) | 0], spin: Math.random() < 0.5 });
  }
}

function angleDiff(a, b) { let d = (a - b) % TAU; if (d > Math.PI) d -= TAU; if (d < -Math.PI) d += TAU; return d; }

function attack() {
  if (player.cd > 0) return;
  player.cd = 0.3; player.swing = 0.16; player.swingDir = player.combo % 2 ? -1 : 1; player.combo++;
  const a = player.aim; player.swingAim = a;
  const flip = player.swingDir < 0;
  const [cx, cy] = slashC, lx = flip ? -cx : cx;
  const rot = a - Math.atan2(cy, lx);
  const rx = lx * Math.cos(rot) - cy * Math.sin(rot), ry = lx * Math.sin(rot) + cy * Math.cos(rot);
  const reach = 18, hx = player.x, hy = player.y - 8;
  spawnFx(swordSlash, hx + Math.cos(a) * reach - rx, hy + Math.sin(a) * reach - ry, { ...slashOpts(), angle: rot, flip, seed: player.combo }, "front", 1.15);
  // 판정: 앞쪽 부채꼴
  let hit = false;
  for (const e of enemies) {
    const dx = e.x - hx, dy = e.y - 4 - hy, d = Math.hypot(dx, dy);
    if (d < 38 && Math.abs(angleDiff(Math.atan2(dy, dx), a)) < 1.25) { damage(e, 1, a); hit = true; }
  }
  for (const g of grass) {
    if (!g.alive) continue;
    const dx = g.x - hx, dy = g.y - hy, d = Math.hypot(dx, dy);
    if (d < 36 && Math.abs(angleDiff(Math.atan2(dy, dx), a)) < 1.3) { g.alive = false; leafBurst(g.x, g.y - 3, 9); }
  }
  if (hit) { if (cfg.hitstop) hitstop = 0.07; if (cfg.shake) shake = 0.12; }
}

function skill() {
  if (player.skillCd > 0) return;
  player.skillCd = SKILL_CD;
  spawnFx(frostNova, player.x, player.y + 2, { ...novaOpts(), seed: (Math.random() * 999) | 0 }, "under", 1.4);
  for (const e of enemies) {
    const d = Math.hypot(e.x - player.x, (e.y - player.y) * 1.4);
    if (d < 70) { e.frozen = 2.2; damage(e, 2, Math.atan2(e.y - player.y, e.x - player.x), true); }
  }
  for (const g of grass) if (g.alive && Math.hypot(g.x - player.x, (g.y - player.y) * 1.4) < 60) { g.alive = false; leafBurst(g.x, g.y - 3, 6, ["#cfefff", "#8fd3f4", "#ffffff", "#3f8a45"]); }
  if (cfg.hitstop) hitstop = 0.09; if (cfg.shake) shake = 0.25;
}

function damage(e, n, dir, quiet) {
  e.hp -= n; e.flash = 0.1;
  e.vx += Math.cos(dir) * 120; e.vy += Math.sin(dir) * 120;
  if (!quiet) spawnFx(hitSpark, e.x, e.y - 4, { ...sparkOpts(), angle: dir, flip: false, seed: (Math.random() * 999) | 0 }, "front", 1);
  if (e.hp <= 0) {
    e.dead = true; kills++;
    spawnFx(poof, e.x, e.y + 2, { ...poofOpts(), seed: (Math.random() * 999) | 0 }, "front", 1.6);
  }
}

// ---------- 입력 ----------
const mouse = { x: W / 2 + 40, y: H / 2, inside: false };
let touchMove = null;
function toWorld(ev) {
  const r = screen.getBoundingClientRect();
  return { x: ((ev.clientX - r.left) / r.width) * W, y: ((ev.clientY - r.top) / r.height) * H };
}
addEventListener("keydown", (e) => {
  if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) return;
  const k = e.key.toLowerCase(); keys.add(k);
  if (k === "j") attack();
  if (k === "k" || k === " ") { skill(); e.preventDefault(); }
  if (["arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k)) e.preventDefault();
});
addEventListener("keyup", (e) => keys.delete(e.key.toLowerCase()));
addEventListener("blur", () => keys.clear());
screen.addEventListener("pointermove", (e) => { Object.assign(mouse, toWorld(e)); mouse.inside = true; if (touchMove) Object.assign(touchMove, toWorld(e)); });
screen.addEventListener("pointerdown", (e) => {
  screen.focus();
  Object.assign(mouse, toWorld(e)); mouse.inside = true;
  if (e.pointerType === "touch") { touchMove = toWorld(e); screen.setPointerCapture(e.pointerId); return; }
  if (e.button === 2) skill(); else attack();
});
screen.addEventListener("pointerup", () => { touchMove = null; });
screen.addEventListener("pointercancel", () => { touchMove = null; });
screen.addEventListener("contextmenu", (e) => e.preventDefault());
$("btn-attack").addEventListener("click", () => { aimNearest(); attack(); });
$("btn-skill").addEventListener("click", () => skill());
function aimNearest() {
  let best = null, bd = 1e9;
  for (const e of enemies) { const d = Math.hypot(e.x - player.x, e.y - player.y); if (d < bd) { bd = d; best = e; } }
  if (best) { mouse.x = best.x; mouse.y = best.y - 4; }
}

// ---------- 업데이트 ----------
function update(dt) {
  time += dt;
  if (hitstop > 0) { hitstop -= dt; return; }
  shake = Math.max(0, shake - dt);
  let ix = 0, iy = 0;
  if (keys.has("a") || keys.has("arrowleft")) ix--;
  if (keys.has("d") || keys.has("arrowright")) ix++;
  if (keys.has("w") || keys.has("arrowup")) iy--;
  if (keys.has("s") || keys.has("arrowdown")) iy++;
  if (touchMove) { const dx = touchMove.x - player.x, dy = touchMove.y - player.y, d = Math.hypot(dx, dy); if (d > 6) { ix = dx / d; iy = dy / d; } }
  const il = Math.hypot(ix, iy) || 1, speed = player.swing > 0 ? 40 : 82;
  player.x = Math.max(8, Math.min(W - 8, player.x + (ix / il) * speed * dt));
  player.y = Math.max(40, Math.min(H - 4, player.y + (iy / il) * speed * dt));
  const moving = ix || iy;
  player.walk = moving ? player.walk + dt * 8 : 0;
  player.aim = Math.atan2(mouse.y - (player.y - 8), mouse.x - player.x);
  if (player.swing <= 0) player.face = Math.cos(player.aim) >= 0 ? 1 : -1;
  player.cd = Math.max(0, player.cd - dt); player.swing = Math.max(0, player.swing - dt);
  player.skillCd = Math.max(0, player.skillCd - dt); player.hurt = Math.max(0, player.hurt - dt);

  for (const e of enemies) {
    e.flash = Math.max(0, e.flash - dt); e.frozen = Math.max(0, e.frozen - dt); e.hop += dt * 6;
    const dx = player.x - e.x, dy = player.y - e.y, d = Math.hypot(dx, dy) || 1;
    const sp = e.frozen > 0 ? 0 : 26;
    e.vx += ((dx / d) * sp - e.vx) * Math.min(1, dt * 3); e.vy += ((dy / d) * sp - e.vy) * Math.min(1, dt * 3);
    e.x = Math.max(6, Math.min(W - 6, e.x + e.vx * dt)); e.y = Math.max(36, Math.min(H - 4, e.y + e.vy * dt));
    if (d < 10 && player.hurt <= 0 && e.frozen <= 0) { player.hurt = 0.8; player.hp = Math.max(0, player.hp - 1); if (cfg.shake) shake = 0.15; if (player.hp === 0) player.hp = 5; }
  }
  enemies = enemies.filter((e) => !e.dead);
  if (!enemies.length) { spawnWave(); if (grass.filter((g) => g.alive).length < 8) spawnGrass(); }

  for (const b of bits) {
    b.life -= dt; b.x += b.vx * dt; b.y += b.vy * dt; b.vz -= 260 * dt; b.z = Math.max(0, b.z + b.vz * dt);
    if (b.z === 0) { b.vx *= 0.8; b.vy *= 0.8; }
  }
  bits = bits.filter((b) => b.life > 0);
  for (const f of effects) f.fx.update(dt * f.speed);
  effects = effects.filter((f) => !f.fx.done());
}

// ---------- 그리기 ----------
function drawFx(layer) {
  clearFrame(frame);
  let any = false;
  for (const f of effects) if (f.layer === layer) { f.fx.draw(frame); any = true; }
  if (!any) return;
  composite(frame, fxImg.data, null);
  fxCtx.putImageData(fxImg, 0, 0);
  sctx.drawImage(fxCanvas, 0, 0);
}
function px(x, y, c) { sctx.fillStyle = c; sctx.fillRect(Math.round(x), Math.round(y), 1, 1); }
function line(x0, y0, x1, y1, c) {
  x0 = Math.round(x0); y0 = Math.round(y0); x1 = Math.round(x1); y1 = Math.round(y1);
  const dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
  let err = dx + dy;
  for (;;) { px(x0, y0, c); if (x0 === x1 && y0 === y1) break; const e2 = 2 * err; if (e2 >= dy) { err += dy; x0 += sx; } if (e2 <= dx) { err += dx; y0 += sy; } }
}
function shadow(x, y, w) { sctx.fillStyle = "rgba(20,40,24,.35)"; sctx.fillRect(Math.round(x - w / 2), Math.round(y - 1), w, 3); }

function drawSword() {
  // 휘두르는 중이면 조준 각을 가로질러 빠르게 쓸고, 아니면 몸 옆에 비스듬히 든다.
  let a;
  if (player.swing > 0) { const k = 1 - player.swing / 0.16, e = 1 - Math.pow(1 - k, 3); a = player.swingAim + player.swingDir * (-1.6 + 3.2 * e); }
  else a = player.face > 0 ? 2.2 : Math.PI - 2.2;
  const hx = player.x + player.face * 4, hy = player.y - 7, L = 15;
  const tx = hx + Math.cos(a) * L, ty = hy + Math.sin(a) * L;
  line(hx + Math.cos(a) * 3, hy + Math.sin(a) * 3 + 1, tx, ty + 1, "#22161f");
  line(hx + Math.cos(a) * 3, hy + Math.sin(a) * 3, tx, ty, "#dfe7e8");
  line(hx - Math.cos(a) * 1, hy - Math.sin(a) * 1, hx + Math.cos(a) * 2, hy + Math.sin(a) * 2, "#7a5a3c");
  px(hx + Math.cos(a + 1.57) * 2, hy + Math.sin(a + 1.57) * 2, "#d9b44a");
  px(hx - Math.cos(a + 1.57) * 2, hy - Math.sin(a + 1.57) * 2, "#d9b44a");
}

function drawSpriteAt(img, x, y, flip) {
  const w = img.width, h = img.height;
  sctx.save();
  sctx.translate(Math.round(x), Math.round(y));
  if (flip) sctx.scale(-1, 1);
  sctx.drawImage(img, -Math.floor(w / 2), -h);
  sctx.restore();
}

function drawPlayer() {
  shadow(player.x, player.y, 10);
  const swordBehind = player.swing > 0 ? Math.sin(player.swingAim) < 0 : true;
  if (swordBehind) drawSword();
  const blink = player.hurt > 0 && Math.floor(player.hurt * 20) % 2 === 0;
  if (custom.hero) {
    const n = custom.heroFrames, fw = Math.floor(custom.hero.width / n), fi = player.walk ? Math.floor(player.walk) % n : 0;
    sctx.save(); sctx.translate(Math.round(player.x), Math.round(player.y)); if (player.face < 0) sctx.scale(-1, 1);
    if (blink) sctx.globalAlpha = 0.4;
    sctx.drawImage(custom.hero, fi * fw, 0, fw, custom.hero.height, -Math.floor(fw / 2), -custom.hero.height, fw, custom.hero.height);
    sctx.restore();
  } else {
    const fi = player.walk ? 1 + (Math.floor(player.walk) % 2) : 0, bob = player.walk && Math.floor(player.walk) % 2 ? -1 : 0;
    drawSpriteAt(blink ? heroFlash : heroFrames[fi], player.x, player.y + bob, player.face < 0);
  }
  if (!swordBehind) drawSword();
}

function drawEnemy(e) {
  const hop = e.frozen > 0 ? 0 : Math.max(0, Math.sin(e.hop)) * 3;
  shadow(e.x, e.y, 10);
  const img = e.flash > 0 ? (custom.enemy ? null : slimeFlash) : custom.enemy ?? slimeSprite;
  if (img) drawSpriteAt(img, e.x, e.y - hop, e.vx < 0);
  else { sctx.save(); sctx.filter = "brightness(4)"; drawSpriteAt(custom.enemy, e.x, e.y - hop, e.vx < 0); sctx.restore(); }
  if (e.frozen > 0) { sctx.fillStyle = "rgba(170,225,255,.45)"; const w = (custom.enemy?.width ?? 12) + 2, h = (custom.enemy?.height ?? 8) + 2; sctx.fillRect(Math.round(e.x - w / 2), Math.round(e.y - h), w, h); }
}

function drawGrass(g) {
  if (!g.alive) { px(g.x - 2, g.y, "#3f7d43"); px(g.x + 1, g.y, "#3f7d43"); px(g.x, g.y - 1, "#3f7d43"); return; }
  const sway = Math.round(Math.sin(time * 2 + g.sway));
  for (let i = -3; i <= 3; i++) {
    const hh = g.h - Math.abs(i) * 1.3, c = i % 2 ? "#2f6b3a" : "#3f8a45";
    line(g.x + i, g.y, g.x + i * 1.4 + sway, g.y - hh, "#22161f");
    line(g.x + i, g.y - 1, g.x + i * 1.4 + sway, g.y - hh + 1, c);
  }
  px(g.x + sway, g.y - g.h + 1, "#6fbf5f");
}

function drawHud() {
  for (let i = 0; i < 5; i++) {
    const x = 8 + i * 9, y = 8;
    sctx.fillStyle = "#22161f"; sctx.fillRect(x - 1, y - 1, 8, 7);
    sctx.fillStyle = i < player.hp ? "#d4483a" : "#5a4a52"; sctx.fillRect(x, y, 6, 5);
    if (i < player.hp) { sctx.fillStyle = "#f08a6a"; sctx.fillRect(x + 1, y + 1, 2, 1); }
  }
  // 스킬 아이콘과 쿨타임
  const sx = W - 26, sy = H - 26;
  sctx.fillStyle = "#22161f"; sctx.fillRect(sx - 1, sy - 1, 20, 20);
  sctx.fillStyle = "#1f3a5a"; sctx.fillRect(sx, sy, 18, 18);
  sctx.fillStyle = "#8fd3f4"; line(sx + 9, sy + 3, sx + 9, sy + 15, "#cfefff"); line(sx + 3, sy + 9, sx + 15, sy + 9, "#cfefff"); line(sx + 5, sy + 5, sx + 13, sy + 13, "#8fd3f4"); line(sx + 13, sy + 5, sx + 5, sy + 13, "#8fd3f4");
  if (player.skillCd > 0) { sctx.fillStyle = "rgba(10,14,20,.7)"; const h = Math.ceil(18 * player.skillCd / SKILL_CD); sctx.fillRect(sx, sy + 18 - h, 18, h); }
  $("kills").textContent = kills;
  $("wave").textContent = wave;
}

function drawCursor() {
  if (!mouse.inside) return;
  const x = Math.round(mouse.x), y = Math.round(mouse.y);
  for (const [a, b] of [[-4, 0], [-3, 0], [3, 0], [4, 0], [0, -4], [0, -3], [0, 3], [0, 4]]) px(x + a, y + b, "#ffffff");
  px(x, y, "#22161f");
}

function render() {
  const sk = shake > 0 ? [Math.round((Math.random() - 0.5) * 4), Math.round((Math.random() - 0.5) * 4)] : [0, 0];
  sctx.setTransform(1, 0, 0, 1, sk[0], sk[1]);
  sctx.fillStyle = "#22161f"; sctx.fillRect(-4, -4, W + 8, H + 8);
  sctx.drawImage(ground, 0, 0);
  drawFx("under");   // 바닥에 깔리는 스킬은 캐릭터 뒤
  // y 순서로 그리기
  const list = [...grass.map((g) => ({ y: g.y, d: () => drawGrass(g) })), ...enemies.map((e) => ({ y: e.y, d: () => drawEnemy(e) })), { y: player.y, d: drawPlayer }];
  list.sort((a, b) => a.y - b.y).forEach((o) => o.d());
  for (const b of bits) { const c = b.life < 0.2 && Math.floor(b.life * 30) % 2 ? null : b.c; if (c) { px(b.x, b.y - b.z, c); if (b.spin && Math.floor(time * 20) % 2) px(b.x + 1, b.y - b.z, c); } }
  drawFx("front");
  drawCursor();
  sctx.setTransform(1, 0, 0, 1, 0, 0);
  drawHud();
}

// ---------- 화면 크기: 정수 배율 ----------
function fit() {
  const box = $("stage"), avail = box.clientWidth;
  const k = Math.max(1, Math.floor(avail / W));
  const kk = avail < W ? avail / W : k;
  screen.style.width = W * kk + "px"; screen.style.height = H * kk + "px";
  $("zoom").textContent = avail < W ? "축소 표시" : `${k}×`;
}
addEventListener("resize", fit);

// ---------- 이미지 테스트 패널 ----------
function loadImage(file) {
  return new Promise((res, rej) => { const img = new Image(); img.onload = () => res(img); img.onerror = rej; img.src = URL.createObjectURL(file); });
}
function describe(id, img, extra = "") { $(id).textContent = img ? `${img.width}×${img.height}px${extra}` : "기본 이미지"; }
$("in-hero").addEventListener("change", async (e) => {
  const f = e.target.files[0]; if (!f) return;
  custom.hero = await loadImage(f);
  const auto = Math.max(1, Math.round(custom.hero.width / custom.hero.height));
  $("in-frames").value = auto; custom.heroFrames = auto;
  describe("hero-info", custom.hero, auto > 1 ? `, ${auto}프레임 가로 시트` : "");
});
$("in-frames").addEventListener("change", (e) => { custom.heroFrames = Math.max(1, +e.target.value || 1); if (custom.hero) describe("hero-info", custom.hero, custom.heroFrames > 1 ? `, ${custom.heroFrames}프레임 가로 시트` : ""); });
$("in-enemy").addEventListener("change", async (e) => { const f = e.target.files[0]; if (!f) return; custom.enemy = await loadImage(f); describe("enemy-info", custom.enemy); });
$("in-tile").addEventListener("change", async (e) => { const f = e.target.files[0]; if (!f) return; custom.tile = await loadImage(f); describe("tile-info", custom.tile); buildGround(); });
$("btn-reset").addEventListener("click", () => {
  Object.assign(custom, { hero: null, heroFrames: 1, enemy: null, tile: null });
  ["in-hero", "in-enemy", "in-tile"].forEach((id) => ($(id).value = ""));
  ["hero-info", "enemy-info", "tile-info"].forEach((id) => describe(id, null));
  buildGround();
});
document.querySelectorAll('input[name="style"]').forEach((r) => r.addEventListener("change", (e) => { cfg.style = e.target.value; measureSlash(); }));
$("in-light").addEventListener("input", (e) => { cfg.light = +e.target.value; $("light-val").textContent = (+e.target.value).toFixed(2); });
$("in-hitstop").addEventListener("change", (e) => (cfg.hitstop = e.target.checked));
$("in-shake").addEventListener("change", (e) => (cfg.shake = e.target.checked));

// ---------- 시작 ----------
buildGround(); spawnGrass(); spawnWave(); measureSlash(); fit();
let last = performance.now();
function loop(now) {
  const dt = Math.min(0.05, (now - last) / 1000); last = now;
  update(dt); render();
  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);
// 첫 화면에서 무엇을 하는지 보이도록 한 번 휘두르고 시작한다.
setTimeout(() => { mouse.x = player.x + 40; mouse.y = player.y - 8; attack(); }, 400);
