import assert from 'node:assert/strict';
import {UnityMatchSession} from './unity-match-session.mjs';
const s=new UnityMatchSession();
try {
  s.command({action:'intent',intent:{t:'g.autoplay',on:true}});
  let seen=false,events=0;
  for(let i=0;i<3000&&s.match.round<2;i++){
    const v=s.command({action:'step'}),c=v.ui.combat;
    if(!c)continue;
    const b=s.match.fields.find(f=>f.players.includes('unity')).battle;
    assert.deepEqual(c.states,b.snapshot().units.map(u=>({id:u[0],x:u[1],y:u[2],hp:u[3],maxHp:u[4],sp:u[5],spMax:u[6],flags:u[7],anim:u[8]})));
    assert.ok(c.tiles.length>0);assert.ok(c.infos.every(u=>Number.isFinite(u.id)&&u.assetId&&u.name));
    if(c.states.length)seen=true;events+=c.events.length;
    const time=b.time;s.command({action:'state'});assert.equal(b.time,time);
  }
  assert.ok(seen);assert.ok(events>0);assert.ok(s.match.round>=2);assert.equal(s.match.errorCount,0);
  console.log('Unity combat adapter PASS: exact snapshot parity, map, metadata, events and next round');
}finally{s.dispose();}
