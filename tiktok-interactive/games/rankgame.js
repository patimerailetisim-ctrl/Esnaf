'use strict';
/**
 * RÜTBE OYUNU (rankgame) - sunucu taraflı oyun modülü
 * ===================================================
 * Bir senaryo dosyasındaki `game: { type: 'rankgame', ... }` bölümünü okuyup çalışan,
 * TAMAMEN yapılandırma ile yönetilen oyun mantığıdır. Osmanlı senaryosu bunu kullanır;
 * ileride başka bir "rütbe/taht" senaryosu da sadece yeni bir config yazarak kullanabilir.
 *
 *  - Kullanıcılar (TikTok id'sine göre tekil) rütbe ve puan sahibidir. Kalıcı olarak diske yazılır.
 *  - Hediye -> puan, hediye -> rütbe eşikleri config'ten gelir (kodda sabit değer YOK).
 *  - Taht (Padişah) rütbeden AYRI yönetilir: tek aktif sahip, bekleme süresi, devralma bedeli.
 *  - Hiçbir olay başka kullanıcıların kaydını silmez; yalnızca eski Padişah bir kademeye iner.
 *  - Komutlar: katıl / rütbe / isim / meslek / ordu / hazine (yazımları config'te).
 *
 * Dışarıya şunları verir:  handleEvent(ev), snapshot(), admin(action, payload), start(), stop(), flush()
 * Dışarıdan şunları alır:  opts.emitFx(type, payload), opts.emitState(snapshot)
 */
const fs = require('fs');
const path = require('path');

const SAVE_VERSION = 1;

// Türkçe karakterleri sadeleştirip küçük harfe çevirir ("!RÜTBE" == "!rutbe")
const norm = (s) => String(s ?? '')
  .toLocaleLowerCase('tr')
  .replace(/ı/g, 'i').replace(/ş/g, 's').replace(/ç/g, 'c').replace(/ğ/g, 'g').replace(/ö/g, 'o').replace(/ü/g, 'u')
  .replace(/\s+/g, ' ')
  .trim();

