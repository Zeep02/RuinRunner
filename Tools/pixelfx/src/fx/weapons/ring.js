import { fit, clamp, ease } from "../../core/index.js";

export const meta = {
  name: "Weapon ring",
  desc: "A flat ring on the floor that opens or closes, dotted or solid, in the weapon's colours: orbit arcs, shockwaves and collapsing fields.",
  tags: ["weapon", "ring", "shockwave", "ground", "one-shot"],
  size: [260, 180], origin: [130, 90], duration: 0.45, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#1e0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"], seed: 16, r: 40, squash: 0.5, inward: false, dotted: false, thick: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), k = clamp(t / meta.duration), e = o.inward ? 1 - ease.inQuad(k) : ease.outQuart(k), r = Math.max(1, o.r * e), th = o.thick * (1 - k * 0.7);
  if (k >= 0.98) return;
  for (let g = 0; g < Math.PI * 2; g += 0.7 / Math.max(6, r)) {
    if (o.dotted && Math.floor(g * 8) % 2) continue;
    for (let d = 0; d < th; d += 0.5) p.px(Math.cos(g) * (r - d), Math.sin(g) * (r - d) * o.squash, C[clamp(Math.round(6 - (d / Math.max(0.5, th)) * 2.5 - k * 3.5), 1, 6)]);
  }
  p.glow(0, 0, r * 0.7, C[4], 0.14 * (1 - k));
}
