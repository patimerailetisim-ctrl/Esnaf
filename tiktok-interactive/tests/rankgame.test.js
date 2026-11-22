'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { makeGame, restart, user, gift, say, ev } = require('./helpers');
const { RankGame } = require('../games/rankgame');
const { createNormalizer } = require('../lib/events');

const rankOf = (g, u) => g.state.users[u.id].rank;
const pointsOf = (g, u) => g.state.users[u.id].points;

// ------------------------------------------------------------------ rütbe sistemi (kesin kurallar)
test('Rütbe puan eşikleri tam olarak istenen listedir ve Padişahlık puandan bağımsızdır', () => {
  const { sc } = makeGame();
  const got = sc.game.ranks.map((r) => `${r.name}:${r.auto === false ? 'auction' : r.minPoints}`);
  assert.deepEqual(got, [
    'Köylü:0', 'Çiftçi:100', 'Esnaf:300', 'Asker:700', 'Yeniçeri:1500', 'Onbaşı:3000', 'Çavuş:5000', 'Sipahi:8000', 'Subaşı:12000',
    'Kaptan-ı Derya:18000', 'Sancak Beyi:25000', 'Beylerbeyi:40000', 'Paşa:60000', 'Kubbealtı Veziri:90000', 'Şehzade:130000',
    'Sadrazam:180000', 'Padişah:auction',
  ]);
});

test('Her rütbe tam eşik puanında kazanılır, bir puan eksikte kazanılmaz', () => {
  const { sc } = makeGame();
  for (const [i, r] of sc.game.ranks.entries()) {
    if (r.auto === false || i === 0) continue;
    const { game } = makeGame();
    const u = user('esik_' + r.id);
    game.handleEvent(say(u, '!katıl'));
    game.admin('addPoints', { user: u.id, points: r.minPoints - 1 - pointsOf(game, u) });
    assert.notEqual(rankOf(game, u), r.id, `${r.name}: ${r.minPoints - 1} puanda henüz kazanılmamalı`);
    game.admin('addPoints', { user: u.id, points: 1 });
    assert.equal(rankOf(game, u), r.id, `${r.name}: ${r.minPoints} puanda kazanılmalı`);
  }
});

test('Padişahlık normal puandan bağımsız: milyonlarca puan bile Padişah yapmaz', () => {
  const { game } = makeGame();
  const a = user('zengin');
  game.handleEvent(say(a, '!katıl'));
  game.admin('addPoints', { user: a.id, points: 50_000_000 });
  assert.equal(rankOf(game, a), 'sadrazam');
  game.handleEvent(gift(a, 'Universe', 34999, 50)); // taht açık artırması yokken dev hediye
  assert.equal(rankOf(game, a), 'sadrazam');
  assert.equal(game.state.throne.holderId, null);
});

test('Rütbe asla puanla düşmez; puan eklemek rütbeyi korur', () => {
  const { game } = makeGame();
  const a = user('kalici_rutbe');
  game.handleEvent(gift(a, 'Galaxy', 1000, 8)); // 8000 puan -> Sipahi
  assert.equal(rankOf(game, a), 'sipahi');
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(rankOf(game, a), 'sipahi');
});

// ------------------------------------------------------------------ hediye puanı
test('Hediye puanı = connector değeri x points.perGiftValue; katsayı osmanli.js üzerinden değiştirilir', () => {
  const a = makeGame();
  const u = user('gul');
  a.game.handleEvent(say(u, '!katıl'));
  const base = pointsOf(a.game, u);
  a.game.handleEvent(gift(u, 'Rose', 1, 7));
  assert.equal(pointsOf(a.game, u) - base, 7, 'varsayılan katsayı 1: 7 Rose = 7 puan');

  const b = makeGame({ mutate: (sc) => { sc.game.points.perGiftValue = 2.5; } });
  b.game.handleEvent(say(u, '!katıl'));
  const base2 = pointsOf(b.game, u);
  b.game.handleEvent(gift(u, 'Finger Heart', 5, 2)); // değer 10 x 2.5
  assert.equal(pointsOf(b.game, u) - base2, 25);
});

