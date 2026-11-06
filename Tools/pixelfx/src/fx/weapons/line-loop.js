import { strip } from "./_line.js";

export const meta = {
  name: "Weapon line loop",
  desc: "A looping straight strip the game stretches between two points while something holds: a taut thread, rope, chain, frost line or crackling arc.",
  tags: ["weapon", "line", "tether", "loop"],
  size: [90, 50], origin: [13, 25], duration: 0.5, loop: true, fps: 12, bg: "ground",
  options: { palette: ["#050308", "#1c1230", "#432468", "#5e3290", "#8048b8", "#a970e0", "#e0c8ff"], seed: 20, L: 64, W: 3, style: "thread", taper: false },
};

export function draw(p, t, o) { strip(p, o, t / meta.duration, 0.05); }
