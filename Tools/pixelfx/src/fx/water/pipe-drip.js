import { fit, WHITE } from "../../core/index.js";

export const meta = {
  name: "Pipe drip",
  desc: "Water from a pipe that is already drawn: a bead swells at the mouth, drops, and plinks on the floor in two flung dots and a small flat ring.",
  tags: ["water", "drip", "prop", "ambient", "loop"],
  size: [40, 40],
  origin: [20, 32],
  duration: 1.8,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#0e4a58", "#1c8ebc", "#88e8e0", "#e0fff8"], height: 15 },
};

const SWELL = 0.8, GRAVITY = 300, SPLASH = 0.45;

export function draw(p, t, o) {
  const C = fit(o.palette, 4), H = o.height;
  const fall = Math.sqrt((2 * H) / GRAVITY);
  if (t < SWELL) {
    const k = t / SWELL;
    p.dot(0, -H, k > 0.6 ? C[2] : C[1], Math.min(1, k * 2));
    if (k > 0.7) p.dot(0, -H + 1, C[3], (k - 0.7) / 0.3);
    return;
  }
  if (t < SWELL + fall) {
    const u = t - SWELL, y = -H + 1 + 0.5 * GRAVITY * u * u;
    p.dot(0, y, WHITE, 1);
    p.dot(0, y - 1, C[2], 0.9);
    return;
  }
  const u = (t - SWELL - fall) / SPLASH;
  if (u >= 1) return;
  const spread = 1 + u * 5, a = 1 - u;
  // Flat on the floor, seen from above: wide and low.
  for (const side of [-1, 1]) {
    p.dot(side * spread, 0, C[2], a);
    p.dot(side * spread * 0.6, Math.round(spread * 0.35), C[1], a * 0.8);
    p.dot(side * spread * 0.6, -Math.round(spread * 0.35), C[1], a * 0.8);
    p.dot(side * (1 + u * 2), -3 * Math.sin(u * Math.PI), C[3], a);
  }
}
