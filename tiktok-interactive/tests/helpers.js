'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { RankGame } = require('../games/rankgame');
const scenarioLoader = require('../scenarios');

function tmpDir() { return fs.mkdtempSync(path.join(os.tmpdir(), 'osmanli-test-')); }

/** Osmanlı senaryosuyla taze bir oyun; fx olayları `fx` dizisine toplanır. Zamanı elle ilerletebilirsin. */
function makeGame({ dir = tmpDir(), mutate } = {}) {
  const sc = scenarioLoader.loadAll().osmanli;
  if (mutate) mutate(sc);
  const clock = { t: 1_700_000_000_000 };
  const fx = [];
  const game = new RankGame(sc, { dataDir: dir, now: () => clock.t, emitFx: (type, p) => fx.push({ type, ...p }), debounceMs: 0 });
  return { game, fx, clock, dir, sc };
}

let n = 0;
const user = (name = 'u' + (++n)) => ({ id: 'id_' + name, uniqueId: name, nickname: name, avatar: `/avatar/fake/${name}.svg` });
const gift = (u, name, diamonds, count = 1) => ({ type: 'gift', user: u, gift: { id: name, name, diamondEach: diamonds / count, diamonds, count, image: '' } });
const say = (u, text) => ({ type: 'comment', user: u, text });

module.exports = { makeGame, user, gift, say, tmpDir };
