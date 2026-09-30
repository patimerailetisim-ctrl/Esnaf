# DAY 6 — Ekspertiz: kapsam ve uygulama notları (GDD v0.3 FINAL'in uygulamaya çevrilmesi)

Yeni tasarım değildir. GDD v0.3 Bölüm 10.1 Gün 6 satırı, v0.2 Bölüm 5 (ekspertiz), 7.4 (koz kartı üretimi) ve v0.3 Bölüm 3.2/4.3/9.4'ün Day 6'da yapılan kısmını ve yorum kararlarını kaydeder.

## 1. GDD'deki Gün 6 satırı (aynen)

> **Ekspertiz** — `AppraisalService.cs`, `AppraisalResult.cs`, `KnowledgeState.cs`, `RiskCard.cs`, `appraisal_levels.json`
> Test: v0.2 5.4 tablosu; S1 kaçırma %30±1,5; kapsama testleri; I5 (kilit); ücret maliyete eklenir/boşa gider
> Çalışan özellik: Ekspertiz komutu çalışır, bulgu+aralık+risk kartı döner

## 2. Day 6'da YAPILANLAR

| # | Sistem | İçerik |
|---|--------|--------|
| 1 | `appraisal_levels.json` | S0–S3: ücret (segmente göre), açılış günü, gereken ekipman, tespit/yanlış alarm, güven düzeyi, kanıt gücü, aralık genişlikleri, kapsama tahmini; kontrol edilen nitelikler (ekran, kamera); risk kartı satış çarpanı (1,02). Ayrıştırma + doğrulama, `ContentDatabase.Appraisal` |
| 2 | `AppraisalCalculator` | Saf hesap (rastgelelik enjekte): bulgular (tespit / yanlış alarm), pil ve kasa aralıkları, değer aralığı, koz kartları |
| 3 | `AppraisalResult`, `KnowledgeState` | Oyuncunun "bildikleri" (GDD 4.3): `instanceId → sonuçlar`; gerçek değer hiçbir yerde saklanmaz |
| 4 | `AppraisalService` | Ücret ödeme (bekleyen ekspertiz ücreti), seviye kilitleri, **kilitli sonuç (I5)**, tohum = `hash(masterSeed, instanceId, level)`, `AppraisalCompleted` olayı |
| 5 | `RiskCard` | v0.2 5.5: kötü / orta / iyi senaryo, beklenen satış = değer × 1,02, kâr, aralık dışı kalma olasılığı |
| 6 | Koz kartı ÜRETİMİ | Her bulgu bir `TrumpCard` (kart gücü = kanıt gücü, sorunun TL değeri). Kartın pazarlıkta kullanımı Gün 7'dir |
| 7 | Boşa ekspertiz | Ürünü alınmadan süresi biten ilanın bekleyen ekspertiz ücreti, ilan kalkarken gider yazılır (gün sonu adım 4) |
| 8 | API | `StartAppraisal(listingId, levelId)`, `GetAppraisals(listingId)`, `GetRiskCard(appraisalId, offer)`; görünümler yanlış alarm bayrağını taşımaz |
| 9 | Durum özeti | `GameStateDigest` ekspertiz sonuçlarını, sayacı ve ekipmanı da içerir (I6) |
| 10 | `EquipmentState` | Yalnızca "hangi ekipman var" kümesi (S3 için `test_device`); satın alma akışı yok |

## 3. Day 6'da YAPILMAYANLAR

