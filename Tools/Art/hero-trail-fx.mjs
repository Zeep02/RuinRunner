#!/usr/bin/env node
// 주인공 기본 무기의 검로 클립 표. 각 행이 CombatFX/FxTrail<무기군><동작> 클립 하나가 된다.
// weapons/hero-slash 의 칼끝 시간표는 MeleeMotionTiming.BladeAngle 과 같아서, 칼날과 베기가 같은 방향, 같은 박자로 돈다.
// R 은 칼끝 바깥의 판정 거리까지 덮는 반지름이고, 게임이 무기별 칼 길이에 맞춰 배율을 준다.
// a0/a1 은 베기가 펼쳐지는 각도다. 기본 270도로 칼날이 도는 범위보다 넓고, 프레임은 게임이 공격 진행도로 넘긴다.
//   node Tools/Art/hero-trail-fx.mjs [클립 이름...]
import { spawnSync } from "node:child_process";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const PAL = {
  steel: ["#141820", "#2a3240", "#4a5468", "#7a8698", "#b4bfcf", "#e4eaf2", "#ffffff"],
  blade: ["#10202e", "#1f4258", "#34708c", "#5aa4c0", "#94d4e4", "#d4f2f6", "#ffffff"],
  gold: ["#201406", "#4a300c", "#7e5414", "#b8821c", "#e4ae34", "#f8d878", "#fff6d0"],
  ember: ["#2a0a10", "#5c1212", "#a4231a", "#e2481c", "#ff8a2a", "#ffd166", "#fff3c4"],
};
const sparksOf = (p) => [PAL[p][6], PAL[p][5], PAL[p][4], PAL[p][3]];
const slash = (p, o) => ["weapons/hero-slash", { palette: PAL[p], ...o }];
const stab = (p, o) => ["weapons/stab", { palette: PAL[p], sparks: sparksOf(p), sparksN: 8, ...o }];

const TABLE = {
  FxTrailDaggerSlash: slash("blade", { R: 46, W: 26, sparksN: 10 }),
  FxTrailDaggerThrust: stab("blade", { LEN: 50, W: 11 }),
  FxTrailLongswordSlash: slash("steel", { R: 64, W: 36, sparksN: 14 }),
  FxTrailLongswordHeavy: slash("gold", { R: 72, W: 46, a0: -155, a1: 155, sparksN: 22 }),
  FxTrailLongswordThrust: stab("steel", { LEN: 72, W: 12 }),
  FxTrailMaceSlash: slash("gold", { R: 56, W: 38, sparksN: 14 }),
  FxTrailMaceCleave: slash("gold", { R: 62, W: 46, a0: -170, a1: 100, sparksN: 20 }),
  FxTrailKatanaSlash: slash("blade", { R: 66, W: 28, sparksN: 14 }),
  FxTrailKatanaCross: slash("blade", { R: 66, W: 24, cross: true, sparksN: 12 }),
  FxTrailSpearThrust: stab("steel", { LEN: 88, W: 12 }),
  FxTrailGreatswordSlash: slash("ember", { R: 88, W: 54, sparksN: 20 }),
  FxTrailGreatswordCleave: slash("ember", { R: 96, W: 66, a0: -170, a1: 100, sparksN: 26 }),
};

const only = process.argv.slice(2);
let failed = 0;
for (const [clip, [effect, options]] of Object.entries(TABLE)) {
  if (only.length && !only.includes(clip)) continue;
  const r = spawnSync(process.execPath, [join(HERE, "pixelfx-to-unity.mjs"), effect, clip, "--opts", JSON.stringify(options), "--seed", "11", "--fps", "60", "--install", "--replace"], { encoding: "utf8" });
  if (r.status !== 0) { failed++; console.log("FAIL " + clip + ": " + (r.stderr || r.stdout).split("\n")[0]); }
  else console.log(r.stdout.split("\n")[0]);
}
console.log("failed", failed);
