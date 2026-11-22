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
    │   ├── rankgame-auction.js ← Taht mücadelesi kartı (geri sayım, ilk 5 teklif, 3-2-1)
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

Halay ve Uzay'dan bağımsız bir senaryodur (`scenarios/osmanli.js`). Oyun mantığı **sunucuda** çalışır (`games/rankgame.js`); izleyicilerin
puanı, rütbesi, taht ve açık artırma durumu `data/osmanli.json` dosyasına kaydedilir, sunucu yeniden başlasa da kaybolmaz.
Seçmek için test panelinde **Senaryo → "OSMANLI: TAHT MÜCADELESİ"** (veya `SCENARIO=osmanli npm start`).
**Bütün kurallar `scenarios/osmanli.js` içindedir.**

> Rütbeler **oyunlaştırma amaçlıdır**; tarihsel makamların birebir hiyerarşik sıralaması olduklarını iddia etmez.

### Nasıl oynanır?
| İzleyici ne yapar | Ne olur |
|---|---|
| Yoruma **!katıl** yazar | Ücretsiz Köylü olur (tekrar yazınca çoğalmaz) |
| Normal yorum yazar | Şehir **nüfusuna** katkı (katılmamış izleyici dahil); oyuncuysa küçük puan. Spam sınırlıdır |
| Takip eder / beğenir / paylaşır | **Ayrı sayaçlara** yazılır (takip, beğeni, paylaşım); oyuncuya küçük puan |
| Hediye gönderir | Normal oyun puanı kazanır; **taht açık artırması sürüyorsa** aynı puan teklifine de eklenir |
| **!rütbe** `!isim Ad` `!meslek çiftçi\|esnaf\|asker` `!ordu` `!hazine` | Ekranda bilgi kartı / ayar |
| **!hediyeler** (`!hediye`, `!rehber`) | Hediye ve rütbe rehberi büyük açılır |

### Hediye değeri ve puan (coin/diamond karışmaz)
- Hediyenin değeri **yalnızca** connector'ın hediye metadata alanından okunur: `giftDetails.diamondCount` (`lib/events.js`). Mesajda ayrı bir "coin" alanı yoktur; **coin ↔ diamond dönüşümü yapılmaz**, `fanTicketCount` gibi başka alanlar kullanılmaz.
- Olay metadata'sında değer eksik gelirse yedek olarak oda hediye kataloğu (`fetchAvailableGifts`) kullanılır. İkisi de yoksa **puan verilmez** (sayaç: `unknownGifts`). Başka alandan gelen değer reddedilir (`rejectedGifts`).
- **Puan = hediye değeri × `points.perGiftValue`** (puan dönüşüm katsayısı, varsayılan `1`; Rose = 1 puan, Lion = 29.999 puan). Tek hediyeye özel birim puan: `points.giftOverrides: { Rose: 2 }`.
- **Combo çift sayılmaz:** combo (`giftDetails.type = 1`) hediyelerinde her güncellemede yalnızca *artış* yazılır; seri bitiş olayı fark 0 olduğu için puan eklemez; seri kimliği (`groupId`) değişince yeni seri başlar.
- Hediye puanları kullanıcı kaydına (`points`, `giftValue`, `giftPoints`) ve genel sayaçlara yazılır, **kalıcıdır**.

### Rütbeler (yalnızca puanla; `ranks` listesi)
Köylü 0 · Çiftçi 100 · Esnaf 300 · Asker 700 · Yeniçeri 1.500 · Onbaşı 3.000 · Çavuş 5.000 · Sipahi 8.000 · Subaşı 12.000 · Kaptan-ı Derya 18.000 ·
Sancak Beyi 25.000 · Beylerbeyi 40.000 · Paşa 60.000 · Kubbealtı Veziri 90.000 · Şehzade 130.000 · Sadrazam 180.000 · **Padişah (puandan bağımsız)**

Rütbe puanla yükselir ve asla puanla düşmez. **Padişahlık normal puana bağlı değildir** (`auto:false`); yalnızca taht açık artırmasını kazananın olur.
Önceki sürümdeki "tek hediyeyle rütbe/taç" kuralları kaldırıldı.

