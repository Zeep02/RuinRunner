import { fit, clamp, TAU } from "../../core/index.js";

export const meta = {
  name: "Weapon bolt",
  desc: "A looping projectile flying right, in a material: magic, crescent, ice, fire, poison, soul flame, shard, spark, spinning blade, tracer, grenade or water.",
  tags: ["weapon", "projectile", "bolt", "directional", "loop"],
  size: [90, 70], origin: [55, 35], duration: 0.33, loop: true, fps: 15, bg: "ground",
  options: { palette: ["#0a0f2e", "#172a78", "#2550d0", "#3f8cff", "#7cc8ff", "#c8f0ff", "#ffffff"], seed: 24, kind: "magic", size: 1, tail: 12 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), f = t / meta.duration, S = o.size, c = (i) => C[clamp(Math.round(i), 1, 6)], K = o.kind, wob = Math.sin(f * TAU);
  const disc = (x, y, r, i) => p.circle(x, y, r, c(i));
  const tail = (L, w) => { for (let x = 0; x < L; x += 1) { const v = x / L, hw = w * (1 - v); for (let y = -hw; y <= hw; y += 0.5) p.dot(-x, y + Math.sin(v * 5 + f * TAU) * v * 1.2, c(5 - v * 3.2 - Math.abs(y)), 1 - v * 0.55); } };
  switch (K) {
    case "magic": tail(o.tail * S, 2.2 * S); disc(0, 0, 4 * S + wob * 0.4, 4); disc(-0.5, -0.5, 2.6 * S, 6); p.glow(0, 0, 11 * S, C[4], 0.4); break;
    case "crescent": for (let a = -1.25; a <= 1.25; a += 0.03) { const v = 1 - Math.abs(a) / 1.25, w = 6 * v * S; for (let d = 0; d < w; d += 0.5) p.px(Math.cos(a) * (15 * S - d) - 8 * S, Math.sin(a) * (15 * S - d), c(6 - (d / Math.max(0.5, w)) * 3.4)); } tail(o.tail * S, 1.2); p.glow(0, 0, 13 * S, C[4], 0.25); break;
    case "ice": { const L = 12 * S; tail(o.tail * S, 1.6 * S); for (let x = -L; x <= L * 0.8; x += 0.5) { const hw = 3.2 * S * (1 - Math.abs(x + L * 0.1) / (L * 0.95)); for (let y = -hw; y <= hw; y += 0.5) p.px(x, y, c(y < 0 ? 6 - Math.abs(y) * 0.3 : 3.5)); } p.glow(0, 0, 9 * S, C[4], 0.25); break; }
    case "fire": for (let i = 0; i < 6; i++) { const v = i / 6, x = -v * (o.tail + 8) * S, y = Math.sin(v * 9 + f * TAU) * v * 3.4 * S; disc(x, y, (4.6 - v * 3.8) * S, 6 - v * 4.5); } disc(0.5, -0.5, 2 * S, 6); p.glow(0, 0, 12 * S, C[4], 0.4); break;
    case "poison": for (let i = 1; i < 4; i++) disc(-i * 5 * S, Math.sin(i + f * TAU) * 1.6, (3 - i * 0.6) * S, 4 - i * 0.7); disc(0, 0, 5 * S + wob * 0.4, 3); disc(-1, -1, 3.2 * S, 5); p.px(-2, -2.5, c(6)); p.glow(0, 0, 9 * S, C[4], 0.3); break;
    case "soul": { for (let y = 0; y < 13 * S; y += 0.5) { const v = y / (13 * S), hw = 4.4 * S * Math.pow(1 - v, 0.65) * (v < 0.2 ? 0.7 + v * 1.5 : 1), cx = Math.sin(v * 5 + f * TAU) * v * 2.4 * S; for (let x = -hw; x <= hw; x += 0.5) p.px(cx + x, 4 * S - y, c(6.2 - v * 3 - (Math.abs(x) / Math.max(0.5, hw)) * 2)); } p.glow(0, -2, 12 * S, C[4], 0.4); break; }
    case "shard": { tail(o.tail * S * 0.6, 1); const a = f * TAU, cs = Math.cos(a), sn = Math.sin(a); for (let x = -7; x <= 7; x += 0.5) for (let y = -7; y <= 7; y += 0.5) { const lx = (x * cs + y * sn) / S, ly = (-x * sn + y * cs) / S; if (Math.abs(lx) / 5.5 + Math.abs(ly) / 3 <= 1) p.px(x, y, c(ly < 0 ? 6 : 3.5)); } break; }
    case "spark": { for (let i = 0; i < 5; i++) { const a = (i / 5) * TAU + f * TAU * 2, L = (5 + (i % 2) * 3) * S; p.line(0, 0, Math.cos(a) * L, Math.sin(a) * L, c(5)); } disc(0, 0, 3 * S, 6); tail(o.tail * S * 0.7, 1); p.glow(0, 0, 13 * S, C[4], 0.45); break; }
    case "knife": { const a = f * TAU * 2, cs = Math.cos(a), sn = Math.sin(a); for (let d = -9 * S; d <= 9 * S; d += 0.5) { const hw = (d > 0 ? 2 * (1 - d / (9 * S)) + 0.5 : 1) * S; for (let w = -hw; w <= hw; w += 0.5) p.px(cs * d - sn * w, sn * d + cs * w, d > 0 ? c(w < 0 ? 6 : 4) : c(2)); } break; }
    case "tracer": { const L = o.tail * S; for (let x = 0; x < L; x += 1) { const v = x / L; p.px(-x, 0, c(6 - v * 4)); if (v < 0.3) p.px(-x, 1, c(4 - v * 3)); } p.px(1, 0, C[6]); p.px(1, 1, C[5]); p.glow(0, 0, 6 * S, C[4], 0.35); break; }
    case "grenade": { const a = f * TAU, cs = Math.cos(a), sn = Math.sin(a); for (let x = -6; x <= 6; x += 0.5) for (let y = -6; y <= 6; y += 0.5) { const lx = x * cs + y * sn, ly = -x * sn + y * cs; if (Math.abs(lx) <= 4.5 * S && Math.abs(ly) <= 3 * S) p.px(x, y, c(Math.abs(lx) > 3.2 * S ? 5 : ly < 0 ? 4 : 2)); } tail(o.tail * S * 0.7, 1); break; }
    case "water": tail((o.tail + 8) * S, 3 * S); disc(0, 0, 4.6 * S + wob, 4); disc(-1, -1, 2.6 * S, 6); for (let i = 0; i < 3; i++) p.px(-5 - i * 5, Math.sin(i * 2 + f * TAU) * 5, c(5)); break;
  }
}
