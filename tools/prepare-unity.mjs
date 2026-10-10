// Copy locally available real models only; copyrighted assets remain git-ignored.
import {readFile,writeFile,mkdir,copyFile,access} from 'node:fs/promises';
import {resolve,dirname,basename} from 'node:path';
const root=resolve(import.meta.dirname,'..');
const project=resolve(root,'unity-prototype');
const manifest=JSON.parse(await readFile(resolve(root,'data/assets.json'),'utf8'));
const candidates=[...Object.entries(manifest.chars).map(([id,v])=>[id,v.spine?.front]),
  ...Object.entries(manifest.enemies).map(([id,v])=>[id,v.spine])];
const models=[];
for(const [id,entry] of candidates) {
  if(!entry?.skel || !entry.atlas || !entry.anims?.idle) continue;
  const files=[entry.skel,entry.atlas,...entry.textures];
  try {await Promise.all(files.map(f=>access(resolve(root,'public',f.replace(/^\//,'')))));}catch{continue;}
  const folder=resolve(project,'Assets/Resources/Models',id);
  await mkdir(folder,{recursive:true});
  await copyFile(resolve(root,'public',entry.skel.replace(/^\//,'')),resolve(folder,'skeleton.bytes'));
  const atlasText=await readFile(resolve(root,'public',entry.atlas.replace(/^\//,'')),'utf8');
  // Web manifest normalizer adds newer pma headers; the 3.8 parser expects repeat then regions.
  await writeFile(resolve(folder,'atlas.txt'),'\n'+atlasText.replace(/^\s*pma:.*\r?\n/gm,'').trim()+'\n');
  for(const texture of entry.textures)
    await copyFile(resolve(root,'public',texture.replace(/^\//,'')),resolve(folder,basename(texture)));
  models.push({id,path:`Models/${id}`,idle:entry.anims.idle,attack:entry.anims.attack?.loop ?? '',pma:entry.pma});
  if(models.length>=60) break;
}
if(models.length===0) throw new Error('No locally available Spine models; download project assets first.');
await mkdir(resolve(project,'Assets/Resources'),{recursive:true});
await writeFile(resolve(project,'Assets/Resources/models.json'),JSON.stringify({models},null,2));
console.log(`Prepared ${models.length} real Spine models in ${project}`);
