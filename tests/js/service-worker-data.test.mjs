// Tests of service-worker.data.js (ADR-020) in Node, with a fake cache and a fake network.
// Run by `dotnet test` through MamCom.Data.Tests (ServiceWorkerTests), or by hand: node --test tests/js
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const sw = require('./service-worker.data.js');

const SCOPE = 'https://mamcom.test/';
const JSON_TYPE = { 'content-type': 'application/json' };
const IMAGE_TYPE = { 'content-type': 'image/webp' };

class FakeCache {
    constructor() { this.entries = new Map(); }
    async match(request) { return this.entries.get(typeof request === 'string' ? request : request.url)?.clone(); }
    async put(request, response) {
        const url = typeof request === 'string' ? request : request.url;
        this.entries.delete(url);   // a put moves the entry to the end, as in a browser
        this.entries.set(url, response);
    }
    async keys() { return [...this.entries.keys()].map(url => new Request(url)); }
    async delete(request) { return this.entries.delete(typeof request === 'string' ? request : request.url); }
}

class FakeCaches {
    constructor() { this.stores = new Map(); }
    async open(name) {
        if (!this.stores.has(name)) this.stores.set(name, new FakeCache());
        return this.stores.get(name);
    }
    get data() { return this.stores.get(sw.DATA_CACHE) ?? new FakeCache(); }
}

/** A network that answers what it is told, remembers what it was asked and can go away. */
function network(routes = {}) {
    const net = {
        calls: [],
        offline: false,
        routes,
        async fetch(request, init) {
            net.calls.push({ url: request.url, init });
            if (net.offline) throw new TypeError('Failed to fetch');
            const answer = net.routes[request.url];
            return answer ? answer() : new Response('not here', { status: 404 });
        },
    };
    return net;
}

const ok = (body, headers = JSON_TYPE) => () => new Response(body, { status: 200, headers });
const get = (path, headers = {}) => new Request(SCOPE + path, { headers });
const setup = routes => { const caches = new FakeCaches(); const net = network(routes); return { caches, net, deps: { caches, fetch: net.fetch.bind(net) } }; };

// ---- which files are the data package -----------------------------------------------------------------------------------------

test('the three kinds of file of the data package are told apart', () => {
    assert.equal(sw.classify(SCOPE + 'data-version.json', SCOPE), 'version');
    assert.equal(sw.classify(SCOPE + 'data-bundle.json', SCOPE), 'bundle');
    assert.equal(sw.classify(SCOPE + 'data-bundle.json?v=abc123', SCOPE), 'bundle');
    assert.equal(sw.classify(SCOPE + 'data-version.json?t=1', SCOPE), 'version');
    assert.equal(sw.classify(SCOPE + 'images/gao-200.webp', SCOPE), 'image');
    assert.equal(sw.classify(SCOPE + 'images/sub/x.webp', SCOPE), 'image');
});

test('everything else is the app', () => {
    for (const path of ['', 'index.html', 'styleguide', 'css/app.css', '_framework/blazor.webassembly.js', 'data-bundle.json.map',
        'data-bundle.jsonx', 'xdata-version.json', 'images', 'images/', 'imagesfoo/x.webp', 'sub/data-bundle.json', 'service-worker.data.js']) {
        assert.equal(sw.classify(SCOPE + path, SCOPE), null, path);
    }
});

test('a file of another site, or of another place on the same site, is not ours', () => {
    assert.equal(sw.classify('https://other.test/data-bundle.json', SCOPE), null);
    assert.equal(sw.classify('http://mamcom.test/data-bundle.json', SCOPE), null);
    assert.equal(sw.classify('https://mamcom.test/other/data-bundle.json', 'https://mamcom.test/app/'), null);
});

test('an app that lives under a folder finds its data under the same folder', () => {
    assert.equal(sw.classify('https://mamcom.test/app/data-bundle.json?v=1', 'https://mamcom.test/app/'), 'bundle');
    assert.equal(sw.classify('https://mamcom.test/app/images/a.webp', 'https://mamcom.test/app/'), 'image');
});

test('the data package is kept out of the precache of the app and the app is not', () => {
    for (const url of ['data-bundle.json', 'data-version.json', 'images/gao-200.webp']) assert.equal(sw.belongsToApp(url), false, url);
    for (const url of ['css/app.css', 'index.html', 'icon-192.png', '_framework/dotnet.native.wasm', '_content/MamCom.UI/icons/icons.svg', 'service-worker.data.js']) {
        assert.equal(sw.belongsToApp(url), true, url);
    }
});

