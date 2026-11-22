'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { makeGame, user, gift, say } = require('./helpers');
const { RankGame } = require('../games/rankgame');
const GuideData = require('../public/js/guide-data.js');

const rankOf = (g, u) => g.state.users[u.id].rank;

test('Rehber verisi senaryo dosyasından okunur: her rütbe için puan eşiği ve hediye karşılığı doğru', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  assert.equal(d.rows.length, sc.game.ranks.length);
  for (const row of d.rows) {
    const rank = sc.game.ranks.find((r) => r.id === row.id);
    assert.equal(row.minPoints, rank.auto !== false ? rank.minPoints : null, `${row.id}: puan eşiği`);
    if (row.pointsNeeded > 0) {
      const ex = row.example;
      assert.ok(ex, `${row.id}: örnek hediye olmalı`);
      assert.ok(sc.game.giftCatalog.some((g) => g.name === ex.gift.name), `${row.id}: örnek hediye katalogdan`);
      assert.equal(ex.points, GuideData.giftPoints(sc.game, ex.gift) * ex.count, `${row.id}: örnek puanı katsayıdan`);
      assert.ok(ex.points >= row.pointsNeeded, `${row.id}: örnek hediye eşiğe ulaşmalı`);
      assert.ok(ex.points <= row.pointsNeeded * 1.15 + 1, `${row.id}: örnek eşiği makul aşmalı (%15)`);
    }
  }
  assert.equal(d.crown.id, 'padisah');
  assert.equal(GuideData.giftLabel(d.crown), 'En yüksek teklif');
  assert.equal(d.auction.everySec, 600);
  assert.equal(d.auction.durationSec, 60);
});

test('Rehber metinleri değişkenleri config\'ten doldurur (komut adı, puan katsayısı, açık artırma süreleri)', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  const t = sc.game.guide.texts;
  assert.equal(GuideData.template(t.freeLine, d), `Yorumuna ${sc.game.commands.join[0]} yaz, Köylü ol!`);
  assert.equal(GuideData.template(t.throneRail, d), 'Açık artırmada en yüksek teklif · her 10 dk');
  assert.equal(GuideData.template(t.throneMore, d), '60 sn sürer · son 10 sn teklif +10 sn uzatır');
  assert.equal(GuideData.template(t.pointsLine, d), '1 💎 hediye değeri = 1 puan · combo toplamı sayılır');
  assert.equal(GuideData.template(t.moreLine, d), `Tüm liste: ${sc.game.commands.guide[0]}`);
});

test('Config değişince rehber de değişir (ikinci kez sabit yazılmış değer yok)', () => {
  const { sc } = makeGame({
    mutate: (s) => {
      s.game.points.perGiftValue = 4;
      s.game.commands.join = ['!gir'];
      s.game.throne.auction.intervalSec = 900;
      s.game.throne.auction.durationSec = 90;
      s.game.throne.auction.extendBySec = 15;
      s.game.ranks.find((r) => r.id === 'ciftci').minPoints = 250;
      s.game.giftCatalog.push({ name: 'Mega Gift', value: 50000, icon: '🎆' });
    },
  });
  const d = GuideData.build(sc.game);
  assert.equal(d.rows.find((r) => r.id === 'ciftci').minPoints, 250);
  assert.equal(GuideData.template(sc.game.guide.texts.pointsLine, d), '1 💎 hediye değeri = 4 puan · combo toplamı sayılır');
  assert.match(GuideData.template(sc.game.guide.texts.freeLine, d), /!gir/);
  assert.equal(GuideData.template(sc.game.guide.texts.throneRail, d), 'Açık artırmada en yüksek teklif · her 15 dk');
  assert.match(GuideData.template(sc.game.guide.texts.throneMore, d), /90 sn sürer.*\+15 sn/);
  assert.equal(GuideData.pointsLabel(d.rows[0], d), '🆓 !gir');
  // katsayı 4 olunca aynı puan için 4 kat daha az hediye değeri gerekir
  const sipahiBase = GuideData.build(makeGame().sc.game).rows.find((r) => r.id === 'sipahi').example.value;
  assert.ok(d.rows.find((r) => r.id === 'sipahi').example.value < sipahiBase / 3);
});

test('Rehberin gösterdiği HER puan eşiği gerçek oyun mekaniğiyle birebir uyuşur (eşik değerinde kazanılır, 1 eksikte kazanılmaz)', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  let n = 0;
  for (const row of d.rows) {
    if (!row.pointsNeeded) continue;
    const need = Math.ceil(row.pointsNeeded / d.perGiftValue); // en düşük hediye değeri
    const a = makeGame().game; const b = makeGame().game;
    const u = user('esik' + (++n));
    a.handleEvent(gift(u, 'EsikTest', need));
    assert.equal(rankOf(a, u), row.id, `${row.name}: ${need} değerinde tek hediye bu rütbeyi vermeli`);
    b.handleEvent(gift(u, 'EsikTest', need - 1));
    assert.notEqual(rankOf(b, u), row.id, `${row.name}: ${need - 1} değeri yetmemeli`);
  }
  assert.ok(n >= 15);
});

