// A tusked beast goring forward along +x: two short horn streaks stab out from the head, grit kicks up behind them. No sweep and no flash.
import { Particles, P, Rng, fit, clamp, smooth } from "../../core/index.js";

export const meta = {
  name: "Enemy gore",
  desc: "Two short tapered streaks stab straight out along the charge, a little grit and dust kicks off the ground. Plain and quick, so it never outshines the hero's attacks.",
  tags: ["enemies", "melee", "thrust", "gore", "directional", "one-shot"],
  size: [160, 90],
  origin: [20, 45],
  duration: 0.5,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#1a1208", "#3e2c14", "#6c5024", "#a0803c", "#ccb060", "#ecdca0", "#fffcec"], reach: 64, spread: 9, seed: 6 },
};

const STAB = 0.07, GONE = 0.24;

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, sparks: [C[5], C[4], C[3]] });
  let t = 0, kicked = false;
  return {
    update(dt) {
      t += dt;
      if (!kicked && t >= STAB * 0.7) {
        kicked = true;
        for (let i = 0; i < 7; i++) {
          const a = Rg.range(-0.7, 0.7), sp = Rg.range(30, 90);
          ps.spawn(P.MOTE, o.reach * Rg.range(0.5, 0.95), Rg.range(-o.spread, o.spread), Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.14, 0.3), Rg.pick([C[2], C[3], C[4]]), 0.6);
        }
      }
      ps.update(dt);
    },
    draw(p) {
      const out = smooth(0, STAB, t), back = smooth(STAB, GONE, t);
      if (back >= 1) { ps.draw(p); return; }
      for (const side of [-1, 1]) {
        const head = o.reach * out, tail = o.reach * back * 0.9, length = head - tail;
        if (length < 1) continue;
        for (let d = 0; d <= length; d += 0.5) {
          const v = d / length, x = tail + d, half = 2.6 * Math.sin(Math.PI * Math.pow(v, 1.4)) * (1 - back * 0.6);
          // The streaks open slightly with distance, as a pair of tusks do.
          const y = side * o.spread * (0.45 + 0.55 * clamp(x / o.reach));
          for (let w = -half; w <= half; w += 0.5) p.px(x, y + w, C[Math.abs(w) < half * 0.4 ? 5 : 3]);
        }
      }
      ps.draw(p);
    },
  };
}
