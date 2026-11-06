// A monster's sweep around its own body, drawn flat so the arc lies exactly on the sector the attack hurts. Faces +x, centred on the
// origin; the game turns it to the aim. Deliberately plain: thin, mid-tone, no white flash, no echo. The hero's slash is the showy one.
import { Particles, Rng, fit, P } from "../../core/index.js";
import { swing, slash, slashEnd } from "../melee/_lib.js";

export const meta = {
  name: "Enemy sweep",
  desc: "A thin mid-tone arc passes over a sector centred on the attacker and is gone, shedding a few sparks. R and span match the hit area. Plain, so it reads under the hero's attacks.",
  tags: ["enemies", "melee", "sweep", "directional", "one-shot"],
  size: [260, 260],
  origin: [130, 130],
  duration: 0.45,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#241006", "#52240a", "#8e4210", "#cc7018", "#f4a42c", "#ffd878", "#fff6d8"], R: 80, W: 10, span: 150, sparksN: 4, seed: 5 },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, sparks: [C[4], C[3]] });
  const span = (o.span * Math.PI) / 180;
  // The ramp stops short of the two brightest colours.
  const edge = swing({ R: o.R, sy: 1, a0: -span / 2, dir: 1, span, ramp: C.slice(1, 6), W: o.W, t0: 0, sweep: 0.1, hold: 0.02, trail: 0.12 });
  const end = slashEnd(edge);
  let t = 0, thrown = false;
  return {
    update(dt) {
      t += dt;
      if (!thrown && t >= edge.sweep * 0.8) {
        thrown = true;
        const out = Math.atan2(end.dy, end.dx);
        for (let i = 0; i < o.sparksN; i++) {
          const a = out + Rg.range(-0.5, 0.5), sp = Rg.range(60, 150);
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
