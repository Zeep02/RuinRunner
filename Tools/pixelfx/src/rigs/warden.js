// THE WARDEN: a chubby stone guardian as a real skeleton in 3D, drawn down to pixels the way rig.js draws the stock
// character. Moss, cracks and rivets live in each bone's own coordinates, so they travel with the body instead of swimming.
// x right, y up inside this file, z towards the viewer; poses are in screen terms like every act (y negative is up).

import { hex } from "../core/color.js";

const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const len = (a) => Math.hypot(a[0], a[1], a[2]);
const unit = (a) => mul(a, 1 / (len(a) || 1e-6));
const rotY = (t) => { const c = Math.cos(t), s = Math.sin(t); return [c, 0, s, 0, 1, 0, -s, 0, c]; };
const rotZ = (t) => { const c = Math.cos(t), s = Math.sin(t); return [c, -s, 0, s, c, 0, 0, 0, 1]; };
const mm = (A, B) => { const o = new Array(9); for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) o[r * 3 + c] = A[r * 3] * B[c] + A[r * 3 + 1] * B[3 + c] + A[r * 3 + 2] * B[6 + c]; return o; };
const mv = (M, v) => [M[0] * v[0] + M[1] * v[1] + M[2] * v[2], M[3] * v[0] + M[4] * v[1] + M[5] * v[2], M[6] * v[0] + M[7] * v[1] + M[8] * v[2]];
const tv = (M, v) => [M[0] * v[0] + M[3] * v[1] + M[6] * v[2], M[1] * v[0] + M[4] * v[1] + M[7] * v[2], M[2] * v[0] + M[5] * v[1] + M[8] * v[2]];

const ramp = (...c) => c.map((v) => hex(v) >>> 0);
const INK = hex("#1a1424") >>> 0, SEAM = hex("#3b3036") >>> 0;
const MAT = {
  stone: ramp("#e0d0a6", "#b79f79", "#84705c", "#574a4b"),
  armor: ramp("#c9b488", "#98805f", "#6c5a4d", "#463b42"),
  moss: ramp("#b9d157", "#7fa83c", "#4f7a32", "#2f5030"),
  joint: ramp("#5d4c4c", "#463a40", "#352c36", "#29222d"),
  horn: ramp("#f3e7c4", "#cdb98c", "#94805f", "#5d4e49"),
  crystal: ramp("#fff6c2", "#ffd24f", "#f39a2c", "#b85a22"),
  crack: ramp("#3b3036", "#3b3036", "#2d2530", "#2d2530"),
  eye: ramp("#fffbe0", "#ffe27a", "#ffc23f", "#ffc23f"),
};
const MATS = Object.keys(MAT);

function vnoise(x, y, z) {
  const h = (i, j, k) => { const s = Math.sin(i * 127.1 + j * 311.7 + k * 74.7) * 43758.5453; return s - Math.floor(s); };
  const fx = Math.floor(x), fy = Math.floor(y), fz = Math.floor(z), u = x - fx, v = y - fy, w = z - fz;
  const a = u * u * (3 - 2 * u), b = v * v * (3 - 2 * v), c = w * w * (3 - 2 * w), L = (p, q, t) => p + (q - p) * t;
  return L(L(L(h(fx, fy, fz), h(fx + 1, fy, fz), a), L(h(fx, fy + 1, fz), h(fx + 1, fy + 1, fz), a), b),
    L(L(h(fx, fy, fz + 1), h(fx + 1, fy, fz + 1), a), L(h(fx, fy + 1, fz + 1), h(fx + 1, fy + 1, fz + 1), a), b), c);
}

// ---- measurements, in pixels: a chibi giant, all shoulders and fists
const M = { hip: 22, spine: 15, chest: 13, shoulderUp: 7, shoulderOut: 23, neck: 17, thigh: 15, shin: 13, hipOut: 11, upper: 18, fore: 21, turn: 0.62 };
/** The stance every clip starts and ends on, so clips join without a jump. */
export const REST = { crouch: 3, lean: 3, fx: -21, fy: -33, fz: 19, bx: 31, by: -34, bz: -14, ffx: 9, ffy: -3, fbx: -9, fby: -3 };