test('the names are the ones MamCom.Core says', () => {
    assert.deepEqual(sw.FILES, { bundle: 'data-bundle.json', version: 'data-version.json', images: 'images/' });
});

// ---- the version file: network first ------------------------------------------------------------------------------------------

test('the version file is asked for fresh, answered from the network and kept', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'data-version.json']: ok('{"version":"a","isRelease":false}') });

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(await response.text(), '{"version":"a","isRelease":false}');
    assert.equal(net.calls.length, 1);
    assert.deepEqual(net.calls[0].init, { cache: 'no-store' });
    assert.equal(await (await caches.data.match(SCOPE + 'data-version.json')).text(), '{"version":"a","isRelease":false}');
});

test('online the version file is never served from the copy', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'data-version.json']: ok('{"version":"new"}') });
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'data-version.json', new Response('{"version":"old"}', { headers: JSON_TYPE }));

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(await response.text(), '{"version":"new"}');
    assert.equal(await (await caches.data.match(SCOPE + 'data-version.json')).text(), '{"version":"new"}');
    assert.equal(net.calls.length, 1);
});

test('offline the copy of the version file answers', async () => {
    const { caches, net, deps } = setup();
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'data-version.json', new Response('{"version":"kept"}', { headers: JSON_TYPE }));
    net.offline = true;

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(await response.text(), '{"version":"kept"}');
});

test('offline with no copy the version file is a network error, which the app understands as "no connection"', async () => {
    const { net, deps } = setup();
    net.offline = true;

    await assert.rejects(sw.handle('version', get('data-version.json'), deps), TypeError);
});

test('a server in trouble does not replace the copy and the copy answers', async () => {
    const { caches, deps } = setup({ [SCOPE + 'data-version.json']: () => new Response('oops', { status: 503 }) });
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'data-version.json', new Response('{"version":"kept"}', { headers: JSON_TYPE }));

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(await response.text(), '{"version":"kept"}');
    assert.equal(await (await caches.data.match(SCOPE + 'data-version.json')).text(), '{"version":"kept"}');
});

test('a server in trouble with no copy is passed on as it is', async () => {
    const { deps } = setup({ [SCOPE + 'data-version.json']: () => new Response('oops', { status: 503 }) });

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(response.status, 503);
});

test('a start page sent in place of the version file is not kept and does not hide the copy', async () => {
    const { caches, deps } = setup({ [SCOPE + 'data-version.json']: ok('<!doctype html>', { 'content-type': 'text/html' }) });
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'data-version.json', new Response('{"version":"kept"}', { headers: JSON_TYPE }));

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(await response.text(), '{"version":"kept"}');
});

test('a start page with no copy is passed on and not kept', async () => {
    const { caches, deps } = setup({ [SCOPE + 'data-version.json']: ok('<!doctype html>', { 'content-type': 'text/html' }) });

    const response = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(response.status, 200);
    assert.equal(await caches.data.match(SCOPE + 'data-version.json'), undefined);
});

// ---- the bundle: cache first --------------------------------------------------------------------------------------------------

test('a bundle that is not on the device is fetched, answered and kept under its own address', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'data-bundle.json?v=a']: ok('{"bundle":"a"}') });

    const response = await sw.handle('bundle', get('data-bundle.json?v=a'), deps);

    assert.equal(await response.text(), '{"bundle":"a"}');
    assert.equal(await (await caches.data.match(SCOPE + 'data-bundle.json?v=a')).text(), '{"bundle":"a"}');
    assert.equal(net.calls.length, 1);
});

test('a bundle that the device has is answered from the cache with no network at all', async () => {
    const { net, deps } = setup({ [SCOPE + 'data-bundle.json?v=a']: ok('{"bundle":"a"}') });
    await sw.handle('bundle', get('data-bundle.json?v=a'), deps);
    net.calls.length = 0;
    net.offline = true;

    const response = await sw.handle('bundle', get('data-bundle.json?v=a'), deps);

    assert.equal(await response.text(), '{"bundle":"a"}');
    assert.equal(net.calls.length, 0);
});

test('every version is an address of its own', async () => {
    const { caches, deps } = setup({
        [SCOPE + 'data-bundle.json?v=a']: ok('{"bundle":"a"}'),
        [SCOPE + 'data-bundle.json?v=b']: ok('{"bundle":"b"}'),
    });

    const a = await sw.handle('bundle', get('data-bundle.json?v=a'), deps);
    const b = await sw.handle('bundle', get('data-bundle.json?v=b'), deps);

    assert.equal(await a.text(), '{"bundle":"a"}');
    assert.equal(await b.text(), '{"bundle":"b"}');
    assert.equal((await caches.data.keys()).length, 2);
});

