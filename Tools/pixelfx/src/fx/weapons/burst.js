// Fire, smoke and flash recipe after small-explosion, adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
import { Particles, Bits, Lights, P, PF, B, Rng, fit, sparkle, clamp, ease, WHITE } from "../../core/index.js";

export const meta = {
  name: "Weapon burst",
  desc: "A ground burst in a chosen material: fire, ice shards, poison, shock, rock, blood, soul, holy light, void or a flower of blades, with a floor ring.",
  tags: ["weapon", "explosion", "burst", "ground", "one-shot"],
  size: [260, 220], origin: [130, 150], duration: 0.7, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#1e0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"], sparks: ["#fff3c4", "#ffd166", "#ff8a2a", "#c42a1a"], seed: 15, kind: "fire", size: 1, ring: true, scorch: true },
};

const TAU = Math.PI * 2;

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), S = o.size, K = o.kind;
  const ps = new Particles({ rng: Rg, ground: () => 8, sparks: o.sparks }), bits = new Bits({ ground: () => 8 }), lights = new Lights();
  const RING = 26 * S;
  lights.add(0, -8, 40 * S, C[4], 0.65, 0.28); lights.add(0, -6, 15 * S, WHITE, 0.75, 0.06);
  const shards = [], jag = [], petals = [];
  if (K === "fire") {
    for (let i = 0; i < 4; i++) ps.spawn(P.FIRE, Rg.range(-2, 2), -6 + Rg.range(-2, 2), Rg.range(-14, 14), Rg.range(-30, -8), Rg.range(0.16, 0.24), 0, Rg.range(7, 9) * S);
    for (let i = 0; i < 9; i++) { const a = Rg.range(-Math.PI, 0), sp = Rg.range(30, 95) * S; ps.spawn(P.FIRE, Math.cos(a) * 3, -5 + Math.sin(a) * 3, Math.cos(a) * sp, Math.sin(a) * sp * 0.75 - 22, Rg.range(0.26, 0.46), 0, Rg.range(3.5, 6.5) * S, 0, Rg.chance(0.4) ? PF.SMOKES : 0); }
  } else if (K === "ice" || K === "holy" || K === "shard") {
    const n = K === "holy" ? 10 : 9;
    for (let i = 0; i < n; i++) shards.push({ a: (i / n) * TAU + Rg.range(-0.15, 0.15), len: Rg.range(14, 30) * S, w: Rg.range(2.5, 4.5) * Math.sqrt(S), d: Rg.range(0, 0.04) });
    for (let i = 0; i < 8; i++) { const a = Rg.range(-Math.PI, 0), sp = Rg.range(40, 130) * S; bits.add({ kind: B.SHARD, x: 0, y: -4, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, a: Rg.range(0, 6), va: Rg.range(-12, 12), s: Rg.range(1.2, 2.4), c: C[Rg.int(3, 6)], g: 0.8, life: Rg.range(0.3, 0.55) }); }
  } else if (K === "poison" || K === "blood" || K === "water") {
    for (let i = 0; i < 16; i++) { const a = Rg.range(-Math.PI * 0.95, -Math.PI * 0.05), sp = Rg.range(40, 150) * S; bits.add({ kind: B.DRIP, x: Rg.range(-5, 5), y: -3, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp * 0.8, g: 0.9, life: Rg.range(0.3, 0.6), c: C[Rg.int(3, 6)] }); }
    for (let i = 0; i < 5; i++) ps.spawn(P.VAPOUR, Rg.range(-12, 12) * S, Rg.range(-6, 0), Rg.range(-12, 12), Rg.range(-22, -6), Rg.range(0.3, 0.5), C[3], Rg.range(3, 5.5) * S);
  } else if (K === "shock") {
    for (let i = 0; i < 7; i++) { const pts = [[0, -6]]; let x = 0, y = -6; const a = (i / 7) * TAU + Rg.range(-0.3, 0.3), L = Rg.range(18, 36) * S; for (let d = 6; d < L; d += 6) { x = Math.cos(a) * d + Rg.range(-3, 3); y = -6 + Math.sin(a) * d * 0.7 + Rg.range(-3, 3); pts.push([x, y]); } jag.push(pts); }
  } else if (K === "rock") {
    for (let i = 0; i < 12; i++) { const a = Rg.range(-Math.PI * 0.95, -Math.PI * 0.05), sp = Rg.range(50, 170) * S; bits.add({ kind: B.CHUNK, x: Rg.range(-4, 4), y: -3, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, a: Rg.range(0, 6), va: Rg.range(-8, 8), s: Rg.range(1.4, 3), c: C[Rg.int(2, 5)], g: 1, life: Rg.range(0.35, 0.6), bounce: 0.3 }); }
    for (let i = 0; i < 5; i++) ps.spawn(P.DUST, Rg.range(-10, 10) * S, Rg.range(-5, 2), Rg.range(-30, 30), Rg.range(-16, -3), Rg.range(0.3, 0.55), C[4], Rg.range(3.5, 6) * S);
  } else if (K === "soul") {
    for (let i = 0; i < 11; i++) { const a = Rg.range(-Math.PI, 0), sp = Rg.range(12, 60) * S; ps.spawn(P.VAPOUR, Math.cos(a) * 4, -4, Math.cos(a) * sp, -Rg.range(30, 90) * S, Rg.range(0.3, 0.55), C[Rg.int(4, 6)], Rg.range(2.6, 5) * S); }
    for (let i = 0; i < 9; i++) ps.spawn(P.EMBER, Rg.range(-10, 10) * S, Rg.range(-16, 0), Rg.range(-12, 12), Rg.range(-44, -18), Rg.range(0.3, 0.6), C[5], 1);
  } else if (K === "void") {
    for (let i = 0; i < 14; i++) { const a = Rg.range(0, TAU), d = Rg.range(18, 34) * S; ps.spawn(P.STREAK, Math.cos(a) * d, -6 + Math.sin(a) * d * 0.7, -Math.cos(a) * d * 4, -Math.sin(a) * d * 2.8, Rg.range(0.16, 0.28), C[Rg.int(4, 6)], 1); }
  } else if (K === "flower") {
    for (let i = 0; i < 5; i++) petals.push({ a: -Math.PI / 2 + (i / 5) * TAU, d: i * 0.018 });
  }
  if (K !== "void") for (let i = 0; i < (K === "fire" ? 14 : 8); i++) { const a = Rg.range(-Math.PI * 0.95, -Math.PI * 0.05), sp = Rg.range(90, 240) * Math.sqrt(S); ps.spawn(P.SPARK, 0, -4, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.12, 0.32)); }
  let t = 0;
  return {
    drawBack(p) {
      const k = 1 - clamp(t / 0.62);
      if (o.scorch && k > 0) for (let y = -4; y <= 4; y++) for (let x = -14; x <= 14; x++) if ((x * x) / (S * S) + (y * y * 8) / (S * S) < 170) p.dot(x, 2 + y, C[0], 0.75 * k);
      if (o.ring && t < 0.17) { const r = RING * ease.outQuart(t / 0.17), a = 1 - t / 0.17; for (let g = 0; g < TAU; g += 0.06) p.dot(Math.cos(g) * r, 2 + Math.sin(g) * r * 0.42, g % 0.4 < 0.2 ? C[5] : C[3], a); }
    },
    update(dt) { t += dt; ps.update(dt); bits.update(dt); lights.update(dt); },
    draw(p) {
      if (t < 0.09) { const k = t / 0.09; sparkle(p, 0, -6, 15 * S * (1 - k * k), WHITE, C[5], 0.6); p.circle(0, -6, 6.5 * S * (1 - k), k < 0.5 ? WHITE : C[6]); }
      for (const s of shards) {
        const u = clamp((t - s.d) / 0.28); if (u <= 0 || u >= 1) continue;
        const L = s.len * ease.outQuart(Math.min(1, u * 2.6)), th = s.w * (1 - u), cs = Math.cos(s.a), sn = Math.sin(s.a) * 0.8;
        for (let d = 0; d <= L; d += 0.5) { const hw = th * (1 - d / L); for (let w = -hw; w <= hw; w += 0.5) p.px(cs * d - sn * w, -6 + sn * d + cs * w, C[clamp(Math.round(6 - (Math.abs(w) / Math.max(0.5, hw)) * 2 - u * 3), 1, 6)]); }
      }
      if (t < 0.22) for (const pts of jag) { const c = t < 0.08 ? WHITE : C[Math.max(2, 6 - Math.round(t * 22))]; for (let i = 1; i < pts.length; i++) p.line(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], c); }
      for (const f of petals) {
        const u = clamp((t - f.d) / 0.42); if (u <= 0 || u >= 1) continue;
        const L = 32 * S * ease.outQuart(Math.min(1, u * 2.8)), W = 8 * S * (1 - u * 0.6), cs = Math.cos(f.a), sn = Math.sin(f.a);
        for (let d = 2; d <= L; d += 0.5) { const hw = W * Math.sin((d / L) * Math.PI); for (let w = -hw; w <= hw; w += 0.5) p.px(cs * d - sn * w, -8 + sn * d + cs * w, C[clamp(Math.round(6 - (Math.abs(w) / Math.max(0.5, hw)) * 3 - u * 2.5), 1, 6)]); }
      }
      bits.draw(p); ps.draw(p); lights.draw(p);
    },
  };
}
