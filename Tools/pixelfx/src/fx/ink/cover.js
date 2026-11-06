import { clamp } from "../../core/index.js";
import { W, H, FX, FY, screenNoise, shade } from "./_lib.js";

export const meta = {
  name: "Ink cover",
  desc: "A blot of black ink bleeds out from the middle of the screen with a torn, spattered edge, slowly and then all at once, until nothing is left uncovered.",
  tags: ["ink", "transition", "screen", "wipe", "one-shot"],
  size: [W, H], origin: [0, 0], duration: 0.9, loop: false, fps: 30, bg: "sky",
  options: { seed: 3 },
};

const COVER = 0.84, START = 0.075, REACH = 275;

export function create(o) {
  const { big, fine } = screenNoise(o.seed);
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const u = clamp(t / COVER), r = REACH * (START + (1 - START) * Math.pow(u, 2.2)), tick = Math.floor(t * 24);
      for (let y = 0; y < H; y++)
        for (let x = 0; x < W; x++) {
          const i = y * W + x, dx = x + 0.5 - FX, dy = y + 0.5 - FY, angle = Math.atan2(dy, dx);
          // the lobes are a share of the radius, so a small blot is still round-ish and a big one is torn
          const v = Math.hypot(dx, dy * 1.1) + big[i] * (10 + r * 0.32) + fine[i] * 3.6 + Math.sin(angle * 5 + t * 9) * 0.8 - r;
          const c = shade(v, dy < -r * 0.15, x, y, tick);
          if (c) p.put(x, y, c, 1);
        }
    },
  };
}
