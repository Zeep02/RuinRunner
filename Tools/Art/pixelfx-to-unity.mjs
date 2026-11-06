#!/usr/bin/env node
// pixelfx 이펙트를 RuinRunner의 개별 프레임 CombatFX 클립으로 내보낸다.
//
//   node Tools/Art/pixelfx-to-unity.mjs <effect-id> <ClipName> [options]
//
//   node Tools/Art/pixelfx-to-unity.mjs ice/frost-nova FrostNova
//   node Tools/Art/pixelfx-to-unity.mjs fire/fireball BlueFireball --opts '{"palette":"ice"}' --fps 15
//   node Tools/Art/pixelfx-to-unity.mjs ice/frost-nova FrostNova --install
//
// 기본 출력은 검수용 Tools/Art/Staging/PixelFx/<ClipName>/ 이다.
//   00.png ...      투명 개별 프레임. 원본 픽셀 그대로이며 확대·축소·재샘플링하지 않는다.
//   _pixelfx.json   출처(id, 커밋, seed, 옵션)와 Unity 임포트 값(PPU, 피벗, 가시 지름).
//   contact.png     검수용 접촉표(어두운 배경, 정수 배율).
// --install 은 검수 후 Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/<ClipName>/ 에 복사한다.
// Assets/RuinRunner/Editor/PixelFxClipImporter.cs 가 _pixelfx.json 을 읽어 Sprite·PPU·피벗을 지정한다.
//
// 옵션
//   --opts JSON     이펙트 옵션. "palette" 는 hex 배열 또는 PAL 이름(ice, holy, shadow ...).
//   --seed N        고정 seed (기본 1). 같은 seed 는 항상 같은 프레임을 만든다.
//   --fps N         내보낼 프레임레이트 (기본: 이펙트 meta.fps). 12~15 는 영웅 모션과 박자를 맞춘다.
//   --palette-from F  PNG(PixelLab 결과 등)에서 램프를 뽑아 palette 로 쓴다. --palette-count N, --hue 200-260 과 함께.
//   --flip          왼쪽을 보게 그린다. 보통은 런타임 회전/flip 을 쓰므로 필요 없다.
//   --center        피벗을 이펙트 원점 대신 크롭 중심으로 둔다(기존 CombatFX 와 같은 중심 피벗).
//   --anchor X,Y    피벗을 이펙트 원점에서 이만큼 옮긴다. 발밑 원점 이펙트의 폭발 중심을 게임이 지정한 지점에 놓을 때 쓴다.
//   --ppu N         Pixels Per Unit (기본 32, 프로젝트 기준).
//   --canvas WxH    이펙트 캔버스를 바꾼다. 넓은 피격 범위에 맞춘 원형·호 이펙트가 잘리지 않게 한다.
//   --from S        앞의 S초(예비 동작)를 버린다.
//   --until S       S초까지만 쓰고, 남은 길이의 마지막 30%에 걸쳐 서서히 사라지게 한다.
//   --install       Resources 의 CombatFX 로 복사한다.
//   --replace       --install 시 같은 이름의 기존 클립 위에 덮어쓴다(프레임 수가 줄면 거부).

