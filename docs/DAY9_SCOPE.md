# DAY 9 — Save/Load: kapsam ve uygulama notları (GDD v0.3 FINAL'in uygulamaya çevrilmesi)

Yeni tasarım değildir. GDD v0.3 Bölüm 6 (Save/Load sistemi), 10.1 Gün 9 satırı, 4.3/4.5 (kayda giren durumlar, kimlik ve rastgelelik kuralları), 9.4 (Save test satırları), UA2 (kayıt `IGameApi`'den geçmez) ve T11/T12/T17'nin uygulamaya çevrilmesidir.

## 0. Yol haritası ile görev metni arasındaki fark (açık not)

GDD v0.3 10.1'e göre **Gün 9 = Save/Load**'dur. Görev metnindeki "stok bekleme değer kaybı", "talebin alış tarafına bağlanması", "eksik gün sonu geçişleri" maddeleri GDD'de Gün 9'a atanmamıştır:

| Madde | GDD'deki durum | Bu günkü karar |
|-------|----------------|----------------|
| Stok bekleme değer kaybı (gün sonu adım 3) | v0.2 10.4 / v0.3 3.4'te var; plan tablosunda **hiçbir güne atanmamış** | Yapılmadı, **açık madde** olarak kalıyor (GDD dışı bir güne zorla atanmadı) |
| Talebin alış tarafına bağlanması | Gün 10 denge ayarı (GDD 10.1 Gün 10) | Yapılmadı (Day 8'de bilinçli bırakıldı) |
| Gün sonu/ertesi gün durum geçişleri | Gün sonu **adım 8 "otomatik kayıt isteği"** Save'in sahibidir | **Yapıldı** (`AutoSaveStep`) |
| Alış → raf → etiket → satış → yeni gün zincirinin kayıt/yükleme ile kesintisiz sürmesi | Gün 9 "Çalışan özellik" | **Yapıldı** ve testlerle doğrulandı |

GDD dışı yeni mekanik eklenmedi.

## 1. GDD'deki Gün 9 satırı (aynen)

> **Save/Load** — `SaveData` DTO'ları, `SaveSerializer.cs`, `SaveStorage.cs` (`ISaveStorage`), `MigrationRunner.cs`, `Backup`, `CorruptHandling`, `Fixtures/save_v1.json`
> Test: Gidiş-dönüş I7; RNG devamı; bozuk→bak1; sürüm reddi; atomiklik
> Çalışan özellik: Durum kaydedilir/yüklenir, yeniden başlayınca aynı oyun devam eder

## 2. Day 9'da YAPILANLAR

| # | Sistem | İçerik |
|---|--------|--------|
| 1 | `GameSnapshot` DTO'ları (Domain) | Kayda giren bütün durumun düz veri karşılığı: zaman, rastgele akışlar, kimlik sayaçları, defter + bekleyen ekspertiz ücretleri, talep, raf, ekipman, pazar, ürün örnekleri, NPC durumları, bilgi (ekspertiz sonuçları), müşteri havuzu, **süren alış ve satış pazarlığı** (T17) |
| 2 | `GameSession.Capture()` / `GameSession.Restore(...)` | UA2'deki dar kayıt yüzeyi. `Capture` durumu değiştirmez; `Restore` yeni bir oturum kurar (Gün 1 açılışı yapılmaz), defteri yeniden oynatarak (append kuralları + bakiye karşılaştırması) doğrular ve bütün başvuruları denetler; geçersizse `save.invalid` döner |
| 3 | `Esnaf.Persistence` | `SaveSerializer` (başlık + yük, SHA-256 sağlama, önizleme), `IMigration` + `MigrationRunner`, `ISaveStorage` + `FileSaveStorage`, `SaveService` (atomik yazma, 2 yedek, bozuk kurtarma, önizleme, silme), `AutoSaver` |
| 4 | Gün sonu adım 8 | `AutoSaveStep` (sıra 8) `AutoSaveRequested` olayı yayınlar; `AutoSaver` bunu ve alım/satım/ekspertiz olaylarını dinleyip kaydeder (GDD 6.4). Kayıt hatası gün sonunu bozmaz |
| 5 | Olaylar | `GameSaved`, `GameLoaded` (Persistence), `AutoSaveRequested` (Domain) |
| 6 | Fikstürler | `Tests/Fixtures/save_v1.json` (gerçek oturumdan üretilmiş, süren pazarlık dahil) ve içerdiği durumun **durum özeti sabiti**; sahte v0 fikstürü ve v99 dosyası kodla üretilir |
| 7 | Test düzeneği | `InMemorySaveStorage` (kesinti/bozulma enjeksiyonu), bağımsız SHA-256 referans değeri |

## 3. Day 9'da YAPILMAYANLAR

| GDD öğesi | Neden / sahibi |
|-----------|----------------|
| `player` / `progress` bölümleri (XP, seviye, itibar, görevler, öğretici durumu) | Sistemler Gün 11'de; sürüm 1 yükü bu bölümlere sahip değil (Gün 11 isteğe bağlı alan olarak ekler) |
| Önizlemede `level` | İlerleme sistemi Gün 11 (önizleme: gün, nakit, servet) |
| `GameBootstrap` / ana menü "Devam et" akışı, "Yeni oyun" onayı, uygulama arka plana alınırken kayıt | App/UI (Gün 12–14); Persistence API'si hazırdır |
| `settings.json` | Ayarlar ekranı (Gün 14); kayıttan bağımsız dosya |
| Çoklu slot, şifreleme, bulut | MVP dışı (6.9) |

## 4. Kayıt biçimi (UA29) — GDD 6.2'ye uygun

```
{ "header": { "format": "esnaf-save", "saveVersion": 1, "appVersion", "contentSchemaVersion": 1,
              "createdAtUtc", "savedAtUtc", "playTimeSeconds", "checksum": "sha256:…",
              "preview": { "day", "cash", "wealth" } },
  "payload": { "time", "rng", "ids", "economy": {…, "demand"}, "inventory", "business", "market",
               "instances", "npcStates", "knowledge", "customers", "activeNegotiation", "activeSale" } }
```

- Kayıt yalnızca ID + sayı tutar (T12): tanım alanları kopyalanmaz; gerçek değer kaydedilmez (formülle hesaplanır). Kaydedilen her `double` (gizli R, müşteri çekimleri, talep endeksleri, kanıt gücü) gidiş-dönüşte **bit düzeyinde** aynıdır (Newtonsoft "R" biçimi; testle doğrulanır).
- `ulong` değerler (ana tohum, PCG durum/artım, ekspertiz tohumu) JSON sayısının kesinlik sınırı nedeniyle **metin** olarak yazılır.
- Nitelikler GDD 4.4 örneğindeki gibi düz JSON değerleridir (`"battery": 78`, `"screen": "original"`, `"box": false`).
- Sağlama: `payload` düğümünün **kanonik** (girintisiz) metninin SHA-256 özeti; dosya girintili yazılsa da aynı özet çıkar. Bozulmayı fark etmek içindir, hile önleme değil.
- Yazarken sıra sabittir (alanlar sabit sırada, listeler oyundaki sırayla); aynı durum her zaman aynı metni verir.

## 5. Yazma ve okuma kuralları (GDD 6.3, 6.5, 6.6)

**Atomik yazma.** `Capture` → JSON → `slot0.tmp` yaz/boşalt → `tmp`'yi **geri oku ve doğrula** (ayrıştır + sağlama + sürüm + başlık) → `bak1 → bak2` kopyala, `slot0 → bak1` kopyala, `tmp → slot0` **atomik değiştir**. Yedekleme adımları **kopyadır** (taşıma değil): hangi adım başarısız olursa olsun `slot0` sağlam kalır (GDD 6.3 adım 5). Hata olursa `tmp` silinmeye çalışılır, `Result` hata kodu döner, oyun devam eder.

**Yükleme.** `slot0.json` yoksa ve yedek de yoksa `NoSave`. `slot0` varsa: ayrıştırma + biçim + sağlama + sürüm + (gerekirse) migrasyon + `Restore` doğrulaması; herhangi biri başarısızsa `slot0`, `slot0.corrupt-YYYYMMDD-HHMMSS.json` olarak **yeniden adlandırılır (silinmez)**, sonra `bak1`, sonra `bak2` denenir; bulunursa `LoadedFromBackup` (gün bilgisiyle), hiçbiri yoksa `Corrupt` (bozuk dosyalar saklanır). `slot0` yoksa ama yedek varsa yedekler sırayla denenir.
**İleri uyumsuzluk:** `saveVersion` desteklenenden büyükse `NewerVersion` döner; dosyalar **dokunulmaz** (bozuk sayılmaz, yeniden adlandırılmaz).
**Migrasyon:** `IMigration { From; To; Apply(JObject) }`; `MigrationRunner` zinciri (`From` ardışık, boşluk yok) sırayla uygular; zincirde eksik adım "desteklenmeyen sürüm"dür. Üretimde yalnızca sürüm 1 vardır, zincir boştur; çerçeve sahte v0→v1 migrasyonuyla test edilir.

## 6. Belirsizlik giderme (UA29–UA33)

**UA29 — Kayıt biçimi:** yukarıdaki bölüm; GDD 6.2'nin `player/progress` dışındaki bütün bölümleri, Day 5–8'de eklenen durumlarla genişletilmiştir.

**UA30 — `Restore` bir oturum FABRİKASIDIR:** UA2'nin `Restore(data)` yüzeyi, hazır bir oturumun üzerine yazmak yerine yeni oturum kurar (Gün 1 ilanları/defter açılışı yapılmaz). Böylece "yarı yüklenmiş" oturum olmaz: başarısızlıkta hiçbir oturum üretilmez. Rastgele akışlar `RngStreams.Restore` ile yenilenir; servisler akış nesnesini saklamaz (her kullanımda `Get` ile alır).

**UA31 — Doğrulama = bozulma:** defter yeniden oynatıldığında kural ihlali ya da bakiye/kimlik uyuşmazlığı, kayıtta tekrar eden/bulunmayan kimlik, rafta olmayan ürüne başvuru, kapasite aşımı gibi tutarsızlıklar `save.invalid` döner ve dosya bozuk sayılır (yedeğe düşülür).

**UA32 — Bilinmeyen ürün kimliği (GDD 6.7):** içerikte bulunmayan `definitionId` için: rafta duran ürün **otomatik iade** edilir (alış fiyatı kadar nakit + defter satırı `content_refund` = "İçerik değişikliği iadesi", ürün silinir, ekspertiz sonuçları/bekleyen ücretler temizlenir); pazardaki ilan ve ürün sessizce kaldırılır; satılmış ürün kaydı kalır. Olaylar `LoadWarnings` listesine yazılır (oyuncuya hata gösterilmez). `content_refund` yeni bir defter türüdür (`transaction_types.json`, giriş, kâr etkisi yok); çekirdek 8 tür değişmedi.

**UA33 — Otomatik kayıt tetikleyicileri:** gün sonu (adım 8), alım anlaşması (`ListingPurchased`), satım (`ItemSold`), ekspertiz (`AppraisalCompleted`). Yatırım (raf/ekipman) ve ana menüye dönüş/uygulama kapanışı, ilgili sistem/UI günlerinde aynı `SaveService.Save` ile bağlanır (yatırım komutu henüz yok).

## 7. Yapısal kararlar

- `Esnaf.Persistence` yalnızca Core + Domain'e bağlıdır, Unity'yi bilmez; gerçek saat yalnızca `ISaveClock` üzerinden, yalnızca kayıt üst verisi ve `.corrupt` dosya adı için kullanılır (K8).
- `IGameApi`'ye kayıt komutu **eklenmedi** (UA2); `SaveService` doğrudan `GameSession.Capture/Restore` kullanır.
- Domain'deki `GameSnapshot` tipleri düz veri sınıflarıdır (`record`/`init` yok); JSON dönüşümü Persistence'tadır.
- `GameSession` kurucusu iki yola ayrıldı (yeni oyun / geri yükleme); yeni oyun davranışı değişmedi.
- Gün sonu adım listesi artık `missed_customers, daily_expense, listing_expiry, demand_update, new_day, auto_save` (sıra 1, 2, 4, 5, 7, 8); mevcut testlerdeki tam liste denetimleri bu bilinçli değişime göre güncellendi.
- Test derlemesi (`Esnaf.Domain.Tests`) `Esnaf.Persistence`'a başvurur (GDD ayrı `Persistence.Tests` öngörür; tek test derlemesi korundu).

## Mutation sonucu (Gün 9)

Snapshot/Persistence/AutoSaver/AutoSaveStep aralıklarında gerçek survivor yok. Belgelenmiş eşdeğer mutantlar:
- `SnapshotRestore` ~L179: gözlemlenebilir davranışı değiştirmeyen eşdeğer mutant.
- `SaveSerializer` L55: `StringBuilder` başlangıç kapasitesi; yalnızca bellek ayırma, çıktı aynı.
- `FileSaveStorage` `Flush(true)`→`Flush(false)`: OS düzeyinde diske yazma zorlaması, test ortamında gözlemlenemez (davranışsal olarak eşdeğer).
Manuel mutasyon: `AutoSaveStep` farklı EventBus'a bağlandı → 6 test düştü; `AutoSaveRequested(day+1)` → 2 test düştü.

## Düzeltme 1 (YERİNE GEÇİLDİ → Düzeltme 2): çalışma zamanından bağımsız kanonik JSON, en kısa metin

**Sorun:** Unity'de `save_v1.json` fikstürü `save.checksum` ile reddedildi. Sağlama, yükün yeniden üretilen kanonik metnine bağlıdır ve Newtonsoft double'ı çalışma zamanının `ToString("R")`'ı ile yazar: eski Mono "R" 15 hane dener, olmazsa 17 hane yazar (`0.51208716694934375`); modern .NET en kısa gidiş-dönüş metni yazar (`0.5120871669493438`). Fikstürdeki 35 farklı ondalıklı sayının 18'i farklı metne dönüyordu → farklı SHA-256. Diğer kayıt testleri aynı çalışma zamanında yazıp okuduğu için geçiyordu.

**Çözüm:** `Persistence/CanonicalJson` — sağlama ve dosya metni artık kendi yazıcımızla üretilir (`SaveSerializer.Serialize` ve `Parse`). Sağlama doğrulaması aynen duruyor.
- double: tam değerden (BigInteger) hesaplanan EN KISA (1..17 hane) ondalık metin; adayın aralık içinde kalması tam sayıyla doğrulanır (ayrıştırıcıya/çalışma zamanına bağlı değil); tam yarıda çifte yuvarlanır. Kültür kullanılmaz. Sayılar JSON'da SAYI olarak kalır; long/ulong double'a çevrilmez.
- Biçim modern .NET "R" + Newtonsoft ile bire bir aynıdır (ondalık üs ≥17 veya ≤-5 → `d.dddE±XX`, aksi halde ondalık; tam sayı değerli double'a `.0`). 3.000.000 rastgele double (rastgele bit desenleri, tam yarılar dahil) .NET "R" ile karşılaştırıldı: 0 fark.
- Satır sonu her zaman `\n`, girinti iki boşluk, metinler JSON.NET sabit kaçışıyla.

**Geriye uyumluluk:** Kayıt sürümü v1 olarak KALDI; migrasyon/sürüm değişmedi. Fikstür (`save_v1.json`) DEĞİŞMEDİ: yeni kanonik yazıcı .NET ile yazılmış dosyanın sağlamasını aynen üretir. Bir sayının başka yazımı (örn. 17 haneli) aynı kanonik metni verdiği için Mono ile yazılmış bir v1 dosyası da okunur.

**Mutation (CanonicalJson, SaveSerializer):** gerçek survivor yok. Eşdeğer mutantlar: `shift >= 0`→`> 0` ve `p >= 0`→`> 0`, `candNum/candDen` için `p <= 0`/`p >= 0` (p=0'da 10^0=1, sonuç aynı); `diff.Sign >= 0`→`> 0` (diff=0'da mutlak fark 0, sınır kullanılmaz); `exp10 < 0`→`<= 0` (bilimsel gösterim yalnızca üs ≥17 veya ≤-5'te, üs 0 ulaşılamaz); `expField > 1`→`>= 1` (yalnızca DBL_MIN etkilenir, çıktı aynı: altın test); `Math.Floor/Ceiling` ve `CompareToPowerOfTen` sınır mutantları (tahmin sonrası iki düzeltme döngüsü ve taşma yolu aynı k/basamakları üretir). Gereksiz `|| diff.IsZero` koşulu (cmp<0 zaten kapsar) koddan kaldırıldı.


## Düzeltme 2: kanonik double = 17 anlamlı hane; -0.0 → +0.0 (Unity Mono ölçümleri)

**Ölçümler (Unity Mono 6.13, sabit değerler, bit deseniyle doğrulandı):**
- Düzeltme 1'in en kısa metni Unity'de 1 ULP yanlış parse edilebiliyor (`-7.756553093017971E-171`). Newtonsoft aynı runtime `double.Parse`'ını kullandığı için aynı sonucu veriyor. 73 sabit değerde en kısa metin 71/73 tam gidiş-dönüş, 17 haneli metin 72/73.
- Tek 17 haneli başarısızlık `-0.0`: Unity Mono `-0.0`, `-0`, `-0e0`, `-0.0e0`, `-0.0000000000000000e0` metinlerinin HEPSİNİ +0.0 (bit 0) parse ediyor (`double.Parse` ve Newtonsoft). İşaretli sıfır JSON sayısı olarak korunamıyor.

**Karar (canonical JSON):**
1. Sonlu sıfırdan farklı her double tam değerinden (BigInteger) hesaplanan, yarıya-çift yuvarlanmış 17 anlamlı haneli bilimsel metinle yazılır: `d.dddddddddddddddde<üs>` (örn. `0.1` → `1.0000000000000001e-1`, `1.0` → `1.0000000000000000e0`). Sayılar JSON'da sayı olarak kalır. Newtonsoft parse yolu değişmedi; özel parser yazılmadı.
2. Sıfır her zaman `0.0` yazılır: `-0.0` bilinçli olarak `+0.0`'a normalize edilir; canonical çıktı negatif sıfır ÜRETMEZ. Oyun durumunda -0.0 ile +0.0 arasında anlamlı fark yoktur ve bu varsayım testle korunur (`TheSnapshot_NeverHoldsANegativeZero…`: 6 tohumlu oynanmış oturumun snapshot'ında -0.0 yok). `GameStateDigest` double'ları bit deseniyle yazar; durum -0.0 taşısaydı kayıt/yükleme digest'i değiştirirdi, bu yüzden test bu varsayımı sabitler.
3. NaN/Infinity JSON.NET sabitleriyle yazılır (oyun durumu sonlu sayı üretir).
4. Kayıt biçimi sürümü v1 olarak KALDI; migrasyon/sürüm değişmedi.

**Geriye uyumluluk (bilinçli kırılma):** Düzeltme 1'in en kısa kanonik metniyle hesaplanmış sağlamaya sahip v1 dosyaları (ör. önceki `save_v1.json` fikstürü) artık sağlama (checksum) nedeniyle `save.checksum` ile REDDEDİLİR. Save v1 hiçbir zaman yayınlanmadı (yalnızca geliştirme ve test dosyaları vardı), bu yüzden gerçek kullanıcı kaydı etkilenmez. Fikstür yeniden üretilir: state'teki tüm double bit desenleri ve `GameStateDigest` (`de4eb924fea4491c`) aynı kalır; yalnızca sayı yazımı ve buna bağlı sağlama değişir.

**Test tarafı notu:** `SaveSerializerTests` içinde sağlamayı `payload.ToString(Formatting.None)` (Newtonsoft'un çalışma zamanına bağlı sayı yazımı) ile hesaplayan üç satır, canonical yazıcıya (`CanonicalJson.Write`) çevrildi; Unity'deki 4 testin (iki Parse yük hatası testi ve iki migrasyon testi) başarısızlığının nedeni buydu.