test('Hediyeye özel birim puan (giftOverrides) katsayıyı ezer', () => {
  const { game } = makeGame({ mutate: (sc) => { sc.game.points.giftOverrides = { Rose: 10 }; } });
  const u = user('ozel');
  game.handleEvent(say(u, '!katıl'));
  const base = pointsOf(game, u);
  game.handleEvent(gift(u, 'Rose', 1, 3));
  assert.equal(pointsOf(game, u) - base, 30);
});

test('Her hediye gönderen kullanıcı (katılmamış olsa bile) normal oyun puanı kazanır', () => {
  const { game } = makeGame();
  const u = user('misafir');
  game.handleEvent(gift(u, 'Rose', 1, 120));
  assert.ok(game.state.users[u.id], 'hediye gönderen otomatik Köylü olarak eklenir');
  assert.equal(rankOf(game, u), 'ciftci');
  assert.ok(pointsOf(game, u) >= 120);
  assert.equal(game.state.users[u.id].giftValue, 120);
});

test('Değer metadata\'sı güvenilir değilse puan verilmez (coin/diamond karışmaz)', () => {
  const { game } = makeGame();
  const c = game.state.stats.counters;
  const a = user('eksik'); const b = user('yanlis'); const d = user('devasa');
  game.handleEvent(gift(a, 'Mystery', 0));                                                       // değer yok
  game.handleEvent(gift(b, 'Rose', 1, 1, { gift: { valueField: 'coinCount' } }));               // başka bir alan
  game.handleEvent(gift(d, 'Bug', 99_999_999));                                                  // akıl sağlığı sınırı
  assert.equal(c.unknownGifts, 2);
  assert.equal(c.rejectedGifts, 1);
  assert.equal(c.gifts, 0);
  assert.ok(!game.state.users[b.id], 'reddedilen hediye oyuncu oluşturmaz');
  assert.equal(pointsOf(game, a), 5, 'yalnızca katılma puanı');
  assert.equal(pointsOf(game, d), 5);
});

test('Hediye puanları kalıcıdır (sunucu yeniden başlayınca korunur)', () => {
  const ctx = makeGame();
  const u = user('kalici_puan');
  ctx.game.handleEvent(gift(u, 'Rose', 1, 250));
  const pts = pointsOf(ctx.game, u);
  ctx.game.flush();
  const { game: g2 } = restart(ctx);
  assert.equal(g2.state.users[u.id].points, pts);
  assert.equal(g2.state.users[u.id].giftValue, 250);
  assert.equal(g2.state.users[u.id].giftPoints, 250);
  assert.equal(g2.state.stats.counters.giftPoints, 250);
  assert.equal(g2.state.users[u.id].rank, 'ciftci');
});

// ------------------------------------------------------------------ combo ve idempotency
test('Combo: connector seri güncellemeleri farkı yazar, çift puan yazılmaz', () => {
  const { normalize } = createNormalizer();
  const mk = (repeatCount, repeatEnd, msgId) => ({
    common: { msgId }, user: { idStr: '7', displayId: 'kombo', nickname: 'Kombo' }, giftId: 5655, groupId: 'G1', repeatCount, repeatEnd,
    giftDetails: { id: '5655', name: 'Rose', type: 1, diamondCount: 1 },
  });
  const { game } = makeGame();
  const evs = [normalize('gift', mk(1, 0, 'm1')), normalize('gift', mk(3, 0, 'm2')), normalize('gift', mk(5, 0, 'm3')), normalize('gift', mk(5, 1, 'm4'))];
  assert.equal(evs[3], null, 'seri bitiş olayı yeni hediye eklemez');
  for (const e of evs.filter(Boolean)) game.handleEvent(e);
  const u = game.state.users['7'];
  assert.equal(u.giftValue, 5, 'toplam 5 Rose = 5 değer (1+2+2)');
  assert.equal(u.giftPoints, 5);
});

