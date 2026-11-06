// A taut chain drawn at its real length, so the links stay links instead of being stretched into dots. Runs along +x from 0 to L.
import { fit, clamp } from "../../core/index.js";

export const meta = {
  name: "Long chain",
  desc: "A looping heavy chain, flat and upright links in turn, shivering slightly with tension along its length.",
  tags: ["weapon", "chain", "line", "tether", "loop"],
  size: [172, 28], origin: [14, 14], duration: 0.5, loop: true, fps: 12, bg: "ground",
  options: { palette: ["#14161c", "#2c3038", "#4a4f5a", "#70757f", "#9ea3ac", "#d0d4da", "#f6f7f8"], seed: 2, L: 141 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), L = o.L, f = t / meta.duration, PITCH = 9;
  const c = (i) => C[clamp(Math.round(i), 1, 6)];
  for (let x = 0; x < L; x += PITCH) {
    const n = Math.round(x / PITCH), shake = Math.sin((f + n * 0.13) * Math.PI * 4) * 0.6, flat = n % 2 === 0;
    if (flat) {
      // Seen from the side of the ring: an open oval.
      for (let a = 0; a < 6.28; a += 0.3) {
        const px = x + 4.5 + Math.cos(a) * 5, py = shake + Math.sin(a) * 2.6;
        p.px(px, py, c(py < shake ? 6 : 3));
      }
    } else {
      // Seen edge on: a solid bar with a highlight along the top.
      for (let d = 0; d < 9; d += 0.5) { p.px(x + d, shake - 0.5, c(5)); p.px(x + d, shake + 0.5, c(2)); }
    }
  }
}