function ik(a, target, L1, L2, pole) {
  let to = sub(target, a), d = len(to);
  const reach = L1 + L2 - 0.02;
  if (d > reach) { to = mul(to, reach / d); d = reach; }
  const u = unit(to), along = (L1 * L1 - L2 * L2 + d * d) / (2 * d), h = Math.sqrt(Math.max(0, L1 * L1 - along * along));
  let v = sub(pole, mul(u, dot(pole, u)));
  v = len(v) < 1e-4 ? unit(cross(u, [0, 0, 1])) : unit(v);
  return { joint: add(a, add(mul(u, along), mul(v, h))), end: add(a, to), u, v };
}
function boneFrame(a, b, pole) {
  const x = unit(sub(b, a));
  let y = sub(pole, mul(x, dot(pole, x)));
  y = len(y) < 1e-4 ? unit(cross(x, [0, 0, 1])) : unit(y);
  const z = cross(x, y);
  return [x[0], y[0], z[0], x[1], y[1], z[1], x[2], y[2], z[2]];
}

function solve(pose) {
  const q = { ...REST, ...pose }, tilt = Math.atan2(q.lean, 22), turn = q.turn ?? M.turn;
  const hipTurn = M.turn + (turn - M.turn) * 0.45;
  const Rp = mm(rotZ(-tilt * 0.3), rotY(-hipTurn)), Rc = mm(rotZ(-tilt), rotY(-turn));
  const ankleN = [q.ffx, -q.ffy, 8], ankleF = [q.fbx, -q.fby, -8];
  let pelvis = [q.lean * 0.25, M.hip - q.crouch, 0];
  const hipAt = (s) => add(pelvis, mv(Rp, [0, -3, s * M.hipOut]));
  const maxLeg = (M.thigh + M.shin) * 0.995;
  for (const [s, a] of [[1, ankleN], [-1, ankleF]]) {
    const h = hipAt(s), dx = h[0] - a[0], dz = h[2] - a[2], flat = dx * dx + dz * dz;
    if (flat < maxLeg * maxLeg) { const top = a[1] + Math.sqrt(maxLeg * maxLeg - flat); if (h[1] > top) pelvis = [pelvis[0], top + 3, pelvis[2]]; }
  }
  const chest = add(pelvis, [Math.sin(tilt) * M.spine, Math.cos(tilt) * M.spine, 0]);
  const neck = add(chest, mv(Rc, [1, M.chest, 0]));
  const headTurn = M.turn * 0.9 + (turn - M.turn) * 0.35, nod = tilt * 0.3 + (q.hy ?? 0) * 0.12;
  const Rh = mm(rotZ(-nod), rotY(-headTurn));
  const head = add(neck, mv(Rh, [2, M.neck, 0]));
  const nearT = [q.fx, -q.fy, q.fz ?? 22], farT = [q.bx, -q.by, q.bz ?? -20];
  const shoulderAt = (s) => add(chest, mv(Rc, [0, M.shoulderUp, s * M.shoulderOut]));
  const sN = shoulderAt(1), sF = shoulderAt(-1);
  const pole = (s, T, sh) => { const up = Math.max(0, Math.min(1, (T[1] - sh[1] + 6) / 22)); return [-0.6 + up * 1.2, -1 + up * 0.9, s * (0.55 + up * 0.5)]; };
  const armN = ik(sN, nearT, M.upper, M.fore, pole(1, nearT, sN)), armF = ik(sF, farT, M.upper, M.fore, pole(-1, farT, sF));
  const kneePole = (s) => mv(Rp, [1, 0, s * 0.4]);
  const hipN = hipAt(1), hipF = hipAt(-1);
  const legN = ik(hipN, ankleN, M.thigh, M.shin, kneePole(1)), legF = ik(hipF, ankleF, M.thigh, M.shin, kneePole(-1));
  return { q, pelvis, chest, head, neck, Rp, Rc, Rh, sN, sF, armN, armF, hipN, hipF, legN, legF };
}

