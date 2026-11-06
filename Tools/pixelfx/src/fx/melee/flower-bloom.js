// A flower cut into the air: five slashes open out of one point in quick succession like petals, white along their edges, and
// fall apart into drifting petals. Made for the last blow of a blossom katana; R is the reach of the blow.
import { Rng, fit, clamp, seg, ease, TAU } from "../../core/index.js";

export const meta = {
  name: "Flower bloom",
  desc: "Five crescent petal slashes burst out of a point one after another, hollow with white edges, then break into a drift of falling petals.",
  tags: ["melee", "slash", "flower", "petal", "burst", "one-shot"],
  size: [208, 208],
  origin: [104, 104],
  duration: 1.15,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2c0e20", "#5e1c40", "#a03068", "#d85a90", "#ff90b8", "#ffc8dc", "#fff4f8"], R: 72, petals: 5, seed: 7 },
};

const START = 0.03, STEP = 0.035, OPEN = 0.14, HOLD = 0.3, FALL = 0.62;

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), n = o.petals, R = o.R;
  const drift = [];
  for (let i = 0; i < 46; i++) {
    drift.push({ a: Rg.range(0, TAU), d: Rg.range(0.25, 1) * R, sp: Rg.range(18, 62), wob: Rg.range(0, TAU), fall: Rg.range(10, 34), late: Rg.range(0, 0.14), life: Rg.range(0.38, 0.72), c: 3 + (i % 4) });
  }
  let t = 0;
  const petal = (p, a, open, close) => {
    const L = R * open, cs = Math.cos(a), sn = Math.sin(a);
    for (let s = 0; s <= L; s += 0.5) {
      const v = s / Math.max(1, R), hw = R * 0.26 * Math.sin(Math.PI * Math.pow(clamp(v), 0.72)) * (1 - close * 0.55);
      if (hw < 0.4) continue;
      const curl = Math.sin(v * Math.PI) * R * 0.065;
      for (let w = -hw; w <= hw; w += 0.5) {
        const e = Math.abs(w) / hw;
        // Hollow: the fill burns out from the middle while the edges stay lit, so it reads as a cut and not a solid leaf.
        if (e < close * 0.95) continue;
        const i = e > 0.78 ? 6 - close * 2 : 2 + e * 3 - close;
        p.px(cs * s - sn * (w + curl), sn * s + cs * (w + curl), C[clamp(Math.round(i), 1, 6)]);
      }
    }
  };
  return {
    update(dt) { t += dt; },
    draw(p) {
      // A shorter second layer opens between the first, so the flower has depth and not just an outline.
      for (let i = 0; i < n; i++) {
        const t0 = START + 0.1 + i * STEP, a = ((i + 0.5) / n) * TAU - Math.PI / 2;
        const open = ease.outQuart(seg(t, t0, t0 + OPEN)) * 0.66, close = seg(t, HOLD + 0.04 + i * 0.015, FALL + i * 0.015);
        if (open > 0 && close < 1) petal(p, a, open, Math.max(close, 0.35));
      }
      for (let i = 0; i < n; i++) {
        const t0 = START + i * STEP, a = (i / n) * TAU - Math.PI / 2;
        const open = ease.outQuart(seg(t, t0, t0 + OPEN)), close = seg(t, HOLD + i * 0.015, FALL + i * 0.015);
        if (open <= 0 || close >= 1) continue;
        petal(p, a, open, close);
        if (t < t0 + 0.05 + OPEN) p.glowLine(0, 0, Math.cos(a) * R * open * 0.8, Math.sin(a) * R * open * 0.8, 12, C[4], 0.3 * (1 - close));
      }
      const flash = 1 - seg(t, 0.02, 0.2);
      if (flash > 0) { p.circle(0, 0, 9 * flash + 1, C[6]); p.glow(0, 0, 40, C[5], 0.75 * flash); }
      for (const q of drift) {
        const u = (t - 0.14 - q.late) / q.life;
        if (u <= 0 || u >= 1) continue;
        const d = q.d * Math.min(1, ease.outQuart(clamp(u * 2))) + q.sp * u * 0.4;
        const x = Math.cos(q.a) * d + Math.sin(q.wob + u * 7) * 3, y = Math.sin(q.a) * d + q.fall * u * u;
        const col = C[clamp(u > 0.7 ? q.c - 2 : q.c, 1, 6)];
        p.px(x, y, col); p.px(x + 1, y, col); if (u < 0.6) p.px(x, y - 1, C[clamp(q.c + 1, 1, 6)]);
      }
    },
  };
}
