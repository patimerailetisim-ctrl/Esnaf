'use strict';
/**
 * SENARYO: OSMANLI - TAHT MÜCADELESİ
 * ==================================
 * Halay ve Uzay'dan TAMAMEN bağımsızdır. Bu senaryo, diğerlerinden farklı olarak bir `game`
 * bölümü içerir: oyun mantığı (kullanıcılar, puan, rütbe, taht, ekonomi, kalıcı kayıt) sunucuda
 * `games/rankgame.js` tarafından yönetilir; görsel taraf `public/js/rankgame-view.js` çizer.
 * Oyun kurallarını değiştirmek için SADECE bu dosyayı düzenle.
 *
 * NOT: Rütbeler OYUNLAŞTIRMA amaçlıdır. Tarihsel makamların birebir hiyerarşik sıralaması
 * olduklarını iddia etmez; sıralama tamamen bu dosyadaki `ranks` listesinin sırasıdır.
 *
 * Verilerin kaydı: data/osmanli.json  (oyuncular, puanlar, taht, açık artırma durumu ve işlenmiş olay kimlikleri;
 * sunucu yeniden başlayınca korunur)
 *
 * KURALLAR ÖZETİ
 *  - Hediye değeri: connector'ın hediye metadata alanı `giftDetails.diamondCount`. Puan = değer x points.perGiftValue.
 *  - Rütbeler yalnızca PUANLA yükselir. Padişahlık puandan bağımsızdır; yalnızca taht açık artırmasıyla kazanılır.
 *  - Taht açık artırması: her 3 dk'da bir, 45 sn; hediye puanı teklife de eklenir; son 10 sn teklif +5 sn uzatır (toplam en çok +30 sn);
 *    en yüksek teklif Padişah olur (eşitlikte son teklif); eski Padişah Sadrazam olur; teklifler sıfırlanır (puanlar ASLA).
 */
