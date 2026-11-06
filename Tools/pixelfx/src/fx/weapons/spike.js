import { Bits, B, Rng, fit, clamp, ease, sparkle } from "../../core/index.js";

export const meta = {
  name: "Weapon spike",
  desc: "A spike bursting from the floor in a material (ice, rock, bone, thorn) with a floor crack, shedding pieces as it sinks.",
  tags: ["weapon", "spike", "pillar", "ground", "one-shot"],
  size: [120, 170], origin: [60, 150], duration: 0.6, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#0c1430", "#1f4f94", "#2f7fcc", "#58b4ec", "#9bdcff", "#d8f4ff", "#ffffff"], seed: 17, h: 34, w: 9, curve: 0, count: 1 },
};

export function create(o) {
  const C = fit(o.palette, 7), Rg = new Rng(o.seed), bits = new Bits({ ground: () => 6 });
  for (let i = 0; i < 6; i++) { const a = Rg.range(-Math.PI * 0.9, -Math.PI * 0.1), sp = Rg.range(40, 120); bits.add({ kind: B.SHARD, x: 0, y: -2, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, a: Rg.range(0, 6), va: Rg.range(-10, 10), s: Rg.range(1, 2.2), c: C[Rg.int(2, 5)], g: 1, life: 0.42 }); }
  const spikes = [];
  for (let i = 0; i < o.count; i++) spikes.push({ x: (i - (o.count - 1) / 2) * o.w * 1.1, h: o.h * (i % 2 ? 0.72 : 1), lean: (i - (o.count - 1) / 2) * 0.12 });
  let t = 0;
  return {
    update(dt) { t += dt; bits.update(dt); },
    drawBack(p) { const k = 1 - clamp(t / 0.55); if (k > 0) for (let x = -o.w * 1.5; x <= o.w * 1.5; x += 1) if (Math.abs(x) % 4 < 3) p.dot(x, 1 + Math.sin(x) * 1.2, C[1], k); },
    draw(p) {
      const up = ease.outBack(clamp(t / 0.11)), down = clamp((t - 0.34) / 0.22), H = up * (1 - ease.inQuad(down));
      if (H > 0.01) for (const s of spikes) {
        const hh = s.h * H;
        for (let y = 0; y < hh; y += 0.5) {
          const v = y / Math.max(1, hh), hw = (o.w / 2) * (1 - v), cx = s.x + s.lean * y + o.curve * v * v;
          for (let w = -hw; w <= hw; w += 0.5) p.px(cx + w, -y, C[clamp(Math.round(3 + (w < 0 ? 2.5 : -1) * (Math.abs(w) / Math.max(0.5, hw)) + v * 1.5 - down * 2), 1, 6)]);
        }
      }
      if (t > 0.03 && t < 0.13) sparkle(p, spikes[0].x, -spikes[0].h * H, 7 * (1 - (t - 0.03) / 0.1), C[6], C[4], 0.4);
      bits.draw(p);
    },
  };
}