test('only the two newest versions are kept', async () => {
    const routes = Object.fromEntries(['a', 'b', 'c', 'd'].map(v => [SCOPE + `data-bundle.json?v=${v}`, ok(`{"bundle":"${v}"}`)]));
    const { caches, deps } = setup(routes);

    for (const v of ['a', 'b', 'c', 'd']) await sw.handle('bundle', get(`data-bundle.json?v=${v}`), deps);

    const kept = (await caches.data.keys()).map(key => new URL(key.url).search);
    assert.deepEqual(kept, ['?v=c', '?v=d']);
    assert.equal(sw.KEEP_BUNDLES, 2);
});

test('dropping old bundles leaves the other files of the data package alone', async () => {
    const routes = {
        [SCOPE + 'data-version.json']: ok('{"version":"x"}'),
        [SCOPE + 'images/a.webp']: ok('img', IMAGE_TYPE),
        ...Object.fromEntries(['a', 'b', 'c'].map(v => [SCOPE + `data-bundle.json?v=${v}`, ok(`{"bundle":"${v}"}`)])),
    };
    const { caches, deps } = setup(routes);
    await sw.handle('version', get('data-version.json'), deps);
    await sw.handle('image', get('images/a.webp'), deps);

    for (const v of ['a', 'b', 'c']) await sw.handle('bundle', get(`data-bundle.json?v=${v}`), deps);

    const kept = (await caches.data.keys()).map(key => key.url.replace(SCOPE, ''));
    assert.deepEqual(kept.sort(), ['data-bundle.json?v=b', 'data-bundle.json?v=c', 'data-version.json', 'images/a.webp']);
});

test('a bundle that cannot be had and is not on the device is a network error', async () => {
    const { net, deps } = setup();
    net.offline = true;

    await assert.rejects(sw.handle('bundle', get('data-bundle.json?v=a'), deps), TypeError);
});

test('a bundle that the server does not have, or that a start page stands in for, is passed on and never kept', async () => {
    const { caches, deps } = setup({
        [SCOPE + 'data-bundle.json?v=gone']: () => new Response('no', { status: 404 }),
        [SCOPE + 'data-bundle.json?v=page']: ok('<!doctype html>', { 'content-type': 'text/html' }),
        [SCOPE + 'data-bundle.json?v=broken']: () => new Response('oops', { status: 500, headers: JSON_TYPE }),
    });

    assert.equal((await sw.handle('bundle', get('data-bundle.json?v=gone'), deps)).status, 404);
    assert.equal((await sw.handle('bundle', get('data-bundle.json?v=page'), deps)).status, 200);
    assert.equal((await sw.handle('bundle', get('data-bundle.json?v=broken'), deps)).status, 500);

    assert.equal((await caches.data.keys()).length, 0);
});

// ---- pictures: stale while revalidate -----------------------------------------------------------------------------------------

test('a picture that is not on the device is fetched, answered and kept', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'images/a.webp']: ok('picture-1', IMAGE_TYPE) });

    const response = await sw.handle('image', get('images/a.webp'), deps, () => {});

    assert.equal(await response.text(), 'picture-1');
    assert.equal(await (await caches.data.match(SCOPE + 'images/a.webp')).text(), 'picture-1');
    assert.equal(net.calls.length, 1);
});

test('a picture on the device answers at once and the network renews it for next time', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'images/a.webp']: ok('picture-2', IMAGE_TYPE) });
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'images/a.webp', new Response('picture-1', { headers: IMAGE_TYPE }));
    const work = [];

    const response = await sw.handle('image', get('images/a.webp'), deps, p => work.push(p));

    assert.equal(await response.text(), 'picture-1');   // the one on hand
    await Promise.all(work);
    assert.equal(net.calls.length, 1);
    assert.equal(await (await caches.data.match(SCOPE + 'images/a.webp')).text(), 'picture-2');   // and the next time, the new one
});

test('offline a picture on the device still answers and the failed renewal is not an error', async () => {
    const { caches, net, deps } = setup();
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'images/a.webp', new Response('picture-1', { headers: IMAGE_TYPE }));
    net.offline = true;
    const work = [];

    const response = await sw.handle('image', get('images/a.webp'), deps, p => work.push(p));

    assert.equal(await response.text(), 'picture-1');
    await Promise.all(work);   // does not reject
});

test('offline a picture that is not on the device is a network error', async () => {
    const { net, deps } = setup();
    net.offline = true;

    await assert.rejects(sw.handle('image', get('images/a.webp'), deps, () => {}), TypeError);
});