// ---- one depth buffer
const BW = 360, BH = 300, OX = 180, OY = 240;
const Zb = new Float32Array(BW * BH), Tone = new Float32Array(BW * BH), Part = new Int16Array(BW * BH), Mid = new Int8Array(BW * BH);
const touched = [];
const LIGHT = unit([0.22, 0.74, 0.62]);
let air = false;
function plot(wx, wy, z, n, part, mat) {
  const i = (OY - wy) * BW + (OX + wx);
  if (i < 0 || i >= BW * BH) return;
  if (Part[i] === 0) touched.push(i);
  else if (z <= Zb[i]) return;
  Zb[i] = z; Tone[i] = dot(n, LIGHT); Part[i] = part; Mid[i] = mat;
}
/** A solid with radii r on frame R. `paint(local, n)` names the material from the point in the part's own coordinates. */
function solid(c, R, r, part, paint) {
  const big = Math.max(r[0], r[1], r[2]), x0 = Math.floor(c[0] - big), x1 = Math.ceil(c[0] + big), y0 = Math.floor(c[1] - big), y1 = Math.ceil(c[1] + big);
  const D = [1 / (r[0] * r[0]), 1 / (r[1] * r[1]), 1 / (r[2] * r[2])], A = new Array(9);
  for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) A[i * 3 + j] = R[i * 3] * D[0] * R[j * 3] + R[i * 3 + 1] * D[1] * R[j * 3 + 1] + R[i * 3 + 2] * D[2] * R[j * 3 + 2];
  for (let wy = y0; wy <= y1; wy++)
    for (let wx = x0; wx <= x1; wx++) {
      if (!air && wy < 0) continue;
      const dx = wx - c[0], dy = wy - c[1], qa = A[8], qb = 2 * (A[2] * dx + A[5] * dy), qc = A[0] * dx * dx + 2 * A[1] * dx * dy + A[4] * dy * dy - 1, disc = qb * qb - 4 * qa * qc;
      if (disc < 0) continue;
      const dz = (-qb + Math.sqrt(disc)) / (2 * qa), n = unit([A[0] * dx + A[1] * dy + A[2] * dz, A[3] * dx + A[4] * dy + A[5] * dz, A[6] * dx + A[7] * dy + A[8] * dz]);
      plot(wx, wy, c[2] + dz, n, part, paint(tv(R, [dx, dy, dz]), n));
    }
}
const IDENT = [1, 0, 0, 0, 1, 0, 0, 0, 1];
function bone(a, b, ra, rb, part, paint, bulge = 0) {
  const R = len(sub(b, a)) > 0.01 ? boneFrame(a, b, [0, 0, 1]) : IDENT, n = Math.max(2, Math.ceil(len(sub(b, a)) / 1.2));
  for (let k = 0; k <= n; k++) {
    const u = k / n, r = ra + (rb - ra) * u + bulge * Math.sin(u * Math.PI), c = add(a, mul(sub(b, a), u)), along = len(sub(b, a)) * u;
    solid(c, IDENT, [r, r, r], part, (p, nrm) => paint([along + dot(p, [R[0], R[3], R[6]]), dot(p, [R[1], R[4], R[7]]), dot(p, [R[2], R[5], R[8]])], nrm));
  }
}
// weathered stone: moss on what faces up, cracks where the noise pinches
const stone = (seed, mossUp = 0.2, base = "stone") => (p, n) => {
  const k = 0.11, a = vnoise(p[0] * k + seed, p[1] * k, p[2] * k), crack = Math.abs(vnoise(p[0] * 0.09 + seed * 3, p[1] * 0.09, p[2] * 0.09) - 0.5);
  if (crack < 0.007) return MATS.indexOf("crack");
  if (n[1] > 0.55 && a > 0.42 - mossUp * 0.2) return MATS.indexOf("moss");
  if (n[1] > 0.1 && a > 0.72) return MATS.indexOf("moss");
  return MATS.indexOf(base);
};
const flat = (name) => () => MATS.indexOf(name);