### Taht açık artırması (`throne.auction`)
| Kural | Ayar |
|---|---|
| Her **3 dk**'da bir otomatik başlar (sayaç açık artırmanın **bitişinden** itibaren işler) | `intervalSec: 180` |
| **45 sn** sürer | `durationSec: 45` |
| Süre boyunca gelen **hediye puanları** kullanıcının *teklifine* eklenir; **normal puan da aynı anda yazılır**. Her kullanıcının teklifi ayrı tutulur | – |
| **Son 10 sn**'de gelen teklif **mevcut bitiş süresine +5 sn** ekler (zincirleme). **Toplam uzatma en fazla 30 sn** (en çok 6 uzatma; kalan hak adımdan azsa yalnızca kalanı eklenir). Tavan dolunca teklif sayılır ama süre uzamaz | `extendWindowSec: 10`, `extendBySec: 5`, `maxExtensionSec: 30` |
| En yüksek teklif **Padişah** olur; **beraberlikte son geçerli teklifi veren** kazanır | – |
| Eski Padişah **Sadrazam** olur (kaydı silinmez) | `throne.demoteTo: 'sadrazam'` |
| Teklifler bitişte **sıfırlanır**; **normal puanlar asla sıfırlanmaz** | – |
| Teklif gelmezse taht değişmez; mevcut Padişah en yükseği verirse tahtını korur | – |
| Ekranda ilk **5** teklif sahibi ve puanları, geri sayım, son 10 sn nabız, son 3 sn dev 3-2-1, "+5 sn" animasyonu, kazanan sinematiği | `topN: 5` |

Sayfa yenilense de durum kaybolmaz: bitiş saati ve toplam uzatma sayacı sunucuda mutlak olarak tutulur, sayfa güncel durumu sunucudan alır.
**Sunucu yeniden başlarsa:** açık artırma sürüyorsa kalan süre, kullanılmış uzatma hakkı ve kayıtlı tekliflerle **devam eder**; süresi dolmuşsa açılışta **kontrollü kapatılır** (kayıtlı tekliflerden kazanan belirlenir, bir kez);
beklerken zamanı geçmiş bir mücadele ani başlamaz, `restartDelaySec` (30 sn) ertelenir.

### Ücretsiz katılım ve spam koruması
- `!katıl` herkesi ücretsiz Köylü yapar; hediye/takip/paylaşım da otomatik katar (`autoJoin`). Ücretsiz oyuncuların verileri de kalıcıdır.
- Yorumlar (`spam`): kullanıcı başına bekleme (`commentCooldownSec`), aynı metin tekrarı (`duplicateWindowSec`), tüm izleyiciler için dakikalık sınır (`globalPerMinute`).
- Takip ve beğeni **nüfusa değil, kendi sayaçlarına** (`follows`, `likes`) yazılır; paylaşım `shares`. Sayaçlar test panelinde görünür.

### İdempotency (aynı olayın tekrar işlenmesi)
Her TikTok olayı bir kimlik taşır (`common.msgId`; yoksa kullanıcı+hediye+seri+sayaç gibi alanlardan türetilir). Oyun, son `idempotency.maxIds` (2000) kimliği **kayıtla birlikte kalıcı** tutar;
aynı kimlik ikinci kez gelirse (yeniden bağlanma, yeniden başlatma sonrası dahil) işlenmez (`duplicates` sayacı). Kimliği olmayan olaylar tekilleştirilemez.

### Ekran
Sol: **Taht Mücadelesi kartı** (sonraki mücadeleye geri sayım / açık artırmada kalan süre, **Padişah ve Sadrazam**, ilk 5 teklif). Sağ: **Hediye ve Rütbe Rehberi**.
Üstte: hazine, **nüfus**, **ordu**, şehir. Altta: **en yüksek puanlı 5 oyuncu**. Rütbe yükselmeleri banner, parçacık ve karakter animasyonuyla duyurulur.

### Hediye ve Rütbe Rehberi
Sahnenin **sağında** altın işlemeli bir rehber; rütbeleri 3'erli sayfalar halinde döndürür (rütbe · hediye karşılığı · değer · puan) ve altta sürekli **👑 PADİŞAH OL** (açık artırma) ve **🆓 ÜCRETSİZ KATIL** kutuları durur.
Rehber yayın sahnesinin parçasıdır; test paneli ayrı bir katmandır ve yayına girmez.

