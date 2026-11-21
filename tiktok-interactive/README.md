# TikTok İnteraktif Sahne (Senaryo Motoru)

TikTok Live'da izleyicilerin **hediye / beğeni / yorum / takip / paylaşım** hareketlerine göre ekranda gerçek zamanlı animasyon tetikleyen,
**OBS Browser Source (1080×1920, 9:16)** uyumlu, **modüler (tak-çalıştır)** bir sistem.

Yeni konsept = `scenarios/` klasörüne yeni bir `.js` dosyası. Motor koduna dokunmazsın.

---

## 1) Klasör yapısı

```
tiktok-interactive/
├── package.json
├── server.js                 ← Sunucu: TikTok'a bağlanır, Socket.io ile olayları yayar
├── README.md                 ← Bu rehber
├── scenarios/
│   ├── index.js              ← Senaryo yükleyici (klasördeki .js dosyalarını otomatik bulur)
│   ├── halay.js              ← Varsayılan senaryo: Halay Modu
│   └── uzay.js               ← Çalışan 2. senaryo + YENİ SENARYO ŞABLONU (roket)
└── public/                   ← Tarayıcının/OBS'in açtığı her şey
    ├── index.html            ← Sahne + sağdaki gizlenebilir Test Paneli
    ├── style.css
    ├── app.js                ← Socket bağlantısı, ölçekleme, test paneli
    ├── js/
    │   ├── engine.js         ← Senaryo Motoru (tetikleyici/aksiyon/sayaç/kamera)
    │   └── audio.js          ← Ses motoru (MP3 yoksa yerleşik synth müzik/efekt)
    └── assets/
        ├── halay/            ← leader.svg, dancer.svg (+ buraya halay.mp3, join.mp3 ... koyabilirsin)
        └── uzay/             ← rocket.svg, astronaut.svg, planet.svg
```

## 2) Kurulum (bir kerelik)

1. **Node.js** kur (LTS, 18 veya üstü): <https://nodejs.org> → "LTS" indir, "İleri, İleri" diyerek kur.
2. Bu klasörü bilgisayarına al. Klasörün içinde bir terminal / Komut İstemi aç ve:

```bash
npm install
```

## 3) Çalıştırma

```bash
npm start
```

TikTok'a otomatik bağlanarak başlatmak için (kullanıcı adını `@` olmadan yaz):

```bash
TIKTOK_USERNAME=kullaniciadin npm start
```

Windows (Komut İstemi):

```bat
set TIKTOK_USERNAME=kullaniciadin && npm start
```

İstersen bağlantıyı Test Panelindeki **"Yayına Bağlan"** kutusundan da yapabilirsin; terminalde bir şey yazmana gerek kalmaz.

Açılan adresler:

