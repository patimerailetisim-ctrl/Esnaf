'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { makeGame, restart, user, gift, say, ev } = require('./helpers');

const S = 1000;
const rankOf = (g, u) => g.state.users[u.id].rank;
const pointsOf = (g, u) => g.state.users[u.id].points;
const ends = (fx) => fx.filter((f) => f.type === 'auction' && f.mode === 'end');

/** Zamanı ilerletip saniye sayacını (auctionTick) çalıştırır */
function setup(opts) {
  const ctx = makeGame(opts);
  const AU = ctx.sc.game.throne.auction;
  const advance = (ms) => { ctx.clock.t += ms; ctx.game.auctionTick(); };
  const startAuction = () => { advance(AU.intervalSec * S); assert.equal(ctx.game.state.auction.phase, 'active'); };
  return Object.assign(ctx, { AU, advance, startAuction });
}

// ------------------------------------------------------------------ zamanlama
test('Her 3 dakikada bir otomatik taht mücadelesi başlar ve 45 saniye sürer', () => {
  const { game, fx, advance, AU, clock } = setup();
  const t0 = clock.t;
  assert.equal(AU.intervalSec, 180);
  assert.equal(AU.durationSec, 45);
  assert.equal(game.state.auction.nextAt, t0 + 180 * S);
  advance(179 * S);
  assert.equal(game.state.auction.phase, 'idle', '3 dakikadan önce başlamaz');
  advance(1 * S);
  assert.equal(game.state.auction.phase, 'active');
  assert.equal(game.state.auction.endsAt - clock.t, 45 * S);
  const start = fx.find((f) => f.type === 'auction' && f.mode === 'start');
  assert.deepEqual([start.id, start.durationSec], [1, 45]);
  advance(44 * S);
  assert.equal(game.state.auction.phase, 'active', '45. saniyeden önce bitmez');
  advance(1 * S);
  assert.equal(game.state.auction.phase, 'idle');
  assert.equal(ends(fx).length, 1);
});

test('Açık artırma bitince yeni 3 dakikalık döngü BİTİŞTEN itibaren başlar ve tekrar eder', () => {
  const { game, advance, clock, fx } = setup();
  advance(180 * S); advance(45 * S);
  assert.equal(game.state.auction.nextAt, clock.t + 180 * S);
  advance(179 * S);
  assert.equal(game.state.auction.phase, 'idle');
  advance(1 * S);
  assert.equal(game.state.auction.phase, 'active');
  assert.equal(game.state.auction.id, 2);
  assert.equal(fx.filter((f) => f.type === 'auction' && f.mode === 'start').length, 2);
});

// ------------------------------------------------------------------ teklifler
test('Teklif puanı hediye puanıdır; her kullanıcının teklifi ayrı tutulur; normal puan da yazılmaya devam eder', () => {
  const { game, startAuction } = setup();
  const a = user('a'); const b = user('b');
  game.handleEvent(say(a, '!katıl')); game.handleEvent(say(b, '!katıl'));
  const a0 = pointsOf(game, a); const b0 = pointsOf(game, b);
  startAuction();
  game.handleEvent(gift(a, 'Hand Hearts', 100));
  game.handleEvent(gift(b, 'Doughnut', 30, 2));
  game.handleEvent(gift(a, 'Finger Heart', 5, 4));
  const bids = game.state.auction.bids;
  assert.equal(bids[a.id].points, 120);
  assert.equal(bids[b.id].points, 60);
  assert.equal(pointsOf(game, a) - a0, 120, 'normal puan teklifle AYNI anda yazıldı');
  assert.equal(pointsOf(game, b) - b0, 60);
  const snap = game.snapshot().auction;
  assert.deepEqual(snap.top.map((t) => [t.name, t.bid]), [['a', 120], ['b', 60]]);
  assert.equal(snap.bidders, 2);
  assert.equal(snap.totalBid, 180);
});

