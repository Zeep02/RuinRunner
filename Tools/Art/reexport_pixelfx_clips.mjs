#!/usr/bin/env node
// 설치된 모든 pixelfx 클립을 _pixelfx.json 기록 그대로 다시 내보낸다(내보내기 형식이 바뀌었을 때).
//   node Tools/Art/reexport_pixelfx_clips.mjs [이름 접두어...]
import { spawn } from "node:child_process";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const COMBATFX = join(HERE, "..", "..", "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX");
const prefixes = process.argv.slice(2);
const clips = readdirSync(COMBATFX).filter((name) => existsSync(join(COMBATFX, name, "_pixelfx.json")) && (!prefixes.length || prefixes.some((p) => name.startsWith(p))));
let next = 0, done = 0;
const failures = [];

function run(clip) {
  return new Promise((resolve) => {
    const meta = JSON.parse(readFileSync(join(COMBATFX, clip, "_pixelfx.json"), "utf8"));
    const args = [join(HERE, "pixelfx-to-unity.mjs"), meta.effect, clip, "--opts", meta.optionsJson, "--seed", String(meta.seed), "--fps", String(meta.fps), "--install", "--replace"];
    if (meta.canvas) args.push("--canvas", meta.canvas);
    if (meta.anchor) args.push("--anchor", meta.anchor);
    if (meta.from) args.push("--from", meta.from);
    if (meta.until) args.push("--until", meta.until);
    // Older records have no canvas or centre field; both can be recovered from the recorded pivot and size.
    if (meta.center || (meta.pivotX === .5 && meta.pivotY === .5)) args.push("--center");
    const child = spawn(process.execPath, args, { stdio: ["ignore", "ignore", "pipe"] });
    let errors = "";
    child.stderr.on("data", (text) => { errors += text; });
    child.on("close", (code) => {
      if (code !== 0) failures.push(clip + ": " + errors.split("\n")[0]);
      else {
        const fresh = JSON.parse(readFileSync(join(COMBATFX, clip, "_pixelfx.json"), "utf8"));
        if (fresh.width !== meta.width || fresh.height !== meta.height || Math.abs(fresh.pivotX - meta.pivotX) > .001 || Math.abs(fresh.pivotY - meta.pivotY) > .001)
          failures.push(`${clip}: moved ${meta.width}x${meta.height} -> ${fresh.width}x${fresh.height}`);
      }
      if (++done % 50 === 0) console.log(done + "/" + clips.length);
      resolve();
    });
  });
}
async function worker() { while (next < clips.length) await run(clips[next++]); }
await Promise.all(Array.from({ length: 8 }, worker));
console.log(`done ${done}, problems ${failures.length}`);
for (const f of failures) console.log(" " + f);
