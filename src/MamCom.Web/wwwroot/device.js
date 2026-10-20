// What the app asks of the phone itself (MamCom.Data's BrowserDeviceFeatures calls these two). Each one checks that the browser has the
// feature before it uses it and answers false when it does not: Safari on iOS has no vibration, an old browser has no media queries.
// The environment is a parameter so the tests (tests/js) can give a fake one.
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

    const api = { vibrate, prefersReducedMotion };
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    } else {
        root.mamcomDevice = api;
    }
})(typeof self !== 'undefined' ? self : globalThis);
