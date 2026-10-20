// Tests of device.js (the app host's two questions to the phone) in Node, with a fake navigator and a fake window.
// Run by `dotnet test` through MamCom.Data.Tests (ServiceWorkerTests), or by hand: node --test tests/js
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';
import vm from 'node:vm';

const require = createRequire(import.meta.url);
const device = require('./device.js');

const withNavigator = navigator => ({ navigator });
let lastQuery = null;
const withMedia = matches => ({ matchMedia: query => { lastQuery = query; return { matches }; } });

test('a device that can vibrate is told how long and the answer is true', () => {
    let asked = null;

    const buzzed = device.vibrate(30, withNavigator({ vibrate: ms => { asked = ms; return true; } }));

    assert.equal(buzzed, true);
    assert.equal(asked, 30);
});

test('a device that refuses to vibrate gives false', () => {
    assert.equal(device.vibrate(30, withNavigator({ vibrate: () => false })), false);
});

test('a browser with no vibration, as Safari on iOS, gives false and does not fail', () => {
    assert.equal(device.vibrate(30, withNavigator({})), false);
    assert.equal(device.vibrate(30, withNavigator({ vibrate: 'yes' })), false);
    assert.equal(device.vibrate(30, {}), false);
});

test('only an answer of true counts as having vibrated', () => {
    assert.equal(device.vibrate(30, withNavigator({ vibrate: () => 1 })), false);
    assert.equal(device.vibrate(30, withNavigator({ vibrate: () => undefined })), false);
});

test('a person who asked for less movement is known by the media query', () => {
    assert.equal(device.prefersReducedMotion(withMedia(true)), true);
    assert.equal(lastQuery, '(prefers-reduced-motion: reduce)');
    assert.equal(device.prefersReducedMotion(withMedia(false)), false);
});

test('a browser with no media queries means no wish for less movement', () => {
    assert.equal(device.prefersReducedMotion({}), false);
    assert.equal(device.prefersReducedMotion({ matchMedia: 'no' }), false);
});

test('only a match of true counts', () => {
    assert.equal(device.prefersReducedMotion({ matchMedia: () => ({ matches: 'yes' }) }), false);
    assert.equal(device.prefersReducedMotion({ matchMedia: () => ({}) }), false);
});

test('in a page the file puts the two functions on mamcomDevice and they use the page itself', () => {
    const page = { navigator: { vibrate: () => true }, matchMedia: () => ({ matches: true }) };
    vm.runInNewContext(fs.readFileSync(new URL('./device.js', import.meta.url), 'utf8'), { self: page });   // no module: the browser branch

    assert.equal(typeof page.mamcomDevice.vibrate, 'function');
    assert.equal(page.mamcomDevice.vibrate(20), true);
    assert.equal(page.mamcomDevice.prefersReducedMotion(), true);
});
