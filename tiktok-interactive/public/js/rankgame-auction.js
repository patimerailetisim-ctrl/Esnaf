'use strict';
/**
 * TAHT MÜCADELESİ KARTI (rankgame-auction.js) - yayın sahnesinin solunda
 * ---------------------------------------------------------------------
 *  - Beklerken: sonraki mücadeleye geri sayım + Padişah ve Sadrazam bilgisi
 *  - Açık artırmada: kalan süre (son 10 sn kırmızı nabız, son 3 sn dev 3-2-1), "+10 sn" uzatma animasyonu,
 *    ilk 5 teklif sahibi ve teklif puanları (sıralama değişimi animasyonlu)
 * Durum sunucudan gelir (game:state); geri sayım sunucunun mutlak bitiş saatinden hesaplanır, bu yüzden
 * sayfa yenilense de açık artırma durumu ve kalan süre kaybolmaz.
 */
(function () {
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html !== undefined) e.innerHTML = html; return e; };
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const fmt = (n) => Math.floor(n).toLocaleString('tr-TR');
  const ROW_H = 60;

  function clock(ms) {
    const s = Math.max(0, Math.ceil(ms / 1000));
    return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }

  class RankGameAuction {
    constructor(view) {
      this.view = view;
      this.engine = view.engine;
      this.au = view.cfg.throne.auction;
      this.topN = this.au.topN || 5;
      this.a = null;          // son auction anlık görüntüsü
      this.throne = null;
      this.skew = 0;          // sunucu saati - istemci saati
      this.rows = new Map();  // kullanıcı id -> satır öğesi
      this.bumped = new Set();
      this.lastSec = null;
      this.destroyed = false;
      this.card = el('aside', 'au-card');
      this.card.innerHTML = `
        <div class="gd-orn gd-tl"></div><div class="gd-orn gd-tr"></div><div class="gd-orn gd-bl"></div><div class="gd-orn gd-br"></div>
        <div class="au-head">⚔️ TAHT MÜCADELESİ</div>
        <div class="au-timer">
          <div class="au-label"></div>
          <div class="au-time">--:--</div>
          <div class="au-bar"><i></i></div>
          <div class="au-ext"></div>
        </div>
        <div class="au-people">
          <div class="au-p holder"><img><div><small>👑 PADİŞAH</small><b>—</b></div></div>
          <div class="au-p vizier"><img><div><small>📜 SADRAZAM</small><b>—</b></div></div>
        </div>
        <div class="au-bids">
          <div class="au-bids-title">İLK ${this.topN} TEKLİF <span>(hediye puanı)</span></div>
          <div class="au-list" style="height:${this.topN * ROW_H}px"></div>
          <div class="au-empty">Henüz teklif yok.<br>Hediye gönder, tahtı kap!</div>
        </div>
        <div class="au-last"></div>`;
      this.q = (s) => this.card.querySelector(s);
      this.count = el('div', 'au-count321');
      this.engine.el.stage.append(this.card, this.count);
      this._loop = setInterval(() => this.update(), 200);
    }

    destroy() {
      this.destroyed = true;
      clearInterval(this._loop);
      this.card.remove(); this.count.remove();
    }

    // ---------------------------------------------------------------- durum
    onState(st) {
      if (this.destroyed || !st.auction) return;
      this.a = st.auction;
      this.throne = st.throne;
      this.skew = st.auction.serverNow - Date.now();
      this._renderStatic();
      this._renderBids();
      this.update();
    }

    _person(sel, u) {
      const box = this.q(sel);
      const img = box.querySelector('img');
      const b = box.querySelector('b');
      if (!u) { img.style.visibility = 'hidden'; b.textContent = '—'; return; }
      img.style.visibility = 'visible';
      if (img.dataset.src !== u.avatar) {
        img.dataset.src = u.avatar;
        img.onerror = () => { img.onerror = null; img.src = `/avatar/fake/${encodeURIComponent(u.uniqueId || 'x')}.svg`; };
        img.src = u.avatar;
      }
      b.textContent = u.name;
    }

    _renderStatic() {
      const a = this.a;
      this.card.classList.toggle('active', a.phase === 'active');
      this._person('.holder', this.throne.holder);
      this._person('.vizier', this.throne.vizier);
      const last = a.last;
      this.q('.au-last').textContent = last && last.winnerName && a.phase !== 'active'
        ? `🏆 Son mücadele: ${last.winnerName} · ${fmt(last.bid)} puan${last.retained ? ' (tahtını korudu)' : ''}`
        : (last && !last.winnerName && a.phase !== 'active' ? 'Son mücadelede teklif gelmedi.' : '');
    }

    /** İlk N teklif: satırlar kullanıcıya göre tutulur, sıra değişince kayarak yer değiştirir */
    _renderBids() {
      const a = this.a;
      const list = this.q('.au-list');
      const top = a.phase === 'active' ? a.top : [];
      this.q('.au-empty').style.display = a.phase === 'active' && !top.length ? 'block' : 'none';
      const keep = new Set(top.map((u) => u.id));
      for (const [id, row] of this.rows) {
        if (!keep.has(id)) { this.rows.delete(id); row.classList.add('out'); setTimeout(() => row.remove(), 350); }
      }
      top.forEach((u, i) => {
        let row = this.rows.get(u.id);
        if (!row) {
          row = el('div', 'au-row in', '<span class="n"></span><img><b></b><em></em>');
          const img = row.querySelector('img');
          img.onerror = () => { img.onerror = null; img.src = `/avatar/fake/${encodeURIComponent(u.uniqueId || 'x')}.svg`; };
          img.src = u.avatar;
          list.appendChild(row);
          this.rows.set(u.id, row);
          setTimeout(() => row.classList.remove('in'), 400);
        }
        row.style.transform = `translateY(${i * ROW_H}px)`;
        row.querySelector('.n').textContent = i + 1;
        row.querySelector('b').textContent = u.name;
        const em = row.querySelector('em');
        const before = em.dataset.v;
        em.textContent = fmt(u.bid);
        em.dataset.v = String(u.bid);
        row.classList.toggle('lead', i === 0);
        if ((before !== undefined && before !== String(u.bid)) || this.bumped.has(u.id)) {
          row.classList.remove('bump'); void row.offsetWidth; row.classList.add('bump');
          this.bumped.delete(u.id);
        }
      });
    }

    // ---------------------------------------------------------------- geri sayım (200 ms'de bir)
    update() {
      const a = this.a;
      if (!a || this.destroyed) return;
      const now = Date.now() + this.skew;
      const timeEl = this.q('.au-time');
      const label = this.q('.au-label');
      const bar = this.q('.au-bar i');
      if (a.phase === 'active') {
        const left = Math.max(0, a.endsAt - now);
        const total = (a.durationSec + a.extensions * a.extendBySec) * 1000;
        label.textContent = 'AÇIK ARTIRMA · KALAN SÜRE';
        timeEl.textContent = left < 10000 ? (left / 1000).toFixed(1) : clock(left);
        bar.style.width = Math.min(100, (left / total) * 100) + '%';
        const urgent = left > 0 && left <= a.extendWindowSec * 1000;
        this.card.classList.toggle('urgent', urgent);
        const sec = Math.ceil(left / 1000);
        if (sec !== this.lastSec) {
          this.lastSec = sec;
          if (urgent && sec > 0) this.view._sound('tick', 300);
          if (sec >= 1 && sec <= 3) this._big(String(sec));
        }
      } else {
        this.card.classList.remove('urgent');
        const left = Math.max(0, a.nextAt - now);
        label.textContent = a.enabled ? 'SONRAKİ MÜCADELEYE' : 'AÇIK ARTIRMA KAPALI';
        timeEl.textContent = a.enabled ? clock(left) : '--:--';
        bar.style.width = a.enabled ? Math.max(0, Math.min(100, (1 - left / (a.intervalSec * 1000)) * 100)) + '%' : '0%';
        this.card.classList.toggle('soon', a.enabled && left > 0 && left <= 10000);
        this.lastSec = null;
      }
    }

    /** Son 3 saniyede sahnenin ortasında dev rakam */
    _big(text) {
      this.count.textContent = text;
      this.count.getAnimations().forEach((x) => x.cancel());
      this.count.animate([{ opacity: 0, transform: 'translate(-50%,-50%) scale(2.4)' }, { opacity: 1, transform: 'translate(-50%,-50%) scale(1)', offset: 0.25 }, { opacity: 1, transform: 'translate(-50%,-50%) scale(.9)', offset: 0.8 }, { opacity: 0, transform: 'translate(-50%,-50%) scale(.6)' }], { duration: 950, easing: 'ease-out' });
    }

    // ---------------------------------------------------------------- sunucu olayları
    onFx(fx) {
      if (this.destroyed) return;
      const e = this.engine;
      switch (fx.mode) {
        case 'start':
          this.view._sound('gong', 0);
          e.runAction('flash', { color: 'rgba(255,200,90,0.45)', duration: 0.7 });
          e.runAction('shake', { intensity: 8, duration: 0.6 });
          e.runAction('burst', { emoji: ['⚔️', '👑', '✨'], count: 22, x: 540, y: 560, spread: 1.4 });
          this.view._banner(`⚔️ TAHT MÜCADELESİ BAŞLADI! ${this.au.durationSec} sn · hediye gönder, teklif ver!`, 'big', null, 4.5, true);
          this.card.classList.remove('au-pop'); void this.card.offsetWidth; this.card.classList.add('au-pop');
          break;
        case 'bid':
          this.bumped.add(fx.user.id);
          this.view._sound('bid', 90);
          this._plus(`+${fmt(fx.delta)}`, fx.user);
          break;
        case 'extend':
          this.view._sound('rankBig', 200);
          this._ext(`+${fx.bySec} sn`);
          e.runAction('shake', { intensity: 5, duration: 0.4 });
          this.view._banner(`⏱️ SÜRE UZADI +${fx.bySec} sn · ${fx.by ? fx.by.name : ''}`, 'small', fx.by, 2.4, true);
          break;
        case 'end':
          this.view._enqueueCine(() => this.view._cineAuctionEnd(fx));
          break;
        default: break;
      }
    }

    _ext(text) {
      const x = this.q('.au-ext');
      x.textContent = text;
      x.getAnimations().forEach((a) => a.cancel());
      x.animate([{ opacity: 0, transform: 'translateY(10px) scale(.5)' }, { opacity: 1, transform: 'translateY(-6px) scale(1.25)', offset: 0.3 }, { opacity: 1, transform: 'none', offset: 0.8 }, { opacity: 0 }], { duration: 1700, easing: 'ease-out' });
    }

    _plus(text) {
      const p = el('div', 'au-plus');
      p.textContent = text;
      this.card.appendChild(p);
      p.animate([{ opacity: 0, transform: 'translateY(8px)' }, { opacity: 1, transform: 'translateY(-14px)', offset: 0.3 }, { opacity: 0, transform: 'translateY(-48px)' }], { duration: 1300, easing: 'ease-out' }).onfinish = () => p.remove();
    }
  }

  window.RankGameAuction = RankGameAuction;
})();
