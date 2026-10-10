// Integration check against the actual webpage harness, not a second JS implementation.
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import puppeteer from 'puppeteer-core';
const root=resolve(import.meta.dirname,'..');
const exported=JSON.parse(await readFile(resolve(root,'unity-prototype/Assets/Resources/stress.json'),'utf8'));
const models=JSON.parse(await readFile(resolve(root,'unity-prototype/Assets/Resources/models.json'),'utf8')).models;
for(const model of models)for(const key of ['idle','move','attack','skill'])assert.equal(typeof model[key],'string',`model ${model.id} ${key} must be a clip name`);
const fxCode=await readFile(resolve(root,'unity-prototype/Assets/Scripts/StressEffects.cs'),'utf8');
const glyphs=[...fxCode.match(/string\[\] digits=\{([^}]+)\}/)[1].matchAll(/"([01]+)"/g)];
assert.equal(glyphs.length,10);for(const glyph of glyphs)assert.equal(glyph[1].length,35);
const browser=await puppeteer.launch({executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,args:['--no-first-run']});
try {
  const page=await browser.newPage();await page.setViewport({width:1920,height:1080,deviceScaleFactor:1});
  await page.goto((process.env.STRESS_BASE_URL||'http://127.0.0.1:3137')+'/dev/render-demo.html?scene=stress&panel=0&quality=high&board=3d');
  await page.waitForFunction(()=>window.__demo?.ready,{timeout:90000});
  const actual=await page.evaluate(async()=>{
    const d=window.__demo;d.pause();const frames=[],snapshots=[];let units;
    const enter=d.view.enterBattle.bind(d.view),push=d.view.pushEvents.bind(d.view),snap=d.view.pushSnapshot.bind(d.view);
    d.view.enterBattle=field=>{units=field.units;return enter(field);};
    d.view.pushEvents=e=>{frames.push(e);return push(e);};
    d.view.pushSnapshot=s=>{snapshots.push(s);return snap(s);};
    await d.setScene('stress');
    for(let i=0;i<100;i++)d.scene.tick(.05);
    return {units,frames,snapshots};
  });
  assert.equal(actual.units.length,exported.units.length);
  for(let i=0;i<120;i++){
    const a=actual.units[i],b=exported.units[i];
    for(const key of ['id','defId','x','y','maxHp'])assert.equal(a[key],b[key],`unit ${i} ${key}`);
    assert.equal(a.spine,b.assetId,`unit ${i} asset`);
  }
  assert.equal(actual.frames.length,100);
  for(let i=0;i<100;i++){
    const expected=[];
    for(const e of exported.ticks[i].events){
      if(e.kind==='attack'){expected.push(['atk',e.a,e.b,e.style],['dmg',e.b,e.value,e.type]);}
      else if(e.kind==='heal')expected.push(['heal',e.b,e.value]);
      else expected.push(['skill',e.a,e.value]);
    }
    assert.deepEqual(actual.frames[i].ev,expected,`tick ${i} event stream`);
    assert.ok(Math.abs(actual.frames[i].gt-exported.ticks[i].gt)<1e-8);
  }
  for(const g of exported.buckets){assert.equal(g.position.length%3,0);assert.equal(g.index.length%3,0);for(const idx of g.index)assert.ok(idx>=0&&idx<g.position.length/3);for(const n of [...g.position,...g.normal,...g.uv,...g.color])assert.ok(Number.isFinite(n));}
  const report={units:120,models:59,matchedTicks:100,matchedEvents:actual.frames.reduce((n,f)=>n+f.ev.length,0),geometryValid:true};
  if(process.env.VERIFY_REPORT)await writeFile(process.env.VERIFY_REPORT,JSON.stringify(report,null,2));
  console.log(JSON.stringify(report));
}finally{await browser.close();}
