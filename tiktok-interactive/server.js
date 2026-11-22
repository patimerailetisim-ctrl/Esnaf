'use strict';
/**
 * TikTok Live İnteraktif Sahne - SUNUCU
 * -------------------------------------
 *  - TikTok canlı yayınına bağlanır (tiktok-live-connector)
 *  - Olayları (hediye / beğeni / yorum / takip / paylaşım / katılım) tek tip formata çevirir
 *  - Socket.io ile tüm sahnelere (OBS + tarayıcı) anında iletir
 *  - Test panelinden gelen sahte olayları da aynı hat üzerinden yayar
 *  - public/ klasörünü ve senaryoları sunar
 *
 *  Çalıştırma:  npm start        (veya)   TIKTOK_USERNAME=kullaniciadi npm start
 */
const path = require('path');
const http = require('http');
const express = require('express');
const { Server } = require('socket.io');
const scenarioLoader = require('./scenarios');
const { createNormalizer } = require('./lib/events');
const games = require('./games');

// ---------------------------------------------------------------- AYARLAR
const PORT = Number(process.env.PORT) || 3000;
const DEFAULT_SCENARIO = process.env.SCENARIO || 'halay';
const AUTO_USERNAME = (process.env.TIKTOK_USERNAME || process.argv[2] || '').replace(/^@/, '');
const DATA_DIR = process.env.DATA_DIR ? path.resolve(process.env.DATA_DIR) : path.join(__dirname, 'data'); // kalıcı oyun kayıtları
const ALLOW_REMOTE_ADMIN = process.env.ALLOW_REMOTE_ADMIN === '1'; // oyun verisini değiştiren işlemler varsayılan olarak sadece bu bilgisayardan

// ---------------------------------------------------------------- DURUM
let scenarios = scenarioLoader.loadAll();
let activeScenarioId = scenarios[DEFAULT_SCENARIO] ? DEFAULT_SCENARIO : Object.keys(scenarios)[0];

const live = {
  status: 'idle', // idle | connecting | connected | error
  username: '',
  message: 'Bağlı değil (test modu)',
  connection: null,
};

// ---------------------------------------------------------------- EXPRESS
const app = express();
const server = http.createServer(app);
const io = new Server(server, { cors: { origin: '*' } });

// ---------------------------------------------------------------- OYUN MODÜLLERİ (yalnızca `game` bölümü olan senaryolar)
// Halay ve Uzay gibi `game` içermeyen senaryolar bu katmandan hiç etkilenmez.
const gameInstances = new Map(); // senaryo id -> oyun örneği (veya hatalıysa null)

function getGame(id) {
  const sc = scenarios[id];
  if (!sc || !sc.game) return null;
  if (!gameInstances.has(id)) {
    try {
      gameInstances.set(id, games.create(sc, {
        dataDir: DATA_DIR,
        emitFx: (type, payload) => io.emit('game:fx', Object.assign({ scenario: id, type }, payload)),
        emitState: (snap) => io.emit('game:state', snap),
      }));
    } catch (err) {
      console.error(`[oyun] "${id}" başlatılamadı: ${err.message}`);
      gameInstances.set(id, null);
    }
  }
  return gameInstances.get(id);
}

/** Aktif senaryonun oyununu çalıştırır (ekonomi tiki vb.), diğerlerini durdurup kaydeder. */
function activateGame() {
  for (const [id, g] of gameInstances) if (g && id !== activeScenarioId) g.stop();
  const g = getGame(activeScenarioId);
  if (g) g.start();
}

function stopAllGames() {
  for (const g of gameInstances.values()) if (g) g.stop();
}

const isLocalSocket = (socket) => {
  const a = String((socket.handshake && socket.handshake.address) || '');
  return a === '::1' || a === '127.0.0.1' || a.endsWith('127.0.0.1');
};

app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));
// Senaryoda yolu verilmiş ama henüz eklenmemiş ses dosyaları için 404 yerine boş cevap ver
// (tarayıcı konsolunu kirletmez; motor otomatik olarak yerleşik sese geçer).
app.use('/assets', (req, res, next) => (/\.(mp3|wav|ogg|m4a)$/i.test(req.path) ? res.status(204).end() : next()));
// Senaryo dosyalarındaki asset yolları (assets/...) public/ içinden sunulur.

app.get('/admin', (_req, res) => res.redirect('/?panel=1'));

app.get('/api/scenarios', (_req, res) => {
  res.json({ active: activeScenarioId, scenarios: Object.values(scenarios).map(({ id, name, description }) => ({ id, name, description })) });
});

app.get('/api/scenario/:id', (req, res) => {
  const sc = scenarios[req.params.id];
  if (!sc) return res.status(404).json({ error: 'Senaryo bulunamadı' });
  res.json(sc);
});

