import { clamp, ease, fbm, fit, rand } from "../../core/index.js";

export const meta = {
  name: "Floor stain",
  desc: "What is left on the floor where something fell: a flat splat that spreads in a few frames and flicks out droplets, then stays as a mark.",
  tags: ["stain", "floor", "decal", "one-shot"],
  size: [72, 44], origin: [36, 22], duration: 0.34, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#141c0c", "#22331a", "#3c5a24"], seed: 11 },
};

const RX = 15, RY = 7.5, D = meta.duration;

export function create(o) {
  const C = fit(o.palette, 3);
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const k = ease.outCubic(clamp(t / D));
      const rx = RX * (0.3 + 0.7 * k), ry = RY * (0.3 + 0.7 * k);
      for (let y = -14; y <= 14; y++)
        for (let x = -24; x <= 24; x++) {
          const n = fbm((x + 70) * 0.16, (y + 40) * 0.3, o.seed, 2);
          const v = Math.hypot(x / rx, y / ry) + n * 0.5 - 1;
          if (v < 0) p.px(x, y, v > -0.16 ? C[y < 0 ? 2 : 1] : v > -0.5 ? C[1] : C[0]);
        }
      // droplets thrown clear of the main mark; they land and stay
      for (let i = 0; i < 9; i++) {
        const a = rand(i, o.seed) * 6.283, d = 1.1 + rand(i, o.seed, 2) * 0.75;
        const reach = clamp(t / (D * (0.5 + rand(i, 4) * 0.5)));
        const x = Math.round(Math.cos(a) * RX * d * reach), y = Math.round(Math.sin(a) * RY * d * reach);
        p.px(x, y, C[rand(i, 8) < 0.5 ? 0 : 1]);
        if (i % 3 === 0) p.px(x + 1, y, C[0]);
      }
    },
  };
}
