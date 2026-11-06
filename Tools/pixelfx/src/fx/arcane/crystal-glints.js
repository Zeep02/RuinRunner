import { fit, sparkle } from "../../core/index.js";
import { noiseFor, looped } from "../fire/_lib.js";

export const meta = {
  name: "Crystal glints",
  desc: "No crystal of its own: to lay over a crystal cluster already drawn. A slow breathing glow, star glints that catch on its points in turn, and motes that lift off it.",
  tags: ["arcane", "crystal", "glint", "sparkle", "prop", "light", "ambient", "loop"],
  size: [56, 64],
  origin: [28, 46],
  duration: 4,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#3c2460", "#8048b8", "#a970e0", "#e0c8ff"], seed: 7, motes: 8 },
};

const D = meta.duration, NL = noiseFor(D);
// The points of the cluster, from its foot, and when in the loop each one catches the light.
const POINTS = [[0, -21, 0.05], [-7, -13, 0.3], [7, -12, 0.55], [-2, -8, 0.8]];
const GLINT_LENGTH = 0.14;

export function draw(p, t, o) {
  const C = fit(o.palette, 4), S = o.seed, phase = t / D;
  const breath = 0.72 + 0.28 * Math.sin(phase * Math.PI * 2);
  p.glow(0, -11, 20 * breath, C[1], 0.2 * breath);
  p.glow(0, -13, 9 * breath, C[2], 0.2 * breath);
  looped(t, D, o.motes, S + 40, (r) => ({ x: r.range(-8, 8), vx: r.range(-1.5, 1.5), vy: -r.range(5, 10), life: r.range(1.2, 2.4) }), (q, age) => {
    const k = 1 - age / q.life;
    const x = q.x + q.vx * age + NL(t - age, 1, S + 6) * age * 2, y = -7 + q.vy * age;
    p.dot(x, y, k > 0.5 ? C[3] : k > 0.25 ? C[2] : C[1], Math.min(1, k * 1.8));
    p.glow(x, y, 2, C[1], 0.16 * k);
  });
  for (const [x, y, at] of POINTS) {
    const into = ((phase - at) % 1 + 1) % 1;
    if (into >= GLINT_LENGTH) continue;
    const k = Math.sin((into / GLINT_LENGTH) * Math.PI);
    sparkle(p, x, y, 1 + k * 1.5, C[3], C[2], 0.2 * k);
  }
}
