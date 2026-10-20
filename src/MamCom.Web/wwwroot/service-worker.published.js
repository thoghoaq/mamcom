// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
// The rules for the data package (the bundle, its version, the pictures): service-worker.data.js, ADR-020.
self.importScripts('./service-worker.data.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.svg$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        // The data package is deployed apart from the app, so it is cached apart: never part of the app's precache.
        .filter(asset => MamComData.belongsToApp(asset.url))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const precache = caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
    // The data package is warmed beside the app: the page of the first visit was loaded before this worker took charge, so none of its
    // requests came through here, and without this a device that goes offline at once would have the app but no dishes. It cannot fail the install.
    const warm = MamComData.warm({ caches, fetch: (request, options) => fetch(request, options) }, self.registration.scope);
    await Promise.all([precache, warm]);
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => (key.startsWith(cacheNamePrefix) && key !== cacheName) || (key.startsWith('mamcom-data-') && key !== MamComData.DATA_CACHE))
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    // The data package has its own rules. Everything else is the app, below.
    const kind = event.request.method === 'GET' ? MamComData.classify(event.request.url, self.registration.scope) : null;
    if (kind) {
        return MamComData.handle(kind, event.request, { caches, fetch: (request, options) => fetch(request, options) }, work => event.waitUntil(work));
    }

    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}
