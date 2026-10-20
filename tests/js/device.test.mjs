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

// ---- sharing the shopping list -------------------------------------------------------------------

const clipboardOf = (writes, refuse = false) => ({
    writeText: async text => {
        if (refuse) {
            throw new Error('NotAllowedError');
        }
        writes.push(text);
    },
});

/** A page with a field the old way of copying can use; `exec` answers the copy command. */
function fakeDocument(exec = () => true) {
    const field = { style: {}, value: '', attributes: {}, selected: false, setAttribute(name, value) { this.attributes[name] = value; }, select() { this.selected = true; } };
    const log = { added: [], removed: [], commands: [] };
    return {
        field,
        log,
        createElement: tag => { field.tag = tag; return field; },
        body: { appendChild: element => log.added.push(element), removeChild: element => log.removed.push(element) },
        execCommand: command => { log.commands.push(command); return exec(command); },
    };
}

const abort = () => Object.assign(new Error('closed'), { name: 'AbortError' });

test('a phone with a share sheet gets the text and the answer is shared', async () => {
    const shared = [];
    const writes = [];

    const answer = await device.shareText('Đi chợ', { navigator: { share: async data => { shared.push(data); }, clipboard: clipboardOf(writes) } });

    assert.equal(answer, 'shared');
    assert.deepEqual(shared, [{ text: 'Đi chợ' }]);
    assert.deepEqual(writes, []);   // the clipboard is not touched when the share sheet took it
});

test('a person who closes the share sheet gets cancelled and nothing is copied behind their back', async () => {
    const writes = [];

    const answer = await device.shareText('Đi chợ', { navigator: { share: async () => { throw abort(); }, clipboard: clipboardOf(writes) } });

    assert.equal(answer, 'cancelled');
    assert.deepEqual(writes, []);
});

test('a share sheet that fails for another reason falls back to the clipboard', async () => {
    const writes = [];

    const answer = await device.shareText('Đi chợ', { navigator: { share: async () => { throw new Error('NotAllowedError'); }, clipboard: clipboardOf(writes) } });

    assert.equal(answer, 'copied');
    assert.deepEqual(writes, ['Đi chợ']);
});

test('a share that throws something that is not an error still falls back', async () => {
    const writes = [];

    const answer = await device.shareText('Đi chợ', { navigator: { share: () => Promise.reject(undefined), clipboard: clipboardOf(writes) } });

    assert.equal(answer, 'copied');
    assert.deepEqual(writes, ['Đi chợ']);
});

test('a browser with no share sheet copies the text', async () => {
    const writes = [];

    assert.equal(await device.shareText('Đi chợ', { navigator: { clipboard: clipboardOf(writes) } }), 'copied');
    assert.equal(await device.shareText('Đi chợ', { navigator: { share: 'yes', clipboard: clipboardOf(writes) } }), 'copied');
    assert.deepEqual(writes, ['Đi chợ', 'Đi chợ']);
});

test('a clipboard that refuses the text is followed by the old way of copying', async () => {
    const page = fakeDocument();

    const answer = await device.shareText('Đi chợ', { navigator: { clipboard: clipboardOf([], true) }, document: page });

    assert.equal(answer, 'copied');
    assert.equal(page.field.value, 'Đi chợ');
    assert.equal(page.field.tag, 'textarea');
    assert.equal(page.field.selected, true);
    assert.equal(page.field.attributes.readonly, '');
    assert.deepEqual(page.log.commands, ['copy']);
});

test('a browser with no clipboard api uses the old way at once', async () => {
    const page = fakeDocument();

    assert.equal(await device.shareText('Đi chợ', { navigator: {}, document: page }), 'copied');
    assert.equal(await device.shareText('Đi chợ', { navigator: { clipboard: {} }, document: fakeDocument() }), 'copied');
    assert.equal(page.field.value, 'Đi chợ');
});

test('the hidden field is taken out of the page again, whatever the copy command does', async () => {
    const worked = fakeDocument();
    const refused = fakeDocument(() => false);
    const broken = fakeDocument(() => { throw new Error('boom'); });

    await device.shareText('x', { navigator: {}, document: worked });
    await device.shareText('x', { navigator: {}, document: refused });
    await device.shareText('x', { navigator: {}, document: broken });

    for (const page of [worked, refused, broken]) {
        assert.equal(page.log.added.length, 1);
        assert.equal(page.log.removed.length, 1);
        assert.equal(page.log.removed[0], page.log.added[0]);
    }
});

test('only a copy command that says true counts as copied', async () => {
    assert.equal(await device.shareText('x', { navigator: {}, document: fakeDocument(() => false) }), 'failed');
    assert.equal(await device.shareText('x', { navigator: {}, document: fakeDocument(() => 1) }), 'failed');
    assert.equal(await device.shareText('x', { navigator: {}, document: fakeDocument(() => { throw new Error('boom'); }) }), 'failed');
});

test('a page with nothing to share with or copy to answers failed and does not throw', async () => {
    assert.equal(await device.shareText('x', {}), 'failed');
    assert.equal(await device.shareText('x', { navigator: {} }), 'failed');
    assert.equal(await device.shareText('x', { navigator: {}, document: {} }), 'failed');
    assert.equal(await device.shareText('x', { navigator: {}, document: { createElement: () => ({}), execCommand: () => true } }), 'failed');   // no body
    assert.equal(await device.shareText('x', { navigator: {}, document: { body: {}, createElement: () => ({}) } }), 'failed');                  // no copy command
    assert.equal(await device.shareText('x', { navigator: {}, document: { body: {}, execCommand: () => true } }), 'failed');                    // no way to make a field
});

test('in a page the file puts the three functions on mamcomDevice and they use the page itself', async () => {
    const shared = [];
    const page = { navigator: { vibrate: () => true, share: async data => { shared.push(data); } }, matchMedia: () => ({ matches: true }) };
    vm.runInNewContext(fs.readFileSync(new URL('./device.js', import.meta.url), 'utf8'), { self: page });   // no module: the browser branch

    assert.equal(typeof page.mamcomDevice.vibrate, 'function');
    assert.equal(page.mamcomDevice.vibrate(20), true);
    assert.equal(page.mamcomDevice.prefersReducedMotion(), true);
    assert.equal(await page.mamcomDevice.shareText('Đi chợ'), 'shared');
    assert.equal(JSON.stringify(shared), '[{"text":"Đi chợ"}]');   // the page lives in another realm, so compare the text
});
