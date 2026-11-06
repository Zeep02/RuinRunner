import { clamp, fit, rand } from "../../core/index.js";

export const meta = {
  name: "Footfall",
  desc: "A foot coming down, small enough to play on every step: 'dust' scuffs up two puffs, 'ripple' spreads a flat ring on wet stone, 'ash' lifts a few glowing flecks.",
  tags: ["step", "footfall", "dust", "one-shot"],
  size: [28, 22], origin: [14, 16], duration: 0.46, loop: false, fps: 30, bg: "ground",
  options: { kind: "dust", palette: ["#6e6444", "#a3966c", "#d1c698"], seed: 4 },
};

const D = meta.duration;

export function create(o) {
  const C = fit(o.palette, 3);
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const u = clamp(t / D);
      if (o.kind === "ripple") {
        const rx = 2 + 9 * u, ry = 1 + 3.2 * u, steps = Math.ceil(rx * 7);
        for (let i = 0; i < steps; i++) {
          const a = (i / steps) * 6.283;
          if (rand(i, 3) < u * 0.7) continue;
          p.dot(Math.cos(a) * rx, Math.sin(a) * ry, u < 0.4 ? C[2] : C[1], 1 - u * 0.6);
        }
        return;
      }
      if (o.kind === "ash") {
        for (let i = 0; i < 4; i++) {
          const x = (rand(i, o.seed) - 0.5) * 8 + Math.sin(t * 6 + i * 2) * 1.2, y = -u * (5 + rand(i, 5) * 7);
          if (u < 0.45 + rand(i, 9) * 0.5) p.dot(x, y, u < 0.5 ? C[2] : C[1], 1);
        }
        return;
      }
      for (let i = 0; i < 3; i++) {
        const side = i === 0 ? -1 : i === 1 ? 1 : 0;
        const x = side * (1.5 + u * 5), y = -u * (i === 2 ? 3.5 : 1.8), r = (i === 2 ? 1.2 : 1.7) * (1 + u * 0.7) * (1 - u * u);
        for (let dy = -2; dy <= 2; dy++)
          for (let dx = -3; dx <= 3; dx++)
            if (Math.hypot(dx, dy * 1.3) <= r) p.dot(x + dx, y + dy, dy < 0 ? C[2] : C[1], 1 - u * 0.5);
      }
    },
  };
}
