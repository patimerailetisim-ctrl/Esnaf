'use strict';
/**
 * SENARYO YÜKLEYİCİ
 * -----------------
 * scenarios/ klasöründeki her .js dosyası bir senaryodur.
 *  - "index.js" ve "_" ile başlayan dosyalar (örn: _sablon.js) yüklenmez.
 *  - Yeni senaryo = klasöre yeni bir .js dosyası eklemek. Başka hiçbir yere dokunmana gerek yok.
 */
const fs = require('fs');
const path = require('path');

const REQUIRED = ['id', 'name'];

function loadAll() {
  const result = {};
  const files = fs.readdirSync(__dirname)
    .filter((f) => f.endsWith('.js') && f !== 'index.js' && !f.startsWith('_'));

  for (const file of files) {
    const full = path.join(__dirname, file);
    try {
      delete require.cache[require.resolve(full)]; // sıcak yeniden yükleme
      const sc = require(full);
      const missing = REQUIRED.filter((k) => !sc[k]);
      if (missing.length) throw new Error('Eksik alan(lar): ' + missing.join(', '));
      // JSON'a çevrilebilir olduğundan emin ol (fonksiyon yok, sadece veri)
      result[sc.id] = JSON.parse(JSON.stringify(sc));
    } catch (err) {
      console.error(`[senaryo] "${file}" yüklenemedi: ${err.message}`);
    }
  }
  return result;
}

module.exports = { loadAll };