const PART = { thighF: 1, shinF: 2, footF: 3, armF1: 4, armF2: 5, fistF: 6, padF: 7, pelvis: 8, waist: 9, chest: 10, core: 11, head: 12, visor: 13, hornA: 14, hornB: 15, thighN: 16, shinN: 17, footN: 18, armN1: 19, armN2: 20, fistN: 21, padN: 22, kneeN: 23, kneeF: 24, plate: 25 };

// ---- A sculpted model: each body group is a signed-distance surface whose parts melt into each other.
const smin = (a, b, k) => { const h = Math.max(k - Math.abs(a - b), 0) / k; return Math.min(a, b) - (h * h * k) / 4; };
const cone = (a, b, ra, rb) => {
  const ba = sub(b, a), l2 = Math.max(dot(ba, ba), 1e-6);
  return (p) => { const pa = sub(p, a), h = Math.max(0, Math.min(1, dot(pa, ba) / l2)); return len(sub(pa, mul(ba, h))) - (ra + (rb - ra) * h); };
};
const ball = (c, r) => (p) => len(sub(p, c)) - r;
const rbox = (c, R, h, r) => (p) => {
  const l = tv(R, sub(p, c)), q = [Math.abs(l[0]) - h[0], Math.abs(l[1]) - h[1], Math.abs(l[2]) - h[2]];
  return Math.hypot(Math.max(q[0], 0), Math.max(q[1], 0), Math.max(q[2], 0)) + Math.min(Math.max(q[0], q[1], q[2]), 0) - r;
};
/** One sculpted group: prims melted with softness k, roughened in the group's own frame so the surface travels with it. */
function sculpt(prims, k, frame, origin, rough, part, paint, reach) {
  const sd = (p) => {
    let d = prims[0](p);
    for (let i = 1; i < prims.length; i++) d = smin(d, prims[i](p), k);
    if (rough > 0) { const l = tv(frame, sub(p, origin)); d += (vnoise(l[0] * 0.17 + 3, l[1] * 0.17, l[2] * 0.17) - 0.5) * rough; }
    return d;
  };
  const x0 = Math.floor(reach[0]), x1 = Math.ceil(reach[1]), y0 = Math.floor(reach[2]), y1 = Math.ceil(reach[3]);
  for (let wy = y0; wy <= y1; wy++)
    for (let wx = x0; wx <= x1; wx++) {
      if (!air && wy < 0) continue;
      let z = 62, hit = false;
      for (let s = 0; s < 44 && z > -62; s++) { const d = sd([wx, wy, z]); if (d < 0.06) { hit = true; break; } z -= Math.max(d * 0.85, 0.06); }
      if (!hit) continue;
      const P = [wx, wy, z], e = 0.5, n = unit([sd([wx + e, wy, z]) - sd([wx - e, wy, z]), sd([wx, wy + e, z]) - sd([wx, wy - e, z]), sd([wx, wy, z + e]) - sd([wx, wy, z - e])]);
      plot(wx, wy, z, n, part, paint(tv(frame, sub(P, origin)), n));
    }
}
const boxOf = (pts, pad) => { let a = 1e9, b = -1e9, c = 1e9, d = -1e9; for (const p of pts) { a = Math.min(a, p[0]); b = Math.max(b, p[0]); c = Math.min(c, p[1]); d = Math.max(d, p[1]); } return [a - pad, b + pad, c - pad, d + pad]; };
const rock = (seed, moss) => (p, n) => {
  const a = vnoise(p[0] * 0.13 + seed, p[1] * 0.13, p[2] * 0.13), crack = Math.abs(vnoise(p[0] * 0.075 + seed * 2, p[1] * 0.075, p[2] * 0.075) - 0.5);
  if (crack < 0.009) return MATS.indexOf("crack");
  if (n[1] > 0.5 - moss && a > 0.38) return MATS.indexOf("moss");
  return MATS.indexOf(a > 0.62 ? "stone" : "armor");
};

