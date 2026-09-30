# DAY 8 — Satış, müşteri, fiyat baskısı: kapsam ve uygulama notları (GDD v0.3 FINAL'in uygulamaya çevrilmesi)

Yeni tasarım değildir. GDD v0.3 Bölüm 10.1 Gün 8 satırı; v0.2 Bölüm 6.2 (müşteri rolü), 7.2 ("müşteri rolünde aynı motor ters yönde"), 7.5 (rapor göster), 8 (Senaryo 2 ve 3 satış tabloları), 10.2/10.4 (talep, satış baskısı, anti-arbitraj), 11.2 (müşteri üretimi) ve v0.3 P4 / 3.4 (gün sonu adım 1 ve 5) / 9.4 test kriterlerinin uygulamaya çevrilmesidir.

## 1. GDD'deki Gün 8 satırı (aynen)

> **Satış, müşteri, fiyat baskısı** — `CustomerGenerator.cs`, `SellFlow`, satış modu pazarlık, `DemandModel.cs`, `economy_constants.json`, anti-arbitraj
> Test: Hatice satışı 8.920, Berk 19.920; müşteri ≤5; baskı 0,985ⁿ; NPC geri satma reddi
> Çalışan özellik: Al → rafa koy → sat → kâr, komutlarla tamamen çalışır

## 2. Day 8'de YAPILANLAR

| # | Sistem | İçerik |
|---|--------|--------|
| 1 | `SaleEngine` | Satış pazarlığı motoru (alış motorunun ters yönü): `Begin`, `Ask`, `ApplyReport`, `AcceptFinal`, `Leave`. SAF hesap, rastgelelik yok. Kesin değerler bağımsız Python referansından üretilen `sale_golden.json` (13 senaryo) ile doğrulanır; Senaryo 2 (Hatice, 8.920) ve 3 (Berk, 19.920) ±10 TL içinde |
| 2 | `CustomerService` (Business) | Günlük müşteri havuzu, gelen müşteri sayısı, ürün ilgisi, müşteri Max'ı (M), açılış teklifi, gün sonu "kaçan müşteri" |
| 3 | `SellService` / `TradeState.CurrentSale` | Satış akışı: `Start`, `Ask`, `ShowReport`, `AcceptFinal`, `Leave`. Anlaşmada `InventoryService.Sell` (defter + maliyet tabanı), NPC hafızası, talep baskısı |
| 4 | `DemandModel` / `DemandState` | Talep endeksi (Gün 5+ canlı) ve satış baskısı `0,985ⁿ` (son 5 gün, taban 0,90) |
| 5 | Etiket | `ProductInstance.ListPrice`, `InventoryService.SetPrice`, `ItemPriced` olayı; etiketsiz ürüne müşteri ilgilenmez |
| 6 | Gün sonu adımları | **1** `missed_customers` (kaçan müşteriler), **5** `demand_update` (talep endeksleri); `NewDayStep` yeni gün kancası (`INewDayHook`) ile müşteri havuzunu çeker |
| 7 | Anti-arbitraj | Ürünü oyuncuya satan NPC, aynı ürüne müşteri olarak ilgilenmez (`NpcStateStore.HasSoldToPlayer`) |
| 8 | Rapor göster | S2/S3 raporu müşterinin değer hatasını (σ) yarıya indirir, güven +10 (Rıza Bey +15) |
| 9 | Veri | `economy_constants.json` → `customers`, `demand`; `negotiation_rules.json` → `sell`; `npc_profiles.json` müşteri alanları (`availableFromDay`, `segments`, `reportTrustGain`). Ayrıştırma + doğrulama + gerçek veri testleri |
| 10 | API | `SetPrice`, `GetCustomers`, `StartSale`, `AskPrice`, `ShowReport`, `AcceptCustomerFinalOffer`, `LetCustomerGo`, `GetSale`; görünümler gizli sayı taşımaz |
| 11 | Durum özeti | `GameStateDigest`: müşteri yuvaları (`K`), süren satış (`S`), talep endeksleri (`D`), satış kayıtları (`T`), etiket fiyatı |

## 3. Day 8'de YAPILMAYANLAR

