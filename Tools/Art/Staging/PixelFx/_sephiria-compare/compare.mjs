import { loadEffect, renderFrames, canvas, upscale } from "../../../../pixelfx/tools/lib/fxlib.mjs";
import { writeFileSync } from "node:fs";
const ids = process.argv.slice(2), white = ["#c8d4d0","#e8f0ec","#ffffff","#ffffff","#ffffff","#ffffff"];
const BG=[76,170,88,255], K=2, N=6, rows=[];
for (const id of ids) for (const mode of ["default","white"]) {
  const fx = await loadEffect(id);
  const opts = mode==="white" ? { palette: white, light: 0 } : {};
  const r = renderFrames(fx, { bg: "none", actor: false, options: opts, seed: 3 });
  const idx = Array.from({length:N},(_,i)=>Math.round(i*(r.n-1)*0.6/(N-1)));
  rows.push({ id, mode, r, idx });
}
const cw = Math.max(...rows.map(o=>o.r.w))*K, ch = Math.max(...rows.map(o=>o.r.h))*K;
const img = canvas(N*(cw+4)+4, rows.length*(ch+18)+4, BG);
rows.forEach((o,j)=>{ const y=4+j*(ch+18); img.text(`${o.id} ${o.mode}`,4,y,2,[20,40,20]);
  o.idx.forEach((fi,i)=>img.paste(upscale(o.r.frames[fi].clear,o.r.w,o.r.h,K),o.r.w*K,o.r.h*K,4+i*(cw+4),y+14)); });
writeFileSync(process.env.OUT, img.png()); console.log(process.env.OUT, rows.length);
