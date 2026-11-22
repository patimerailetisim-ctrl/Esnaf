'use strict';
/**
 * RÜTBE OYUNU (rankgame) - sunucu taraflı oyun modülü
 * ===================================================
 * Bir senaryo dosyasındaki `game: { type: 'rankgame', ... }` bölümünü okuyup çalışan,
 * TAMAMEN yapılandırma ile yönetilen oyun mantığıdır (scenarios/osmanli.js).
 *
 *  PUAN: Her hediye, connector'ın hediye metadata değerinden (giftDetails.diamondCount) puana çevrilir:
 *        puan = değer x points.perGiftValue. Coin/diamond dönüşümü yapılmaz; değer yoksa puan verilmez.
 *  RÜTBE: Yalnızca puanla yükselir (ranks[].minPoints). Padişah (auto:false) puandan bağımsızdır.
 *  TAHT: Her `throne.auction.intervalSec` saniyede bir `durationSec` saniyelik AÇIK ARTIRMA:
 *        hediye puanları kullanıcının teklifine de eklenir (normal puan da kazanılmaya devam eder);
 *        son `extendWindowSec` sn içinde gelen teklif süreyi `extendBySec` uzatır; en yüksek teklif Padişah olur
 *        (eşitlikte son teklifi veren), eski Padişah `demoteTo` (Sadrazam) olur, teklifler sıfırlanır, puanlar ASLA.
 *  KALICILIK: data/<storage>.json (açık artırma durumu ve işlenmiş olay kimlikleri dahil). Yeniden başlatmada
 *        açık artırma kalan süreyle devam eder; süresi dolmuşsa kontrollü biçimde kapatılır.
 *  İDEMPOTENCY: Aynı olay kimliği (ev.id) ikinci kez işlenmez (yeniden başlatma sonrası da).
 *
 * Dışarıya: handleEvent(ev), snapshot(), admin(action, payload), start(), stop(), flush(), auctionTick(), intro()
 * Dışarıdan: opts.emitFx(type, payload), opts.emitState(snapshot), opts.now(), opts.dataDir
 */
const fs = require('fs');
const path = require('path');

const SAVE_VERSION = 2;

// Türkçe karakterleri sadeleştirip küçük harfe çevirir ("!RÜTBE" == "!rutbe")
const norm = (s) => String(s ?? '')
  .toLocaleLowerCase('tr')
  .replace(/ı/g, 'i').replace(/ş/g, 's').replace(/ç/g, 'c').replace(/ğ/g, 'g').replace(/ö/g, 'o').replace(/ü/g, 'u')
  .replace(/\s+/g, ' ')
  .trim();

const fmtNum = (n) => Math.floor(n).toLocaleString('tr-TR');
const r2 = (n) => Math.round(n * 100) / 100;

const BLANK_COUNTERS = () => ({
  comments: 0, follows: 0, likes: 0, shares: 0,           // ayrı sayaçlar
  gifts: 0, giftValue: 0, giftPoints: 0,                  // hediye adedi / toplam değer / toplam puan
  duplicates: 0, rejectedGifts: 0, unknownGifts: 0,       // idempotency ve güvenlik sayaçları
});

class RankGame {
  constructor(scenario, opts = {}) {
    if (!scenario || !scenario.game) throw new Error('Senaryoda "game" bölümü yok');
    this.scenarioId = scenario.id;
    this.cfg = scenario.game;
    this.now = opts.now || Date.now;
    this.emitFx = opts.emitFx || (() => {});
    this.emitState = opts.emitState || (() => {});
    this.autosave = opts.autosave !== false;
    this.debounceMs = opts.debounceMs ?? 200;

    // Eksik bölümler için güvenli varsayılanlar
    const c = this.cfg;
    c.economy = c.economy || {};
    c.economy.effects = c.economy.effects || {};
    c.cooldowns = c.cooldowns || {};
    c.professions = c.professions || {};
    c.commands = Object.assign({ join: ['!katıl'], rank: ['!rütbe'], name: ['!isim'], job: ['!meslek'], army: ['!ordu'], treasury: ['!hazine'], guide: ['!hediyeler'] }, c.commands);
    c.autoJoin = c.autoJoin || {};
    c.points = Object.assign({ perGiftValue: 1 }, c.points);
    c.giftValue = Object.assign({ field: 'diamondCount', maxPerUnit: 1000000 }, c.giftValue);
    c.throne = c.throne || {};
    c.throne.auction = Object.assign({
      enabled: true, intervalSec: 600, durationSec: 60, extendWindowSec: 10, extendBySec: 10, minBidPoints: 0, restartDelaySec: 30, topN: 5,
    }, c.throne.auction);
    c.spam = Object.assign({ commentCooldownSec: c.points.commentCooldownSec ?? 15, duplicateWindowSec: 120, globalPerMinute: 120 }, c.spam);
    c.idempotency = Object.assign({ maxIds: 2000 }, c.idempotency);
    this.au = c.throne.auction;

    this._validate();
    this.ranks = this.cfg.ranks;
    this.rankIndex = new Map(this.ranks.map((r, i) => [r.id, i]));
    this.throneIdx = this.rankIndex.get(this.cfg.throne.rank);
    this.demoteIdx = this.rankIndex.get(this.cfg.throne.demoteTo);

    // komutlar: normalize edilmiş eşleşme tablosu
    this.cmdMap = new Map();
    for (const [name, list] of Object.entries(this.cfg.commands || {})) {
      for (const cmd of [].concat(list)) this.cmdMap.set(norm(cmd), name);
    }
    // meslek takma adları
    this.jobAlias = new Map();
    for (const [id, j] of Object.entries(this.cfg.professions || {})) {
      this.jobAlias.set(norm(id), id);
      for (const n of j.names || []) this.jobAlias.set(norm(n), id);
    }

    this.dir = opts.dataDir || path.join(__dirname, '..', 'data');
    this.file = path.join(this.dir, `${this.cfg.storage || this.scenarioId}.json`);
    this.cd = new Map();       // bekleme süreleri (bellekte)
    this.lastText = new Map(); // yinelenen yorum tespiti (bellekte)
    this._cmtWindow = [];      // genel yorum hız sınırı (bellekte)
    this.state = this._blank();
    this._saveTimer = null;
    this._pushTimer = null;
    this._tick = null;
    this._auTimer = null;
    this._lastBackup = 0;
    this._dirtyState = false;
    this._load();
    this.seenSet = new Set(this.state.seen);
    this._enforceThrone();
    this._initAuction();
  }

