// Export the existing webpage stress workload and board geometry without changing the live game.
import {readFile,writeFile,mkdir,copyFile,access} from 'node:fs/promises';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import {spineEntry} from '../public/js/assets.js';
import {buildBoard,AREAS} from '../public/js/render/board3d/layout.js';
import {resolveUvTable} from '../public/js/render/board3d/atlas.js';
import {presetCamera,threeCameraParams} from '../public/js/render/projection.js';
import {UNIT} from '../public/js/render/style.js';
import puppeteer from 'puppeteer-core';
const root=resolve(import.meta.dirname,'..'), output=resolve(root,'unity-prototype/Assets/Resources');
const json=async p=>JSON.parse(await readFile(resolve(root,p),'utf8'));
const assets=await json('data/assets.json'), chess=Object.values(await json('data/chess.json')).filter(c=>c.visible&&!c.isGolden);
const enemyData=await json('data/enemies.json'), stages=await json('data/stages.json');
let local;
try{local=await json('data/local-assets.json');}catch{
  const response=await fetch('http://47.97.28.175:8080/data/local-assets.json');
  if(!response.ok) throw new Error('Deployment asset manifest unavailable');
  local=await response.json();await writeFile(resolve(root,'data/local-assets.json'),JSON.stringify(local));
}
const enemies=Object.values(enemyData).filter(e=>spineEntry(assets,e.key,{local})&&e.rank!=='BOSS');
const units=[];
for(let i=0;i<40;i++){const c=chess[(i*7)%chess.length];units.push({id:i+1,defId:c.chessId,assetId:c.assets.spine,enemy:false,x:2+i%17,y:9+((i/17)|0)%4,facing:1,maxHp:3000,scale:1,scaleY:1});}
for(let i=0;i<80;i++){const e=enemies[(i*5)%enemies.length];units.push({id:i+41,defId:e.key,assetId:e.spine||e.key,enemy:true,x:20-i%20,y:9+i%4+.3,facing:-1,maxHp:5000,scale:e.modelScale||1,scaleY:e.modelScaleY||1});}
async function ensure(url){const path=resolve(root,'public',url.replace(/^\//,''));try{await access(path);}catch{const response=await fetch('http://47.97.28.175:8080'+url,{signal:AbortSignal.timeout(30000)});if(!response.ok)throw new Error('Missing asset '+url);await mkdir(resolve(path,'..'),{recursive:true});await writeFile(path,Buffer.from(await response.arrayBuffer()));}return path;}
const models=[], indices=new Map();
const clip=(role,fallback='')=>typeof role==='string'?role:role?.loop||fallback;
for(const unit of units){
  const entry=spineEntry(assets,unit.assetId,{local});
  if(!entry)throw new Error('Missing skeleton '+unit.assetId);
  if(!indices.has(entry.skel)){
    const id='stress_'+createHash('sha256').update(entry.skel).digest('hex').slice(0,12);
    const folder=resolve(output,'Models',id);await mkdir(folder,{recursive:true});
    await copyFile(await ensure(entry.skel),resolve(folder,'skeleton.bytes'));
    const atlas=await readFile(await ensure(entry.atlas),'utf8');
    await writeFile(resolve(folder,'atlas.txt'),'\n'+atlas.replace(/^\s*pma:.*\r?\n/gm,'').trim()+'\n');
    for(const texture of entry.textures)await copyFile(await ensure(texture),resolve(folder,basename(texture)));
    indices.set(entry.skel,models.length);
    const idle=clip(entry.anims.idle,Object.keys(entry.animations)[0]);
    models.push({id,path:`Models/${id}`,idle,move:clip(entry.anims.move,idle),attack:clip(entry.anims.attack,idle),skill:clip(entry.anims.skill),pma:entry.pma});
  }
  unit.model=indices.get(entry.skel);
}
// Same JS seeded RNG, consumption order, event rate and movement formula as stressScene.
let seed=7;const rnd=()=>((seed=(seed*1103515245+12345)&0x7fffffff)/0x7fffffff);
const kinds=['arrow','bolt','orb','none','bomb','chain'],ticks=[];
for(let step=1;step<=2400;step++){
  const events=[];
  for(let k=0;k<14;k++){
    const a=units[(rnd()*40)|0],b=units[40+((rnd()*80)|0)],style=kinds[(rnd()*kinds.length)|0],damage=100+((rnd()*900)|0),type=rnd()<.6?'phys':'arts';
    events.push({kind:'attack',a:a.id,b:b.id,style,value:damage,type});
  }
  for(let k=0;k<4;k++){const b=units[(rnd()*40)|0];events.push({kind:'heal',a:b.id,b:b.id,value:150});}
  if(rnd()<.2){const a=units[(rnd()*40)|0];events.push({kind:'skill',a:a.id,b:a.id,value:1});}
  ticks.push({gt:step*.1,events});
}
const stage=stages.act2autochess_m01,tiles=await json('public/assets/local/map/autochess/tiles.json');
const board=buildBoard(stage,{area:AREAS.unite,uv:resolveUvTable(tiles)});
const buckets=Object.entries(board.buckets).filter(([,g])=>g.index.length).map(([name,g])=>({name,position:Array.from(g.position),normal:Array.from(g.normal),uv:Array.from(g.uv),color:Array.from(g.color),index:Array.from(g.index)}));
const camera=threeCameraParams(presetCamera('unite',{width:1920,height:1080},{rect:{r0:9,r1:12,c0:0,c1:20},config:stage.config}),1920,1080);
const sources={D:'TX_autochessi_D',E:'TX_autochessi_E',common:'TX_autochessi_common_D',BG:'TX_autochessi_BG'};
await mkdir(resolve(output,'Board'),{recursive:true});
const browser=await puppeteer.launch({executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,args:['--no-first-run']});
try{
  const page=await browser.newPage();
  for(const [key,name]of Object.entries(sources)){
    const source=local.groups['map/autochess']?.[name]?.path;
    if(!source)throw new Error('Missing board atlas '+name);
    const bytes=await readFile(await ensure(source));
    const encoded=await page.evaluate(async data=>{const img=new Image();img.src=data;await img.decode();const c=document.createElement('canvas');c.width=img.width;c.height=img.height;c.getContext('2d').drawImage(img,0,0);return c.toDataURL('image/png').split(',')[1];},'data:image/webp;base64,'+bytes.toString('base64'));
    await writeFile(resolve(output,'Board',key+'.png'),Buffer.from(encoded,'base64'));
  }
}finally{await browser.close();}
const terrain=board.grid.flat().map(t=>({x:t.c,y:t.r,z:t.h}));
const workload={version:2,stageId:stage.id,modelScale:UNIT.modelScale,units,ticks,camera,buckets,terrain,boardStats:board.stats};
await writeFile(resolve(output,'models.json'),JSON.stringify({models},null,2));
await writeFile(resolve(output,'stress.json'),JSON.stringify(workload));
console.log(JSON.stringify({units:units.length,models:models.length,ticks:ticks.length,board:board.stats,camera},null,2));
