// What the app asks of the phone itself (MamCom.Data's BrowserDeviceFeatures calls these). Each one checks that the browser has the
// feature before it uses it and answers false (or 'failed') when it does not: Safari on iOS has no vibration, an old browser has no media
// queries or clipboard. The environment is a parameter so the tests (tests/js) can give a fake one.
(function (root) {
    'use strict';

    /** Buzzes for `milliseconds`; true only when the device did. */
    function vibrate(milliseconds, env) {
        const navigator = (env || root).navigator;
        return !!navigator && typeof navigator.vibrate === 'function' && navigator.vibrate(milliseconds) === true;
    }

    /** True when the person asked their device for less movement. */
    function prefersReducedMotion(env) {
        const win = env || root;
        return typeof win.matchMedia === 'function' && win.matchMedia('(prefers-reduced-motion: reduce)').matches === true;
    }

    /** Puts `text` on the clipboard with the old way (a hidden field and a copy command), for a browser without the clipboard API. */
    function copyWithSelection(text, document) {
        if (!document || typeof document.createElement !== 'function' || typeof document.execCommand !== 'function' || !document.body) {
            return false;
        }

        const field = document.createElement('textarea');
        field.value = text;
        field.setAttribute('readonly', '');
        field.style.position = 'fixed';
        field.style.opacity = '0';
        document.body.appendChild(field);
        try {
            field.select();
            return document.execCommand('copy') === true;
        } catch (error) {
            return false;
        } finally {
            document.body.removeChild(field);
        }
    }

    /** True when `text` is on the clipboard. */
    async function copyText(text, win) {
        const navigator = win.navigator;
        if (navigator && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch (error) {
                // refused (no permission, or the page is not focused): try the old way
            }
        }

        return copyWithSelection(text, win.document);
    }

    /**
     * Offers `text` to the share sheet of the phone, where Zalo is; a browser with no share sheet gets it on the clipboard instead.
     * Answers 'shared', 'copied', 'cancelled' (the person closed the sheet, so nothing is copied behind their back) or 'failed'.
     */
    async function shareText(text, env) {
        const win = env || root;
        const navigator = win.navigator;

        if (navigator && typeof navigator.share === 'function') {
            try {
                await navigator.share({ text });
                return 'shared';
            } catch (error) {
                if (error && error.name === 'AbortError') {
                    return 'cancelled';
                }
                // any other refusal: the clipboard still works
            }
        }

        return (await copyText(text, win)) ? 'copied' : 'failed';
    }

    // What the cooking mode needs: a lit screen and an alarm. Each is kept per window, so a test with a fake window has its own.
    const wakeStates = new WeakMap();
    const audioContexts = new WeakMap();

    function wakeStateOf(win) {
        let state = wakeStates.get(win);
        if (!state) {
            state = { sentinel: null, wanted: false, listening: false };
            wakeStates.set(win, state);
        }
        return state;
    }

    /** True when the screen is held on: it already was, or the browser gave the lock now. */
    async function acquire(win, state) {
        if (state.sentinel && !state.sentinel.released) {
            return true;
        }

        try {
            state.sentinel = await win.navigator.wakeLock.request('screen');
            return true;
        } catch (error) {
            state.sentinel = null;   // refused: a low battery, a hidden page, a setting of the device
            return false;
        }
    }

    /**
     * Keeps the screen lit (Screen Wake Lock). The browser lets go of the lock when the page is hidden, so it is asked for again
     * when the page comes back. Answers 'active', 'unsupported' (the browser has none) or 'denied'.
     */
    async function keepScreenOn(env) {
        const win = env || root;
        const wakeLock = win.navigator && win.navigator.wakeLock;
        if (!wakeLock || typeof wakeLock.request !== 'function') {
            return 'unsupported';
        }

        const state = wakeStateOf(win);
        state.wanted = true;
        if (!state.listening && win.document && typeof win.document.addEventListener === 'function') {
            state.listening = true;
            win.document.addEventListener('visibilitychange', () => {
                if (state.wanted && win.document.visibilityState === 'visible') {
                    acquire(win, state);
                }
            });
        }

        return (await acquire(win, state)) ? 'active' : 'denied';
    }

    /** Lets the screen turn off again; true when there was a lock to give back. */
    async function releaseScreen(env) {
        const state = wakeStateOf(env || root);
        state.wanted = false;

        const sentinel = state.sentinel;
        state.sentinel = null;
        if (!sentinel) {
            return false;
        }

        try {
            await sentinel.release();
            return true;
        } catch (error) {
            return false;
        }
    }

    /**
     * Readies the sound of the alarm. A page may only make a sound after the person tapped it, so this runs on the tap that starts a
     * timer; the sound is played later, when the timer ends. True when the page can make a sound.
     */
    async function prepareAlert(env) {
        const win = env || root;
        let context = audioContexts.get(win);
        if (!context) {
            const Context = win.AudioContext || win.webkitAudioContext;
            if (typeof Context !== 'function') {
                return false;
            }

            try {
                context = new Context();
            } catch (error) {
                return false;
            }
            audioContexts.set(win, context);
        }

        try {
            if (context.state === 'suspended' && typeof context.resume === 'function') {
                await context.resume();
            }
        } catch (error) {
            return false;
        }

        return context.state === 'running';
    }

    /** Three short beeps with the sound that <see cref="prepareAlert"/> got ready; false when there is none. */
    function beep(win) {
        const context = audioContexts.get(win);
        if (!context || context.state !== 'running') {
            return false;
        }

        try {
            for (let i = 0; i < 3; i++) {
                const oscillator = context.createOscillator();
                const gain = context.createGain();
                oscillator.frequency.value = 880;
                gain.gain.value = 0.3;
                oscillator.connect(gain);
                gain.connect(context.destination);
                oscillator.start(context.currentTime + i * 0.4);
                oscillator.stop(context.currentTime + i * 0.4 + 0.2);
            }
            return true;
        } catch (error) {
            return false;
        }
    }

    /** The alarm of a timer that ran out: a buzz where the phone can and beeps where the page may; true when either happened. */
    function alertUser(env) {
        const win = env || root;
        const navigator = win.navigator;
        const buzzed = !!navigator && typeof navigator.vibrate === 'function' && navigator.vibrate([300, 150, 300, 150, 600]) === true;
        const sounded = beep(win);
        return buzzed || sounded;
    }

    const api = { vibrate, prefersReducedMotion, shareText, keepScreenOn, releaseScreen, prepareAlert, alertUser };
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    } else {
        root.mamcomDevice = api;
    }
})(typeof self !== 'undefined' ? self : globalThis);
