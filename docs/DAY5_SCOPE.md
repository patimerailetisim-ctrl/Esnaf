# DAY 5 — Kapsam ve uygulama notları (GDD v0.3 FINAL'in uygulamaya çevrilmesi)

Bu belge **yeni tasarım değildir**. GDD v0.3 (Bölüm 10.1, Gün 5 satırı) ve v0.2'nin ilgili bölümlerinin (6, 10.4, 11, 12) hangi kısmının Day 5'te yapıldığını, hangi kısmının sonraki günlere ait olduğu için yapılmadığını ve GDD'nin belirsiz bıraktığı yerlerde hangi yorumun seçildiğini kaydeder.

## 1. GDD'deki Gün 5 satırı (aynen)

> **NPC + pazar + gün döngüsü** — `NPCDefinition.cs`, `NpcStateStore.cs`, `npc_profiles.json`, `MarketState.cs`, `ListingGenerator.cs`, `TimeState.cs`, `IDayEndStep.cs` + adımlar, `GameSession.cs`, `IGameApi.cs` (ilk komutlar)
> Test: 1.000 gün: ≥2 fırsat, ≥1 tuzak (Gün 5+), jackpot kotası; gün sonu sırası; I6 (determinizm)
> Çalışan özellik: "Günü Bitir" ilanları yeniler, gider düşer (komutla)

## 2. Day 5'te YAPILANLAR

| # | Sistem | İçerik |
|---|--------|--------|
| 1 | NPC tanımları | `npc_profiles.json` (10 NPC, satıcı + müşteri sayıları, v0.2 6.1/6.2), ayrıştırma, doğrulama, `NpcDefinition` |
| 2 | NPC durumu | `NpcStateStore` (görüşme sayısı; oyuncuya sattığı / oyuncudan aldığı ürün kimlikleri) |
| 3 | Pazar sabitleri | `economy_constants.json` içine `market` bölümü (GDD 4.6: kotalar P1–P5 bu dosyadadır) |
| 4 | Pazar | `MarketListing`, `MarketState`, `ListingGenerator` (ilan sayısı, ömür 2–4, segment ağırlığı, satıcı seçimi, P1–P5 kotaları, rehberli Gün 1 ilanı) |
| 5 | Zaman | `TimeState`, `IDayEndStep`, `DayEndPipeline` (sıra GDD 3.4 numaralarıyla sabit) |
| 6 | Gün sonu adımları | Yalnızca sahibi Day 5'te var olan adımlar: **2** günlük gider, **4** ilan ömrü/kaldırma, **7** yeni gün + yeni ilanlar |
| 7 | Oyun oturumu | `GameSession` (composition root; singleton yok), `IGameApi` + `GameApi` |
| 8 | API | Komut: `EndDay()`. Sorgular: `GetDay`, `GetCash`, `GetListings` (gizli bilgi sızdırmaz), `GetInventory`, `GetTodaySummary`, `GetStateDigest`. Biten günün özeti `EndDay` raporundadır (geçmiş günün stok/servetini yeniden kurmak yanıltıcı olurdu) |
| 9 | Determinizm | `GameStateDigest` (I6: aynı tohum + aynı komutlar → aynı özet) |
| 10 | Olaylar | `DayEnded`, `DayStarted`, `ListingsGenerated`, `ListingExpired` (yalnızca bildirim, K5) |

## 3. Day 5'te YAPILMAYANLAR (ve nedeni)

| GDD öğesi | Sahibi olan gün |
|-----------|-----------------|
| Gün sonu adım 1 (kaçan müşteriler), müşteri üretimi, satış baskısı, talep (adım 5) | Gün 8 |
| Gün sonu adım 3 (stok bekleme değer kaybı) | GDD planında ayrı bir gün atanmamış → **açık madde** (raporda) |
| Gün sonu adım 6 (XP/görev), adım 8 (otomatik kayıt) | Gün 11 / Gün 9 |
| Ekspertiz (`AppraisalService`, koz kartı, `KnowledgeState`) | Gün 6 |
| Pazarlık motoru, `BuyListing`, `SetPrice`, `MakeOffer` komutları | Gün 7 |
| Kayıt/yükleme (`Capture/Restore`, JSON kayıt) | Gün 9 |
| Tamir, ilerleme, görevler, öğretici, UI | Gün 11–13 |

`IGameApi`'ye **alış komutu eklenmedi**: GDD'de alış pazarlıkla (Gün 7) yapılır; sabit fiyatlı alış tasarımda yoktur.

## 4. Belirsizlik giderme (yeni özellik değil; UA4–UA8)

Bunlar GDD'nin sayılarının makineye çevrilmesinde gereken yorumlardır. Hepsi veri dosyasındadır; kodda sabit değildir.

**UA4 — Satıcının Gün'e göre açılması.** v0.2 Bölüm 12'nin "yeni kişilik" sütunu satıcı havuzunu belirler: Gün 1 Ayşe; Gün 2 Kemal, Selin, Ozan; Gün 3 Hatice; Gün 4 Rıza, Dr. Murat; Gün 5 Cengiz; Gün 6 Berk, Nermin. (`seller.availableFromDay`.)