test('Açık artırma dışında hediye teklif sayılmaz; yorum/takip/beğeni hiç teklif sayılmaz', () => {
  const { game, startAuction, advance } = setup();
  const a = user('dis');
  game.handleEvent(gift(a, 'Hand Hearts', 100));
  assert.deepEqual(game.state.auction.bids, {});
  startAuction();
  game.handleEvent(say(a, 'merhaba'));
  game.handleEvent(ev('follow', a));
  game.handleEvent(ev('like', a, { count: 40 }));
  game.handleEvent(gift(a, 'Mystery', 0));
  assert.deepEqual(game.state.auction.bids, {}, 'yalnızca puanı olan hediye teklif olur');
  advance(0);
});

test('Teklif puanları açık artırma bitince sıfırlanır; normal kullanıcı puanları ASLA sıfırlanmaz', () => {
  const { game, startAuction, advance } = setup();
  const a = user('x'); const b = user('y');
  startAuction();
  game.handleEvent(gift(a, 'Hand Hearts', 100, 3));
  game.handleEvent(gift(b, 'Hand Hearts', 100));
  const pa = pointsOf(game, a); const pb = pointsOf(game, b);
  advance(45 * S);
  assert.deepEqual(game.state.auction.bids, {});
  assert.equal(game.state.auction.seq, 0);
  assert.equal(pointsOf(game, a), pa);
  assert.equal(pointsOf(game, b), pb);
  assert.equal(game.snapshot().auction.top.length, 0);
  advance(180 * S);
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(game.state.auction.bids[a.id].points, 1, 'yeni açık artırma sıfırdan başlar');
});

test('İlk 5 teklif sahibi sıralı gösterilir (7 teklif sahibinden 5\'i, büyükten küçüğe)', () => {
  const { game, startAuction } = setup();
  const names = ['u1', 'u2', 'u3', 'u4', 'u5', 'u6', 'u7'];
  startAuction();
  names.forEach((n, i) => game.handleEvent(gift(user(n), 'Rose', 1, (i + 1) * 10)));
  const top = game.snapshot().auction.top;
  assert.equal(top.length, 5);
  assert.deepEqual(top.map((t) => t.name), ['u7', 'u6', 'u5', 'u4', 'u3']);
  assert.deepEqual(top.map((t) => t.bid), [70, 60, 50, 40, 30]);
});

// ------------------------------------------------------------------ süre uzatma
test('Son 10 saniyede gelen teklif MEVCUT bitişe 5 saniye ekler; öncesinde uzatmaz', () => {
  const { game, startAuction, advance, clock, fx, AU } = setup();
  assert.deepEqual([AU.extendWindowSec, AU.extendBySec, AU.maxExtensionSec], [10, 5, 30]);
  const a = user('uzat');
  startAuction();
  const end0 = game.state.auction.endsAt;
  advance(33 * S); // kalan 12 sn
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(game.state.auction.endsAt, end0, 'kalan 12 sn: uzatma yok');
  advance(2 * S);  // kalan tam 10 sn
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(game.state.auction.endsAt, end0 + 5 * S, 'kalan 10 sn: mevcut bitişe +5 sn');
  assert.equal(game.state.auction.extensions, 1);
  assert.equal(game.state.auction.extendedSec, 5);
  advance(14 * S + 500); // yeni bitişe (end0+5s) 0,5 sn kala (şimdi end0+4.5-... hesap: kalan 0,5 sn)
  assert.equal(game.state.auction.endsAt - clock.t, 500);
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(game.state.auction.endsAt, end0 + 10 * S, 'zincirleme: mevcut bitişe tekrar +5 sn');
  assert.equal(game.state.auction.extendedSec, 10);
  assert.equal(fx.filter((f) => f.type === 'auction' && f.mode === 'extend').length, 2);
  advance(5 * S);
  assert.equal(game.state.auction.phase, 'active', 'orijinal bitişten 10 sn sonrasına kadar uzadı');
  advance(500);
  assert.equal(game.state.auction.phase, 'idle');
});

