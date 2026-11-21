'use strict';
/**
 * SENARYO MOTORU (Scenario Engine)
 * --------------------------------
 * Bir senaryo nesnesini (scenarios/*.js) alır ve şunları yapar:
 *   - arka planı, sabit karakterleri (entities), HUD'u kurar
 *   - gelen TikTok olaylarını (gift/like/comment/follow/share/join) tetikleyicilerle eşleştirir
 *   - eşleşen aksiyonları çalıştırır (karakter ekle, ses çal, ekran salla, sayaç artır ...)
 *   - sayaçları -> tempo / ses / zoom / nesne konumu gibi değerlere bağlar (mappings)
 * Yeni bir konsept = yeni senaryo dosyası. Bu dosyaya dokunmak gerekmez.
 */
(function () {
  const W = 1080;
  const H = 1920;

  const ACTION_ALIASES = {
    ekrana_yeni_karakter_ekle: 'spawn_character',
    nesneyi_yukari_hareket_ettir: 'move_entity',
    nesneyi_hareket_ettir: 'move_entity',
    ses_cal: 'play_sound',
    ekran_salla: 'shake',
    sayac_artir: 'counter_add',
    sayac_ayarla: 'counter_set',
    yazi_goster: 'banner',
    parcacik_patlat: 'burst',
    ekran_flas: 'flash',
    gecici_hizlan: 'temp_boost',
    hepsini_ziplat: 'jump_all',
    varlik_animasyonu: 'entity_anim',
    hediye_goster: 'gift_popup',
    sahneyi_sifirla: 'reset_scene',
  };

  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  const lerp = (a, b, t) => a + (b - a) * t;
  const rand = (a, b) => a + Math.random() * (b - a);
  const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];
  const hash = (s) => { let h = 0; for (const c of String(s)) h = (h * 31 + c.charCodeAt(0)) >>> 0; return h; };

  // Türkçe karakterleri sadeleştirip küçük harfe çevirir ("!ZIPLA" == "!zıpla")
  const norm = (s) => String(s ?? '')
    .toLocaleLowerCase('tr')
    .replace(/ı/g, 'i').replace(/ş/g, 's').replace(/ç/g, 'c').replace(/ğ/g, 'g').replace(/ö/g, 'o').replace(/ü/g, 'u')
    .trim();

  const asArray = (v) => (v === undefined || v === null ? [] : Array.isArray(v) ? v : [v]);

  class SceneEngine {
    constructor({ audio, options = {} }) {
      this.audio = audio;
      this.options = options;
      this.el = {
        stage: document.getElementById('stage'),
        shaker: document.getElementById('shaker'),
        bg: document.getElementById('bg'),
        camera: document.getElementById('camera'),
        world: document.getElementById('world'),
        fx: document.getElementById('fx'),
        hud: document.getElementById('hud'),
        banners: document.getElementById('banners'),
        feed: document.getElementById('feed'),
      };
      this.sc = null;
      this.last = performance.now();
      this.time = 0;
      this.phase = 0;
      this.tempo = 1;
      this.volume = 1;
      this.zoom = 1;
      this.zoomTarget = 1;
      this.shakeState = { amp: 0, until: 0, dur: 1 };
      this.particles = 0;
      this._audioTick = 0;
      this._sendTick = 0;
      this._reset();
      requestAnimationFrame((t) => this._loop(t));
    }

    // ---------------------------------------------------------------- KURULUM
    _reset() {
      this.counters = {};
      this.members = new Map();
      this.slots = [];
      this.entities = new Map();
      this.boosts = [];
      this.mapped = {};
      this.acc = new Map();
      this.cooldowns = new Map();
      this.hudItems = {};
      this.ringEls = [];
      this.stars = null;
      this._counterDepth = 0;
    }

    async loadScenario(sc) {
      this.sc = sc;
      this._reset();
      const { world, bg, hud, banners, feed, fx, camera } = this.el;
      [world, bg, hud, banners, feed, fx].forEach((n) => { n.innerHTML = ''; });
      this.tempo = 1; this.volume = 1; this.zoom = 1; this.zoomTarget = 1;
      camera.style.transformOrigin = '540px 960px';

      this._buildBackground(sc.background || {});
      this._buildHud(sc);
      this._buildRings(sc);
      this._buildEntities(sc);
      for (const [k, v] of Object.entries(sc.counters || {})) this.counters[k] = v;
      this._refreshHud(true);
      this._applyMappings();
      this.entities.forEach((e) => { e.x = e.tx; e.y = e.ty; e.scale = e.tscale; e.rotate = e.trotate; e.opacity = e.topacity; });

      const cam = sc.camera || {};
      if (cam.focus) camera.style.transformOrigin = `${cam.focus.x}px ${cam.focus.y}px`;
      this.zoom = this.zoomTarget = cam.zoom || 1;

      await this.audio.load(sc.assets || {});
      this.audio.setTempo(1);
      this.audio.setMusicVolume(1);
      if (this.audio.running) this.audio.startMusic();
    }

    resolveAsset(ref) {
      if (!ref) return '';
      const map = (this.sc && this.sc.assets && this.sc.assets.images) || {};
      return map[ref] || ref;
    }

    // ---------------------------------------------------------------- ARKA PLAN
    _buildBackground(bgCfg) {
      const bg = this.el.bg;
      bg.style.display = this.options.transparent ? 'none' : 'block';
      bg.style.background = '';
      if (bgCfg.type === 'color') bg.style.background = bgCfg.color || '#000';
      else if (bgCfg.type === 'image') bg.style.cssText += `;background:#000 url("${bgCfg.image}") center/cover no-repeat;`;
      else if (bgCfg.type === 'video') {
        const v = document.createElement('video');
        v.src = bgCfg.video; v.autoplay = true; v.loop = true; v.muted = true; v.playsInline = true;
        v.className = 'bg-video';
        bg.appendChild(v);
        v.play().catch(() => {});
      } else {
        const colors = bgCfg.colors || ['#111', '#333'];
        bg.style.background = `linear-gradient(${bgCfg.angle ?? 180}deg, ${colors.join(', ')})`;
      }

      if (bgCfg.floor) {
        const f = bgCfg.floor;
        const d = document.createElement('div');
        d.className = 'bg-floor';
        d.style.cssText = `left:${f.x ?? 540}px;top:${f.y}px;width:${f.rx * 2}px;height:${f.ry * 2}px;background:radial-gradient(closest-side, ${f.color}, transparent);`;
        bg.appendChild(d);
      }

      if (bgCfg.starfield) {
        const c = document.createElement('canvas');
        c.width = W; c.height = H; c.className = 'bg-stars';
        bg.appendChild(c);
        this.stars = {
          ctx: c.getContext('2d'),
          list: Array.from({ length: 170 }, () => ({ x: rand(0, W), y: rand(0, H), z: rand(0.25, 1), tw: rand(0, 6) })),
        };
      }
    }

    // ---------------------------------------------------------------- HUD
    _buildHud(sc) {
      for (const h of sc.hud || []) {
        const item = document.createElement('div');
        item.className = 'hud-item';
        item.innerHTML = `<div class="hud-icon">${h.icon || ''}</div>
          <div class="hud-text"><div class="hud-label">${h.label || h.counter}</div><div class="hud-value">0</div></div>
          ${h.bar ? '<div class="hud-bar"><div class="hud-bar-fill"></div></div>' : ''}`;
        this.el.hud.appendChild(item);
        this.hudItems[h.counter] = {
          cfg: h, el: item,
          value: item.querySelector('.hud-value'),
          fill: item.querySelector('.hud-bar-fill'),
          shown: 0,
        };
      }
    }

    _refreshHud(instant = false) {
      for (const [name, it] of Object.entries(this.hudItems)) {
        const target = this.counters[name] || 0;
        if (instant) it.shown = target;
        it.target = target;
      }
    }

    // ---------------------------------------------------------------- SAYAÇLAR
    getCounter(name) { return this.counters[name] || 0; }

    setCounter(name, value, ev) {
      const prev = this.getCounter(name);
      this.counters[name] = value;
      this._refreshHud();
      this._applyMappings();
      if (this._counterDepth > 4) return;
      this._counterDepth++;
      for (const tr of this.sc.triggers || []) {
        if (tr.enabled === false || tr.on !== 'counter' || tr.counter !== name) continue;
        if (typeof tr.gte === 'number' && prev < tr.gte && value >= tr.gte) this._runActions(tr.actions, ev || { type: 'counter' });
        if (typeof tr.lte === 'number' && prev > tr.lte && value <= tr.lte) this._runActions(tr.actions, ev || { type: 'counter' });
      }
      this._counterDepth--;
    }

    // ---------------------------------------------------------------- MAPPINGS (sayaç -> efekt)
    _applyMappings() {
      const out = {};
      for (const m of this.sc.mappings || []) {
        const v = this.getCounter(m.counter);
        const [a0, a1] = m.from;
        let t = a1 === a0 ? 1 : clamp((v - a0) / (a1 - a0), 0, 1);
        if (m.ease === 'out') t = 1 - (1 - t) * (1 - t);
        else if (m.ease === 'in') t = t * t;
        out[m.target] = lerp(m.to[0], m.to[1], t);
      }
      this.mapped = out;
      for (const [target, val] of Object.entries(out)) {
        const mt = /^entity:([\w-]+)\.(x|y|scale|rotate|opacity)$/.exec(target);
        if (mt) {
          const e = this.entities.get(mt[1]);
          if (e) e['t' + mt[2]] = val;
        }
      }
    }

    getValue(target, fallback) {
      let v = target in this.mapped ? this.mapped[target] : fallback;
      const now = performance.now();
      // Geçici hızlandırmalar toplanır ama sınırlıdır (limits.maxBoost) ki sistem kontrolden çıkmasın
      let boost = 0;
      for (const b of this.boosts) if (b.target === target && b.until > now) boost += b.add;
      const limits = (this.sc && this.sc.limits) || {};
      const cap = (limits.maxBoost && limits.maxBoost[target]) ?? (target === 'tempo' ? 1.0 : target === 'bgspeed' ? 8 : Infinity);
      v += Math.min(boost, cap);
      if (limits[target]) v = clamp(v, limits[target][0], limits[target][1]);
      else if (target === 'tempo') v = clamp(v, 0.5, 3);
      return v;
    }

    // ---------------------------------------------------------------- SABİT VARLIKLAR
    _buildEntities(sc) {
      for (const def of sc.entities || []) {
        const wrap = document.createElement('div');
        wrap.className = 'entity';
        wrap.style.width = def.width + 'px';
        wrap.style.height = def.height + 'px';
        wrap.style.zIndex = def.z ?? 10;
        const inner = document.createElement('div');
        inner.className = 'entity-inner';
        if (def.lottie) {
          this._loadLottie().then((lottie) => {
            if (lottie) lottie.loadAnimation({ container: inner, renderer: 'svg', loop: true, autoplay: true, path: def.lottie });
          });
        } else {
          const img = document.createElement('img');
          img.src = this.resolveAsset(def.image);
          img.draggable = false;
          inner.appendChild(img);
        }
        wrap.appendChild(inner);
        if (def.label) {
          const l = document.createElement('div');
          l.className = 'entity-label';
          l.textContent = def.label;
          wrap.appendChild(l);
        }
        this.el.world.appendChild(wrap);
        this.entities.set(def.id, {
          def, wrap, inner,
          x: def.x, y: def.y, scale: def.scale ?? 1, rotate: 0, opacity: def.opacity ?? 1,
          tx: def.x, ty: def.y, tscale: def.scale ?? 1, trotate: 0, topacity: def.opacity ?? 1,
          speed: 3, jumpUntil: 0, tmp: null,
        });
      }
    }

    _loadLottie() {
      if (window.lottie) return Promise.resolve(window.lottie);
      if (this._lottiePromise) return this._lottiePromise;
      this._lottiePromise = new Promise((resolve) => {
        const s = document.createElement('script');
        s.src = 'https://cdnjs.cloudflare.com/ajax/libs/lottie-web/5.12.2/lottie.min.js';
        s.onload = () => resolve(window.lottie);
        s.onerror = () => { console.warn('[lottie] yüklenemedi (internet gerekli)'); resolve(null); };
        document.head.appendChild(s);
      });
      return this._lottiePromise;
    }

    // ---------------------------------------------------------------- HALKA DÜZENİ (ring)
    _ringCfg() {
      const c = (this.sc.crowd && this.sc.crowd.ring) || {};
      return { cx: c.cx ?? 540, cy: c.cy ?? 1000, firstRx: c.firstRx ?? 250, stepRx: c.stepRx ?? 175, ratioY: c.ratioY ?? 1.3, spacing: c.spacing ?? 138 };
    }

    _ringCapacity(r) {
      const c = this._ringCfg();
      const a = c.firstRx + r * c.stepRx;
      const b = a * c.ratioY;
      const perim = 2 * Math.PI * Math.sqrt((a * a + b * b) / 2);
      return Math.max(6, Math.floor(perim / c.spacing));
    }

    _ringPos(slot) {
      const c = this._ringCfg();
      let r = 0;
      let rest = slot;
      while (rest >= this._ringCapacity(r)) { rest -= this._ringCapacity(r); r++; }
      const cap = this._ringCapacity(r);
      const rx = c.firstRx + r * c.stepRx;
      const ry = rx * c.ratioY;
      const sign = rest % 2 === 1 ? 1 : -1;
      const ang = Math.PI / 2 + sign * Math.ceil(rest / 2) * (2 * Math.PI / cap) + (r % 2 ? Math.PI / cap : 0);
      return { x: c.cx + rx * Math.cos(ang), y: c.cy + ry * Math.sin(ang), ring: r, angle: ang };
    }

    _buildRings(sc) {
      if (!sc.crowd || sc.crowd.layout !== 'ring') return;
      const c = this._ringCfg();
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('class', 'ring-svg');
      svg.setAttribute('width', W); svg.setAttribute('height', H);
      svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
      this.el.world.appendChild(svg);
      this.ringSvg = svg;
      this.ringEls = [];
      for (let r = 0; r < 9; r++) {
        const rx = c.firstRx + r * c.stepRx;
        const e = document.createElementNS('http://www.w3.org/2000/svg', 'ellipse');
        e.setAttribute('cx', c.cx); e.setAttribute('cy', c.cy + 70);
        e.setAttribute('rx', rx); e.setAttribute('ry', rx * c.ratioY);
        e.setAttribute('class', 'ring-line');
        e.style.opacity = 0;
        svg.appendChild(e);
        this.ringEls.push(e);
      }
    }

    _updateRings() {
      if (!this.ringEls.length) return;
      const used = new Set();
      for (const m of this.members.values()) if (m.ring !== undefined) used.add(m.ring);
      this.ringEls.forEach((e, r) => { e.style.opacity = used.has(r) ? 1 : 0; });
    }

    // ---------------------------------------------------------------- KARAKTER (KALABALIK) EKLEME
    _scatterPos() {
      const s = this.sc.crowd.scatter || { x: [100, 980], y: [300, 1500], minDistance: 180 };
      const pts = [...this.members.values()].map((m) => ({ x: m.x, y: m.y }));
      for (const a of s.avoid || []) pts.push({ x: a.x, y: a.y, extra: a.r });
      let best = null; let bestD = -1;
      for (let i = 0; i < 40; i++) {
        const p = { x: rand(s.x[0], s.x[1]), y: rand(s.y[0], s.y[1]) };
        let d = Infinity;
        for (const q of pts) d = Math.min(d, Math.hypot(p.x - q.x, p.y - q.y) - (q.extra || 0));
        if (d > bestD) { best = p; bestD = d; }
        if (d >= (s.minDistance || 180)) break;
      }
      return { x: best.x, y: best.y, ring: undefined, angle: rand(0, Math.PI * 2) };
    }

    spawnMember(ev, opts = {}) {
      const crowd = this.sc.crowd;
      if (!crowd || !ev.user) return null;
      const u = ev.user;
      const existing = this.members.get(u.id);

      if (existing) {
        if (crowd.unique !== false) {
          existing.jumpUntil = performance.now() + 1200;
          if (opts.badge) this._setBadge(existing, opts.badge);
          existing.outer.classList.remove('pulse'); void existing.outer.offsetWidth; existing.outer.classList.add('pulse');
          return existing;
        }
      }

      const max = crowd.max || 80;
      if (this.members.size >= max) this.removeMember(this.members.keys().next().value);

      let pos;
      let slot = -1;
      if (crowd.layout === 'scatter') pos = this._scatterPos();
      else {
        slot = this.slots.findIndex((s) => !s);
        if (slot < 0) { slot = this.slots.length; this.slots.push(true); } else this.slots[slot] = true;
        pos = this._ringPos(slot);
      }

      const size = crowd.size || { width: 128, height: 190, head: 82 };
      const outer = document.createElement('div');
      outer.className = 'member pop';
      outer.style.cssText = `left:${pos.x}px;top:${pos.y}px;width:${size.width}px;height:${size.height}px;margin-left:${-size.width / 2}px;margin-top:${-size.height * 0.85}px;z-index:${Math.round(pos.y)};`;

      const inner = document.createElement('div');
      inner.className = 'member-inner';
      const hue = hash(u.id) % 360;
      inner.innerHTML = `
        <img class="m-body" draggable="false" style="filter:hue-rotate(${hue}deg) saturate(1.1)" src="${this.resolveAsset(crowd.body)}">
        <div class="m-head" style="width:${size.head}px;height:${size.head}px;left:${(size.width - size.head) / 2}px;top:${-size.head * 0.18}px"><img draggable="false"></div>
        <div class="m-name"></div>`;
      const head = inner.querySelector('.m-head img');
      head.src = u.avatar;
      head.onerror = () => { head.onerror = null; head.src = `/avatar/fake/${encodeURIComponent(u.uniqueId || 'x')}.svg`; };
      inner.querySelector('.m-name').textContent = (u.nickname || u.uniqueId || '').slice(0, 14);
      outer.appendChild(inner);
      this.el.world.appendChild(outer);

      const m = {
        id: u.id, user: u, outer, inner, slot,
        x: pos.x, y: pos.y, ring: pos.ring,
        off: pos.ring !== undefined ? pos.angle / Math.PI : rand(0, 4),
        depth: pos.ring !== undefined ? 0.9 + 0.1 * Math.sin(pos.angle) : 1,
        scale: opts.scale || 1,
        jumpUntil: 0, size,
      };
      this.members.set(u.id, m);
      if (opts.badge) this._setBadge(m, opts.badge);
      setTimeout(() => outer.classList.remove('pop'), 700);

      this._updateRings();
      this._updateCamera();
      this.setCounter(crowd.counter || 'crowd', this.members.size, ev);
      return m;
    }

    _setBadge(m, badge) {
      let b = m.inner.querySelector('.m-badge');
      if (!b) { b = document.createElement('div'); b.className = 'm-badge'; m.inner.appendChild(b); }
      b.textContent = badge;
    }

    removeMember(id) {
      const m = this.members.get(id);
      if (!m) return;
      this.members.delete(id);
      if (m.slot >= 0) this.slots[m.slot] = false;
      m.outer.classList.add('leave');
      setTimeout(() => m.outer.remove(), 400);
      this._updateRings();
      this._updateCamera();
      this.setCounter(this.sc.crowd.counter || 'crowd', this.members.size);
    }

    // ---------------------------------------------------------------- KAMERA
    _updateCamera() {
      const cam = this.sc.camera || {};
      if (cam.mode !== 'fitCrowd') { this.zoomTarget = cam.zoom || 1; return; }
      const f = cam.focus || { x: 540, y: 960 };
      let ext = { l: 0, r: 0, u: 0, d: 0 };
      for (const m of this.members.values()) {
        const s = m.size;
        ext.l = Math.max(ext.l, f.x - (m.x - s.width * 0.6));
        ext.r = Math.max(ext.r, (m.x + s.width * 0.6) - f.x);
        ext.u = Math.max(ext.u, f.y - (m.y - s.height * 0.95));
        ext.d = Math.max(ext.d, (m.y + s.height * 0.25) - f.y);
      }
      const padX = cam.paddingX ?? 60;
      const padB = cam.paddingY ?? 120;
      const padT = cam.paddingTop ?? 240; // HUD alanı
      const ratios = [
        (W / 2 - padX) / Math.max(ext.l, 1),
        (W / 2 - padX) / Math.max(ext.r, 1),
        (f.y - padT) / Math.max(ext.u, 1),
        (H - f.y - padB) / Math.max(ext.d, 1),
      ];
      this.zoomTarget = clamp(Math.min(...ratios), cam.minZoom ?? 0.3, cam.maxZoom ?? 1);
    }

    // ---------------------------------------------------------------- OLAY İŞLEME
    handleEvent(ev) {
      if (!this.sc) return;
      if (ev.type === 'comment') this._addChat(ev);
      const now = performance.now();
      for (let i = 0; i < (this.sc.triggers || []).length; i++) {
        const tr = this.sc.triggers[i];
        if (tr.enabled === false || tr.on !== ev.type) continue;
        if (!this._matches(tr.match, ev)) continue;

        if (tr.cooldown) {
          const lastFire = this.cooldowns.get(i) || 0;
          if (now - lastFire < tr.cooldown * 1000) continue;
          this.cooldowns.set(i, now);
        }

        let times = 1;
        if (tr.every) {
          const total = (this.acc.get(i) || 0) + (ev.type === 'like' ? ev.count || 1 : 1);
          times = Math.min(Math.floor(total / tr.every), 3);
          this.acc.set(i, total % tr.every);
          if (times < 1) continue;
        }
        for (let k = 0; k < times; k++) this._runActions(tr.actions, ev);
        if (tr.exclusive) break;
      }
    }

    _matches(m, ev) {
      if (!m) return true;
      if (ev.type === 'gift') {
        const g = ev.gift || {};
        if (m.giftName && !asArray(m.giftName).some((n) => norm(n) === norm(g.name))) return false;
        if (m.giftId && !asArray(m.giftId).map(String).includes(String(g.id))) return false;
        if (m.minDiamonds !== undefined && (g.diamonds || 0) < m.minDiamonds) return false;
        if (m.maxDiamonds !== undefined && (g.diamonds || 0) > m.maxDiamonds) return false;
        if (m.minCount !== undefined && (g.count || 1) < m.minCount) return false;
      }
      if (ev.type === 'comment') {
        const text = norm(ev.text);
        if (m.command && !asArray(m.command).some((c) => norm(c) === text.split(/\s+/)[0])) return false;
        if (m.text && !asArray(m.text).some((t) => norm(t) === text)) return false;
        if (m.contains && !asArray(m.contains).some((t) => text.includes(norm(t)))) return false;
        if (m.startsWith && !asArray(m.startsWith).some((t) => text.startsWith(norm(t)))) return false;
        if (m.regex && !new RegExp(m.regex, 'i').test(ev.text || '')) return false;
      }
      if (ev.type === 'like' && m.minCount !== undefined && (ev.count || 1) < m.minCount) return false;
      if (m.moderator && !(ev.user && ev.user.isModerator)) return false;
      if (m.subscriber && !(ev.user && ev.user.isSubscriber)) return false;
      return true;
    }

    // Metin şablonları: {nickname} {gift} {count} ...
    fmt(str, ev) {
      const u = ev.user || {};
      const g = ev.gift || {};
      return String(str ?? '').replace(/\{([\w:]+)\}/g, (_m, key) => {
        if (key.startsWith('counter:')) return Math.round(this.getCounter(key.slice(8)));
        switch (key) {
          case 'nickname': return u.nickname || u.uniqueId || '';
          case 'uniqueId': return u.uniqueId || '';
          case 'gift': return g.name || '';
          case 'count': return g.count ?? ev.count ?? 1;
          case 'diamonds': return g.diamonds ?? 0;
          case 'text': return ev.text || '';
          case 'total': return ev.total ?? '';
          default: return '';
        }
      });
    }

    _amount(a, ev) {
      let amount = typeof a.amount === 'number' ? a.amount : 1;
      switch (a.multiplyBy) {
        case 'count': amount *= (ev.gift && ev.gift.count) || ev.count || 1; break;
        case 'diamonds': amount *= (ev.gift && ev.gift.diamonds) || 0; break;
        case 'likes': amount *= ev.count || 1; break;
        default: break;
      }
      return amount;
    }

    _runActions(actions, ev) {
      for (const raw of actions || []) {
        const type = ACTION_ALIASES[raw.type] || raw.type;
        try { this._action(type, raw, ev); } catch (err) { console.error('[aksiyon hatası]', type, err); }
      }
    }

    // ---------------------------------------------------------------- AKSİYONLAR
    _action(type, a, ev) {
      switch (type) {
        case 'spawn_character':
          this.spawnMember(ev, { badge: a.badge, scale: a.scale });
          break;

        case 'move_entity': {
          const e = this.entities.get(a.target);
          if (!e) break;
          if (a.to) { if (a.to.x !== undefined) e.tx = a.to.x; if (a.to.y !== undefined) e.ty = a.to.y; }
          if (a.dx) e.tx += a.dx;
          if (a.dy) e.ty += a.dy;
          if (a.minY !== undefined) e.ty = Math.max(e.ty, a.minY);
          if (a.maxY !== undefined) e.ty = Math.min(e.ty, a.maxY);
          e.speed = a.duration ? 3 / a.duration : 3;
          break;
        }

        case 'play_sound': this.audio.play(a.sound, a.volume ?? 1); break;

        case 'shake':
          this.shakeState = { amp: a.intensity ?? 12, until: performance.now() + (a.duration ?? 0.6) * 1000, dur: (a.duration ?? 0.6) * 1000 };
          break;

        case 'counter_add': this.setCounter(a.counter, this.getCounter(a.counter) + this._amount(a, ev), ev); break;
        case 'counter_set': this.setCounter(a.counter, a.value ?? 0, ev); break;

        case 'banner': this._banner(this.fmt(a.text, ev), a, ev); break;

        case 'burst': this._burst(asArray(a.emoji), a.count ?? 14, a.x ?? 540, a.y ?? 1000, a.spread ?? 1); break;

        case 'flash': this._flash(a.color || 'rgba(255,255,255,0.8)', (a.duration ?? 0.6) * 1000); break;

        case 'temp_boost':
          this.boosts = this.boosts.filter((b) => b.until > performance.now());
          this.boosts.push({ target: a.target, add: a.add, until: performance.now() + (a.seconds ?? 5) * 1000 });
          break;

        case 'jump_all': {
          const until = performance.now() + 900;
          this.members.forEach((m) => { m.jumpUntil = until + rand(0, 200); });
          this.entities.forEach((e) => { e.jumpUntil = until; });
          break;
        }

        case 'entity_anim': {
          const e = this.entities.get(a.target);
          if (e) e.tmp = { anim: a.anim || 'pulse', until: performance.now() + (a.seconds ?? 1) * 1000 };
          break;
        }

        case 'gift_popup': this._giftPopup(ev); break;

        case 'reset_scene': this.loadScenario(this.sc); break;

        default: console.warn('[motor] bilinmeyen aksiyon:', type);
      }
    }

    _banner(text, a, ev) {
      if (!text) return;
      const b = document.createElement('div');
      b.className = 'banner ' + (a.style || 'small');
      if (a.color) b.style.setProperty('--banner-color', a.color);
      const showAvatar = a.avatar !== false && ev.user && ev.user.avatar && a.style !== 'mega';
      if (showAvatar) {
        const img = document.createElement('img');
        img.className = 'banner-avatar';
        img.src = ev.user.avatar;
        img.onerror = () => { img.remove(); };
        b.appendChild(img);
      }
      const span = document.createElement('span');
      span.textContent = text;
      b.appendChild(span);
      const box = this.el.banners;
      while (box.children.length >= 3) box.firstChild.remove();
      box.appendChild(b);
      const dur = (a.duration ?? 2.5) * 1000;
      b.animate(
        [{ transform: 'translateY(40px) scale(.6)', opacity: 0 }, { transform: 'translateY(0) scale(1.08)', opacity: 1, offset: 0.12 }, { transform: 'scale(1)', opacity: 1, offset: 0.2 },
          { transform: 'scale(1)', opacity: 1, offset: 0.85 }, { transform: 'translateY(-20px) scale(.95)', opacity: 0 }],
        { duration: dur, easing: 'ease-out' },
      ).onfinish = () => b.remove();
    }

    _burst(emojis, count, ox, oy, spread) {
      if (!emojis.length) emojis = ['✨'];
      count = Math.min(count, 60);
      for (let i = 0; i < count; i++) {
        if (this.particles > 220) return;
        this.particles++;
        const s = document.createElement('span');
        s.className = 'particle';
        s.textContent = pick(emojis);
        s.style.left = ox + 'px';
        s.style.top = oy + 'px';
        s.style.fontSize = rand(36, 84) + 'px';
        this.el.fx.appendChild(s);
        const ang = rand(0, Math.PI * 2);
        const dist = rand(140, 520) * spread;
        const dx = Math.cos(ang) * dist;
        const dy = Math.sin(ang) * dist - 160;
        const rot = rand(-260, 260);
        s.animate(
          [{ transform: 'translate(-50%,-50%) scale(.2)', opacity: 0 },
            { transform: `translate(calc(-50% + ${dx * 0.7}px), calc(-50% + ${dy * 0.7}px)) scale(1.2) rotate(${rot * 0.5}deg)`, opacity: 1, offset: 0.35 },
            { transform: `translate(calc(-50% + ${dx}px), calc(-50% + ${dy + 380}px)) scale(.9) rotate(${rot}deg)`, opacity: 0 }],
          { duration: rand(1100, 2100), easing: 'cubic-bezier(.2,.7,.3,1)', delay: rand(0, 160) },
        ).onfinish = () => { s.remove(); this.particles--; };
      }
    }

    _flash(color, ms) {
      const f = document.createElement('div');
      f.className = 'flash';
      f.style.background = color;
      this.el.fx.appendChild(f);
      f.animate([{ opacity: 1 }, { opacity: 0 }], { duration: ms, easing: 'ease-out' }).onfinish = () => f.remove();
    }

    _giftPopup(ev) {
      if (!ev.gift || !ev.gift.image) return;
      const img = document.createElement('img');
      img.className = 'gift-popup';
      img.src = ev.gift.image.startsWith('http') ? `/avatar/proxy?url=${encodeURIComponent(ev.gift.image)}` : ev.gift.image;
      this.el.fx.appendChild(img);
      img.animate([{ transform: 'translate(-50%, 0) scale(.3)', opacity: 0 }, { transform: 'translate(-50%, -160px) scale(1.2)', opacity: 1, offset: 0.3 }, { transform: 'translate(-50%, -420px) scale(1)', opacity: 0 }],
        { duration: 2200, easing: 'ease-out' }).onfinish = () => img.remove();
    }

    _addChat(ev) {
      if (this.sc.feed === false || !ev.user) return;
      const box = this.el.feed;
      const row = document.createElement('div');
      row.className = 'chat-row';
      const img = document.createElement('img');
      img.src = ev.user.avatar;
      img.onerror = () => img.remove();
      const t = document.createElement('div');
      t.className = 'chat-text';
      const n = document.createElement('b');
      n.textContent = (ev.user.nickname || ev.user.uniqueId) + ' ';
      const msg = document.createElement('span');
      msg.textContent = String(ev.text || '').slice(0, 70);
      t.append(n, msg);
      row.append(img, t);
      while (box.children.length >= 5) box.firstChild.remove();
      box.appendChild(row);
      row.animate([{ opacity: 0, transform: 'translateX(-30px)' }, { opacity: 1, transform: 'none', offset: 0.08 }, { opacity: 1, offset: 0.9 }, { opacity: 0 }], { duration: 8000 }).onfinish = () => row.remove();
    }

    // ---------------------------------------------------------------- ANİMASYON DÖNGÜSÜ
    _anim(kind, phase, off, jumpK, amp = 1) {
      const t = this.time;
      switch (kind) {
        case 'dance': {
          const s = Math.sin((phase + off) * Math.PI);
          const bob = Math.abs(s);
          let dy = -bob * 18 * amp;
          if (jumpK > 0) dy -= Math.sin((1 - jumpK) * Math.PI) * 95 * amp;
          return { dx: 0, dy, rot: s * 5 * amp, sx: 1, sy: 1 + (1 - bob) * 0.035 };
        }
        case 'float':
          return { dx: Math.cos(t * 0.9 + off * 2) * 9, dy: Math.sin(t * 1.4 + off * 3) * 16 - (jumpK > 0 ? Math.sin((1 - jumpK) * Math.PI) * 60 : 0), rot: Math.sin(t * 1.1 + off) * 5, sx: 1, sy: 1 };
        case 'rumble': {
          const k = 1 + Math.max(0, this.tempo - 1) * 0.8;
          return { dx: Math.sin(t * 57) * 3 * k, dy: Math.sin(t * 43) * 3 * k, rot: Math.sin(t * 31) * 0.8 * k, sx: 1, sy: 1 };
        }
        case 'pulse': {
          const p = 1 + Math.sin(t * 14) * 0.09;
          return { dx: 0, dy: 0, rot: 0, sx: p, sy: p };
        }
        case 'spin': return { dx: 0, dy: 0, rot: (t * 120) % 360, sx: 1, sy: 1 };
        default: return { dx: 0, dy: 0, rot: 0, sx: 1, sy: 1 };
      }
    }

    _loop(now) {
      const dt = Math.min(0.05, (now - this.last) / 1000);
      this.last = now;
      this.time += dt;

      // tempo / ses (yumuşak geçiş)
      const tempoT = this.getValue('tempo', 1);
      const volT = this.getValue('volume', 1);
      this.tempo += (tempoT - this.tempo) * (1 - Math.exp(-dt * 2));
      this.volume += (volT - this.volume) * (1 - Math.exp(-dt * 1.5));
      this.phase += dt * ((this.sc && this.sc.assets && this.sc.assets.music ? this.sc.assets.music.baseBpm || 100 : 100) * this.tempo / 60);

      this._audioTick += dt;
      if (this._audioTick > 0.12) {
        this._audioTick = 0;
        this.audio.setTempo(this.tempo);
        this.audio.setMusicVolume(this.volume);
        if (this.audio.running && !this.audio.playing && !this.musicDisabled) this.audio.startMusic();
      }

      if (this.sc) {
        // kamera
        const cam = this.sc.camera || {};
        this.zoom += (this.zoomTarget - this.zoom) * (1 - Math.exp(-dt * (cam.smooth || 2)));
        this.el.camera.style.transform = `scale(${this.zoom.toFixed(4)})`;

        // ekran sallama
        const nowMs = performance.now();
        const sh = this.shakeState;
        if (sh.until > nowMs) {
          const k = (sh.until - nowMs) / sh.dur;
          this.el.shaker.style.transform = `translate(${(Math.random() - 0.5) * 2 * sh.amp * k}px, ${(Math.random() - 0.5) * 2 * sh.amp * k}px)`;
        } else if (sh.amp) { sh.amp = 0; this.el.shaker.style.transform = ''; }

        // varlıklar
        this.entities.forEach((e) => {
          const f = 1 - Math.exp(-dt * e.speed);
          e.x += (e.tx - e.x) * f; e.y += (e.ty - e.y) * f;
          e.scale += (e.tscale - e.scale) * f; e.rotate += (e.trotate - e.rotate) * f; e.opacity += (e.topacity - e.opacity) * f;
          const anim = e.tmp && e.tmp.until > nowMs ? e.tmp.anim : e.def.anim;
          const jumpK = e.jumpUntil > nowMs ? (e.jumpUntil - nowMs) / 900 : 0;
          const a = this._anim(anim, this.phase, e.def.phaseOffset || 0, jumpK, e.def.amp ?? 1.5);
          e.wrap.style.transform = `translate(${(e.x - e.def.width / 2).toFixed(1)}px, ${(e.y - e.def.height / 2).toFixed(1)}px)`;
          e.wrap.style.opacity = e.opacity;
          e.inner.style.transform = `translate(${a.dx.toFixed(1)}px, ${a.dy.toFixed(1)}px) rotate(${(a.rot + e.rotate).toFixed(2)}deg) scale(${(a.sx * e.scale).toFixed(3)}, ${(a.sy * e.scale).toFixed(3)})`;
        });

        // kalabalık
        const kind = (this.sc.crowd && this.sc.crowd.anim) || 'dance';
        this.members.forEach((m) => {
          const jumpK = m.jumpUntil > nowMs ? (m.jumpUntil - nowMs) / 900 : 0;
          const a = this._anim(kind, this.phase, m.off, jumpK, 1);
          const s = m.scale * m.depth;
          m.inner.style.transform = `translate(${a.dx.toFixed(1)}px, ${a.dy.toFixed(1)}px) rotate(${a.rot.toFixed(2)}deg) scale(${(a.sx * s).toFixed(3)}, ${(a.sy * s).toFixed(3)})`;
        });

        // HUD sayaçları
        for (const it of Object.values(this.hudItems)) {
          it.shown += ((it.target ?? 0) - it.shown) * (1 - Math.exp(-dt * 8));
          const shownR = Math.abs(it.target - it.shown) < 0.05 ? it.target : it.shown;
          const dec = it.cfg.decimals ?? 0;
          it.value.textContent = shownR.toFixed(dec) + (it.cfg.unit || '');
          if (it.fill) it.fill.style.width = clamp((shownR / (it.cfg.max || 100)) * 100, 0, 100) + '%';
        }

        // yıldız alanı
        if (this.stars && !this.options.transparent) this._drawStars(dt);
      }
      requestAnimationFrame((t) => this._loop(t));
    }

    _drawStars(dt) {
      const speed = 25 + 70 * this.getValue('bgspeed', 1);
      const { ctx, list } = this.stars;
      ctx.clearRect(0, 0, W, H);
      for (const s of list) {
        s.y += speed * s.z * dt;
        if (s.y > H) { s.y = -4; s.x = rand(0, W); }
        const tw = 0.55 + 0.45 * Math.sin(this.time * 2 + s.tw);
        ctx.globalAlpha = tw * s.z;
        ctx.fillStyle = '#fff';
        const len = Math.min(26, speed * s.z * 0.02);
        ctx.fillRect(s.x, s.y, s.z * 2.6, s.z * 2.6 + len);
      }
      ctx.globalAlpha = 1;
    }
  }

  window.SceneEngine = SceneEngine;
})();
