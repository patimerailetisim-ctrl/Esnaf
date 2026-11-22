'use strict';
/**
 * HEDİYE REHBERİ VERİSİ (tek kaynak)
 * ----------------------------------
 * Rehberin gösterdiği bütün sayılar (puan eşikleri, hediye karşılıkları, puan katsayısı, açık artırma
 * süreleri, komut adları) senaryonun `game` bölümünden (scenarios/osmanli.js) OKUNARAK türetilir.
 * Hiçbir değer burada veya görünüm kodunda ikinci kez yazılmaz. Aynı dosya tarayıcıda (yayın sahnesi + test paneli)
 * ve Node testlerinde kullanılır.
 *
 * Modeller: rütbe yalnızca PUANLA yükselir; hediye puanı = hediye değeri (giftDetails.diamondCount) x points.perGiftValue.
 * Padişahlık puandan bağımsızdır (taht açık artırması).
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.GuideData = factory();
}(typeof self !== 'undefined' ? self : this, function () {
  const fmt = (n) => Math.floor(n).toLocaleString('tr-TR');
  const first = (v) => [].concat(v || [])[0] || '';
  const trim = (n) => String(Math.round(n * 100) / 100).replace('.', ',');

  /** Bir hediyenin kazandırdığı puan (hediyeye özel birim puan varsa o, yoksa değer x katsayı) */
  function giftPoints(game, gift) {
    const o = game.points.giftOverrides && game.points.giftOverrides[gift.name];
    return (typeof o === 'number' ? o : gift.value * game.points.perGiftValue);
  }

  /**
   * Gereken puana en yakın örnek: katalogdaki en değerli hediyeden başlayarak, adedi makul (<=20) ve
   * gereken puanı en fazla %15 aşan ilk hediye ("3× Hand Hearts"). Hiçbiri uymazsa en az aşanı seçer.
   */
  function exampleFor(game, catalog, pointsNeeded) {
    if (!(pointsNeeded > 0) || !catalog.length) return null;
    const scored = catalog.map((g) => ({ gift: g, pts: giftPoints(game, g) })).filter((x) => x.pts > 0).sort((a, b) => b.pts - a.pts);
    if (!scored.length) return null;
    let best = null;
    for (const s of scored) {
      const count = Math.ceil(pointsNeeded / s.pts);
      const over = (count * s.pts - pointsNeeded) / pointsNeeded;
      if (count <= 20 && over <= 0.15) { best = { s, count }; break; }
      if (count <= 50 && (!best || over < best.over)) best = Object.assign({ s, count, over }, best && best.over <= over ? best : {});
    }
    if (!best) best = { s: scored[scored.length - 1], count: Math.ceil(pointsNeeded / scored[scored.length - 1].pts) };
    return { gift: best.s.gift, count: best.count, points: best.s.pts * best.count, value: best.s.gift.value * best.count };
  }

  function build(game) {
    const catalog = (game.giftCatalog || []).slice().sort((a, b) => a.value - b.value);
    const joinPoints = (game.points && game.points.join) || 0;
    const perGiftValue = (game.points && game.points.perGiftValue) || 1;
    const throneRank = game.throne.rank;
    const au = (game.throne && game.throne.auction) || {};

    const rows = game.ranks.map((r, i) => {
      const auto = r.auto !== false;
      // Yeni bir oyuncu (katılma puanıyla başlar) bu rütbeye ulaşmak için ne kadar puan toplamalı?
      const pointsNeeded = auto && i > 0 ? Math.max(0, r.minPoints - joinPoints) : null;
      return {
        id: r.id, name: r.name, icon: r.icon, index: i,
        auto, minPoints: auto ? r.minPoints : null,
        pointsNeeded, example: pointsNeeded ? exampleFor(game, catalog, pointsNeeded) : null,
        throne: r.id === throneRank, start: i === 0,
      };
    });
    const cmds = {};
    for (const [k, v] of Object.entries(game.commands || {})) cmds[k] = first(v);

    return {
      rows, cmds, catalog,
      crown: rows.find((r) => r.throne) || null,
      perGiftValue,
      unit: (game.giftValue && game.giftValue.unitLabel) || '💎',
      auction: {
        enabled: au.enabled !== false, everySec: au.intervalSec, durationSec: au.durationSec,
        extendWindowSec: au.extendWindowSec, extendBySec: au.extendBySec, maxExtensionSec: au.maxExtensionSec, topN: au.topN || 5,
      },
      guide: game.guide || {},
    };
  }

  /** "≈ 3× Universe" | "En yüksek teklif" | "Hediyesiz" */
  function giftLabel(row) {
    if (row.start) return 'Hediyesiz';
    if (row.throne) return 'En yüksek teklif';
    if (!row.example) return '—';
    return `≈ ${row.example.count > 1 ? row.example.count + '× ' : ''}${row.example.gift.name}`;
  }

  /** Başlangıç rütbesi: komut (config'ten) · taht: açık artırma · diğerleri: "N puan" */
  function pointsLabel(row, data) {
    if (row.start) return `🆓 ${data.cmds.join || ''}`.trim();
    if (row.throne) return 'açık artırma';
    return `${fmt(row.minPoints)} puan`;
  }

  /** Yan rehber satırı için kısa iki satır: hediye karşılığı + (değer · puan) */
  function railLines(row, data) {
    const gift = giftLabel(row);
    if (row.start) return { gift, detail: `🆓 ${data.cmds.join || ''}`.trim() };
    if (row.throne) {
      const a = data.auction;
      return { gift, detail: `her ${trim(a.everySec / 60)} dk · ${a.durationSec} sn` };
    }
    const parts = [];
    if (row.example) parts.push(`${fmt(row.example.value)} ${data.unit}`);
    parts.push(`${fmt(row.minPoints)} puan`);
    return { gift, detail: parts.join(' · ') };
  }

  /** Metin şablonu: {join} {guide} {perGiftValue} {auctionEvery} {auctionSeconds} {extendWindow} {extendBy} {extendMax} */
  function template(str, data) {
    const a = data.auction;
    const vars = {
      join: data.cmds.join || '', guide: data.cmds.guide || '', rank: data.cmds.rank || '',
      perGiftValue: trim(data.perGiftValue),
      auctionEvery: trim(a.everySec / 60), auctionSeconds: String(a.durationSec),
      extendWindow: String(a.extendWindowSec), extendBy: String(a.extendBySec), extendMax: String(a.maxExtensionSec),
    };
    return String(str || '').replace(/\{(\w+)\}/g, (_m, k) => (k in vars ? vars[k] : ''));
  }

  return { build, giftLabel, pointsLabel, railLines, template, fmt, giftPoints, exampleFor };
}));
