import { Particles, Lights, P, Rng, fit, clamp, WHITE } from "../../core/index.js";

export const meta = {
  name: "Weapon zap",
  desc: "A vertical lightning strike from above onto the floor: a jagged bolt with forks, a white-hot flash, sparks and a current running low along the ground.",
  tags: ["weapon", "lightning", "strike", "ground", "one-shot"],
  size: [200, 260], origin: [100, 230], duration: 0.42, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#24160a", "#5e3a0c", "#a8680f", "#e8a218", "#ffd23c", "#fff08a", "#ffffff"], sparks: ["#ffffff", "#fff08a", "#ffd23c", "#e8a218"], seed: 18, h: 120, width: 3, spread: 30 },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, ground: () => 4, sparks: o.sparks }), lights = new Lights();
  const path = (x0, y0, x1, y1, n, j) => { const pts = []; for (let i = 0; i <= n; i++) { const v = i / n; pts.push([x0 + (x1 - x0) * v + (i && i < n ? Rg.range(-j, j) : 0), y0 + (y1 - y0) * v]); } return pts; };
  const bolts = [path(Rg.range(-8, 8), -o.h, 0, 0, 9, 7), path(Rg.range(-8, 8), -o.h, 0, 0, 9, 7)];
  const forks = []; for (let i = 0; i < 4; i++) { const b = bolts[0][Rg.int(2, 7)]; forks.push(path(b[0], b[1], b[0] + Rg.range(-18, 18), b[1] + Rg.range(10, 24), 3, 3)); }
  const floor = [path(0, 0, -o.spread, 3, 5, 2), path(0, 0, o.spread, 3, 5, 2)];
  lights.add(0, -8, 34, C[4], 0.65, 0.22); lights.add(0, -3, 12, WHITE, 0.7, 0.06);
  for (let i = 0; i < 12; i++) { const a = Rg.range(-Math.PI * 0.95, -Math.PI * 0.05), sp = Rg.range(70, 210); ps.spawn(P.SPARK, 0, -2, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.08, 0.24)); }
  const stroke = (p, pts, c, w, a = 1) => { for (let i = 1; i < pts.length; i++) { p.line(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], c, a); if (w > 1) p.line(pts[i - 1][0] + 1, pts[i - 1][1], pts[i][0] + 1, pts[i][1], c, a); } };
  let t = 0;
  return {
    update(dt) { t += dt; ps.update(dt); lights.update(dt); },
    draw(p) {
      if (t < 0.17) {
        const b = bolts[Math.floor(t / 0.05) % 2], c = t < 0.05 ? WHITE : t < 0.11 ? C[5] : C[3];
        stroke(p, b, c, o.width); for (const f of forks) stroke(p, f, t < 0.08 ? C[5] : C[3], 1);
      }
      if (t < 0.3) for (const f of floor) stroke(p, f, t < 0.1 ? C[6] : C[3], 1, 1 - clamp((t - 0.14) / 0.16));
      ps.draw(p); lights.draw(p);
    },
  };
}
