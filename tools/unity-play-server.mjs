import http from 'node:http';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {Battle} from '../server/sim/Battle.js';
import {getDefaultSource,spawnsFromTemplate} from '../server/sim/simdata.js';
import {getData} from '../server/data.js';
import {UnityMatchSession} from './unity-match-session.mjs';
const quiet={warn(){},error(){},info(){},debug(){}};
const ds=getDefaultSource(),data=getData({log:quiet});
const recording=JSON.parse(readFileSync(new URL('../public/dev/recordings/normal-m01.json',import.meta.url)));
const roster=recording.field.units;
const stage=data.stages[recording.stageId];
const tiles=stage.deployTiles.normal;
export class PlaySession {
  constructor(){this.battle=null;this.message='Arrange your seven operators, then Start battle.';}
  start(placements){
    if(!Array.isArray(placements)||placements.length!==7)throw Error('Place all seven operators');
    const occupied=new Set();
    const units=placements.map((p,i)=>{
      if(p.id!==i+1||!Number.isInteger(p.row)||!Number.isInteger(p.col))throw Error('Invalid operator placement');
      const def=ds.rawChess(roster[i].defId),legal=def.position==='MELEE'?tiles.melee:[...tiles.melee,...tiles.rangedOnly];
      if(!legal.some(([r,c])=>r===p.row&&c===p.col))throw Error('This operator cannot use that tile');
      const key=p.row+','+p.col;if(occupied.has(key))throw Error('Two operators cannot share one tile');occupied.add(key);
      return {uid:i+1,kind:'chess',chessId:roster[i].defId,row:p.row,col:p.col,dir:'RIGHT'};
    });
    const scale=data.config.modes.mode_multi_normal.enemyScale['5'];
    const wave=spawnsFromTemplate(ds.getWave(recording.wave),{mods:{hpMul:scale?.hp||1,atkMul:scale?.atk||1,speedMul:scale?.speed||1}});
    this.battle=new Battle({seed:20261012,kind:'normal',modeId:'mode_multi_normal',round:5,stageId:recording.stageId,fieldId:'local',timeLimit:55,routes:wave.routes,spawns:wave.spawns,enemyOverrides:wave.overrides||{},players:[{playerId:'P1',seat:0,side:'L',units}],flags:{layerGainsEnabled:true,dpInit:10,dpPerSec:1,dpMax:99},logger:quiet});
    this.battle.start();
    // Keep passive/auto skills automatic; manual skills wait for the Unity skill button.
    for(const u of this.battle.allyUnits)if(u.skill?.manual)u.skill.rule='NEVER';
    this.message='Battle started';
    return this.view();
  }
  command(input){
    if(input.action==='start')return this.start(input.placements);
    if(!this.battle)throw Error('Start a battle first');
    if(input.action==='skill'){
      const u=this.battle.allyUnits.find(u=>u.uid===input.id);
      const accepted=!!(u?.skill?.manual&&u.canAct&&!u.s.flags.silence&&u.skill.activate('manual'));
      this.message=accepted?'Skill activated':'Skill unavailable: wait for SP and deployment';
    }else if(input.action==='step'){
      for(let i=0;i<3&&!this.battle.finished;i++)this.battle.step();
    }else if(input.action!=='state')throw Error('Unknown command');
    return this.view();
  }
  view(){
    const b=this.battle,s=b.snapshot(),infos=b.fieldMeta().units;
    return {gt:s.t,finished:b.finished,killed:s.killed,total:s.total,message:this.message,result:b.finished?b.result().reason:'',
      infos:infos.map(u=>({id:u.id,assetId:u.spine,enemy:u.kind==='enemy',x:u.x,y:u.y,facing:u.facing,maxHp:u.maxHp})),
      states:s.units.map(u=>({id:u[0],x:u[1],y:u[2],hp:u[3],maxHp:u[4],sp:u[5],spMax:u[6],flags:u[7],anim:u[8]})),
      events:b.drainEvents().map(e=>({kind:e[0],a:e[0]==='spawn'?e[1].id:typeof e[1]==='number'?e[1]:0,b:e[0]==='atk'?e[2]:0,value:['dmg','heal','skill'].includes(e[0])?e[2]:0,style:e[0]==='atk'?e[3]:'',type:e[0]==='dmg'?e[3]:''})),
      skills:b.allyUnits.filter(u=>u.uid).map(u=>({id:u.uid,ready:!!(u.skill?.manual&&u.skill.ready&&u.canAct),sp:u.skill?.sp||0,spMax:u.skill?.spCost||0}))};
  }
}
if(process.argv[1]===fileURLToPath(import.meta.url)){
  const session=new PlaySession();
  let matchSession=null;
  const server=http.createServer(async(req,res)=>{
    res.setHeader('Content-Type','application/json');
    if(req.method!=='POST'||!['/command','/match'].includes(req.url)){res.writeHead(404);res.end('{}');return;}
    try{let body='';for await(const chunk of req){body+=chunk;if(body.length>8192)throw Error('Request too large');}
      const input=JSON.parse(body);
      if(req.url==='/match'){
        if(input.action==='new'){matchSession?.dispose();matchSession=new UnityMatchSession();res.end(JSON.stringify(matchSession.view()));}
        else {matchSession??=new UnityMatchSession();res.end(JSON.stringify(matchSession.command(input)));}
      }else res.end(JSON.stringify(session.command(input)));
    }catch(error){res.writeHead(400);res.end(JSON.stringify({error:error.message}));}
  });
  const port=process.argv.includes('--match')?3142:3141;
  server.listen(port,'127.0.0.1',()=>console.log(`Unity live battle bridge: http://127.0.0.1:${port}`));
}
