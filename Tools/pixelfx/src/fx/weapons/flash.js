import { Particles, Lights, P, Rng, fit, clamp, WHITE } from "../../core/index.js";

export const meta = {
  name: "Weapon flash",
  desc: "A muzzle or cast flash facing right: a hard star of light and short streaks, single or doubled, tinted to the weapon.",
  tags: ["weapon", "muzzle", "flash", "directional", "one-shot"],
  size: [90, 60], origin: [12, 30], duration: 0.16, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#24160a", "#5e3a0c", "#a8680f", "#e8a218", "#ffd23c", "#fff08a", "#ffffff"], sparks: ["#ffffff", "#fff08a", "#ffd23c", "#e8a218"], seed: 22, len: 14, wide: 5, twin: 0, sparksN: 3 },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, ground: () => 28, sparks: o.sparks }), lights = new Lights();
  lights.add(3, 0, 12 + o.len * 0.5, C[4], 0.5, 0.1);
  for (let i = 0; i < o.sparksN; i++) { const a = Rg.range(-0.6, 0.6), sp = Rg.range(90, 230); ps.spawn(P.SPARK, o.len * 0.5, 0, Math.cos(a) * sp, Math.sin(a) * sp, Rg.range(0.05, 0.11)); }
  let t = 0;
  const cone = (p, oy, k) => {
    const L = o.len * (k < 0.4 ? 0.7 + k : 1.1 - (k - 0.4) * 1.2), W = o.wide * (1 - k * 0.8);
    for (let x = 0; x <= L; x += 0.5) { const hw = W * Math.pow(1 - x / L, 0.7) * (x < 2 ? 0.6 + x * 0.2 : 1); for (let y = -hw; y <= hw; y += 0.5) p.px(x, oy + y, k < 0.25 ? WHITE : C[clamp(Math.round(6 - (Math.abs(y) / Math.max(0.5, hw)) * 2.5 - (x / L) * 2 - k * 2), 1, 6)]); }
    for (let y = -W * 1.1; y <= W * 1.1; y += 0.5) p.px(1, oy + y, C[clamp(Math.round(5 - k * 3), 1, 6)]);
  };
  return {
    update(dt) { t += dt; ps.update(dt); lights.update(dt); },
    draw(p) {
      const k = clamp(t / 0.1);
      if (k < 1) { if (o.twin) { cone(p, -o.twin, k); cone(p, o.twin, k); } else cone(p, 0, k); }
      ps.draw(p); lights.draw(p);
    },
  };
}