function drawBody(S) {
  const { pelvis, chest, head, Rp, Rc, Rh, sN, sF, armN, armF, hipN, hipF, legN, legF } = S;
  // A leg is one stump, thigh into shin into a big rounded foot: no separate parts to look wrong.
  const leg = (L, hip, part, seed) => {
    const foot = add(L.end, [5, -1.5, 0]);
    sculpt([cone(hip, L.joint, 11, 10), cone(L.joint, L.end, 10, 10.5), rbox(foot, rotY(-0.2), [8.5, 2.5, 7], 4)], 6, IDENT, L.end, 1.6, part, rock(seed, 0.05), boxOf([hip, L.joint, L.end, foot], 19));
  };
  leg(legF, hipF, PART.thighF, 11);
  leg(legN, hipN, PART.thighN, 21);
  // An arm tapers from the shoulder and swells into the fist in one line.
  const arm = (A, sh, part, seed) => {
    const dir = unit(sub(A.end, A.joint)), fist = add(A.end, mul(dir, 5)), R = boneFrame(A.joint, A.end, A.v);
    sculpt([cone(sh, A.joint, 9, 7.5), cone(A.joint, A.end, 8, 9.5), rbox(fist, R, [5.5, 6.5, 6.5], 5.5)], 5, R, A.end, 1.4, part, (p, n) => {
      if (p[0] > 9.5 && Math.abs(Math.abs(p[2]) - 4) < 0.7) return MATS.indexOf("crack");
      return rock(seed, 0)(p, n);
    }, boxOf([sh, A.joint, A.end, fist], 19));
  };
  arm(armF, sF, PART.armF1, 31);
  // Torso: hips, belly, barrel chest and shoulders melted into one heavy mass.
  const top = add(chest, mv(Rc, [0, 7, 0]));
  sculpt([rbox(add(pelvis, mv(Rp, [0, -1, 0])), Rp, [8, 4, 11], 6), cone(pelvis, chest, 13, 16), rbox(top, Rc, [9, 8, 13], 8), ball(add(sN, mv(Rc, [0, 1, 0])), 11), ball(add(sF, mv(Rc, [0, 1, 0])), 11)], 8, Rc, chest, 1.8, PART.chest, rock(61, 0.12), boxOf([pelvis, chest, top, sN, sF], 24));
  const core = add(top, mv(Rc, [16, -3, 4]));
  solid(add(core, mv(Rc, [-1, 0, 0])), Rc, [2.5, 8.5, 6.5], PART.plate, flat("joint"));
  solid(core, Rc, [2.6, 6.2, 4.4], PART.core, flat("crystal"));
  arm(armN, sN, PART.armN1, 41);
  // A big rounded head with a heavy brow and huge lit eyes: this is where the charm lives.
  const brow = add(head, mv(Rh, [9.5, 5.5, 0])), snout = add(head, mv(Rh, [5, -5, 0]));
  sculpt([rbox(head, Rh, [9, 8, 10.5], 6), rbox(brow, Rh, [2.5, 1.2, 11], 2), ball(snout, 9)], 4, Rh, head, 0.9, PART.head, (p, n) => {
    if (p[0] > 7) {
      const e = (z) => Math.hypot(p[1] + 0.8, p[2] - z);
      if (e(6.2) < 4.3 || e(-6.2) < 4.3) return MATS.indexOf(e(6.2) < 1.7 || e(-6.2) < 1.7 ? "joint" : "eye");
    }
    return rock(71, 0.05)(p, n);
  }, boxOf([head, brow, snout], 24));
  for (const [s, part] of [[1, PART.hornA], [-1, PART.hornB]]) {
    const a = add(head, mv(Rh, [-1, 9, s * 10])), b = add(a, mv(Rh, [1, 7, s * 5])), tip = add(b, mv(Rh, [5, 8, -s * 1.5]));
    bone(a, b, 4.2, 3, part, flat("horn"));
    bone(b, tip, 3, 0.6, part, flat("horn"));
  }
}

