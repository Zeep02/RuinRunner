// A spear of light thrown along +x from the origin: the tip shoots out in a blink, a star rides it, and the shaft thins and goes out from the back.
import { Rng, fit, clamp, seg, ease } from "../../core/index.js";

export const meta = {
  name: "Light spear",
  desc: "A long lance of white light flies out along the aim in a few frames with a star at its tip, then its shaft narrows from behind and leaves a line of motes.",
  tags: ["holy", "beam", "spear", "lance", "directional", "one-shot"],
  size: [304, 80],
  origin: [28, 40],
  duration: 0.62,
  loop: false,
  fps: 30,
  bg: "ground",
  options: { palette: ["#060a20", "#10184a", "#1e2c80", "#3a54c0", "#7c9cf0", "#c4d8ff", "#ffffff"], L: 256, W: 9, seed: 4, stars: 0 },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), L = o.L, W = o.W;
  const motes = [];
  for (let i = 0; i < 34; i++) motes.push({ x: Rg.range(0.04, 1) * L, y: Rg.range(-1, 1) * W * 1.5, up: Rg.range(4, 20), late: Rg.range(0, 0.12), life: Rg.range(0.2, 0.38), c: 4 + (i % 3) });
  const stars = [];
  for (let i = 0; i < (o.stars || 0); i++) stars.push({ x: Rg.range(0.05, 1) * L, y: Rg.range(-1, 1) * W * 1.1, late: Rg.range(0.02, 0.2), r: Rg.range(2, 4.5) });
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const head = L * ease.outExpo(seg(t, 0, 0.09)), tail = head * ease.outQuart(seg(t, 0.14, 0.42)) * 0.9;
      const thin = 1 - seg(t, 0.1, 0.46);
      if (thin > 0 && head - tail > 1) {
        p.glowLine(tail, 0, head, 0, W * 1.8, C[4], 0.42 * thin);
        for (let x = tail; x < head; x += 0.5) {
          const tip = clamp((head - x) / (W * 5));
          const w = W * thin * Math.sqrt(tip) * clamp((x - tail) / (W * 2) + 0.25);
          for (let y = -w; y <= w; y += 0.5) {
            const e = Math.abs(y) / Math.max(0.5, w);
            p.px(x, y, C[clamp(Math.round(6.4 - e * e * 3.4), 2, 6)]);
          }
        }
      }
      if (t < 0.24) {
        const s = (1 - seg(t, 0.09, 0.24)) * W * 2.6 + 2;
        for (let d = -s; d <= s; d += 0.5) { p.px(head + d, 0, C[6]); p.px(head, d * 0.75, C[6]); }
        p.glow(head, 0, W * 3.2, C[5], 0.7 * (1 - seg(t, 0.09, 0.24)));
      }
      if (t < 0.1) { const k = 1 - t / 0.1; p.circle(0, 0, W * 0.9 * k + 1, C[6]); p.glow(0, 0, W * 3, C[5], 0.7 * k); }
      for (const m of motes) {
        const u = (t - 0.1 - m.late) / m.life;
        if (u <= 0 || u >= 1 || m.x > head) continue;
        p.px(m.x + u * 6, m.y - m.up * u, C[clamp(Math.round(m.c - u * 2), 1, 6)]);
      }
      for (const s of stars) {
        const u = (t - s.late) / 0.3;
        if (u <= 0 || u >= 1 || s.x > head) continue;
        const r = s.r * Math.sin(Math.PI * u);
        for (let d = -r; d <= r; d += 0.5) { p.px(s.x + d, s.y, C[6]); p.px(s.x, s.y + d, C[5]); }
      }
    },
  };
}
