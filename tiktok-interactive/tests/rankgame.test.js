'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { makeGame, user, gift, say } = require('./helpers');
const { RankGame } = require('../games/rankgame');

const rankOf = (g, u) => g.state.users[u.id].rank;
const types = (fx) => fx.map((f) => f.type);

test('!katıl köylü olarak ekler ve tekrar yazınca ÇOĞALTMAZ', () => {
  const { game, fx, clock } = makeGame();
  const a = user('ali');
  game.handleEvent(say(a, '!katıl'));
  assert.equal(Object.keys(game.state.users).length, 1);
  assert.equal(rankOf(game, a), 'koylu');
  for (let i = 0; i < 5; i++) { clock.t += 30_000; game.handleEvent(say(a, '!KATIL')); }
  assert.equal(Object.keys(game.state.users).length, 1, 'aynı kullanıcı tekrar eklenmemeli');
  assert.equal(fx.filter((f) => f.type === 'join').length, 1);
});

test('Türkçe karakter duyarsız komutlar (!rütbe == !rutbe)', () => {
  const { game, fx } = makeGame();
  const a = user('ayse');
  game.handleEvent(say(a, '!katil'));
  game.handleEvent(say(a, '!RÜTBE'));
  const n = fx.find((f) => f.type === 'notice' && f.kind === 'rank');
  assert.ok(n, 'rütbe kartı üretilmeli');
  assert.match(n.title, /Köylü/);
});

test('Katılmamış kullanıcı komut kullanınca ipucu alır, kayıt oluşmaz', () => {
  const { game, fx } = makeGame();
  game.handleEvent(say(user('x'), '!ordu'));
  assert.equal(Object.keys(game.state.users).length, 0);
  assert.ok(fx.some((f) => f.kind === 'hint'));
});

test('Rose (1 elmas) küçük puan verir, rütbe sıçratmaz; 5 Rose çiftçi yapar', () => {
  const { game } = makeGame();
  const a = user('gul');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(rankOf(game, a), 'koylu');
  for (let i = 0; i < 4; i++) game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(rankOf(game, a), 'ciftci', '5 Rose ≈ 5+50 puan → Çiftçi');
});

test('Hediye eşikleri config\'ten gelir: eşik değişince sonuç değişir', () => {
  const { game } = makeGame({ mutate: (sc) => { sc.game.giftRankGrants = [{ minDiamonds: 1, rank: 'yenicer' }]; } });
  const a = user('cfg');
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(rankOf(game, a), 'yenicer');
});

test('Büyük hediyeler yüksek rütbe verir ve rütbe asla düşmez', () => {
  const { game } = makeGame();
  const a = user('zengin');
  game.handleEvent(gift(a, 'Galaxy', 1000));
  assert.equal(rankOf(game, a), 'beylerbeyi');
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(rankOf(game, a), 'beylerbeyi');
  game.handleEvent(gift(a, 'Sports Car', 7000));
  assert.equal(rankOf(game, a), 'sadrazam');
});

test('Puanla otomatik terfi yalnızca auto rütbelere kadar gider (Sadrazam/Şehzade/Padişah puanla gelinmez)', () => {
  const { game } = makeGame();
  const a = user('puan');
  game.handleEvent(say(a, '!katıl'));
  game.admin('addPoints', { user: a.id, points: 10_000_000 });
  assert.equal(rankOf(game, a), 'kubbealti');
});

test('En yüksek hediye Padişah yapar; aynı anda yalnızca BİR Padişah', () => {
  const { game, fx, clock } = makeGame();
  const a = user('birinci'); const b = user('ikinci');
  game.handleEvent(gift(a, 'Lion', 29999));
  assert.equal(rankOf(game, a), 'padisah');
  assert.equal(game.state.throne.holderId, a.id);
  clock.t += 200_000; // koruma süresi geçsin
  game.handleEvent(gift(b, 'Universe', 34999));
  assert.equal(rankOf(game, b), 'padisah');
  assert.equal(rankOf(game, a), 'sehzade', 'eski Padişah Şehzade\'ye iner (config: demoteTo)');
  const padisahlar = Object.values(game.state.users).filter((u) => u.rank === 'padisah');
  assert.equal(padisahlar.length, 1);
  const crown = fx.filter((f) => f.type === 'coronation');
  assert.equal(crown.length, 2);
  assert.equal(crown[1].previous.id, a.id);
});

