import { test } from 'node:test';
import assert from 'node:assert/strict';
import { adaptiveResolution } from '../../public/js/render/resolution.js';
import { DeviceView } from '../../public/js/render/units.js';
import { presetCamera } from '../../public/js/render/projection.js';
import { installFakePixi, fakeViewCtx } from './fakepixi.js';
import { ImpostorAtlas } from '../../public/js/render/impostor.js';

test('a 120-unit crowd distributes scheduled skeleton refreshes evenly instead of random spikes', () => {
  const fake = installFakePixi();
  try {
    const atlas = new ImpostorAtlas({ resolution: 1 });
    const phases = Array.from({ length: 120 }, () => atlas.nextPhase());
    for (let interval = 2; interval <= 6; interval++) {
      const frames = Array.from({ length: interval }, (_, frame) => phases.filter(p => (p + frame) % interval === 0).length);
      assert.equal(frames.reduce((a, b) => a + b), 120, 'each unit refreshes exactly once per interval');
      assert.ok(Math.max(...frames) - Math.min(...frames) <= 1, 'no crowded refresh frame');
    }
    atlas.destroy();
  } finally { fake.restore(); }
});

test('sustained load lowers raster pixels and recovery restores the chosen resolution', () => {
  const base = 2;
  const busy = adaptiveResolution(base, 3);
  assert.ok(busy * busy / (base * base) <= 0.37, 'at least 63% fewer pixels per canvas at DPR 2');
  assert.equal(adaptiveResolution(base, 0), base);
  assert.equal(adaptiveResolution(1, 3), 0.75);
  assert.equal(adaptiveResolution(0.5, 3), 0.5, 'never raises a lower native resolution');
});

test('static fallback device geometry stays cached; movement, camera and death invalidate it', () => {
  const fake = installFakePixi();
  try {
    const v = new DeviceView(fakeViewCtx(fake.P), { id: 1, defId: 'trap_1105_accrate', x: 5, y: 10 });
    const cam = presetCamera('normal', { width: 1280, height: 720 });
    let clears = 0;
    v.gfx.clear = () => { clears++; return v.gfx; };
    for (let i = 0; i < 120; i++) v.update(1 / 60, cam, i / 60);
    assert.equal(clears, 1, '120 static frames tessellate once');
    v.x++;
    v.update(1 / 60, cam, 2);
    assert.equal(clears, 2);
    const replacement = presetCamera('normal', { width: 1920, height: 1080 });
    v.update(1 / 60, replacement, 3);
    assert.equal(clears, 3, 'new camera even at the same version');
    v.dying = 0.1;
    v.update(0.05, replacement, 4);
    assert.equal(clears, 4);
    v.update(0.1, replacement, 5);
    assert.equal(clears, 5);
    v.update(1 / 60, replacement, 6);
    assert.equal(clears, 6, 'final geometry catches the end of the death animation');
    v.destroy();
  } finally { fake.restore(); }
});