**UA5 — Satıcının inandığı değer ve istenen fiyat.** v0.2 6.1: istenen fiyat = satıcının **inandığı** değer × çarpan; ret fiyatı R = R oranı × inandığı değer.
- Kusurları saklamayan satıcı: inandığı değer = gerçek değer × (1 + sapma + gürültü). Toplam hata `valueSigma`'yı aşmaz: gürültü, `valueSigma − |valueBias|` genişliğinde düzgün dağılımdır. Hatice Teyze için `valueBias = −0,28` (v0.2: "genelde ~%28 eksik bilir"), `valueSigma = 0,30`.
- Kusurları saklayan satıcı (`concealChance`, ör. Cengiz %60): gizli kusurlar (`hiddenDefects`: ekran "değişmiş", kamera lekeli/arızalı) yokmuş gibi hesaplanan değere inanır (v0.2 Senaryo 4: 8.846 ÷ (0,88 × 0,82)).
- Ayşe Hanım dürüsttür: kusur saklamaz.
- İstenen fiyat, senaryolardaki tüm örneklerle uyumlu olacak şekilde **50 TL'ye** yuvarlanır (30.420→30.400, 18.375→18.400, 6.765→6.750, 12.505→12.500, 5.808→5.800). Bu, 10 TL kuralıyla çelişmez.

**UA6 — Fırsat / jackpot / tuzak tanımları (P1, P3, P5).**
- *Makul fırsat*: gerçek ret fiyatı R ≤ 0,90 × gerçek değer (P1'in metni). Bir ilan hem tuzak hem fırsat sayılmaz: tuzak olan ilan fırsat kotasına girmez.
- *Jackpot*: ret oranı < 0,85 olan satıcının ilanı (P3'ün metni).
- *Tuzak*: satıcı kusuru **sakladı** ve inandığı değer ≥ 1,15 × gerçek değer (v0.2 Senaryo 4'te oran ≈ 1,39). Yalnızca `concealChance > 0` olan satıcılar tuzak üretir.
- Kotalar (her gün): Gün 1'de rehberli ilan tek fırsattır; Gün 2+ ≥ 2 fırsat; Gün 5+ ≥ 1 tuzak, Gün 6+ ≤ 2 tuzak; jackpot ≤ 1 (Gün 6+ ≤ 2).

**UA7 — Gün 1 rehberli ilan (v0.2 Gün 1).** Yıldız Y5 64 GB, 24 ay, satıcı Ayşe Hanım; gerçek değer 5.280, istenen 5.800, ret fiyatı 4.750 ("kolay mod"). Nitelikler bu sayıları tam veren değerlerdir (pil ≥ 90, kasa 100, ekran orijinal, kamera sağlam, kutu/fatura yok); v0.2'deki "kutusu var" ipucu metni Gün 12 (UI/öğretici) konusudur. Diğer 2 ilan dolgudur (v0.2 11.1: Gün 1: 3 ilan).

**UA8 — Gün 1–4 satıcı ağırlığı (P2).** `learningFriendly` (Aceleci, Bilgisiz, Dürüst = Selin, Hatice, Ayşe) satıcılar, ilk 4 günde ilanların %45'ini alır; kalan %55 diğer satıcılara dağılır (bir grup boşsa tüm ilanlar diğer gruptan gelir).

## 5. Yapısal kararlar (mevcut mimariye uyum)

- Tüm yeni kod `Esnaf.Domain` içindedir (Unity bağımsız, C# 9, `record`/`init` yok). Singleton yok; bağımlılıklar constructor'dan.
- Rastgelelik yalnızca `RngStreams` akışlarındandır; ilan üretimi `"market"` akışını kullanır. `NewDayStep` akışı her çalışmada `RngStreams.Get` ile alır (Gün 9'da `Restore` akışları yenileyecek; eski referans tutulmaz).
- Para `Money` (long TL). `double` yalnızca satıcı değer hesabında; sınırda `Money.FromDoubleRoundedTo10` / 50'ye yuvarlama.
- Süresi dolan ilanın ürün örneği, hâlâ **Pazar** konumundaysa `InstanceStore`'dan silinir (sahipsiz Pazar örneği kalmaz; I8 ve kayıt boyutu için). Bunun için `InstanceStore.Remove` eklendi.
- Yeni ilanlar yalnızca `deprecated` olmayan ve o gün açık modellerden üretilir.
- Gün sonu adımları sırayla uygulanır. 7. adım (ilan üretimi) yalnızca **bozuk içerikte** (doğrulayıcıdan geçtiği hâlde kota sağlanamıyorsa) başarısız olabilir; bu durumda 2. ve 4. adımların etkisi geri alınmaz ve oturum atılmalıdır. Başarısız üretim, ilan deposunu, kimlik sayaçlarını ve günü değiştirmez.
- `MarketListing`'in gizli alanları (R, inandığı değer, fırsat/tuzak/jackpot işareti) yalnızca Domain içindedir. `IGameApi.GetListings` `ListingView` döndürür; bir yansıma testi bu tipte gizli alan olmadığını korur.
- `ContentDatabase.Load` artık `npc_profiles.json` dosyasını ve `economy_constants.json` içindeki `market` bölümünü de zorunlu tutar; NPC kimlikleri (`npc.*`) `content_id_manifest.json`'a eklenir (I10).