// --- Sahte avatar üretici (test için, internet gerektirmez) ---
app.get('/avatar/fake/:seed.svg', (req, res) => {
  const seed = String(req.params.seed || 'x');
  let h = 0;
  for (const ch of seed) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  const hue = h % 360;
  const letter = (seed.replace(/[^A-Za-z0-9ÇĞİÖŞÜçğıöşü]/g, '')[0] || '?').toUpperCase();
  res.type('image/svg+xml').send(
    `<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
      <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stop-color="hsl(${hue},80%,62%)"/><stop offset="1" stop-color="hsl(${(hue + 50) % 360},75%,42%)"/>
      </linearGradient></defs>
      <rect width="200" height="200" fill="url(#g)"/>
      <text x="100" y="132" font-family="Arial,Helvetica,sans-serif" font-size="100" font-weight="700" fill="#fff" text-anchor="middle">${letter}</text>
    </svg>`
  );
});

// --- Profil fotoğrafı proxy'si: TikTok CDN bazen doğrudan yüklemeyi engeller / süresi dolar ---
const AVATAR_HOSTS = [/tiktokcdn[\w-]*\.com$/i, /tiktok\.com$/i, /ibyteimg\.com$/i, /byteoversea\.com$/i, /muscdn\.com$/i, /tiktokv\.com$/i];
const avatarCache = new Map(); // url -> {buf, type, at}
app.get('/avatar/proxy', async (req, res) => {
  try {
    const url = new URL(String(req.query.url || ''));
    if (url.protocol !== 'https:' || !AVATAR_HOSTS.some((re) => re.test(url.hostname))) {
      return res.status(400).send('izin verilmeyen adres');
    }
    const key = url.toString();
    let item = avatarCache.get(key);
    if (!item) {
      const r = await fetch(key, { headers: { 'user-agent': 'Mozilla/5.0', referer: 'https://www.tiktok.com/' } });
      if (!r.ok) throw new Error('HTTP ' + r.status);
      item = { buf: Buffer.from(await r.arrayBuffer()), type: r.headers.get('content-type') || 'image/jpeg', at: Date.now() };
      if (avatarCache.size > 500) avatarCache.delete(avatarCache.keys().next().value);
      avatarCache.set(key, item);
    }
    res.set('Cache-Control', 'public, max-age=3600').type(item.type).send(item.buf);
  } catch (_e) {
    res.status(502).send('avatar alınamadı');
  }
});

// ---------------------------------------------------------------- OLAY NORMALİZASYONU
// (lib/events.js: hediye combo çift sayımını önleyen seri takibi dahil)
const { normalize } = createNormalizer();

function broadcastEvent(ev) {
  if (!ev) return;
  ev.ts = Date.now();
  io.emit('event', ev);
  // Aktif senaryo bir oyun içeriyorsa olayı oyun mantığına da ver (rütbe, puan, taht...)
  const g = getGame(activeScenarioId);
  if (g) g.handleEvent(ev);
}

// ---------------------------------------------------------------- TİKTOK BAĞLANTISI
function setLive(patch) {
  Object.assign(live, patch);
  io.emit('live:status', { status: live.status, username: live.username, message: live.message });
}

async function connectTikTok(username) {
  username = String(username || '').trim().replace(/^@/, '');
  if (!username) return setLive({ status: 'error', message: 'Kullanıcı adı boş.' });

  await disconnectTikTok(true);
  setLive({ status: 'connecting', username, message: `@${username} yayınına bağlanılıyor...` });

  let lib;
  try {
    lib = await import('tiktok-live-connector'); // paket ESM olduğu için dynamic import
  } catch (err) {
    return setLive({ status: 'error', message: 'Kütüphane yüklenemedi: ' + err.message });
  }
  const { TikTokLiveConnection, WebcastEvent, ControlEvent } = lib;

  try {
    const conn = new TikTokLiveConnection(username);
    live.connection = conn;

    conn.on(WebcastEvent.GIFT, (d) => broadcastEvent(normalize('gift', d)));
    conn.on(WebcastEvent.LIKE, (d) => broadcastEvent(normalize('like', d)));
    conn.on(WebcastEvent.CHAT, (d) => broadcastEvent(normalize('chat', d)));
    conn.on(WebcastEvent.FOLLOW, (d) => broadcastEvent(normalize('follow', d)));
    conn.on(WebcastEvent.SHARE, (d) => broadcastEvent(normalize('share', d)));
    conn.on(WebcastEvent.MEMBER, (d) => broadcastEvent(normalize('member', d)));
    conn.on(WebcastEvent.STREAM_END, () => setLive({ status: 'idle', message: 'Yayın sona erdi.', connection: null }));
    conn.on(ControlEvent.DISCONNECTED, () => {
      if (live.connection === conn) setLive({ status: 'idle', message: 'Bağlantı koptu.', connection: null });
    });
    conn.on(ControlEvent.ERROR, (err) => console.error('[tiktok] hata:', err?.message || err));

    const state = await conn.connect();
    setLive({ status: 'connected', message: `@${username} yayınına bağlı (oda: ${state?.roomId ?? '?'})` });
    const g = getGame(activeScenarioId);
    if (g && g.intro) g.intro(); // yayın açıldı: 10 sn'lik katılım + hediye bilgilendirmesi
    console.log(`[tiktok] @${username} yayınına bağlandı.`);
  } catch (err) {
    live.connection = null;
    const msg = (err && err.message) || String(err);
    console.error('[tiktok] bağlantı hatası:', msg);
    setLive({ status: 'error', message: 'Bağlanılamadı: ' + msg + ' (Kullanıcı adı doğru mu? Yayın açık mı?)' });
  }
}

