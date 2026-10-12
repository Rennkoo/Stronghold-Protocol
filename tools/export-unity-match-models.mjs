// Export original manifest models. Downloaded art and generated manifests stay untracked.
import {readFile,writeFile,mkdir,copyFile,access} from 'node:fs/promises';
import {resolve,basename,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {spineEntry} from '../public/js/assets.js';
const root=resolve(import.meta.dirname,'..'),out=resolve(root,'unity-prototype/Assets/Resources');
const read=async p=>JSON.parse(await readFile(resolve(root,p),'utf8'));
const assets=await read('data/assets.json'),local=await read('data/local-assets.json');
const models=[],missing=[],copied=new Set();
const requested=new Set((process.argv.find(x=>x.startsWith('--download-ids='))||'').split('=').slice(1).join('=').split(',').filter(Boolean));
const clip=(role,fallback='')=>typeof role==='string'?role:role?.loop||fallback;
async function ensure(url){
  const path=resolve(root,'public',url.replace(/^\//,''));
  if(!path.startsWith(resolve(root,'public')+String.fromCharCode(92))&&!path.startsWith(resolve(root,'public')+'/'))throw Error('Invalid asset path');
  try{await access(path);}catch{
    if(process.argv.includes('--local-only'))throw Error('Not available locally');
    const r=await fetch('http://47.97.28.175:8080'+url,{signal:AbortSignal.timeout(15000)});if(!r.ok)throw Error(`HTTP ${r.status}`);
    await mkdir(dirname(path),{recursive:true});await writeFile(path,Buffer.from(await r.arrayBuffer()));
  }return path;
}
for(const group of ['chars','enemies','tokens'])for(const id of Object.keys(assets[group]||{})){
  const entry=spineEntry(assets,id,{local});if(!entry){missing.push({id,error:'No manifest entry'});continue;}
  const hash='match_'+createHash('sha256').update(entry.skel).digest('hex').slice(0,12),folder=resolve(out,'Models',hash);
  try{
    if(requested.size&&!requested.has(id)){
      for(const url of [entry.skel,entry.atlas,...entry.textures])await access(resolve(root,'public',url.replace(/^\//,'')));
    }
    if(!copied.has(hash)){
      const skel=await ensure(entry.skel),atlas=await ensure(entry.atlas),textures=[];
      for(const texture of entry.textures)textures.push([await ensure(texture),basename(texture)]);
      await mkdir(folder,{recursive:true});await copyFile(skel,resolve(folder,'skeleton.bytes'));
      await writeFile(resolve(folder,'atlas.txt'),'\n'+(await readFile(atlas,'utf8')).replace(/^\s*pma:.*\r?\n/gm,'').trim()+'\n');
      for(const [file,name]of textures)await copyFile(file,resolve(folder,name));copied.add(hash);
    }
    const idle=clip(entry.anims.idle,Object.keys(entry.animations||{})[0]);
    for(const skillIndex of [...new Set([1,...Object.keys(entry.anims.skills||{}).map(Number)])]){
      models.push({assetId:id,skillIndex,id:hash+'_'+skillIndex,path:'Models/'+hash,idle,move:clip(entry.anims.move,idle),attack:clip(entry.anims.attack,idle),skill:clip(entry.anims.skills?.[skillIndex]||entry.anims.skill),die:clip(entry.anims.die),deploy:clip(entry.anims.deploy),pma:entry.pma});
    }
  }catch(error){missing.push({id,error:error.message});}
}
await writeFile(resolve(out,'match-models.json'),JSON.stringify({models},null,2));
await mkdir(resolve(root,'.cache/unity-match'),{recursive:true});
await writeFile(resolve(root,'.cache/unity-match/model-export.json'),JSON.stringify({uniqueModels:copied.size,entries:models.length,missing},null,2));
console.log(JSON.stringify({uniqueModels:copied.size,entries:models.length,missing:missing.length}));
