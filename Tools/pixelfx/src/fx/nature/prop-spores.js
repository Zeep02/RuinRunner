import { fit } from "../../core/index.js";
import { noiseFor, looped } from "../fire/_lib.js";

export const meta = {
  name: "Prop spores",
  desc: "Specks lifting off a plant that is already drawn: spores from mushrooms, pollen from grass. They rise slowly, wander on the air, catch a little light and thin out.",
  tags: ["nature", "spore", "pollen", "prop", "ambient", "loop"],
  size: [56, 64],
  origin: [28, 46],
  duration: 4,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#4a5a24", "#84ac48", "#d8e080", "#fbf6c8"], seed: 5, motes: 7, spread: 8, drift: 2, glow: 0.14 },
};

const D = meta.duration, NL = noiseFor(D);

export function draw(p, t, o) {
  const C = fit(o.palette, 4), S = o.seed;
  looped(t, D, o.motes, S + 40, (r) => ({ x: r.range(-o.spread, o.spread), vx: r.range(-1, 1) + o.drift, vy: -r.range(4, 9), life: r.range(1.4, 2.8), big: r.range(0, 1) }), (q, age) => {
    const k = 1 - age / q.life, fade = Math.min(1, age * 3, k * 2.5);
    const x = q.x + q.vx * age + NL(t - age, 1, S + 6) * age * 3, y = q.vy * age;
    const c = k > 0.5 ? C[q.big > 0.6 ? 3 : 2] : k > 0.25 ? C[2] : C[1];
    p.dot(x, y, c, fade);
    if (q.big > 0.8) p.dot(x + 1, y, C[1], fade * 0.7);
    p.glow(x, y, 2, C[1], o.glow * fade);
  });
}