test('a request that only fills the cache does not renew a picture the device already has', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'images/a.webp']: ok('picture-2', IMAGE_TYPE) });
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'images/a.webp', new Response('picture-1', { headers: IMAGE_TYPE }));

    const response = await sw.handle('image', get('images/a.webp', { [sw.PRELOAD_HEADER]: '1' }), deps, () => assert.fail('no work was expected'));

    assert.equal(await response.text(), 'picture-1');
    assert.equal(net.calls.length, 0);
});

test('a request that only fills the cache does fetch a picture the device does not have', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'images/a.webp']: ok('picture-1', IMAGE_TYPE) });

    await sw.handle('image', get('images/a.webp', { [sw.PRELOAD_HEADER]: '1' }), deps, () => {});

    assert.equal(net.calls.length, 1);
    assert.equal(await (await caches.data.match(SCOPE + 'images/a.webp')).text(), 'picture-1');
});

test('only a good picture is kept: not a 404, not a start page, not a server error', async () => {
    const { caches, deps } = setup({
        [SCOPE + 'images/gone.webp']: () => new Response('no', { status: 404, headers: IMAGE_TYPE }),
        [SCOPE + 'images/page.webp']: ok('<!doctype html>', { 'content-type': 'text/html' }),
        [SCOPE + 'images/broken.webp']: () => new Response('oops', { status: 500, headers: IMAGE_TYPE }),
    });

    for (const name of ['gone', 'page', 'broken']) await sw.handle('image', get(`images/${name}.webp`), deps, () => {});

    assert.equal((await caches.data.keys()).length, 0);
});

test('a renewal that brings a bad answer does not replace the picture on the device', async () => {
    const { caches, deps } = setup({ [SCOPE + 'images/a.webp']: () => new Response('no', { status: 404 }) });
    await (await caches.open(sw.DATA_CACHE)).put(SCOPE + 'images/a.webp', new Response('picture-1', { headers: IMAGE_TYPE }));
    const work = [];

    await sw.handle('image', get('images/a.webp'), deps, p => work.push(p));
    await Promise.all(work);

    assert.equal(await (await caches.data.match(SCOPE + 'images/a.webp')).text(), 'picture-1');
});

test('asking to handle something that is not a file of the data package is a mistake', () => {
    const { deps } = setup();

    assert.throws(() => sw.handle('page', get('index.html'), deps), /Not a file of the data package/);
});

// ---- warming the cache when the worker is installed ----------------------------------------------------------------------------

const versionRoute = version => ok(JSON.stringify({ version, isRelease: false }));

test('installing brings the bundle of the version the server has onto the device', async () => {
    const { caches, net, deps } = setup({
        [SCOPE + 'data-version.json']: versionRoute('v7'),
        [SCOPE + 'data-bundle.json?v=v7']: ok('{"version":"v7"}'),
    });

    const warmed = await sw.warm(deps, SCOPE);

    assert.equal(warmed, 'v7');
    assert.deepEqual([...caches.data.entries.keys()].sort(), [SCOPE + 'data-bundle.json?v=v7', SCOPE + 'data-version.json']);
    assert.equal(net.calls[0].init.cache, 'no-store');   // the version is asked for fresh, as everywhere
});

test('later the app finds that bundle on the device with no network', async () => {
    const { net, deps } = setup({
        [SCOPE + 'data-version.json']: versionRoute('v7'),
        [SCOPE + 'data-bundle.json?v=v7']: ok('{"version":"v7"}'),
    });
    await sw.warm(deps, SCOPE);
    net.offline = true;

    const bundle = await sw.handle('bundle', get('data-bundle.json?v=v7'), deps);
    const version = await sw.handle('version', get('data-version.json'), deps);

    assert.equal(await bundle.text(), '{"version":"v7"}');
    assert.equal((await version.json()).version, 'v7');
});

test('a bundle that is already on the device is not fetched again', async () => {
    const { net, deps } = setup({
        [SCOPE + 'data-version.json']: versionRoute('v7'),
        [SCOPE + 'data-bundle.json?v=v7']: ok('{"version":"v7"}'),
    });
    await sw.warm(deps, SCOPE);
    net.calls.length = 0;

    assert.equal(await sw.warm(deps, SCOPE), 'v7');

    assert.deepEqual(net.calls.map(c => c.url), [SCOPE + 'data-version.json']);
});

