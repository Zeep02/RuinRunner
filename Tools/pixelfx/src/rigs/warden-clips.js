// Every Warden clip: 24 drawings, first and last on REST so any clip can follow any other. The game plays the slices of a
// clip over its own clock: u = 0..3/8 is usually the tell, then the blow, then the recovery. Ease names say how a key arrives.
import { drawWarden, REST } from "./warden.js";

const E = {
  smooth: (u) => u * u * (3 - 2 * u), in: (u) => u * u * u, out: (u) => 1 - (1 - u) ** 3, line: (u) => u,
  back: (u) => { const s = 1.9, v = u - 1; return v * v * ((s + 1) * v + s) + 1; }, snap: (u) => (u >= 1 ? 1 : 0),
};
function at(track, u) {
  if (u <= track[0][0]) return track[0][1];
  for (let i = 0; i < track.length - 1; i++) {
    const [u0, a] = track[i], [u1, b, how] = track[i + 1];
    if (u >= u1) continue;
    const e = (E[how] ?? E.smooth)((u - u0) / (u1 - u0)), out = {};
    for (const k of new Set([...Object.keys(REST), ...Object.keys(a), ...Object.keys(b)])) {
      const A = a[k] ?? REST[k] ?? 0, B = b[k] ?? REST[k] ?? 0;
      out[k] = A + (B - A) * e;
    }
    if (a.turn === undefined && b.turn === undefined) delete out.turn;
    return out;
  }
  return track[track.length - 1][1];
}
export const SIZE = [230, 176], ORIGIN = [100, 150];
const clipMeta = (name, desc, loop) => ({ name: "Warden " + name, desc, tags: ["boss", "warden", loop ? "loop" : "one-shot"], size: SIZE, origin: ORIGIN, duration: 1.6, loop, fps: 15, bg: "ground", options: { seed: 1 } });

// Falling grit and chips shaken off the body, a pure function of time so the clip stays deterministic.
function grit(p, u, u0, x, y, n, span = 0.35, spread = 26) {
  if (u < u0 || u > u0 + span) return;
  const v = (u - u0) / span;
  for (let i = 0; i < n; i++) {
    const a = Math.sin(i * 91.7) * 0.5 + 0.5, b = Math.sin(i * 37.3) * 0.5 + 0.5, dir = i % 2 ? 1 : -1;
    const px = x + dir * a * spread * v * 1.6, py = Math.min(0, y - (40 + b * 50) * v + 200 * v * v);
    p.px(Math.round(px), Math.round(py), i % 3 ? 0x6c5a4d : 0xb79f79);
  }
}

