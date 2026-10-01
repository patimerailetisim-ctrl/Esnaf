# Gün 10 — Adım 7: Müşteri temel sistemi (kişilik + durum)

Bu adım Domain katmanıdır. Unity görseli, diyalog metni ve gerçek satış pazarlığı arayüzü yoktur.

## 1. Önce incelenen mevcut sistem (Gün 8) — değişmedi

Müşteri sistemi zaten vardı: `CustomerService` günde 5 yuva çeker (akış `customers`, yuva başına 4 çekim: NPC, değer, güven, seçim), zengin/koleksiyoncu kotası
(günde en çok 1), gelen sayısı `2+⌈raf×0,5⌉` (en çok 5), `FindInterest/IsEligible` (segment, seller/customer anti-arbitraj, fiyat ≤ 1,15×Max),
`MaxFor` (σ, değer oranı, paket, dükkân primi, tavan 1,25×gerçek değer), `OpeningFor`, `BuildSetup` (başlangıç güveni 50 ± 10), Cengiz kuralı (`availableFromDay`, segmentler).
On müşteri sabit adlı NPC'dir; her birinin `customer` ve `seller` rollerinde zaten gerçek sayılar vardır.

**Karar:** yeni davranış/formül EKLENMEDİ. Kişilik, bu gerçek sayıların *okunur özeti* ve doğrulanan bir sözleşmedir.

## 2. Kişilik sistemi

Dosya: `npc_profiles.json` → `personalityScale`, `personalities`, `npcs[].personalityId`.

| Boyut | Kaynak (mevcut alan) | Düzey kuralı |
|---|---|---|
| Sabır | `customer.patience` | `negotiation_rules.view` eşikleri (mevcut) |
| Aciliyet | `seller.urgency` | ≥0,3 orta, ≥0,6 yüksek |
| Teknoloji/değer bilgisi | `seller.valueSigma` (küçük = bilgili) | ≤0,12 orta, ≤0,06 yüksek |
| Bütçe esnekliği | `customer.valueRatio` | ≥1,0 orta, ≥1,05 yüksek |
| Pazarlık toleransı | `customer.openingOfferRatio` (küçük = çok pazarlık) | ≤0,90 orta, ≤0,85 yüksek |
| Ürün ilgisi | `customer.segments` | boş = hepsi |
| Başlangıç güveni | yuvanın mevcut güven çekimi | `NegotiationLevels.MoodOf` |

On arketip: Pazarlıkçı, Aceleci, Kararsız, Teknoloji meraklısı, Fiyat odaklı, Rahat/samimi, Bilinçli alıcı, Gösteriş meraklısı, Bütçesi sınırlı, Güven odaklı.
Her arketip `expects` ile boyutlarda kabul ettiği düzeyleri yazar. **İçerik yüklenirken** her NPC'nin gerçek sayıları arketipe karşı denetlenir
(`personality.inconsistent`): bir NPC'ye "Pazarlıkçı" demek, açılış oranını yükseltmeden geçmez. Kişilik etiketi mekanikten kopamaz.

Atamalar: Kemal=Pazarlıkçı, Selin/Ozan=Aceleci, Hatice=Kararsız, Nermin=Teknoloji meraklısı, Murat=Bilinçli alıcı, Berk=Gösteriş meraklısı, Cengiz=Bütçesi sınırlı, Rıza=Güven odaklı, Ayşe=Rahat/samimi.
"Fiyat odaklı" arketibi tanımlıdır, henüz atanmış NPC yoktur (açık nokta).

Kod: `CustomerTrait`, `PersonalityScale(s)`, `PersonalityDefinition`, `PersonalityCatalog`, `CustomerProfile`, `CustomerProfiler` (hepsi `Esnaf.Domain.Npc`).
Katalog bölümleri isteğe bağlıdır (yoksa `PersonalityCatalog.Empty`; mevcut fixture'lar geçerli kalır).

## 3. Gün 8 kurallarıyla ilişki

Hiçbir Gün 8 kuralı değişmedi: günlük sayı, yuvalar, talep baskısı, zengin/koleksiyoncu sınırı, Cengiz kuralı, anti-arbitraj, satış durumu aynı kodla çalışır.
Tek yeniden düzenleme: `BuildSetup` içindeki başlangıç güveni hesabı `StartTrustOf` yöntemine alındı (aynı formül); profil ve satış kurulumu aynı sayıyı kullanır.
Mevcut testler korunur; tek bilinçli güncelleme `SellFlowTests.Views_CarryNoHiddenSaleState`: `CustomerView` ve `SaleView` artık `Profile` üyesi taşır
(yasak sözcük denetimi geçerlidir; profil Max, güven sayısı, sabır sayısı içermez — yalnızca düzeyler).

## 4. Deterministik RNG

Profil üretimi **rastgelelik kullanmaz**: NPC sabit içeriktir, düzeyler saf fonksiyondur, başlangıç ruh hali yuvanın MEVCUT çekiminden okunur.
`customers` akışına yeni çekim eklenmedi, RNG devamı ve durum özeti (`fixture` digest) değişmedi. Testler: aynı tohum = aynı müşteri + kişilik dizisi;
profil sorgusu durumu/RNG'yi değiştirmez.

## 5. Save/Load

Yeni kayıt durumu YOKTUR → Save v1 aynen, migration gerekmedi. Profil kayıtlı durumdan (NPC + yuva çekimi) yeniden türetilir; test, kaydet/yükle sonrası aynı profili doğrular.

## 6. Doğal diyalog için hazırlanan durum

`CustomerProfile` (kişilik kimliği/adı, beş düzey, tercih segmentleri, başlangıç ruh hali) `CustomerView` ve `SaleView` üzerinden UI/diyalog katmanına gider.
Satış sırasındaki canlı ruh hali ve sabır `SaleView.Mood/Patience` zaten vardır. Gelecekteki diyalog sistemi (arketip × düzey × aşama) cümleyi bunlardan seçer.
Bu adımda metin havuzu YOKTUR.

## 7. Yapılmadı

Unity müşteri arayüzü, karakter görseli, telefon görselleri, diyalog altyazısı/ses, gerçek satış pazarlığı arayüzü, yeni satış/ekonomi kuralı,
"Fiyat odaklı" için atanmış NPC, çok oyunculu/oyuncudan oyuncuya ticaret. Satışla bağlantı: mevcut `SellService` (`StartSale` vb.) aynen kullanılır; yalnızca görünümlere profil eklendi.

## 8. Test ve mutation

Testler: 2568/2568 (`dotnet test`). Yeni kod üzerinde operatör mutasyonu: PersonalityScale 5, CustomerProfiler 5, PersonalityDefinition 2, PersonalityCatalog 5,
ContentValidator.ValidatePersonalities 43, ContentParser.ParsePersonalities 64, CustomerService (StartTrustOf/ProfileOf/ProfileFor) 6 mutant.
5 gerçek hayatta kalan bulundu ve testlerle öldürüldü (`TryGet(null)`, eşit/ters eşik sırası, yön mesajı, bozuk JSON/eksik şema sürümü). Denklik (equivalent) hayatta kalan yok.
Unity tarafı doğrulanmadı (Unity bu ortamda çalıştırılamaz); yeni `.cs` dosyaları için Unity `.meta` dosyalarını kendisi üretir.
