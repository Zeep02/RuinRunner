// The hero's melee slash: a broad crescent of about 270 degrees on the blade tip's timetable (MeleeMotionTiming.BladeAngle). It turns
// the way the weapon turns and meets the blade at contact, but opens wider than the blade travels. Built on the melee slash recipe
// in ../melee/_lib.js. Frames are stepped by swing progress.
import { Particles, Lights, P, Rng, fit, sparkle, flare, clamp, smooth, WHITE } from "../../core/index.js";
import { swing, smear, glowAlong, trace, onSwing } from "../melee/_lib.js";

export const meta = {
  name: "Hero slash",
  desc: "A broad crescent that follows the blade tip through the swing: white at contact, an echo inside it, a lit rim, sparks and shavings off the far end, a dotted trace left behind.",
  tags: ["weapon", "melee", "slash", "arc", "directional", "one-shot"],
  size: [320, 320],
  origin: [160, 160],
  duration: 0.56,
  loop: false,
  fps: 60,
  bg: "ground",
  options: { palette: ["#141820", "#2a3240", "#4a5468", "#7a8698", "#b4bfcf", "#e4eaf2", "#ffffff"], seed: 11, R: 52, W: 30, a0: -135, a1: 135, serrate: 0, gaps: 0, cross: false, sparksN: 14 },
};

// Seconds the swing takes at normal attack speed, and its progress marks. The game steps frames by progress over these seconds.
const SWING = 0.246, P0 = 0.18, CONTACT = 0.3, STOP = 0.58, HOLD = 0.64, END = 1.18, RAD = Math.PI / 180;
const progressAt = (t) => P0 + (1 - P0) * (t / SWING);
const tipAngle = (x, a0, a1) => (x < CONTACT ? a0 * (1 - smooth(P0, CONTACT, x)) : a1 * smooth(CONTACT, STOP, x));

function stroke(p, ramp, q, R, W, a0, a1, o, power) {
  // The blade crosses its whole arc in a blink, so the crescent holds that arc open and only then collapses toward where the blade stopped.
  const head = tipAngle(q, a0, a1), tail = a0 + (a1 - a0) * smooth(HOLD, END, q);
  if (q <= P0 || head - tail < 0.02) return;
  const s = swing({ R, sy: 1, a0, dir: 1 }), U = head - a0, T = tail - a0, n = ramp.length;
  const age = clamp((q - P0) / (END - P0)), fade = 1 - clamp((q - HOLD) / (END - HOLD)) * 0.5;
  const hot = q < CONTACT + 0.06 ? power : 0;
  smear(p, s, T, U, W, (u, r, out) => {
    const v = (u - T) / (U - T);
    if (o.gaps && Math.floor((u * R) / o.gaps) % 2 === 1) return;
    // Thin while the arc is short, opening and closing, so it never reads as a pie slice or a stray chip.
    let w = W * fade * clamp((U - T) / 1.6) * Math.pow(Math.sin(Math.PI * Math.pow(v, 1.5)), 0.5);
    if (o.serrate) w *= 0.62 + 0.38 * (Math.floor((u * R) / o.serrate) % 2 ? 1 : 0.15);
    if (w < 0.35) return;
    const d = (R - r) / w;
    if (d < 0 || d > 1) return;
    const ci = d < hot * 0.7 || (d < 0.22 && age < 0.6) ? 0 : Math.round(d * (n - 1) * 0.7 + age * 1.8 - v * 0.8 + 0.2 - hot * 3);
    out.c = ramp[n - 1 - clamp(ci, 0, n - 1)];
    out.a = Math.min(1, v / 0.18);
  });
  glowAlong(p, s, R - W * 0.4, T, U, 6 + W * 0.9, ramp[n - 3], (u) => (0.3 + hot * 0.3) * fade * ((u - T) / (U - T)), 0, 0.2);
  if (T > 0.02) trace(p, s, R - 0.5, 0, T, ramp[2], (u) => 0.55 * fade * (0.4 + (0.6 * u) / U));
}

export function create(o) {
  const C = fit(o.palette, 7), ramp = C.slice(1), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, sparks: [C[6], C[5], C[4], C[3]] }), lights = new Lights();
  const a0 = o.a0 * RAD, a1 = o.a1 * RAD;
  const cuts = o.cross ? [[-35 * RAD, -0.04], [35 * RAD, 0.14]] : [[0, 0]];
  const pending = cuts.map(() => true);
  let t = 0;
  return {
    update(dt) {
      t += dt;
      cuts.forEach(([turn, late], i) => {
        if (!pending[i] || progressAt(t) - late < CONTACT) return;
        pending[i] = false;
        // Thrown off the tip where the blade is at contact, forward along its travel.
        const x = Math.cos(turn) * o.R, y = Math.sin(turn) * o.R, along = turn + Math.PI / 2;
        lights.add(x, y, o.W * 2, C[4], 0.55, 0.16);
        lights.add(x, y, o.W * 0.8, WHITE, 0.6, 0.06);
        for (let n = 0; n < o.sparksN; n++) {
          const a = along + Rg.range(-0.7, 0.7), sp = Rg.range(120, 320);
          ps.spawn(P.SPARK, x, y, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.1, 0.26));
        }
        for (let n = 0; n < o.sparksN * 0.6; n++) {
          const [sx, sy] = onSwing(swing({ R: o.R, sy: 1, a0: a0 + turn, dir: 1 }), o.R, (a1 - a0) * Rg.range(0.25, 0.8)), l = Math.hypot(sx, sy) || 1, sp = Rg.range(40, 110);
          ps.spawn(P.MOTE, sx, sy, (sx / l) * sp, (sy / l) * sp, Rg.range(0.16, 0.34), Rg.pick([C[4], C[5]]), 0.6);
        }
      });
      ps.update(dt);
      lights.update(dt);
    },
    draw(p) {
      const q = Math.min(progressAt(t), 1.3);
      cuts.forEach(([turn, late]) => {
        const x = q - late, A0 = a0 + turn, A1 = a1 + turn;
        stroke(p, ramp, x - 0.05, o.R - o.W * 0.85, o.W * 0.42, A0, A1, o, 0);
        stroke(p, ramp, x, o.R, o.W, A0, A1, o, 1);
        if (x > P0 && x < STOP + 0.2) {
          const a = tipAngle(x, A0, A1), g = 1 - clamp((x - STOP) / 0.2);
          sparkle(p, Math.cos(a) * (o.R - o.W * 0.3), Math.sin(a) * (o.R - o.W * 0.3), o.W * 0.3 * g, C[5], WHITE, 0.5);
        }
        const f = (x - CONTACT) / 0.16;
        if (f >= 0 && f < 1) flare(p, Math.cos(turn) * o.R * 0.92, Math.sin(turn) * o.R * 0.92, o.W * 0.7 * Math.pow(1 - f, 1.4), 1 - f, C);
      });
      ps.draw(p);
      lights.draw(p);
    },
  };
}