| Ne için | Adres |
|---|---|
| Test / yönetim (Chrome'da aç) | `http://localhost:3000/?panel=1` (veya `/admin`) |
| **OBS'e eklenecek adres** | `http://localhost:3000/?obs=1` |
| OBS, şeffaf arka plan | `http://localhost:3000/?obs=1&transparent=1` |
| Belirli senaryo | `...&scenario=uzay` |

Port değiştirmek için: `PORT=4000 npm start`. Başlangıç senaryosu: `SCENARIO=uzay npm start`.

## 4) Önce test et (yayına bağlanmadan)

1. Chrome'da `http://localhost:3000/?panel=1` aç. Sağda test paneli görünür (gizlemek için ✕ veya **H** tuşu).
2. **"🔊 Sesi Aç"** butonuna bas (tarayıcılar sesi kullanıcı tıklamadan açmaz).
3. **🌹 Rose** butonuna bas → halaya yeni biri katılır, müzik hızlanır.
4. **"50 Kişi Ekle"** → kamera yavaşça uzaklaşır, tempo ve ses artar.
5. Yorum kutusuna `!zıpla` yaz → herkes zıplar. Beğeni butonlarıyla `❤ +100` dene.
6. **"Otomatik rastgele olaylar"** kutusu sürekli rastgele olay üretir (uzun süre izlemek için).

## 5) OBS'e ekleme (adım adım)

1. OBS Studio'yu aç. Canvas'ın dikey olması için: **Ayarlar → Video → Temel (Canvas) Çözünürlüğü = `1080x1920`**, Çıkış çözünürlüğü de `1080x1920`.
2. **Kaynaklar** kutusunda **＋** → **Tarayıcı (Browser)** → "Yeni oluştur" → **Tamam**.
3. Açılan pencerede:
   - **URL:** `http://localhost:3000/?obs=1`
   - **Genişlik:** `1080`  **Yükseklik:** `1920`
   - **"OBS ile sesi kontrol et (Control audio via OBS)"** kutusunu işaretle (sesi OBS karıştırıcıdan yönetirsin).
   - **Tamam**.
4. Sahne ekrana tam oturur. Sunucu (`npm start`) çalışırken kaynak canlıdır. Takılırsa kaynağa sağ tık → **Tarayıcıyı yenile (Refresh cache of current page)**.
5. Şeffaf kullanmak istersen URL'ye `&transparent=1` ekle (senaryo arka planı gizlenir, sadece karakterler/efektler kalır; kendi videonun üstüne bindirirsin).

> **Yayın sırasında OBS kaynağını yenileme**: sahnedeki kalabalık sıfırlanır.

## 6) Yeni senaryo nasıl yapılır?

1. `scenarios/uzay.js` dosyasını kopyala → `scenarios/yarisma.js`.
2. En üstteki `id` ve `name` değerlerini değiştir.
3. Görsellerini `public/assets/yarisma/` klasörüne koy (PNG, GIF, SVG, WebP; Lottie için `.json`), dosya yollarını `assets` bölümüne yaz.
4. `triggers` bölümünü düzenle: **hangi olay → hangi aksiyonlar**.
5. Test panelinde **"Senaryoları Yenile"** (sunucuyu kapatmana gerek yok) → senaryo seçiciden seç.

### Senaryo dosyasının bölümleri

| Bölüm | Ne işe yarar |
|---|---|
| `background` | Arka plan: `gradient` / `color` / `image` / `video`; `starfield: true` hareketli yıldızlar |
| `assets` | `images` (gövde/karakter görselleri), `music` (MP3 + yedek synth), `sounds` (efektler) |
| `entities` | Sahnedeki sabit nesneler (halay başı, roket...). `anim`: `dance` `float` `rumble` `pulse` `spin` `none`. `lottie:` yolu verirsen Lottie oynatır (internet gerekir) |
| `crowd` | İzleyicilerden oluşan kalabalık: `layout: ring` (halka halka) ya da `scatter` (süzülen). Profil fotoğrafı + kullanıcı adı otomatik |
| `camera` | `fitCrowd`: kalabalık büyüdükçe otomatik uzaklaşır (zoom-out). `fixed`: sabit |
| `counters` / `hud` | Sayaçlar ve ekranın üstündeki sayaç kutuları |
| `mappings` | Sayaç → efekt bağlantısı: `tempo`, `volume`, `bgspeed`, `entity:<id>.y` (nesneyi yukarı çıkarma) vb. |
| `limits` | Tempo ve geçici hızlanma tavanları |
| `triggers` | **Asıl kural listesi** |

### Tetikleyici örnekleri

```js
{ on: 'gift',    match: { giftName: 'Rose' },        actions: [ ... ] }   // Rose gelince
{ on: 'gift',    match: { minDiamonds: 100 },        actions: [ ... ] }   // 100+ elmaslık hediye
{ on: 'like',    every: 100,                         actions: [ ... ] }   // her 100 beğenide
{ on: 'comment', match: { command: '!zıpla' },       actions: [ ... ] }   // yorum komutu
{ on: 'comment', match: { contains: 'merhaba' },     actions: [ ... ] }   // yorum içinde geçiyorsa
{ on: 'follow' }  { on: 'share' }  { on: 'join' }                         // takip / paylaşım / yayına giriş
{ on: 'counter', counter: 'altitude', gte: 100,      actions: [ ... ] }   // sayaç eşiği
```

Ek seçenekler: `exclusive: true` (çalışırsa alttaki tetikleyicilere bakma), `cooldown: 5` (saniye), `enabled: false` (kapat).
Türkçe karakter duyarsızdır: `!zıpla` = `!zipla`.

### Aksiyonlar

| Aksiyon (Türkçe takma ad) | Parametreler |
|---|---|
| `spawn_character` (`ekrana_yeni_karakter_ekle`) | `badge`, `scale` |
| `move_entity` (`nesneyi_yukari_hareket_ettir`) | `target`, `dx`, `dy`, `to:{x,y}`, `minY`, `maxY`, `duration` |
| `play_sound` (`ses_cal`) | `sound`, `volume` |
| `shake` (`ekran_salla`) | `intensity`, `duration` |
| `counter_add` (`sayac_artir`) / `counter_set` | `counter`, `amount`, `multiplyBy: 'count'｜'diamonds'｜'likes'`, `value` |
| `banner` (`yazi_goster`) | `text`, `style: small｜big｜mega`, `duration`, `color` |
| `burst` (`parcacik_patlat`) | `emoji: [..]`, `count`, `x`, `y` |
| `flash` (`ekran_flas`) | `color`, `duration` |
| `temp_boost` (`gecici_hizlan`) | `target: tempo｜volume｜bgspeed`, `add`, `seconds` |
| `jump_all` (`hepsini_ziplat`) | – |
| `entity_anim` (`varlik_animasyonu`) | `target`, `anim`, `seconds` |
| `gift_popup` (`hediye_goster`) | hediye görselini ekranda uçurur |
| `reset_scene` (`sahneyi_sifirla`) | – |

Metinlerde: `{nickname}` `{uniqueId}` `{gift}` `{count}` `{diamonds}` `{text}` `{counter:crowd}`.

### Kendi görsel/ses dosyalarını kullanmak

- Halay başını değiştirmek: GIF/PNG dosyanı `public/assets/halay/` içine koy, `halay.js` içinde `leader: 'assets/halay/baskan.gif'` yaz. Gövde için `dancer` aynı şekilde.
- Müzik: `public/assets/halay/halay.mp3` dosyasını koy → otomatik kullanılır (yoksa yerleşik synth halay ritmi çalar). Tempo arttıkça MP3'ün hızı da artar.
- Efektler: `join.mp3`, `big.mp3`, `mega.mp3`, `like.mp3`, `jump.mp3` aynı mantıkla.
- Halaycı gövdesinin kafa yeri boş bırakılmıştır; profil fotoğrafı oraya yuvarlak olarak yerleşir.

## 7) Sık karşılaşılan sorunlar

| Sorun | Çözüm |
|---|---|
| `Bağlanılamadı` | Kullanıcı adı doğru mu (`@` olmadan)? Yayın **şu an canlı mı**? Kütüphaneyi güncelle: `npm update tiktok-live-connector`. TikTok zaman zaman yöntem değiştirir; çoğunlukla güncelleme çözer. |
| Ses yok | Tarayıcıda **"Sesi Aç"**'a tıkla. OBS'te "OBS ile sesi kontrol et"i işaretle ve karıştırıcıda kaynağın sesini aç. |
| Profil fotoğrafları harf olarak görünüyor | Test kullanıcılarında normaldir (sahte avatar). Gerçek yayında TikTok fotoğrafları gelir; gelmezse harfli avatara düşer. |
| OBS'te sahne küçük/büyük | Kaynak Genişlik/Yükseklik `1080×1920` olmalı. |
| Port dolu | `PORT=3100 npm start` ve OBS adresini buna göre değiştir. |
| Yeni senaryo listede yok | Dosya adı `_` ile başlamasın, `id` ve `name` olsun; terminalde hata satırına bak; "Senaryoları Yenile"ye bas. |

## 8) Teknik notlar

- Olay hattı: TikTok → `server.js` (normalize) → Socket.io `event` → `engine.js` (tetikleyici eşleştirme → aksiyon).
- Test paneli sahte olayları da **aynı hattan** gönderir; test ettiğin şey yayında çalışanla aynı koddur.
- Hediye serileri (combo) için yalnızca **artış** gönderilir; aynı serideki tekrarlar çift sayılmaz.
- Profil fotoğrafları `/avatar/proxy` üzerinden (yalnızca TikTok CDN adresleri) çekilir; hotlink engeli ve süresi dolan linkler sorun çıkarmaz.
- Bu araç TikTok'un resmî olmayan canlı yayın verisini kullanır (`tiktok-live-connector`); TikTok tarafındaki değişiklikler geçici kesintiye yol açabilir.
