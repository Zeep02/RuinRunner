import { Lights, fit, clamp, ease, WHITE } from "../../core/index.js";

export const meta = {
  name: "Dawn gate opening",
  desc: "A golden gate frame stands on the floor; two plates of light part from a white slit in its centre, then the frame dissolves upward.",
  tags: ["weapon", "gate", "holy", "ground", "one-shot"],
  size: [90, 120], origin: [45, 100], duration: 0.62, loop: false, fps: 24, bg: "ground",
  options: { palette: ["#2a1c08", "#5a3e10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6"], seed: 23 },
};

export function create(o) {
  const C = fit(o.palette, 7), lights = new Lights(), W = 22, H = 64;
  let t = 0;
  lights.add(0, -H / 2, 40, C[4], 0.5, 0.5);
  return {
    update(dt) { t += dt; lights.update(dt); },
    draw(p) {
      const up = ease.outBack(clamp(t / 0.14)), open = ease.inOutQuad(clamp((t - 0.22) / 0.2)), gone = clamp((t - 0.44) / 0.18), h = H * up, a = 1 - gone;
      if (a <= 0) return;
      for (let y = 0; y <= h; y += 1) { const c = y % 8 < 2 ? C[5] : C[3]; p.dot(-W, -y, c, a); p.dot(-W - 1, -y, C[2], a); p.dot(W, -y, c, a); p.dot(W + 1, -y, C[2], a); }
      for (let x = -W - 2; x <= W + 2; x += 1) { p.dot(x, -h, x % 6 === 0 ? C[6] : C[4], a); p.dot(x, -h - 1, C[2], a); }
      const gap = open * (W - 2);
      for (let s = -1; s <= 1; s += 2) for (let x = gap; x < W - 1; x += 1) for (let y = 2; y < h - 2; y += 1) if ((x + y) % 2 === 0 || x % 5 === 0) p.dot(s * x, -y, x < gap + 3 ? C[6] : C[3], a * 0.9);
      if (up >= 0.9) { const w = open * W * 0.9 + 1; for (let x = -w; x <= w; x += 0.5) for (let y = 1; y < h - 1; y += 1) p.dot(x, -y, Math.abs(x) < w * 0.5 ? WHITE : C[6], a); }
      lights.draw(p);
    },
  };
}
