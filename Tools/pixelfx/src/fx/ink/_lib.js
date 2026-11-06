// Shared by the ink sequence: a black liquid that bleeds out over the screen, drains back to a puddle at the
// hero's feet, then rises around them and runs off. The focus is the middle of the screen, where the camera keeps the hero.
import { fbm, hex, rand } from "../../core/index.js";

export const W = 320, H = 180, FX = 160, FY = 90;
export const INK = hex("#08080e"), BODY = hex("#12131b"), EDGE = hex("#2a2f3b"), RIM = hex("#5a6373"), GLINT = hex("#aab3c2");

/** The screen's big lobes and its fine jitter, computed once a seed. */
const cache = new Map();
export function screenNoise(seed) {
  if (cache.has(seed)) return cache.get(seed);
  const big = new Float32Array(W * H), fine = new Float32Array(W * H);
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++) {
      big[y * W + x] = fbm(x * 0.021, y * 0.026, seed, 3);
      fine[y * W + x] = fbm(x * 0.19, y * 0.19, seed + 9, 2);
    }
  const out = { big, fine };
  cache.set(seed, out);
  return out;
}

/** Ink's outside edge at signed distance v (negative is inside): the pale lit rim on the side facing up, spray beyond it. */
export function shade(v, lit, x, y, tick) {
  if (v < -3) return INK;
  if (v < -1) return BODY;
  if (v < 0) return lit ? RIM : EDGE;
  if (v < 8) {
    const k = 1 - v / 8;
    return rand(x, y, tick) < 0.42 * k * k ? INK : 0;
  }
  return 0;
}
