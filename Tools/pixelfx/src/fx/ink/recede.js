import { clamp, fbm, lerp } from "../../core/index.js";
import { W, H, FX, FY, screenNoise, shade } from "./_lib.js";

export const meta = {
  name: "Ink recede",
  desc: "The screen of ink drains: a wavering front runs down from the top while the mass pulls in, ending as a flat puddle in the middle of the screen.",
  tags: ["ink", "transition", "screen", "wipe", "one-shot"],
  size: [W, H], origin: [0, 0], duration: 0.55, loop: false, fps: 30, bg: "sky",
  options: { seed: 3 },
};

const D = meta.duration, PUDDLE_RX = 14, PUDDLE_RY = 5;

export function create(o) {
  const { big, fine } = screenNoise(o.seed);
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const u = clamp(t / D), pull = Math.pow(u, 2.1);
      const front = lerp(-46, FY - PUDDLE_RY - 2, Math.pow(u, 1.5));
      const rx = lerp(420, PUDDLE_RX, pull), ry = lerp(300, PUDDLE_RY, pull), tick = Math.floor(t * 24);
      for (let x = 0; x < W; x++) {
        const wave = Math.sin(x * 0.033 + t * 5) * 6 * (1 - pull) + fbm(x * 0.018, t * 1.6, o.seed + 4, 2) * 20 * (1 - pull);
        for (let y = 0; y < H; y++) {
          const i = y * W + x, dx = x + 0.5 - FX, dy = y + 0.5 - FY;
          // two walls close in on the puddle: the front from above, the mass from every side
          const fromFront = front + wave - y;
          const scale = Math.min(rx, ry * 2.4);
          const fromMass = (Math.hypot(dx / rx, dy / ry) - 1) * scale + big[i] * 22 * (1 - pull) + fine[i] * (1.2 + 2 * (1 - pull));
          const v = Math.max(fromFront, fromMass);
          const c = shade(v, fromFront >= fromMass || dy < 0, x, y, tick);
          if (c) p.put(x, y, c, 1);
        }
      }
    },
  };
}