test('a new version of the package brings the new bundle and keeps the older one beside it', async () => {
    const { caches, net, deps } = setup({
        [SCOPE + 'data-version.json']: versionRoute('v1'),
        [SCOPE + 'data-bundle.json?v=v1']: ok('1'),
        [SCOPE + 'data-bundle.json?v=v2']: ok('2'),
    });
    await sw.warm(deps, SCOPE);
    net.routes[SCOPE + 'data-version.json'] = versionRoute('v2');

    assert.equal(await sw.warm(deps, SCOPE), 'v2');

    assert.ok(await caches.data.match(SCOPE + 'data-bundle.json?v=v1'));
    assert.ok(await caches.data.match(SCOPE + 'data-bundle.json?v=v2'));
});

test('offline there is nothing to warm and installing is not disturbed', async () => {
    const { caches, net, deps } = setup({});
    net.offline = true;

    assert.equal(await sw.warm(deps, SCOPE), null);

    assert.equal(caches.data.entries.size, 0);
});

test('with no package published there is nothing to warm and nothing is kept', async () => {
    const { caches, deps } = setup({});   // everything is a 404

    assert.equal(await sw.warm(deps, SCOPE), null);

    assert.equal(caches.data.entries.size, 0);
});

test('a start page sent in place of the version file warms nothing and is not kept', async () => {
    const { caches, net, deps } = setup({ [SCOPE + 'data-version.json']: ok('<html></html>', { 'content-type': 'text/html' }) });

    assert.equal(await sw.warm(deps, SCOPE), null);

    assert.equal(caches.data.entries.size, 0);
    assert.equal(net.calls.length, 1);
});

test('a version file that does not name a version warms nothing and no bundle is asked for', async () => {
    for (const body of ['not json', '{}', '{"version":""}', '{"version":7}', 'null']) {
        const { net, deps } = setup({ [SCOPE + 'data-version.json']: ok(body) });

        assert.equal(await sw.warm(deps, SCOPE), null, body);

        assert.equal(net.calls.length, 1, body);
    }
});

test('an error answer that happens to carry a version is not believed', async () => {
    const { net, deps } = setup({
        [SCOPE + 'data-version.json']: () => new Response(JSON.stringify({ version: 'v7' }), { status: 503, headers: JSON_TYPE }),
        [SCOPE + 'data-bundle.json?v=v7']: ok('{}'),
    });

    assert.equal(await sw.warm(deps, SCOPE), null);

    assert.equal(net.calls.length, 1);   // the bundle was not asked for
});

test('a version is put into the address safely', async () => {
    const { net, deps } = setup({
        [SCOPE + 'data-version.json']: versionRoute('a b/c'),
        [SCOPE + 'data-bundle.json?v=a%20b%2Fc']: ok('{}'),
    });

    assert.equal(await sw.warm(deps, SCOPE), 'a b/c');

    assert.equal(net.calls[1].url, SCOPE + 'data-bundle.json?v=a%20b%2Fc');
});

test('a version without its bundle on the server warms nothing and the bundle is not kept', async () => {
    const { caches, deps } = setup({ [SCOPE + 'data-version.json']: versionRoute('v7') });   // the bundle is a 404

    assert.equal(await sw.warm(deps, SCOPE), null);

    assert.equal(await caches.data.match(SCOPE + 'data-bundle.json?v=v7'), undefined);
});

test('a server that does not answer cannot hold the installation for longer than the limit', async () => {
    const { deps } = setup({});
    deps.fetch = () => new Promise(() => { /* never answers */ });

    const started = Date.now();
    assert.equal(await sw.warm(deps, SCOPE, 30), null);

    // Not a tight bound: on a busy machine a 30 ms timer can fire seconds late. It only has to be far from the real limit, so a warm that
    // waited for the network (which never answers here) or for the default limit would still fail.
    assert.ok(Date.now() - started < sw.WARM_TIMEOUT_MS / 2);
    assert.ok(sw.WARM_TIMEOUT_MS >= 5000 && sw.WARM_TIMEOUT_MS <= 60000);
});

test('a cache that cannot be opened does not make the warm fail', async () => {
    const { deps } = setup({ [SCOPE + 'data-version.json']: versionRoute('v7') });
    deps.caches = { open: async () => { throw new Error('storage is full'); } };

    assert.equal(await sw.warm(deps, SCOPE), null);
});

test('an app that lives under a folder warms the data under the same folder', async () => {
    const scope = 'https://mamcom.test/app/';
    const { net, deps } = setup({
        [scope + 'data-version.json']: versionRoute('v7'),
        [scope + 'data-bundle.json?v=v7']: ok('{}'),
    });

    assert.equal(await sw.warm(deps, scope), 'v7');

    assert.deepEqual(net.calls.map(c => c.url), [scope + 'data-version.json', scope + 'data-bundle.json?v=v7']);
});
