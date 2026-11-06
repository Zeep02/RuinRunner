import { clamp, ease, fbm, lerp, noise, rand, smooth } from "../../core/index.js";
import { INK, BODY, EDGE, RIM, GLINT } from "./_lib.js";

export const meta = {
  name: "Ink rise",
  desc: "Ink wells out of a puddle into a tall cloaked shape, sags, then runs down off whoever is inside; the puddle holds, tears flat and dries. Render the 'puddle' layer under the figure and the 'mound' layer over it.",
  tags: ["ink", "spawn", "revive", "one-shot"],
  size: [120, 96], origin: [60, 84], duration: 1.8, loop: false, fps: 30, bg: "ground",
  options: { layer: "mound", seed: 3, height: 33 },
};

const RISE = 0.2, SAG = 0.7, DRAIN = 1.02, TEAR = 1.42, GONE = 1.76, PRX = 23, PRY = 8;

// half the width of the cloak at share h of its height: a flared hem, shoulders, then a head
function halfWidth(h) {
  const body = lerp(17, 9, smooth(0, 0.6, h)) - 1.2 * smooth(0.6, 0.76, h);
  if (h < 0.76) return body;
  const k = clamp((h - 0.76) / 0.24);
  return Math.min(body, 7.6) * Math.sqrt(Math.max(0, 1 - k * k));
}

export function create(o) {
  const mound = o.layer !== "puddle", H0 = o.height;
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p0) {
      const p = p0;
      if (!mound) {
        const u = smooth(TEAR, GONE, t);
        if (u >= 1) return;
        const rx = PRX * (1 + 0.025 * Math.sin(t * 5)) * lerp(1, 0.86, u), ry = PRY * lerp(1, 0.42, u);
        for (let y = -PRY - 5; y <= PRY + 5; y++)
          for (let x = -PRX - 6; x <= PRX + 6; x++) {
            // tearing stretches the noise sideways, so it comes apart in long flat scraps
            const n = fbm((x + 90) * lerp(0.2, 0.06, u), (y + 40) * lerp(0.2, 0.34, u) + t * 0.5, o.seed, 2);
            const v = Math.hypot(x / rx, y / ry) + n * lerp(0.17, 1.3, u) - lerp(1, 0.55, u);
            if (v < 0) p.px(x, y, v > -0.09 ? (y < 0 ? (rand(x, 11) < 0.25 ? GLINT : RIM) : EDGE) : v > -0.2 ? BODY : INK);
            else if (v < 0.24 && rand(x, y, Math.floor(t * 12)) < 0.3 * (1 - v / 0.24)) p.px(x, y, INK);
          }
        return;
      }
      if (t >= DRAIN + 0.04) return;
      const rise = ease.outBack(clamp(t / RISE)), sag = smooth(RISE, SAG, t), H = H0 * rise * lerp(1, 0.9, sag);
      const wide = lerp(1, 1.1, sag) * (0.85 + 0.15 * clamp(t / RISE));
      // draining: the top comes down the figure and leaves the hem to last
      const cut = H * (1 - ease.inOutCubic(smooth(SAG, DRAIN, t)));
      if (H < 1) return;
      for (let py = 0; py < H + 2; py++) {
        const h = py / H;
        const lean = Math.sin(h * 3.5 + t * 8) * 1.5 * h * (1 - sag * 0.5);
        const w = halfWidth(h) * wide;
        for (let x = Math.floor(-w - 1); x <= Math.ceil(w + 1); x++) {
          const edge = noise(x * 0.5 + 40, o.seed) * 2.6 + Math.sin(x * 0.9 + t * 11) * 0.8;
          if (py > cut + edge) continue;
          const d = w - Math.abs(x - lean);
          if (d < 0 || h > 1) continue;
          const top = cut + edge - py;
          p.px(x, -py, d < 1 ? (x < lean && h > 0.2 ? RIM : EDGE) : top < 1.2 ? EDGE : d < 2.5 ? BODY : INK);
        }
      }
      // what was flung off it as it stood
      if (t < RISE + 0.5)
        for (let i = 0; i < 9; i++) {
          const a = t - 0.04 - i * 0.012;
          if (a <= 0) continue;
          const vx = (rand(i, o.seed) - 0.5) * 80, vy = 35 + rand(i, o.seed, 5) * 60;
          const x = vx * a, y = -vy * a + 190 * a * a;
          if (y < 2) p.dot(x, y - 4, rand(i, 3) < 0.3 ? EDGE : INK);
        }
    },
  };
}
