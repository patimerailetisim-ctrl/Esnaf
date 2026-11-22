'use strict';
/**
 * TikTok olaylarını tek tip formata çevirir (hem v2 iç içe hem v1 düz alan yapısını destekler).
 * Ayrı dosyada olması testlerde (combo çift sayım, hediye değeri, olay kimliği) doğrudan denenebilmesi içindir.
 *
 * Normalize edilmiş olay biçimleri:
 *   { type:'gift',    id, user, gift }
 *   { type:'like',    id, user, count, total }
 *   { type:'comment', id, user, text }
 *   { type:'follow' | 'share' | 'join', id, user }
 *   user = { id, uniqueId, nickname, avatar, isModerator, isSubscriber }
 *
 * HEDİYE DEĞERİ (coin / diamond karışmasın diye):
 *   Connector'ın hediye mesajında tek bir değer alanı vardır: giftDetails.diamondCount (hediyenin katalog değeri).
 *   Başka bir "coin" alanı yoktur ve hiçbir çarpanla coin<->diamond DÖNÜŞÜMÜ yapılmaz.
 *   gift.valueEach  : bu alandan gelen BİRİM değer (güvenilir değilse 0)
 *   gift.value      : valueEach x count
 *   gift.valueField : değerin hangi metadata alanından geldiği ('diamondCount')
 *   gift.valueSource: 'message' (olayın kendi metadata'sı) | 'catalog' (oda hediye kataloğu) | 'missing'
 *   gift.diamondEach / gift.diamonds: ESKİ senaryolar (Halay/Uzay) için aynı değerin takma adı; değer yoksa 1 sayılır (eski davranış).
 */
const pick = (...vals) => vals.find((v) => v !== undefined && v !== null && v !== '');

const VALUE_FIELD = 'diamondCount';

function normUser(raw = {}) {
  const u = raw.user || {};
  const urls = pick(u.avatarLarge?.urlList, u.avatarMedium?.urlList, u.avatarThumb?.urlList, u.profilePicture?.urls, []) || [];
  const bestUrl = urls.find((x) => /100x100/.test(x) && /\.(jpeg|jpg|webp)/.test(x)) || urls.find((x) => !/shrink/.test(x)) || urls[0];
  const uniqueId = String(pick(raw.uniqueId, u.displayId, u.uniqueId, u.nickname, 'misafir'));
  const avatar = pick(raw.profilePictureUrl, bestUrl);
  return {
    id: String(pick(raw.userId, u.idStr, u.userId, uniqueId)),
    uniqueId,
    nickname: String(pick(raw.nickname, u.nickname, uniqueId)),
    avatar: avatar
      ? `/avatar/proxy?url=${encodeURIComponent(avatar)}`
      : `/avatar/fake/${encodeURIComponent(uniqueId)}.svg`,
    isModerator: !!raw.isModerator,
    isSubscriber: !!raw.isSubscriber,
  };
}

/**
 * Oda hediye kataloğundan (fetchAvailableGifts yanıtı; yapısı sürümden sürüme değişebilir) giftId -> diamondCount tablosu çıkarır.
 * Yalnızca hem bir id hem de sayısal bir diamond_count/diamondCount taşıyan kayıtları alır.
 */
function extractGiftCatalog(info) {
  const map = new Map();
  const walk = (o, depth) => {
    if (!o || depth > 5) return;
    if (Array.isArray(o)) { o.forEach((x) => walk(x, depth + 1)); return; }
    if (typeof o !== 'object') return;
    const id = pick(o.id, o.gift_id, o.giftId);
    const dc = pick(o.diamond_count, o.diamondCount);
    if (id !== undefined && typeof dc === 'number' && dc >= 0) map.set(String(id), dc);
    for (const v of Object.values(o)) walk(v, depth + 1);
  };
  walk(info, 0);
  return map;
}

const STREAK_TTL_MS = 2 * 60 * 1000;