  // ---------------------------------------------------------------- doğrulama
  _validate() {
    const c = this.cfg;
    const need = (cond, msg) => { if (!cond) throw new Error(`[${this.scenarioId}] game yapılandırması hatalı: ${msg}`); };
    need(Array.isArray(c.ranks) && c.ranks.length >= 2, 'ranks en az 2 rütbe içermeli');
    const ids = new Set(c.ranks.map((r) => r.id));
    need(ids.size === c.ranks.length, 'rütbe id\'leri tekil olmalı');
    need(c.throne && ids.has(c.throne.rank), 'throne.rank geçerli bir rütbe id\'si olmalı');
    need(ids.has(c.throne.demoteTo), 'throne.demoteTo geçerli bir rütbe id\'si olmalı');
    need(c.ranks.find((r) => r.id === c.throne.rank).auto === false, 'taht rütbesi (throne.rank) auto:false olmalı: Padişahlık puandan bağımsızdır');
    need(c.ranks[0].auto !== false && c.ranks[0].minPoints === 0, 'ilk rütbe (başlangıç) auto olmalı ve minPoints 0 olmalı');
    let last = -1;
    for (const r of c.ranks) {
      if (r.auto === false) continue;
      need(typeof r.minPoints === 'number' && r.minPoints > last, `"${r.id}" rütbesinin minPoints değeri bir öncekinden büyük olmalı`);
      last = r.minPoints;
    }
    need(typeof c.points.perGiftValue === 'number' && c.points.perGiftValue > 0, 'points.perGiftValue pozitif sayı olmalı');
    const a = c.throne.auction;
    for (const k of ['intervalSec', 'durationSec', 'extendWindowSec', 'extendBySec']) need(typeof a[k] === 'number' && a[k] > 0, `throne.auction.${k} pozitif sayı olmalı`);
    for (const [k, j] of Object.entries(c.professions || {})) need(!j.minRank || ids.has(j.minRank), `professions.${k}.minRank bilinmiyor`);
  }

  _blank() {
    const st = (this.cfg.economy && this.cfg.economy.start) || {};
    return {
      version: SAVE_VERSION,
      users: {},
      stats: { treasury: st.treasury ?? 0, population: st.population ?? 0, production: st.production ?? 0, cities: 0, counters: BLANK_COUNTERS() },
      throne: { holderId: null, vizierId: null, crownedAt: 0, crownBid: 0, history: [] },
      auction: { phase: 'idle', id: 0, nextAt: 0, startedAt: 0, endsAt: 0, extensions: 0, seq: 0, bids: {}, last: null },
      seen: [],
      ui: { guideVisible: this.cfg.guide ? this.cfg.guide.visible !== false : false }, // yan rehber açık mı (yayıncı ayarı, kalıcı)
      savedAt: 0,
    };
  }

  // ---------------------------------------------------------------- kalıcı kayıt
  _load() {
    try {
      fs.mkdirSync(this.dir, { recursive: true });
      if (!fs.existsSync(this.file)) return;
      const raw = fs.readFileSync(this.file, 'utf8');
      const data = JSON.parse(raw);
      if (!data || typeof data !== 'object' || !data.users) throw new Error('beklenmeyen dosya biçimi');
      const migrated = (data.version || 1) < SAVE_VERSION;
      if (migrated) this._migrate(data, raw);
      const blank = this._blank();
      this.state = Object.assign(blank, data);
      this.state.stats = Object.assign(blank.stats, data.stats);
      this.state.stats.counters = Object.assign(BLANK_COUNTERS(), data.stats && data.stats.counters);
      this.state.throne = Object.assign(this._blank().throne, data.throne);
      this.state.auction = Object.assign(this._blank().auction, data.auction);
      this.state.ui = Object.assign(this._blank().ui, data.ui);
      this.state.seen = Array.isArray(data.seen) ? data.seen.slice(-this.cfg.idempotency.maxIds) : [];
      this.state.version = SAVE_VERSION;
      // config'te artık olmayan rütbeleri başlangıç rütbesine çek
      for (const u of Object.values(this.state.users)) {
        if (!this.rankIndex.has(u.rank)) u.rank = this.ranks[0].id;
      }
      this._recomputeCities(true);
      if (migrated) this._dirtyState = true;
      console.log(`[oyun:${this.scenarioId}] kayıt yüklendi: ${Object.keys(this.state.users).length} oyuncu${migrated ? ' (eski sürümden taşındı)' : ''}`);
    } catch (err) {
      // Bozuk dosyayı SİLME: kenara al, sıfırdan başla
      try {
        const bad = `${this.file}.bozuk-${Date.now()}`;
        if (fs.existsSync(this.file)) fs.renameSync(this.file, bad);
        console.error(`[oyun:${this.scenarioId}] kayıt okunamadı (${err.message}); dosya "${path.basename(bad)}" olarak saklandı.`);
      } catch (_e) { /* yoksay */ }
      this.state = this._blank();
    }
  }

  /** v1 -> v2: eski hediye eşiği/taç sistemi kalktı. Eski dosyayı yedekler; rütbeleri YENİ puan eşiklerine göre yeniden hesaplar. */
  _migrate(data, raw) {
    try { fs.writeFileSync(`${this.file}.v${data.version || 1}.bak`, raw); } catch (_e) { /* yoksay */ }
    const holder = data.throne && data.throne.holderId;
    const autos = this.cfg.ranks.filter((r) => r.auto !== false);
    for (const u of Object.values(data.users)) {
      u.giftValue = u.giftValue ?? u.diamonds ?? 0;
      u.giftPoints = u.giftPoints ?? 0;
      if (u.id === holder) { u.rank = this.cfg.throne.rank; continue; }
      let r = autos[0];
      for (const a of autos) if ((u.points || 0) >= a.minPoints) r = a;
      u.rank = r.id;
    }
    if (data.throne) { delete data.throne.crownValue; data.throne.crownBid = 0; }
    data.version = SAVE_VERSION;
  }