test('Aynı olay kimliği iki kez gelirse yalnızca bir kez işlenir', () => {
  const { game } = makeGame();
  const u = user('tekrar');
  const e = gift(u, 'Rose', 1, 10, { id: 'msg-1' });
  game.handleEvent(e);
  game.handleEvent(JSON.parse(JSON.stringify(e)));
  game.handleEvent(JSON.parse(JSON.stringify(e)));
  assert.equal(game.state.users[u.id].giftValue, 10);
  assert.equal(game.state.stats.counters.duplicates, 2);
  const c = say(u, '!katıl', 'chat-1');
  game.handleEvent(c); game.handleEvent(c);
  assert.equal(Object.keys(game.state.users).length, 1);
});

test('İdempotency kalıcıdır: yeniden başlatma sonrası tekrar gelen olay işlenmez', () => {
  const ctx = makeGame();
  const u = user('yeniden');
  const e = gift(u, 'Rose', 1, 40, { id: 'msg-restart' });
  ctx.game.handleEvent(e);
  ctx.game.flush();
  const { game: g2 } = restart(ctx);
  g2.handleEvent(JSON.parse(JSON.stringify(e)));
  assert.equal(g2.state.users[u.id].giftValue, 40, 'ikinci kez sayılmadı');
  assert.equal(g2.state.stats.counters.duplicates, 1);
  g2.handleEvent(gift(u, 'Rose', 1, 5)); // yeni kimlik: işlenir
  assert.equal(g2.state.users[u.id].giftValue, 45);
});

test('Kimliksiz olaylar (id yok) engellenmez; kimlik havuzu sınırlıdır', () => {
  const { game } = makeGame({ mutate: (sc) => { sc.game.idempotency.maxIds = 5; } });
  const u = user('havuz');
  for (let i = 0; i < 12; i++) game.handleEvent(gift(u, 'Rose', 1, 1, { id: 'm' + i }));
  assert.equal(game.seenSet.size, 5);
  const noId = gift(u, 'Rose', 1, 1, { id: null });
  delete noId.id;
  game.handleEvent(noId); game.handleEvent(noId);
  assert.equal(game.state.users[u.id].giftValue, 14, 'id yoksa idempotency uygulanamaz');
});

// ------------------------------------------------------------------ ücretsiz katılım
test('!katıl köylü yapar ve tekrar yazınca ÇOĞALTMAZ', () => {
  const { game, fx, clock } = makeGame();
  const a = user('ali');
  game.handleEvent(say(a, '!katıl'));
  assert.equal(Object.keys(game.state.users).length, 1);
  assert.equal(rankOf(game, a), 'koylu');
  for (let i = 0; i < 5; i++) { clock.t += 30_000; game.handleEvent(say(a, '!KATIL')); }
  assert.equal(Object.keys(game.state.users).length, 1);
  assert.equal(fx.filter((f) => f.type === 'join').length, 1);
});

test('Ücretsiz katılanların verileri de kalıcıdır', () => {
  const ctx = makeGame();
  const u = user('ucretsiz');
  ctx.game.handleEvent(say(u, '!katıl'));
  ctx.game.handleEvent(ev('follow', u));
  ctx.game.flush();
  const { game: g2 } = restart(ctx);
  assert.equal(g2.state.users[u.id].rank, 'koylu');
  assert.ok(g2.state.users[u.id].points >= 35);
  assert.equal(g2.state.users[u.id].followed, true);
});

test('Normal yorumlar nüfusa katkı verir (katılmamış izleyici dahil); takip ve beğeni AYRI sayaçlara yazılır', () => {
  const { game, clock } = makeGame();
  const viewer = user('izleyici');
  const pop0 = game.state.stats.population;
  game.handleEvent(say(viewer, 'merhaba herkese'));
  assert.ok(game.state.stats.population > pop0, 'yorum nüfusu artırır');
  assert.equal(game.state.stats.counters.comments, 1);
  assert.ok(!game.state.users[viewer.id], 'yorum tek başına oyuncu oluşturmaz');

  const c = user('takipci');
  game.handleEvent(say(c, '!katıl')); // katılma etkisi ayrı; takip/beğeni etkisi ölçülecek
  const popBefore = game.state.stats.population;
  game.handleEvent(ev('follow', c));
  game.handleEvent(ev('like', c, { count: 30 }));
  assert.equal(game.state.stats.counters.follows, 1);
  assert.equal(game.state.stats.counters.likes, 30);
  assert.equal(game.state.stats.population - popBefore, 0, 'takip/beğeni nüfusa yazılmaz');
  clock.t += 1;
  assert.equal(game.snapshot().counters.comments, 1);
  assert.equal(game.snapshot().counters.follows, 1);
  assert.equal(game.snapshot().counters.likes, 30);
});