const fmtNum = (n) => Math.floor(n).toLocaleString('tr-TR');

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
    c.points = Object.assign({ perDiamond: 1 }, c.points);

    this._validate();
    this.ranks = this.cfg.ranks;
    this.rankIndex = new Map(this.ranks.map((r, i) => [r.id, i]));
    this.throneIdx = this.rankIndex.get(this.cfg.throne.rank);
    this.demoteIdx = this.rankIndex.get(this.cfg.throne.demoteTo);
    this.fallbackIdx = this.rankIndex.get(this.cfg.throne.fallbackRank);

    // komutlar: normalize edilmiş eşleşme tablosu
    this.cmdMap = new Map();
    for (const [name, list] of Object.entries(this.cfg.commands || {})) {
      for (const c of [].concat(list)) this.cmdMap.set(norm(c), name);
    }
    // meslek takma adları
    this.jobAlias = new Map();
    for (const [id, j] of Object.entries(this.cfg.professions || {})) {
      this.jobAlias.set(norm(id), id);
      for (const n of j.names || []) this.jobAlias.set(norm(n), id);
    }

    this.dir = opts.dataDir || path.join(__dirname, '..', 'data');
    this.file = path.join(this.dir, `${this.cfg.storage || this.scenarioId}.json`);
    this.cd = new Map(); // bekleme süreleri (bellekte)
    this.state = this._blank();
    this._saveTimer = null;
    this._pushTimer = null;
    this._tick = null;
    this._lastBackup = 0;
    this._dirtyState = false;
    this._load();
    this._enforceThrone();
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
    need(ids.has(c.throne.fallbackRank), 'throne.fallbackRank geçerli bir rütbe id\'si olmalı');
    need(c.ranks[0].auto !== false, 'ilk rütbe (başlangıç) auto olmalı');
    need(c.points && typeof c.points.perDiamond === 'number', 'points.perDiamond gerekli');
    for (const g of c.giftRankGrants || []) need(ids.has(g.rank), `giftRankGrants: bilinmeyen rütbe "${g.rank}"`);
    for (const [k, j] of Object.entries(c.professions || {})) need(!j.minRank || ids.has(j.minRank), `professions.${k}.minRank bilinmiyor`);
    // Padişah kademesi en üst rütbe olmak zorunda değil ama aynı anda tek sahibi olacak.
  }

  _blank() {
    const st = (this.cfg.economy && this.cfg.economy.start) || {};
    return {
      version: SAVE_VERSION,
      users: {},
      stats: { treasury: st.treasury ?? 0, population: st.population ?? 0, production: st.production ?? 0, cities: 0 },
      throne: { holderId: null, crownedAt: 0, crownValue: 0, history: [] },
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
      this.state = Object.assign(this._blank(), data);
      this.state.stats = Object.assign(this._blank().stats, data.stats);
      this.state.throne = Object.assign(this._blank().throne, data.throne);
      this.state.ui = Object.assign(this._blank().ui, data.ui);
      // config'te artık olmayan rütbeleri başlangıç rütbesine çek
      for (const u of Object.values(this.state.users)) {
        if (!this.rankIndex.has(u.rank)) u.rank = this.ranks[0].id;
      }
      this._recomputeCities(true);
      console.log(`[oyun:${this.scenarioId}] kayıt yüklendi: ${Object.keys(this.state.users).length} oyuncu`);
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

  /** Diske yazar. Son kayıttan beri değişiklik yoksa atlar (force: zorla yaz). */
  save(force = false) {
    if (!this.autosave) return;
    if (!force && !this._dirtyState) return;
    this._dirtyState = false;
    try {
      fs.mkdirSync(this.dir, { recursive: true });
      this.state.savedAt = this.now();
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
    if (this._tick.unref) this._tick.unref();
  }

  stop() {
    if (this._tick) { clearInterval(this._tick); this._tick = null; }
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
        diamonds: 0, followed: false,
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
    if (user && user.job) {
      const bonus = (this.cfg.professions[user.job] || {}).commentEffect;
      if (bonus && eff === this._commentEffect) for (const k of Object.keys(bonus)) st[k] += bonus[k] * mult;
    }
    this._recomputeCities(false);
  }

  get _commentEffect() { return this.cfg.economy && this.cfg.economy.effects && this.cfg.economy.effects.comment; }

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
  _giftPoints(gift) {
    const p = this.cfg.points;
    const override = p.giftOverrides && p.giftOverrides[gift.name];
    const unit = typeof override === 'number' ? override : (gift.diamondEach ?? gift.diamonds / Math.max(1, gift.count)) * p.perDiamond;
    return unit * (gift.count || 1);
  }

  _grantFor(diamonds) {
    // Tek bir olaydaki toplam elmasa göre kazanılabilecek EN YÜKSEK rütbe
    let best = -1;
    for (const g of this.cfg.giftRankGrants || []) {
      if (diamonds >= g.minDiamonds) best = Math.max(best, this.rankIndex.get(g.rank));
    }
    return best;
  }

  /** Puan ekler; sadece "auto" rütbelere kadar otomatik terfi eder. Terfi olduysa true */
  _addPoints(user, amount, opts = {}) {
    if (!(amount > 0)) return false;
    user.points = Math.round((user.points + amount) * 100) / 100;
    let target = this.rankIdxOf(user);
    for (let i = target + 1; i < this.ranks.length; i++) {
      const r = this.ranks[i];
      if (r.auto !== false && user.points >= r.minPoints) target = i;
    }
    return this._setRank(user, target, 'points', opts);
  }

  /** Rütbe değiştirir. Yalnızca yükselmede (veya force ile) uygular. Padişah rütbesi sadece _crown ile verilir. */
  _setRank(user, idx, reason, { force = false, silent = false } = {}) {
    const old = this.rankIdxOf(user);
    if (idx === old) return false;
    if (idx > old || force) {
      if (idx === this.throneIdx && !force) return false; // taç sadece taht kuralları üzerinden
      user.rank = this.ranks[idx].id;
      const need = this.ranks[idx].minPoints || 0;
      if (idx > old && user.points < need) user.points = need;
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

  _crown(user, value, { force = false } = {}) {
    const th = this.state.throne;
    const prev = th.holderId && th.holderId !== user.id ? this.state.users[th.holderId] : null;
    if (prev) this._setRank(prev, this.demoteIdx, 'dethroned', { force: true, silent: true });
    user.rank = this.ranks[this.throneIdx].id;
    user.points = Math.max(user.points, this.ranks[this.throneIdx].minPoints || 0);
    th.holderId = user.id;
    th.crownedAt = this.now();
    th.crownValue = value;
    th.history.unshift({ id: user.id, name: this.displayName(user), at: th.crownedAt, value });
    th.history.length = Math.min(th.history.length, 20);
    this.emitFx('coronation', { user: this.pub(user), previous: this.pub(prev), value, forced: !!force, demotedTo: prev ? this.ranks[this.demoteIdx].id : null });
  }

  /** Taht değişmezi: kayıtta tam olarak tek kişi Padişah rütbesinde olabilir ve o da tahtın sahibidir. */
  _enforceThrone() {
    const th = this.state.throne;
    const holder = th.holderId ? this.state.users[th.holderId] : null;
    if (th.holderId && !holder) th.holderId = null;
    for (const u of Object.values(this.state.users)) {
      if (this.rankIdxOf(u) === this.throneIdx && u.id !== th.holderId) {
        if (!th.holderId) { th.holderId = u.id; th.crownedAt = th.crownedAt || this.now(); } // sahipsiz kalmış kayıt: ilk bulunan sahip olur
        else u.rank = this.ranks[this.demoteIdx].id;
      }
    }
    const h = th.holderId ? this.state.users[th.holderId] : null;
    if (h && this.rankIdxOf(h) !== this.throneIdx) h.rank = this.ranks[this.throneIdx].id;
  }

  throneCooldownLeftSec() {
    const th = this.state.throne;
    if (!th.holderId) return 0;
    return Math.max(0, Math.ceil((th.crownedAt + (this.cfg.throne.cooldownSec || 0) * 1000 - this.now()) / 1000));
  }

  // ---------------------------------------------------------------- olay girişi
  handleEvent(ev) {
    if (!ev || !ev.type || !ev.user) return;
    try {
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
    this._effect(this.cfg.economy && this.cfg.economy.effects && this.cfg.economy.effects.join);
    this._addPoints(user, this.cfg.points.join || 0);
    this.emitFx('join', { user: this.pub(user), via });
  }

  _onGift(ev) {
    const g = ev.gift || {};
    const diamonds = Math.max(0, Number(g.diamonds) || 0);
    const user = this._autoJoin(ev, 'gift');
    const eff = this.cfg.economy && this.cfg.economy.effects && this.cfg.economy.effects.giftPerDiamond;
    this._effect(eff, diamonds);
    if (!user) return; // oyuna katılmamış ve otomatik katılım kapalı: sadece hazineye katkı
    user.diamonds = (user.diamonds || 0) + diamonds;

    // Tek hediyede birden çok ara terfi olabilir (puan + eşik + taç). Ekrana yalnızca NİHAİ sonuç duyurulur.
    const before = this.rankIdxOf(user);
    this._addPoints(user, this._giftPoints(g), { silent: true });

    const grant = this._grantFor(diamonds);
    let crowned = false;
    let note = null;
    if (grant >= 0) {
      if (grant === this.throneIdx) {
        const r = this._tryCrown(user, diamonds);
        crowned = r.crowned;
        note = r.note;
      } else {
        this._setRank(user, grant, 'gift', { silent: true });
      }
    }
    const after = this.rankIdxOf(user);
    if (after > before && !crowned) {
      const r = this.ranks[after];
      this.emitFx('rankup', { user: this.pub(user), from: this.ranks[before].id, to: r.id, fx: r.fx || 'minor', reason: 'gift' });
    }
    // Ekrandaki "hediye etkisi" animasyonu için bilgi: kazanılan puan, hediyenin eşik rütbesi, sonraki rütbe
    const nx = this._nextInfo(user);
    this.emitFx('gift', {
      user: this.pub(user), gift: { name: g.name, count: g.count, diamonds, image: g.image || '' }, crowned, note, rankChanged: after > before,
      points: Math.round(this._giftPoints(g) * 100) / 100, total: Math.floor(user.points),
      grantRank: grant >= 0 ? this.ranks[grant].id : null, rank: this.ranks[after].id,
      next: nx ? { id: nx.id, name: nx.name, need: Math.ceil(nx.need) } : null,
    });
  }

  _tryCrown(user, diamonds) {
    const th = this.state.throne;
    const rule = this.cfg.throne;
    if (th.holderId === user.id) return { crowned: false, note: 'holder' };
    if (th.holderId) {
      const left = this.throneCooldownLeftSec();
      if (left > 0) {
        this._setRank(user, this.fallbackIdx, 'gift', { silent: true });
        this.emitFx('notice', { kind: 'throne', user: this.pub(user), icon: '🛡️', title: 'Taht korunuyor', lines: [`Yeni taç için ${left} sn beklenmeli.`, `${this.displayName(user)} ${this.ranks[this.fallbackIdx].name} oldu.`] });
        return { crowned: false, note: 'protected' };
      }
      const need = Math.ceil(th.crownValue * (rule.overtakeFactor ?? 1));
      if (diamonds < need) {
        this._setRank(user, this.fallbackIdx, 'gift', { silent: true });
        this.emitFx('notice', { kind: 'throne', user: this.pub(user), icon: '⚔️', title: 'Tahta geçmek için yetersiz', lines: [`Mevcut Padişah'ı geçmek için tek hediyede en az ${fmtNum(need)} 💎 gerekir.`] });
        return { crowned: false, note: 'too-low' };
      }
    }
    this._crown(user, diamonds);
    return { crowned: true };
  }

  _onFollow(ev) {
    const user = this._autoJoin(ev, 'follow');
    if (!user) return;
    if (!user.followed) {
      user.followed = true;
      this._effect(this.cfg.economy.effects.follow);
      this._addPoints(user, this.cfg.points.follow || 0);
    }
  }

  _onShare(ev) {
    const user = this._autoJoin(ev, 'share');
    if (!user) return;
    if (this._cooling(user.id, 'share', 60)) return;
    this._effect(this.cfg.economy.effects.share);
    this._addPoints(user, this.cfg.points.share || 0);
  }

  _onLike(ev) {
    const user = this._autoJoin(ev, 'like');
    const n = Math.max(1, Number(ev.count) || 1);
    this._effect(this.cfg.economy.effects.likePerLike, n);
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
    // Sıradan yorum: oyundaki kullanıcı küçük puan ve ekonomi katkısı verir (bekleme süreli)
    const user = this._autoJoin(ev, 'comment');
    if (!user) return;
    if (this._cooling(user.id, 'comment', this.cfg.points.commentCooldownSec || 0)) return;
    this._effect(this._commentEffect, 1, user);
    this._addPoints(user, this.cfg.points.comment || 0);
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
    const sorted = this._sortedUsers();
    return sorted.findIndex((u) => u.id === user.id) + 1;
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
    let nextLine = 'En üst kademedesin!';
    const nx = this._nextInfo(user);
    if (nx) nextLine = `Sonraki: ${nx.name} (${fmtNum(nx.need)} puan kaldı)`;
    else if (idx < this.ranks.length - 1) nextLine = 'Üst rütbeler hediye ile kazanılır.';
    this.emitFx('notice', {
      kind: 'rank', user: this.pub(user), icon: r.icon, title: `${this.displayName(user)} · ${r.name}`,
      lines: [`${fmtNum(user.points)} puan · Sıra #${this.rankPosition(user)}/${Object.keys(this.state.users).length}`, nextLine],
    });
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

  snapshot() {
    const sorted = this._sortedUsers();
    const top = (this.cfg.view && this.cfg.view.sendTop) || 250;
    const counts = {};
    for (const u of sorted) counts[u.rank] = (counts[u.rank] || 0) + 1;
    const th = this.state.throne;
    const army = this.armyStats();
    const cities = (this.cfg.economy && this.cfg.economy.cities) || [];
    const st = this.state.stats;
    return {
      scenario: this.scenarioId,
      users: sorted.slice(0, top).map((u) => this.pub(u)),
      total: sorted.length,
      rankCounts: counts,
      stats: {
        treasury: Math.floor(st.treasury), population: Math.floor(st.population), production: Math.round(st.production * 10) / 10,
        cities: st.cities, cityName: st.cities ? cities[st.cities - 1].name : '', army: Math.floor(army.power), soldiers: army.soldiers,
      },
      ui: { guideVisible: !!(this.state.ui && this.state.ui.guideVisible) },
      throne: {
        holder: th.holderId ? this.pub(this.state.users[th.holderId]) : null,
        crownedAt: th.crownedAt, crownValue: th.crownValue, cooldownLeft: this.throneCooldownLeftSec(), history: th.history.slice(0, 5),
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
    switch (action) {
      case 'setRank': {
        const u = this._find(p.user);
        const idx = this.rankIndex.get(p.rank);
        if (!u) return fail('Kullanıcı bulunamadı (önce !katıl ile ekle).');
        if (idx === undefined) return fail('Rütbe bulunamadı.');
        if (idx === this.throneIdx) { this._crown(u, p.value || 0, { force: true }); return ok('Taç giydirildi.'); }
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
        this._crown(u, p.value || 0, { force: true });
        return ok('Taç giydirildi.');
      }
      case 'inspect': { // salt okunur: bir oyuncunun güncel rütbe/puanı (panelin eşik sınaması için)
        const u = this._find(p.user);
        if (!u) return { ok: false, message: 'Kullanıcı bulunamadı.', data: null };
        return { ok: true, message: `${this.displayName(u)}: ${u.rank}`, data: { id: u.id, rank: u.rank, points: Math.floor(u.points) } };
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
        th.holderId = null; th.crownedAt = 0; th.crownValue = 0;
        return ok('Taht boşaltıldı.');
      }
      case 'removeUser': {
        const u = this._find(p.user);
        if (!u) return fail('Kullanıcı bulunamadı.');
        if (this.state.throne.holderId === u.id) { this.state.throne.holderId = null; this.state.throne.crownedAt = 0; }
        this.state.throne.history = this.state.throne.history.filter((h) => h.id !== u.id);
        delete this.state.users[u.id];
        return ok('Oyuncu silindi.');
      }
      case 'wipe': {
        if (p.confirm !== 'SIFIRLA') return fail('Onay metni hatalı.');
        this.state = this._blank();
        this.cd.clear();
        this.save(true);
        return ok('Tüm oyun verisi silindi.');
      }
      default: return fail('Bilinmeyen işlem.');
    }
  }
}

module.exports = { RankGame, norm };
