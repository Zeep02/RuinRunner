import { fit } from "../../core/index.js";
import { noiseFor, smokePuff, looped, drift } from "./_lib.js";

export const meta = {
  name: "Prop embers",
  desc: "No flame of its own: a flickering warm light, embers that climb and wander, and a thin thread of smoke, to lay over a candle or brazier that already draws its fire.",
  tags: ["fire", "ember", "smoke", "prop", "light", "ambient", "loop"],
  size: [56, 72],
  origin: [28, 50],
  duration: 4,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#7a3a10", "#c9681c", "#f4a42c", "#ffd878"], seed: 3, embers: 9, smoke: 7 },
};

const D = meta.duration, NL = noiseFor(D);

export function draw(p, t, o) {
  const C = fit(o.palette, 4), S = o.seed;
  const fl = 0.8 + 0.13 * NL(t, 3, S) + 0.07 * NL(t, 8, S + 1), sway = NL(t, 0.75, S + 2) * 1.5;
  p.glow(0, 0, 20 * fl, C[1], 0.2 * fl);
  p.glow(0, 0, 9 * fl, C[2], 0.28 * fl);
  // The smoke goes first, so embers pass in front of it.
  looped(t, D, o.smoke, S + 300, (r) => ({ vx: r.range(-1, 1), vy: -r.range(8, 12), life: r.range(1.4, 2.3), s: r.range(0.6, 0.95) }), (q, age) => {
    const [x, y] = drift(sway, -7, q.vx, q.vy, age, 1, -5, 1, 0.8);
    smokePuff(p, x + NL(t - age, 0.75, S + 60) * (1 + age * 1.6), y, 1 - age / q.life, q.s, 0, false, 0.42);
  });
  looped(t, D, o.embers, S + 40, (r) => ({ x: r.range(-5, 5), vx: r.range(-3, 3), vy: -r.range(9, 17), life: r.range(0.8, 1.9), bright: r.range(0, 1) }), (q, age) => {
    const k = 1 - age / q.life;
    const x = q.x + q.vx * age + NL(t - age, 1.5, S + 6) * age * 3, y = -4 + q.vy * age;
    const c = k > 0.55 ? (q.bright > 0.5 ? C[3] : C[2]) : k > 0.25 ? C[2] : C[1];
    p.dot(x, y, c, Math.min(1, k * 1.8));
    p.glow(x, y, 2.5, C[2], 0.18 * k);
  });
}