  /** Diske yazar. Son kayıttan beri değişiklik yoksa atlar (force: zorla yaz). */
  save(force = false) {
    if (!this.autosave) return;
    if (!force && !this._dirtyState) return;
    this._dirtyState = false;
    try {
      fs.mkdirSync(this.dir, { recursive: true });
      this.state.savedAt = this.now();
      this.state.seen = [...this.seenSet].slice(-this.cfg.idempotency.maxIds);
      const tmp = `${this.file}.tmp`;
      fs.writeFileSync(tmp, JSON.stringify(this.state));
      if (fs.existsSync(this.file) && this.now() - this._lastBackup > 60000) {
        fs.copyFileSync(this.file, `${this.file}.bak`);
        this._lastBackup = this.now();
      }
      fs.renameSync(tmp, this.file); // atomik: yarım yazılmış dosya oluşmaz
    } catch (err) {
      console.error(`[oyun:${this.scenarioId}] kayıt yazılamadı:`, err.message);
    }
  }

  _scheduleSave() {
    if (!this.autosave || this._saveTimer) return;
    this._saveTimer = setTimeout(() => { this._saveTimer = null; this.save(); }, 1500);
    if (this._saveTimer.unref) this._saveTimer.unref();
  }

  flush() {
    if (this._saveTimer) { clearTimeout(this._saveTimer); this._saveTimer = null; }
    if (this._pushTimer) { clearTimeout(this._pushTimer); this._pushTimer = null; }
    this.save();
  }

  _changed() {
    this._dirtyState = true;
    this._scheduleSave();
    if (this._pushTimer) return;
    this._pushTimer = setTimeout(() => { this._pushTimer = null; this.emitState(this.snapshot()); }, this.debounceMs);
    if (this._pushTimer.unref) this._pushTimer.unref();
  }

  // ---------------------------------------------------------------- yaşam döngüsü
  start() {
    this.stop();
    const sec = (this.cfg.economy && this.cfg.economy.tickSec) || 5;
    this._tick = setInterval(() => this.tick(), sec * 1000);
    this._auTimer = setInterval(() => this.auctionTick(), 1000);
    if (this._tick.unref) this._tick.unref();
    if (this._auTimer.unref) this._auTimer.unref();

    // (Yeniden) başlarken açık artırma güvenliği:
    //  - aktif ve süresi dolmuşsa  -> auctionTick() kontrollü kapatır (kayıtlı tekliflerle)
    //  - aktif ve süresi varsa     -> kalan süreyle devam eder
    //  - beklemede ve zamanı geçmişse -> ani başlamasın diye restartDelaySec kadar ertelenir
    const a = this.state.auction;
    if (this.au.enabled && a.phase === 'idle' && a.nextAt <= this.now()) {
      a.nextAt = this.now() + this.au.restartDelaySec * 1000;
      this._changed();
    }
    this.auctionTick();
    this.emitState(this.snapshot());
  }

  stop() {
    if (this._tick) { clearInterval(this._tick); this._tick = null; }
    if (this._auTimer) { clearInterval(this._auTimer); this._auTimer = null; }
    this.flush();
  }

  /** Pasif ekonomi geliri + bekleme tablosu temizliği */
  tick() {
    const e = this.cfg.economy || {};
    const st = this.state.stats;
    const income = (st.production || 0) * (e.incomePerProduction ?? 1);
    if (income > 0) st.treasury += income;
    const t = this.now();
    for (const [k, until] of this.cd) if (until < t) this.cd.delete(k);
    for (const [k, v] of this.lastText) if (t - v.at > this.cfg.spam.duplicateWindowSec * 1000) this.lastText.delete(k);
    this._changed();
  }

  // ---------------------------------------------------------------- yardımcılar
  rankIdxOf(user) { return this.rankIndex.get(user.rank) ?? 0; }
  rankOf(user) { return this.ranks[this.rankIdxOf(user)]; }
  displayName(u) { return u.name || u.nickname || u.uniqueId; }

  pub(u) {
    if (!u) return null;
    return {
      id: u.id, uniqueId: u.uniqueId, name: this.displayName(u), nickname: u.nickname,
      avatar: u.avatar, rank: u.rank, points: Math.floor(u.points), job: u.job || null,
    };
  }

  _cooling(userId, key, sec) {
    const k = `${userId}:${key}`;
    const t = this.now();
    if ((this.cd.get(k) || 0) > t) return true;
    if (sec > 0) this.cd.set(k, t + sec * 1000);
    return false;
  }

  _cdLeft(userId, key) {
    return Math.max(0, Math.ceil(((this.cd.get(`${userId}:${key}`) || 0) - this.now()) / 1000));
  }

  _getUser(evUser, create) {
    if (!evUser || !evUser.id) return null;
    let u = this.state.users[evUser.id];
    if (!u) {
      if (!create) return null;
      const max = (this.cfg.limits && this.cfg.limits.maxUsers) || 5000;
      if (Object.keys(this.state.users).length >= max) return null;
      u = {
        id: evUser.id, uniqueId: evUser.uniqueId, nickname: evUser.nickname, name: '', avatar: evUser.avatar,
        rank: this.ranks[0].id, points: 0, job: null, joinedAt: this.now(), lastSeenAt: this.now(),
        giftValue: 0, giftPoints: 0, followed: false,
      };
      this.state.users[u.id] = u;
      u._new = true;
    } else {
      // profil fotoğrafı/ad TikTok'ta değişmiş olabilir
      if (evUser.avatar) u.avatar = evUser.avatar;
      if (evUser.nickname) u.nickname = evUser.nickname;
      if (evUser.uniqueId) u.uniqueId = evUser.uniqueId;
      u.lastSeenAt = this.now();
    }
    return u;
  }

  _effect(eff, mult = 1, user = null) {
    if (!eff) return;
    const st = this.state.stats;
    for (const k of ['treasury', 'population', 'production']) {
      if (typeof eff[k] === 'number') st[k] += eff[k] * mult;
    }
    if (user && user.job && eff === this._playerCommentEffect) {
      const bonus = (this.cfg.professions[user.job] || {}).commentEffect;
      if (bonus) for (const k of Object.keys(bonus)) st[k] += bonus[k] * mult;
    }
    this._recomputeCities(false);
  }

  get _effects() { return this.cfg.economy.effects; }
  get _playerCommentEffect() { return this._effects.playerComment; }

  _recomputeCities(silent) {
    const list = (this.cfg.economy && this.cfg.economy.cities) || [];
    const pop = this.state.stats.population;
    const count = list.filter((c) => pop >= (c.population || 0)).length;
    const prev = this.state.stats.cities || 0;
    this.state.stats.cities = count;
    if (!silent && count > prev) {
      this.emitFx('city', { name: list[count - 1].name, count });
    }
  }

