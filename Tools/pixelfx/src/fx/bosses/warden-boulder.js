import { Particles, Bits, Lights, P, PF, B, Rng, clamp, ease, TAU, WHITE, hex, mix, band } from "../../core/index.js";
import { stone, rock, rockShape, makeCracks, drawCracks, SOOT } from "../impacts/_lib.js";

export const FALL = 0.85; // the floor warning runs this long in the game; the rock lands exactly at the end of it

export const meta = {
  name: "Warden boulder",
  desc: "A boulder drops out of the sky onto its growing shadow: a heavy mossy rock tumbling down, then it bursts on the floor in chunks, grit, dust and a thin amber crack.",
  tags: ["boss", "warden", "rock", "falling", "impact", "one-shot"],
  size: [176, 236], origin: [88, 200], duration: FALL + 1.15, loop: false, fps: 15, bg: "ground",
  options: { stone: "#6b5a4c", seed: 3 },
};

const MOSS = [hex("#b9d157"), hex("#7fa83c"), hex("#4f7a32")];
export function create(o) {
  const st = stone(o.stone), R = new Rng(o.seed), floor = () => 0, D = meta.duration, AMBER = [WHITE, hex("#ffe27a"), hex("#ffb23c"), hex("#c8641f")];
  const ps = new Particles({ rng: R, ground: floor, sparks: [WHITE, AMBER[1], AMBER[2], AMBER[3]] }), bits = new Bits({ ground: floor }), lights = new Lights();
  const cracks = makeCracks(R, 7, { len: [10, 26], dive: 0.5, maxY: 12, forks: 0.5, x0: [2, 6] });
  const dust = mix(st.dust ?? hex("#8f8693"), WHITE, 0.15), shape = rockShape(R);
  let t = 0, landed = false;
  const land = () => {
    landed = true;
    lights.add(0, -8, 46, AMBER[2], 0.5, 0.35); lights.add(0, -3, 22, WHITE, 0.8, 0.09);
    for (let i = 0; i < 9; i++) {
      const a = -Math.PI / 2 + R.range(-1, 1), sp = R.range(90, 240);
      bits.add({ kind: B.CHUNK, x: R.range(-7, 7), y: -3, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, g: 1, drag: 0.4, bounce: 0.35, life: R.range(0.7, 1.05), a: R.range(0, TAU), va: R.range(-16, 16), s: i < 4 ? 5 : R.int(2, 3), pts: rockShape(R) });
    }
    for (let i = 0; i < 34; i++) {
      const u = R.range(-1, 1), a = -Math.PI / 2 + u * 0.8, sp = R.range(70, 230);
      ps.spawn(P.GRIT, u * 6, -1, Math.cos(a) * sp, Math.sin(a) * sp, R.range(0.5, 1), R.pick([st.light, st.body, st.shade, MOSS[1]]), R.chance(0.3) ? 2 : 1);
    }
    for (let i = 0; i < 12; i++) ps.spawn(P.SPARK, R.range(-4, 4), -1, R.range(-150, 150), -R.range(40, 220), R.range(0.12, 0.3), WHITE);
    for (let i = 0; i < 11; i++) { const s = R.sign(); ps.spawn(P.SMOKE, s * R.range(0, 10), -R.range(1, 6), s * R.range(8, 48), -R.range(6, 40), R.range(0.6, 1), R.chance(0.3) ? dust : st.dust, R.range(3.5, 7.5), 0, PF.BIG); }
  };
  const boulder = { kind: B.CHUNK, x: 0, y: 0, a: 0, s: 14, pts: shape };
  return {
    update(dt) { t += dt; if (!landed && t >= FALL) land(); bits.update(dt); ps.update(dt); lights.update(dt); },
    drawBack(p) {
      // the shadow tightens as the rock comes down
      if (t < FALL) {
        const u = clamp(t / FALL), r = 8 + 13 * u * u;
        p.ellipse(0, 0, r, r * 0.3, (d) => hex("#0d0a14"), 0.2 + 0.45 * u);
      }
      if (landed) {
        const k = clamp(1 - (t - FALL) / 0.95), H = clamp(1 - (t - FALL) / 0.45);
        drawCracks(p, cracks, (c) => c.len * ease.outCubic(clamp((t - FALL) / 0.08)), (d) => H * clamp(1.2 - d / 24), AMBER, SOOT, st.light, k);
        const v = (t - FALL) / 0.26;
        if (v < 1) band(p, 0, 0.5, 5 + 30 * ease.outCubic(v), 0.2, Math.max(1, 3 * (1 - v)), 1 - v * 0.6, [st.body, st.light, AMBER[2], AMBER[1]], (g) => Math.sin(g) < 0, v < 0.2);
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (t < FALL) {
        // accelerating from far above; a thin streak says how fast
        const u = clamp((t - 0.42) / (FALL - 0.42));
        if (t > 0.42) {
          const y = -186 + 180 * u * u;
          boulder.y = y; boulder.a = t * 3.4;
          rock(p, boulder, st);
          for (let i = 0; i < 3; i++) p.line(-5 + i * 5, y - 18, -5 + i * 5, y - 18 - 26 * u, mix(st.light, WHITE, 0.3), 0.45 * u);
          for (let m = -6; m <= 6; m++) { p.px(m, y - 11 + Math.abs(m) * 0.5, MOSS[Math.abs(m) % 3]); p.px(m, y - 10 + Math.abs(m) * 0.5, MOSS[(Math.abs(m) + 1) % 3]); }
        }
      }
      for (const b of bits.list) rock(p, b, st);
      ps.draw(p, "front");
      if (landed && t < FALL + 0.05) p.ellipse(0, -1, 18, 5, () => WHITE);
      lights.draw(p);
      void D;
    },
  };
}
