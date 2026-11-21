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

// ---------------------------------------------------------------- AYARLAR
const PORT = Number(process.env.PORT) || 3000;
const DEFAULT_SCENARIO = process.env.SCENARIO || 'halay';
const AUTO_USERNAME = (process.env.TIKTOK_USERNAME || process.argv[2] || '').replace(/^@/, '');

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
const pick = (...vals) => vals.find((v) => v !== undefined && v !== null && v !== '');

function normUser(raw = {}) {
  const u = raw.user || {};
  const urls = pick(u.avatarLarge?.urlList, u.avatarMedium?.urlList, u.avatarThumb?.urlList, u.profilePicture?.urls, []) || [];
  const bestUrl = urls.find((x) => /100x100/.test(x) && /\.(jpeg|jpg|webp)/.test(x)) || urls.find((x) => !/shrink/.test(x)) || urls[0];
  const uniqueId = String(pick(raw.uniqueId, u.displayId, u.uniqueId, u.nickname, 'misafir'));
  const avatar = pick(raw.profilePictureUrl, bestUrl);
  return {
    id: String(pick(raw.userId, u.idStr, u.userId, uniqueId)),
    uniqueId,
    nickname: String(pick(raw.nickname, u.nickname, uniqueId)),
    avatar: avatar
      ? `/avatar/proxy?url=${encodeURIComponent(avatar)}`
      : `/avatar/fake/${encodeURIComponent(uniqueId)}.svg`,
    isModerator: !!raw.isModerator,
    isSubscriber: !!raw.isSubscriber,
  };
}

// Hediye serisi (streak) takibi: seri sürerken her güncellemede sadece FARKI gönderir
const streaks = new Map(); // "userId:giftId" -> son repeatCount
function normGift(raw) {
  const d = raw.giftDetails || {};
  const giftType = pick(d.giftType, raw.giftType, 0);
  const repeatCount = Number(pick(raw.repeatCount, 1));
  const repeatEnd = !!raw.repeatEnd;
  const user = normUser(raw);
  const giftId = String(pick(raw.giftId, d.id, d.giftName, raw.giftName));
  let count = 1;
  if (giftType === 1) {
    const key = `${user.id}:${giftId}`;
    const last = streaks.get(key) || 0;
    count = Math.max(0, repeatCount - last);
    if (repeatEnd) streaks.delete(key);
    else streaks.set(key, repeatCount);
  } else {
    count = Math.max(1, repeatCount);
  }
  if (count <= 0) return null;
  const diamondEach = Number(pick(d.diamondCount, raw.diamondCount, 1));
  return {
    type: 'gift',
    user,
    gift: {
      id: giftId,
      name: String(pick(d.giftName, raw.giftName, 'Hediye')),
      diamonds: diamondEach * count,
      diamondEach,
      count,
      image: pick(d.giftImage?.url?.[0], d.icon?.urlList?.[0], raw.giftPictureUrl, ''),
    },
  };
}

function normalize(type, raw) {
  switch (type) {
    case 'gift': return normGift(raw);
    case 'like':
      return { type: 'like', user: normUser(raw), count: Number(pick(raw.likeCount, 1)), total: Number(pick(raw.totalLikeCount, 0)) };
    case 'chat':
      return { type: 'comment', user: normUser(raw), text: String(pick(raw.comment, raw.content, '')) };
    case 'follow': return { type: 'follow', user: normUser(raw) };
    case 'share': return { type: 'share', user: normUser(raw) };
    case 'member': return { type: 'join', user: normUser(raw) };
    default: return null;
  }
}

function broadcastEvent(ev) {
  if (!ev) return;
  ev.ts = Date.now();
  io.emit('event', ev);
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
    if (ev.type === 'gift') {
      ev.gift = Object.assign({ id: 'test', name: 'Rose', diamonds: 1, diamondEach: 1, count: 1, image: '' }, ev.gift);
    }
    if (ev.user && !ev.user.avatar) ev.user.avatar = `/avatar/fake/${encodeURIComponent(ev.user.uniqueId || 'x')}.svg`;
    broadcastEvent(ev);
  });

  socket.on('admin:setScenario', (id) => {
    if (!scenarios[id]) return;
    activeScenarioId = id;
    io.emit('scenario:change', id);
  });

  socket.on('admin:reloadScenarios', () => {
    scenarios = scenarioLoader.loadAll();
    if (!scenarios[activeScenarioId]) activeScenarioId = Object.keys(scenarios)[0];
    io.emit('hello', {
      active: activeScenarioId,
      scenarios: Object.values(scenarios).map(({ id, name, description }) => ({ id, name, description })),
      live: { status: live.status, username: live.username, message: live.message },
    });
    io.emit('scenario:change', activeScenarioId);
  });

  socket.on('admin:reset', () => io.emit('scene:reset'));
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
  if (AUTO_USERNAME) connectTikTok(AUTO_USERNAME);
});

process.on('unhandledRejection', (e) => console.error('[unhandledRejection]', e && e.message ? e.message : e));
process.on('SIGINT', async () => { await disconnectTikTok(true); process.exit(0); });