test('Taht koruma süresi: sürekli hediye ile taht kapılamaz, kimsenin kaydı silinmez', () => {
  const { game, fx } = makeGame();
  const king = user('kral'); const whale = user('balina');
  game.handleEvent(gift(king, 'Lion', 29999));
  game.handleEvent(say(user('koylu1'), '!katıl'));
  const before = Object.keys(game.state.users).length;
  for (let i = 0; i < 10; i++) game.handleEvent(gift(whale, 'Universe', 34999)); // süre dolmadan spam
  assert.equal(game.state.throne.holderId, king.id, 'koruma süresinde taht değişmez');
  assert.equal(rankOf(game, whale), 'sehzade', 'bunun yerine fallbackRank verilir');
  assert.equal(Object.keys(game.state.users).length, before + 1, 'hiç kullanıcı silinmedi (yalnızca balina eklendi)');
  assert.ok(fx.some((f) => f.kind === 'throne'));
});

test('Devralma bedeli: mevcut taç değerinden küçük hediye tahtı alamaz', () => {
  const { game, clock } = makeGame();
  const a = user('a'); const b = user('b');
  game.handleEvent(gift(a, 'Universe', 34999));
  clock.t += 200_000;
  game.handleEvent(gift(b, 'Lion', 29999)); // 29999 < 34999
  assert.equal(game.state.throne.holderId, a.id);
});

test('Padişah kendi kendini tekrar taçlandıramaz (taç animasyonu tekrarlanmaz)', () => {
  const { game, fx, clock } = makeGame();
  const a = user('tek');
  game.handleEvent(gift(a, 'Lion', 29999));
  clock.t += 500_000;
  game.handleEvent(gift(a, 'Lion', 29999));
  assert.equal(fx.filter((f) => f.type === 'coronation').length, 1);
  assert.equal(game.state.throne.holderId, a.id);
});

test('Demote hedefi config ile Sadrazam yapılabilir', () => {
  const { game, clock } = makeGame({ mutate: (sc) => { sc.game.throne.demoteTo = 'sadrazam'; } });
  const a = user('eski'); const b = user('yeni');
  game.handleEvent(gift(a, 'Lion', 29999));
  clock.t += 200_000;
  game.handleEvent(gift(b, 'Universe', 34999));
  assert.equal(rankOf(game, a), 'sadrazam');
});

test('Hediye combo: lib/events seri takibi farkı gönderir (çift sayım yok)', () => {
  const { createNormalizer } = require('../lib/events');
  const { normalize } = createNormalizer();
  const mk = (repeatCount, repeatEnd) => ({ userId: '7', uniqueId: 'kombo', nickname: 'Kombo', giftId: 5655, giftName: 'Rose', diamondCount: 1, giftType: 1, repeatCount, repeatEnd });
  const a = normalize('gift', mk(1, false));
  const b = normalize('gift', mk(3, false));
  const c = normalize('gift', mk(3, true));   // seri bitişi aynı sayıyı tekrar bildirir
  assert.equal(a.gift.count, 1);
  assert.equal(b.gift.count, 2);
  assert.equal(c, null, 'bitiş olayı fark 0 ise yok sayılmalı');
  const { game } = makeGame();
  const u = user('komboc');
  for (const ev of [a, b]) { ev.user.id = u.id; game.handleEvent(ev); }
  assert.equal(game.state.users[u.id].diamonds, 3, 'toplam 3 Rose = 3 elmas');
});

test('!isim: temizler, sınırlar, rütbe adlarını/boşu reddeder, bekleme süresi uygular', () => {
  const { game, clock } = makeGame();
  const a = user('isimci');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(say(a, '!isim <b>Kara</b> Mustafa Çok Uzun Bir İsim Bu'));
  const name = game.state.users[a.id].name;
  assert.ok(name.length <= 16 && !/[<>]/.test(name), `temizlenmiş isim: ${name}`);
  clock.t += 10_000;
  game.handleEvent(say(a, '!isim Yeni Ad'));
  assert.equal(game.state.users[a.id].name, name, '120 sn bekleme süresi içinde değişmez');
  clock.t += 200_000;
  game.handleEvent(say(a, '!isim Padişah'));
  assert.equal(game.state.users[a.id].name, name, 'rütbe adı yasak');
  clock.t += 10_000; // komut spam korumasını geçsin
  game.handleEvent(say(a, '!isim Ali'));
  assert.equal(game.state.users[a.id].name, 'Ali');
});