import { existsSync, mkdirSync, readdirSync, writeFileSync, readFileSync, copyFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT = join(HERE, "..", "..");
const PIXELFX = join(PROJECT, "Tools", "pixelfx");
const STAGING = join(HERE, "Staging", "PixelFx");
const COMBATFX = join(PROJECT, "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated", "CombatFX");

if (!existsSync(join(PIXELFX, "tools", "lib", "fxlib.mjs"))) {
  console.error(`pixelfx 가 없습니다: ${PIXELFX}\nTools/pixelfx 에 https://github.com/patch-gg/pixelfx 를 두세요.`);
  process.exit(1);
}
const { loadEffect, renderFrames, contactSheet, resolveIds } = await import(pathToFileURL(join(PIXELFX, "tools", "lib", "fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(join(PIXELFX, "tools", "lib", "png.mjs")).href);
const { PAL } = await import(pathToFileURL(join(PIXELFX, "src", "core", "color.js")).href);

const args = process.argv.slice(2), flags = {}, pos = [];
for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (!a.startsWith("--")) pos.push(a);
  else if (["install", "replace", "flip", "center"].includes(a.slice(2))) flags[a.slice(2)] = true;
  else flags[a.slice(2)] = args[++i];
}
const [id, clip] = pos;
if (!id || !clip || !/^[A-Z][A-Za-z0-9]*$/.test(clip)) {
  console.error("usage: node Tools/Art/pixelfx-to-unity.mjs <effect-id> <ClipName(PascalCase)> [--opts JSON] [--seed N] [--fps N] [--center] [--install]");
  process.exit(1);
}
if (resolveIds([id]).length !== 1 || resolveIds([id])[0] !== id) {
  console.error(`정확한 이펙트 id 하나가 필요합니다: ${id}  (찾기: node Tools/pixelfx/tools/list.mjs <단어>)`);
  process.exit(1);
}

const options = flags.opts ? JSON.parse(flags.opts) : {};
if (flags["palette-from"]) {
  // PixelLab 결과나 채택 스프라이트의 색면으로 이펙트를 칠한다.
  const { rampFromPNG } = await import(pathToFileURL(join(HERE, "palette-from-png.mjs")).href);
  options.palette = rampFromPNG(flags["palette-from"], +(flags["palette-count"] ?? 7), flags.hue ? flags.hue.split("-").map(Number) : null);
  console.log(`palette from ${flags["palette-from"]}: ${JSON.stringify(options.palette)}`);
}
if (typeof options.palette === "string") {
  if (!PAL[options.palette]) { console.error(`PAL 에 ${options.palette} 가 없습니다: ${Object.keys(PAL).join(" ")}`); process.exit(1); }
  options.palette = PAL[options.palette];
}
if (flags.flip) options.flip = true;
const seed = flags.seed !== undefined ? +flags.seed : 1;
const ppu = flags.ppu ? +flags.ppu : 32;

const fx = await loadEffect(id, join(PIXELFX, "src", "fx"));
// A wider canvas for effects sized to a large hit area. The origin keeps its place relative to the canvas.
if (flags.canvas) {
  const [cw, ch] = flags.canvas.split("x").map(Number), [mw, mh] = fx.meta.size;
  fx.meta = { ...fx.meta, size: [cw, ch], origin: [Math.round(fx.meta.origin[0] / mw * cw), Math.round(fx.meta.origin[1] / mh * ch)] };
}
const r = renderFrames(fx, { fps: flags.fps ? +flags.fps : undefined, seed, bg: "none", actor: false, options });
const [OX, OY] = fx.meta.origin;
// Keep only part of the clock: drop a wind-up the game plays itself and fade out long-lived debris instead of cutting it.
const from = Math.round((flags.from ? +flags.from : 0) * r.fps), until = flags.until ? Math.min(r.n, Math.round(+flags.until * r.fps)) : r.n;
const tailFade = flags.until ? Math.max(1, Math.round((until - from) * 0.3)) : 0;
r.frames = r.frames.slice(from, until);
r.n = r.frames.length;
const fadeAt = (i) => (tailFade ? Math.min(1, (r.n - i) / tailFade) : 1);

// 모든 프레임의 가시 픽셀과 빛을 덮는 하나의 크롭. 프레임마다 다르게 자르면 피벗이 흔들리고, 빛을 빼면 후광이 네모로 잘린다.
let x0 = r.w, y0 = r.h, x1 = -1, y1 = -1;
for (const f of r.frames)
  for (let y = 0; y < r.h; y++)
    for (let x = 0; x < r.w; x++)
      if (f.clear[(y * r.w + x) * 4 + 3] > 0 || (f.light[y * r.w + x] & 0xffffff) !== 0) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
if (x1 < 0) { console.error(`${id}: 보이는 픽셀이 없습니다.`); process.exit(1); }
const w = x1 - x0 + 1, h = y1 - y0 + 1;

// Unity 피벗은 왼쪽 아래 기준 0..1. 기본은 이펙트 원점(발밑, 손, 발사 지점)이다.
const [AX, AY] = flags.anchor ? flags.anchor.split(",").map(Number) : [0, 0];
const pivotX = flags.center ? 0.5 : (OX + AX - x0 + 0.5) / w;
const pivotY = flags.center ? 0.5 : 1 - (OY + AY - y0 + 0.5) / h;

// 가시 지름: NativeCombatEffect.diameters 에 들어갈 값. 최대 프레임의 가시 크기.
// 빛 버퍼가 알파로 구워진 옅은 후광(알파 < 128)은 몸체 크기에 넣지 않는다.
let diameter = 0;
for (const f of r.frames) {
  let a = r.w, b = r.h, c = -1, d = -1;
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++)
    if (f.clear[(y * r.w + x) * 4 + 3] >= 128) { if (x < a) a = x; if (x > c) c = x; if (y < b) b = y; if (y > d) d = y; }
  if (c >= 0) diameter = Math.max(diameter, c - a + 1, d - b + 1);
}

const digits = Math.max(2, String(r.n - 1).length);
const names = r.frames.map((_, i) => String(i).padStart(digits, "0") + ".png");
const out = join(STAGING, clip);
mkdirSync(out, { recursive: true });
const stale = readdirSync(out).filter((f) => /^\d+\.png$/.test(f) && !names.includes(f));
// Premultiplied: colour keeps its coverage in alpha, light adds to RGB with no coverage at all. Drawn with
// Blend One OneMinusSrcAlpha, the glow really adds to whatever is underneath, as it does in the gallery.
// Baking light into alpha (f.clear) only looks right over a dark background; on a bright floor it is a pale veil.
r.frames.forEach((f, i) => {
  const crop = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++)
    for (let x = 0; x < w; x++) {
      const source = (y + y0) * r.w + x + x0, c = f.raw[source], l = f.light[source], a = c >>> 24, k = a / 255, to = (y * w + x) * 4, fade = fadeAt(i);
      crop[to] = ((c & 255) * k + (l & 255)) * fade;
      crop[to + 1] = (((c >>> 8) & 255) * k + ((l >>> 8) & 255)) * fade;
      crop[to + 2] = (((c >>> 16) & 255) * k + ((l >>> 16) & 255)) * fade;
      crop[to + 3] = a * fade;
    }
  writeFileSync(join(out, names[i]), encodePNG(w, h, crop));
});

let commit = "unknown";
try { commit = readFileSync(join(PIXELFX, "VENDORED_FROM.txt"), "utf8").split(/\s+/)[0]; } catch {}
const meta = {
  // PixelFxClipImporter.cs 가 읽는 평면 필드
  clip, ppu, pivotX: +pivotX.toFixed(5), pivotY: +pivotY.toFixed(5), width: w, height: h, premultiplied: true, canvas: flags.canvas ?? null, center: !!flags.center, anchor: flags.anchor ?? null, from: flags.from ?? null, until: flags.until ?? null,
  // 출처와 재현 정보
  source: "pixelfx", effect: id, pixelfxCommit: commit, license: "MIT (patch-gg/pixelfx)",
  seed, fps: r.fps, loop: !!fx.meta.loop, frames: r.n, durationSeconds: +(r.n / r.fps).toFixed(3),
  visibleDiameter: diameter, origin: [OX - x0, OY - y0],
  optionsJson: JSON.stringify(options),
};
writeFileSync(join(out, "_pixelfx.json"), JSON.stringify(meta, null, 2) + "\n");
writeFileSync(join(out, "contact.png"), contactSheet(fx, { ...r, frames: r.frames.map((f) => ({ ...f, color: f.clear })) }, { max: 400, zoom: 3,
  title: `${clip} <- ${id}  ${w}x${h}  ${r.n}F @${r.fps}  D${diameter}  PPU${ppu}` }));

console.log(`${id} -> ${clip}: ${r.n} frames ${w}x${h} @${r.fps}fps, visible diameter ${diameter}px`);
console.log(`  검수: ${join(out, "contact.png")}`);
if (stale.length) console.log(`  주의: 이전 실행의 남은 프레임 ${stale.join(", ")} 가 스테이징에 있습니다. 설치 전에 정리하세요.`);

if (flags.install) {
  if (stale.length) { console.error("남은 프레임이 있어 설치하지 않습니다."); process.exit(1); }
  const dest = join(COMBATFX, clip);
  if (existsSync(dest)) {
    if (!flags.replace) { console.error(`이미 있습니다: ${dest} (--replace 로 덮어쓰기)`); process.exit(1); }
    const old = readdirSync(dest).filter((f) => /^\d+\.png$/.test(f) && !names.includes(f));
    if (old.length) { console.error(`기존 클립에 프레임이 더 많습니다(${old.join(", ")}). 직접 정리한 뒤 다시 실행하세요.`); process.exit(1); }
  }
  mkdirSync(dest, { recursive: true });
  // json 을 먼저 써야 PNG 첫 임포트 때 임포터가 읽을 수 있다.
  copyFileSync(join(out, "_pixelfx.json"), join(dest, "_pixelfx.json"));
  for (const n of names) copyFileSync(join(out, n), join(dest, n));
  console.log(`  설치: ${dest}`);
  console.log(`  NativeCombatEffect.diameters 에 추가: {"${clip}",${diameter}}`);
} else {
  console.log(`  설치하려면 같은 명령에 --install 을 붙이세요. diameters 값: {"${clip}",${diameter}}`);
}
