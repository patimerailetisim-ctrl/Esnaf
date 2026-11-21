'use strict';
/**
 * SENARYO: HALAY MODU
 * ===================
 * Bir senaryo = tamamen VERİ (JSON'a çevrilebilir bir nesne). Fonksiyon yazılmaz.
 * Motor (public/js/engine.js) bu veriyi okuyup sahneyi kurar.
 *
 * Bölümler:
 *   background  -> arka plan (renk geçişi / resim / video / yıldız alanı)
 *   assets      -> görseller, müzik, ses efektleri (dosya yolları public/ klasörüne göre)
 *   counters    -> sayaçların başlangıç değerleri
 *   hud         -> ekranın üstünde görünecek sayaç kutuları
 *   crowd       -> izleyicilerden oluşan "kalabalık" (halaycılar) nasıl dizilecek
 *   camera      -> kamera (otomatik uzaklaşma)
 *   entities    -> sahnedeki sabit karakter/nesneler (halay başı, roket vb.)
 *   mappings    -> sayaç -> efekt bağlantıları (kalabalık arttıkça tempo/ses/zoom)
 *   triggers    -> HANGİ olay -> HANGİ aksiyonları çalıştırsın
 *
 * Kullanılabilir aksiyonlar (Türkçe takma adlar parantez içinde):
 *   spawn_character (ekrana_yeni_karakter_ekle)   move_entity (nesneyi_hareket_ettir)
 *   play_sound (ses_cal)        shake (ekran_salla)       counter_add (sayac_artir)
 *   counter_set (sayac_ayarla)  banner (yazi_goster)      burst (parcacik_patlat)
 *   flash (ekran_flas)          temp_boost (gecici_hizlan) jump_all (hepsini_ziplat)
 *   entity_anim (varlik_animasyonu)   gift_popup (hediye_goster)   reset_scene (sahneyi_sifirla)
 *
 * Metinlerde kullanılabilen değişkenler:
 *   {nickname} {uniqueId} {gift} {count} {diamonds} {text} {total} {counter:crowd}
 */
