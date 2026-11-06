import { Particles, P, Rng, sparkle, clamp, ease, WHITE, hex } from "../../core/index.js";

export const meta = {
  name: "Cut flash",
  desc: "A body-sized crescent that exists for a heartbeat: a white arc whips round in two frames, thins to gold and is gone, throwing a few hard sparks off its tip.",
  tags: ["player", "melee", "slash", "flash", "one-shot"],
  size: [136, 136], origin: [40, 68], duration: 0.3, loop: false, fps: 30, bg: "dark", options: { seed: 2 },
};

const GOLD = hex("#ffd86a"), DEEP = hex("#f08a2c");
export function create(o) {
  const R = new Rng(o.seed), ps = new Particles({ rng: R, sparks: [WHITE, GOLD, DEEP, DEEP] });
  for (let i = 0; i < 10; i++) { const a = R.range(-0.7, 0.9), sp = R.range(120, 340); ps.spawn(P.SPARK, 40, 0, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.08, 0.2), WHITE); }
  let t = 0;
  const A0 = -1.45, A1 = 1.35;
  return {
    update(dt) { t += dt; ps.update(dt); },
    draw(p) {
      const sweep = ease.outCubic(clamp(t / 0.05)), fade = clamp((t - 0.05) / 0.17), head = A0 + (A1 - A0) * sweep, tail = A0 + (A1 - A0) * Math.max(0, fade * 1.1), thin = 1 - fade;
      if (thin > 0 && head > tail) {
        const R0 = 44 + fade * 6, c = fade < 0.25 ? WHITE : fade < 0.6 ? GOLD : DEEP;
        p.arc(0, 0, R0, (u) => (11 * thin + 1) * Math.sin(u * Math.PI) ** 0.7, tail, head, c, 1);
        if (fade < 0.4) p.arc(0, 0, R0 + 3, (u) => 3 * Math.sin(u * Math.PI), tail, head, WHITE, 1);
        p.arc(0, 0, R0 - 12, (u) => 2 * thin * Math.sin(u * Math.PI), tail, head, GOLD, thin * 0.8);
        p.glow(Math.cos((head + tail) / 2) * R0, Math.sin((head + tail) / 2) * R0, 36, GOLD, 0.34 * thin);
      }
      if (t < 0.12) sparkle(p, Math.cos(head) * 44, Math.sin(head) * 44, 11 * (1 - t / 0.12), WHITE, GOLD);
      ps.draw(p);
    },
  };
}
