import { fit, WHITE } from "../../core/index.js";
import { noiseFor, looped } from "../fire/_lib.js";

export const meta = {
  name: "Prop bubbles",
  desc: "A few small bubbles leaving something already drawn, a coral or a clump of reeds: they wobble up, swell a little, and pop into a dot.",
  tags: ["water", "bubble", "prop", "ambient", "loop"],
  size: [48, 64],
  origin: [24, 50],
  duration: 4,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0e4a58", "#1c8ebc", "#88e8e0", "#e0fff8"], seed: 9, bubbles: 6, spread: 6 },
};

const D = meta.duration, NL = noiseFor(D), POP = 0.14;

export function draw(p, t, o) {
  const C = fit(o.palette, 4), S = o.seed;
  looped(t, D, o.bubbles, S + 40, (r) => ({ x: r.range(-o.spread, o.spread), vy: -r.range(9, 15), life: r.range(1.1, 2.1), size: r.range(1, 2.2), wob: r.range(0, 6) }), (q, age) => {
    const left = q.life - age;
    const x = q.x + Math.sin(age * 5 + q.wob) * 1.1 + NL(t - age, 1, S + 6) * 1.2, y = q.vy * age;
    if (left < POP) {
      // the pop: the rim flies apart in four dots
      const u = 1 - left / POP, r = q.size + 1 + u * 2;
      for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) p.dot(x + dx * r, y + dy * r, C[2], 1 - u);
      return;
    }
    const r = Math.max(0.8, q.size * Math.min(1, 0.45 + age * 0.9));
    if (r < 1.3) {
      p.dot(x, y, C[2], 0.9);
      return;
    }
    p.ring(x, y, r, 1, C[1], 0.85);
    p.dot(x - r * 0.5, y - r * 0.5, age > 0.2 ? C[3] : WHITE, 0.95);
    p.glow(x, y, 3, C[1], 0.1);
  });
}
