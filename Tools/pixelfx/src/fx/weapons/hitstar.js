import { Particles, Lights, P, Rng, fit, clamp, WHITE, hex } from "../../core/index.js";
import { star } from "../melee/_lib.js";

export const meta = {
  name: "Weapon hit star",
  desc: "A blade or bolt contact: a hard white-hot star with a coloured rim, sparks along the blow and an optional coloured dot at its heart.",
  tags: ["weapon", "impact", "hit", "directional", "one-shot"],
  size: [120, 120], origin: [60, 60], duration: 0.3, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#2a0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"], sparks: ["#fff3c4", "#ffd166", "#ff8a2a", "#c42a1a"], seed: 13, len: 13, spikes: 4, sparksN: 9, dot: "" },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, ground: () => 40, sparks: o.sparks }), lights = new Lights();
  const spikes = [];
  for (let i = 0; i < o.spikes; i++) spikes.push({ a: (i / o.spikes) * Math.PI * 2 + 0.32, len: i === 0 ? o.len : o.len * 0.6, w: 2.2 + o.len * 0.05 });
  lights.add(0, 0, o.len * 1.8, C[4], 0.5, 0.14); lights.add(0, 0, o.len * 0.7, WHITE, 0.55, 0.05);
  for (let i = 0; i < o.sparksN; i++) { const a = Rg.range(-0.8, 1.1), sp = Rg.range(70, 200); ps.spawn(P.SPARK, 1, 0, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.08, 0.2)); }
  const dot = o.dot ? hex(o.dot) : 0;
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); lights.update(dt); },
    draw(p) {
      if (t < 0.13) {
        const k = clamp(t / 0.13), sc = k < 0.25 ? 0.55 + k * 2.2 : 1.1 - (k - 0.25) * 0.35, th = 1 - k * 0.85;
        star(p, 0, 0, spikes, sc, th, k < 0.45 ? WHITE : C[6], C[4], C[5], C[2]);
      }
      if (dot && t < 0.2) { const r = 2 + 3 * clamp(t / 0.08); p.circle(0, 0, r * (1 - clamp((t - 0.1) / 0.1)), dot); }
      ps.draw(p); lights.draw(p);
    },
  };
}
