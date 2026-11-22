'use strict';
/**
 * RÜTBE OYUNU GÖRÜNÜMÜ (rankgame-view.js)
 * ---------------------------------------
 * Sunucudaki oyun modülünün (games/rankgame.js) durumunu ekrana çizer:
 *   - her oyuncu: profil fotoğrafı (kafa), rütbeye göre kıyafet + başlık, isim, rütbe, puan
 *   - rütbe bölgeleri (taht, divan, ordu, halk) senaryodaki `game.view.zones` ile tanımlanır
 *   - terfi / taç giyme / Sadrazam girişi animasyonları, bildirim kartları, şeref listesi
 * Animasyonlar CSS ile yapılır (karakter başına JS döngüsü yok) -> yüzlerce karakterde performanslıdır.
 * Hiçbir Osmanlı'ya özel sabit yoktur; her şey senaryo yapılandırmasından okunur.
 */
(function () {
  const enc = (svg) => 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html !== undefined) e.innerHTML = html; return e; };
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const fmt = (n) => Math.floor(n).toLocaleString('tr-TR');
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  function shade(hex, amt) {
    const m = /^#?([0-9a-f]{6})$/i.exec(hex || '');
    if (!m) return hex || '#888';
    const n = parseInt(m[1], 16);
    const f = (v) => Math.max(0, Math.min(255, Math.round(amt < 0 ? v * (1 + amt) : v + (255 - v) * amt)));
    const r = f((n >> 16) & 255); const g = f((n >> 8) & 255); const b = f(n & 255);
    return '#' + ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
  }

  // ------------------------------------------------------------------ KIYAFET ÇİZİMİ (SVG -> data URI, rütbe başına bir kez)
  function bodySVG(look) {
    const robe = look.robe || '#888'; const trim = look.trim || '#555'; const sash = look.sash || '#c9a227';
    const dark = shade(robe, -0.35); const light = shade(robe, 0.25);
    return `<svg xmlns="http://www.w3.org/2000/svg" width="128" height="190" viewBox="0 0 128 190">
      <defs><linearGradient id="r" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="${light}"/><stop offset=".5" stop-color="${robe}"/><stop offset="1" stop-color="${dark}"/></linearGradient></defs>
      <ellipse cx="64" cy="186" rx="46" ry="5" fill="#000" opacity=".22"/>
      <ellipse cx="46" cy="183" rx="18" ry="7" fill="#2a1a10"/><ellipse cx="82" cy="183" rx="18" ry="7" fill="#2a1a10"/>
      <path d="M40 148 L36 181 H56 L64 158 L72 181 H92 L88 148Z" fill="${dark}"/>
      <path d="M34 82 Q12 100 17 134 L33 134 Q32 108 44 94Z" fill="${robe}" stroke="${trim}" stroke-width="2"/>
      <path d="M94 82 Q116 100 111 134 L95 134 Q96 108 84 94Z" fill="${robe}" stroke="${trim}" stroke-width="2"/>
      <path d="M17 126 H33 M111 126 H95" stroke="${trim}" stroke-width="5"/>
      <circle cx="25" cy="138" r="8" fill="#e2b184"/><circle cx="103" cy="138" r="8" fill="#e2b184"/>
      <path d="M38 78 Q64 70 90 78 L104 170 Q64 180 24 170 Z" fill="url(#r)" stroke="${trim}" stroke-width="3"/>
      <path d="M64 84 V174" stroke="${trim}" stroke-width="4"/>
      <path d="M26 164 Q64 174 102 164" fill="none" stroke="${trim}" stroke-width="5" opacity=".8"/>
      <circle cx="64" cy="100" r="2.8" fill="#f2d27a"/><circle cx="64" cy="152" r="2.8" fill="#f2d27a"/>
      <path d="M29 119 Q64 130 99 119 L101 136 Q64 147 27 136 Z" fill="${sash}" stroke="${trim}" stroke-width="2"/>
      <path d="M52 76 Q64 92 76 76" fill="#e2b184" stroke="${trim}" stroke-width="2"/>
    </svg>`;
  }

  // Başlıklar: viewBox "0 -12 120 92" ; alt kenar (y=80) alın hizasıdır
  function hatInner(type, c, trim) {
    const d = shade(c, -0.32); const l = shade(c, 0.28); const gold = '#d4a72c';
    switch (type) {
      case 'cap': return `<path d="M26 78 Q28 32 60 30 Q92 32 94 78 Z" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M26 66 H94" stroke="${d}" stroke-width="4"/>`;
      case 'straw': return `<ellipse cx="60" cy="70" rx="58" ry="12" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M34 68 Q36 24 60 24 Q84 24 86 68 Z" fill="${l}" stroke="${d}" stroke-width="3"/><path d="M34 60 H86" stroke="#a33" stroke-width="5"/>`;
      case 'fez': return `<path d="M36 78 L42 24 H78 L84 78 Z" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M60 24 Q98 22 100 54" stroke="#111" stroke-width="3" fill="none"/><circle cx="100" cy="58" r="5" fill="#111"/>`;
      case 'helmet': return `<path d="M28 78 Q30 26 60 24 Q90 26 92 78 Z" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M60 24 V6" stroke="${d}" stroke-width="4"/><path d="M28 70 H92" stroke="${gold}" stroke-width="5"/><path d="M60 26 V78" stroke="${d}" stroke-width="2" opacity=".6"/>`;
      case 'bork': return `<path d="M78 24 Q114 32 108 80 Q96 62 80 66 Z" fill="${l}" stroke="${d}" stroke-width="3"/><path d="M40 78 L42 16 Q60 8 78 16 L80 78 Z" fill="${c}" stroke="${d}" stroke-width="3"/><rect x="40" y="62" width="40" height="13" fill="${gold}" stroke="#8a6512" stroke-width="2"/><path d="M52 62 q8 -22 16 0" fill="#eee" stroke="#999" stroke-width="2"/>`;
      case 'turban': return `<ellipse cx="60" cy="64" rx="46" ry="17" fill="${c}" stroke="${d}" stroke-width="3"/><ellipse cx="60" cy="46" rx="36" ry="18" fill="${l}" stroke="${d}" stroke-width="3"/><path d="M24 60 Q60 80 96 60 M34 44 Q60 62 86 44" stroke="${d}" stroke-width="3" fill="none"/><circle cx="60" cy="62" r="5" fill="${gold}" stroke="#8a6512" stroke-width="1.5"/>`;
      case 'bigturban': return `<ellipse cx="60" cy="66" rx="54" ry="16" fill="${c}" stroke="${d}" stroke-width="3"/><ellipse cx="60" cy="48" rx="46" ry="20" fill="${l}" stroke="${d}" stroke-width="3"/><ellipse cx="60" cy="30" rx="32" ry="16" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M8 62 Q60 84 112 62 M18 46 Q60 68 102 46 M30 30 Q60 46 90 30" stroke="${d}" stroke-width="3" fill="none"/><circle cx="60" cy="64" r="6" fill="#c0392b" stroke="${gold}" stroke-width="2.5"/>`;
      case 'kalpak': return `<path d="M30 78 Q24 24 60 18 Q96 24 90 78 Z" fill="${c}" stroke="${d}" stroke-width="3"/><ellipse cx="60" cy="24" rx="28" ry="10" fill="${l}"/><g fill="${d}" opacity=".6"><circle cx="44" cy="46" r="3"/><circle cx="62" cy="40" r="3"/><circle cx="78" cy="50" r="3"/><circle cx="52" cy="62" r="3"/><circle cx="72" cy="66" r="3"/></g>`;
      case 'captain': return `<path d="M24 76 Q26 36 60 34 Q94 36 96 76 Z" fill="${c}" stroke="${d}" stroke-width="3"/><rect x="24" y="62" width="72" height="12" fill="${gold}" stroke="#8a6512" stroke-width="2"/><text x="60" y="56" text-anchor="middle" font-size="20" fill="#f2d27a">⚓</text><path d="M24 50 Q60 28 96 50" fill="none" stroke="#fff" stroke-width="3" opacity=".7"/>`;
      case 'vizier': return `<ellipse cx="60" cy="68" rx="50" ry="14" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M26 66 Q22 4 60 0 Q98 4 94 66 Z" fill="${c}" stroke="${d}" stroke-width="3"/><path d="M22 52 Q60 76 98 52 M28 34 Q60 52 92 34 M38 16 Q60 28 82 16" stroke="${d}" stroke-width="3" fill="none"/><rect x="26" y="58" width="68" height="9" fill="${gold}" stroke="#8a6512" stroke-width="2"/><path d="M60 -4 Q76 -12 82 4" stroke="${gold}" stroke-width="4" fill="none"/><circle cx="60" cy="62" r="6" fill="#c0392b" stroke="${gold}" stroke-width="2.5"/>`;
      case 'prince': return `<ellipse cx="60" cy="66" rx="50" ry="15" fill="${c}" stroke="${d}" stroke-width="3"/><ellipse cx="60" cy="44" rx="40" ry="22" fill="${l}" stroke="${d}" stroke-width="3"/><path d="M14 60 Q60 82 106 60 M24 42 Q60 62 96 42" stroke="${d}" stroke-width="3" fill="none"/><circle cx="60" cy="62" r="7" fill="#27ae60" stroke="${gold}" stroke-width="3"/><path d="M60 56 Q64 20 88 -8 Q70 14 64 38 Z" fill="${gold}" stroke="#8a6512" stroke-width="2"/>`;
      case 'sultan': return `<ellipse cx="60" cy="68" rx="58" ry="13" fill="${c}" stroke="#bda46a" stroke-width="3"/><ellipse cx="60" cy="48" rx="52" ry="22" fill="#fffaf0" stroke="#bda46a" stroke-width="3"/><ellipse cx="60" cy="26" rx="38" ry="18" fill="${c}" stroke="#bda46a" stroke-width="3"/><path d="M6 64 Q60 86 114 64 M14 46 Q60 70 106 46 M24 26 Q60 44 96 26" stroke="#bda46a" stroke-width="3" fill="none"/><rect x="8" y="58" width="104" height="10" fill="${gold}" stroke="#8a6512" stroke-width="2"/><path d="M40 12 L46 -8 L53 8 L60 -12 L67 8 L74 -8 L80 12 Z" fill="${gold}" stroke="#8a6512" stroke-width="2"/><circle cx="60" cy="62" r="8" fill="#c0392b" stroke="#fff0a6" stroke-width="3"/><path d="M60 8 Q84 -22 100 -6 Q80 -4 70 16 Z" fill="#fff0a6" stroke="#8a6512" stroke-width="2"/>`;
      default: return `<ellipse cx="60" cy="64" rx="40" ry="14" fill="${c}" stroke="${d}" stroke-width="3"/>`;
    }
  }
  const hatSVG = (look) => `<svg xmlns="http://www.w3.org/2000/svg" width="120" height="92" viewBox="0 -12 120 92">${hatInner(look.hat, look.hatColor || '#888', look.trim)}</svg>`;

  const SKINS = ['#e8b98f', '#d9a577', '#f0c8a0', '#c98f64', '#e2b184'];
  function faceSVG(i, ghost) {
    if (ghost) return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="#59483a"/><text x="50" y="68" text-anchor="middle" font-size="58" font-weight="700" fill="#e9d6a4" font-family="Georgia,serif">?</text></svg>`;
    const skin = SKINS[i % SKINS.length];
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="${skin}"/><circle cx="35" cy="44" r="5" fill="#2a1a10"/><circle cx="65" cy="44" r="5" fill="#2a1a10"/><path d="M26 62 Q50 52 74 62 Q62 74 50 66 Q38 74 26 62Z" fill="#3a2518"/><path d="M40 78 Q50 84 60 78" stroke="#8a3b2e" stroke-width="3" fill="none" stroke-linecap="round"/></svg>`;
  }

  // ------------------------------------------------------------------ GÖRÜNÜM SINIFI
  class RankGameView {
    constructor(engine, sc) {
      this.engine = engine;
      this.sc = sc;
      this.cfg = sc.game;
      this.ranks = this.cfg.ranks;
      this.rankById = new Map(this.ranks.map((r, i) => [r.id, Object.assign({ idx: i }, r)]));
      this.zones = (this.cfg.view && this.cfg.view.zones) || [];
      this.state = null;
      this.chars = new Map();
      this.overflow = new Map();
      this.pending = new Map();
      this.hold = new Set();
      this.destroyed = false;
      this.first = true;
      this.renderQueued = false;
      this.cineQueue = [];
      this.cineRunning = false;
      this.noticeQueue = [];
      this.noticeRunning = false;
      this.lastBanner = 0;
      this.lastSound = {};
      this.timers = new Set();

      this.sprites = {};
      for (const r of this.ranks) this.sprites[r.id] = { body: enc(bodySVG(r.look || {})), hat: enc(hatSVG(r.look || {})) };

      const stage = engine.el.stage;
      this.layer = el('div', 'g-layer');
      engine.el.world.appendChild(this.layer);
      this.board = el('div', 'g-board');
      this.throneStatus = el('div', 'g-throne-status');
      this.noticeBox = el('div', 'g-notices');
      stage.append(this.board, this.throneStatus, this.noticeBox);

      this.prevBannerTop = engine.el.banners.style.top;
      engine.el.banners.style.top = '330px'; // saray gökyüzü bölgesine

      this._countdown = setInterval(() => this._updateThroneStatus(), 1000);

      // Hediye ve rütbe rehberi (senaryoda `guide` bölümü varsa)
      this.guide = (this.cfg.guide && window.RankGameGuide && window.GuideData) ? new window.RankGameGuide(this) : null;
      // Taht mücadelesi (açık artırma) kartı: geri sayım, Padişah/Sadrazam, ilk 5 teklif
      const au = this.cfg.throne && this.cfg.throne.auction;
      this.auction = (au && au.enabled !== false && window.RankGameAuction) ? new window.RankGameAuction(this) : null;
    }

    destroy() {
      this.destroyed = true;
      if (this.guide) this.guide.destroy();
      if (this.auction) this.auction.destroy();
      clearInterval(this._countdown);
      this.timers.forEach((t) => clearTimeout(t));
      this.layer.remove(); this.board.remove(); this.throneStatus.remove(); this.noticeBox.remove();
      this.engine.el.stage.querySelectorAll('.g-cine, .g-carpet, .g-walker').forEach((n) => n.remove());
      this.engine.el.banners.style.top = this.prevBannerTop || '';
    }

    after(ms, fn) {
      const t = setTimeout(() => { this.timers.delete(t); if (!this.destroyed) fn(); }, ms);
      this.timers.add(t);
    }

    // ---------------------------------------------------------------- SUNUCUDAN GELEN DURUM
    onState(state) {
      if (this.destroyed || !state || state.scenario !== this.sc.id) return;
      this.state = state;
      if (this.guide && state.ui) this.guide.setVisible(state.ui.guideVisible); // yayıncının göster/gizle ayarı (kalıcı)
      if (this.auction) this.auction.onState(state);
      const e = this.engine;
      const s = state.stats;
      e.setCounter('treasury', s.treasury);
      e.setCounter('population', s.population);
      e.setCounter('army', s.army);
      e.setCounter('cities', s.cities);
      this._renderBoard();
      this._updateThroneStatus(true);
      this._scheduleRender();
    }

    _scheduleRender() {
      if (this.renderQueued) return;
      this.renderQueued = true;
      requestAnimationFrame(() => { this.renderQueued = false; if (!this.destroyed) this.render(); });
    }

    // ---------------------------------------------------------------- YERLEŞİM
    render() {
      const st = this.state;
      if (!st) return;
      const want = new Map();
      const overflowWant = new Map();

      for (const z of this.zones) {
        const inZone = st.users.filter((u) => z.ranks.includes(u.rank));
        if (z.pin && st.throne[z.pin]) { // örn. Sadrazam koltuğu: tahttan inen Padişah önce
          const i = inZone.findIndex((u) => u.id === st.throne[z.pin].id);
          if (i > 0) inZone.unshift(inZone.splice(i, 1)[0]);
        }
        const totalInZone = z.ranks.reduce((a, r) => a + (st.rankCounts[r] || 0), 0);
        const shown = inZone.slice(0, z.max);
        let npcN = 0;
        if (z.seat) npcN = shown.length ? 0 : 1;
        else if (z.npc) {
          const byPop = z.npc.perPopulation ? Math.ceil(st.stats.population / z.npc.perPopulation) : 0;
          const target = Math.min(z.max, Math.max(z.npc.count || 0, byPop));
          npcN = Math.max(0, target - shown.length);
        }
        const n = shown.length + npcN;
        const cols = Math.max(1, z.cols || 1);
        for (let i = 0; i < n; i++) {
          const row = Math.floor(i / cols);
          const inRow = Math.min(cols, n - row * cols);
          const col = i % cols;
          const x = z.x + (col - (inRow - 1) / 2) * (z.dx || 0);
          const y = z.y + row * (z.dy || 0);
          if (i < shown.length) {
            want.set('u:' + shown[i].id, { zone: z, x, y, user: shown[i], rankId: shown[i].rank });
          } else {
            const k = i - shown.length;
            let rankId; let name; let ghost = false;
            if (z.seat) { rankId = z.seat.rank; name = z.seat.name; ghost = true; } else {
              const rl = z.npc.ranks || [z.ranks[z.ranks.length - 1]];
              rankId = rl[k % rl.length]; name = (z.npc.names || [])[k % ((z.npc.names || [1]).length)] || this.rankById.get(rankId).name;
            }
            want.set(`n:${z.id}:${k}`, { zone: z, x, y, npc: true, ghost, rankId, name, seed: k });
          }
        }
        const extra = totalInZone - shown.length;
        if (extra > 0) {
          // taşan oyuncu sayısı: bölgenin ilk satırının üstünde, ortada
          overflowWant.set(z.id, { text: `+${fmt(extra)} daha`, x: z.x, y: z.y - 190 * z.scale - 16 });
        }
      }

      // kaldırılanlar
      for (const [key, c] of this.chars) {
        if (!want.has(key)) {
          this.chars.delete(key);
          c.el.classList.add('g-leave');
          this.after(450, () => c.el.remove());
        }
      }
      // eklenen / güncellenen
      for (const [key, d] of want) {
        let c = this.chars.get(key);
        if (!c) {
          c = this._createChar(key, d);
          this.chars.set(key, c);
          this.layer.appendChild(c.el);
          if (!this.first && !d.npc) c.el.classList.add('g-pop');
          else c.el.classList.add('g-fade');
          this.after(900, () => c.el.classList.remove('g-pop', 'g-fade'));
        }
        this._updateChar(c, d);
      }
      // taşma etiketleri
      for (const [zid, o] of overflowWant) {
        let node = this.overflow.get(zid);
        if (!node) { node = el('div', 'g-overflow'); this.layer.appendChild(node); this.overflow.set(zid, node); }
        node.textContent = o.text; node.style.left = o.x + 'px'; node.style.top = o.y + 'px';
      }
      for (const [zid, node] of this.overflow) if (!overflowWant.has(zid)) { node.remove(); this.overflow.delete(zid); }

      this.first = false;
      this._flushPending();
    }

    _createChar(key, d) {
      const s = d.zone.scale;
      const w = 128 * s; const h = 190 * s;
      const D = Math.round(Math.max(46, 80 * s));
      const root = el('div', 'g-char');
      root.style.width = w + 'px'; root.style.height = h + 'px';
      const inner = el('div', 'g-inner');
      inner.style.animationDelay = (-Math.random() * 2.4).toFixed(2) + 's';
      const body = el('img', 'g-body'); body.draggable = false;
      const head = el('div', 'g-head');
      head.style.cssText = `width:${D}px;height:${D}px;left:${(w - D) / 2}px;top:${82 * s - D}px;border-width:${Math.max(2, Math.round(3 * s + 1))}px`;
      const face = el('img'); face.draggable = false; head.appendChild(face);
      const hatW = D * 1.5; const hatH = hatW * 92 / 120;
      const hat = el('img', 'g-hat'); hat.draggable = false;
      hat.style.cssText = `width:${hatW}px;height:${hatH}px;left:${(w - hatW) / 2}px;top:${82 * s - D + D * 0.3 - hatH * (80 + 12) / 92}px`;
      const prop = el('div', 'g-prop'); prop.style.cssText = `font-size:${Math.max(14, 34 * s)}px;left:${w * 0.74}px;top:${h * 0.5}px`;
      const badge = el('div', 'g-badge'); badge.style.cssText = `font-size:${Math.max(14, 26 * s)}px;left:${w * 0.62}px;top:${82 * s - D - 6}px`;
      inner.append(body, head, hat, prop, badge);
      const label = el('div', 'g-label ' + d.zone.label);
      label.style.top = (h + 1) + 'px';
      label.style.maxWidth = (d.zone.dx ? Math.max(78, d.zone.dx - 4) : 170) + 'px';
      label.innerHTML = '<div class="g-name"></div><div class="g-sub"></div>';
      root.append(inner, label);
      return { key, el: root, inner, body, head, face, hat, prop, badge, label, name: label.firstChild, sub: label.lastChild, rankId: null, w, h, x: 0, y: 0, userId: d.user ? d.user.id : null, avatar: null };
    }

    _updateChar(c, d) {
      const r = this.rankById.get(d.rankId);
      const look = r.look || {};
      c.x = d.x; c.y = d.y;
      c.el.style.left = (d.x - c.w / 2) + 'px';
      c.el.style.top = (d.y - c.h) + 'px';
      c.el.style.zIndex = String(100 + Math.round(d.y) + (d.zone.seat ? 400 : 0));
      c.el.classList.toggle('g-ghost', !!d.ghost);
      c.el.classList.toggle('g-npc', !!d.npc);
      c.el.classList.toggle('g-royal', d.rankId === 'padisah' || r.fx === 'coronation');
      c.el.classList.toggle('g-hold', !!(d.user && this.hold.has(d.user.id)));
      if (c.rankId !== d.rankId) {
        c.rankId = d.rankId;
        c.body.src = this.sprites[d.rankId].body;
        c.hat.src = this.sprites[d.rankId].hat;
        c.prop.textContent = d.ghost ? '' : (look.prop || '');
        c.badge.textContent = d.zone.label === 'full' && !d.ghost ? r.icon : '';
        c.el.style.setProperty('--rank-color', look.trim || '#c9a227');
      }
      if (d.user) {
        if (c.avatar !== d.user.avatar) {
          c.avatar = d.user.avatar;
          c.face.onerror = () => { c.face.onerror = null; c.face.src = `/avatar/fake/${encodeURIComponent(d.user.uniqueId || 'x')}.svg`; };
          c.face.src = d.user.avatar;
        }
        c.name.textContent = d.user.name;
        c.sub.textContent = d.zone.label === 'full' ? `${r.icon} ${r.name} · ${fmt(d.user.points)}` : '';
      } else if (c.avatar !== 'npc' + d.seed + d.ghost) {
        c.avatar = 'npc' + d.seed + d.ghost;
        c.face.src = enc(faceSVG(d.seed || 0, d.ghost));
        c.name.textContent = d.ghost ? d.name : '';
        c.sub.textContent = '';
        c.label.classList.toggle('g-hidden', !d.ghost);
      }
    }

    // ---------------------------------------------------------------- ŞEREF LİSTESİ + TAHT DURUMU
    _renderBoard() {
      // "En yüksek puanlı oyuncular": rütbeden bağımsız, puana göre ilk N
      const n = (this.cfg.view && this.cfg.view.leaderboardTop) || 5;
      const top = (this.state.topPoints || this.state.users || []).slice(0, n);
      this.board.innerHTML = '<div class="g-board-cap">🏆 EN YÜKSEK PUAN</div>';
      top.forEach((u, i) => {
        const r = this.rankById.get(u.rank);
        const chip = el('div', 'g-chip' + (i === 0 ? ' first' : ''));
        chip.innerHTML = `<span class="gc-pos">${i + 1}</span><img><span class="gc-text"><b>${esc(u.name)}</b><small>${r.icon} ${esc(r.name)} · ${fmt(u.points)}</small></span>`;
        const img = chip.querySelector('img');
        img.onerror = () => { img.onerror = null; img.src = `/avatar/fake/${encodeURIComponent(u.uniqueId || 'x')}.svg`; };
        img.src = u.avatar;
        this.board.appendChild(chip);
      });
    }

    /** Alt şerit: açık artırma sürüyorsa çağrı, değilse bir sonraki mücadeleye kalan süre */
    _updateThroneStatus(fresh) {
      if (!this.state || !this.state.auction) return;
      const a = this.state.auction;
      if (fresh) this.skew = a.serverNow - Date.now();
      const now = Date.now() + (this.skew || 0);
      if (!a.enabled) this.throneStatus.textContent = '👑 Taht açık artırması kapalı';
      else if (a.phase === 'active') this.throneStatus.textContent = '⚔️ AÇIK ARTIRMA SÜRÜYOR · hediye göndererek teklif ver!';
      else {
        const s = Math.max(0, Math.ceil((a.nextAt - now) / 1000));
        this.throneStatus.textContent = `👑 Sonraki taht mücadelesi: ${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
      }
    }

    // ---------------------------------------------------------------- SUNUCUDAN GELEN OLAYLAR (animasyonlar)
    onFx(fx) {
      if (this.destroyed || !fx || fx.scenario !== this.sc.id) return;
      switch (fx.type) {
        case 'join': return this._fxJoin(fx);
        case 'rankup': return this._fxRankUp(fx);
        case 'demote': return this._fxDemote(fx);
        case 'coronation': return this._enqueueCine(() => this._cineCoronation(fx)); // yönetici (zorla) taç giydirme
        case 'auction': return this.auction ? this.auction.onFx(fx) : undefined;
        case 'gift': if (this.guide) this.guide.flashGift(fx); return this._fxGift(fx);
        case 'guide': return this.guide ? this.guide.onFx(fx) : undefined;
        case 'notice': return this._queueNotice(fx);
        case 'city': return this._fxCity(fx);
        case 'job': return this._fxJob(fx);
        default: return undefined;
      }
    }

    _sound(name, minGap = 80) {
      const t = performance.now();
      if (t - (this.lastSound[name] || 0) < minGap) return;
      this.lastSound[name] = t;
      this.engine.audio.play(name);
    }

    _banner(text, style, user, duration, force) {
      const t = performance.now();
      if (!force && t - this.lastBanner < 700) return; // yoğun akışta ekranı bannerla doldurma
      this.lastBanner = t;
      this.engine.runAction('banner', { text, style, duration: duration || 2.6 }, { user });
    }

    _withChar(userId, fn) {
      const c = this.chars.get('u:' + userId);
      if (c) fn(c);
      else {
        if (!this.pending.has(userId)) this.pending.set(userId, []);
        this.pending.get(userId).push({ fn, at: Date.now() });
      }
    }

    _flushPending() {
      for (const [uid, list] of this.pending) {
        const c = this.chars.get('u:' + uid);
        if (c) { list.forEach((p) => p.fn(c)); this.pending.delete(uid); } else {
          const fresh = list.filter((p) => Date.now() - p.at < 4000);
          if (fresh.length) this.pending.set(uid, fresh); else this.pending.delete(uid);
        }
      }
    }

    _float(c, text, cls) {
      const f = el('div', 'g-float ' + (cls || ''));
      f.textContent = text;
      f.style.left = (c.w / 2) + 'px';
      f.style.top = '-30px';
      c.el.appendChild(f);
      f.animate([{ transform: 'translate(-50%,0) scale(.6)', opacity: 0 }, { transform: 'translate(-50%,-28px) scale(1.1)', opacity: 1, offset: 0.2 }, { transform: 'translate(-50%,-90px) scale(1)', opacity: 0 }], { duration: 1900, easing: 'ease-out' }).onfinish = () => f.remove();
    }

    _glow(c, ms) { c.el.classList.add('g-glow'); this.after(ms, () => c.el.classList.remove('g-glow')); }
    _pulse(c) { c.inner.classList.remove('g-pulse'); void c.inner.offsetWidth; c.inner.classList.add('g-pulse'); this.after(800, () => c.inner.classList.remove('g-pulse')); }
    _burstAt(c, emoji, count) { this.engine.runAction('burst', { emoji, count, x: c.x, y: c.y - c.h * 0.6, spread: 0.45 }); }

    _fxJoin(fx) {
      this._sound('join', 120);
      this._withChar(fx.user.id, (c) => this._float(c, '🌾 +', 'g-join'));
      if (this.guide && !this.first) this.guide.welcome(fx.user); // ücretsiz katılım bilgisi
      if (!this.first) this._banner(`🌾 ${fx.user.name} krallığa katıldı!`, 'small', fx.user, 2.2);
    }

    _fxRankUp(fx) {
      const r = this.rankById.get(fx.to);
      const tier = r.fx || 'minor';
      if (tier === 'vizier') { this._enqueueCine(() => this._cineVizier(fx, r)); return; }
      this._withChar(fx.user.id, (c) => {
        this._float(c, `↑ ${r.name}`, 'g-rank');
        this._pulse(c);
        if (tier === 'minor') { this._burstAt(c, [r.icon], 6); } else {
          this._glow(c, tier === 'prince' ? 4500 : 2600);
          this._burstAt(c, [r.icon, '✨', '⭐'], tier === 'prince' ? 30 : 18);
        }
      });
      if (tier === 'minor') {
        this._sound('rank', 100);
        this._banner(`${r.icon} ${fx.user.name} → ${r.name}`, 'small', fx.user, 2.2);
      } else {
        this._sound('rankBig', 200);
        this.engine.runAction('shake', { intensity: tier === 'prince' ? 12 : 6, duration: 0.6 });
        if (tier === 'prince') this.engine.runAction('flash', { color: 'rgba(120,255,200,0.45)', duration: 0.8 });
        this._banner(`${r.icon} ${fx.user.name} ${r.name} oldu!`, 'big', fx.user, 3.4, true);
      }
    }

    _fxDemote(fx) {
      const r = this.rankById.get(fx.to);
      this._withChar(fx.user.id, (c) => this._float(c, `↓ ${r.name}`, 'g-down'));
      this._banner(`⬇️ ${fx.user.name} → ${r.name}`, 'small', fx.user, 2.4, true);
    }

    _fxGift(fx) {
      const g = fx.gift || {};
      this._withChar(fx.user.id, (c) => {
        this._float(c, `🎁 ${g.name}${g.count > 1 ? ' ×' + g.count : ''}`, 'g-gift');
        this._burstAt(c, ['🪙', '✨'], Math.min(16, 3 + Math.floor(g.diamonds / 30)));
      });
      if (!fx.rankChanged && !fx.crowned) {
        this._sound('rank', 120);
        this._banner(`🎁 ${fx.user.name}: ${g.name}${g.count > 1 ? ' ×' + g.count : ''} (+${fmt(g.diamonds)} 💎)`, 'small', fx.user, 2.2);
      }
      if (g.diamonds >= 1000) this.engine.runAction('flash', { color: 'rgba(255,220,120,0.4)', duration: 0.6 });
    }

    _fxCity(fx) {
      this._sound('city', 300);
      this.engine.runAction('burst', { emoji: ['🏙️', '🏰', '✨'], count: 20, x: 540, y: 600, spread: 1 });
      this._banner(`🏙️ Yeni şehir kazanıldı: ${fx.name}!`, 'big', null, 3.2, true);
    }

    _fxJob(fx) {
      this._sound('rank', 100);
      this._withChar(fx.user.id, (c) => { this._float(c, `${fx.icon} ${fx.label}`, 'g-job'); this._pulse(c); });
      this._banner(`${fx.icon} ${fx.user.name} artık ${fx.label}`, 'small', fx.user, 2.2);
    }

    // ---------------------------------------------------------------- BİLDİRİM KARTLARI (!rütbe, !ordu, !hazine ...)
    _queueNotice(fx) {
      if (this.noticeQueue.length >= 4) this.noticeQueue.shift();
      this.noticeQueue.push(fx);
      if (!this.noticeRunning) this._runNotice();
    }

    async _runNotice() {
      this.noticeRunning = true;
      while (this.noticeQueue.length && !this.destroyed) {
        const fx = this.noticeQueue.shift();
        const card = el('div', 'g-notice ' + (fx.kind || ''));
        const avatar = fx.user ? `<img class="gn-av">` : '';
        card.innerHTML = `${avatar}<div class="gn-icon">${esc(fx.icon || '📜')}</div><div class="gn-body"><div class="gn-title">${esc(fx.title)}</div>${(fx.lines || []).map((l) => `<div class="gn-line">${esc(l)}</div>`).join('')}</div>`;
        const img = card.querySelector('.gn-av');
        if (img) { img.onerror = () => img.remove(); img.src = fx.user.avatar; }
        this.noticeBox.appendChild(card);
        this._sound('notice', 150);
        const anim = card.animate([{ transform: 'translateY(-40px) scale(.9)', opacity: 0 }, { transform: 'none', opacity: 1, offset: 0.1 }, { transform: 'none', opacity: 1, offset: 0.88 }, { transform: 'translateY(-20px)', opacity: 0 }], { duration: 4300, easing: 'ease-out' });
        await anim.finished.catch(() => {});
        card.remove();
      }
      this.noticeRunning = false;
    }

    // ---------------------------------------------------------------- SİNEMATİK SIRASI
    _enqueueCine(fn) {
      if (this.cineQueue.length >= 3) this.cineQueue.shift();
      this.cineQueue.push(fn);
      if (!this.cineRunning) this._runCine();
    }

    async _runCine() {
      this.cineRunning = true;
      while (this.cineQueue.length && !this.destroyed) {
        const fn = this.cineQueue.shift();
        try { await fn(); } catch (err) { console.error('[sinematik]', err); }
      }
      this.cineRunning = false;
    }

    _entitySeat(zoneId) { return this.zones.find((z) => z.id === zoneId); }

    // ---------------------------------------------------------------- TAÇ GİYME (PADİŞAH)
    /** Açık artırma sonucu: kazanan varsa taç giyme sinematiği, yoksa kısa duyuru */
    async _cineAuctionEnd(fx) {
      if (!fx.winner) {
        this._sound('gong', 0);
        this.engine.runAction('banner', { text: '⚔️ Açık artırma bitti: teklif gelmedi, taht değişmedi.', style: 'big', duration: 4 }, {});
        return;
      }
      await this._cineCoronation(fx);
    }

    async _cineCoronation(fx) {
      const e = this.engine;
      const u = fx.winner || fx.user;
      this.hold.add(u.id);
      this.render();

      const wrap = el('div', 'g-cine g-crown');
      wrap.innerHTML = `<div class="gc-dim"></div><div class="gc-rays"></div>
        <div class="gc-portrait"><img><div class="gc-crown">👑</div></div>
        <div class="gc-title"></div><div class="gc-name"></div><div class="gc-prev"></div><div class="gc-top"></div>`;
      wrap.querySelector('.gc-title').textContent = fx.retained ? 'TAHTINI KORUDU' : 'PADİŞAH';
      wrap.querySelector('.gc-name').textContent = u.name;
      const prevLines = [];
      if (fx.bid) prevLines.push(`${fmt(fx.bid)} puanlık teklifle`);
      if (fx.previous) prevLines.push(`${fx.previous.name} tahttan indi → ${this.rankById.get(fx.demotedTo).name}`);
      wrap.querySelector('.gc-prev').textContent = prevLines.join(' · ');
      if (fx.top && fx.top.length > 1) wrap.querySelector('.gc-top').textContent = fx.top.map((t, i) => `${i + 1}. ${t.name} ${fmt(t.bid)}`).join('   ');
      const img = wrap.querySelector('.gc-portrait img');
      img.onerror = () => { img.onerror = null; img.src = `/avatar/fake/${encodeURIComponent(u.uniqueId || 'x')}.svg`; };
      img.src = u.avatar;
      e.el.stage.appendChild(wrap);

      e.audio.play('gong');
      e.audio.play('crown');
      requestAnimationFrame(() => wrap.classList.add('on'));
      await sleep(800);
      e.runAction('entity_anim', { target: 'throne', anim: 'pulse', seconds: 5 });
      e.runAction('move_entity', { target: 'throne', dy: -26, duration: 0.8 });
      e.runAction('burst', { emoji: ['✨', '🪙'], count: 24, x: 540, y: 820, spread: 1.2 });
      await sleep(700);
      wrap.classList.add('landed');            // taç başa iner
      e.runAction('shake', { intensity: 16, duration: 0.8 });
      e.runAction('flash', { color: 'rgba(255,225,130,0.85)', duration: 1.0 });
      e.runAction('burst', { emoji: ['👑', '✨', '⭐', '🪙'], count: 56, x: 540, y: 760, spread: 1.6 });
      e.runAction('banner', { text: fx.retained ? `👑 ${u.name} TAHTINI KORUDU! 👑` : `👑 ${u.name} PADİŞAH OLDU! 👑`, style: 'mega', duration: 4.6 }, { user: u });
      await sleep(1300);
      e.runAction('burst', { emoji: ['🪙', '✨', '🎉'], count: 50, x: 540, y: 700, spread: 1.8 });
      await sleep(1700);
      e.runAction('move_entity', { target: 'throne', dy: 26, duration: 1.0 });
      await sleep(900);
      wrap.classList.remove('on');
      await sleep(700);
      wrap.remove();
      this.hold.delete(u.id);
      this.render();
      this._withChar(u.id, (c) => { this._glow(c, 6000); this._pulse(c); this._burstAt(c, ['👑', '✨'], 18); });
    }

    // ---------------------------------------------------------------- SADRAZAM'IN SARAYA GİRİŞİ
    async _cineVizier(fx, r) {
      const e = this.engine;
      const u = fx.user;
      const seat = this._entitySeat('vizier') || { x: 300, y: 990, scale: 0.95 };
      this.hold.add(u.id);
      this.render();

      const carpet = el('div', 'g-carpet');
      e.el.stage.appendChild(carpet);
      carpet.animate([{ transform: 'scaleY(0)', opacity: 0 }, { transform: 'scaleY(1)', opacity: 1 }], { duration: 1000, easing: 'ease-out', fill: 'forwards' });

      const s = seat.scale;
      const walker = el('div', 'g-walker');
      const w = 128 * s; const h = 190 * s; const D = Math.round(Math.max(46, 80 * s));
      walker.style.cssText = `width:${w}px;height:${h}px;`;
      const hatW = D * 1.5; const hatH = hatW * 92 / 120;
      walker.innerHTML = `<div class="g-inner g-walk"><img class="g-body" src="${this.sprites[r.id].body}">
        <div class="g-head" style="width:${D}px;height:${D}px;left:${(w - D) / 2}px;top:${82 * s - D}px"><img></div>
        <img class="g-hat" src="${this.sprites[r.id].hat}" style="width:${hatW}px;height:${hatH}px;left:${(w - hatW) / 2}px;top:${82 * s - D + D * 0.3 - hatH * 92 / 92}px"></div>
        <div class="g-label full" style="top:${h + 1}px"><div class="g-name"></div><div class="g-sub"></div></div>`;
      walker.querySelector('.g-name').textContent = u.name;
      walker.querySelector('.g-sub').textContent = `${r.icon} ${r.name}`;
      const face = walker.querySelector('.g-head img');
      face.onerror = () => { face.onerror = null; face.src = `/avatar/fake/${encodeURIComponent(u.uniqueId || 'x')}.svg`; };
      face.src = u.avatar;
      e.el.stage.appendChild(walker);

      const startX = 540 - w / 2; const startY = 1900 - h;
      const gateY = 1010 - h;
      const endX = seat.x - w / 2; const endY = seat.y - h;
      walker.style.left = '0px'; walker.style.top = '0px';
      e.audio.play('gong');
      e.audio.play('vizier');
      await sleep(500);
      e.runAction('banner', { text: `📜 SADRAZAM ${u.name} saraya giriş yaptı!`, style: 'big', duration: 4.2 }, { user: u });
      const walk = walker.animate([
        { transform: `translate(${startX}px, ${startY}px)` },
        { transform: `translate(${startX}px, ${gateY}px)`, offset: 0.72 },
        { transform: `translate(${endX}px, ${endY}px)` },
      ], { duration: 3600, easing: 'ease-in-out', fill: 'forwards' });
      this.after(1800, () => e.runAction('burst', { emoji: ['✨', '🪙'], count: 14, x: 540, y: 1250, spread: 0.6 }));
      await walk.finished.catch(() => {});
      e.runAction('flash', { color: 'rgba(255,215,120,0.5)', duration: 0.7 });
      e.runAction('shake', { intensity: 7, duration: 0.5 });
      e.runAction('burst', { emoji: ['📜', '✨', '⭐'], count: 26, x: seat.x, y: seat.y - 120, spread: 0.9 });
      walker.remove();
      this.hold.delete(u.id);
      this.render();
      this._withChar(u.id, (c) => { this._glow(c, 5000); this._pulse(c); });
      carpet.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 900, fill: 'forwards' }).onfinish = () => carpet.remove();
      await sleep(600);
    }
  }

  window.RankGameView = RankGameView;
})();