  // ---------------------------------------------------------------- puan / rütbe
  /** Puan ekler; yalnızca "auto" rütbelere kadar otomatik terfi eder. Terfi olduysa true */
  _addPoints(user, amount, opts = {}) {
    if (!(amount > 0)) return false;
    user.points = r2(user.points + amount);
    let target = this.rankIdxOf(user);
    for (let i = target + 1; i < this.ranks.length; i++) {
      const r = this.ranks[i];
      if (r.auto !== false && user.points >= r.minPoints) target = i;
    }
    return this._setRank(user, target, 'points', opts);
  }

  /** Rütbe değiştirir. Yalnızca yükselmede (veya force ile) uygular. Padişah rütbesine yalnızca taht kuralları (_crown) ulaştırır. */
  _setRank(user, idx, reason, { force = false, silent = false } = {}) {
    const old = this.rankIdxOf(user);
    if (idx === old) return false;
    if (idx > old || force) {
      if (idx === this.throneIdx && !force) return false;
      user.rank = this.ranks[idx].id;
      if (!silent) {
        const r = this.ranks[idx];
        this.emitFx(idx > old ? 'rankup' : 'demote', {
          user: this.pub(user), from: this.ranks[old].id, to: r.id, fx: r.fx || 'minor', reason,
        });
      }
      return true;
    }
    return false;
  }

  /** Taç verir (puana DOKUNMAZ). Önceki sahip demoteTo rütbesine iner ve vizier olur. Olay yayınlamaz; çağıran duyurur. */
  _crown(user, bid) {
    const th = this.state.throne;
    const prev = th.holderId && th.holderId !== user.id ? this.state.users[th.holderId] : null;
    if (prev) {
      this._setRank(prev, this.demoteIdx, 'dethroned', { force: true, silent: true });
      th.vizierId = prev.id;
    }
    user.rank = this.ranks[this.throneIdx].id;
    th.holderId = user.id;
    th.crownedAt = this.now();
    th.crownBid = bid || 0;
    th.history.unshift({ id: user.id, name: this.displayName(user), at: th.crownedAt, bid: bid || 0 });
    th.history.length = Math.min(th.history.length, 20);
    return prev;
  }

  /** Taht değişmezi: kayıtta tam olarak tek kişi Padişah rütbesinde olabilir ve o da tahtın sahibidir. */
  _enforceThrone() {
    const th = this.state.throne;
    if (th.holderId && !this.state.users[th.holderId]) th.holderId = null;
    if (th.vizierId && !this.state.users[th.vizierId]) th.vizierId = null;
    for (const u of Object.values(this.state.users)) {
      if (this.rankIdxOf(u) === this.throneIdx && u.id !== th.holderId) {
        if (!th.holderId) { th.holderId = u.id; th.crownedAt = th.crownedAt || this.now(); } // sahipsiz kalmış kayıt: ilk bulunan sahip olur
        else u.rank = this.ranks[this.demoteIdx].id;
      }
    }
    const h = th.holderId ? this.state.users[th.holderId] : null;
    if (h && this.rankIdxOf(h) !== this.throneIdx) h.rank = this.ranks[this.throneIdx].id;
  }

  // ---------------------------------------------------------------- AÇIK ARTIRMA
  _initAuction() {
    const a = this.state.auction;
    if (!a.nextAt) a.nextAt = this.now() + this.au.intervalSec * 1000;
  }

  /** Her saniye çağrılır (testlerde elle). Zamanı gelen açık artırmayı başlatır, süresi dolanı bitirir. */
  auctionTick() {
    if (!this.au.enabled) return;
    const a = this.state.auction;
    const now = this.now();
    if (a.phase === 'idle') { if (now >= a.nextAt) this._auctionStart(false); } else if (now >= a.endsAt) this._auctionEnd();
  }

  _auctionStart() {
    const a = this.state.auction;
    const now = this.now();
    a.phase = 'active'; a.id += 1; a.startedAt = now; a.endsAt = now + this.au.durationSec * 1000;
    a.extensions = 0; a.seq = 0; a.bids = {};
    this.emitFx('auction', { mode: 'start', id: a.id, endsAt: a.endsAt, durationSec: this.au.durationSec, holder: this.pub(this._holder()) });
    this._changed();
  }

  _holder() { const id = this.state.throne.holderId; return id ? this.state.users[id] : null; }

  _bidList() {
    const a = this.state.auction;
    return Object.entries(a.bids)
      .map(([id, b]) => ({ user: this.state.users[id], points: b.points, seq: b.seq }))
      .filter((e) => e.user)
      .sort((x, y) => (y.points - x.points) || (y.seq - x.seq)); // eşitlikte SON teklif veren öne geçer
  }

  /** Bir hediye puanını teklif olarak işler (yalnızca açık artırma sürerken). Süre uzatma kuralı burada. */
  _bid(user, points) {
    const a = this.state.auction;
    if (!this.au.enabled || a.phase !== 'active') return null;
    const now = this.now();
    if (now >= a.endsAt || !(points > 0) || points < (this.au.minBidPoints || 0)) return null;
    const b = a.bids[user.id] || (a.bids[user.id] = { points: 0, seq: 0, at: 0 });
    b.points = r2(b.points + points);
    b.seq = ++a.seq;
    b.at = now;
    let extended = false;
    if (a.endsAt - now <= this.au.extendWindowSec * 1000) { // son N saniyede gelen teklif süreyi uzatır
      a.endsAt += this.au.extendBySec * 1000;
      a.extensions += 1;
      extended = true;
      this.emitFx('auction', { mode: 'extend', id: a.id, endsAt: a.endsAt, bySec: this.au.extendBySec, by: this.pub(user) });
    }
    const position = this._bidList().findIndex((e) => e.user.id === user.id) + 1;
    this.emitFx('auction', { mode: 'bid', id: a.id, user: this.pub(user), bid: b.points, delta: points, position });
    return { total: b.points, position, extended };
  }

