import { clamp, fbm, rand } from "../../core/index.js";
import { INK, BODY, EDGE } from "./_lib.js";

export const meta = {
  name: "Ink banner",
  desc: "A dry brush stroke to put words on. 'in' drags it across from the left, trailing bristle gaps; 'out' lets it break up and blow away to the right.",
  tags: ["ink", "ui", "banner", "one-shot"],
  size: [320, 44], origin: [0, 0], duration: 0.34, loop: false, fps: 30, bg: "sky",
  options: { phase: "in", seed: 7 },
};

const W = 320, H = 44, MID = 22, D = meta.duration;

export function create(o) {
  const entering = o.phase !== "out";
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      const u = clamp(t / D), head = entering ? -30 + (W + 70) * (1 - (1 - u) * (1 - u)) : 999;
      const gone = entering ? 0 : u * u;
      for (let x = 0; x < W; x++) {
        // the stroke tapers at both ends and its top and bottom wander
        const ends = clamp(Math.min(x, W - x) / 26);
        const half = (6 + 11 * Math.pow(ends, 0.6)) * (0.92 + 0.14 * fbm(x * 0.04, 3, o.seed, 2));
        const shift = fbm(x * 0.022, 9, o.seed + 2, 2) * 2.4;
        for (let y = 0; y < H; y++) {
          const v = Math.abs(y + 0.5 - MID - shift) - half;
          if (v > 0) continue;
          // bristles: long thin horizontal gaps, thickest near the ends and behind the moving head
          const bristle = fbm(x * 0.05, y * 0.9, o.seed + 5, 2);
          const dry = (1 - ends) * 0.55 + (entering ? clamp((x - head + 46) / 46) * 0.8 : 0) + gone * (0.4 + x / W) * 1.7;
          if (x > head || bristle + 0.1 < dry - 0.55 || rand(x >> 2, y, 3) < dry * 0.16) continue;
          p.put(x, y, v > -1.1 ? EDGE : v > -2.4 ? BODY : INK, 1);
        }
      }
    },
  };
}
