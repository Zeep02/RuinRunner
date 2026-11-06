import { Particles, Lights, P, Rng, fit, clamp, ease } from "../../core/index.js";

export const meta = {
  name: "Weapon stab",
  desc: "A short stab ahead of the hand: a tapered wedge of light, optionally forked, snuffed from the back into a spit of sparks.",
  tags: ["weapon", "melee", "thrust", "stab", "directional", "one-shot"],
  size: [220, 80], origin: [16, 40], duration: 0.208, loop: false, fps: 45, bg: "ground",
  options: { palette: ["#2a0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"], sparks: ["#fff3c4", "#ffd166", "#ff8a2a", "#c42a1a"], seed: 12, LEN: 40, W: 7, fork: 0 },
};

const T_HIT = 0.036, OUT = 0.05, GONE = 0.17;

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, ground: () => 36, sparks: o.sparks }), lights = new Lights(), LEN = o.LEN;
  let t = 0, struck = false;
  return {
    update(dt) {
      t += dt;
      if (!struck && t >= T_HIT) {
        struck = true;
        lights.add(LEN, 0, 14 + LEN * 0.1, C[4], 0.4, 0.09);
        for (let i = 0; i < 5; i++) { const a = Rg.range(-0.5, 0.5), sp = Rg.range(70, 170); ps.spawn(P.SPARK, LEN, 0, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.04, 0.1)); }
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const head = LEN * ease.outQuart(clamp(t / OUT)), tail = LEN * ease.inOutQuad(clamp((t - OUT) / (GONE - OUT)));
      if (head - tail > 1) {
        const age = clamp(t / GONE);
        for (let x = tail; x <= head; x += 0.5) {
          const v = (x - tail) / (head - tail), w = o.W * Math.pow(v, 0.6) * (v > 0.82 ? (1 - v) / 0.18 : 1) * (1 - age * 0.5);
          for (let y = -w * 0.5; y <= w * 0.5; y += 0.5) {
            const q = Math.abs(y) / Math.max(0.5, w * 0.5);
            const yy = o.fork && v > 0.72 ? y + Math.sign(y || 1) * (v - 0.72) * o.fork : y;
            p.px(x, yy, C[clamp(Math.round(6 - q * 3 - age * 2.5 - (1 - v) * 1.5), 1, 6)]);
          }
        }
      }
      ps.draw(p); lights.draw(p);
    },
  };
}
