import { Particles, P, Rng, sparkle, clamp, ease, WHITE, hex, TAU } from "../../core/index.js";

export const meta = {
  name: "Hit ring",
  desc: "The contact: a one-frame white disc, a hard ring that snaps outward and thins to nothing, four needle flares and a spray of sparks.",
  tags: ["player", "hit", "impact", "flash", "one-shot"],
  size: [112, 112], origin: [56, 56], duration: 0.26, loop: false, fps: 30, bg: "dark", options: { seed: 4 },
};

const GOLD = hex("#ffd86a");
export function create(o) {
  const R = new Rng(o.seed), ps = new Particles({ rng: R, sparks: [WHITE, GOLD, hex("#f08a2c"), hex("#c8641f")] });
  for (let i = 0; i < 14; i++) { const a = R.range(0, TAU), sp = R.range(120, 360); ps.spawn(P.SPARK, 0, 0, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.08, 0.2), WHITE); }
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); },
    draw(p) {
      const v = clamp(t / 0.17), r = 7 + 26 * ease.outCubic(v), w = 7 * (1 - v) ** 1.4 + 0.6;
      if (t < 0.035) { p.circle(0, 0, 12, () => WHITE); p.glow(0, 0, 40, GOLD, 0.8); }
      if (v < 1) p.ring(0, 0, r, w, v < 0.4 ? WHITE : GOLD);
      if (t < 0.1) sparkle(p, 0, 0, 22 * (1 - t / 0.1), WHITE, GOLD);
      ps.draw(p);
    },
  };
}
