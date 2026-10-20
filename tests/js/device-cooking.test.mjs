// Tests of device.js for the cooking mode (a lit screen, the alarm of a timer) in Node, with a fake navigator, a fake window and a fake sound system.
// Run by `dotnet test` through MamCom.Data.Tests (ServiceWorkerTests), or by hand: node --test tests/js
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';
import vm from 'node:vm';

const require = createRequire(import.meta.url);
const device = require('./device.js');

const tick = () => new Promise(resolve => setImmediate(resolve));

// ---- keeping the screen lit ----------------------------------------------------------------------------------------------------------

/** A page whose Screen Wake Lock hands out sentinels; `refuse` makes it say no, as a low battery does. */
function fakeWakePage({ refuse = false, withDocument = true } = {}) {
    const requests = [];
    const sentinels = [];
    const listeners = {};
    const page = {
        navigator: {
            wakeLock: {
                request: async type => {
                    requests.push(type);
                    if (refuse) {
                        throw new Error('NotAllowedError');
                    }
                    const sentinel = { released: false, release: async () => { sentinel.released = true; } };
                    sentinels.push(sentinel);
                    return sentinel;
                },
            },
        },
    };
    if (withDocument) {
        page.document = { visibilityState: 'visible', addEventListener: (name, listener) => { (listeners[name] ??= []).push(listener); } };
    }
    return { page, requests, sentinels, listeners };
}

test('a browser with no wake lock says unsupported and does not fail', async () => {
    assert.equal(await device.keepScreenOn({}), 'unsupported');
    assert.equal(await device.keepScreenOn({ navigator: {} }), 'unsupported');
    assert.equal(await device.keepScreenOn({ navigator: { wakeLock: {} } }), 'unsupported');
    assert.equal(await device.keepScreenOn({ navigator: { wakeLock: { request: 'yes' } } }), 'unsupported');
});

test('a browser that gives the lock is asked for a screen lock and the answer is active', async () => {
    const { page, requests } = fakeWakePage();

    assert.equal(await device.keepScreenOn(page), 'active');
    assert.deepEqual(requests, ['screen']);
});

test('a browser that refuses the lock gives denied and does not fail', async () => {
    const { page, requests } = fakeWakePage({ refuse: true });

    assert.equal(await device.keepScreenOn(page), 'denied');
    assert.equal(requests.length, 1);
    assert.equal(await device.releaseScreen(page), false);   // there was nothing to give back
});

test('asking twice while the lock is held does not ask the browser twice', async () => {
    const { page, requests, listeners } = fakeWakePage();

    await device.keepScreenOn(page);
    await device.keepScreenOn(page);

    assert.equal(requests.length, 1);
    assert.equal(listeners.visibilitychange.length, 1);   // and one listener, not two
});

test('a lock the browser took back when the page was hidden is asked for again when the page is shown', async () => {
    const { page, requests, sentinels, listeners } = fakeWakePage();
    await device.keepScreenOn(page);

    sentinels[0].released = true;                 // what the browser does when the page goes to the background
    page.document.visibilityState = 'hidden';
    listeners.visibilitychange[0]();
    assert.equal(requests.length, 1);             // nothing to ask for while it is hidden

    page.document.visibilityState = 'visible';
    listeners.visibilitychange[0]();
    await tick();

    assert.equal(requests.length, 2);
});

test('a lock that is still held is not asked for again when the page is shown', async () => {
    const { page, requests, listeners } = fakeWakePage();
    await device.keepScreenOn(page);

    listeners.visibilitychange[0]();
    await tick();

    assert.equal(requests.length, 1);
});

test('giving the screen back releases the lock once and stops asking for it again', async () => {
    const { page, requests, sentinels, listeners } = fakeWakePage();
    await device.keepScreenOn(page);

    assert.equal(await device.releaseScreen(page), true);
    assert.equal(sentinels[0].released, true);
    assert.equal(await device.releaseScreen(page), false);

    sentinels[0].released = true;
    listeners.visibilitychange[0]();
    await tick();
    assert.equal(requests.length, 1);             // the page no longer wants it
});

test('a lock that cannot be released is not a failure', async () => {
    const { page, sentinels } = fakeWakePage();
    await device.keepScreenOn(page);
    sentinels[0].release = async () => { throw new Error('boom'); };

    assert.equal(await device.releaseScreen(page), false);
});

test('a page with no document still keeps the screen lit', async () => {
    const { page } = fakeWakePage({ withDocument: false });

    assert.equal(await device.keepScreenOn(page), 'active');
});

test('two pages do not share their locks', async () => {
    const one = fakeWakePage();
    const two = fakeWakePage();
    await device.keepScreenOn(one.page);
    await device.keepScreenOn(two.page);

    await device.releaseScreen(one.page);

    assert.equal(one.sentinels[0].released, true);
    assert.equal(two.sentinels[0].released, false);
});

// ---- the alarm of a timer ------------------------------------------------------------------------------------------------------------