test('Toplam uzatma en fazla 30 saniye: 6. uzatmadan sonra teklif gelse de süre uzamaz', () => {
  const { game, startAuction, advance, clock, fx } = setup();
  const a = user('sinir');
  startAuction();
  const end0 = game.state.auction.endsAt;
  for (let i = 1; i <= 6; i++) {
    clock.t = game.state.auction.endsAt - 9 * S; // her teklif bitişe 9 sn kala gelir (son 10 sn içinde)
    game.auctionTick();
    game.handleEvent(gift(a, 'Rose', 1));
    assert.equal(game.state.auction.endsAt, end0 + i * 5 * S, `${i}. uzatma`);
  }
  clock.t = game.state.auction.endsAt - 9 * S;
  assert.equal(game.state.auction.extendedSec, 30);
  const endMax = game.state.auction.endsAt;
  assert.equal(endMax, end0 + 30 * S);
  game.handleEvent(gift(a, 'Rose', 1)); // tavan doldu
  assert.equal(game.state.auction.endsAt, endMax, 'üst sınırdan sonra uzatma yok');
  assert.equal(fx.filter((f) => f.type === 'auction' && f.mode === 'extend').length, 6);
  assert.equal(game.state.auction.bids[a.id].points, 7, 'teklif yine de sayıldı');
  clock.t = endMax; game.auctionTick();
  assert.equal(game.state.auction.phase, 'idle', 'en geç orijinal bitiş + 30 sn');
});

test('Kalan uzatma hakkı adımdan azsa yalnızca kalanı eklenir (config: adım 7, tavan 10 -> 7 + 3)', () => {
  const { game, startAuction, advance } = setup({ mutate: (sc) => { sc.game.throne.auction.extendBySec = 7; sc.game.throne.auction.maxExtensionSec = 10; } });
  const a = user('kalan');
  startAuction();
  const end0 = game.state.auction.endsAt;
  advance(40 * S);
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(game.state.auction.endsAt, end0 + 7 * S);
  advance(10 * S);
  game.handleEvent(gift(a, 'Rose', 1));
  assert.equal(game.state.auction.endsAt, end0 + 10 * S);
  assert.equal(game.state.auction.extendedSec, 10);
});

test('Süre bittikten SONRA gelen hediye teklif olmaz (saniye sayacı gecikse bile)', () => {
  const { game, startAuction, clock, fx } = setup();
  const a = user('gec');
  startAuction();
  game.handleEvent(gift(a, 'Rose', 1, 5));
  clock.t += 45 * S + 1; // sayaç henüz çalışmadı ama süre doldu
  const before = pointsOf(game, a);
  game.handleEvent(gift(a, 'Rose', 1, 100));
  assert.equal(ends(fx).length, 1, 'olay işlenmeden önce açık artırma kapatıldı');
  assert.deepEqual(game.state.auction.bids, {});
  assert.equal(pointsOf(game, a), before + 100, 'normal puan yine yazıldı');
  assert.equal(game.state.throne.holderId, a.id, 'kazanan 5 puanlık teklifle belirlendi, geç teklif katılmadı');
});

// ------------------------------------------------------------------ kazanan, eşitlik, Padişah/Sadrazam
test('En yüksek teklif sahibi Padişah olur; eski Padişah Sadrazam olur; puanlar korunur', () => {
  const { game, startAuction, advance, fx } = setup();
  const old = user('eski'); const a = user('a'); const b = user('b');
  game.admin('crown', { user: (game.handleEvent(say(old, '!katıl')), old.id) });
  assert.equal(game.state.throne.holderId, old.id);
  startAuction();
  game.handleEvent(gift(a, 'Hand Hearts', 100, 2));
  game.handleEvent(gift(b, 'Hand Hearts', 100, 5));
  const pa = pointsOf(game, a); const pb = pointsOf(game, b);
  advance(45 * S);
  assert.equal(game.state.throne.holderId, b.id);
  assert.equal(rankOf(game, b), 'padisah');
  assert.equal(rankOf(game, old), 'sadrazam', 'eski Padişah Sadrazam oldu');
  assert.equal(rankOf(game, a) === 'padisah', false);
  assert.equal(game.snapshot().throne.vizier.id, old.id, 'ekranda Sadrazam bilgisi');
  assert.equal(pointsOf(game, b), pb, 'Padişah olmak puanı değiştirmez');
  assert.equal(pointsOf(game, a), pa);
  assert.equal(Object.values(game.state.users).filter((u) => u.rank === 'padisah').length, 1);
  const end = ends(fx)[0];
  assert.deepEqual([end.winner.id, end.bid, end.previous.id, end.demotedTo, end.retained, end.bidders], [b.id, 500, old.id, 'sadrazam', false, 2]);
  assert.deepEqual(end.top.map((t) => [t.name, t.bid]), [['b', 500], ['a', 200]]);
});

