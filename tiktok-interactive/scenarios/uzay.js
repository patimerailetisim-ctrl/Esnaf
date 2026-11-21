'use strict';
/**
 * SENARYO: UZAYA ROKET (ÇALIŞAN ŞABLON)
 * =====================================
 * Bu dosya aynı zamanda YENİ SENARYO YAPMAK İÇİN şablondur:
 *   1) Bu dosyayı kopyala  ->  scenarios/yarisma.js
 *   2) "id" ve "name" değerlerini değiştir
 *   3) Görselleri public/assets/<senaryo>/ klasörüne koy, yolları aşağıda güncelle
 *   4) triggers bölümünde "hangi olay -> hangi aksiyon" kuralını düzenle
 *   5) Sunucuyu yeniden başlatmadan: Test Paneli > "Senaryoları yenile"
 *
 * Fikir: Hediyeler roketi yukarı fırlatır (irtifa sayacı). İzleyicinin profil fotoğrafı
 * bir astronotun kaskında görünür ve uzayda süzülür. 100 km'ye ulaşınca kutlama olur.
 * Alanların açıklaması için scenarios/halay.js dosyasının başındaki yoruma bak.
 */
module.exports = {
  id: 'uzay',
  name: 'Uzaya Roket',
  description: 'Hediyeler roketi yukarı fırlatır, izleyiciler astronot olarak uzayda süzülür.',

  canvas: { width: 1080, height: 1920 },

  background: {
    type: 'gradient',
    colors: ['#02030f', '#0a1240', '#2b1a6b'],
    angle: 180,
    starfield: true,                         // hareketli yıldız alanı (hız: "bgspeed" hedefiyle değişir)
  },

  assets: {
    images: {
      rocket: 'assets/uzay/rocket.svg',
      astronaut: 'assets/uzay/astronaut.svg',
      planet: 'assets/uzay/planet.svg',
    },
    music: { src: 'assets/uzay/uzay.mp3', synth: 'space', baseBpm: 92, volume: 0.55 },
    sounds: {
      launch: { src: 'assets/uzay/launch.mp3', synth: 'rocket' },
      coin:   { src: 'assets/uzay/coin.mp3',   synth: 'coin' },
      pop:    { src: 'assets/uzay/pop.mp3',    synth: 'pop' },
      boom:   { src: 'assets/uzay/boom.mp3',   synth: 'boom' },
      win:    { src: 'assets/uzay/win.mp3',    synth: 'cheer' },
    },
  },

  counters: { altitude: 0, astronauts: 0, fuel: 0 },
  hud: [
    { counter: 'altitude', label: 'İRTİFA', icon: '🚀', unit: ' km', bar: true, max: 100 },
    { counter: 'astronauts', label: 'ASTRONOT', icon: '👨‍🚀' },
  ],

  crowd: {
    layout: 'scatter',
    counter: 'astronauts',
    body: 'astronaut',
    unique: true,
    max: 40,
    size: { width: 130, height: 180, head: 70 },
    scatter: { x: [90, 990], y: [330, 1400], minDistance: 190, avoid: [{ x: 540, y: 1100, r: 190 }] },
    anim: 'float',
  },

  camera: { mode: 'fixed', focus: { x: 540, y: 960 }, zoom: 1 },

  entities: [
    { id: 'planet', image: 'planet', x: 540, y: 1900, width: 1500, height: 700, anim: 'none', z: 1 },
    { id: 'rocket', image: 'rocket', x: 540, y: 1480, width: 230, height: 420, anim: 'rumble', z: 5000, label: '' },
  ],

  mappings: [
    // İrtifa 0 -> 100 arası roket ekranda aşağıdan yukarı çıkar
    { counter: 'altitude', target: 'entity:rocket.y', from: [0, 100], to: [1480, 430] },
    // Gezegen irtifa arttıkça aşağı kayıp gözden kaybolur
    { counter: 'altitude', target: 'entity:planet.y', from: [0, 40], to: [1900, 2600] },
    // Yıldızlar hızlanır (hız hissi)
    { counter: 'altitude', target: 'bgspeed', from: [0, 100], to: [1, 5] },
    { counter: 'altitude', target: 'tempo', from: [0, 100], to: [1, 1.5] },
  ],

  triggers: [
    // Büyük hediye: roket büyük sıçrar
    {
      name: 'Büyük hediye',
      on: 'gift', match: { minDiamonds: 100 }, exclusive: true,
      actions: [
        { type: 'spawn_character', badge: '⭐' },
        { type: 'counter_add', counter: 'altitude', amount: 0.15, multiplyBy: 'diamonds' },
        { type: 'play_sound', sound: 'launch' },
        { type: 'shake', intensity: 22, duration: 1.2 },
        { type: 'flash', color: 'rgba(255,170,60,0.7)', duration: 0.8 },
        { type: 'burst', emoji: ['🔥', '🚀', '⭐'], count: 28 },
        { type: 'banner', text: '🚀 {nickname} roketi fırlattı: {count}x {gift}!', style: 'big', duration: 3.2 },
        { type: 'temp_boost', target: 'bgspeed', add: 6, seconds: 5 },
      ],
    },
    // Gül: küçük itiş + astronot
    {
      name: 'Gül = yakıt',
      on: 'gift', match: { giftName: ['Rose', 'Gül'] }, exclusive: true,
      actions: [
        { type: 'spawn_character', badge: '🌹' },
        { type: 'counter_add', counter: 'altitude', amount: 3, multiplyBy: 'count' },
        { type: 'counter_add', counter: 'fuel', amount: 1, multiplyBy: 'count' },
        { type: 'play_sound', sound: 'coin' },
        { type: 'entity_anim', target: 'rocket', anim: 'pulse', seconds: 0.6 },
        { type: 'banner', text: '🌹 {nickname} yakıt ekledi!', style: 'small', duration: 2.2 },
        { type: 'temp_boost', target: 'bgspeed', add: 3, seconds: 2 },
      ],
    },
    {
      name: 'Diğer hediyeler',
      on: 'gift', match: {},
      actions: [
        { type: 'spawn_character' },
        { type: 'counter_add', counter: 'altitude', amount: 0.5, multiplyBy: 'diamonds' },
        { type: 'play_sound', sound: 'pop' },
        { type: 'banner', text: '🎁 {nickname}: {gift}', style: 'small', duration: 2 },
      ],
    },
    // Beğeniler roketi yavaş yavaş yükseltir
    { name: 'Beğeni itişi', on: 'like', actions: [{ type: 'counter_add', counter: 'altitude', amount: 0.05, multiplyBy: 'count' }] },
    {
      name: 'Her 100 beğenide turbo',
      on: 'like', every: 100,
      actions: [
        { type: 'play_sound', sound: 'launch' },
        { type: 'burst', emoji: ['❤️', '🔥'], count: 14 },
        { type: 'temp_boost', target: 'bgspeed', add: 4, seconds: 4 },
        { type: 'banner', text: '🔥 TURBO! 100 beğeni', style: 'small', duration: 2 },
      ],
    },
    // Yorum komutu
    {
      name: '!fırlat',
      on: 'comment', match: { command: ['!fırlat', '!firlat'] },
      actions: [
        { type: 'counter_add', counter: 'altitude', amount: 2 },
        { type: 'play_sound', sound: 'pop' },
        { type: 'shake', intensity: 8, duration: 0.5 },
        { type: 'banner', text: '🚀 {nickname}: FIRLAT!', style: 'small', duration: 1.6 },
      ],
    },
    {
      name: '!astronot',
      on: 'comment', match: { command: ['!astronot', '!katıl', '!katil'] },
      actions: [{ type: 'spawn_character', badge: '💬' }, { type: 'play_sound', sound: 'pop' }],
    },
    // Sayaç tetikleyici: irtifa 100'ü geçince
    {
      name: 'Hedefe ulaşıldı',
      on: 'counter', counter: 'altitude', gte: 100,
      actions: [
        { type: 'play_sound', sound: 'win' },
        { type: 'flash', color: 'rgba(255,255,255,0.9)', duration: 1.2 },
        { type: 'burst', emoji: ['🎉', '🪐', '⭐', '🌟'], count: 40 },
        { type: 'banner', text: '🪐 UZAYA ULAŞTIK! 🪐', style: 'mega', duration: 5 },
        { type: 'shake', intensity: 18, duration: 1.6 },
        { type: 'counter_set', counter: 'altitude', value: 0 },
      ],
    },
  ],
};