export const idle = {
  meta: clipMeta("idle", "Breathing: shoulders heave, fists hang heavy, the seal crystal pulses.", true),
  draw(p, t) {
    const a = (t / 1.6) * Math.PI * 2, b = Math.sin(a), d = Math.sin(a - 0.9);
    drawWarden(p, { ...REST, crouch: REST.crouch + 1.6 + b * 1.6, lean: REST.lean + b * 0.7, fy: REST.fy + d * 1.8, by: REST.by + d * 2, fx: REST.fx + d * 0.8, bx: REST.bx - d * 0.7 }, { glow: 0.26 + b * 0.09 });
  },
};
export const walk = {
  meta: clipMeta("walk", "A slow stomp: the planted leg takes all the weight, the body rolls over it, fists swing opposite.", true),
  draw(p, t) {
    const a = (t / 1.6) * Math.PI * 2, s = Math.sin(a), c = Math.cos(a), lift = (v) => Math.max(0, v);
    drawWarden(p, { ...REST, lean: 6 + c * 1.2, crouch: 4 - Math.abs(s) * 2.4 + 1.6,
      ffx: 2 + s * 15, ffy: -3 - lift(c) * 8, fbx: -2 - s * 15, fby: -3 - lift(-c) * 8,
      fx: -19 - s * 8, fy: -34 + Math.abs(c) * 2, bx: 30 + s * 8, by: -35 + Math.abs(s) * 2 }, { glow: 0.28 });
  },
};
const TOP = { crouch: 5, lean: -12, fx: -6, fy: -103, fz: 14, bx: 17, by: -103, bz: -12, hy: -3, ffx: 10, fbx: -10 };
const COIL = { crouch: 11, lean: 8, fx: -20, fy: -22, bx: 28, by: -23, hy: 1, ffx: 12, fbx: -11 };
const SMASH = { crouch: 19, lean: 21, fx: 32, fy: -7, fz: 16, bx: 50, by: -7, bz: -14, hy: 3, ffx: 14, fbx: -14 };
export const pound = {
  meta: clipMeta("ground pound", "Coils down, hauls both fists overhead, hangs there, then drops the whole body into the blow. The floor throws grit.", false),
  track: [[0, REST], [0.1, COIL, "smooth"], [0.3, TOP, "out"], [0.375, { ...TOP, fy: -105, by: -105, lean: -13 }, "line"], [0.45, SMASH, "in"], [0.625, { ...SMASH, crouch: 18, lean: 20, fy: -8, by: -8 }, "line"], [0.8, { crouch: 11, lean: 14, fx: 8, fy: -24, bx: 42, by: -24, ffx: 12, fbx: -12 }, "smooth"], [1, REST, "smooth"]],
  draw(p, t) {
    const u = t / 1.6;
    drawWarden(p, at(pound.track, u), { glow: u > 0.1 && u < 0.46 ? 0.3 + u : 0.3, flash: u >= 0.45 && u < 0.5 ? 1 : 0 });
    grit(p, u, 0.45, 42, -4, 18);
  },
};
const WIND = { crouch: 9, lean: -9, turn: 1.15, fx: -48, fy: -60, fz: 6, bx: 33, by: -40, ffx: 13, fbx: -13, hy: -1 };
const JAB = { crouch: 11, lean: 17, turn: 0.05, fx: 63, fy: -48, fz: 14, bx: -7, by: -36, ffx: 21, fbx: -17, hy: 1 };
export const punch = {
  meta: clipMeta("punch", "Winds a giant fist far behind the shoulder, whips the whole torso round and throws it, overshoots and hauls back.", false),
  track: [[0, REST], [0.3, WIND, "smooth"], [0.375, { ...WIND, fx: -50, lean: -10 }, "line"], [0.46, JAB, "in"], [0.625, { ...JAB, fx: 58, lean: 14 }, "out"], [0.8, { crouch: 7, lean: 8, fx: 18, fy: -38, bx: 12, by: -36, ffx: 14, fbx: -13 }, "smooth"], [1, REST, "smooth"]],
  draw(p, t) { const u = t / 1.6; drawWarden(p, at(punch.track, u), { flash: u >= 0.46 && u < 0.5 ? 1 : 0 }); },
};
const BRACE = { crouch: 13, lean: 20, fx: 5, fy: -32, fz: 22, bx: 34, by: -28, ffx: 15, fbx: -18, hy: 2 };
const stride = (s) => ({ crouch: 7, lean: 29, fx: 26 - s * 11, fy: -30, fz: 22, bx: 42 + s * 11, by: -28, ffx: s * 25, ffy: -3 - Math.max(0, s) * 11, fbx: -s * 25, fby: -3 - Math.max(0, -s) * 11, hy: 3 });
export const charge = {
  meta: clipMeta("ram", "Plants, drops the shoulder, then runs at full tilt, far past comfortable, and digs its feet in to stop.", false),
  track: [[0, REST], [0.25, BRACE, "smooth"], [0.3, stride(1), "in"], [0.38, stride(-1), "line"], [0.46, stride(1), "line"], [0.54, stride(-1), "line"], [0.625, stride(0.6), "line"], [0.76, { crouch: 15, lean: -10, fx: 16, fy: -38, bx: 40, by: -38, ffx: 26, fbx: -6, hy: -2 }, "out"], [1, REST, "smooth"]],
  draw(p, t) { const u = t / 1.6; drawWarden(p, at(charge.track, u)); grit(p, u, 0.62, 22, -3, 16, 0.3, 22); },
};
const SQUAT = { crouch: 19, lean: 15, fx: -30, fy: -22, bx: 12, by: -20, ffx: 13, fbx: -13, hy: 2 };
const UP = { crouch: 1, lean: -7, fx: -18, fy: -93, fz: 12, bx: 26, by: -93, ffx: 8, ffy: -11, fbx: -7, fby: -15, hy: -2, y: -4 };
const TUCK = { crouch: 9, lean: 14, fx: 8, fy: -67, fz: 16, bx: 40, by: -67, ffx: 15, ffy: -13, fbx: -5, fby: -9, y: -4 };
const CRUSH = { crouch: 21, lean: 19, fx: 26, fy: -8, bx: 44, by: -8, ffx: 17, fbx: -17, hy: 3 };
export const leap = {
  meta: clipMeta("leap", "Sinks into a deep squat, explodes upward arms first, tucks in the air and lands flat with its whole weight.", false),
  track: [[0, REST], [0.23, SQUAT, "smooth"], [0.3, UP, "out"], [0.48, TUCK, "smooth"], [0.625, { ...TUCK, fx: 16, fy: -36, bx: 36, by: -36 }, "in"], [0.64, CRUSH, "snap"], [0.82, { crouch: 13, lean: 11, fx: 2, fy: -21, bx: 38, by: -21, ffx: 13, fbx: -13 }, "smooth"], [1, REST, "smooth"]],
  draw(p, t) { const u = t / 1.6; drawWarden(p, at(leap.track, u), { flash: u >= 0.64 && u < 0.68 ? 1 : 0 }); grit(p, u, 0.64, 8, -3, 20, 0.3, 40); },
};
export const hit = {
  meta: clipMeta("stagger", "Rocked backward with the crystal flickering, staggers a step, shakes itself and squares up again.", false),
  track: [[0, REST], [0.1, { crouch: 7, lean: -16, fx: -33, fy: -45, bx: 38, by: -47, ffx: 11, fbx: -16, hy: -3 }, "out"], [0.42, { crouch: 12, lean: 13, fx: -13, fy: -18, bx: 32, by: -19, ffx: 14, fbx: -10, hy: 3 }, "smooth"], [0.7, { crouch: 8, lean: 9, fx: -18, fy: -24, bx: 30, by: -27 }, "smooth"], [1, REST, "smooth"]],
  draw(p, t) {
    const u = t / 1.6, flicker = Math.floor(u * 24) % 3 === 0 ? 0.04 : 0.28;
    drawWarden(p, at(hit.track, u), { glow: u < 0.7 ? flicker : 0.28, flash: u < 0.05 ? 1 : 0 });
    grit(p, u, 0.02, 4, -60, 12, 0.3, 24);
  },
};
export const death = {
  meta: clipMeta("death", "Drops to its knees as cracks spread, the crystal gutters out, the body slumps forward and comes apart in chunks.", false),
  track: [[0, REST], [0.2, { crouch: 21, lean: -7, fx: -20, fy: -44, bx: 32, by: -46, hy: -3 }, "out"], [0.48, { crouch: 25, lean: 18, fx: -8, fy: -5, bx: 42, by: -5, ffx: 15, fbx: -15, hy: 4 }, "in"], [1, { crouch: 25, lean: 28, fx: -4, fy: -4, bx: 48, by: -4, ffx: 16, fbx: -16, hy: 4 }, "smooth"]],
  draw(p, t) {
    const u = t / 1.6;
    drawWarden(p, at(death.track, u), { glow: Math.max(0, 0.42 * (1 - u * 2.2)), crumble: Math.max(0, (u - 0.46) / 0.54) });
    grit(p, u, 0.5, 6, -22, 26, 0.5, 58);
  },
};
const KNEEL = { crouch: 25, lean: 26, fx: 0, fy: -5, fz: 21, bx: 30, by: -5, ffx: 13, fbx: -13, hy: 4 };
const ROAR = { crouch: 5, lean: -12, fx: -37, fy: -86, fz: 16, bx: 45, by: -86, ffx: 13, fbx: -13, hy: -4 };
export const entrance = {
  meta: clipMeta("awakening", "A dormant heap: the crystal lights, the head lifts, it heaves upright and throws both fists up in a roar.", false),
  track: [[0, KNEEL], [0.26, { ...KNEEL, lean: 21, hy: 1 }, "smooth"], [0.52, { crouch: 14, lean: 6, fx: -18, fy: -22, bx: 33, by: -24, ffx: 12, fbx: -12 }, "smooth"], [0.66, ROAR, "out"], [0.82, { ...ROAR, fy: -82, by: -82 }, "line"], [1, REST, "smooth"]],
  draw(p, t) {
    const u = t / 1.6;
    drawWarden(p, at(entrance.track, u), { glow: Math.min(0.5, u * 1.1), flash: u >= 0.64 && u < 0.68 ? 1 : 0 });
    grit(p, u, 0.3, 0, -46, 20, 0.45, 40);
  },
};