test('Beraberlikte SON geçerli teklifi veren kazanır (A önce / B sonra)', () => {
  const { game, startAuction, advance } = setup();
  const a = user('ilk'); const b = user('son');
  startAuction();
  game.handleEvent(gift(a, 'Hand Hearts', 100));
  game.handleEvent(gift(b, 'Hand Hearts', 100));
  advance(45 * S);
  assert.equal(game.state.throne.holderId, b.id);
});

test('Beraberlikte son teklif A\'daysa A kazanır (A 50, B 100, A +50)', () => {
  const { game, startAuction, advance } = setup();
  const a = user('a2'); const b = user('b2');
  startAuction();
  game.handleEvent(gift(a, 'Rose', 1, 50));
  game.handleEvent(gift(b, 'Rose', 1, 100));
  game.handleEvent(gift(a, 'Rose', 1, 50));
  advance(45 * S);
  assert.equal(game.state.throne.holderId, a.id);
});

test('Beraberlik kesin: aynı milisaniyede gelen tekliflerde de olay sırası belirler; geride kalan kazanamaz', () => {
  const { game, startAuction, advance } = setup();
  const a = user('s1'); const b = user('s2'); const c = user('s3');
  startAuction();
  game.handleEvent(gift(a, 'Rose', 1, 80));
  game.handleEvent(gift(b, 'Rose', 1, 80));
  game.handleEvent(gift(c, 'Rose', 1, 79));
  advance(45 * S);
  assert.equal(game.state.throne.holderId, b.id);
});

test('Mevcut Padişah en yüksek teklifi verirse tahtını korur (kimse düşürülmez)', () => {
  const { game, startAuction, advance, fx } = setup();
  const k = user('kral'); const a = user('rakip');
  game.handleEvent(say(k, '!katıl')); game.handleEvent(say(a, '!katıl'));
  game.admin('crown', { user: k.id });
  startAuction();
  game.handleEvent(gift(a, 'Rose', 1, 10));
  game.handleEvent(gift(k, 'Rose', 1, 20));
  advance(45 * S);
  assert.equal(game.state.throne.holderId, k.id);
  assert.equal(rankOf(game, a), 'koylu');
  const end = ends(fx)[0];
  assert.equal(end.retained, true);
  assert.equal(end.previous, null);
});

test('Teklif gelmezse taht değişmez, yine de yeni döngü başlar', () => {
  const { game, startAuction, advance, fx, clock } = setup();
  const k = user('mevcut');
  game.handleEvent(say(k, '!katıl'));
  game.admin('crown', { user: k.id });
  startAuction();
  advance(45 * S);
  const end = ends(fx)[0];
  assert.equal(end.winner, null);
  assert.equal(game.state.throne.holderId, k.id);
  assert.equal(game.state.auction.nextAt, clock.t + 180 * S);
  assert.equal(game.state.auction.last.winnerId, null);
});

test('Padişahlık normal puandan bağımsız: açık artırma dışında dev hediye taç vermez; Sadrazam üst sınırdır', () => {
  const { game } = setup();
  const a = user('whale');
  game.handleEvent(gift(a, 'Universe', 34999, 10));
  assert.equal(rankOf(game, a), 'sadrazam');
  assert.equal(game.state.throne.holderId, null);
});

test('Zincir: ikinci mücadelede yeni kazanan Padişah olur, önceki Padişah Sadrazam olur, ilk Sadrazam yerinde kalır', () => {
  const { game, startAuction, advance } = setup();
  const a = user('k1'); const b = user('k2'); const c = user('k3');
  startAuction(); game.handleEvent(gift(a, 'Rose', 1, 10)); advance(45 * S);
  startAuction(); game.handleEvent(gift(b, 'Rose', 1, 10)); advance(45 * S);
  startAuction(); game.handleEvent(gift(c, 'Rose', 1, 10)); advance(45 * S);
  assert.equal(game.state.throne.holderId, c.id);
  assert.equal(rankOf(game, b), 'sadrazam');
  assert.equal(rankOf(game, a), 'sadrazam', 'önceki Sadrazam rütbesini korur');
  assert.equal(game.snapshot().throne.vizier.id, b.id, 'son inen Padişah Sadrazam olarak öne çıkar');
  assert.equal(game.state.throne.history.length, 3);
});

