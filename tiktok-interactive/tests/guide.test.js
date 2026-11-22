'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { makeGame, user, gift, say } = require('./helpers');
const { RankGame } = require('../games/rankgame');
const GuideData = require('../public/js/guide-data.js');

const rankOf = (g, u) => g.state.users[u.id].rank;

test('Rehber verisi senaryo dosyasından okunur: her rütbe için eşik, örnek hediye ve puan doğru', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  assert.equal(d.rows.length, sc.game.ranks.length);
  for (const row of d.rows) {
    const rank = sc.game.ranks.find((r) => r.id === row.id);
    const grants = sc.game.giftRankGrants.filter((g) => g.rank === row.id).map((g) => g.minDiamonds);
    const grant = grants.length ? Math.min(...grants) : null;
    assert.equal(row.grantDiamonds, grant, `${row.id}: config'teki hediye eşiği`);
    const idx = sc.game.ranks.indexOf(rank);
    const byPts = rank.auto !== false && idx > 0 ? Math.ceil(Math.max(0, rank.minPoints - sc.game.points.join) / sc.game.points.perDiamond) : Infinity;
    const expected = Math.min(grant ?? Infinity, byPts);
    assert.equal(row.diamonds, expected === Infinity ? null : expected, `${row.id}: etkin hediye eşiği (hediye eşiği / puan yolundan düşük olan)`);
    assert.equal(row.minPoints, rank.auto !== false ? rank.minPoints : null, `${row.id}: puan eşiği`);
    if (row.gift) {
      assert.ok(row.gift.diamonds >= row.diamonds, `${row.id}: örnek hediye eşiğe ulaşmalı`);
      const cheaper = sc.game.testGifts.filter((g) => g.diamonds >= row.diamonds && g.diamonds < row.gift.diamonds);
      assert.equal(cheaper.length, 0, `${row.id}: örnek, eşiğe ulaşan en ucuz hediye olmalı`);
    }
  }
  assert.equal(d.crown.id, 'padisah');
  assert.equal(d.crown.diamonds, 29999);
  assert.equal(d.crown.gift.name, 'Lion');
});

test('Rehber metinleri değişkenleri config\'ten doldurur (komut adı, taç bedeli, puan çarpanı)', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  const g = sc.game.guide.texts;
  assert.equal(GuideData.template(g.freeLine, d), `Yorumuna ${sc.game.commands.join[0]} yaz, Köylü ol!`);
  assert.equal(GuideData.template(g.throneLine, d), 'Tek hediyede 29.999 💎 (Lion)');
  assert.equal(GuideData.template(g.pointsLine, d), '1 💎 = 10 puan · combo toplamı sayılır');
  assert.equal(GuideData.template(g.moreLine, d), `Tüm liste: ${sc.game.commands.guide[0]}`);
});

test('Config değişince rehber de değişir (ikinci kez sabit yazılmış değer yok)', () => {
  const { sc } = makeGame({
    mutate: (s) => {
      s.game.giftRankGrants.find((x) => x.rank === 'padisah').minDiamonds = 50000;
      s.game.points.perDiamond = 25;
      s.game.commands.join = ['!gir'];
      s.game.testGifts.push({ name: 'Mega Gift', diamonds: 50000, icon: '🎆' });
      s.game.ranks.find((r) => r.id === 'ciftci').minPoints = 777;
    },
  });
  const d = GuideData.build(sc.game);
  assert.equal(d.crown.diamonds, 50000);
  assert.equal(d.crown.gift.name, 'Mega Gift');
  assert.equal(d.rows.find((r) => r.id === 'ciftci').minPoints, 777);
  assert.equal(GuideData.template(sc.game.guide.texts.pointsLine, d), '1 💎 = 25 puan · combo toplamı sayılır');
  assert.match(GuideData.template(sc.game.guide.texts.freeLine, d), /!gir/);
  assert.equal(GuideData.pointsLabel(d.rows[0], d), '🆓 !gir');
});

test('Rehberin gösterdiği HER hediye eşiği gerçek oyun mekaniğiyle birebir uyuşur', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  let n = 0;
  for (const row of d.rows) {
    if (row.diamonds === null) continue;
    const { game } = makeGame();            // her eşik temiz oyunda (taht boş) denenir
    const u = user('esik' + (++n));
    game.handleEvent(gift(u, 'EsikTest', row.diamonds));
    assert.equal(rankOf(game, u), row.id, `${row.name}: ${row.diamonds} 💎 tek hediye bu rütbeyi vermeli`);
  }
  assert.ok(n >= 10);
});