function finish() {
  const toneOf = (v) => (v > 0.76 ? 0 : v > 0.42 ? 1 : v > 0.02 ? 2 : 3), tones = new Map();
  for (const i of touched) tones.set(i, toneOf(Tone[i]));
  for (const i of touched) {
    const t = tones.get(i), votes = [0, 0, 0, 0];
    let same = false;
    for (const d of [-1, 1, -BW, BW]) {
      const j = i + d;
      if (Part[j] !== Part[i] || Mid[j] !== Mid[i] || !tones.has(j)) continue;
      const u = tones.get(j); votes[u]++; if (u === t) same = true;
    }
    if (!same && votes.some((v) => v > 0)) tones.set(i, votes.indexOf(Math.max(...votes)));
  }
  const col = new Map();
  for (const i of touched) col.set(i, MAT[MATS[Mid[i]]][tones.get(i)]);
  for (const i of touched) for (const d of [1, BW]) {
    const j = i + d;
    if (Part[j] === 0 || Part[j] === Part[i]) continue;
    const far = Zb[i] < Zb[j] ? i : j;
    col.set(far, Math.abs(Zb[i] - Zb[j]) > 3 ? INK : SEAM);
  }
  const ring = new Set();
  for (const i of touched) for (const d of [-1, 1, -BW, BW]) if (Part[i + d] === 0) ring.add(i + d);
  const xs = [], ys = [], cs = [];
  for (const [i, c] of col) { xs.push((i % BW) - OX); ys.push(((i / BW) | 0) - OY); cs.push(c); }
  for (const i of ring) { xs.push((i % BW) - OX); ys.push(((i / BW) | 0) - OY); cs.push(INK); }
  for (const i of touched) Part[i] = 0;
  touched.length = 0;
  return { xs, ys, cs };
}

const CACHE = new Map();
/** Pixels of a pose in local space, feet at (0, 0), y down. */
export function wardenPixels(pose) {
  const key = JSON.stringify(pose);
  let r = CACHE.get(key);
  if (!r) {
    const S = solve(pose);
    air = (pose.y ?? 0) < -0.5;
    drawBody(S);
    r = finish();
    const cc = add(S.chest, mv(S.Rc, [0, 7, 0])), core = add(cc, mv(S.Rc, [16, -3, 4]));
    r.core = [core[0], -core[1]];
    r.fists = [[S.armN.end[0], -S.armN.end[1]], [S.armF.end[0], -S.armF.end[1]]];
    CACHE.set(key, r);
  }
  return r;
}
/** Draws the Warden with the crystal's light on top. `x, y` of the pose move the whole figure. */
export function drawWarden(p, pose, { glow = 0.3, flash = 0, crumble = 0 } = {}) {
  const r = wardenPixels(pose), ox = Math.round(pose.x ?? 0), oy = Math.round(pose.y ?? 0);
  const white = hex("#fff4d8") >>> 0;
  for (let i = 0; i < r.xs.length; i++) {
    // A collapsing body sheds pixels from the top down, in chunks that drop to the floor.
    let dy = 0;
    if (crumble > 0) {
      const h = (Math.sin(Math.floor(r.xs[i] / 3) * 12.9 + Math.floor(r.ys[i] / 3) * 78.2) * 43758.5) % 1, w = Math.abs(h);
      const rise = Math.min(1, -r.ys[i] / 100), go = Math.max(0, crumble * 1.7 - (1 - rise) * 0.7 - w * 0.4);
      if (go >= 1) continue;
      dy = go * go * -r.ys[i];
    }
    p.px(r.xs[i] + ox, Math.min(r.ys[i] + oy + Math.round(dy), 2), flash > 0 && r.cs[i] !== INK ? white : r.cs[i]);
  }
  if (glow > 0) { p.glow(r.core[0] + ox, r.core[1] + oy, 13, hex("#ffb23c"), glow); p.glow(r.core[0] + ox, r.core[1] + oy, 4, hex("#fff0b0"), glow); }
  return r;
}
