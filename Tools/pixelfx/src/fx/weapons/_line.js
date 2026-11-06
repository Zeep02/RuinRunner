import { fit, clamp, TAU } from "../../core/index.js";
// Straight strips drawn along +x from 0 to L, stretched along their length by the game. Shared by line and line-loop.
export function strip(p, o, phase, k) {
  const C = fit(o.palette, 7), L = o.L, W = o.W, s = o.style;
  const c = (i) => C[clamp(Math.round(i), 1, 6)];
  for (let x = 0; x < L; x += 1) {
    const v = x / L, taper = o.taper ? Math.sin(Math.PI * v) : 1;
    switch (s) {
      case "slash": { const w = W * taper * (1 - k * 0.8); for (let y = -w / 2; y <= w / 2; y += 0.5) p.px(x, y, c(6 - (Math.abs(y) / Math.max(0.5, w / 2)) * 2.5 - k * 3)); break; }
      case "dash": { if (Math.floor(x / 5) % 2 === 0) { p.px(x, 0, c(5 - k * 3)); if (W > 2) p.px(x, 1, c(3 - k * 2)); } break; }
      case "thread": { const y = Math.sin(v * 9 + phase * TAU) * (1 - k) * 0.8; p.px(x, y, c(5 - k * 2)); if (x % 7 === 0) p.px(x, y - 1, c(6)); break; }
      case "rope": { p.px(x, Math.floor(((x + phase * 6) % 4) / 2), c(4)); p.px(x, 1, c(2)); break; }
      case "chain": { const m = Math.floor(x + phase * 6) % 6; if (m < 4) { p.px(x, m === 0 || m === 3 ? 0 : -1, c(5)); p.px(x, m === 0 || m === 3 ? 0 : 1, c(3)); } break; }
      case "crack": { const y = Math.sin(x * 0.7) * 2.2 + Math.sin(x * 0.23) * 3, w = W * taper * (1 - k * 0.5); for (let d = -w / 2; d <= w / 2; d += 0.5) p.px(x, y + d, c(k < 0.6 ? 4 - (Math.abs(d) / Math.max(0.5, w / 2)) * 3 : 2)); break; }
      case "flame": { const h = (W + Math.sin(x * 0.9 + phase * TAU * 2) * W * 0.5 + Math.sin(x * 2.1 - phase * TAU * 3) * W * 0.35) * taper * (1 - k * 0.6); for (let y = 0; y < h; y += 0.5) p.px(x, -y, c(6 - (y / Math.max(0.5, h)) * 4.5 - k * 2)); break; }
      case "water": { const y = Math.sin(v * 12 + phase * TAU) * W * 0.35, w = W * taper * (1 - k * 0.6); for (let d = -w / 2; d <= w / 2; d += 0.5) p.px(x, y + d, c(5 - (Math.abs(d) / Math.max(0.5, w / 2)) * 3 - k * 2)); break; }
      case "frost": { if (x % 6 < 4) { const h = W * (x % 12 < 6 ? 1 : 0.6) * (1 - k * 0.6) * (0.5 + 0.5 * Math.abs(Math.sin(x * 0.9))); for (let y = 0; y < h; y += 0.5) p.px(x, -y + 1, c(6 - (y / Math.max(0.5, h)) * 2 - k * 3)); p.px(x, 2, c(2)); } break; }
      case "zap": { const y = (Math.sin(x * 1.3 + phase * 40) + Math.sin(x * 0.47 - phase * 90)) * W; p.px(x, y, k < 0.3 ? C[6] : c(5 - k * 3)); if (x % 3 === 0) p.px(x, y + 1, c(4 - k * 2)); break; }
      case "beam": { const w = W * (1 - k) * (v > 0.94 ? (1 - v) / 0.06 : 1); for (let y = -w; y <= w; y += 0.5) p.px(x, y, c(6.4 - (Math.abs(y) / Math.max(0.5, w)) * 3.4 - k * 2)); break; }
    }
  }
  if (s === "beam" || s === "zap" || s === "slash") p.glow(L / 2, 0, Math.max(10, W * 3), C[4], 0.2 * (1 - k));
}
