'use strict';
/**
 * TikTok olaylarını tek tip formata çevirir (hem v2 iç içe hem v1 düz alan yapısını destekler).
 * Ayrı dosyada olması testlerde (combo çift sayım vb.) doğrudan denenebilmesi içindir.
 *
 * Normalize edilmiş olay biçimleri:
 *   { type:'gift',    user, gift:{ id, name, diamonds, diamondEach, count, image } }
 *   { type:'like',    user, count, total }
 *   { type:'comment', user, text }
 *   { type:'follow' | 'share' | 'join', user }
 *   user = { id, uniqueId, nickname, avatar, isModerator, isSubscriber }
 */
const pick = (...vals) => vals.find((v) => v !== undefined && v !== null && v !== '');

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

const STREAK_TTL_MS = 2 * 60 * 1000;

/** Her çağrıda kendi seri hafızası olan bir normalleştirici üretir. */
function createNormalizer({ now = Date.now } = {}) {
  // Hediye serisi (streak/combo) takibi: seri sürerken her güncellemede sadece FARKI gönderir,
  // böylece aynı serideki tekrarlar iki kez sayılmaz.
  const streaks = new Map(); // "userId:giftId" -> { last, at }

  function sweep() {
    const t = now();
    for (const [k, v] of streaks) if (t - v.at > STREAK_TTL_MS) streaks.delete(k);
  }

  function normGift(raw) {
    const d = raw.giftDetails || {};
    const giftType = pick(d.giftType, raw.giftType, 0);
    const repeatCount = Number(pick(raw.repeatCount, 1));
    const repeatEnd = !!raw.repeatEnd;
    const user = normUser(raw);
    const giftId = String(pick(raw.giftId, d.id, d.giftName, raw.giftName));
    let count = 1;
    if (giftType === 1) {
      sweep();
      const key = `${user.id}:${giftId}`;
      const last = (streaks.get(key) || { last: 0 }).last;
      count = Math.max(0, repeatCount - last);
      if (repeatEnd) streaks.delete(key);
      else streaks.set(key, { last: repeatCount, at: now() });
    } else {
      count = Math.max(1, repeatCount);
    }
    if (count <= 0) return null;
    const diamondEach = Number(pick(d.diamondCount, raw.diamondCount, 1));
    return {
      type: 'gift',
      user,
      gift: {
        id: giftId,
        name: String(pick(d.giftName, raw.giftName, 'Hediye')),
        diamonds: diamondEach * count,
        diamondEach,
        count,
        image: pick(d.giftImage?.url?.[0], d.icon?.urlList?.[0], raw.giftPictureUrl, ''),
      },
    };
  }

  function normalize(type, raw) {
    switch (type) {
      case 'gift': return normGift(raw);
      case 'like':
        return { type: 'like', user: normUser(raw), count: Number(pick(raw.likeCount, 1)), total: Number(pick(raw.totalLikeCount, 0)) };
      case 'chat':
        return { type: 'comment', user: normUser(raw), text: String(pick(raw.comment, raw.content, '')) };
      case 'follow': return { type: 'follow', user: normUser(raw) };
      case 'share': return { type: 'share', user: normUser(raw) };
      case 'member': return { type: 'join', user: normUser(raw) };
      default: return null;
    }
  }

  return { normalize, normUser };
}

module.exports = { createNormalizer, normUser, pick };
