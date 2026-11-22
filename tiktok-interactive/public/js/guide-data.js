'use strict';
/**
 * HEDİYE REHBERİ VERİSİ (tek kaynak)
 * ----------------------------------
 * Rehberin gösterdiği bütün sayılar (hediye eşikleri, puanlar, taç bedeli, komut adları)
 * senaryonun `game` bölümünden (scenarios/osmanli.js) OKUNARAK türetilir. Hiçbir değer burada
 * veya görünüm kodunda ikinci kez yazılmaz. Aynı dosya tarayıcıda (yayın sahnesi + test paneli)
 * ve Node testlerinde kullanılır.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.GuideData = factory();
}(typeof self !== 'undefined' ? self : this, function () {
  const fmt = (n) => Math.floor(n).toLocaleString('tr-TR');
  const first = (v) => [].concat(v || [])[0] || '';

  /** Katalogdaki (game.testGifts) elmas değeri eşiğe ulaşan en ucuz hediye */
  function exampleGift(catalog, diamonds) {
    return catalog.find((g) => g.diamonds >= diamonds) || null;
  }

  function build(game) {
    const catalog = (game.testGifts || []).slice().sort((a, b) => a.diamonds - b.diamonds);
    const minByRank = {};
    for (const g of game.giftRankGrants || []) {
      minByRank[g.rank] = Math.min(minByRank[g.rank] ?? Infinity, g.minDiamonds);
    }
    const throneRank = game.throne.rank;

    // Yeni bir oyuncunun tek hediyede o rütbeye ulaşması için gereken ETKİN elmas değeri:
    //  - hediye eşiği (giftRankGrants) VEYA
    //  - puan yolu: katılma puanı + elmas x perDiamond >= rütbenin minPoints değeri
    // ikisinden hangisi daha düşükse o geçerlidir (gerçek oyun mekaniğiyle aynı kural; aşağıdaki test bunu doğrular).
    const perDiamond = (game.points && game.points.perDiamond) || 1;
    const joinPoints = (game.points && game.points.join) || 0;
    const rows = game.ranks.map((r, i) => {
      const auto = r.auto !== false;
      const grantDiamonds = minByRank[r.id] ?? null;
      let diamonds = grantDiamonds;
      let via = grantDiamonds !== null ? 'gift' : null;
      if (auto && i > 0) {
        const byPoints = Math.ceil(Math.max(0, r.minPoints - joinPoints) / perDiamond);
        if (diamonds === null || byPoints < diamonds) { diamonds = byPoints; via = 'points'; }
      }
      const gift = diamonds !== null ? exampleGift(catalog, diamonds) : null;
      return {
        id: r.id, name: r.name, icon: r.icon, index: i,
        auto, minPoints: auto ? r.minPoints : null,
        grantDiamonds, diamonds, via, gift, exact: !!(gift && gift.diamonds === diamonds),
        throne: r.id === throneRank, start: i === 0,
      };
    });
    const crown = rows.find((r) => r.throne) || null;
    const cmds = {};
    for (const [k, v] of Object.entries(game.commands || {})) cmds[k] = first(v);

    return {
      rows, crown, cmds,
      perDiamond: (game.points && game.points.perDiamond) || 1,
      guide: game.guide || {},
    };
  }

  /** "Finger Heart (5 💎)"  |  "15 💎+ · örn. Doughnut"  |  "—" */
  function giftLabel(row) {
    if (row.diamonds === null) return row.start ? 'Hediyesiz' : '—';
    if (row.exact) return `${row.gift.name} (${fmt(row.diamonds)} 💎)`;
    return `${fmt(row.diamonds)} 💎+` + (row.gift ? ` · örn. ${row.gift.name}` : '');
  }

  /** Başlangıç rütbesi: komut (config'ten) · puanla gelinen rütbe: "N puan" · diğerleri: "hediye ile" */
  function pointsLabel(row, data) {
    if (row.start) return `🆓 ${data.cmds.join || ''}`.trim();
    return row.auto ? `${fmt(row.minPoints)} puan` : 'hediye ile';
  }

  /** Yan rehber satırı için kısa iki satır: hediye adı + (eşik · puan) */
  function railLines(row, data) {
    const gift = row.gift ? (row.exact ? row.gift.name : `örn. ${row.gift.name}`) : (row.start ? 'Hediyesiz' : '—');
    if (row.start) return { gift, detail: `🆓 ${data.cmds.join || ''}`.trim() };
    const parts = [];
    if (row.diamonds !== null) parts.push(`${fmt(row.diamonds)} 💎${row.exact ? '' : '+'}`);
    if (row.auto) parts.push(`${fmt(row.minPoints)} puan`);
    return { gift, detail: parts.join(' · ') };
  }

  /** Metin şablonu: {join} {guide} {crownDiamonds} {crownGift} {perDiamond} */
  function template(str, data) {
    const c = data.crown;
    const vars = {
      join: data.cmds.join || '', guide: data.cmds.guide || '', rank: data.cmds.rank || '',
      crownDiamonds: c && c.diamonds !== null ? fmt(c.diamonds) : '?',
      crownGift: c && c.gift ? c.gift.name : '',
      perDiamond: fmt(data.perDiamond),
    };
    return String(str || '').replace(/\{(\w+)\}/g, (_m, k) => (k in vars ? vars[k] : ''));
  }

  return { build, giftLabel, pointsLabel, railLines, template, fmt, exampleGift };
}));
