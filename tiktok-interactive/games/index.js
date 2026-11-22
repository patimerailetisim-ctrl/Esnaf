'use strict';
/**
 * OYUN MODÜLÜ KAYIT DEFTERİ
 * -------------------------
 * Bir senaryo `game: { type: 'rankgame', ... }` içeriyorsa sunucu burada o tipin fabrikasını arar.
 * Yeni bir oyun tipi yazarsan buraya bir satır ekle. `game` içermeyen senaryolar (Halay, Uzay)
 * bu katmandan hiç etkilenmez.
 */
const { RankGame } = require('./rankgame');

const TYPES = {
  rankgame: (scenario, opts) => new RankGame(scenario, opts),
};

function create(scenario, opts) {
  if (!scenario || !scenario.game) return null;
  const factory = TYPES[scenario.game.type];
  if (!factory) throw new Error(`Bilinmeyen oyun tipi: "${scenario.game.type}" (senaryo: ${scenario.id})`);
  return factory(scenario, opts);
}

module.exports = { create, TYPES };