test('!meslek: puan eşiği ister; çiftçi/asker meslek rütbe ve ordu katkısı sağlar', () => {
  const { game, fx, clock } = makeGame();
  const a = user('meslek');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(say(a, '!meslek asker'));
  assert.equal(game.state.users[a.id].job, null, 'puan yetersiz');
  assert.ok(fx.some((f) => f.kind === 'error'));
  game.admin('addPoints', { user: a.id, points: 250 });
  clock.t += 60_000;
  game.handleEvent(say(a, '!meslek ASKER'));
  assert.equal(game.state.users[a.id].job, 'asker');
  assert.ok(game.armyStats().power >= 2, 'asker rütbesi (1) + meslek bonusu (1)');
  clock.t += 60_000;
  game.handleEvent(say(a, '!meslek çiftçi'));
  assert.equal(game.state.users[a.id].job, 'ciftci');
});

test('Ücretsiz katılım ekonomiyi büyütür: nüfus/üretim/şehir', () => {
  const { game, fx, clock } = makeGame();
  const start = game.snapshot().stats;
  for (let i = 0; i < 20; i++) {
    const u = user('f' + i);
    game.handleEvent(say(u, '!katıl'));
    clock.t += 20_000;
    for (let k = 0; k < 4; k++) { game.handleEvent(say(u, 'merhaba')); clock.t += 16_000; }
  }
  const end = game.snapshot().stats;
  assert.ok(end.population > start.population + 150, 'nüfus artmalı');
  assert.ok(end.production > start.production);
  assert.ok(end.cities >= 2, 'şehir kazanılmalı');
  assert.ok(fx.some((f) => f.type === 'city'));
  game.tick();
  assert.ok(game.snapshot().stats.treasury > end.treasury, 'tik pasif gelir üretmeli');
});

test('Sıradan yorum puanı bekleme süresiyle sınırlıdır (spam ile puan basılamaz)', () => {
  const { game } = makeGame();
  const a = user('spam');
  game.handleEvent(say(a, '!katıl'));
  const p0 = game.state.users[a.id].points;
  for (let i = 0; i < 50; i++) game.handleEvent(say(a, 'selam'));
  assert.equal(game.state.users[a.id].points, p0 + 1, '15 sn içinde tek yorum puanı');
});

test('Takip yalnızca bir kez puan verir', () => {
  const { game } = makeGame();
  const a = user('takipci');
  game.handleEvent({ type: 'follow', user: a });
  const p = game.state.users[a.id].points;
  game.handleEvent({ type: 'follow', user: a });
  assert.equal(game.state.users[a.id].points, p);
});

test('Kalıcı kayıt: sunucu yeniden başlayınca kullanıcılar, rütbeler, taht ve istatistik korunur', () => {
  const { game, dir } = makeGame();
  const a = user('kalici'); const k = user('kralk');
  game.handleEvent(say(a, '!katıl'));
  game.handleEvent(gift(a, 'Galaxy', 1000));
  game.handleEvent(say(a, '!isim Kalıcı Ali'));
  game.handleEvent(gift(k, 'Lion', 29999));
  game.flush();
  assert.ok(fs.existsSync(path.join(dir, 'osmanli.json')));
  const { sc } = makeGame();
  const g2 = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(g2.state.users[a.id].rank, 'beylerbeyi');
  assert.equal(g2.state.users[a.id].name, 'Kalıcı Ali');
  assert.equal(g2.state.throne.holderId, k.id);
  assert.equal(g2.snapshot().users[0].id, k.id, 'Padişah listenin başında');
  assert.equal(g2.snapshot().stats.treasury, game.snapshot().stats.treasury);
});

test('Bozuk kayıt dosyası silinmez, kenara alınır ve oyun temiz başlar', () => {
  const { dir, sc } = makeGame();
  fs.writeFileSync(path.join(dir, 'osmanli.json'), '{bozuk json');
  const g = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(Object.keys(g.state.users).length, 0);
  assert.ok(fs.readdirSync(dir).some((f) => f.includes('.bozuk-')));
});

