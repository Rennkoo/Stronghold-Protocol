import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
const root=resolve(import.meta.dirname,'..');
const read=async path=>JSON.parse(await readFile(path,'utf8'));
const source=await read(resolve(root,'public/dev/recordings/normal-m01.json'));
const exported=await read(resolve(root,'unity-prototype/Assets/Resources/replay-workload.json'));
const units=new Map(source.field.units.map(u=>[u.id,u]));
const counts={};
for(const f of source.frames)for(const e of f.ev){counts[e[0]]=(counts[e[0]]||0)+1;if(e[0]==='spawn')units.set(e[1].id,e[1]);}
assert.equal(exported.units.length,units.size);
assert.deepEqual(exported.initialIds,source.field.units.map(u=>u.id));
assert.equal(exported.replayFrames.length,source.frames.length);
for(let i=0;i<source.frames.length;i++){
  const original=source.frames[i],converted=exported.replayFrames[i];
  assert.equal(converted.gt,original.snap.t);
  assert.deepEqual(converted.states.map(s=>[s.id,s.x,s.y,s.hp,s.maxHp,s.sp,s.spMax,s.flags,s.anim]),original.snap.units);
  assert.equal(converted.events.length,original.ev.length);
  for(let j=0;j<original.ev.length;j++){
    const a=original.ev[j],b=converted.events[j];assert.equal(b.kind,a[0]);
    if(a[0]==='spawn')assert.equal(b.a,a[1].id);
    if(['atk','deploy','dmg','heal','die','skill','status'].includes(a[0]))assert.equal(b.a,a[1]);
    if(a[0]==='atk'){assert.equal(b.b,a[2]);assert.equal(b.style,a[3]);}
    if(['dmg','heal','skill'].includes(a[0]))assert.equal(b.value,a[2]);
  }
}
for(const u of exported.units){const s=units.get(u.id);assert.equal(u.assetId,s.spine);assert.equal(u.enemy,s.kind==='enemy');assert.equal(u.maxHp,s.maxHp);assert.equal(u.skillIndex,s.skillIndex||1);}
if(process.argv[2]){
  const result=await read(resolve(process.argv[2]));
  assert.equal(result.failures,0);assert.equal(result.units,units.size);assert.equal(result.snapshots,source.frames.length);
  assert.deepEqual(Object.fromEntries(result.events.map(e=>[e.kind,e.count])),counts);
  const final=source.frames.at(-1).snap.units;
  assert.deepEqual([...result.visibleIds].sort((a,b)=>a-b),final.map(s=>s[0]).sort((a,b)=>a-b));
  for(const s of final){
    const i=exported.units.findIndex(u=>u.id===s[0]);assert.equal(result.hp[i],s[3]);
    assert.ok(Math.abs(result.positions[i*3]-s[1])<.001,'Final X differs');
    assert.ok(Math.abs(result.positions[i*3+2]-s[2])<.001,'Final board Y differs');
  }
}
console.log(JSON.stringify({verified:true,native:!!process.argv[2],frames:source.frames.length,units:units.size,events:counts},null,2));