module.exports = {
  id: 'osmanli',
  name: 'OSMANLI: TAHT MÜCADELESİ',
  description: 'Yorumla !katıl yaz köylü ol; hediyelerle rütbe atla; en büyük hediye tahtı kazandırır. Tek Padişah, kalıcı kayıt.',

  canvas: { width: 1080, height: 1920 },
  feed: false, // yorum akışı kapalı: oyun kendi bildirimlerini gösterir

  // ------------------------------------------------------------ ARKA PLAN (minyatür tadında akşam gökyüzü + avlu)
  background: {
    type: 'css',
    css: [
      'radial-gradient(ellipse 70% 38% at 50% 27%, rgba(255,226,150,.65), rgba(255,226,150,0) 70%)',
      'repeating-linear-gradient(45deg, rgba(110,64,24,.07) 0 16px, rgba(110,64,24,0) 16px 32px)',
      'repeating-linear-gradient(-45deg, rgba(110,64,24,.07) 0 16px, rgba(110,64,24,0) 16px 32px)',
      'linear-gradient(180deg, #17294a 0%, #2c4673 10%, #c9763f 20%, #f0c987 31%, #ecd6a2 46%, #d9bb82 66%, #bd9a62 100%)',
    ].join(', '),
  },

  // ------------------------------------------------------------ DOSYALAR
  assets: {
    images: {
      palace: 'assets/osmanli/palace.svg',
      throne: 'assets/osmanli/throne.svg',
      sancak: 'assets/osmanli/sancak.svg',
      tugra: 'assets/osmanli/tugra.svg',
      hazine: 'assets/osmanli/hazine.svg',
      ordu: 'assets/osmanli/ordu.svg',
      frame: 'assets/osmanli/frame.svg',
    },
    // Mehter müziği: src dosyası yoksa yerleşik synth mehter ritmi çalar.
    music: { src: 'assets/osmanli/mehter.mp3', synth: 'mehter', baseBpm: 92, volume: 0.5 },
    sounds: {
      join:    { src: 'assets/osmanli/join.mp3',    synth: 'pop' },
      rank:    { src: 'assets/osmanli/rank.mp3',    synth: 'coin' },
      rankBig: { src: 'assets/osmanli/rankbig.mp3', synth: 'cheer' },
      notice:  { src: 'assets/osmanli/notice.mp3',  synth: 'ding' },
      gong:    { src: 'assets/osmanli/gong.mp3',    synth: 'gong' },
      vizier:  { src: 'assets/osmanli/vizier.mp3',  synth: 'fanfareShort' },
      crown:   { src: 'assets/osmanli/crown.mp3',   synth: 'fanfare' },   // taç giyme mehter fanfarı
      city:    { src: 'assets/osmanli/city.mp3',    synth: 'boom' },
      tick:    { src: 'assets/osmanli/tick.mp3',    synth: 'pop' },      // açık artırma son 10 sn geri sayım
      bid:     { src: 'assets/osmanli/bid.mp3',     synth: 'coin' },     // yeni teklif
    },
  },

  // ------------------------------------------------------------ SAYAÇLAR / HUD (değerleri oyun modülü besler)
  counters: { treasury: 0, population: 0, army: 0, cities: 0 },
  hud: [
    { counter: 'treasury', label: 'HAZİNE', icon: '💰' },
    { counter: 'population', label: 'NÜFUS', icon: '👥' },
    { counter: 'army', label: 'ORDU', icon: '⚔️' },
    { counter: 'cities', label: 'ŞEHİR', icon: '🏙️' },
  ],

  camera: { mode: 'fixed', focus: { x: 540, y: 960 }, zoom: 1 },

  // ------------------------------------------------------------ SAHNE NESNELERİ (arkadan öne)
  entities: [
    { id: 'palace', image: 'palace', x: 540, y: 585, width: 1040, height: 640, anim: 'none', z: 1 },
    { id: 'tugra',  image: 'tugra',  x: 540, y: 617, width: 170, height: 105, anim: 'none', z: 2 },
    { id: 'sancakL1', image: 'sancak', x: 62,  y: 760, width: 120, height: 340, anim: 'sway', z: 3, phaseOffset: 0 },
    { id: 'sancakL2', image: 'sancak', x: 190, y: 800, width: 96,  height: 270, anim: 'sway', z: 3, phaseOffset: 0.5 },
    { id: 'sancakR2', image: 'sancak', x: 890, y: 800, width: 96,  height: 270, anim: 'sway', z: 3, phaseOffset: 1.0 },
    { id: 'sancakR1', image: 'sancak', x: 1018, y: 760, width: 120, height: 340, anim: 'sway', z: 3, phaseOffset: 1.5 },
    { id: 'throne', image: 'throne', x: 540, y: 800, width: 340, height: 370, anim: 'none', z: 4 },
    { id: 'hazine', image: 'hazine', x: 118, y: 930, width: 170, height: 130, anim: 'none', z: 6, label: 'HAZİNE' },
    { id: 'ordu',   image: 'ordu',   x: 962, y: 930, width: 150, height: 130, anim: 'none', z: 6, label: 'ORDU' },
    { id: 'frame',  image: 'frame',  x: 540, y: 960, width: 1080, height: 1920, anim: 'none', z: 9000 },
  ],

  // ------------------------------------------------------------ SAYAÇ -> EFEKT
  mappings: [
    { counter: 'army', target: 'tempo', from: [0, 400], to: [1, 1.3] },
    { counter: 'population', target: 'volume', from: [0, 600], to: [0.7, 1.0] },
  ],
  limits: { tempo: [0.8, 1.5] },

  // Genel (istemci tarafı) tetikleyiciler kullanılmıyor: tüm kurallar sunucudaki oyun modülünde.
  triggers: [],

  // =====================================================================
  //  OYUN KURALLARI (sunucu: games/rankgame.js)
  // =====================================================================
  game: {
    type: 'rankgame',
    storage: 'osmanli', // data/osmanli.json
    disclaimer: 'Rütbeler oyunlaştırma amaçlıdır; tarihsel makamların birebir hiyerarşik sıralaması değildir.',

    // ---------------------------------------------------------------- HEDİYE DEĞERİ
    // TikTok hediyesinin değeri YALNIZCA connector'ın hediye metadata alanından okunur: giftDetails.diamondCount.
    // (Mesajda ayrı bir "coin" alanı yoktur; coin <-> diamond dönüşümü yapılmaz, iki kavram karıştırılmaz.)
    // Olayda bu alan yoksa oda hediye kataloğundaki diamond_count yedek olarak kullanılır; ikisi de yoksa PUAN VERİLMEZ.
    giftValue: {
      field: 'diamondCount',   // geçerli metadata alanı (lib/events.js bunu gift.valueField olarak işaretler)
      unitLabel: '💎',         // ekranda değerin yanında gösterilen simge
      maxPerUnit: 1000000,     // akıl sağlığı sınırı: tek hediyenin birim değeri bunu aşarsa reddedilir
    },

    // ---------------------------------------------------------------- RÜTBELER (aşağıdan yukarıya; puanla)
    // minPoints : bu rütbeye ulaşmak için toplam puan (auto:true olanlar yalnızca puanla yükselir)
    // auto:false: puanla ASLA gelinmez (Padişah: yalnızca taht açık artırmasıyla)
    // army      : ordu gücüne katkısı · fx: terfi animasyonu (minor | major | vizier | prince | coronation)
    // look      : karakter kıyafeti (robe/trim/sash renkleri, hat: başlık türü, prop: elindeki simge)
    ranks: [
      { id: 'koylu',      name: 'Köylü',            icon: '🌾', minPoints: 0,      auto: true,  army: 0,  fx: 'minor', look: { robe: '#a68a64', trim: '#6e5638', sash: '#7d6444', hat: 'cap',   hatColor: '#7b5e3b', prop: '🌾' } },
      { id: 'ciftci',     name: 'Çiftçi',           icon: '🌱', minPoints: 100,    auto: true,  army: 0,  fx: 'minor', look: { robe: '#7c9a5b', trim: '#4f6a36', sash: '#d9c27a', hat: 'straw', hatColor: '#d8b45a', prop: '🌾' } },
      { id: 'esnaf',      name: 'Esnaf',            icon: '⚒️', minPoints: 300,    auto: true,  army: 0,  fx: 'minor', look: { robe: '#4f7cac', trim: '#2f527a', sash: '#e8d9b0', hat: 'fez',   hatColor: '#b3262d', prop: '🔨' } },
      { id: 'asker',      name: 'Asker',            icon: '🛡️', minPoints: 700,    auto: true,  army: 1,  fx: 'minor', look: { robe: '#8a2c2c', trim: '#5e1c1c', sash: '#c9a227', hat: 'helmet', hatColor: '#9aa3ad', prop: '🗡️' } },
      { id: 'yenicer',    name: 'Yeniçeri',         icon: '🪖', minPoints: 1500,   auto: true,  army: 3,  fx: 'minor', look: { robe: '#2b4a8b', trim: '#1b2f5e', sash: '#c9a227', hat: 'bork',  hatColor: '#f3ecd8', prop: '🏹' } },
      { id: 'onbasi',     name: 'Onbaşı',           icon: '🎖️', minPoints: 3000,   auto: true,  army: 4,  fx: 'minor', look: { robe: '#2b4a8b', trim: '#c9a227', sash: '#b3262d', hat: 'bork',  hatColor: '#f3ecd8', prop: '📯' } },
      { id: 'cavus',      name: 'Çavuş',            icon: '🎗️', minPoints: 5000,   auto: true,  army: 5,  fx: 'minor', look: { robe: '#1f6f78', trim: '#134a50', sash: '#c9a227', hat: 'turban', hatColor: '#1f8a95', prop: '🗡️' } },
      { id: 'sipahi',     name: 'Sipahi',           icon: '🐎', minPoints: 8000,   auto: true,  army: 6,  fx: 'major', look: { robe: '#b23a2e', trim: '#7d2219', sash: '#c9a227', hat: 'kalpak', hatColor: '#4b3a2a', prop: '🐎' } },
      { id: 'subasi',     name: 'Subaşı',           icon: '🏅', minPoints: 12000,  auto: true,  army: 7,  fx: 'major', look: { robe: '#7a3b6e', trim: '#c9a227', sash: '#e8d9b0', hat: 'turban', hatColor: '#9b4f8c', prop: '📜' } },
      { id: 'kaptan',     name: 'Kaptan-ı Derya',   icon: '⚓', minPoints: 18000,  auto: true,  army: 8,  fx: 'major', look: { robe: '#1c4a7a', trim: '#c9a227', sash: '#e8d9b0', hat: 'captain', hatColor: '#1c4a7a', prop: '⚓' } },
      { id: 'sancak',     name: 'Sancak Beyi',      icon: '🚩', minPoints: 25000,  auto: true,  army: 9,  fx: 'major', look: { robe: '#8c2f39', trim: '#c9a227', sash: '#f3e5c0', hat: 'turban', hatColor: '#f3e5c0', prop: '🚩' } },
      { id: 'beylerbeyi', name: 'Beylerbeyi',       icon: '🏹', minPoints: 40000,  auto: true,  army: 10, fx: 'major', look: { robe: '#5b2f7d', trim: '#c9a227', sash: '#f3e5c0', hat: 'bigturban', hatColor: '#f3e5c0', prop: '🏹' } },
      { id: 'pasa',       name: 'Paşa',             icon: '🧿', minPoints: 60000,  auto: true,  army: 12, fx: 'major', look: { robe: '#245c4f', trim: '#c9a227', sash: '#f3e5c0', hat: 'bigturban', hatColor: '#f6f0dc', prop: '📜' } },
      { id: 'kubbealti',  name: 'Kubbealtı Veziri', icon: '🕌', minPoints: 90000,  auto: true,  army: 0,  fx: 'major', look: { robe: '#2d3a6b', trim: '#e0b93a', sash: '#e0b93a', hat: 'bigturban', hatColor: '#f6f0dc', prop: '📜' } },
      { id: 'sehzade',    name: 'Şehzade',          icon: '🌙', minPoints: 130000, auto: true,  army: 0,  fx: 'prince', look: { robe: '#1f7a5a', trim: '#e0b93a', sash: '#e0b93a', hat: 'prince', hatColor: '#2fa37a', prop: '🌙' } },
      { id: 'sadrazam',   name: 'Sadrazam',         icon: '📜', minPoints: 180000, auto: true,  army: 0,  fx: 'vizier', look: { robe: '#7b1e3a', trim: '#e0b93a', sash: '#e0b93a', hat: 'vizier', hatColor: '#fff7e0', prop: '📜' } },
      // Padişahlık normal rütbe puanından BAĞIMSIZDIR: yalnızca taht açık artırmasının kazananı olur.
      { id: 'padisah',    name: 'Padişah',          icon: '👑', minPoints: null,   auto: false, army: 0,  fx: 'coronation', look: { robe: '#c9a227', trim: '#7b1e3a', sash: '#7b1e3a', hat: 'sultan', hatColor: '#fff7e0', prop: '👑' } },
    ],

    // ---------------------------------------------------------------- PUAN SİSTEMİ (hepsi ayarlanabilir)
    points: {
      perGiftValue: 1,         // PUAN DÖNÜŞÜM KATSAYISI: hediye değeri (giftDetails.diamondCount) başına kaç puan. Rose (1) = 1 puan.
      giftOverrides: {},       // Tek bir hediyeye özel BİRİM puan: { Rose: 2 }  (verilmezse değer x perGiftValue)
      join: 5,                 // !katıl ile katılma
      comment: 1,              // oyundaki oyuncunun sıradan yorumu
      follow: 30,              // takip (bir kez)
      share: 20,               // yayın paylaşımı (60 sn'de bir)
      like: 0.1,               // beğeni başına
      likeMaxPerEvent: 50,     // tek beğeni olayında sayılacak en fazla beğeni
    },

    // ---------------------------------------------------------------- TAHT (rütbeden ayrı yönetilir) + AÇIK ARTIRMA
    throne: {
      rank: 'padisah',        // taht rütbesi; aynı anda yalnızca 1 kişide; puanla kazanılmaz
      demoteTo: 'sadrazam',   // yeni Padişah gelince eskisi bu rütbeye iner
      auction: {
        enabled: true,
        intervalSec: 180,         // mücadeleler arası süre: açık artırma BİTİŞİNDEN itibaren 3 dk
        durationSec: 45,          // açık artırma süresi
        extendWindowSec: 10,      // son kaç saniyede gelen teklif süreyi uzatır
        extendBySec: 5,           // her uzatmada MEVCUT bitiş süresine eklenen saniye
        maxExtensionSec: 30,      // bir açık artırmada toplam uzatma üst sınırı
        minBidPoints: 0,          // geçerli bir tekliften beklenen en düşük tek hediye puanı (0: puanı olan her hediye)
        topN: 5,                  // ekranda gösterilen teklif sahibi sayısı
        restartDelaySec: 30,      // sunucu yeniden başlayınca, zamanı geçmiş bir mücadele ani başlamasın diye ertelenir
      },
    },

    // ---------------------------------------------------------------- KATILIM (ücretsiz katılım engellenmez)
    // Hangi olaylar oyuncuyu OTOMATİK Köylü olarak ekler. Yorumla katılım !katıl komutuyla yapılır.
    autoJoin: { comment: false, gift: true, follow: true, share: true, like: false, join: false },

    // ---------------------------------------------------------------- SPAM VE TEKRAR KORUMASI
    spam: {
      commentCooldownSec: 15,    // aynı kişinin sayılan yorumları arası
      duplicateWindowSec: 120,   // aynı kişinin aynı metni tekrarı bu sürede sayılmaz
      globalPerMinute: 120,      // tüm izleyicilerden dakikada en çok bu kadar yorum nüfusa/ekonomiye yazılır
    },
    idempotency: { maxIds: 2000 }, // son N olay kimliği saklanır (aynı TikTok olayı ikinci kez işlenmez; kayıtla birlikte kalıcı)

    // ---------------------------------------------------------------- KOMUTLAR (yazımlar değiştirilebilir)
    commands: {
      join:     ['!katıl', '!katil'],
      rank:     ['!rütbe', '!rutbe'],
      name:     ['!isim'],
      job:      ['!meslek'],
      army:     ['!ordu'],
      treasury: ['!hazine'],
      guide:    ['!hediyeler', '!hediye', '!rehber'],   // hediye ve rütbe rehberini ekranda açar
    },
    cooldowns: { join: 20, rank: 15, name: 5, nameChange: 120, job: 10, army: 20, treasury: 20, guide: 20 },

    // ---------------------------------------------------------------- MESLEKLER (!meslek çiftçi / asker / esnaf)
    // Rütbe yalnızca puanla yükseldiği için meslekler rütbe vermez; ekonomiye/orduya katkı sağlar.
    // unlockPoints: gereken puan · commentEffect: yorum başına ek üretim · armyBonus: ordu katkısı
    professions: {
      ciftci: { names: ['çiftçi', 'ciftci'], label: 'Çiftçi', icon: '🌾', unlockPoints: 30,  commentEffect: { production: 0.3, treasury: 0.5 } },
      esnaf:  { names: ['esnaf'],            label: 'Esnaf',  icon: '⚒️', unlockPoints: 100, commentEffect: { treasury: 2 } },
      asker:  { names: ['asker'],            label: 'Asker',  icon: '🛡️', unlockPoints: 200, armyBonus: 1 },
    },

    // ---------------------------------------------------------------- EKONOMİ (nüfus, üretim, şehir, hazine)
    economy: {
      start: { treasury: 100, population: 20, production: 1 },
      tickSec: 5,                  // pasif gelir aralığı
      incomePerProduction: 2,      // her tikte: hazine += üretim x 2
      effects: {
        join:            { population: 10, production: 0.2, treasury: 5 },
        comment:         { population: 0.5 },                       // her (spam sınırını geçen) normal yorum: şehir nüfusu
        playerComment:   { production: 0.05, treasury: 1 },         // yorumcu oyundaysa ek üretim/hazine
        follow:          {},                                        // takip ve beğeni AYRI sayaçlara yazılır (nüfusa değil)
        share:           { population: 3, treasury: 5 },
        likePerLike:     {},
        giftPerValue:    { treasury: 5, production: 0.01, population: 0.05 },  // hediye değeri başına
      },
      // Nüfus eşiğine ulaşılınca yeni şehir kazanılır
      cities: [
        { name: 'Söğüt', population: 0 },
        { name: 'Bursa', population: 120 },
        { name: 'Edirne', population: 300 },
        { name: 'İstanbul', population: 700 },
        { name: 'Selanik', population: 1200 },
        { name: 'Belgrad', population: 2000 },
        { name: 'Kahire', population: 3500 },
        { name: 'Bağdat', population: 5000 },
      ],
    },

    limits: { maxUsers: 5000, nameLength: 16 },
    nameBlocklist: [],             // yasaklı isimler (rütbe adları zaten yasaktır)

    // ---------------------------------------------------------------- HEDİYE VE RÜTBE REHBERİ (yayın sahnesinde, sağda)
    // Rehberdeki puan eşikleri, hediye karşılıkları, katsayı, açık artırma süreleri ve komut adları YUKARIDAKİ
    // ranks / points / throne.auction / commands / giftCatalog bölümlerinden OTOMATİK okunur; ikinci kez yazılmaz.
    // Burada yalnızca görünüm ayarları ve açıklama metinleri bulunur.
    // Metin değişkenleri: {join} {guide} {perGiftValue} {auctionEvery} {auctionSeconds} {extendWindow} {extendBy} {extendMax}
    guide: {
      title: 'HEDİYE VE RÜTBE REHBERİ',
      visible: true,                 // yan rehber varsayılan olarak açık (panelden gizlenip açılabilir)
      pageSize: 3,                   // yan rehberde aynı anda görünen rütbe sayısı
      pageSeconds: 4,                // sayfa değiştirme aralığı
      openSeconds: 15,               // !hediyeler yazılınca büyük rehber kaç sn açık kalsın
      giftFlashSeconds: 4.5,         // hediye geldiğinde puan/rütbe bilgisinin ekranda kalma süresi
      intro: {                       // yayın açıldığında otomatik bilgilendirme
        enabled: true,
        seconds: 10,
        onSceneLoad: true,           // sahne (OBS/LIVE Studio kaynağı) açılınca
        onLiveConnect: true,         // TikTok yayınına bağlanınca
      },
      texts: {
        freeTitle: 'ÜCRETSİZ KATIL',
        freeLine: 'Yorumuna {join} yaz, Köylü ol!',
        freeMore: 'Yorum, takip ve beğeniyle puan kazan.',
        throneTitle: 'PADİŞAH OL',
        throneRail: 'Açık artırmada en yüksek teklif · her {auctionEvery} dk',
        throneLine: 'Her {auctionEvery} dk taht açık artırması: en çok hediye puanı veren Padişah',
        throneMore: '{auctionSeconds} sn sürer · son {extendWindow} sn teklif +{extendBy} sn (en çok +{extendMax} sn)',
        pointsLine: '1 💎 hediye değeri = {perGiftValue} puan · combo toplamı sayılır',
        moreLine: 'Tüm liste: {guide}',
        welcomeTitle: 'Hoş geldin! Ücretsiz katıldın',
        welcomeLine: 'Köylü olarak başlıyorsun. Hediyeler için {guide} yaz.',
      },
    },

    // ---------------------------------------------------------------- HEDİYE KATALOĞU (rehberdeki örnek hediyeler + test paneli düğmeleri)
    // value: hediyenin connector metadata değeri (giftDetails.diamondCount)
    giftCatalog: [
      { name: 'Rose', value: 1, icon: '🌹' },
      { name: 'TikTok', value: 1, icon: '🎵' },
      { name: 'Finger Heart', value: 5, icon: '🫰' },
      { name: 'Doughnut', value: 30, icon: '🍩' },
      { name: 'Hand Hearts', value: 100, icon: '🫶' },
      { name: 'Diamond Ring', value: 500, icon: '💍' },
      { name: 'Galaxy', value: 1000, icon: '🌌' },
      { name: 'Sports Car', value: 7000, icon: '🏎️' },
      { name: 'Castle', value: 20000, icon: '🏰' },
      { name: 'Lion', value: 29999, icon: '🦁' },
      { name: 'Universe', value: 34999, icon: '🪐' },
    ],

    // ---------------------------------------------------------------- GÖRÜNÜM (rankgame-view.js)
    // Her bölge belirli rütbelerdeki oyuncuları gösterir. En yüksek rütbe/puan saraya en yakın yerde durur.
    // x,y: ilk koltuğun alt-orta noktası · cols/dx/dy: ızgara · scale: karakter ölçeği · max: görünür üst sınır
    // label: 'full' (isim+rütbe+puan) | 'name' · npc: oyuncu yokken sahneyi canlı tutan figüranlar
    // pin: bölgenin başına önce bu kişi alınır (throne.vizier: tahttan inen Padişah / Sadrazam)
    view: {
      sendTop: 250,
      zones: [
        { id: 'sultan', ranks: ['padisah'],  max: 1, x: 540, y: 925, cols: 1, dx: 0,   dy: 0,   scale: 1.2,  label: 'full', seat: { rank: 'padisah', name: 'TAHT BOŞ' } },
        { id: 'vizier', ranks: ['sadrazam'],  max: 1, x: 300, y: 990, cols: 1, dx: 0,   dy: 0,   scale: 0.95, label: 'full', pin: 'vizier', seat: { rank: 'sadrazam', name: 'Sadrazam' } },
        { id: 'prince', ranks: ['sehzade'],   max: 1, x: 780, y: 990, cols: 1, dx: 0,   dy: 0,   scale: 0.95, label: 'full', seat: { rank: 'sehzade', name: 'Şehzade' } },
        { id: 'divan',  ranks: ['kubbealti', 'pasa', 'beylerbeyi', 'sancak', 'kaptan'], max: 8, x: 540, y: 1140, cols: 8, dx: 118, dy: 0, scale: 0.68, label: 'full',
          npc: { count: 3, ranks: ['pasa', 'kubbealti', 'beylerbeyi'], names: ['Vezir', 'Vezir', 'Vezir'] } },
        { id: 'army',   ranks: ['subasi', 'sipahi', 'cavus', 'onbasi', 'yenicer', 'asker'], max: 20, x: 540, y: 1305, cols: 10, dx: 100, dy: 92, scale: 0.58, label: 'name',
          npc: { count: 12, ranks: ['yenicer', 'sipahi', 'yenicer', 'asker'], names: ['Yeniçeri', 'Sipahi', 'Yeniçeri', 'Asker'] } },
        { id: 'commons', ranks: ['esnaf', 'ciftci', 'koylu'], max: 36, x: 540, y: 1580, cols: 12, dx: 84, dy: 90, scale: 0.46, label: 'name',
          npc: { count: 12, perPopulation: 10, ranks: ['koylu', 'ciftci', 'esnaf', 'koylu'], names: ['Köylü', 'Çiftçi', 'Esnaf', 'Köylü'] } },
      ],
      leaderboardTop: 5,   // "en yüksek puanlı oyuncular" şeridi
    },
  },
};
