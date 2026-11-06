import { Particles, Lights, P, PF, Rng, clamp, ease, WHITE, hex, mix } from "../../core/index.js";
import { stone } from "../impacts/_lib.js";

export const meta = {
  name: "Warden stone spike",
  desc: "A crooked limestone shard punches up through the floor in two frames with a burst of grit, holds for a beat with amber light in its root, then cracks and sinks.",
  tags: ["boss", "warden", "spike", "earth", "one-shot"],
  size: [84, 88], origin: [42, 74], duration: 1.05, loop: false, fps: 15, bg: "ground", options: { stone: "#6b5a4c", seed: 8 },
};

export function create(o) {
  const st = stone(o.stone), R = new Rng(o.seed), floor = () => 0, ps = new Particles({ rng: R, ground: floor }), lights = new Lights();
  const AMBER = hex("#ffb23c"), HOT = hex("#ffe27a"), dust = mix(st.dust ?? hex("#8f8693"), WHITE, 0.1);
  const shards = [0, 1, 2].map((i) => ({ x: [-1, -9, 8][i], w: [7, 4, 4.5][i], h: [40, 19, 24][i], lean: [1.5, -5, 6][i], at: [0, 0.035, 0.05][i] }));
  lights.add(0, -8, 26, AMBER, 0.65, 0.26);
  for (let i = 0; i < 20; i++) { const a = -Math.PI / 2 + R.range(-1, 1), sp = R.range(50, 190); ps.spawn(P.GRIT, R.range(-6, 6), -1, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.4, 0.8), R.pick([st.light, st.body, st.shade]), R.chance(0.3) ? 2 : 1); }
  for (let i = 0; i < 6; i++) { const s = R.sign(); ps.spawn(P.SMOKE, s * R.range(1, 8), -2, s * R.range(8, 28), -R.range(4, 18), R.range(0.45, 0.8), R.chance(0.4) ? dust : st.dust, R.range(2.5, 5), 0, PF.BIG); }
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); lights.update(dt); },
    drawBack(p) { ps.draw(p, "back"); },
    draw(p) {
      for (const s of shards) {
        const rise = ease.outBack ? ease.outBack(clamp((t - s.at) / 0.1)) : ease.outCubic(clamp((t - s.at) / 0.1)), sink = ease.inCubic ? ease.inCubic(clamp((t - 0.55) / 0.42)) : clamp((t - 0.55) / 0.42) ** 3, h = s.h * rise * (1 - sink);
        if (h < 1) continue;
        for (let y = 0; y <= h; y++) {
          const v = y / h, half = s.w * (1 - v) + 0.3, cx = s.x + s.lean * v;
          for (let x = -half; x <= half; x += 1) {
            const side = x / Math.max(half, 0.01), broken = sink > 0 && Math.sin((x + s.x) * 9.1 + y * 7.7) > 1 - sink * 1.6;
            if (broken) continue;
            const c = Math.abs(side) > 0.72 && half > 1.2 ? 0x1a1424 : side < -0.15 ? st.light : side < 0.42 ? st.body : st.shade;
            p.px(Math.round(cx + x), -Math.round(y), c);
          }
        }
        // the amber seam at its root while it's hot
        if (t < 0.45) { p.px(Math.round(s.x), -1, HOT); p.px(Math.round(s.x) + 1, -2, AMBER); }
      }
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