// ------------------------------------------------------------------ yönetici ve idempotency
test('Yönetici: şimdi başlat / şimdi bitir; çifte başlatma ve boşta bitirme reddedilir; kapalıysa başlamaz', () => {
  const { game } = setup();
  assert.equal(game.admin('auctionEnd').ok, false);
  assert.equal(game.admin('auctionStart').ok, true);
  assert.equal(game.admin('auctionStart').ok, false);
  const a = user('adm');
  game.handleEvent(gift(a, 'Rose', 1, 3));
  assert.equal(game.admin('auctionEnd').ok, true);
  assert.equal(game.state.throne.holderId, a.id);
  const off = setup({ mutate: (sc) => { sc.game.throne.auction.enabled = false; } });
  off.advance(2000 * S);
  assert.equal(off.game.state.auction.phase, 'idle');
  assert.equal(off.game.admin('auctionStart').ok, false);
});

test('Aynı hediye olayı tekrar gelirse teklife iki kez eklenmez', () => {
  const { game, startAuction } = setup();
  const a = user('dup');
  startAuction();
  const e = gift(a, 'Rose', 1, 50, { id: 'same' });
  game.handleEvent(e); game.handleEvent(JSON.parse(JSON.stringify(e)));
  assert.equal(game.state.auction.bids[a.id].points, 50);
});

test('Açık artırma sırasında silinen oyuncunun teklifi düşer', () => {
  const { game, startAuction, advance } = setup();
  const a = user('sil'); const b = user('kal');
  startAuction();
  game.handleEvent(gift(a, 'Rose', 1, 90));
  game.handleEvent(gift(b, 'Rose', 1, 10));
  game.admin('removeUser', { user: a.id });
  advance(45 * S);
  assert.equal(game.state.throne.holderId, b.id);
});

// ------------------------------------------------------------------ kalıcılık ve yeniden başlatma
test('Yeniden başlatma: açık artırma kalan süreyle, kayıtlı tekliflerle devam eder ve orijinal bitişte sonuçlanır', () => {
  const ctx = setup();
  const a = user('r1'); const b = user('r2');
  ctx.startAuction();
  ctx.game.handleEvent(gift(a, 'Rose', 1, 30));
  ctx.advance(20 * S);
  ctx.game.handleEvent(gift(b, 'Rose', 1, 45));
  const endsAt = ctx.game.state.auction.endsAt;
  ctx.game.flush();

  ctx.clock.t += 5 * S; // sunucu 5 sn kapalı kaldı
  const r = restart(ctx);
  r.game.start();
  const a2 = r.game.state.auction;
  assert.equal(a2.phase, 'active');
  assert.equal(a2.endsAt, endsAt, 'bitiş saati değişmedi (mutlak saat)');
  assert.equal(a2.bids[a.id].points, 30);
  assert.equal(a2.bids[b.id].points, 45);
  assert.equal(r.game.snapshot().auction.top[0].name, 'r2');
  assert.equal(r.game.snapshot().auction.endsAt - r.game.snapshot().auction.serverNow, endsAt - ctx.clock.t, 'sayfa yenilenince kalan süre doğru');

  r.game.handleEvent(gift(a, 'Rose', 1, 30)); // a: 60 > b: 45
  ctx.clock.t = endsAt; r.game.auctionTick();
  assert.equal(r.game.state.throne.holderId, a.id);
  r.game.stop();
});

test('Yeniden başlatma: süre uzatması da kalıcıdır', () => {
  const ctx = setup();
  const a = user('uz');
  ctx.startAuction();
  ctx.advance(40 * S);
  ctx.game.handleEvent(gift(a, 'Rose', 1));
  const endsAt = ctx.game.state.auction.endsAt;
  assert.equal(ctx.game.state.auction.extensions, 1);
  ctx.game.flush();
  const r = restart(ctx);
  assert.equal(r.game.state.auction.endsAt, endsAt);
  assert.equal(r.game.state.auction.extensions, 1);
  assert.equal(r.game.state.auction.extendedSec, 5, 'toplam uzatma sayacı da kalıcı: tavan yeniden başlatmayla sıfırlanmaz');
});

