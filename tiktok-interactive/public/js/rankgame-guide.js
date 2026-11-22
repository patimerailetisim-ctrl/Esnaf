'use strict';
/**
 * HEDİYE VE RÜTBE REHBERİ (yayın sahnesi bileşeni)
 * ------------------------------------------------
 *  - Sağda altın işlemeli yan rehber: rütbeler sayfa sayfa döner, altta Padişah ve ücretsiz katılım vurgusu
 *  - Büyük rehber: !hediyeler yazılınca veya yayın açılınca (10 sn) ekranda belirgin açılır
 *  - Hediye gelince kazanılan puan + rütbe bilgisi kısa animasyonla gösterilir
 * Bütün sayılar GuideData.build(game) ile senaryodan okunur; burada sabit değer yoktur.
 * Rehber #stage'in (yayına giren sahnenin) parçasıdır; admin test paneli ayrı bir katmandır.
 */
(function () {
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html !== undefined) e.innerHTML = html; return e; };
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const GD = () => window.GuideData;

  class RankGameGuide {
    constructor(view) {
      this.view = view;
      this.engine = view.engine;
      this.cfg = view.cfg;
      this.gcfg = this.cfg.guide || {};
      this.data = GD().build(this.cfg);
      this.t = (str) => GD().template(str, this.data);
      this.texts = this.gcfg.texts || {};
      this.pageSize = Math.max(1, this.gcfg.pageSize || 3);
      this.pages = [];
      for (let i = 0; i < this.data.rows.length; i += this.pageSize) this.pages.push(this.data.rows.slice(i, i + this.pageSize));
      this.page = 0;
      this.visible = this.gcfg.visible !== false;
      this.destroyed = false;
      this.timers = new Set();
      this.flashQueue = [];
      this.flashRunning = false;
      this.lastWelcome = 0;

      this._buildRail();
      this._buildOverlay();
      this.engine.el.stage.append(this.rail, this.overlay);
      this.setVisible(this.visible);
      this._renderPage(false);
      this._pager = setInterval(() => this._nextPage(), Math.max(2, this.gcfg.pageSeconds || 4) * 1000);

      // Yayın açıldığında (sahne yüklendiğinde) otomatik bilgilendirme
      const intro = this.gcfg.intro || {};
      const off = new URLSearchParams(location.search).get('intro') === '0';
      if (intro.enabled !== false && intro.onSceneLoad !== false && !off) this.after(900, () => this.showOverlay(intro.seconds || 10, 'intro'));
    }

    after(ms, fn) {
      const t = setTimeout(() => { this.timers.delete(t); if (!this.destroyed) fn(); }, ms);
      this.timers.add(t);
      return t;
    }

    destroy() {
      this.destroyed = true;
      clearInterval(this._pager);
      this.timers.forEach((t) => clearTimeout(t));
      this.rail.remove(); this.overlay.remove();
    }

    // ---------------------------------------------------------------- YAN REHBER
    _rowHTML(r) {
      const l = GD().railLines(r, this.data);
      const cls = 'gd-row' + (r.throne ? ' gd-throne-row' : '');
      return `<div class="${cls}" data-rank="${esc(r.id)}">
        <div class="gd-r1"><i>${esc(r.icon)}</i><b>${esc(r.name)}</b></div>
        <div class="gd-r2">🎁 ${esc(l.gift)}</div>
        <div class="gd-r3">${esc(l.detail)}</div></div>`;
    }

    _buildRail() {
      const tx = this.texts; const d = this.data;
      this.rail = el('aside', 'gd-rail');
      this.rail.innerHTML = `
        <div class="gd-orn gd-tl"></div><div class="gd-orn gd-tr"></div><div class="gd-orn gd-bl"></div><div class="gd-orn gd-br"></div>
        <div class="gd-rail-head">📜 ${esc(this.gcfg.title || '')}</div>
        <div class="gd-stage"><div class="gd-list"></div><div class="gd-flash"></div></div>
        <div class="gd-dots">${this.pages.map(() => '<span></span>').join('')}</div>
        <div class="gd-throne"><div class="t">👑 ${esc(this.t(tx.throneTitle))}</div><div class="l">${esc(this.t(tx.throneLine))}</div></div>
        <div class="gd-free"><div class="t">🆓 ${esc(this.t(tx.freeTitle))}</div><div class="l">${esc(this.t(tx.freeLine))}</div></div>
        <div class="gd-more">${esc(this.t(tx.moreLine))}</div>`;
      this.listEl = this.rail.querySelector('.gd-list');
      this.flashEl = this.rail.querySelector('.gd-flash');
      this.dots = [...this.rail.querySelectorAll('.gd-dots span')];
      void d;
    }

    _renderPage(animate) {
      const rows = this.pages[this.page] || [];
      this.listEl.innerHTML = rows.map((r) => this._rowHTML(r)).join('');
      this.dots.forEach((d, i) => d.classList.toggle('on', i === this.page));
      if (animate) this.listEl.animate([{ opacity: 0, transform: 'translateX(24px)' }, { opacity: 1, transform: 'none' }], { duration: 450, easing: 'ease-out' });
    }

    _nextPage() {
      if (!this.visible || this.flashRunning || Date.now() < (this.holdUntil || 0)) return;
      this.page = (this.page + 1) % this.pages.length;
      this._renderPage(true);
    }

    /** Belirli rütbenin bulunduğu sayfayı gösterip satırı vurgular */
    focusRank(rankId) {
      const idx = this.data.rows.findIndex((r) => r.id === rankId);
      if (idx < 0) return;
      this.page = Math.floor(idx / this.pageSize);
      this._renderPage(true);
      const row = this.listEl.querySelector(`[data-rank="${rankId}"]`);
      this.holdUntil = Date.now() + 3300; // vurgulanan satır görünürken sayfa değişmesin
      if (row) { row.classList.add('gd-hit'); this.after(3200, () => row.classList.remove('gd-hit')); }
    }

    setVisible(v) {
      this.visible = !!v;
      this.rail.classList.toggle('gd-hidden', !this.visible);
    }

    // ---------------------------------------------------------------- BÜYÜK REHBER (!hediyeler / yayın açılışı)
    _buildOverlay() {
      const tx = this.texts; const d = this.data; const gd = GD();
      this.overlay = el('div', 'gd-ov');
      const cells = d.rows.map((r) => `
        <div class="gd-cell${r.throne ? ' throne' : ''}">
          <div class="c1"><i>${esc(r.icon)}</i><b>${esc(r.name)}</b><em>${esc(gd.pointsLabel(r, d))}</em></div>
          <div class="c2">🎁 ${esc(gd.giftLabel(r))}</div>
        </div>`).join('');
      const cmdList = ['rank', 'job', 'army', 'treasury'].map((k) => d.cmds[k]).filter(Boolean).join('  ·  ');
      this.overlay.innerHTML = `
        <div class="gd-ov-card">
          <div class="gd-ov-tag"></div>
          <div class="gd-ov-head">📜 ${esc(this.gcfg.title || '')}</div>
          <div class="gd-ov-band gd-ov-free"><div class="b1">🆓 ${esc(this.t(tx.freeTitle))}</div><div class="b2">${esc(this.t(tx.freeLine))}</div><div class="b3">${esc(this.t(tx.freeMore))}</div></div>
          <div class="gd-ov-band gd-ov-throne"><div class="b1">👑 ${esc(this.t(tx.throneTitle))}</div><div class="b2">${esc(this.t(tx.throneLine))}</div></div>
          <div class="gd-ov-conv">${esc(this.t(tx.pointsLine))}</div>
          <div class="gd-ov-grid">${cells}</div>
          <div class="gd-ov-foot">${esc(cmdList)}</div>
          <div class="gd-ov-bar"><i></i></div>
        </div>`;
      this.ovTag = this.overlay.querySelector('.gd-ov-tag');
      this.ovBar = this.overlay.querySelector('.gd-ov-bar i');
    }

    showOverlay(seconds, reason) {
      if (this.destroyed) return;
      this.ovTag.textContent = reason === 'intro' ? '🎬 YAYINA HOŞ GELDİNİZ 🎬' : '';
      this.overlay.classList.add('on');
      this.engine.audio.play('notice');
      if (this._ovAnim) this._ovAnim.cancel();
      this._ovAnim = this.ovBar.animate([{ transform: 'scaleX(1)' }, { transform: 'scaleX(0)' }], { duration: seconds * 1000, easing: 'linear', fill: 'forwards' });
      clearTimeout(this._ovTimer);
      this._ovTimer = setTimeout(() => this.overlay.classList.remove('on'), seconds * 1000);
      this.timers.add(this._ovTimer);
    }

    onFx(fx) {
      if (fx.mode === 'open') this.showOverlay(fx.seconds || this.gcfg.openSeconds || 15, fx.reason);
    }

    // ---------------------------------------------------------------- HEDİYE ETKİSİ (puan + rütbe) ANİMASYONU
    flashGift(fx) {
      if (!this.visible || this.destroyed) return; // rehber gizliyken gösterilmez
      if (this.flashQueue.length >= 3) this.flashQueue.shift();
      this.flashQueue.push(fx);
      if (!this.flashRunning) this._runFlash();
    }

    _flashHTML(fx) {
      const gd = GD();
      const rk = (id) => this.data.rows.find((r) => r.id === id);
      const g = fx.gift || {};
      let line; let cls = '';
      if (fx.crowned) { line = '👑 PADİŞAH OLDU!'; cls = 'crown'; } else if (fx.note === 'protected') line = '🛡️ Taht korumada';
      else if (fx.note === 'too-low') line = '⚔️ Taç için daha büyük hediye gerek';
      else if (fx.rankChanged && rk(fx.rank)) { const r = rk(fx.rank); line = `${r.icon} ${r.name} oldu!`; cls = 'up'; } else if (fx.grantRank && rk(fx.grantRank)) { const r = rk(fx.grantRank); line = `Hediye rütbesi: ${r.icon} ${r.name}`; } else line = '';
      const sub = fx.next ? `${fx.next.name} için ${gd.fmt(fx.next.need)} puan` : (fx.rankChanged ? '' : 'Üst rütbeler hediye ile');
      return `<div class="gd-fl ${cls}">
        <div class="f1">🎁 ${esc(g.name || '')}${g.count > 1 ? ' ×' + g.count : ''}</div>
        <div class="f2">+${esc(gd.fmt(fx.points))} PUAN</div>
        ${line ? `<div class="f3">${esc(line)}</div>` : ''}
        ${sub ? `<div class="f4">${esc(sub)}</div>` : ''}
        <div class="f5">${esc(fx.user ? fx.user.name : '')}</div></div>`;
    }

    async _runFlash() {
      this.flashRunning = true;
      const full = (this.gcfg.giftFlashSeconds || 4.5) * 1000;
      while (this.flashQueue.length && !this.destroyed && this.visible) {
        const fx = this.flashQueue.shift();
        const dur = this.flashQueue.length ? Math.min(full, 2200) : full; // yoğun akışta kısalt
        this.flashEl.innerHTML = this._flashHTML(fx);
        this.flashEl.classList.add('on');
        this.flashEl.animate([{ opacity: 0, transform: 'scale(.85)' }, { opacity: 1, transform: 'scale(1.04)', offset: 0.12 }, { opacity: 1, transform: 'scale(1)', offset: 0.9 }, { opacity: 0 }], { duration: dur, easing: 'ease-out' });
        await new Promise((r) => { const t = setTimeout(r, dur); this.timers.add(t); });
        this.flashEl.classList.remove('on');
        if (!this.flashQueue.length) this.focusRank(fx.grantRank || fx.rank); // hediyenin kazandırdığı rütbeyi listede göster
      }
      this.flashEl.classList.remove('on');
      this.flashRunning = false;
    }

    // ---------------------------------------------------------------- !katıl bilgisi (ücretsiz katılım)
    welcome(user) {
      const now = Date.now();
      if (now - this.lastWelcome < 8000 || this.view.noticeQueue.length || this.view.noticeRunning) return;
      this.lastWelcome = now;
      this.view._queueNotice({ kind: 'welcome', icon: '🆓', user, title: this.t(this.texts.welcomeTitle), lines: [this.t(this.texts.welcomeLine)] });
    }
  }

  window.RankGameGuide = RankGameGuide;
})();
