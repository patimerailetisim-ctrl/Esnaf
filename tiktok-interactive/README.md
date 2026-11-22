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
├── lib/events.js             ← TikTok olaylarını normalize eder (hediye combo çift sayımı önlenir)
├── games/                    ← Sunucu taraflı oyun modülleri (yalnızca `game` bölümü olan senaryolar)
│   ├── index.js              ← Oyun tipi kayıt defteri
│   └── rankgame.js           ← Rütbe/taht oyunu: puan, rütbe, taht, komutlar, ekonomi, kalıcı kayıt
├── data/                     ← Kalıcı oyun kayıtları (osmanli.json ...) — otomatik oluşur
├── tests/                    ← Otomatik testler (npm test)
├── scenarios/
│   ├── index.js              ← Senaryo yükleyici (klasördeki .js dosyalarını otomatik bulur)
│   ├── halay.js              ← Varsayılan senaryo: Halay Modu
│   ├── uzay.js               ← Çalışan 2. senaryo + YENİ SENARYO ŞABLONU (roket)
│   └── osmanli.js            ← OSMANLI: TAHT MÜCADELESİ (tüm oyun kuralları bu dosyada)
└── public/                   ← Tarayıcının/OBS'in açtığı her şey
    ├── index.html            ← Sahne + sağdaki gizlenebilir Test Paneli
    ├── style.css
    ├── app.js                ← Socket bağlantısı, ölçekleme, test paneli
    ├── js/
    │   ├── engine.js         ← Senaryo Motoru (tetikleyici/aksiyon/sayaç/kamera)
    │   ├── audio.js          ← Ses motoru (MP3 yoksa yerleşik synth müzik/efekt, mehter dahil)
    │   ├── rankgame-view.js  ← Rütbe oyunu görünümü (karakterler, taç giyme, Sadrazam girişi)
    │   ├── rankgame-guide.js ← Hediye ve Rütbe Rehberi (sağ rehber, büyük rehber, hediye etkisi animasyonu)
    │   └── guide-data.js     ← Rehber verisini senaryo config'inden türetir (tarayıcı + panel + testler ortak kullanır)
    └── assets/
        ├── halay/            ← leader.svg, dancer.svg (+ buraya halay.mp3, join.mp3 ... koyabilirsin)
        ├── uzay/             ← rocket.svg, astronaut.svg, planet.svg
        └── osmanli/          ← palace, throne, sancak, tugra, hazine, ordu, frame (.svg); mehter.mp3 vb. buraya
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

## 6b) OSMANLI: TAHT MÜCADELESİ

Halay ve Uzay'dan bağımsız bir senaryodur (`scenarios/osmanli.js`). Diğerlerinden farkı: izleyicilerin **rütbesi, puanı ve tahtın sahibi
sunucuda tutulur ve diske kaydedilir** (`data/osmanli.json`), yani sunucuyu kapatıp açsan da kaybolmaz.
Sahneyi seçmek için test panelinde **Senaryo → "OSMANLI: TAHT MÜCADELESİ"** seç (veya `SCENARIO=osmanli npm start`).

> Rütbeler **oyunlaştırma amaçlıdır**; tarihsel makamların birebir hiyerarşik sıralaması olduklarını iddia etmez.