test('Rehberdeki puan eşikleri gerçek terfiyle uyuşur (auto rütbeler)', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  for (const row of d.rows.filter((r) => r.auto && !r.start)) {
    const { game } = makeGame();
    const u = user('puan_' + row.id);
    game.handleEvent(say(u, '!katıl'));
    game.admin('addPoints', { user: u.id, points: row.minPoints - game.state.users[u.id].points });
    assert.equal(rankOf(game, u), row.id, `${row.name}: ${row.minPoints} puan`);
  }
});

test('!hediyeler katılmadan da açar; genel bekleme süresi spam\'i engeller; takma adlar çalışır', () => {
  const { game, fx, clock } = makeGame();
  const anon = user('izleyici');
  game.handleEvent(say(anon, '!hediyeler'));
  let open = fx.filter((f) => f.type === 'guide');
  assert.equal(open.length, 1);
  assert.equal(open[0].mode, 'open');
  assert.equal(open[0].seconds, 15);
  assert.equal(open[0].reason, 'command');
  assert.equal(Object.keys(game.state.users).length, 0, 'komut oyuncu oluşturmaz');
  game.handleEvent(say(user('baska'), '!HEDİYELER'));
  game.handleEvent(say(anon, '!rehber'));
  assert.equal(fx.filter((f) => f.type === 'guide').length, 1, '20 sn içinde tekrar açılmaz (kimse ekranı kilitleyemez)');
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

test('Hediye olayı rehber animasyonu için puan, rütbe ve sonraki rütbe bilgisini taşır (mekanik değişmez)', () => {
  const { game, fx } = makeGame();
  const a = user('gul');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(gift(a, 'Rose', 1));
  const g1 = fx.filter((f) => f.type === 'gift').pop();
  assert.equal(g1.points, 10);
  assert.equal(g1.rank, 'koylu');
  assert.equal(g1.grantRank, null, 'Rose (1 💎) rütbe eşiğine ulaşmaz');
  assert.equal(g1.rankChanged, false);
  assert.equal(g1.next.id, 'ciftci');
  assert.equal(g1.next.need, 50 - g1.total);
  game.handleEvent(gift(a, 'Finger Heart', 5));
  const g2 = fx.filter((f) => f.type === 'gift').pop();
  assert.equal(g2.grantRank, 'ciftci');
  assert.equal(g2.rankChanged, true);
  assert.equal(g2.points, 50);
  const b = user('kral');
  game.handleEvent(gift(b, 'Lion', 29999));
  const g3 = fx.filter((f) => f.type === 'gift').pop();
  assert.equal(g3.crowned, true);
  assert.equal(g3.grantRank, 'padisah');
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
  assert.equal(g2.snapshot().ui.guideVisible, false, 'sunucu yeniden başlayınca gizli kalır');
});

test('Rehber ayarı rütbe/hediye/taht mekaniklerini değiştirmez (aynı olay dizisi aynı sonucu verir)', () => {
  const run = (mutate) => {
    const { game, clock } = makeGame({ mutate });
    const a = user('m1'); const b = user('m2'); const c = user('m3');
    game.handleEvent(gift(a, 'Galaxy', 1000));
    game.handleEvent(gift(b, 'Lion', 29999));
    game.handleEvent(gift(c, 'Universe', 34999));
    clock.t += 200_000;
    game.handleEvent(gift(c, 'Universe', 34999));
    return [a, b, c].map((u) => game.state.users[u.id].rank + ':' + Math.floor(game.state.users[u.id].points)).join(',') + '|' + game.state.throne.holderId;
  };
  const base = run();
  assert.equal(run((sc) => { delete sc.game.guide; }), base, 'rehber bölümü olmasa da sonuç aynı');
  assert.equal(run((sc) => { sc.game.guide.visible = false; sc.game.guide.pageSize = 7; }), base);
});

test('Yan rehber satırları: hediye adı + eşik + puan config\'ten gelir', () => {
  const { sc } = makeGame();
  const d = GuideData.build(sc.game);
  const row = (id) => d.rows.find((r) => r.id === id);
  assert.deepEqual(GuideData.railLines(row('ciftci'), d), { gift: 'Finger Heart', detail: '5 💎 · 50 puan' });
  assert.deepEqual(GuideData.railLines(row('yenicer'), d), { gift: 'örn. Hand Hearts', detail: '60 💎+ · 600 puan' });
  assert.deepEqual(GuideData.railLines(row('padisah'), d), { gift: 'Lion', detail: '29.999 💎' });
  assert.deepEqual(GuideData.railLines(row('koylu'), d), { gift: 'Hediyesiz', detail: '🆓 !katıl' });
});

test('Silinen oyuncu taç geçmişinden de temizlenir', () => {
  const { game } = makeGame();
  const a = user('gecici');
  game.handleEvent(gift(a, 'Lion', 29999));
  assert.equal(game.state.throne.history.length, 1);
  game.admin('removeUser', { user: a.id });
  assert.equal(game.state.throne.history.length, 0);
  assert.equal(game.state.throne.holderId, null);
});