  _auctionEnd() {
    const a = this.state.auction;
    const th = this.state.throne;
    const now = this.now();
    const list = this._bidList();
    const top = list.slice(0, this.au.topN).map((e) => Object.assign(this.pub(e.user), { bid: Math.floor(e.points) }));
    let payload;
    if (!list.length) {
      payload = { mode: 'end', id: a.id, winner: null, bid: 0, retained: false, previous: null, demotedTo: null, top, bidders: 0 };
    } else {
      const w = list[0];
      const holder = this._holder();
      const retained = !!holder && holder.id === w.user.id;
      let prev = null;
      if (retained) { th.crownBid = w.points; th.crownedAt = now; th.history.unshift({ id: w.user.id, name: this.displayName(w.user), at: now, bid: w.points, retained: true }); th.history.length = Math.min(th.history.length, 20); } else prev = this._crown(w.user, w.points);
      payload = {
        mode: 'end', id: a.id, winner: this.pub(w.user), bid: Math.floor(w.points), retained,
        previous: this.pub(prev), demotedTo: prev ? this.ranks[this.demoteIdx].id : null, top, bidders: list.length,
      };
    }
    a.last = { id: a.id, endedAt: now, winnerId: payload.winner ? payload.winner.id : null, winnerName: payload.winner ? payload.winner.name : null, bid: payload.bid, retained: payload.retained, bidders: payload.bidders };
    // Teklifler sıfırlanır; oyuncu PUANLARI asla. Yeni mücadele için aralık sayacı BİTİŞTEN itibaren başlar.
    a.phase = 'idle'; a.bids = {}; a.seq = 0; a.extensions = 0; a.startedAt = 0; a.endsAt = 0;
    a.nextAt = now + this.au.intervalSec * 1000;
    this.emitFx('auction', payload);
    this._changed();
    return payload;
  }

  // ---------------------------------------------------------------- olay girişi
  handleEvent(ev) {
    if (!ev || !ev.type || !ev.user) return;
    this.auctionTick(); // gecikmiş saniye sayacı olsa bile süresi dolan açık artırmadan SONRA gelen olay teklif sayılmasın
    try {
      // İdempotency: aynı TikTok olayı ikinci kez işlenmez (yeniden başlatma sonrası da)
      if (ev.id) {
        if (this.seenSet.has(ev.id)) { this.state.stats.counters.duplicates++; this._changed(); return; }
        this.seenSet.add(ev.id);
        if (this.seenSet.size > this.cfg.idempotency.maxIds) this.seenSet.delete(this.seenSet.values().next().value);
      }
      switch (ev.type) {
        case 'gift': this._onGift(ev); break;
        case 'comment': this._onComment(ev); break;
        case 'follow': this._onFollow(ev); break;
        case 'share': this._onShare(ev); break;
        case 'like': this._onLike(ev); break;
        case 'join': if (this.cfg.autoJoin && this.cfg.autoJoin.join) this._join(ev, true); break;
        default: break;
      }
    } catch (err) {
      console.error(`[oyun:${this.scenarioId}] olay işlenemedi (${ev.type}):`, err);
    }
    this._changed();
  }

  _autoJoin(ev, kind) {
    const aj = this.cfg.autoJoin || {};
    const user = this._getUser(ev.user, !!aj[kind]);
    if (user && user._new) this._announceJoin(user, kind);
    return user;
  }

  _announceJoin(user, via) {
    delete user._new;
    this._effect(this._effects.join);
    this._addPoints(user, this.cfg.points.join || 0);
    this.emitFx('join', { user: this.pub(user), via });
  }

  /** Hediye puanı: connector metadata değeri (giftDetails.diamondCount) x points.perGiftValue (veya hediyeye özel birim puan) */
  _giftPoints(name, valueEach, count) {
    const o = this.cfg.points.giftOverrides && this.cfg.points.giftOverrides[name];
    const unit = typeof o === 'number' ? o : valueEach * this.cfg.points.perGiftValue;
    return r2(unit * count);
  }

  _onGift(ev) {
    const g = ev.gift || {};
    const c = this.state.stats.counters;
    const vf = this.cfg.giftValue;
    // Değer alanı karışıklığı: yalnızca yapılandırmadaki metadata alanından gelen değer kabul edilir
    if (g.valueField !== vf.field) { c.rejectedGifts++; return; }
    const count = Math.max(1, Math.floor(Number(g.count) || 1));
    const each = Number(g.valueEach);
    const user = this._autoJoin(ev, 'gift');
    if (!(each > 0) || !Number.isFinite(each) || each > vf.maxPerUnit) { c.unknownGifts++; return; } // güvenilir değer yok: puan verilmez
    const value = each * count;
    const points = this._giftPoints(g.name, each, count);
    this._effect(this._effects.giftPerValue, value);
    c.gifts += count; c.giftValue += value; c.giftPoints = r2(c.giftPoints + points);
    if (!user) return; // oyuna alınamadı (kapasite): yalnızca ekonomiye katkı

    user.giftValue = (user.giftValue || 0) + value;
    user.giftPoints = r2((user.giftPoints || 0) + points);

    // Normal puan HER ZAMAN yazılır; açık artırma sürüyorsa aynı puan teklife de eklenir.
    const before = this.rankIdxOf(user);
    this._addPoints(user, points, { silent: true });
    const bid = this._bid(user, points);
    const after = this.rankIdxOf(user);
    if (after > before) {
      const r = this.ranks[after];
      this.emitFx('rankup', { user: this.pub(user), from: this.ranks[before].id, to: r.id, fx: r.fx || 'minor', reason: 'gift' });
    }
    const nx = this._nextInfo(user);
    this.emitFx('gift', {
      user: this.pub(user), gift: { name: g.name, count, value, image: g.image || '' }, rankChanged: after > before,
      points, total: Math.floor(user.points), rank: this.ranks[after].id,
      next: nx ? { id: nx.id, name: nx.name, need: Math.ceil(nx.need) } : null,
      bid: bid ? { total: Math.floor(bid.total), position: bid.position, extended: bid.extended } : null,
    });
  }

  // ---------------------------------------------------------------- ücretsiz etkileşimler (yorum, takip, paylaşım, beğeni)
  _globalCommentAllowed() {
    const t = this.now();
    this._cmtWindow = this._cmtWindow.filter((x) => t - x < 60000);
    if (this._cmtWindow.length >= this.cfg.spam.globalPerMinute) return false;
    this._cmtWindow.push(t);
    return true;
  }

  _onFollow(ev) {
    this.state.stats.counters.follows++;
    this._effect(this._effects.follow);
    const user = this._autoJoin(ev, 'follow');
    if (!user || user.followed) return;
    user.followed = true;
    this._addPoints(user, this.cfg.points.follow || 0);
  }

  _onShare(ev) {
    this.state.stats.counters.shares++;
    const user = this._autoJoin(ev, 'share');
    if (!user) return;
    if (this._cooling(user.id, 'share', 60)) return;
    this._effect(this._effects.share);
    this._addPoints(user, this.cfg.points.share || 0);
  }

