// The layered flame tongue is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
import { fit, mix, WHITE } from "../../core/index.js";
import { noiseFor, hotFirst, flameTongue, smokePuff, looped, drift, floorLight } from "./_lib.js";

export const meta = {
  name: "Skull brazier",
  desc: "A squat iron dish on short legs with a skull in it, burning from the crown with a thin, restless flame that throws off a thread of smoke. Character-sized, for standing about in a room.",
  tags: ["fire", "brazier", "skull", "prop", "light", "ambient", "loop"],
  size: [60, 76],
  origin: [30, 62],
  duration: 4,
  loop: true,
  fps: 30,
  bg: "ground",
  options: {
    palette: ["#14161c", "#262a33", "#3d424e", "#5a606e", "#79808e", "#9aa1ae", "#bcc2cc", "#dcdfe5", "#f6f7f9"],
    bone: ["#17151a", "#4a4640", "#8e8778", "#c9c2ae", "#ece6d4"],
    iron: ["#07070b", "#14151c", "#262833", "#3c404d"],
    seed: 5,
  },
};

const D = meta.duration, NL = noiseFor(D), CROWN = -14;
const CALM = [[1, 1, 5], [0.8, 0.88, 3], [0.6, 0.72, 2], [0.38, 0.52, 1], [0.18, 0.3, 0]];

function dish(p, I, H, fl) {
  // three stubby legs, then the bowl
  p.rect(-7, -3, 2, 3, I[1]);
  p.rect(5, -3, 2, 3, I[1]);
  p.rect(-1, -2, 2, 3, I[0]);
  p.ellipse(0, -5, 10, 3, (d, a) => (d > 0.8 ? I[0] : Math.sin(a) < -0.1 ? mix(I[2], H[4], 0.14 * fl) : I[1]));
  p.rect(-8, -3, 16, 1, I[0]);
}

function skull(p, B, H, fl) {
  const lit = (c) => mix(c, H[2], 0.18 * fl);
  // the cranium, wider than the jaw, and the jaw sitting down in the coals
  p.rect(-3, -13, 7, 1, lit(B[3]));
  p.rect(-4, -12, 9, 3, lit(B[4]));
  p.rect(-4, -9, 9, 2, lit(B[3]));
  p.rect(-3, -7, 7, 2, B[2]);
  p.px(4, -12, B[3]); p.rect(4, -11, 1, 3, B[2]);
  // sockets and the nose, with the fire showing in the sockets
  p.rect(-3, -10, 2, 2, B[0]);
  p.rect(1, -10, 2, 2, B[0]);
  p.px(0, -8, B[1]);
  p.dot(-2, -10, H[3], 0.5 * fl); p.dot(2, -10, H[3], 0.5 * fl);
  for (let x = -2; x <= 2; x += 2) p.px(x, -6, B[1]);
}

export function draw(p, t, o) {
  const F = fit(o.palette, 9), H = hotFirst(F), B = fit(o.bone, 5), I = fit(o.iron, 4), S = o.seed;
  const N = (rate, seed = 0, off = 0) => NL(t, rate * 0.55, seed, off);
  const fl = 0.84 + 0.1 * NL(t, 2, S + 1) + 0.06 * NL(t, 7, S + 2), stretch = Math.max(0, NL(t, 1, S + 3) - 0.1) * 0.7;
  const h = 15 * (1 + stretch), sway = NL(t, 0.75, S + 4) * 2.2 + NL(t, 2, S + 5) * 0.8;
  const GLOW = mix(mix(H[3], H[4], 0.5), WHITE, 0.2), cy = CROWN - h * 0.4;

  floorLight(p, 0, 0, 24, 6, GLOW, 0.22 * fl);
  p.glow(sway * 0.3, cy, 30 * fl, GLOW, 0.26 * fl);
  dish(p, I, H, fl);
  skull(p, B, H, fl);
  // a low flame either side of the main one, licking at different times
  flameTongue(p, -3, CROWN + 1, h * 0.5 * (0.8 + 0.5 * NL(t, 1.5, S + 7)), 2.2, sway * 0.7, N, S * 17, H, CALM, 0.3, 0.7);
  flameTongue(p, 3, CROWN + 1, h * 0.44 * (0.8 + 0.5 * NL(t, 1.25, S + 8)), 2.1, sway * 0.7, N, S * 23, H, CALM, 0.3, 0.7);
  flameTongue(p, 0, CROWN + 1, h, 4, sway, N, S * 31, H, CALM, 0.28, 0.65);
  // the thread of smoke
  looped(t, D, 9, S + 300, (r) => ({ vx: r.range(-1.2, 1.2), vy: -r.range(9, 13), life: r.range(1.4, 2.4), s: r.range(0.7, 1.1) }), (q, age) => {
    const [x, y] = drift(sway, CROWN - h, q.vx, q.vy, age, 1, -6, 1, 0.8);
    smokePuff(p, x + NL(t - age, 0.75, S + 60) * (1 + age * 2), y, 1 - age / q.life, q.s, 0, false, 0.55);
  });
  p.glow(sway * 0.5, CROWN - h * 0.3, 9, H[1], 0.28 * fl);
}
