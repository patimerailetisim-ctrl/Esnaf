# DAY 7 — Alış pazarlığı: kapsam ve uygulama notları (GDD v0.3 FINAL'in uygulamaya çevrilmesi)

Yeni tasarım değildir. GDD v0.3 Bölüm 10.1 Gün 7 satırı ve v0.2 Bölüm 7 (pazarlık), 5.2/5.5 (koz kartı), Bölüm 6 (satıcılar) ile Senaryo 1–4'ün (v0.2 Bölüm 13) uygulamaya çevrilmesidir.

## 1. GDD'deki Gün 7 satırı

Pazarlık motoru (alış tarafı), koz kartı kullanımı, `BuyListing` akışı; testler v0.2 Senaryo 1–4 (UA3: ±10 TL), hakaret/yanlış kart/sabır kuralları, I4 (ret fiyatının altına satmaz), I9 (fiyat düşmez/yükselmez tutarlılığı).

## 2. Day 7'de YAPILANLAR

| # | Sistem | İçerik |
|---|--------|--------|
| 1 | `negotiation_rules.json` | Tüm pazarlık sayıları veridedir: indirim payları, hakaret, yakın teklif, koz kartı, başlangıç güveni, ruh hali/sabır kademeleri. Ayrıştırma + doğrulama, `ContentDatabase.Negotiation`; rapor seviyesi `appraisal_levels.json` ile çapraz denetlenir |
| 2 | `NegotiationEngine` | SAF motor: `Begin`, `Offer(state, offer, card)`, `AcceptFinal`, `WalkAway`. Rastgelelik yok; durum yalnızca `NegotiationState`'te. Başarısız çağrı durumu değiştirmez |
| 3 | `NegotiationSetupFactory` | İlan + satıcı → başlangıç durumu. Sabit iki `NextDouble` (ruh hali, güven); `"negotiation"` akışı (ilan akışı `"market"` etkilenmez) |
| 4 | `TradeService` / `TradeState` | Alış akışı: `Start`, `Offer` (+ koz kartı), `AcceptFinal`, `WalkAway`. Anlaşmada `InventoryService.Acquire`, ilan kaldırma, `NpcState.RecordSoldToPlayer` |
| 5 | Koz kartı kullanımı | Day 6'da üretilen `TrumpCard`lar pazarlıkta oynanır (bir pazarlıkta kart bir kez) |
| 6 | Ekspertiz ücreti | Ürün alınınca bekleyen ücret maliyet tabanına eklenir (Day 6'nın açık maddesi); pazarlık başarısız bitince gider yazılır |
| 7 | Satıcı verisi | `seller.wrongCardPenaltyMultiplier` (varsayılan 1; Dr. Murat 2) — v0.2 6.1 "yanlış kartı iki kat cezalandırır" |
| 8 | API | `StartNegotiation`, `MakeOffer`, `MakeOfferWithCard`, `AcceptFinalPrice`, `WalkAway`, `GetNegotiation`; görünümler güven/sabır/R sayılarını ve kartın doğru/yanlış alarm olduğunu taşımaz |
| 9 | Olaylar | `NegotiationStarted`, `OfferMade`, `ListingPurchased`, `NegotiationEnded` (yalnızca bildirim, K5) |
| 10 | Durum özeti | `GameStateDigest` süren pazarlığı (gizli sayılar dahil, double'lar bit deseniyle) içerir (I6) |

## 3. Day 7'de YAPILMAYANLAR

| GDD öğesi | Sahibi |
|-----------|--------|
| Satış tarafı pazarlık (müşteri rolleri, "rapor göster", `SetPrice`) | Gün 8 |
| Rıza Bey'in ekspertiz sırasındaki güven kuralları (S2 −10, S3 −20, S3'ü %30 reddeder) | Açık madde (ekspertiz + pazarlık etkileşimi; GDD'de sayısal bağlantı yok) |
| Dr. Murat "doğru kartı hemen kabul eder" | Açık madde (formülde karşılığı yok; kart R'yi düşürür) |
| "Güven < 25 ise satıcı ayrılır" (yalnızca Senaryo 5 metninde) | Açık madde |
| Test cihazı satın alma (12.000 TL) | Gün 11 |
| Kayıt/yükleme (`TradeState` dahil) | Gün 9 |

## 4. Motor formülü (UA14) — v0.2 Senaryo 1–4 ile doğrulanmış

GDD v0.2 7.3'ün fiyat kuralı, dört senaryonun ara fiyatlarını (Kemal 28.100 → 26.120 → 24.870 → 24.110 → 23.650, Hatice 6.300 → 6.030, Selin 16.440 → 15.390, Cengiz 11.030 → 10.270) ±10 TL içinde üretecek biçimde şöyle yorumlanır. Bütün sayılar `negotiation_rules.json`'dadır.

Bir turun sırası:
0. **Koz kartı** (varsa): doğru kart → `R' = min(R, max(taban, R − ProblemDeğeri × KanıtGücü × (ikna + rapor payı)))`, güven +5. `taban = 0,65 × gerçek değer`; taban R'yi ASLA yükseltmez. S3 raporundan gelen kartın ikna payı +0,20. Yanlış alarm → güven −10 ve sabır −1 (satıcı çarpanıyla çarpılır: Murat ×2), R değişmez.
1. **Hakaret**: teklif `< 0,90 × R` → hakaret. Gün ≥ 5'te güven −15 ve sabır ek −1; Gün 5'ten önce yalnızca uyarıdır (`insult.penaltyFromDay`).
2. **Satıcı indirimi**: `t = 0,20 + 0,30 × (güven/100) + 0,15 × aciliyet`; `yeniFiyat = max(R, F − t × (F − R))`. Gösterilen fiyat `max(10 TL'ye yuvarla(yeniFiyat), ceil10(R))` — **I4: hiçbir zaman R'nin altına inmez**.
3. **Anlaşma**: teklif ≥ gösterilen fiyat → anlaşma, **fiyat teklife değil satıcının o turdaki fiyatınadır**.
4. Aksi halde: teklif `≥ 0,97 × R` ise güven +5 ("yakın teklif"); sabır −1; sabır 0 → **son teklif** ("Son fiyatım X, al ya da git") — yalnızca kabul ya da masadan kalkma kalır.

İç değerler (fiyat, R) `double`'dır; oyuncuya 10 TL'ye yuvarlı `Money` gösterilir. Ürünün kayda giren fiyatları yine `long` TL ve 10 TL katıdır.

**Kesin değerlerin kaynağı (UA3-c).** `Assets/_Project/Tests/Fixtures/negotiation_golden.json` 13 senaryonun her turunu (fiyat, güven, sabır, R, aşama, anlaşma) içerir; bağımsız bir Python referans modelinden üretilmiştir ve testler C# motoruyla **tam eşitlik** ister. Ayrıca dört v0.2 senaryosu, dokümandaki yuvarlanmış sayılarla ±10 TL karşılaştırılır.

## 5. Belirsizlik giderme (yeni özellik değil; UA14–UA19)

**UA14 — Formül yorumu**: yukarıdaki bölüm. GDD "t = …" katsayılarını verir, yuvarlama ve teklif→fiyat ilişkisini vermez; senaryolarla uyumlu tek sadelik seçildi.

**UA15 — Başlangıç durumu (v0.2 7.2)**: `R = ilanın ret fiyatı × (1 ± %3)` ve başlangıç güveni `50 ± 10` (rastgele; `start` bölümü). **Rehberli Gün 1 ilanında** ruh hali ve güven oynamaz (R = 4.750, güven 50; "kolay mod"); rastgele çekimler yine yapılır (akış tüketimi ilan türünden bağımsız). `R` istenen fiyatı geçemez.

**UA16 — Kart uygulama sırası**: kart, hakaret denetiminden ÖNCE uygulanır (oyuncu kartı kullanarak düşen R'ye göre teklif verir). Doğru kartın etkisi kanıt gücüyle ölçeklenir (S1 0,4 / S2 0,7 / S3 1,0).

**UA17 — Başarısız pazarlık**: masadan kalkınca ilan kalkar ve ürün silinir (aynı ilana bedava yeniden deneme sömürüsü olmaz); bekleyen ekspertiz ücreti gider yazılır. Sabır bitince "son teklif" aşamasında kabul edilmezse aynı sonuç.

**UA18 — Tek pazarlık, gün bitirme**: aynı anda tek pazarlık vardır; sürerken `EndDay` `negotiation.in_progress` ile reddedilir (ilanın gün sonunda kaybolması pazarlık ortasında tutarsızlık yaratırdı).

**UA19 — Oyuncuya görünenler**: ruh hali (güvenden) ve sabır kademesi (düşük/orta/yüksek); eşikler veridedir (`view`) ve GDD'de sayısı yoktur (varsayım). Koz kartının doğru mu yanlış alarm mı olduğu görünümde ve olaylarda YOKTUR; oyuncu bunu ruh halinden sezer.

**Ön koşullar**: pazarlığı başlatmak için raf dolu olmamalı (`inventory.full`); anlaşmada nakit yetmezse `cash.insufficient` döner, pazarlık açık kalır ve durum değişmez (motor önce kopya üzerinde çalışır).

## 6. Yapısal kararlar

- Domain'de, Unity bağımsız; motorda hiç `IRandom` yok. Rastgelelik yalnızca `NegotiationSetupFactory`'de, `"negotiation"` akışında.
- Olay sırası (anlaşma): `NegotiationStarted` → (`InventoryService`'in) `ItemAddedToShelf` → `OfferMade` → `ListingPurchased` → `NegotiationEnded`. Başarısız: `NegotiationStarted` → `OfferMade`… → `NegotiationEnded(Failed)`.
- `NpcSellerRole.WrongCardPenaltyMultiplier` isteğe bağlı kurucu parametresidir (varsayılan 1); mevcut testler bozulmaz.
- `ContentDatabase.Load` artık `negotiation_rules.json` dosyasını zorunlu tutar.