  _onLike(ev) {
    const n = Math.max(1, Math.floor(Number(ev.count) || 1));
    this.state.stats.counters.likes += n;
    this._effect(this._effects.likePerLike, n);
    const user = this._autoJoin(ev, 'like');
    if (!user) return;
    const capped = Math.min(n, this.cfg.points.likeMaxPerEvent || 50);
    this._addPoints(user, capped * (this.cfg.points.like || 0));
  }

  // ---------------------------------------------------------------- yorum ve komutlar
  parseCommand(text) {
    const t = norm(text);
    if (!t.startsWith('!')) return null;
    const [head, ...rest] = t.split(' ');
    const name = this.cmdMap.get(head);
    if (!name) return null;
    return { name, args: rest.join(' '), rawArgs: String(text).trim().split(/\s+/).slice(1).join(' ') };
  }

  _onComment(ev) {
    const cmd = this.parseCommand(ev.text);
    if (cmd) return this._command(cmd, ev);
    // Sıradan yorum: şehir nüfusuna katkı (herkes) + oyuncuya küçük puan/ekonomi. SPAM sınırları:
    //  1) kullanıcı başına bekleme süresi  2) aynı metni tekrar etme  3) genel dakikalık sınır
    const text = norm(ev.text);
    if (!text) return undefined;
    const sp = this.cfg.spam;
    if (this._cooling(ev.user.id, 'comment', sp.commentCooldownSec)) return undefined;
    const last = this.lastText.get(ev.user.id);
    if (last && last.t === text && this.now() - last.at < sp.duplicateWindowSec * 1000) return undefined;
    this.lastText.set(ev.user.id, { t: text, at: this.now() });
    if (!this._globalCommentAllowed()) return undefined;

    this.state.stats.counters.comments++;
    this._effect(this._effects.comment);
    const user = this._autoJoin(ev, 'comment');
    if (user) {
      this._effect(this._playerCommentEffect, 1, user);
      this._addPoints(user, this.cfg.points.comment || 0);
    }
    return undefined;
  }

  _join(ev, silentIfExists) {
    const existing = this.state.users[ev.user.id];
    if (existing) {
      this._getUser(ev.user, false); // profil tazele
      if (!silentIfExists && !this._cooling(existing.id, 'cmd:join', this.cfg.cooldowns.join ?? 20)) {
        this.emitFx('notice', { kind: 'already', user: this.pub(existing), icon: '📜', title: `${this.displayName(existing)}, zaten krallıktasın`, lines: [`${this.rankOf(existing).name} · ${fmtNum(existing.points)} puan`] });
      }
      return existing;
    }
    const user = this._getUser(ev.user, true);
    if (!user) {
      this.emitFx('notice', { kind: 'full', icon: '🏰', title: 'Krallık dolu', lines: ['Yeni oyuncu kabul edilemiyor.'] });
      return null;
    }
    this._announceJoin(user, 'command');
    return user;
  }

  _command(cmd, ev) {
    const cd = this.cfg.cooldowns || {};
    if (cmd.name === 'join') { this._join(ev, false); return; }
    if (cmd.name === 'guide') { // herkes açabilir; tek bir genel bekleme süresi var (spam ile ekran kilitlenemez)
      if (!this._cooling('*', 'cmd:guide', cd.guide ?? 20)) this.showGuide('command');
      return;
    }

    const user = this._getUser(ev.user, false);
    if (!user) {
      if (!this._cooling(ev.user.id, 'cmd:nojoin', 20)) {
        this.emitFx('notice', { kind: 'hint', icon: '📜', title: `${ev.user.nickname}, önce krallığa katıl`, lines: [`Yorumuna ${this.cfg.commands.join[0]} yaz.`] });
      }
      return;
    }
    if (this._cooling(user.id, `cmd:${cmd.name}`, cd[cmd.name] ?? 10)) return; // spam koruması

    switch (cmd.name) {
      case 'rank': return this._cmdRank(user);
      case 'name': return this._cmdName(user, cmd.rawArgs);
      case 'job': return this._cmdJob(user, cmd.args);
      case 'army': return this._cmdArmy(user);
      case 'treasury': return this._cmdTreasury(user);
      default: return undefined;
    }
  }

  rankPosition(user) {
    return this._sortedByPoints().findIndex((u) => u.id === user.id) + 1;
  }

  /** Puanla ulaşılabilecek bir sonraki rütbe ve kalan puan (yoksa null) */
  _nextInfo(user) {
    for (let i = this.rankIdxOf(user) + 1; i < this.ranks.length; i++) {
      const r = this.ranks[i];
      if (r.auto !== false) return { id: r.id, name: r.name, icon: r.icon, need: Math.max(0, r.minPoints - user.points) };
    }
    return null;
  }

  _cmdRank(user) {
    const idx = this.rankIdxOf(user);
    const r = this.ranks[idx];
    let nextLine = 'Puanla ulaşılabilecek en yüksek rütbedesin!';
    const nx = this._nextInfo(user);
    if (nx) nextLine = `Sonraki: ${nx.name} (${fmtNum(nx.need)} puan kaldı)`;
    const lines = [`${fmtNum(user.points)} puan · Sıra #${this.rankPosition(user)}/${Object.keys(this.state.users).length}`, nextLine];
    const a = this.state.auction;
    if (a.phase === 'active' && a.bids[user.id]) lines.push(`Açık artırma teklifin: ${fmtNum(a.bids[user.id].points)} puan`);
    this.emitFx('notice', { kind: 'rank', user: this.pub(user), icon: r.icon, title: `${this.displayName(user)} · ${r.name}`, lines });
  }

  _cmdName(user, raw) {
    const res = this.sanitizeName(raw);
    if (!res.ok) {
      this.emitFx('notice', { kind: 'error', user: this.pub(user), icon: '✒️', title: 'İsim değişmedi', lines: [res.reason] });
      return;
    }
    const left = this._cdLeft(user.id, 'namechange');
    if (left > 0 && user.name) {
      this.emitFx('notice', { kind: 'error', user: this.pub(user), icon: '⏳', title: 'Biraz bekle', lines: [`İsmi ${left} sn sonra tekrar değiştirebilirsin.`] });
      return;
    }
    this._cooling(user.id, 'namechange', (this.cfg.cooldowns && this.cfg.cooldowns.nameChange) ?? 120);
    const old = this.displayName(user);
    user.name = res.name;
    this.emitFx('notice', { kind: 'name', user: this.pub(user), icon: '✒️', title: 'Yeni isim', lines: [`${old} → ${res.name}`] });
  }