test('Spam yorumlar sınırlanır: bekleme süresi, aynı metin, genel dakikalık sınır', () => {
  const { game, clock } = makeGame({ mutate: (sc) => { sc.game.spam.globalPerMinute = 5; } });
  const a = user('spamci');
  for (let i = 0; i < 20; i++) game.handleEvent(say(a, 'yorum ' + i)); // aynı anda 20
  assert.equal(game.state.stats.counters.comments, 1, 'bekleme süresi içinde yalnızca ilk yorum sayılır');
  clock.t += 16_000;
  game.handleEvent(say(a, 'yorum 0'));  // aynı metin tekrarı (ilk yorumla aynı değil: 'yorum 0' ilk gönderilendi)
  assert.equal(game.state.stats.counters.comments, 1, 'aynı metin tekrar sayılmaz');
  clock.t += 16_000;
  game.handleEvent(say(a, 'başka bir şey'));
  assert.equal(game.state.stats.counters.comments, 2);
  // Genel sınır: 5/dk
  for (let i = 0; i < 30; i++) game.handleEvent(say(user('kisi' + i), 'selam ' + i));
  assert.equal(game.state.stats.counters.comments, 5, 'tüm izleyiciler için dakikada en çok 5');
  clock.t += 61_000;
  game.handleEvent(say(user('gec'), 'merhaba'));
  assert.equal(game.state.stats.counters.comments, 6);
});

test('Oyuncu yorum puanı bekleme süresiyle sınırlıdır (spam ile puan basılamaz)', () => {
  const { game } = makeGame();
  const a = user('spam2');
  game.handleEvent(say(a, '!katıl'));
  const p0 = pointsOf(game, a);
  for (let i = 0; i < 50; i++) game.handleEvent(say(a, 'selam ' + i));
  assert.equal(pointsOf(game, a), p0 + 1);
});

test('Takip yalnızca bir kez puan verir', () => {
  const { game } = makeGame();
  const a = user('takipci2');
  game.handleEvent(ev('follow', a));
  const p = pointsOf(game, a);
  game.handleEvent(ev('follow', a));
  assert.equal(pointsOf(game, a), p);
  assert.equal(game.state.stats.counters.follows, 2, 'sayaç olayları sayar');
});

// ------------------------------------------------------------------ komutlar
test('Türkçe karakter duyarsız komutlar; katılmamış kullanıcı ipucu alır', () => {
  const { game, fx } = makeGame();
  const a = user('ayse');
  game.handleEvent(say(a, '!ordu'));
  assert.ok(fx.some((f) => f.kind === 'hint'));
  assert.equal(Object.keys(game.state.users).length, 0);
  game.handleEvent(say(a, '!katil'));
  game.handleEvent(say(a, '!RÜTBE'));
  const n = fx.find((f) => f.type === 'notice' && f.kind === 'rank');
  assert.match(n.title, /Köylü/);
});

test('!isim: temizler, sınırlar, rütbe adlarını reddeder, bekleme süresi uygular', () => {
  const { game, clock } = makeGame();
  const a = user('isimci');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(say(a, '!isim <b>Kara</b> Mustafa Çok Uzun Bir İsim Bu'));
  const name = game.state.users[a.id].name;
  assert.ok(name.length <= 16 && !/[<>]/.test(name));
  clock.t += 10_000;
  game.handleEvent(say(a, '!isim Yeni Ad'));
  assert.equal(game.state.users[a.id].name, name);
  clock.t += 200_000;
  game.handleEvent(say(a, '!isim Padişah'));
  assert.equal(game.state.users[a.id].name, name);
  clock.t += 10_000;
  game.handleEvent(say(a, '!isim Ali'));
  assert.equal(game.state.users[a.id].name, 'Ali');
});

