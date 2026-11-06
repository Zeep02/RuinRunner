import { Particles, Rng, P, fit } from "../../core/index.js";

export const meta = {
  name: "Weapon puff",
  desc: "A low puff kicked along the floor: dust, soot, coolant smoke or ash, drifting one way, optionally with a few chips of wood or rising motes.",
  tags: ["weapon", "dust", "smoke", "ground", "one-shot"],
  size: [160, 120], origin: [80, 90], duration: 0.6, loop: false, fps: 24, bg: "ground",
  options: { palette: ["#1c1610", "#33281c", "#4f3f2c", "#6e5a40", "#927a58", "#b89f78", "#dcc8a2"], seed: 21, dir: -1, n: 6, rise: 8, motes: 0, kind: "dust" },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), ps = new Particles({ rng: Rg, ground: () => 6 });
  const kind = o.kind === "smoke" ? P.SMOKE : P.DUST;
  for (let i = 0; i < o.n; i++) ps.spawn(kind, Rg.range(-4, 4), Rg.range(-3, 1), o.dir * Rg.range(12, 58) + Rg.range(-6, 6), -Rg.range(2, o.rise * 2.5), Rg.range(0.3, 0.52), o.kind === "smoke" ? C[2] : C[Rg.int(3, 5)], Rg.range(2.6, 4.8), Rg.range(0, 0.05));
  for (let i = 0; i < o.motes; i++) { const a = Rg.range(-Math.PI, 0), sp = Rg.range(20, 70); ps.spawn(P.MOTE, Rg.range(-5, 5), -2, Math.cos(a) * sp + o.dir * 16, Math.sin(a) * sp, Rg.range(0.25, 0.5), C[Rg.int(3, 6)], 0.5); }
  return { update(dt) { ps.update(dt); }, draw(p) { ps.draw(p); } };
}