  sanitizeName(raw) {
    const max = (this.cfg.limits && this.cfg.limits.nameLength) || 16;
    let s = String(raw || '').normalize('NFKC').replace(/[^\p{L}\p{N} ._'-]/gu, '').replace(/\s+/g, ' ').trim();
    if (!s) return { ok: false, reason: `Kullanım: ${this.cfg.commands.name[0]} Yeni Ad` };
    if (s.length > max) s = s.slice(0, max).trim();
    if (s.length < 2) return { ok: false, reason: 'İsim en az 2 karakter olmalı.' };
    const n = norm(s);
    const blocked = new Set([...(this.cfg.nameBlocklist || []).map(norm), ...this.ranks.map((r) => norm(r.name))]);
    if (blocked.has(n)) return { ok: false, reason: 'Bu isim kullanılamaz.' };
    return { ok: true, name: s };
  }

  _cmdJob(user, args) {
    const profs = this.cfg.professions || {};
    const key = this.jobAlias.get(norm(args));
    const list = Object.values(profs).map((p) => p.label || p.names[0]).join(' / ');
    if (!key) {
      this.emitFx('notice', { kind: 'error', user: this.pub(user), icon: '🛠️', title: 'Meslek seç', lines: [`Kullanım: ${this.cfg.commands.job[0]} ${list.toLowerCase()}`] });
      return;
    }
    const p = profs[key];
    if (user.job === key) {
      this.emitFx('notice', { kind: 'job', user: this.pub(user), icon: p.icon, title: 'Zaten bu mesleksin', lines: [p.label] });
      return;
    }
    if (user.points < (p.unlockPoints || 0)) {
      this.emitFx('notice', { kind: 'error', user: this.pub(user), icon: p.icon, title: `${p.label} olmak için ${fmtNum(p.unlockPoints - user.points)} puan daha gerekli`, lines: [`Yorum yaparak ve etkinliklere katılarak puan kazan.`] });
      return;
    }
    user.job = key;
    if (p.minRank) this._setRank(user, this.rankIndex.get(p.minRank), 'job');
    this.emitFx('job', { user: this.pub(user), job: key, label: p.label, icon: p.icon });
  }

  armyStats() {
    let power = 0;
    let soldiers = 0;
    const byRank = {};
    for (const u of Object.values(this.state.users)) {
      const r = this.rankOf(u);
      let p = r.army || 0;
      const jb = u.job && this.cfg.professions[u.job] ? this.cfg.professions[u.job].armyBonus || 0 : 0;
      p += jb;
      if (p > 0) {
        power += p;
        soldiers++;
        byRank[r.id] = (byRank[r.id] || 0) + 1;
      }
    }
    return { power, soldiers, byRank };
  }

  _cmdArmy(user) {
    const a = this.armyStats();
    const mine = (this.rankOf(user).army || 0) + (user.job && this.cfg.professions[user.job] ? this.cfg.professions[user.job].armyBonus || 0 : 0);
    this.emitFx('notice', {
      kind: 'army', user: this.pub(user), icon: '⚔️', title: `Ordu gücü: ${fmtNum(a.power)}`,
      lines: [`${fmtNum(a.soldiers)} asker · Senin katkın: ${fmtNum(mine)}`],
    });
  }

  _cmdTreasury(user) {
    const st = this.state.stats;
    const e = this.cfg.economy || {};
    this.emitFx('notice', {
      kind: 'treasury', user: this.pub(user), icon: '💰', title: `Hazine: ${fmtNum(st.treasury)} altın`,
      lines: [`Üretim: ${st.production.toFixed(1)} · Gelir: +${fmtNum(st.production * (e.incomePerProduction ?? 1))}/${e.tickSec || 5}sn`, `Nüfus: ${fmtNum(st.population)} · Şehir: ${st.cities}`],
    });
  }

  // ---------------------------------------------------------------- rehber (görünüm olayları)
  /** Büyük hediye/rütbe rehberini ekranda açtırır (reason: command | admin | intro) */
  showGuide(reason, seconds) {
    const g = this.cfg.guide;
    if (!g) return;
    const sec = seconds ?? (reason === 'intro' ? (g.intro && g.intro.seconds) || 10 : g.openSeconds || 15);
    this.emitFx('guide', { mode: 'open', seconds: sec, reason });
  }

  /** Yayın açıldığında (TikTok'a bağlanınca) otomatik katılım + hediye bilgilendirmesi */
  intro() {
    const g = this.cfg.guide;
    if (g && g.intro && g.intro.enabled !== false && g.intro.onLiveConnect !== false) this.showGuide('intro');
  }

  // ---------------------------------------------------------------- anlık görüntü
  _sortedUsers() {
    return Object.values(this.state.users).sort((a, b) => (this.rankIdxOf(b) - this.rankIdxOf(a)) || (b.points - a.points) || (a.joinedAt - b.joinedAt));
  }

  _sortedByPoints() {
    return Object.values(this.state.users).sort((a, b) => (b.points - a.points) || (a.joinedAt - b.joinedAt));
  }

  /** Sadrazam: son tahttan inen Padişah (hâlâ Sadrazam ise); yoksa puanı en yüksek Sadrazam */
  _vizier() {
    const th = this.state.throne;
    const v = th.vizierId ? this.state.users[th.vizierId] : null;
    if (v && this.rankIdxOf(v) === this.demoteIdx) return v;
    return this._sortedByPoints().find((u) => this.rankIdxOf(u) === this.demoteIdx && u.id !== th.holderId) || null;
  }

  snapshot() {
    const sorted = this._sortedUsers();
    const top = (this.cfg.view && this.cfg.view.sendTop) || 250;
    const counts = {};
    for (const u of sorted) counts[u.rank] = (counts[u.rank] || 0) + 1;
    const th = this.state.throne;
    const army = this.armyStats();
    const cities = (this.cfg.economy && this.cfg.economy.cities) || [];
    const st = this.state.stats;
    const a = this.state.auction;
    const bids = a.phase === 'active' ? this._bidList() : [];
    const now = this.now();
    return {
      scenario: this.scenarioId,
      users: sorted.slice(0, top).map((u) => this.pub(u)),
      topPoints: this._sortedByPoints().slice(0, (this.cfg.view && this.cfg.view.leaderboardTop) || 5).map((u) => this.pub(u)),
      total: sorted.length,
      rankCounts: counts,
      stats: {
        treasury: Math.floor(st.treasury), population: Math.floor(st.population), production: Math.round(st.production * 10) / 10,
        cities: st.cities, cityName: st.cities ? cities[st.cities - 1].name : '', army: Math.floor(army.power), soldiers: army.soldiers,
      },
      counters: Object.assign({}, st.counters, { giftValue: Math.floor(st.counters.giftValue), giftPoints: Math.floor(st.counters.giftPoints) }),
      ui: { guideVisible: !!(this.state.ui && this.state.ui.guideVisible) },
      throne: {
        holder: th.holderId ? this.pub(this.state.users[th.holderId]) : null,
        vizier: this.pub(this._vizier()),
        crownedAt: th.crownedAt, crownBid: Math.floor(th.crownBid || 0), history: th.history.slice(0, 5),
      },
      auction: {
        enabled: !!this.au.enabled, phase: a.phase, id: a.id, serverNow: now, nextAt: a.nextAt, endsAt: a.endsAt,
        durationSec: this.au.durationSec, intervalSec: this.au.intervalSec, extendWindowSec: this.au.extendWindowSec, extendBySec: this.au.extendBySec,
        extensions: a.extensions, bidders: bids.length, totalBid: Math.floor(bids.reduce((s, e) => s + e.points, 0)),
        top: bids.slice(0, this.au.topN).map((e) => Object.assign(this.pub(e.user), { bid: Math.floor(e.points) })),
        last: a.last,
      },
    };
  }

  // ---------------------------------------------------------------- yönetici (test paneli) işlemleri
  _find(ref) {
    if (!ref) return null;
    const direct = this.state.users[ref];
    if (direct) return direct;
    const n = norm(ref);
    return Object.values(this.state.users).find((u) => norm(u.uniqueId) === n || norm(this.displayName(u)) === n) || null;
  }

  admin(action, p = {}) {
    const ok = (message) => { this._changed(); return { ok: true, message }; };
    const fail = (message) => ({ ok: false, message });
    const forceCrown = (u) => {
      const prev = this._crown(u, 0);
      this.emitFx('coronation', { user: this.pub(u), previous: this.pub(prev), forced: true, demotedTo: prev ? this.ranks[this.demoteIdx].id : null });
    };
    switch (action) {
      case 'setRank': {
        const u = this._find(p.user);
        const idx = this.rankIndex.get(p.rank);
        if (!u) return fail('Kullanıcı bulunamadı (önce !katıl ile ekle).');
        if (idx === undefined) return fail('Rütbe bulunamadı.');
        if (idx === this.throneIdx) { forceCrown(u); return ok('Taç giydirildi.'); }
        if (this.state.throne.holderId === u.id) this.state.throne.holderId = null; // tahttan indir
        this._setRank(u, idx, 'admin', { force: true });
        return ok(`${this.displayName(u)} → ${this.ranks[idx].name}`);
      }
      case 'addPoints': {
        const u = this._find(p.user);
        if (!u) return fail('Kullanıcı bulunamadı.');
        this._addPoints(u, Number(p.points) || 0);
        return ok('Puan eklendi.');
      }
      case 'crown': {
        const u = this._find(p.user);
        if (!u) return fail('Kullanıcı bulunamadı.');
        forceCrown(u);
        return ok('Taç giydirildi.');
      }
      case 'inspect': { // salt okunur: bir oyuncunun güncel rütbe/puanı (panelin eşik sınaması için)
        const u = this._find(p.user);
        if (!u) return { ok: false, message: 'Kullanıcı bulunamadı.', data: null };
        const b = this.state.auction.bids[u.id];
        return { ok: true, message: `${this.displayName(u)}: ${u.rank}`, data: { id: u.id, rank: u.rank, points: Math.floor(u.points), bid: b ? b.points : 0 } };
      }
      case 'auctionStart': {
        if (!this.au.enabled) return fail('Açık artırma kapalı (config).');
        if (this.state.auction.phase === 'active') return fail('Açık artırma zaten sürüyor.');
        this._auctionStart();
        return ok('Açık artırma başlatıldı.');
      }
      case 'auctionEnd': {
        if (this.state.auction.phase !== 'active') return fail('Sürmekte olan açık artırma yok.');
        this._auctionEnd();
        return ok('Açık artırma bitirildi.');
      }
      case 'guide': { // yan rehberi göster/gizle (yayıncı kontrolü; OBS sahnesine anında yansır)
        if (!this.cfg.guide) return fail('Bu senaryoda rehber yok.');
        const v = p.visible === 'toggle' ? !this.state.ui.guideVisible : !!p.visible;
        this.state.ui.guideVisible = v;
        return ok(v ? 'Rehber gösteriliyor.' : 'Rehber gizlendi.');
      }
      case 'showGuide': {
        this.showGuide(p.intro ? 'intro' : 'admin');
        return ok('Büyük rehber açıldı.');
      }
      case 'vacateThrone': {
        const th = this.state.throne;
        const h = th.holderId ? this.state.users[th.holderId] : null;
        if (!h) return fail('Taht zaten boş.');
        this._setRank(h, this.demoteIdx, 'admin', { force: true });
        th.vizierId = h.id; th.holderId = null; th.crownedAt = 0; th.crownBid = 0;
        return ok('Taht boşaltıldı.');
      }
      case 'removeUser': {
        const u = this._find(p.user);
        if (!u) return fail('Kullanıcı bulunamadı.');
        const th = this.state.throne;
        if (th.holderId === u.id) { th.holderId = null; th.crownedAt = 0; }
        if (th.vizierId === u.id) th.vizierId = null;
        th.history = th.history.filter((h) => h.id !== u.id);
        delete this.state.auction.bids[u.id];
        delete this.state.users[u.id];
        return ok('Oyuncu silindi.');
      }
      case 'wipe': {
        if (p.confirm !== 'SIFIRLA') return fail('Onay metni hatalı.');
        this.state = this._blank();
        this.seenSet = new Set();
        this.cd.clear(); this.lastText.clear(); this._cmtWindow = [];
        this._initAuction();
        this.save(true);
        return ok('Tüm oyun verisi silindi.');
      }
      default: return fail('Bilinmeyen işlem.');
    }
  }
}

module.exports = { RankGame, norm };
