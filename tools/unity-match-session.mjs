// Unity adapter: keep economy, drafts, placement and round transitions in the original engine.
import {Match} from '../server/match/Match.js';
import {VirtualScheduler} from '../server/match/scheduler.js';
import {getData} from '../server/data.js';
import {validateC2S} from '../shared/protocol.js';
const log={info(){},warn(){},debug(){},error(){}};
export class UnityMatchSession {
  constructor({seed=20261012}={}){
    this.messages=[];
    this.scheduler=new VirtualScheduler({instantCombat:false});
    const emit=msg=>{this.messages.push(msg);return true;};
    this.match=new Match({roomCode:'UNITY',mode:'solo',difficulty:'NORMAL',seed,
      seats:[{seat:0,playerId:'unity',name:'指挥官',isBot:false,connected:true}],
      data:getData({log}),log,scheduler:this.scheduler,clientCombat:false,
      send:(_,msg)=>emit(msg),broadcast:emit,onEnd:summary=>{this.summary=summary;}});
    this.match.start();
  }
  command(input){
    let reply={ok:true};
    if(input.action==='intent'){
      const error=validateC2S(input.intent);
      if(error||!input.intent.t.startsWith('g.'))reply={error:error||'Only game intents supported'};
      else reply=this.match.handle('unity',input.intent);
    }else if(input.action==='step')this.scheduler.advance(100);
    else if(input.action!=='state')reply={error:'Unknown match action'};
    // Zero-time callbacks resolve confirmed drafts; state reads do not advance the game clock.
    if(input.action==='intent')this.scheduler.advance(0);
    return this.view(reply);
  }
  view(reply={ok:true}){
    const ps=this.match.players.get('unity');
    const messages=this.messages.splice(0);
    const pub=this.match.publicView(),priv=ps.privateView();
    const field=this.match.fields.find(f=>f.players.includes('unity'));
    let combat=null;
    if(field?.battle&&['COMBAT','UNITE','FINAL_ASSAULT','HIDDEN_CORE'].includes(pub.phase)){
      const b=field.battle,snap=b.snapshot(),meta=b.fieldMeta();
      const stage=this.match.gd.raw.stages[meta.stageId||pub.stageId];
      combat={key:`${pub.round}:${field.fieldId}:${pub.phase}`,gt:snap.t,killed:snap.killed,total:snap.total,finished:b.finished,
        infos:meta.units.map(u=>({id:u.id,name:u.name||u.defId||String(u.id),assetId:u.spine,enemy:u.kind==='enemy',x:u.x,y:u.y,facing:u.facing,maxHp:u.maxHp})),
        states:snap.units.map(u=>({id:u[0],x:u[1],y:u[2],hp:u[3],maxHp:u[4],sp:u[5],spMax:u[6],flags:u[7],anim:u[8]})),
        events:messages.filter(m=>m.t==='b.ev'&&m.fieldId===field.fieldId).flatMap(m=>m.ev).map(e=>({kind:e[0],a:e[0]==='spawn'?e[1].id:typeof e[1]==='number'?e[1]:0,b:e[0]==='atk'?e[2]:0,value:['dmg','heal','skill'].includes(e[0])?e[2]:0})),
        tiles:(stage?.rows||[]).flatMap((line,row)=>Array.from(line,(glyph,col)=>({row,col,kind:stage.tiles[glyph]?.tileKey||glyph})))};
    }
    const named=p=>{
      if(!p)return null;
      const rec=p.kind==='item'?this.match.gd.item(p.id):p.kind==='token'?this.match.gd.token(p.id):this.match.gd.chess(p.id);
      return {...p,name:rec?.name||p.id,itemType:rec?.itemType||'',desc:rec?.desc||'',
        items:(p.items||[]).map(it=>named({...it,kind:'item'}))};
    };
    const ui={phase:pub.phase,round:pub.round,stageId:pub.stageId,lp:priv.lp,funds:priv.funds,
      deployCount:priv.deployCount,deployCap:priv.deployCap,canReady:priv.canReady,ready:priv.ready,
      shop:{...priv.shop,slots:priv.shop.slots.map((p,slot)=>p?{...named(p),slot}:null),
        rewardOffer:priv.shop.rewardOffer?{...priv.shop.rewardOffer,slots:priv.shop.rewardOffer.slots.map(named)}:null},
      hand:priv.hand.map((p,idx)=>p?{...named(p),idx}:null),temp:priv.temp.map(named),board:priv.board.map(named),
      tiles:[...ps.deployMap()].map(([key,kind])=>{const [row,col]=key.split(',').map(Number);return {row,col,kind};}),
      bands:this.match.gd.bandIds().map(id=>({id,name:this.match.gd.band(id).name,desc:this.match.gd.band(id).desc})),
      cards:pub.sp?.cards||[],effects:priv.effects,bonds:priv.bonds.map(b=>({...b,name:this.match.gd.bond(b.bondId)?.name||b.bondId})),
      ended:this.match.ended,paused:pub.paused,combat};
    return {reply,public:this.match.publicView(),private:ps.privateView(),messages,
      ui,
      bands:this.match.gd.bandIds().map(id=>({id,...this.match.gd.band(id)})),
      errors:this.match.errorCount,ended:this.match.ended,summary:this.summary||null};
  }
  dispose(){this.match.dispose();this.scheduler.dispose();}
}
