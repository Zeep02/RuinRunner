// A monster's beam: a straight lance through the attacker, L pixels end to end, lasting a blink. Grows out of the source point, thins, ends empty.
import { Particles, Lights, P, Rng, fit, flare, clamp, smooth, ease } from "../../core/index.js";

export const meta = {
  name: "Enemy beam",
  desc: "A lance of light shoots both ways from its source: a white core in two rippling hot bands, a flare at the source, sparks along the line. Fattens, then pinches out.",
  tags: ["enemies", "arcane", "beam", "line", "directional", "one-shot"],
  size: [480, 90],
  origin: [20, 45],
  duration: 0.5,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#2a1c08", "#5a3e10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6"], L: 420, W: 14, source: 0.5, seed: 9 },
};

const GROW = 0.05, HOLD = 0.12, GONE = 0.34;

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, sparks: [C[6], C[5], C[4]] }), lights = new Lights();
  const S = o.L * o.source, reach = Math.max(S, o.L - S);
  let t = 0, spawned = 0;
  return {
    update(dt) {
      t += dt;
      // sparks peel off the line while it burns
      if (t < HOLD + 0.08) {
        for (const want = Math.floor((t / (HOLD + 0.08)) * (o.L / 18)); spawned < want; spawned++) {
          const x = Rg.range(0, o.L), side = Rg.chance(0.5) ? 1 : -1;
          ps.spawn(P.SPARK, x, side * Rg.range(1, o.W * 0.4), Rg.range(-50, 50), side * Rg.range(40, 150), Rg.range(0.1, 0.24));
        }
      }
      ps.update(dt);
      lights.update(dt);
    },
    draw(p) {
      const grown = reach * ease.outExpo(clamp(t / GROW)), left = Math.max(0, S - grown), right = Math.min(o.L, S + grown);
      const pinch = 1 - smooth(HOLD, GONE, t), burst = 1 + 0.45 * (1 - clamp(t / 0.07)), half = (o.W / 2) * pinch * burst;
      if (half > 0.3) {
        const f = Math.floor(t * 30);
        p.shade(left, -half - 2, right, half + 2, (x, y, out) => {
          // Two ripples at different lengths keep the edge from reading as a ruled line.
          const ripple = 1 + 0.14 * Math.sin(x * 0.11 - f * 2.1) + 0.06 * Math.sin(x * 0.37 + f * 1.3), q = Math.abs(y) / (half * ripple);
          if (q > 1) return;
          // The ends taper over a few pixels so the lance has points.
          if (q > Math.min(1, Math.min(x - left, right - x) / 10)) return;
          out.c = C[q < 0.3 ? 6 : q < 0.55 ? 5 : q < 0.8 ? 4 : 3];
          out.a = 1;
        });
        for (let x = left; x <= right; x += 20) p.glow(x, 0, half * 2.6 + 3, C[3], 0.22 * pinch);
      }
      const q = t / 0.26;
      if (q < 1) {
        flare(p, S, 0, o.W * 1.7 * Math.pow(1 - q, 1.4), 1 - q, C);
        p.glow(S, 0, o.W * 2.6, C[4], 0.5 * (1 - q));
      }
      ps.draw(p);
      lights.draw(p);
    },
  };
}