async function disconnectTikTok(silent = false) {
  const conn = live.connection;
  live.connection = null;
  if (conn) {
    try { await conn.disconnect(); } catch (_e) { /* yoksay */ }
  }
  if (!silent) setLive({ status: 'idle', message: 'Bağlı değil (test modu)' });
}

// ---------------------------------------------------------------- SOCKET.IO
io.on('connection', (socket) => {
  socket.emit('hello', {
    active: activeScenarioId,
    scenarios: Object.values(scenarios).map(({ id, name, description }) => ({ id, name, description })),
    live: { status: live.status, username: live.username, message: live.message },
  });

  // Test panelinden sahte olay -> gerçek olaylarla aynı hattan herkese
  socket.on('test:event', (ev) => {
    if (!ev || typeof ev !== 'object' || !ev.type) return;
    // Kalıcı oyun verisi söz konusuysa sahte olayları yalnızca bu bilgisayardan kabul et
    if (!ALLOW_REMOTE_ADMIN && !isLocalSocket(socket) && getGame(activeScenarioId)) return;
    if (ev.type === 'gift') {
      ev.gift = Object.assign({ id: 'test', name: 'Rose', diamonds: 1, diamondEach: 1, count: 1, image: '' }, ev.gift);
    }
    if (ev.user && !ev.user.avatar) ev.user.avatar = `/avatar/fake/${encodeURIComponent(ev.user.uniqueId || 'x')}.svg`;
    broadcastEvent(ev);
  });

  socket.on('admin:setScenario', (id) => {
    if (!scenarios[id]) return;
    activeScenarioId = id;
    activateGame();
    io.emit('scenario:change', id);
  });

  socket.on('admin:reloadScenarios', () => {
    scenarios = scenarioLoader.loadAll();
    if (!scenarios[activeScenarioId]) activeScenarioId = Object.keys(scenarios)[0];
    // Oyun kurallarını yeni config ile yeniden kur (kayıt dosyadan geri yüklenir)
    stopAllGames();
    gameInstances.clear();
    activateGame();
    io.emit('hello', {
      active: activeScenarioId,
      scenarios: Object.values(scenarios).map(({ id, name, description }) => ({ id, name, description })),
      live: { status: live.status, username: live.username, message: live.message },
    });
    io.emit('scenario:change', activeScenarioId);
  });

  // Sahneyi sıfırla: yalnızca GÖRSEL sahne yeniden kurulur; kalıcı oyun verisi (rütbeler, puanlar) silinmez.
  socket.on('admin:reset', () => io.emit('scene:reset'));

  // --- Oyun: istemci kendi senaryosunun güncel durumunu ister
  socket.on('game:sync', (req) => {
    const g = getGame(req && req.scenario);
    if (g) socket.emit('game:state', g.snapshot());
  });

  // --- Oyun yönetimi (test paneli): rütbe ver, taç giydir, taht boşalt, oyuncu sil, tüm veriyi sıfırla
  socket.on('admin:game', (req) => {
    const reply = (res) => socket.emit('game:admin-result', res);
    if (!ALLOW_REMOTE_ADMIN && !isLocalSocket(socket)) return reply({ ok: false, message: 'Bu işlem yalnızca sunucu bilgisayarından yapılabilir.' });
    const g = getGame(activeScenarioId);
    if (!g) return reply({ ok: false, message: 'Aktif senaryoda oyun yok.' });
    if (!req || typeof req.action !== 'string') return reply({ ok: false, message: 'Geçersiz istek.' });
    const res = g.admin(req.action, req.payload || {});
    if (req.action === 'wipe' && res.ok) io.emit('game:state', g.snapshot());
    reply(res);
  });
  socket.on('admin:connect', (username) => connectTikTok(username));
  socket.on('admin:disconnect', () => disconnectTikTok());
});

// ---------------------------------------------------------------- BAŞLAT
server.listen(PORT, () => {
  console.log('');
  console.log('  ┌──────────────────────────────────────────────────────────┐');
  console.log('  │  TikTok İnteraktif Sahne çalışıyor                        │');
  console.log('  └──────────────────────────────────────────────────────────┘');
  console.log(`  Test/yönetim (tarayıcıda aç) : http://localhost:${PORT}/?panel=1`);
  console.log(`  OBS Tarayıcı Kaynağı URL'si  : http://localhost:${PORT}/?obs=1`);
  console.log(`  Aktif senaryo                : ${activeScenarioId}`);
  console.log('');
  activateGame();
  if (AUTO_USERNAME) connectTikTok(AUTO_USERNAME);
});

process.on('unhandledRejection', (e) => console.error('[unhandledRejection]', e && e.message ? e.message : e));
const shutdown = async () => { stopAllGames(); await disconnectTikTok(true); process.exit(0); }; // oyun verisini diske yazıp çık
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