### Nasıl oynanır?
| İzleyici ne yapar | Ne olur |
|---|---|
| Yoruma **!katıl** yazar | Köylü olarak oyuna girer (ücretsiz). Tekrar yazınca çoğalmaz. |
| Yorum yapar / takip eder / paylaşır / beğenir | Küçük puan kazanır, nüfus-üretim-hazine büyür, şehirler kazanılır |
| **!rütbe** | Ekranda rütbe kartı: rütbe, puan, sıra, sonraki rütbe |
| **!isim Yeni Ad** | Ekranda görünen adı değiştirir (temizlenir, en çok 16 karakter, 2 dk'da bir) |
| **!meslek çiftçi / esnaf / asker** | Meslek seçer (yeterli puan gerekir); üretim veya ordu gücüne katkı verir |
| **!ordu** / **!hazine** | Ordu gücü ve hazine özeti kartı |
| **!hediyeler** | Hediye ve rütbe rehberini büyük açar (katılmadan da kullanılabilir) |
| Hediye gönderir | Elmas değerine göre puan ve **rütbe** kazanır; en büyük hediye **Padişah** yapar |

TikTok'a sohbetten cevap yazılamadığı için tüm yanıtlar **ekranda kart olarak** görünür.

### Rütbe sıralaması (aşağıdan yukarıya)
Köylü → Çiftçi → Esnaf → Asker → Yeniçeri → Onbaşı → Çavuş → Sipahi → Subaşı → Kaptan-ı Derya → Sancak Beyi → Beylerbeyi → Paşa → Kubbealtı Veziri → **Sadrazam** → **Şehzade** → **Padişah**

> ⚠ İstek metninde liste (Sadrazam < Şehzade < Padişah) ile "Padişah'ın bir altı Sadrazam" cümlesi birbirini tutmuyordu. **Listedeki sıra** uygulandı.
> Sadrazam'ı Padişah'ın hemen altına almak için `scenarios/osmanli.js` içinde **üç şeyi** değiştir (testle doğrulandı):
> 1. `ranks` listesinde `sadrazam` kaydını `sehzade` kaydının **ardına** taşı (… Kubbealtı → Şehzade → Sadrazam → Padişah).
> 2. `giftRankGrants` içinde `sadrazam` ile `sehzade` eşiklerinin rütbe adlarını yer değiştir (5000💎 → `sehzade`, 15000💎 → `sadrazam`).
> 3. İstersen `throne.demoteTo` / `throne.fallbackRank` değerini `'sadrazam'` yap.
> Kod değişikliği gerekmez.

### Hediye sistemi (hepsi `scenarios/osmanli.js` → `game` içinde, kodda sabit eşik yok)
- **`points.perDiamond`**: 1 elmas (coin) kaç puan? (varsayılan 10 → Rose = 10 puan). Tek hediyeye özel puan için `points.giftOverrides: { Rose: 15 }`.
- **Puanla terfi**: her `ranks` kaydının `minPoints` değeri. `auto: false` olan rütbelere (Sadrazam, Şehzade, Padişah) puanla gelinmez.
- **`giftRankGrants`**: *tek bir olaydaki* (combo toplamı dahil) elmas değeri `minDiamonds`'e ulaşırsa o rütbe hemen kazanılır. Örnek: 5💎 Çiftçi, 1000💎 Beylerbeyi, 5000💎 Sadrazam, 15000💎 Şehzade, 29999💎 (Lion) **Padişah**.
- Rütbe hediyeyle **asla düşmez**. Rose ve küçük hediyeler yalnızca yavaş gelişim sağlar.

### Oyun dengesi
- **Tek aktif Padişah.** Taht, rütbeden ayrı tutulur (`throne` durumu ayrı kaydedilir); yeni Padişah gelince eskisi `throne.demoteTo` rütbesine (varsayılan Şehzade; Sadrazam yazabilirsin) iner.
- **Taç koruma süresi** (`throne.cooldownSec`, varsayılan 120 sn): bu sürede kimse tahta geçemez; en büyük hediyeyi gönderen bile `throne.fallbackRank` (Şehzade) kazanır.
- **Devralma bedeli** (`throne.overtakeFactor`): tahtı almak için tek hediye, mevcut Padişah'ın taç değeri x çarpan kadar olmalı. Padişah kendi kendini tekrar taçlandıramaz.
- Hiçbir hediye **başka kullanıcının karakterini silmez**. Kalabalık ekranda en çok `view.zones[].max` kişi çizilir (taşanlar "+N daha" olarak gösterilir) ama kayıtları durur.
- Yorum puanı bekleme süreli (`commentCooldownSec`), komutlarda spam koruması (`cooldowns`) vardır.

### Test panelinden oynamak (yayına bağlanmadan)
`http://localhost:3000/?panel=1` → senaryoyu seç → **"🎮 Oyun"** bölümü açılır:
1. **Test Kullanıcısı** kutusuna bir ad yaz (örn. `ahmet`) → **Oyun Komutları → !katıl** (veya "50 Kişi !katıl").
2. **Oyun Hediyeleri**: 🌹 Rose'dan 🪐 Universe'e kadar; her biri o adla gönderilir. Rütbe animasyonlarını izle.
3. **Sadrazam** girişi için `Sports Car` (7000💎), **taç giyme** için `Lion` (29999💎) gönder.
4. Aynı anda ikinci biriyle `Universe` gönder → önce **taht koruma** kartını gör; 2 dk sonra (veya yönetici düğmesiyle tahtı boşaltarak) devralmayı dene.
5. **Yönetici** düğmeleri: seçili oyuncuya rütbe ver, taç giydir, tahtı boşalt, +100 puan, oyuncuyu sil.
6. **Kullanıcı Listesi** (satıra tıklayınca o oyuncu seçilir) ve **Rütbe Tablosu** panelde canlı görünür.
7. **Sahneyi Sıfırla** yalnızca görseli yeniden kurar, kalıcı veriyi **silmez**. Veriyi silmek için "⚠ Tüm Oyun Verisini Sıfırla" (onay için SIFIRLA yazılır).

### Hediye ve Rütbe Rehberi (yayın sahnesinde)
Sahnenin **sağ tarafında** altın işlemeli bir rehber durur; izleyiciler hangi rütbe için hangi hediyenin ve kaç puanın gerektiğini görür.
Rehber **yayın sahnesinin** (OBS / TikTok LIVE Studio'ya giren sayfanın) parçasıdır; test paneli ayrı bir katmandır ve yayına girmez.

| Bölüm | Ne yapar |
|---|---|
| **Yan rehber** | Rütbeleri 3'erli sayfalar halinde döndürür: rütbe adı · hediye adı · gereken 💎 ve puan. Altta sürekli **👑 PADİŞAH OL** (taç bedeli + hediye adı) ve **🆓 ÜCRETSİZ KATIL** (`!katıl`) kutuları görünür. |
| **Büyük rehber** | `!hediyeler` (veya `!hediye`, `!rehber`) yazılınca 15 sn tüm rütbeleri iki sütunda, büyük puntoyla gösterir. Rehber gizliyken de açılır. Spam'e karşı tüm izleyiciler için ortak 20 sn bekleme vardır. |
| **Yayın açılışı** | Sahne açılınca ve TikTok yayınına bağlanınca otomatik **10 sn'lik** katılım + hediye bilgilendirmesi çıkar. Kapatmak için `intro.enabled: false`; tek bir sayfada kapatmak için URL'ye `&intro=0` ekle. |
| **Hediye etkisi** | Hediye gelince rehberin içinde kısa animasyonla: kazanılan **puan**, kazanılan/yaklaşılan **rütbe**, sonraki rütbe için kalan puan. Ardından listede o rütbe vurgulanır. Rehber gizliyken gösterilmez. |
| **`!katıl` bilgisi** | Yeni katılana "Hoş geldin, ücretsiz katıldın" kartı çıkar; rehberin altında kalıcı olarak da yazar. |
| **Göster / gizle** | Test panelinde **Rehberi Gizle / Göster** düğmesi (yayıncı kontrolü, OBS sahnesine anında yansır ve kalıcıdır). |

**Tek kaynak:** Rehberdeki hiçbir hediye eşiği, puan, taç bedeli veya komut adı ikinci kez yazılmaz; hepsi `scenarios/osmanli.js` içindeki
`ranks`, `points`, `giftRankGrants`, `commands` ve `testGifts` (örnek hediye kataloğu) bölümlerinden okunur. Eşiği değiştirirsen rehber kendiliğinden değişir.
Yalnızca görünüm ve metinler `game.guide` bölümündedir (`pageSize`, `pageSeconds`, `openSeconds`, `intro`, `texts`...).

**Önemli (etkin eşik):** Bir rütbeye iki yoldan ulaşılır: hediye eşiği (`giftRankGrants`) veya puan (`minPoints`).
Rehber, yeni bir oyuncunun tek hediyede o rütbeye ulaşması için gereken **en düşük** elmas değerini gösterir. Varsayılan ayarda (1💎 = 10 puan)
Sipahi ve üstü (Kubbealtı Veziri'ne kadar) için puan yolu hediye eşiğinden daha ucuzdur; bu yüzden örneğin Kaptan-ı Derya `450 💎+` görünür (`giftRankGrants` içindeki `600` değil).
Bunu dengelemek istersen `points.perDiamond` değerini düşür veya `minPoints` değerlerini yükselt; rehber otomatik uyar.

**Test paneli (Oyun bölümü → Hediye Rehberi):** rehberi gizle/göster, büyük rehberi aç, 10 sn tanıtımı oynat ve **Hediye Eşikleri** tablosu
(yayındaki rehberle aynı veriden). **"Eşikleri Canlı Sına"** her eşik için geçici bir test oyuncusu ekleyip tam eşik değerinde hediye gönderir,
sunucudaki gerçek sonucu okur ve tabloda ✓/✗ gösterir; sonra test oyuncularını siler (taht doluysa Padişah satırını atlar, gerçek Padişah'ı korur). Yayın sırasında kullanma.
Panel açıkken sahne alanı daralır, böylece panel rehberi örtmez.

### Güvenlik ve kayıt notları
- Oyun verisini değiştiren işlemler (yönetici düğmeleri, sahte olaylar) varsayılan olarak **yalnızca sunucunun çalıştığı bilgisayardan** kabul edilir. Başka cihazdan test için `ALLOW_REMOTE_ADMIN=1 npm start`.
- Kayıt klasörü: `data/` (değiştirmek için `DATA_DIR=...`). Yazma atomiktir; bozuk dosya silinmez, `.bozuk-...` adıyla kenara alınır; son iyi kayıt `osmanli.json.bak` olarak tutulur.
- Olaylar **aktif senaryoya** işlenir. OBS URL'sine `&scenario=osmanli` eklemek yalnızca *görüntülenen* senaryoyu seçer; sunucudaki aktif senaryo Osmanlı değilse olaylar oyuna işlenmez. Önce panelden Osmanlı'yı seç.
- `npm test` oyun kurallarını (taht, rütbe, komutlar, kalıcılık, combo) otomatik sınar.

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
