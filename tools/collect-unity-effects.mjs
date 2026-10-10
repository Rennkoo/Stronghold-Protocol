import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const root=resolve(import.meta.dirname,'..');
const read=async p=>JSON.parse(await readFile(resolve(root,p),'utf8'));
const labels=['fx-old-a','fx-new-a','fx-new-b','fx-old-b'];
const overall=[];
for(const label of labels){
  const result=await read(`.cache/unity-measurements/${label}-summary.json`);
  assert.equal(result.units,120);assert.equal(result.uniqueModels,59);assert.equal(result.samples,600);assert.equal(result.effectsDropped,0);
  overall.push({variant:label.includes('-old-')?'old':'new',label,...result});
}
const cpu=await read('.cache/unity-effects-cpu/result.json');
assert.equal(cpu.identicalGeometry,true);
const average=variant=>{const rows=cpu.trials.filter(t=>t.variant===variant);return rows.reduce((s,r)=>s+r.msPerFrame,0)/rows.length;};
const oldMs=average('old'),newMs=average('new');
const report={baselineCommit:'3d19038a16e033d222692acccfac0b2c238f2e0e',order:'old-new-new-old',overall,cpu,meanOldEffectsMs:oldMs,meanNewEffectsMs:newMs,effectsCpuReductionPercent:100*(1-newMs/oldMs),interpretation:'CPU fixture is Editor-only. Whole-game FPS improvement is not established by these short samples.'};
const path=resolve(root,process.argv[2]||'.cache/unity-effects-report.json');
await writeFile(path,JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({path,oldMs,newMs,effectsCpuReductionPercent:report.effectsCpuReductionPercent},null,2));