test('Yeniden başlatma: açık artırma süresi dolmuşsa kontrollü kapatılır (kayıtlı tekliflerle), çift kapanmaz', () => {
  const ctx = setup();
  const a = user('kapan'); const b = user('kapan2');
  ctx.startAuction();
  ctx.game.handleEvent(gift(a, 'Rose', 1, 10));
  ctx.game.handleEvent(gift(b, 'Rose', 1, 70));
  ctx.game.flush();
  ctx.clock.t += 5 * 45 * S; // uzun kesinti
  const r = restart(ctx);
  assert.equal(r.game.state.auction.phase, 'active', 'yüklemede henüz kapatılmadı (kayıt olduğu gibi)');
  r.game.start();
  assert.equal(r.game.state.auction.phase, 'idle');
  assert.equal(r.game.state.throne.holderId, b.id, 'kayıtlı tekliflerden kazanan belirlendi');
  assert.equal(ends(r.fx).length, 1);
  assert.deepEqual(r.game.state.auction.bids, {});
  assert.equal(r.game.state.auction.nextAt, ctx.clock.t + 180 * S);
  r.game.auctionTick(); r.game.auctionTick();
  assert.equal(ends(r.fx).length, 1);
  r.game.stop();
});

test('Yeniden başlatma: zamanı geçmiş bekleyen mücadele ani başlamaz, restartDelaySec kadar ertelenir', () => {
  const ctx = setup();
  ctx.game.save(true);
  ctx.clock.t += 3180 * S; // 1 saat kapalı
  const r = restart(ctx);
  r.game.start();
  assert.equal(r.game.state.auction.phase, 'idle');
  assert.equal(r.game.state.auction.nextAt, ctx.clock.t + 30 * S);
  ctx.clock.t += 29 * S; r.game.auctionTick();
  assert.equal(r.game.state.auction.phase, 'idle');
  ctx.clock.t += 1 * S; r.game.auctionTick();
  assert.equal(r.game.state.auction.phase, 'active');
  r.game.stop();
});

test('Kalıcılık: kazanan Padişah ve Sadrazam yeniden başlatmadan sonra aynı kalır; vizier korunur', () => {
  const ctx = setup();
  const a = user('p1'); const b = user('p2');
  ctx.startAuction(); ctx.game.handleEvent(gift(a, 'Rose', 1, 5)); ctx.advance(45 * S);
  ctx.startAuction(); ctx.game.handleEvent(gift(b, 'Rose', 1, 9)); ctx.advance(45 * S);
  ctx.game.flush();
  const r = restart(ctx);
  assert.equal(r.game.state.throne.holderId, b.id);
  assert.equal(r.game.snapshot().throne.vizier.id, a.id);
  assert.equal(r.game.state.auction.last.winnerId, b.id);
  assert.equal(r.game.state.auction.nextAt, ctx.game.state.auction.nextAt);
});

test('Sıfırlama (wipe) açık artırma döngüsünü de baştan kurar', () => {
  const { game, startAuction, clock } = setup();
  startAuction();
  game.handleEvent(gift(user('w'), 'Rose', 1, 5));
  assert.equal(game.admin('wipe', { confirm: 'SIFIRLA' }).ok, true);
  assert.equal(game.state.auction.phase, 'idle');
  assert.deepEqual(game.state.auction.bids, {});
  assert.equal(game.state.auction.nextAt, clock.t + 180 * S);
});

test('Açık artırma sırasında gelen hediye normal rütbe yükselmesini de tetikler', () => {
  const { game, startAuction, fx } = setup();
  const a = user('terfi');
  startAuction();
  game.handleEvent(gift(a, 'Hand Hearts', 100, 3)); // 300 puan -> Esnaf
  assert.equal(rankOf(game, a), 'esnaf');
  const up = fx.filter((f) => f.type === 'rankup').pop();
  assert.equal(up.to, 'esnaf');
  const g = fx.filter((f) => f.type === 'gift').pop();
  assert.deepEqual(g.bid, { total: 300, position: 1, extended: false });
});