module.exports = {
  id: 'halay',
  name: 'Halay Modu',
  description: 'Gül atan / takip eden / paylaşan herkes halaya katılır. Kalabalık arttıkça kamera uzaklaşır, müzik hızlanır.',

  canvas: { width: 1080, height: 1920 },

  // ------------------------------------------------------------ ARKA PLAN
  background: {
    type: 'gradient',                       // gradient | color | image | video
    colors: ['#1a0b3b', '#6b1d5e', '#e8553d', '#ffb347'],
    angle: 180,
    // image: 'assets/halay/zemin.jpg',     // type: 'image' için
    // video: 'assets/halay/arka.mp4',      // type: 'video' için
    starfield: false,
    floor: { color: 'rgba(255, 220, 160, 0.10)', y: 1000, rx: 520, ry: 700 }, // halay zemini (yumuşak elips)
  },

  // ------------------------------------------------------------ DOSYALAR
  assets: {
    images: {
      leader: 'assets/halay/leader.svg',    // kendi GIF/PNG dosyanla değiştirebilirsin: 'assets/halay/baskan.gif'
      dancer: 'assets/halay/dancer.svg',
    },
    // Müzik: src dosyası yoksa otomatik olarak yerleşik "synth" halay ritmi çalar.
    music: { src: 'assets/halay/halay.mp3', synth: 'halay', baseBpm: 100, volume: 0.6 },
    sounds: {
      join:  { src: 'assets/halay/join.mp3',  synth: 'pop' },
      big:   { src: 'assets/halay/big.mp3',   synth: 'cheer' },
      mega:  { src: 'assets/halay/mega.mp3',  synth: 'boom' },
      like:  { src: 'assets/halay/like.mp3',  synth: 'ding' },
      jump:  { src: 'assets/halay/jump.mp3',  synth: 'whoosh' },
    },
    // lottie: { confetti: 'assets/halay/confetti.json' }   // (isteğe bağlı) Lottie animasyonları
  },

  // ------------------------------------------------------------ SAYAÇLAR VE HUD
  counters: { crowd: 0, likes: 0, gifts: 0 },
  hud: [
    { counter: 'crowd', label: 'HALAYCI', icon: '💃' },
    { counter: 'likes', label: 'BEĞENİ', icon: '❤️' },
    { counter: 'gifts', label: 'HEDİYE', icon: '🎁' },
  ],

  // ------------------------------------------------------------ KALABALIK
  crowd: {
    layout: 'ring',               // ring (halka halka) | scatter (rastgele süzülme)
    counter: 'crowd',             // kalabalık sayısı bu sayaca yazılır
    body: 'dancer',               // assets.images içindeki gövde görseli
    unique: true,                 // aynı kişi tekrar atarsa yeni karakter eklenmez, mevcut olan coşar
    max: 100,                     // ekranda aynı anda en fazla kaç kişi (fazlası eskiyi çıkarır)
    size: { width: 128, height: 190, head: 82 },
    ring: { cx: 540, cy: 1010, firstRx: 250, stepRx: 175, ratioY: 1.3, spacing: 138 },
    anim: 'dance',
  },

  // ------------------------------------------------------------ KAMERA
  camera: { mode: 'fitCrowd', focus: { x: 540, y: 1010 }, paddingX: 70, paddingY: 150, minZoom: 0.3, maxZoom: 1, smooth: 2.2 },

  // ------------------------------------------------------------ SABİT KARAKTERLER
  entities: [
    { id: 'leader', image: 'leader', x: 540, y: 1010, width: 250, height: 340, anim: 'dance', label: 'HALAY BAŞI', z: 5000 },
  ],

  // ------------------------------------------------------------ SINIRLAR
  // tempo: nihai hız çarpanı aralığı, maxBoost: geçici hızlandırmaların toplam tavanı
  limits: { tempo: [0.6, 2.6], maxBoost: { tempo: 0.8 } },

  // ------------------------------------------------------------ SAYAÇ -> EFEKT BAĞLANTILARI
  mappings: [
    // 0 kişiyken normal tempo, 60 kişide 2.1 kat hızlı
    { counter: 'crowd', target: 'tempo',  from: [0, 60], to: [1.0, 2.1] },
    // kalabalık arttıkça müzik sesi yükselir
    { counter: 'crowd', target: 'volume', from: [0, 60], to: [0.45, 1.0] },
  ],

  // ------------------------------------------------------------ TETİKLEYİCİLER (ÖNEMLİ KISIM)
  // Sırayla kontrol edilir. "exclusive: true" olan bir tetikleyici çalışırsa alttakilere bakılmaz.
  triggers: [
    // --- MEGA hediyeler (>= 1000 elmas: Galaxy, Lion, Universe...)
    {
      name: 'Mega hediye',
      on: 'gift', match: { minDiamonds: 1000 }, exclusive: true,
      actions: [
        { type: 'spawn_character', badge: '👑', scale: 1.25 },
        { type: 'counter_add', counter: 'gifts', amount: 1, multiplyBy: 'count' },
        { type: 'play_sound', sound: 'mega' },
        { type: 'shake', intensity: 26, duration: 1.4 },
        { type: 'flash', color: 'rgba(255,230,120,0.85)', duration: 0.9 },
        { type: 'burst', emoji: ['👑', '🎉', '✨', '🔥'], count: 36 },
        { type: 'banner', text: '👑 {nickname} MEGA {gift} gönderdi! 👑', style: 'mega', duration: 4 },
        { type: 'temp_boost', target: 'tempo', add: 0.9, seconds: 14 },
        { type: 'jump_all' },
      ],
    },

    // --- Büyük hediyeler (>= 30 elmas)
    {
      name: 'Büyük hediye',
      on: 'gift', match: { minDiamonds: 30 }, exclusive: true,
      actions: [
        { type: 'spawn_character', badge: '⭐', scale: 1.12 },
        { type: 'counter_add', counter: 'gifts', amount: 1, multiplyBy: 'count' },
        { type: 'play_sound', sound: 'big' },
        { type: 'shake', intensity: 12, duration: 0.7 },
        { type: 'burst', emoji: ['🎉', '✨', '💃'], count: 20 },
        { type: 'banner', text: '⭐ {nickname} {count}x {gift} ile halayı coşturdu!', style: 'big', duration: 3 },
        { type: 'temp_boost', target: 'tempo', add: 0.5, seconds: 9 },
        { type: 'jump_all' },
      ],
    },

    // --- GÜL (Rose) -> halaya katıl
    {
      name: 'Gül = halaya katıl',
      on: 'gift', match: { giftName: ['Rose', 'Gül'] }, exclusive: true,
      actions: [
        { type: 'spawn_character', badge: '🌹' },
        { type: 'counter_add', counter: 'gifts', amount: 1, multiplyBy: 'count' },
        { type: 'play_sound', sound: 'join' },
        { type: 'burst', emoji: ['🌹'], count: 10 },
        { type: 'banner', text: '🌹 {nickname} halaya katıldı!', style: 'small', duration: 2.4 },
        { type: 'temp_boost', target: 'tempo', add: 0.25, seconds: 5 },
      ],
    },

    // --- Diğer tüm hediyeler
    {
      name: 'Her hediye halaya katar',
      on: 'gift', match: {},
      actions: [
        { type: 'spawn_character' },
        { type: 'counter_add', counter: 'gifts', amount: 1, multiplyBy: 'count' },
        { type: 'play_sound', sound: 'join' },
        { type: 'burst', emoji: ['🎁'], count: 8 },
        { type: 'banner', text: '🎁 {nickname} {gift} gönderdi, halaya katıldı!', style: 'small', duration: 2.4 },
        { type: 'temp_boost', target: 'tempo', add: 0.2, seconds: 5 },
      ],
    },

    // --- Takip / Paylaşım -> yeni kişi halaya katılır
    {
      name: 'Takip = halaya katıl',
      on: 'follow',
      actions: [
        { type: 'spawn_character', badge: '➕' },
        { type: 'play_sound', sound: 'join' },
        { type: 'banner', text: '➕ {nickname} takip etti ve halaya girdi!', style: 'small', duration: 2.4 },
      ],
    },
    {
      name: 'Paylaşım = halaya katıl',
      on: 'share',
      actions: [
        { type: 'spawn_character', badge: '🔁' },
        { type: 'play_sound', sound: 'join' },
        { type: 'banner', text: '🔁 {nickname} yayını paylaştı, halaya katıldı!', style: 'small', duration: 2.4 },
      ],
    },

    // --- Yayına giren herkes için (kalabalık fazla olur diye kapalı; açmak için enabled: true yap)
    {
      name: 'Yayına katılan (kapalı)',
      enabled: false,
      on: 'join',
      actions: [{ type: 'spawn_character' }],
    },

    // --- Beğeniler
    { name: 'Beğeni sayacı', on: 'like', actions: [{ type: 'counter_add', counter: 'likes', amount: 1, multiplyBy: 'count' }] },
    {
      name: 'Her 100 beğenide coşku',
      on: 'like', every: 100,
      actions: [
        { type: 'play_sound', sound: 'like' },
        { type: 'burst', emoji: ['❤️', '💖'], count: 16 },
        { type: 'banner', text: '❤️ 100 beğeni daha! Halay hızlanıyor!', style: 'small', duration: 2.2 },
        { type: 'temp_boost', target: 'tempo', add: 0.4, seconds: 8 },
        { type: 'jump_all' },
      ],
    },

    // --- Yorum komutları
    {
      name: '!zıpla',
      on: 'comment', match: { command: ['!zıpla', '!zipla'] },
      actions: [
        { type: 'play_sound', sound: 'jump' },
        { type: 'jump_all' },
        { type: 'banner', text: '🦘 {nickname}: ZIPLAAA!', style: 'small', duration: 1.8 },
      ],
    },
    {
      name: '!hızlan',
      on: 'comment', match: { command: ['!hızlan', '!hizlan', '!hiz'] },
      actions: [
        { type: 'temp_boost', target: 'tempo', add: 0.6, seconds: 6 },
        { type: 'banner', text: '⚡ {nickname} halayı hızlandırdı!', style: 'small', duration: 2 },
      ],
    },
    {
      name: '!halay (yorumla katıl)',
      on: 'comment', match: { command: ['!halay', '!katıl', '!katil'] },
      actions: [
        { type: 'spawn_character', badge: '💬' },
        { type: 'play_sound', sound: 'join' },
        { type: 'banner', text: '💬 {nickname} halaya katıldı!', style: 'small', duration: 2.2 },
      ],
    },
  ],
};
