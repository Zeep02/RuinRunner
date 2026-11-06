import { strip } from "./_line.js";
import { clamp } from "../../core/index.js";

export const meta = {
  name: "Weapon line",
  desc: "A one-shot straight strip the game stretches between two points: a cut, a crack, a flame line, a water jet, a lightning arc or a beam.",
  tags: ["weapon", "line", "beam", "one-shot"],
  size: [90, 70], origin: [13, 35], duration: 0.34, loop: false, fps: 30, bg: "ground",
  options: { palette: ["#2a0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"], seed: 19, L: 64, W: 5, style: "slash", taper: true },
};

export function draw(p, t, o) { const k = clamp(t / meta.duration); if (k < 0.97) strip(p, o, k, k); }
