import assert from 'node:assert/strict';
import {UnityMatchSession} from './unity-match-session.mjs';
const s=new UnityMatchSession();
try {
  const intent=msg=>s.command({action:'intent',intent:msg});
  intent({t:'g.infoReady'});intent({t:'g.band',bandId:s.match.gd.bandIds()[0]});
  for(let i=0;i<200&&s.match.phase!=='PREP';i++)s.command({action:'step'});
  assert.equal(s.match.phase,'PREP');
  const ps=s.match.players.get('unity'),gd=s.match.gd;
  // Controlled grants exercise the existing engine's equipment and merge paths, not a replacement ruleset.
  const id=gd.visibleChess.find(id=>gd.chess(id)?.tier===1&&!gd.chess(id).isGolden&&s.match.pool.left(id)>=3);
  assert.ok(id);const unit=ps.acquireChess(id);
  const itemIds=Object.values(gd.raw.items).filter(x=>x.itemType==='EQUIP'&&!x.isGolden&&x.kind==='passive').slice(0,3).map(x=>x.itemId||x.id);
  assert.equal(itemIds.length,3);
  const items=itemIds.map(id=>ps.acquireItem(id));
  for(const item of items.slice(0,2))assert.ok(intent({t:'g.equip',itemUid:item.uid,targetUid:unit.uid}).reply.ok);
  let view=s.view();let target=view.ui.hand.find(x=>x?.uid===unit.uid);
  assert.equal(target.items.length,2);assert.ok(target.items.every(x=>x.name&&x.itemType==='EQUIP'));
  assert.ok(intent({t:'g.equip',itemUid:items[2].uid,targetUid:unit.uid,replaceUid:999999}).reply.error);
  assert.ok(intent({t:'g.equip',itemUid:items[2].uid,targetUid:unit.uid,replaceUid:items[0].uid}).reply.ok);
  assert.equal(ps.find(items[0].uid),null,'selected replaced item is destroyed');
  assert.ok(ps.find(items[2].uid));
  ps.acquireChess(id);const elite=ps.acquireChess(id);
  assert.equal(elite.id,gd.goldenIdOf(id));
  view=s.view();assert.ok(view.ui.shop.rewardOffer?.slots.length);
  assert.ok(view.ui.shop.rewardOffer.slots.every(x=>x.name));
  const offers=ps.offers.length;assert.ok(intent({t:'g.reward',idx:0}).reply.ok);assert.equal(ps.offers.length,offers-1);
  const spare=Object.values(gd.raw.items).find(x=>x.itemType==='EQUIP'&&!x.isGolden&&x.kind==='passive'&&!itemIds.includes(x.itemId||x.id));
  for(let i=0;i<ps.hand.length;i++)if(!ps.hand[i])ps.hand[i]=ps.newPiece('item',spare.itemId||spare.id);
  const temp=ps.newPiece('item',spare.itemId||spare.id);assert.equal(ps.stow(temp,{toTemp:true}),'temp');ps.recompute();
  assert.ok(intent({t:'g.ready',ready:true}).reply.error,'temporary pieces block ready');
  const disposable=ps.hand.find(x=>x?.kind==='item');assert.ok(disposable);
  assert.ok(intent({t:'g.destroy',uid:disposable.uid}).reply.ok);
  assert.equal(ps.find(temp.uid).area,'hand','freeing a hand slot pulls temporary inventory in');
  assert.ok(s.view().ui.canReady);
  assert.equal(s.match.errorCount,0);assert.equal(s.scheduler.errors.length,0);
  console.log('Unity preparation PASS: equip, explicit replacement, promotion reward, temporary inventory and readiness');
}finally{s.dispose();}