| GDD öğesi | Neden / sahibi |
|-----------|----------------|
| Gün sonu adım 3 (stok bekleme değer kaybı) | GDD planında ayrı bir güne atanmamış → **açık madde** |
| İtibar, dükkân priminin %12'ye çıkışı, müşteri bonusu (+1–2) | İlerleme sistemi (Gün 11); prim sabit %5, tavan yalnızca doğrulanır |
| "Önerilen fiyat bandı", Gün 1 "kabul et/reddet" mini satış, öğretici ipuçları | UI / öğretici (Gün 11–13) |
| `CustomerArrived` olayı | Gelen müşteri sayısı raftan türetilir; ayrı bir bildirim olayı yok (UI sorgu ile alır) |
| Rıza Bey'in ekspertiz sırasındaki güven kuralları, Dr. Murat "doğru kartı hemen kabul eder", "güven < 25 ise ayrılır" | Açık madde (Day 7'den beri) |
| Kayıt/yükleme (`CustomerState`, `DemandState`, `TradeState` dahil) | Gün 9 |
| Ekonomi simülasyonu / sömürü botları | Gün 10 |

## 4. Motor formülü (UA20) — Senaryo 2 ve 3 satış tablolarıyla doğrulanmış

GDD v0.2 7.2: "müşteri rolünde aynı motor ters yönde çalışır; müşteri teklifini kendi Max'ına (M) doğru yükseltir; oyuncunun istediği fiyat ≤ müşterinin yeni teklifiyse anlaşma müşterinin teklifi üzerinden olur." Sayılar `negotiation_rules.json`'da alış motoruyla ortaktır.