test('Taht değişmezi: kayıtta birden fazla Padişah varsa yüklemede tekil hale getirilir', () => {
  const { game, dir, sc } = makeGame();
  const a = user('p1'); const b = user('p2');
  game.handleEvent(say(a, '!katıl')); game.handleEvent(say(b, '!katıl'));
  game.state.users[a.id].rank = 'padisah'; game.state.users[b.id].rank = 'padisah';
  game.state.throne.holderId = a.id;
  game.save(true);
  const g2 = new RankGame(sc, { dataDir: dir, emitFx: () => {} });
  assert.equal(Object.values(g2.state.users).filter((u) => u.rank === 'padisah').length, 1);
  assert.equal(g2.state.users[b.id].rank, 'sehzade');
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
  assert.equal(rankOf(game, a), 'sehzade');
  assert.equal(game.admin('wipe', { confirm: 'x' }).ok, false);
  assert.equal(Object.keys(game.state.users).length, 1);
  assert.equal(game.admin('wipe', { confirm: 'SIFIRLA' }).ok, true);
  assert.equal(Object.keys(game.state.users).length, 0);
});

test('Hatalı game yapılandırması anlaşılır hata verir', () => {
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.throne.rank = 'yok'; } }), /throne\.rank/);
  assert.throws(() => makeGame({ mutate: (sc) => { sc.game.giftRankGrants.push({ minDiamonds: 1, rank: 'yok' }); } }), /bilinmeyen rütbe/);
});

test('Snapshot: kullanıcılar rütbe sonra puana göre sıralı, istemciye yalnızca üst N gönderilir', () => {
  const { game } = makeGame({ mutate: (sc) => { sc.game.view.sendTop = 3; } });
  for (let i = 0; i < 6; i++) game.handleEvent(gift(user('s' + i), 'Rose', 1 + i));
  game.handleEvent(gift(user('buyuk'), 'Galaxy', 1000));
  const s = game.snapshot();
  assert.equal(s.users.length, 3);
  assert.equal(s.total, 7);
  assert.equal(s.users[0].rank, 'beylerbeyi');
  assert.ok(types(game.snapshot().users).length === 3);
});

test('Tek hediyede ara terfiler tek bir NİHAİ olay olarak duyurulur (gereksiz ara rütbe bildirimi yok)', () => {
  const { game, fx } = makeGame();
  const a = user('nihai');
  game.handleEvent(gift(a, 'Sports Car', 7000)); // puanla Kubbealtı'na, eşikle Sadrazam'a
  const ups = fx.filter((f) => f.type === 'rankup');
  assert.equal(ups.length, 1);
  assert.equal(ups[0].to, 'sadrazam');
  const b = user('taclanan');
  game.handleEvent(gift(b, 'Lion', 29999));
  assert.equal(fx.filter((f) => f.type === 'rankup' && f.user.id === b.id).length, 0, 'taç takan kişi için ayrıca rankup yok');
  assert.equal(fx.filter((f) => f.type === 'coronation').length, 1);
});

test('Değişiklik yoksa diske yazılmaz (senaryo değişimi gereksiz yazma yapmaz)', () => {
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

test('Sıralama config ile değiştirilebilir: Sadrazam, Padişah\'ın hemen altına alınabilir', () => {
  const { game, clock } = makeGame({
    mutate: (sc) => {
      const g = sc.game;
      const sz = g.ranks.findIndex((r) => r.id === 'sadrazam');
      const sh = g.ranks.findIndex((r) => r.id === 'sehzade');
      [g.ranks[sz], g.ranks[sh]] = [g.ranks[sh], g.ranks[sz]];                  // ... Kubbealtı, Şehzade, Sadrazam, Padişah
      for (const gr of g.giftRankGrants) {                                       // eşikleri de yer değiştir
        if (gr.rank === 'sadrazam') gr.rank = 'sehzade'; else if (gr.rank === 'sehzade') gr.rank = 'sadrazam';
      }
      g.throne.demoteTo = 'sadrazam'; g.throne.fallbackRank = 'sadrazam';
    },
  });
  assert.deepEqual(game.ranks.slice(-3).map((r) => r.id), ['sehzade', 'sadrazam', 'padisah']);
  const a = user('x1'); const b = user('x2'); const c = user('x3');
  game.handleEvent(gift(a, 'Car', 5000));
  assert.equal(rankOf(game, a), 'sehzade');
  game.handleEvent(gift(b, 'Castle', 15000));
  assert.equal(rankOf(game, b), 'sadrazam');
  game.handleEvent(gift(c, 'Lion', 29999));
  assert.equal(rankOf(game, c), 'padisah');
  clock.t += 200_000;
  game.handleEvent(gift(a, 'Universe', 34999));
  assert.equal(rankOf(game, c), 'sadrazam', 'eski Padişah artık Padişah\'ın bir altına (Sadrazam) iner');
});