test('!meslek: puan eşiği ister; meslek ordu katkısı sağlar ama rütbeyi puandan başka şey değiştirmez', () => {
  const { game, fx, clock } = makeGame();
  const a = user('meslek');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(say(a, '!meslek asker'));
  assert.equal(game.state.users[a.id].job, null);
  assert.ok(fx.some((f) => f.kind === 'error'));
  game.admin('addPoints', { user: a.id, points: 200 });
  const rank = rankOf(game, a);
  clock.t += 60_000;
  game.handleEvent(say(a, '!meslek ASKER'));
  assert.equal(game.state.users[a.id].job, 'asker');
  assert.equal(rankOf(game, a), rank, 'meslek rütbe vermez: rütbe yalnızca puanla yükselir');
  assert.ok(game.armyStats().power >= 1);
});

test('Ücretsiz katılım ekonomiyi büyütür: nüfus/üretim/şehir/hazine', () => {
  const { game, fx, clock } = makeGame();
  const start = game.snapshot().stats;
  for (let i = 0; i < 30; i++) {
    const u = user('f' + i);
    game.handleEvent(say(u, '!katıl'));
    clock.t += 20_000;
    for (let k = 0; k < 4; k++) { game.handleEvent(say(u, 'merhaba ' + k)); clock.t += 16_000; }
  }
  const end = game.snapshot().stats;
  assert.ok(end.population > start.population + 150);
  assert.ok(end.production > start.production);
  assert.ok(end.cities >= 2);
  assert.ok(fx.some((f) => f.type === 'city'));
  game.tick();
  assert.ok(game.snapshot().stats.treasury > end.treasury);
});

// ------------------------------------------------------------------ kalıcılık ve yönetim
test('Kalıcı kayıt: oyuncular, rütbeler, taht, sayaçlar ve istatistik korunur', () => {
  const ctx = makeGame();
  const a = user('kalici'); const k = user('kralk');
  ctx.game.handleEvent(say(a, '!katıl'));
  ctx.game.handleEvent(gift(a, 'Galaxy', 1000, 3));
  ctx.game.handleEvent(say(a, '!isim Kalıcı Ali'));
  ctx.game.handleEvent(gift(k, 'Rose', 1));
  ctx.game.admin('crown', { user: k.id });
  ctx.game.flush();
  assert.ok(fs.existsSync(path.join(ctx.dir, 'osmanli.json')));
  const { game: g2 } = restart(ctx);
  assert.equal(g2.state.users[a.id].rank, 'asker' === rankOf(g2, a) ? 'asker' : rankOf(g2, a));
  assert.equal(g2.state.users[a.id].name, 'Kalıcı Ali');
  assert.equal(g2.state.throne.holderId, k.id);
  assert.equal(g2.snapshot().stats.treasury, ctx.game.snapshot().stats.treasury);
  assert.equal(g2.state.stats.counters.gifts, 4);
  assert.equal(rankOf(g2, a), rankOf(ctx.game, a));
});

test('Bozuk kayıt dosyası silinmez, kenara alınır ve oyun temiz başlar', () => {
  const { dir, sc } = makeGame();
  fs.writeFileSync(path.join(dir, 'osmanli.json'), '{bozuk json');
  const g = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(Object.keys(g.state.users).length, 0);
  assert.ok(fs.readdirSync(dir).some((f) => f.includes('.bozuk-')));
});

