// What the app asks of the phone itself (MamCom.Data's BrowserDeviceFeatures calls these three). Each one checks that the browser has the
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

    const api = { vibrate, prefersReducedMotion, shareText };
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    } else {
        root.mamcomDevice = api;
    }
})(typeof self !== 'undefined' ? self : globalThis);
