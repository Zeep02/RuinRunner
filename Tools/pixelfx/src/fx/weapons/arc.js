import { Particles, Lights, P, Rng, fit, clamp, smooth, ease } from "../../core/index.js";

export const meta = {
  name: "Blade trail arc",
  desc: "A melee weapon tip's trail stepped by swing progress: one stroke round the hand, optionally hooked, serrated, dotted or crossed, burning out with flecks.",
  tags: ["weapon", "melee", "slash", "arc", "directional", "one-shot"],
  size: [240, 240], origin: [120, 120], duration: 0.246, loop: false, fps: 45, bg: "ground",
  options: { palette: ["#2a0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"], sparks: ["#fff3c4", "#ffd166", "#ff8a2a", "#c42a1a"], seed: 11,
    R: 31, W: 8, a0: -92, a1: 68, over: 8, curl: 0, serrate: 0, gaps: 0, cross: false, sparksN: 5, glow: 0 },
};

const P0 = 0.18, CONTACT = 0.3, STOP = 0.58, RAD = Math.PI / 180;
const progressAt = (t) => P0 + (1 - P0) * clamp(t / meta.duration);

function stroke(p, C, q, o, a0, a1, tOffset) {
  const A2 = a1 + o.over * RAD;
  const tip = (x) => {
    if (x < CONTACT) return a0 * (1 - smooth(P0, CONTACT, x));
    if (x < STOP) return a1 * smooth(CONTACT, STOP, x);
    return a1 + (A2 - a1) * ease.outQuad(clamp((x - STOP) / 0.14));
  };
  const radius = (a) => o.R - o.curl * Math.pow(clamp((a - a1 * 0.35) / (A2 - a1 * 0.35)), 1.7);
  const qq = q - tOffset, head = tip(qq), tail = tip(Math.max(P0, qq - 0.19));
  if (qq < P0 || head - tail < 0.03) return;
  const heat = 1 - clamp((qq - CONTACT) / 0.55);
  for (let a = tail; a <= head; a += 0.8 / Math.max(14, o.R)) {
    const v = (a - tail) / (head - tail), rc = radius(a);
    if (o.gaps && Math.floor(((a - a0) * o.R) / o.gaps) % 2 === 1) continue;
    // a broad band thins right out as it dies, so its tail never hangs detached from the blade
    let w = o.W * Math.pow(Math.sin(Math.PI * Math.pow(v, 1.5)), 0.8) * (o.W > 20 ? Math.pow(heat, 1.4) * clamp((head - tail) / 1.1) : 0.55 + heat * 0.45);
    if (o.serrate) w *= 0.62 + 0.38 * (Math.floor(((a - a0) * o.R) / o.serrate) % 2 ? 1 : 0.15);
    for (let d = 0; d <= w; d += 0.5) {
      const f = d / Math.max(0.5, w), ci = 6 - Math.round(f * 3.4 + (1 - heat) * 2 + (1 - v) * 1.4);
      p.px(Math.cos(a) * (rc - d), Math.sin(a) * (rc - d), C[clamp(ci, 1, 6)]);
    }
  }
  if (o.glow) {
    const n = Math.ceil(((head - tail) * o.R) / 10);
    for (let i = 0; i <= n; i++) {
      const v = i / n, a = tail + (head - tail) * v, r = radius(a) - o.W * 0.3;
      p.glow(Math.cos(a) * r, Math.sin(a) * r, 5 + o.W * 0.8, C[4], o.glow * heat * v);
    }
  }
}

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, ground: () => 90, sparks: o.sparks }), lights = new Lights();
  let t = 0, struck = false;
  const a0 = o.a0 * RAD, a1 = o.a1 * RAD, sx = o.R;
  return {
    update(dt) {
      t += dt;
      if (!struck && progressAt(t) >= CONTACT) {
        struck = true;
        lights.add(sx, 0, o.R * 0.7, C[4], 0.35, 0.1);
        for (let i = 0; i < o.sparksN; i++) {
          const a = Rg.range(-0.8, 0.8), sp = Rg.range(60, 150);
          ps.spawn(P.SPARK, sx, 0, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.05, 0.11));
        }
      }
      ps.update(dt); lights.update(dt);
    },
    draw(p) {
      const q = progressAt(t);
      if (o.cross) {
        stroke(p, C, q, o, (o.a0 - 35) * RAD, (o.a1 - 35) * RAD, -0.04);
        stroke(p, C, q, o, (o.a0 + 35) * RAD, (o.a1 + 35) * RAD, 0.14);
      } else stroke(p, C, q, o, a0, a1, 0);
      ps.draw(p); lights.draw(p);
    },
  };
}
