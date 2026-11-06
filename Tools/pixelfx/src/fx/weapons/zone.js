// A circular field on the floor whose rim is exactly R pixels from the centre, so a game can show the area an effect really covers.
import { fit, clamp, TAU } from "../../core/index.js";

export const meta = {
  name: "Weapon zone",
  desc: "A looping circle on the floor: a bright rim at a set radius, marching ticks, a pulse running out from the centre and a faint fill.",
  tags: ["weapon", "zone", "field", "ground", "loop"],
  size: [208, 208], origin: [104, 104], duration: 1, loop: true, fps: 12, bg: "ground",
  options: { palette: ["#180a0a", "#3c1412", "#6c1e18", "#a03020", "#d0502c", "#f09050", "#ffe0b0"], R: 83, ticks: 12, seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), R = o.R, f = t / meta.duration;
  p.disc(0, 0, R, C[3], 0.13);
  const steps = Math.ceil(TAU * R * 1.6);
  for (let i = 0; i < steps; i++) { const a = (i / steps) * TAU; p.px(Math.cos(a) * R, Math.sin(a) * R, C[5]); p.px(Math.cos(a) * (R - 1), Math.sin(a) * (R - 1), C[3]); }
  for (let k = 0; k < o.ticks; k++) {
    const a = ((k + f) / o.ticks) * TAU;
    for (let d = 0; d < 7; d++) p.px(Math.cos(a + 0.01 * d) * (R - 3 - d), Math.sin(a + 0.01 * d) * (R - 3 - d), C[clamp(6 - d, 2, 6)]);
  }
  const pulse = f * R, n = Math.ceil(TAU * pulse + 1);
  for (let i = 0; i < n; i++) if (i % 4 < 2) { const a = (i / n) * TAU; p.px(Math.cos(a) * pulse, Math.sin(a) * pulse, C[clamp(Math.round(4 - f * 2), 1, 6)]); }
}
