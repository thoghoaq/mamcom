// How the service worker keeps the data package of MâmCơm (ADR-020).
//
// The app and its data are deployed apart (SPEC §7.6), so they are cached apart. Blazor's own code caches the app. This file is
// the rest: three kinds of file, three rules.
//
//   data-version.json     network first. A few bytes that say which bundle the server has. Online it is always fresh, and a copy
//                         is kept; offline the copy answers.
//   data-bundle.json?v=X  cache first. The address names the version, so the answer can never change: whoever has seen a version
//                         has it, offline too. The two newest versions are kept, older ones are dropped.
//   images/*              stale while revalidate. The picture on hand answers at once and the network renews it for next time.
//                         A request marked X-MamCom-Preload only fills the cache and does not renew a picture it already has.
//
// When the worker is installed it also fetches the newest bundle itself (warm), so the first visit is enough for offline use.
//
// Only a good answer is ever kept: a 404, a server error, or the start page that a single-page host sends for a path it does not
// know would otherwise be served from the cache for ever. The functions take the browser's caches and fetch as arguments, so the
// tests (tests/js) run them in Node.
(function (root) {
    'use strict';

    // The names of the package on the server. MamCom.Core's BundleFiles has the same three; a test keeps them equal.
    const FILES = { bundle: 'data-bundle.json', version: 'data-version.json', images: 'images/' };
    const DATA_CACHE = 'mamcom-data-v1';
    const KEEP_BUNDLES = 2;
    const PRELOAD_HEADER = 'X-MamCom-Preload';
    const WARM_TIMEOUT_MS = 20000;

    /** 'version', 'bundle' or 'image' for a file of the data package under the scope of the worker; null for anything else. */
    function classify(url, scope) {
        const address = new URL(url);
        const base = new URL(scope);
        if (address.origin !== base.origin || !address.pathname.startsWith(base.pathname)) {
            return null;
        }

        const path = address.pathname.slice(base.pathname.length);
        if (path === FILES.version) return 'version';
        if (path === FILES.bundle) return 'bundle';
        if (path.startsWith(FILES.images) && path.length > FILES.images.length) return 'image';
        return null;
    }

    /** Blazor's precache takes every file the build lists; the data package is not part of the app and must not be in it. */
    function belongsToApp(assetUrl) {
        return classify(new URL(assetUrl, 'https://app.invalid/').href, 'https://app.invalid/') === null;
    }

    function isGood(kind, response) {
        if (!response || !response.ok) return false;
        const type = (response.headers.get('content-type') || '').toLowerCase();
        return kind === 'image' ? type.startsWith('image/') : type.includes('json');
    }

    async function networkFirst(request, deps) {
        const cache = await deps.caches.open(DATA_CACHE);
        let failure = null;
        try {
            const response = await deps.fetch(request, { cache: 'no-store' });
            if (isGood('version', response)) {
                await cache.put(request.url, response.clone());
                return response;
            }
            failure = response;
        } catch (error) {
            const kept = await cache.match(request.url);
            if (kept) return kept;
            throw error;
        }

        return (await cache.match(request.url)) || failure;
    }

    async function cacheFirst(request, deps) {
        const cache = await deps.caches.open(DATA_CACHE);
        const kept = await cache.match(request.url);
        if (kept) return kept;

        const response = await deps.fetch(request);
        if (isGood('bundle', response)) {
            await cache.put(request.url, response.clone());
            await dropOldBundles(cache);
        }
        return response;
    }

    /** The newest versions stay; a cache lists its entries in the order they were added. */
    async function dropOldBundles(cache) {
        const bundles = (await cache.keys()).filter(key => new URL(key.url).pathname.endsWith('/' + FILES.bundle));
        for (const old of bundles.slice(0, Math.max(0, bundles.length - KEEP_BUNDLES))) {
            await cache.delete(old);
        }
    }

    async function staleWhileRevalidate(request, deps, waitUntil) {
        const cache = await deps.caches.open(DATA_CACHE);
        const kept = await cache.match(request.url);

        if (kept && request.headers.get(PRELOAD_HEADER)) {
            return kept;
        }

        const renew = deps.fetch(request)
            .then(async response => {
                if (isGood('image', response)) {
                    await cache.put(request.url, response.clone());
                }
                return response;
            });

        if (kept) {
            waitUntil(renew.catch(() => { /* offline: the picture on hand is the answer */ }));
            return kept;
        }

        return renew;
    }

    /**
     * The answer to a request for a file of the data package.
     * @param {'version'|'bundle'|'image'} kind what {@link classify} said
     * @param {{caches: CacheStorage, fetch: Function}} deps the browser's caches and fetch
     * @param {(work: Promise) => void} waitUntil keeps the worker alive for work that outlives the answer
     */
    function handle(kind, request, deps, waitUntil) {
        switch (kind) {
            case 'version': return networkFirst(request, deps);
            case 'bundle': return cacheFirst(request, deps);
            case 'image': return staleWhileRevalidate(request, deps, waitUntil || (() => { }));
            default: throw new Error('Not a file of the data package: ' + kind);
        }
    }

    /**
     * Brings the newest bundle onto the device when the worker is installed, so a device that goes offline right after its first
     * visit still has its data: the page of that visit was loaded before this worker took charge, so none of its requests passed
     * through here. It never fails and never takes longer than the timeout: no network, no package published or a slow server only
     * mean there is nothing to warm yet, and the app installs all the same. The next visit asks through the worker as usual.
     * The pictures are not part of it: the app keeps them (they are only a convenience, a placeholder stands in), and it knows which.
     * @returns {Promise<string|null>} the version that is on the device now, or null
     */
    async function warm(deps, scope, timeoutMs = WARM_TIMEOUT_MS) {
        let timer;
        const limit = new Promise(resolve => { timer = setTimeout(() => resolve(null), timeoutMs); });
        try {
            return await Promise.race([bringBundle(deps, scope), limit]);
        } catch {
            return null;
        } finally {
            clearTimeout(timer);
        }
    }

    async function bringBundle(deps, scope) {
        const base = new URL(scope);
        const answer = await handle('version', new Request(new URL(FILES.version, base)), deps);
        if (!isGood('version', answer)) return null;

        const version = (await answer.json()).version;
        if (typeof version !== 'string' || version === '') return null;

        const bundle = await handle('bundle', new Request(new URL(FILES.bundle, base).href + '?v=' + encodeURIComponent(version)), deps);
        return isGood('bundle', bundle) ? version : null;
    }

    const api = { FILES, DATA_CACHE, KEEP_BUNDLES, PRELOAD_HEADER, WARM_TIMEOUT_MS, classify, belongsToApp, handle, warm };
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    } else {
        root.MamComData = api;
    }
})(typeof self !== 'undefined' ? self : globalThis);
