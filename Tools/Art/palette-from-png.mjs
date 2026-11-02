#!/usr/bin/env node
// 스프라이트 PNG(PixelLab 결과, 기존 채택 에셋 등)에서 pixelfx 팔레트 램프를 뽑는다.
// 불투명 픽셀 색을 빈도순으로 모아 비슷한 색을 합치고, 어두운 색 → 밝은 색 순서로 정렬한다.
//
//   node Tools/Art/palette-from-png.mjs <file.png> [--count 7] [--hue 200-260]
//
// --hue 는 색상(도) 범위만 남긴다. 예: 푸른 마법 이펙트에는 캐릭터의 파란 색면만 쓴다.
// 출력 JSON 배열은 pixelfx-to-unity.mjs 의 --opts '{"palette":[...]}' 또는 --palette-from 으로 쓴다.

import { readFileSync } from "node:fs";
import { inflateSync } from "node:zlib";
import { fileURLToPath } from "node:url";

/** 8비트 PNG(그레이, RGB, 팔레트, 그레이+알파, RGBA, 인터레이스 없음)를 RGBA 로 디코드한다. */
export function decodePNG(buf) {
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error("PNG 가 아닙니다");
  let o = 8, w = 0, h = 0, depth = 0, type = 0, interlace = 0, plte = null, trns = null;
  const idat = [];
  while (o < buf.length) {
    const len = buf.readUInt32BE(o), t = buf.toString("ascii", o + 4, o + 8), d = buf.subarray(o + 8, o + 8 + len);
    if (t === "IHDR") { w = d.readUInt32BE(0); h = d.readUInt32BE(4); depth = d[8]; type = d[9]; interlace = d[12]; }
    else if (t === "PLTE") plte = d;
    else if (t === "tRNS") trns = d;
    else if (t === "IDAT") idat.push(d);
    else if (t === "IEND") break;
    o += 12 + len;
  }
  if (depth !== 8 || interlace) throw new Error(`지원하지 않는 PNG (bit depth ${depth}, interlace ${interlace})`);
  const ch = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[type], stride = w * ch, raw = inflateSync(Buffer.concat(idat));
  const px = Buffer.alloc(stride * h);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1)), row = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= ch ? px[row + x - ch] : 0, b = y ? px[row - stride + x] : 0, c = x >= ch && y ? px[row - stride + x - ch] : 0;
      let v = src[x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      px[row + x] = v & 255;
    }
  }
  const out = new Uint8ClampedArray(w * h * 4);
  for (let i = 0; i < w * h; i++) {
    const s = i * ch;
    if (type === 6) out.set(px.subarray(s, s + 4), i * 4);
    else if (type === 2) out.set([px[s], px[s + 1], px[s + 2], 255], i * 4);
    else if (type === 0) out.set([px[s], px[s], px[s], 255], i * 4);
    else if (type === 4) out.set([px[s], px[s], px[s], px[s + 1]], i * 4);
    else { const k = px[s]; out.set([plte[k * 3], plte[k * 3 + 1], plte[k * 3 + 2], trns && k < trns.length ? trns[k] : 255], i * 4); }
  }
  return { w, h, data: out };
}

const lum = ([r, g, b]) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
function hue([r, g, b]) {
  const M = Math.max(r, g, b), m = Math.min(r, g, b), d = M - m;
  if (!d) return -1;
  const H = M === r ? ((g - b) / d) % 6 : M === g ? (b - r) / d + 2 : (r - g) / d + 4;
  return (H * 60 + 360) % 360;
}
const hex = (c) => "#" + c.map((v) => Math.round(v).toString(16).padStart(2, "0")).join("");

/** 이미지에서 dark→bright 램프 count 개. hue: [from, to] 도(감싸기 허용) 또는 null. */
export function rampFromPNG(file, count = 7, hueRange = null) {
  const { w, h, data } = decodePNG(readFileSync(file));
  const freq = new Map();
  for (let i = 0; i < w * h; i++) {
    if (data[i * 4 + 3] < 200) continue;
    const c = [data[i * 4], data[i * 4 + 1], data[i * 4 + 2]];
    if (hueRange) {
      const hh = hue(c), [a, b] = hueRange;
      if (hh < 0 || (a <= b ? hh < a || hh > b : hh < a && hh > b)) continue;
    }
    const k = c.join(",");
    freq.set(k, (freq.get(k) ?? 0) + 1);
  }
  // 빈도순으로 훑으며 가까운 색은 대표색에 합친다.
  const reps = [];
  for (const [k, n] of [...freq].sort((a, b) => b[1] - a[1])) {
    const c = k.split(",").map(Number);
    const near = reps.find((r) => Math.hypot(r.c[0] - c[0], r.c[1] - c[1], r.c[2] - c[2]) < 28);
    if (near) near.n += n; else reps.push({ c, n });
  }
  if (!reps.length) throw new Error("조건에 맞는 불투명 색이 없습니다");
  return reps.slice(0, count).map((r) => r.c).sort((a, b) => lum(a) - lum(b)).map(hex);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const a = process.argv.slice(2), file = a.find((x, i) => !x.startsWith("--") && !(i > 0 && a[i - 1].startsWith("--")));
  const get = (k) => { const i = a.indexOf(k); return i >= 0 ? a[i + 1] : undefined; };
  if (!file) { console.error("usage: node Tools/Art/palette-from-png.mjs <file.png> [--count 7] [--hue 200-260]"); process.exit(1); }
  const hr = get("--hue")?.split("-").map(Number) ?? null;
  console.log(JSON.stringify(rampFromPNG(file, +(get("--count") ?? 7), hr)));
}
