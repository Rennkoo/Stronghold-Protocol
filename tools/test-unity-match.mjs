import assert from 'node:assert/strict';
import {UnityMatchSession} from './unity-match-session.mjs';
const s=new UnityMatchSession();
try {
  assert.equal(s.view().public.phase,'INFO_CHECK');
  const intent=msg=>s.command({action:'intent',intent:msg});
  assert.ok(intent({t:'g.buy',slot:-1}).reply.error);
  assert.equal(intent({t:'g.infoReady'}).public.phase,'BAND_DRAFT');
  const band=s.match.gd.bandIds()[0];
  assert.ok(intent({t:'g.band',bandId:band}).reply.ok);
  for(let i=0;i<200&&s.match.phase!=='PREP';i++)s.command({action:'step'});
  assert.equal(s.match.phase,'PREP');
  const before=s.view().private;
  const slot=before.shop.slots.findIndex(x=>x&&!x.sold&&x.price<=before.funds);
  assert.ok(slot>=0);
  const bought=intent({t:'g.buy',slot});
  assert.ok(bought.reply.ok);
  assert.equal(bought.private.funds,before.funds-before.shop.slots[slot].price);
  assert.ok(bought.private.hand.some(Boolean));
  const clock=s.scheduler.now();s.command({action:'state'});assert.equal(s.scheduler.now(),clock);
  // Original bot handles drafts and legal deployment; run a real round and return to prep.
  intent({t:'g.autoplay',on:true});
  for(let i=0;i<3000&&s.match.round<2&&!s.match.ended;i++)s.command({action:'step'});
  assert.ok(s.match.round>=2,'round settlement must reach next round');
  assert.equal(s.match.errorCount,0);assert.equal(s.scheduler.errors.length,0);
  console.log('Unity Match PASS: original shop purchase, validation, draft, combat and next round');
} finally {s.dispose();}