/** Her çağrıda kendi seri hafızası olan bir normalleştirici üretir. */
function createNormalizer({ now = Date.now } = {}) {
  // Hediye serisi (streak/combo) takibi: seri sürerken her güncellemede sadece FARKI gönderir,
  // böylece aynı serideki tekrarlar iki kez sayılmaz.
  const streaks = new Map(); // "userId:giftId:groupId" -> { last, at }
  let catalog = new Map();

  function sweep() {
    const t = now();
    for (const [k, v] of streaks) if (t - v.at > STREAK_TTL_MS) streaks.delete(k);
  }

  /** Olay kimliği (idempotency). Sunucu mesaj kimliği varsa o; yoksa olayı tekilleştiren alanlardan türetilir; hiçbiri yoksa null. */
  function eventId(type, raw, user) {
    const msg = pick(raw.common?.msgId, raw.msgId);
    if (msg !== undefined) return `${type}:${msg}`;
    const created = pick(raw.common?.createTime, raw.createTime, raw.timestamp);
    switch (type) {
      case 'gift': return `gift:${user.id}:${pick(raw.giftId, raw.giftDetails?.id, '')}:${pick(raw.groupId, '')}:${pick(raw.repeatCount, 1)}:${raw.repeatEnd ? 1 : 0}`;
      case 'chat': return created !== undefined ? `chat:${user.id}:${created}:${pick(raw.comment, raw.content, '')}` : null;
      case 'like': return created !== undefined ? `like:${user.id}:${created}:${pick(raw.totalLikeCount, '')}` : null;
      default: return created !== undefined ? `${type}:${user.id}:${created}` : null;
    }
  }

  function normGift(raw, user) {
    const d = raw.giftDetails || {};
    const giftType = pick(d.giftType, d.type, raw.giftType, 0);
    const repeatCount = Number(pick(raw.repeatCount, 1));
    const repeatEnd = !!raw.repeatEnd;
    const giftId = String(pick(raw.giftId, d.id, d.giftName, raw.giftName));
    const groupId = pick(raw.groupId, '');
    let count = 1;
    if (Number(giftType) === 1) {
      sweep();
      const key = `${user.id}:${giftId}:${groupId}`;
      const prev = streaks.get(key);
      // repeatCount geriye giderse (veya seri kimliği değiştiyse) yeni bir seri başlamıştır
      const last = prev && repeatCount >= prev.last ? prev.last : 0;
      count = Math.max(0, repeatCount - last);
      if (repeatEnd) streaks.delete(key);
      else streaks.set(key, { last: repeatCount, at: now() });
    } else {
      count = Math.max(1, repeatCount);
    }
    if (count <= 0) return null;

    // Değer: yalnızca connector metadata'sı (giftDetails.diamondCount). Eksikse oda kataloğu. Yoksa 'missing' (0).
    const msgVal = pick(d.diamondCount, raw.diamondCount);
    let valueEach = 0; let valueSource = 'missing';
    if (typeof msgVal === 'number' && Number.isFinite(msgVal) && msgVal > 0) { valueEach = msgVal; valueSource = 'message'; } else if (catalog.has(giftId) && catalog.get(giftId) > 0) { valueEach = catalog.get(giftId); valueSource = 'catalog'; }

    const legacyEach = Number(pick(d.diamondCount, raw.diamondCount, 1)); // Halay/Uzay'ın eski davranışı
    return {
      type: 'gift',
      user,
      gift: {
        id: giftId,
        name: String(pick(d.giftName, d.name, raw.giftName, 'Hediye')),
        count,
        valueEach, value: valueEach * count, valueField: VALUE_FIELD, valueSource,
        diamondEach: legacyEach, diamonds: legacyEach * count,
        type: Number(giftType) || 0,
        image: pick(d.giftImage?.url?.[0], d.image?.urlList?.[0], d.icon?.urlList?.[0], raw.giftPictureUrl, ''),
      },
    };
  }

  function normalize(type, raw) {
    const user = normUser(raw);
    let ev;
    switch (type) {
      case 'gift': ev = normGift(raw, user); break;
      case 'like':
        ev = { type: 'like', user, count: Number(pick(raw.likeCount, 1)), total: Number(pick(raw.totalLikeCount, 0)) };
        break;
      case 'chat':
        ev = { type: 'comment', user, text: String(pick(raw.comment, raw.content, '')) };
        break;
      case 'follow': ev = { type: 'follow', user }; break;
      case 'share': ev = { type: 'share', user }; break;
      case 'member': ev = { type: 'join', user }; break;
      default: return null;
    }
    if (ev) ev.id = eventId(type, raw, user);
    return ev;
  }

  return {
    normalize,
    normUser,
    /** Oda hediye kataloğunu (giftId -> diamondCount) ayarlar; olay metadata'sı eksikse yedek olarak kullanılır */
    setCatalog(map) { catalog = map instanceof Map ? map : new Map(); },
    get catalogSize() { return catalog.size; },
  };
}

module.exports = { createNormalizer, normUser, pick, extractGiftCatalog, VALUE_FIELD };
