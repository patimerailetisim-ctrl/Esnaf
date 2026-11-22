'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createNormalizer, extractGiftCatalog } = require('../lib/events');

const giftMsg = (over = {}) => Object.assign({
  common: { msgId: 'M1', createTime: '1700000000' },
  user: { idStr: '42', displayId: 'ayse', nickname: 'Ayşe' },
  giftId: 5655, groupId: 'G1', repeatCount: 1, repeatEnd: 0,
  giftDetails: { id: '5655', name: 'Rose', type: 1, diamondCount: 1 },
}, over);

test('Hediye değeri yalnızca giftDetails.diamondCount metadata alanından gelir ve kaynağı işaretlenir', () => {
  const { normalize } = createNormalizer();
  const e = normalize('gift', giftMsg({ giftDetails: { id: '1', name: 'Lion', type: 0, diamondCount: 29999 }, repeatCount: 2 }));
  assert.equal(e.gift.valueEach, 29999);
  assert.equal(e.gift.value, 59998);
  assert.equal(e.gift.valueField, 'diamondCount');
  assert.equal(e.gift.valueSource, 'message');
  assert.equal(e.gift.name, 'Lion');
  assert.equal(e.gift.count, 2);
});

test('Metadata eksikse oda kataloğu yedeği kullanılır; ikisi de yoksa değer 0 (güvenilmez)', () => {
  const n = createNormalizer();
  const raw = giftMsg({ giftDetails: { id: '77', name: 'Yeni', type: 0 }, giftId: 77 });
  const none = n.normalize('gift', raw);
  assert.equal(none.gift.valueEach, 0);
  assert.equal(none.gift.valueSource, 'missing');
  assert.equal(none.gift.diamondEach, 1, 'eski senaryolar için takma ad davranışı korunur');
  n.setCatalog(extractGiftCatalog({ data: { gifts: [{ id: 77, name: 'Yeni', diamond_count: 250 }, { id: 5, diamond_count: 'x' }] } }));
  const fromCatalog = n.normalize('gift', giftMsg({ common: { msgId: 'M2' }, giftDetails: { id: '77', name: 'Yeni', type: 0 }, giftId: 77 }));
  assert.equal(fromCatalog.gift.valueEach, 250);
  assert.equal(fromCatalog.gift.valueSource, 'catalog');
});

test('Kataloğa göre değil, mesajdaki değer önceliklidir', () => {
  const n = createNormalizer();
  n.setCatalog(new Map([['5655', 999]]));
  const e = n.normalize('gift', giftMsg());
  assert.equal(e.gift.valueEach, 1);
  assert.equal(e.gift.valueSource, 'message');
});

test('Combo (type=1): yalnızca fark yazılır; bitiş olayı çift sayılmaz; yeni seri (groupId) sıfırdan başlar', () => {
  const { normalize } = createNormalizer();
  const send = (rc, end, id, group = 'G1') => normalize('gift', giftMsg({ common: { msgId: id }, repeatCount: rc, repeatEnd: end, groupId: group }));
  assert.equal(send(1, 0, 'a').gift.count, 1);
  assert.equal(send(4, 0, 'b').gift.count, 3);
  assert.equal(send(4, 0, 'b2'), null, 'aynı güncelleme tekrar gelirse (fark 0) yok sayılır');
  assert.equal(send(4, 1, 'c'), null, 'bitiş olayı fark 0');
  assert.equal(send(2, 0, 'd', 'G2').gift.count, 2, 'yeni seri kimliği: sıfırdan');
  // seri bitişi kaçırılsa bile geriye giden repeatCount yeni seri sayılır
  assert.equal(send(6, 0, 'e', 'G3').gift.count, 6);
  assert.equal(send(1, 0, 'f', 'G3').gift.count, 1);
});

test('Combo olmayan hediyelerde repeatCount adedi verir', () => {
  const { normalize } = createNormalizer();
  const e = normalize('gift', giftMsg({ giftDetails: { id: '9', name: 'Heart', type: 0, diamondCount: 5 }, repeatCount: 3 }));
  assert.equal(e.gift.count, 3);
  assert.equal(e.gift.value, 15);
});

test('Olay kimliği: sunucu msgId varsa o; yoksa türetilir; hiçbiri yoksa null', () => {
  const { normalize } = createNormalizer();
  const { normalize: normalize2 } = createNormalizer(); // msgId'siz örnek için ayrı seri hafızası
  assert.equal(normalize('gift', giftMsg()).id, 'gift:M1');
  assert.equal(normalize('chat', { common: { msgId: 'C9' }, user: { idStr: '1' }, comment: 'selam' }).id, 'chat:C9');
  const noMsg = normalize2('gift', giftMsg({ common: undefined }));
  assert.match(noMsg.id, /^gift:42:5655:G1:1:0$/);
  assert.equal(normalize('follow', { user: { idStr: '3' } }).id, null);
  assert.match(normalize('chat', { createTime: 1700, user: { idStr: '1' }, comment: 'a' }).id, /^chat:1:1700:a$/);
});

test('Katalog çıkarıcı iç içe yapılarda id + diamond_count ikililerini bulur', () => {
  const m = extractGiftCatalog({ a: [{ gifts: [{ id: 1, diamond_count: 10 }, { gift_id: '2', diamondCount: 20 }, { id: 3 }] }], b: null });
  assert.equal(m.get('1'), 10);
  assert.equal(m.get('2'), 20);
  assert.equal(m.has('3'), false);
  assert.equal(extractGiftCatalog(null).size, 0);
});

test('Halay/Uzay için eski gift alanları (diamonds/diamondEach) değişmeden gelir', () => {
  const { normalize } = createNormalizer();
  const e = normalize('gift', { userId: '7', uniqueId: 'x', nickname: 'X', giftId: 1, giftName: 'Rose', diamondCount: 1, giftType: 0, repeatCount: 3 });
  assert.equal(e.type, 'gift');
  assert.equal(e.gift.diamonds, 3);
  assert.equal(e.gift.diamondEach, 1);
  assert.equal(e.gift.name, 'Rose');
});