test('!hediyeler katılmadan da açar; genel bekleme süresi spam\'i engeller; takma adlar çalışır', () => {
  const { game, fx, clock } = makeGame();
  const anon = user('izleyici');
  game.handleEvent(say(anon, '!hediyeler'));
  const open = fx.filter((f) => f.type === 'guide');
  assert.equal(open.length, 1);
  assert.deepEqual([open[0].mode, open[0].seconds, open[0].reason], ['open', 15, 'command']);
  assert.equal(Object.keys(game.state.users).length, 0, 'komut oyuncu oluşturmaz');
  game.handleEvent(say(user('baska'), '!HEDİYELER'));
  game.handleEvent(say(anon, '!rehber'));
  assert.equal(fx.filter((f) => f.type === 'guide').length, 1);
  clock.t += 21_000;
  game.handleEvent(say(anon, '!hediye'));
  assert.equal(fx.filter((f) => f.type === 'guide').length, 2);
});

test('Yayın açılışı: intro() 10 sn açar; config ile kapatılabilir', () => {
  const a = makeGame();
  a.game.intro();
  const f = a.fx.find((x) => x.type === 'guide');
  assert.equal(f.seconds, 10);
  assert.equal(f.reason, 'intro');
  const b = makeGame({ mutate: (sc) => { sc.game.guide.intro.onLiveConnect = false; } });
  b.game.intro();
  assert.equal(b.fx.filter((x) => x.type === 'guide').length, 0);
});

test('Hediye olayı rehber animasyonu için puan, rütbe ve sonraki rütbe bilgisini taşır', () => {
  const { game, fx } = makeGame();
  const a = user('gul');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(gift(a, 'Rose', 1));
  const g1 = fx.filter((f) => f.type === 'gift').pop();
  assert.equal(g1.points, 1);
  assert.equal(g1.rank, 'koylu');
  assert.equal(g1.rankChanged, false);
  assert.equal(g1.next.id, 'ciftci');
  assert.equal(g1.next.need, 100 - g1.total);
  assert.equal(g1.bid, null, 'açık artırma yokken teklif bilgisi yok');
  game.handleEvent(gift(a, 'Hand Hearts', 100));
  const g2 = fx.filter((f) => f.type === 'gift').pop();
  assert.equal(g2.points, 100);
  assert.equal(g2.rankChanged, true);
  assert.equal(g2.rank, 'ciftci');
  assert.equal(g2.next.id, 'esnaf');
});

test('Rehber göster/gizle ayarı kalıcıdır ve anlık görüntüye girer', () => {
  const { game, dir, sc } = makeGame();
  assert.equal(game.snapshot().ui.guideVisible, true);
  assert.equal(game.admin('guide', { visible: false }).ok, true);
  assert.equal(game.snapshot().ui.guideVisible, false);
  game.admin('guide', { visible: 'toggle' });
  assert.equal(game.snapshot().ui.guideVisible, true);
  game.admin('guide', { visible: false });
  game.flush();
  const g2 = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(g2.snapshot().ui.guideVisible, false);
});

test('Yan rehber satırları: hediye karşılığı + değer + puan config\'ten gelir', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  const row = (id) => d.rows.find((r) => r.id === id);
  assert.deepEqual(GuideData.railLines(row('ciftci'), d), { gift: '≈ Hand Hearts', detail: '100 💎 · 100 puan' });
  assert.deepEqual(GuideData.railLines(row('esnaf'), d), { gift: '≈ 3× Hand Hearts', detail: '300 💎 · 300 puan' });
  assert.deepEqual(GuideData.railLines(row('padisah'), d), { gift: 'En yüksek teklif', detail: 'her 10 dk · 60 sn' });
  assert.deepEqual(GuideData.railLines(row('koylu'), d), { gift: 'Hediyesiz', detail: '🆓 !katıl' });
});

test('Silinen oyuncu taç geçmişinden ve tekliflerinden de temizlenir', () => {
  const { game } = makeGame();
  const a = user('gecici');
  game.handleEvent(say(a, '!katıl'));
  game.admin('crown', { user: a.id });
  assert.equal(game.state.throne.history.length, 1);
  game.admin('removeUser', { user: a.id });
  assert.equal(game.state.throne.history.length, 0);
  assert.equal(game.state.throne.holderId, null);
});
