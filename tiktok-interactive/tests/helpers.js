'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { RankGame } = require('../games/rankgame');
const scenarioLoader = require('../scenarios');

function tmpDir() { return fs.mkdtempSync(path.join(os.tmpdir(), 'osmanli-test-')); }

/** Osmanlı senaryosuyla taze bir oyun; fx olayları `fx` dizisine toplanır. Zamanı (clock.t) elle ilerletebilirsin. */
function makeGame({ dir = tmpDir(), mutate, clock = { t: 1_700_000_000_000 }, fx = [] } = {}) {
  const sc = scenarioLoader.loadAll().osmanli;
  if (mutate) mutate(sc);
  const game = new RankGame(sc, { dataDir: dir, now: () => clock.t, emitFx: (type, p) => fx.push({ type, ...p }), debounceMs: 0 });
  return { game, fx, clock, dir, sc };
}

/** Aynı dizinde "sunucuyu yeniden başlat": yeni örnek, aynı kayıt dosyası */
function restart({ dir, sc, clock, fx = [] }) {
  const game = new RankGame(sc, { dataDir: dir, now: () => clock.t, emitFx: (type, p) => fx.push({ type, ...p }), debounceMs: 0 });
  return { game, fx };
}

let n = 0;
const user = (name = 'u' + (++n)) => ({ id: 'id_' + name, uniqueId: name, nickname: name, avatar: `/avatar/fake/${name}.svg` });

/** Hediye: `value` = BİRİM değer (giftDetails.diamondCount); toplam = value x count. Her olay benzersiz kimlik alır (id verilmezse). */
const gift = (u, name, value, count = 1, extra = {}) => ({
  type: 'gift', id: extra.id !== undefined ? extra.id : `g${++n}`, user: u,
  gift: Object.assign({
    id: name, name, count, valueEach: value, value: value * count, valueField: 'diamondCount', valueSource: 'message',
    diamondEach: value, diamonds: value * count, image: '',
  }, extra.gift),
});
const say = (u, text, id) => ({ type: 'comment', id: id !== undefined ? id : `c${++n}`, user: u, text });
const ev = (type, u, extra = {}) => Object.assign({ type, id: `e${++n}`, user: u }, extra);

module.exports = { makeGame, restart, user, gift, say, ev, tmpDir };