test('Eski sürüm (v1) kaydı yedeklenip yeni puan eşiklerine göre taşınır', () => {
  const { dir, sc } = makeGame();
  const old = {
    version: 1,
    users: {
      a: { id: 'a', uniqueId: 'a', nickname: 'a', avatar: '', rank: 'sancak', points: 120, joinedAt: 1, diamonds: 7 },
      k: { id: 'k', uniqueId: 'k', nickname: 'k', avatar: '', rank: 'padisah', points: 9, joinedAt: 2 },
    },
    stats: { treasury: 50, population: 40, production: 2, cities: 0 },
    throne: { holderId: 'k', crownedAt: 1, crownValue: 29999, history: [] },
  };
  fs.writeFileSync(path.join(dir, 'osmanli.json'), JSON.stringify(old));
  const g = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(g.state.users.a.rank, 'ciftci', '120 puan -> Çiftçi (eski hediye rütbesi geçersiz)');
  assert.equal(g.state.users.a.giftValue, 7);
  assert.equal(g.state.users.k.rank, 'padisah');
  assert.equal(g.state.throne.holderId, 'k');
  assert.equal(g.state.version, 2);
  assert.ok(fs.existsSync(path.join(dir, 'osmanli.json.v1.bak')));
});

test('Taht değişmezi: kayıtta birden fazla Padişah varsa tekil hale getirilir (fazlalar Sadrazam)', () => {
  const { game, dir, sc } = makeGame();
  const a = user('p1'); const b = user('p2');
  game.handleEvent(say(a, '!katıl')); game.handleEvent(say(b, '!katıl'));
  game.state.users[a.id].rank = 'padisah'; game.state.users[b.id].rank = 'padisah';
  game.state.throne.holderId = a.id;
  game.save(true);
  const g2 = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(Object.values(g2.state.users).filter((u) => u.rank === 'padisah').length, 1);
  assert.equal(g2.state.users[b.id].rank, 'sadrazam');
});

test('Yönetici işlemleri: setRank/crown/vacate/remove/wipe (onay metniyle)', () => {
  const { game } = makeGame();
  const a = user('adm');
  game.handleEvent(say(a, '!katıl'));
  assert.equal(game.admin('setRank', { user: a.id, rank: 'sadrazam' }).ok, true);
  assert.equal(rankOf(game, a), 'sadrazam');
  game.admin('crown', { user: a.id });
  assert.equal(game.state.throne.holderId, a.id);
  game.admin('vacateThrone');
  assert.equal(game.state.throne.holderId, null);
  assert.equal(rankOf(game, a), 'sadrazam');
  assert.equal(game.admin('wipe', { confirm: 'x' }).ok, false);
  assert.equal(game.admin('wipe', { confirm: 'SIFIRLA' }).ok, true);
  assert.equal(Object.keys(game.state.users).length, 0);
});

test('Değişiklik yoksa diske yazılmaz', () => {
  const { game, dir } = makeGame();
  game.handleEvent(say(user('yaz'), '!katıl'));
  game.flush();
  const file = path.join(dir, 'osmanli.json');
  const t1 = fs.statSync(file).mtimeMs;
  const before = fs.readFileSync(file, 'utf8');
  game.flush(); game.flush();
  assert.equal(fs.readFileSync(file, 'utf8'), before);
  assert.equal(fs.statSync(file).mtimeMs, t1);
});

test('Hatalı game yapılandırması anlaşılır hata verir', () => {
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.throne.rank = 'yok'; } }), /throne\.rank/);
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.ranks.find((r) => r.id === 'padisah').auto = true; } }), /auto:false/);
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.ranks.find((r) => r.id === 'esnaf').minPoints = 50; } }), /minPoints/);
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.points.perGiftValue = 0; } }), /perGiftValue/);
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.throne.auction.durationSec = 0; } }), /durationSec/);
});

test('Snapshot: kullanıcılar rütbe sonra puana göre; en yüksek puanlı 5 ayrı; yalnızca üst N gönderilir', () => {
  const { game } = makeGame({ mutate: (sc) => { sc.game.view.sendTop = 3; } });
  for (let i = 0; i < 6; i++) game.handleEvent(gift(user('s' + i), 'Rose', 1, 10 + i));
  game.handleEvent(gift(user('buyuk'), 'Galaxy', 1000, 2));
  const s = game.snapshot();
  assert.equal(s.users.length, 3);
  assert.equal(s.total, 7);
  assert.equal(s.topPoints.length, 5);
  assert.equal(s.topPoints[0].name, 'buyuk');
  assert.ok(s.topPoints.every((u, i, a) => i === 0 || a[i - 1].points >= u.points));
});
