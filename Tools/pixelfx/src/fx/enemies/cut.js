// A monster's straight cut: one thin tapering stroke from the origin along +x, L pixels long. Plain on purpose: no white flash, no echo.
import { Particles, Rng, fit, P } from "../../core/index.js";
import { through, slash, slashEnd } from "../melee/_lib.js";

export const meta = {
  name: "Enemy cut",
  desc: "A thin blade stroke along the aim, barely bowed, gone quickly with a few sparks off its far end. L matches the attack's hit box. Plain, so it reads under the hero's attacks.",
  tags: ["enemies", "melee", "line", "directional", "one-shot"],
  size: [240, 140],
  origin: [30, 70],
  duration: 0.42,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#14161c", "#2c3038", "#4a4f5a", "#70757f", "#9ea3ac", "#d0d4da", "#f6f7f8"], L: 122, W: 9, sparksN: 4, seed: 7 },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, sparks: [C[4], C[3]] });
  // A wide circle whose chord is L: the stroke reads straight with only a slight bow.
  const R = o.L * 1.35, span = 2 * Math.asin(o.L / 2 / R);
  const edge = through(o.L / 2, -o.W * 0.5, 0, 1, R, span, 0.5, { W: o.W, t0: 0, sweep: 0.09, hold: 0.02, trail: 0.11, ramp: C.slice(1, 6) });
  const end = slashEnd(edge);
  let t = 0, thrown = false;
  return {
    update(dt) {
      t += dt;
      if (!thrown && t >= edge.sweep * 0.8) {
        thrown = true;
        for (let i = 0; i < o.sparksN; i++) {
          const a = Rg.range(-0.5, 0.5), sp = Rg.range(60, 150);
          ps.spawn(P.SPARK, end.x, end.y, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.08, 0.18));
        }
      }
      ps.update(dt);
    },
    draw(p) {
      slash(p, edge, t);
      ps.draw(p);
    },
  };
}