| GDD öğesi | Sahibi |
|-----------|--------|
| Koz kartının pazarlıkta kullanımı, R düşüşü, güven/sabır etkisi, "Rıza Bey S3'ü %30 reddeder" | Gün 7 |
| "Rapor göster" (satışta σ yarıya iner, güven +10/+15) | Gün 8 |
| Test cihazı satın alma (12.000 TL yatırım), seviye açılımlarının görevlere bağlanması | Gün 11 (İlerleme) / iş akışı; bu gün açılış günü veri dosyasındadır |
| Ürünü alınca ekspertiz ücretinin maliyet tabanına eklenmesi (`BuyFlow`) | Gün 7 (servis `CapitalizePendingAppraisals` Gün 4'te hazır) |
| Kayıt/yükleme | Gün 9 |

## 4. Belirsizlik giderme (yeni özellik değil; UA9–UA13)

**UA9 — Gözlenen değer formülü (v0.2 5.3).** Ekspertiz "gözlenen durumu" kurar; değer, aynı `ValueCalculator` formülüyle bu durumdan hesaplanır:
1. Pil ve kasa, ekspertizin bulduğu aralığın **orta noktasıdır** (gerçeğe ± küçük sapma, UA10).
2. Gizli kusurlar (ekran "değişmiş", kamera lekeli/arızalı) önce "yokmuş gibi" alınır. **Yakalanan** kusur çarpanının **güven üssü** kadar etkiyi geri katar: `çarpan ^ kanıt gücü` (ekran 0,88 ^ 0,7 ≈ 0,914). Yanlış alarm da aynı şekilde katılır (oyuncu ayırt edemez).
3. Görünür kusurlar (çizik/kırık ekran) olduğu gibi kalır.
4. Değer aralığı = gözlenen değer × (1 ± yarı genişlik): S1 ±%10, S2 ±%5, S3 ±%2,5; uçlar 10 TL'ye yuvarlanır.
Bu formül v0.2 5.4 tablosundaki tüm aralıkları üretir (S1 21.680–26.490, kaçırınca 22.810–27.880, S2 22.020–24.340, S3 21.750–22.860; tabloda 100'e yuvarlı). Değer, ekspertiz, defter golden'ları gibi sayılar tam eşitlikle; v0.2'nin "≈" yazılı yuvarlanmış sayıları ±100 TL toleransla (UA3'ün devamı) karşılaştırılır.

**UA10 — Aralık merkezi kayması ve değer gürültüsü.** v0.2 "aralığın merkezi rastgele sapmayla kaydırılır" der ve kapsama hedeflerini (S1 %85, S2 %92, S3 %96) "simülasyonla ayarlanacak" diye bırakır. Bu yüzden iki ayar parametresi veridedir: `centerShift` (pil/kasa merkezinin, yarı genişliğin bu oranına kadar kayması) ve `valueNoise` (gözlenen değere ±oran gürültü). Örnek tablo bu ikisi 0 iken oluşur; gerçek dosyadaki değerler kapsama testiyle (10.000 deneme, Gün 5+ doğal ürün karışımı) kalibre edilmiştir:

| Seviye | `centerShift` | `valueNoise` | Ölçülen kapsama | Hedef (v0.2 5.3) |
|--------|---------------|--------------|-----------------|------------------|
| S1 | 0,5 | 0,09 | ≈ %85,4 | %85 ± 3 |
| S2 | 0,5 | 0 | ≈ %91,6 | %92 ± 3 |
| S3 | 0,6 | 0,022 | ≈ %95,5 | %96 ± 3 |

Gürültüsüz ölçüm S1'de ≈ %92, S3'te ≈ %99 çıktığı için (kusuru yakalama olasılıkları tabloda sabit) yalnızca bu iki seviyeye gürültü eklendi. Seviyeler arası sıra (S1 < S2 < S3) bir testle korunur.

**UA11 — Hangi kusurlar "kontrol edilir".** v0.2 5.2'nin kategorik bulguları: ekran (`replaced_aftermarket`) ve kamera (`spotted`, `faulty`). Yanlış alarmda varsayılan kusur: ekran `replaced_aftermarket`, kamera `spotted`.

**UA12 — Seviye kilitleri.** S1 Gün 3, S2 Gün 5, S3 Gün 6 + `test_device` ekipmanı (v0.2 5.1). Bunlar veridedir; İlerleme sistemi (Gün 11) aynı bilgiyi görev/öğreticiyle birleştirecek.

**UA13 — Yalnızca pazardaki ürün ekspertize girer** (ürün alınınca sonuç zaten `KnowledgeState`'te kalır). Ücret, ürün alınana (Gün 7) ya da ilan kalkana kadar "bekleyen"dir: nakit hemen düşer, servet değişmez, ürün alınınca maliyete eklenir, alınmazsa gider yazılır.

## 5. Yapısal kararlar

- Domain'de, Unity bağımsız; rastgelelik yalnızca `PcgRandom`: ekspertiz seed'i `FNV-1a("appraisal|masterSeed|instanceId|levelId")`, akış sabittir. Aynı ürün + aynı seviye = aynı sonuç; ayrıca sonuç `KnowledgeState`'te kilitlidir (tekrar çağrı ücret almaz).
- Çekim sırası (sabit): her kontrol için bir `Chance` (veri sırasıyla), sonra pil, kasa, değer için birer `NextDouble`.
- `ListingExpiryStep` artık `EconomyService` alır (bekleyen ekspertiz ücretini gider yazmak için).