Bir turun sırası (oyuncu bir fiyat İSTER):
1. **Çok pahalı**: istenen `> 1,15 × M` → güven −15 ve sabır ek −1 (Gün ≥ 5'ten itibaren ceza; öncesinde yalnızca uyarı). Sabır turu ayrıca −1 düştüğü için toplam −2 (v0.2 7.2).
2. **Müşteri teklifini yükseltir**: `t = 0,20 + 0,30 × (güven/100) + 0,15 × aciliyet`; `yeniTeklif = min(M, teklif + t × (M − teklif))`; gösterilen teklif `min(10 TL'ye yuvarla(yeniTeklif), taban10(M))`.
3. **Anlaşma**: istenen ≤ gösterilen teklif → fiyat müşterinin teklifi (I4: ≤ M).
4. Aksi halde: istenen `≤ M / 0,97` ise güven +5 ("yakın istek"); sabır −1; sabır 0 → **son teklif** (yalnızca kabul ya da yolcu etme).

Doğrulama: Hatice (M 9.150, açılış 7.320, güven 50, aciliyet 0,2): turlar 8.020 → 8.460 → 8.750 → **8.920** anlaşma (v0.2: 8.015 / 8.463 / 8.745 / 8.920). Berk (M 20.410, açılış 18.370, aciliyet 0,1): 19.110 → 19.610 → **19.920** (v0.2: 19.115 / 19.607 / 19.924 → 19.920). Güven her turda +5 ilerler (t: 0,38 / 0,395 / 0,41 / 0,425) — tablodaki t değerleriyle birebir.

## 5. Belirsizlik giderme (yeni özellik değil; UA21–UA28)

**UA21 — Müşterinin aciliyeti ve sabrı.** Aciliyet: NPC'nin kendi `seller.urgency` değeri (tablodaki t değerlerini yalnızca bu üretir: Kemal 0,2; Berk 0,1). Sabır: `customer.patience`. Başlangıç güveni satıcıyla aynı: `50 ± 10` (rastgele çekim).

**UA22 — Müşteri Max'ı (v0.2 6.2 + 10.4).** `M = V_müşteri × mRatio × paket × (1 + dükkân primi)`; `V_müşteri = gerçek değer (güncel talep dahil) × (1 + σ × (2u − 1))`; `M ≤ 1,25 × gerçek değer`; 10 TL'ye yuvarlı. Paket çarpanı (Nermin 1,12) ürün **kutulu ya da faturalıysa** uygulanır ("kutulu/faturalı" ifadesi belirsizdi; "ya da" seçildi). Açılış teklifi `round10(açılış oranı × M)` (Senaryo 2: 7.320, Senaryo 3: 18.370).

**UA23 — Günlük müşteri havuzu.** Her sabah 5 (üst sınır) müşteri yuvası sabit sırayla çekilir (`"customers"` akışı; yuva başına 4 çekim: kişi, değer hatası, güven, ürün seçimi). Yuvada kim olabileceği veridedir (`customer.availableFromDay`): Gün 1–2 Kemal/Selin (v0.2 11.2), Gün 3 Hatice/Ayşe/Ozan/Cengiz (11.2'de belirtilmeyen kişiler Gün 2'den sonraya konuldu), Gün 4 Rıza/Murat, Gün 6 Berk/Nermin. P4: zengin/koleksiyoncu (Berk, Nermin) günde en fazla 1 yuva. Kaç yuvanın **geldiği** `2 + ⌈raf × 0,5⌉` (üst sınır 5) formülüyle, o günkü **en yüksek raf doluluğuna** göre belirlenir: sabah rafa göre başlar, gün içindeki alışlarla artar, satışlarla azalmaz (Gün 1'de ürün gün içinde alınıp satılabildiği için).

**UA24 — Müşterinin ilgisi.** Gelen müşteri, etiketli ürünler arasından şu koşulları sağlayanlarla ilgilenir: segment uyumlu (Cengiz: giriş/orta), aynı NPC'nin oyuncuya sattığı ürün değil (anti-arbitraj), etiket `≤ 1,15 × M`. Uygun ürünler arasından seçim çekimiyle biri seçilir; hiçbiri yoksa müşteri gider. İlgi durumdan türetilir (sorgu durum değiştirmez). Müşteri yolcu edilirse o gün geri gelmez.

**UA25 — Talep (v0.2 10.2).** Gün 1–4 tüm modeller 1,00. Yeni gün ≥ 5 olan her gün sonu, her model için (içerik sırasıyla, `"demand"` akışı, model başına 1 çekim) `talep += (2u − 1) × 0,02 + 0,20 × (1 − talep)`, sınır [0,90; 1,10]. Satış baskısı: son 5 günde (bugün dahil) aynı modelden satılan her adet `×0,985`, taban 0,90. Etkin çarpan = endeks × baskı ve satış tarafındaki gerçek değere girer. **Alış tarafı (ilan üretimi, ekspertiz, satıcı değeri) Gün 5–7 davranışını korumak için talebe bağlanmadı** (talep 1,0); alış tarafının talebe bağlanması Gün 10 denge ayarına bırakıldı (açık madde).

**UA26 — Rapor göster (v0.2 7.5).** Yalnızca S2/S3 raporu, bir pazarlıkta bir kez. Müşterinin σ'sı yarıya iner (M yeniden hesaplanır; kusurlu üründe M düşer, iyi üründe artar — ikisi de gerçek değere yaklaşma sonucudur), güven +10; NPC `reportTrustGain` verirse o (Rıza Bey +15).

**UA27 — Tek pazarlık.** Alış ve satış birlikte en fazla bir pazarlık vardır; sürerken `EndDay` `negotiation.in_progress` döner.

**UA28 — Kaçan müşteri.** Gün sonu adım 1: gelmiş ama satın almamış (bekleyen ya da yolcu edilen) müşteri sayısı `DayEndReport.MissedCustomers` ve `CustomerState.MissedTotal`'a yazılır.

## 6. Yapısal kararlar

- Domain'de, Unity bağımsız; rastgelelik yalnızca `RngStreams` (`"customers"`, `"demand"`); motorlarda rastgelelik yok. Satış pazarlığı için ayrı akış gerekmez (güven/değer hatası yuva çekimlerinden türetilir).
- Olay sırası (satış): `CustomerNegotiationStarted` → `CustomerOfferMade` → `ItemSold` → `CustomerNegotiationEnded`.
- `NewDayStep` isteğe bağlı `INewDayHook` listesi alır (mevcut kurucu çağrıları bozulmaz); gün sonu adım listesi artık `missed_customers, daily_expense, listing_expiry, demand_update, new_day` (sıra 1, 2, 4, 5, 7). Mevcut testlerin bu tam listeyi denetleyen 4 satırı ve rastgelelik akışı listesi bu bilinçli değişime göre güncellendi.
- `DayEndReport` sonuna isteğe bağlı `missedCustomers` parametresi eklendi.
- `ContentDatabase.Load` artık `economy_constants.json` içinde `customers` ve `demand` bölümlerini ve `negotiation_rules.json` içinde `sell` bölümünü zorunlu tutar; `customers.richQuota.npcIds` ve `sell.reportLevelIds` çapraz denetlenir.
