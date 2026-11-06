import { fit, WHITE } from "../../core/index.js";
import { noiseFor, looped } from "./_lib.js";

export const meta = {
  name: "Lamp glow",
  desc: "No flame of its own: the unsteady light and a few rising motes to lay over a lamp that is already drawn, so a still picture looks lit.",
  tags: ["fire", "lamp", "light", "ambient", "loop"],
  size: [56, 64],
  origin: [28, 36],
  duration: 3,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#7a3a10", "#c9681c", "#f4a42c", "#ffd878"], seed: 4 },
};

const D = meta.duration, NL = noiseFor(D);

export function draw(p, t, o) {
  const C = fit(o.palette, 4), S = o.seed;
  const fl = 0.78 + 0.14 * NL(t, 3, S) + 0.08 * NL(t, 8, S + 1);
  p.glow(0, 0, 24 * fl, C[1], 0.3 * fl);
  p.glow(0, 0, 11 * fl, C[2], 0.42 * fl);
  p.glow(0, 0, 4.5, C[3], 0.5 * fl);
  looped(t, D, 5, S + 40, (r) => ({ x: r.range(-3, 3), vx: r.range(-2.5, 2.5), vy: -r.range(7, 12), life: r.range(0.9, 1.6) }), (q, age) => {
    const k = 1 - age / q.life;
    const x = q.x + q.vx * age + NL(t - age, 1, S + 6) * age * 2, y = -3 + q.vy * age;
    p.dot(x, y, k > 0.6 ? WHITE : k > 0.3 ? C[3] : C[2], Math.min(1, k * 1.6));
    p.glow(x, y, 2.5, C[2], 0.2 * k);
  });
}