/** A sound system that remembers how many contexts were made and which beeps were scheduled. */
function fakeAudio({ state = 'running', resumeFails = false, staysSuspended = false, failsToBeep = false } = {}) {
    const log = { created: 0, resumed: 0, beeps: [], contexts: [] };
    class Context {
        constructor() {
            log.created += 1;
            log.contexts.push(this);
            this.state = state;
            this.currentTime = 10;
            this.destination = {};
        }

        async resume() {
            log.resumed += 1;
            if (resumeFails) {
                throw new Error('NotAllowedError');
            }
            if (!staysSuspended) {
                this.state = 'running';
            }
        }

        createOscillator() {
            if (failsToBeep) {
                throw new Error('boom');
            }
            const beep = { frequency: {}, started: null, stopped: null };
            log.beeps.push(beep);
            return { frequency: beep.frequency, connect() {}, start: at => { beep.started = at; }, stop: at => { beep.stopped = at; } };
        }

        createGain() {
            return { gain: {}, connect() {} };
        }
    }
    return { Context, log };
}

test('a page with no sound system cannot ready an alarm sound', async () => {
    assert.equal(await device.prepareAlert({}), false);
    assert.equal(await device.prepareAlert({ AudioContext: 'no' }), false);
});

test('the sound is readied on the sound system of the page, once, and also on the old name', async () => {
    const audio = fakeAudio();
    const page = { AudioContext: audio.Context };
    const old = fakeAudio();

    assert.equal(await device.prepareAlert(page), true);
    assert.equal(await device.prepareAlert(page), true);
    assert.equal(audio.log.created, 1);   // the second tap uses the context of the first
    assert.equal(await device.prepareAlert({ webkitAudioContext: old.Context }), true);
    assert.equal(old.log.created, 1);
});

test('a sound system that was suspended is woken by the tap', async () => {
    const audio = fakeAudio({ state: 'suspended' });

    assert.equal(await device.prepareAlert({ AudioContext: audio.Context }), true);
    assert.equal(audio.log.resumed, 1);
});

test('a sound system that refuses to wake, or stays asleep, or cannot be made gives false and does not fail', async () => {
    assert.equal(await device.prepareAlert({ AudioContext: fakeAudio({ state: 'suspended', resumeFails: true }).Context }), false);
    assert.equal(await device.prepareAlert({ AudioContext: fakeAudio({ state: 'suspended', staysSuspended: true }).Context }), false);
    assert.equal(await device.prepareAlert({ AudioContext: function () { throw new Error('boom'); } }), false);
});

test('the alarm buzzes with a pattern and beeps three times when the sound was readied', async () => {
    const audio = fakeAudio();
    let pattern = null;
    const page = { AudioContext: audio.Context, navigator: { vibrate: p => { pattern = p; return true; } } };
    await device.prepareAlert(page);

    const alarmed = device.alertUser(page);

    assert.equal(alarmed, true);
    assert.deepEqual(pattern, [300, 150, 300, 150, 600]);
    assert.equal(audio.log.beeps.length, 3);
    assert.deepEqual(audio.log.beeps.map(b => b.started), [10, 10.4, 10.8]);
    assert.deepEqual(audio.log.beeps.map(b => Math.round((b.stopped - b.started) * 100) / 100), [0.2, 0.2, 0.2]);
    assert.ok(audio.log.beeps.every(b => b.frequency.value === 880));
});

test('without a readied sound the alarm only buzzes, and without a buzz it only beeps', async () => {
    const audio = fakeAudio();
    const buzzOnly = { AudioContext: audio.Context, navigator: { vibrate: () => true } };
    const beepOnly = { AudioContext: fakeAudio().Context, navigator: {} };
    await device.prepareAlert(beepOnly);

    assert.equal(device.alertUser(buzzOnly), true);     // never readied: no beeps, but the phone buzzed
    assert.equal(audio.log.beeps.length, 0);
    assert.equal(device.alertUser(beepOnly), true);     // a phone that cannot buzz
});

test('an alarm that neither buzzes nor beeps says false and does not fail', () => {
    assert.equal(device.alertUser({}), false);
    assert.equal(device.alertUser({ navigator: {} }), false);
    assert.equal(device.alertUser({ navigator: { vibrate: () => false } }), false);
    assert.equal(device.alertUser({ navigator: { vibrate: () => 1 } }), false);
});

test('a sound system that breaks while beeping is no sound', async () => {
    const broken = { AudioContext: fakeAudio({ failsToBeep: true }).Context, navigator: {} };
    await device.prepareAlert(broken);

    assert.equal(device.alertUser(broken), false);
});

test('a sound system that went back to sleep is not used', async () => {
    const audio = fakeAudio();
    const page = { AudioContext: audio.Context, navigator: {} };
    await device.prepareAlert(page);
    audio.log.contexts[0].state = 'suspended';   // the page was hidden and the browser put the sound to sleep

    assert.equal(device.alertUser(page), false);
    assert.equal(audio.log.beeps.length, 0);
});

test('in a page the file puts the lit screen and the alarm on mamcomDevice too', async () => {
    const page = { navigator: { wakeLock: { request: async () => ({ released: false, release: async () => {} }) } } };
    vm.runInNewContext(fs.readFileSync(new URL('./device.js', import.meta.url), 'utf8'), { self: page });

    assert.equal(await page.mamcomDevice.keepScreenOn(), 'active');
    assert.equal(await page.mamcomDevice.releaseScreen(), true);
    assert.equal(await page.mamcomDevice.prepareAlert(), false);   // no sound system in this page
    assert.equal(page.mamcomDevice.alertUser(), false);
});
