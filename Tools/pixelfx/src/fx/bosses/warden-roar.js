import { Particles, Lights, P, PF, Rng, clamp, ease, band, TAU, WHITE, hex, mix } from "../../core/index.js";
import { stone } from "../impacts/_lib.js";

export const meta = {
  name: "Warden phase roar",
  desc: "Pebbles lift off the floor and hang as amber light gathers, then the air breaks: three flat shock rings race out, a white crown of light, and every pebble is hurled away.",
  tags: ["boss", "warden", "roar", "phase", "shockwave", "one-shot"],
  size: [360, 210], origin: [180, 140], duration: 2.3, loop: false, fps: 15, bg: "ground", options: { stone: "#6b5a4c", seed: 11 },
};
export const BLAST = 0.8;

export function create(o) {
  const st = stone(o.stone), R = new Rng(o.seed), floor = () => 0, ps = new Particles({ rng: R, ground: floor, sparks: [WHITE, hex("#ffe27a"), hex("#ffb23c"), hex("#c8641f")] }), lights = new Lights();
  const HOT = [WHITE, hex("#ffe27a"), hex("#ffb23c"), hex("#c8641f")], dust = mix(st.dust ?? hex("#8f8693"), WHITE, 0.12);
  const pebbles = Array.from({ length: 34 }, () => { const a = R.range(0, TAU), r = R.range(16, 96); return { x: Math.cos(a) * r, z: Math.sin(a) * r * 0.26, h: R.range(14, 58), at: R.range(0, 0.42), s: R.chance(0.45) ? 3 : 2, c: R.pick([st.light, st.light, st.body]), va: R.range(120, 320), vy: R.range(60, 260) }; });
  let t = 0, blown = false;
  return {
    update(dt) {
      t += dt;
      if (!blown && t >= BLAST) {
        blown = true;
        lights.add(0, -30, 130, HOT[2], 0.7, 0.55); lights.add(0, -30, 54, WHITE, 1, 0.16);
        for (let i = 0; i < 46; i++) { const a = R.range(0, TAU), sp = R.range(160, 480); ps.spawn(P.SPARK, Math.cos(a) * 8, -30 + Math.sin(a) * 8, Math.cos(a) * sp, Math.sin(a) * sp * 0.75, R.range(0.2, 0.55), WHITE); }
        for (let i = 0; i < 20; i++) { const s = R.sign(); ps.spawn(P.SMOKE, s * R.range(4, 30), -R.range(1, 6), s * R.range(40, 150), -R.range(2, 22), R.range(0.9, 1.4), R.chance(0.3) ? dust : st.dust, R.range(4, 8), 0, PF.BIG); }
      }
      ps.update(dt); lights.update(dt);
    },
    drawBack(p) {
      if (blown) for (const [delay, reach] of [[0, 130], [0.09, 100], [0.2, 72]]) {
        const v = (t - BLAST - delay) / 0.5;
        if (v > 0 && v < 1) band(p, 0, 0.5, 8 + reach * ease.outCubic(v), 0.26, Math.max(1, 5 * (1 - v)), 1.15 - v, v < 0.3 ? HOT : [st.body, HOT[3], HOT[2], HOT[1]], (g) => Math.sin(g) < 0, v < 0.15);
      }
      ps.draw(p, "back");
    },
    draw(p) {
      if (blown) for (const [delay, reach] of [[0, 130], [0.09, 100], [0.2, 72]]) {
        const v = (t - BLAST - delay) / 0.5;
        if (v > 0 && v < 1) band(p, 0, 0.5, 8 + reach * ease.outCubic(v), 0.26, Math.max(1, 5 * (1 - v)), 1.15 - v, v < 0.3 ? HOT : [st.body, HOT[3], HOT[2], HOT[1]], (g) => Math.sin(g) > 0, v < 0.15);
      }
      for (const b of pebbles) {
        const lift = ease.outCubic(clamp((t - b.at) / 0.34));
        if (lift <= 0) continue;
        let x = b.x, y = b.z - b.h * lift + Math.sin(t * 7 + b.x) * 1.4;
        if (blown) {
          const d = t - BLAST, dir = Math.sign(b.x) || 1;
          x += dir * b.va * d;
          y += -b.vy * d + 330 * d * d;
          if (y > b.z) continue;
        }
        p.rect(Math.round(x), Math.round(y), b.s, b.s, b.c);
      }
      // gathering: thin streaks fall into the chest, then one white ring of light
      if (t < BLAST) {
        const k = clamp(t / BLAST);
        p.glow(0, -46, 12 + 26 * k, HOT[2], 0.5 * k);
        for (let i = 0; i < 10; i++) {
          const a = (i / 10) * TAU + t * 0.7, r = 70 * (1 - ((k * 2.4 + i * 0.13) % 1));
          p.line(Math.cos(a) * r, -46 + Math.sin(a) * r * 0.7, Math.cos(a) * (r + 8), -46 + Math.sin(a) * (r + 8) * 0.7, HOT[1], 0.7 * k);
        }
      } else if (t < BLAST + 0.07) p.glow(0, -40, 120, WHITE, 1 - (t - BLAST) / 0.07);
      ps.draw(p, "front");
      lights.draw(p);
    },
  };
}