| Bölüm | Ne yapar |
|---|---|
| **Büyük rehber** | `!hediyeler` yazılınca 15 sn tüm rütbeleri iki sütunda büyük puntoyla açar (rehber gizliyken de). Ortak 20 sn bekleme vardır |
| **Yayın açılışı** | Sahne açılınca ve TikTok yayınına bağlanınca otomatik **10 sn**'lik katılım + hediye bilgilendirmesi. Kapatmak: `intro.enabled: false` veya URL'ye `&intro=0` |
| **Hediye etkisi** | Hediye gelince rehberde: kazanılan **puan**, rütbe yükselmesi / sonraki rütbeye kalan puan, açık artırmada **teklifin ve sıran** |
| **Göster / gizle** | Test panelinde düğme (OBS sahnesine anında yansır, kalıcı) |

**Tek kaynak:** rehberdeki puan eşikleri, hediye karşılıkları, katsayı, açık artırma süreleri ve komut adları `ranks`, `points`, `throne.auction`, `commands`, `giftCatalog` bölümlerinden okunur; ikinci kez yazılmaz.
Yalnızca görünüm ve metinler `game.guide` içindedir. Hediye karşılığı (örn. "≈ 3× Hand Hearts") `giftCatalog` içindeki en uygun hediyeden hesaplanır.

### Test panelinden oynamak (yayına bağlanmadan)
`http://localhost:3000/?panel=1` → senaryoyu seç → **"🎮 Oyun"** bölümü:
1. **Test Kullanıcısı** kutusuna ad yaz (örn. `ahmet`) → **!katıl**; hediyeler seçili kullanıcı adına gider (Rose'dan Universe'e; her düğmede değer ve kazandırdığı puan yazar).
2. **Taht Mücadelesi:** durum satırı + **⚔️ Şimdi Başlat / Şimdi Bitir**. Açık artırmada farklı adlarla hediye gönderip ilk 5'i, uzatmayı ve kazananı izle (süre uzatmayı görmek için son 10 sn'de teklif ver).
3. **Hediye Rehberi:** göster/gizle, büyük rehberi aç, 10 sn tanıtımı oynat; **Hediye Eşikleri** tablosu (yayındakiyle aynı veri) ve **"Eşikleri Canlı Sına"**: her rütbe için geçici oyuncu ekleyip eşik değerinde hediye gönderir, sunucudaki gerçek sonucu okur (✓/✗) ve oyuncuları siler. Açık artırma sürerken çalışmaz; yayın sırasında kullanma.
4. **Yönetici** düğmeleri, **Kullanıcı Listesi** (tıklayınca oyuncu seçilir), **Rütbe Tablosu**, sayaçlar.
5. **Sahneyi Sıfırla** yalnızca görseli yeniden kurar. "⚠ Tüm Oyun Verisini Sıfırla" kalıcı veriyi siler (onay: SIFIRLA).
Panel açıkken sahne alanı daralır; panel yayın sahnesini (ve rehberi) örtmez.

### Güvenlik, kayıt ve test notları
- Oyun verisini değiştiren işlemler (yönetici düğmeleri, sahte olaylar) varsayılan olarak **yalnızca sunucunun çalıştığı bilgisayardan** kabul edilir. Başka cihazdan: `ALLOW_REMOTE_ADMIN=1 npm start`.
- Kayıt klasörü: `data/` (`DATA_DIR=...`). Yazma atomiktir; bozuk dosya silinmez, `.bozuk-...` adıyla kenara alınır; son iyi kayıt `osmanli.json.bak`.
- **Eski kayıt (v1):** ilk açılışta `osmanli.json.v1.bak` olarak yedeklenir ve rütbeler **yeni puan eşiklerine göre yeniden hesaplanır** (eski hediye rütbeleri geçersizdir; Padişah korunur).
- Olaylar **aktif senaryoya** işlenir. OBS URL'sine `&scenario=osmanli` yalnızca *görüntülenen* senaryoyu seçer; önce panelden Osmanlı'yı seç. Halay/Uzay çalışırken Osmanlı oyunu ve açık artırma sayacı **çalışmaz**, kayıt dosyasına dokunulmaz.
- `npm test`: puan eşikleri, hediye değeri/katsayı, combo, idempotency, kalıcılık, yeniden başlatma, açık artırma (zamanlama, teklif, süre uzatma, eşitlik, Sadrazam'a düşme, sıfırlama), spam ve rehberin config ile uyumu.

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
