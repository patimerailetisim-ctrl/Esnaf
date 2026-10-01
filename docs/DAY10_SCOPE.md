# GÜN 10 — Unity oynanabilir MVP entegrasyonu (kapsam ve ilerleme)

Hedef dilim: Yeni Oyun → İlanlar → Telefon Detayı → Ekspertiz → Alım Pazarlığı → Satın Al → Raf. Müşteri satış akışı bu dilimde YOK.

## Kararlar
- UI yalnızca `IGameApi` üzerinden (Unity'siz `Esnaf.Presentation` içindeki `UiFlow` ile) konuşur; oyun kuralı MonoBehaviour'da yoktur.
- `IGameApi`, Domain/Core/Persistence/Kaydet-Yükle DEĞİŞMEDİ. İçerik JSON'u kaynak olmaya devam eder (`ContentPresentation` yalnızca salt okunur arama).
- Yeni assembly: `Esnaf.Presentation` (netstandard2.1, Unity'siz; `dotnet test` ile test edilir, mutation uygulanır).
- Metin: `UnityEngine.UI.Text` (TMP sonra). Düzen: mobil dikey, `CanvasScaler` Scale With Screen Size, referans 1080x1920.
- Arayüz sahnede değil çalışma anında kodla kurulur; sahneyi `Esnaf > Setup Day 10` menüsü üretir.

## Adımlar
1. **Temel (bu adım):** `Esnaf.Presentation` (MoneyFormatter, TurkishTexts, ContentPresentation, UiFlow, TopBarViewModel), `GameBootstrap`, `Day10Setup`, EventSystem (+ `InputSystemUIInputModule`), üst bar (Gün + Nakit).
2. İlanlar ekranı. 3. Telefon detayı. 4. Ekspertiz paneli. 5. Pazarlık paneli. 6. Raf ekranı ve cila.

## Bilinen sınırlar (Adım 1)
- Unity tarafı (`GameBootstrap`, `UiBuilder`, `TopBarView`, `Day10Setup`) Linux'ta Unity yokken yalnızca Unity API taklitleriyle (stub) derleme denetiminden geçti; gerçek derleme ve görünüm Windows/Unity'de doğrulanmalıdır.
- Güvenli alan (notch) ve yatay yerleşim sonraki adımlarda.

## Adım 2: İlanlar ekranı (tamamlandı)
- `UiFlow`: `Listings` (ListingRowViewModel: model adı, depolama, yaş, istenen fiyat, kalan gün, kutu, fatura, satıcı), `SelectListing` / `ClearSelection` / `SelectedListingId` / `SelectedListing` (seçim, ilan pazardan kalkana kadar korunur; Telefon Detayı bunu kullanacak), `EndDay()` (IGameApi.EndDay + yenileme), `StatusMessage` (Türkçe hata; başarılı komut temizler). Olaylar: nakit, gün, ilan üretildi/kalktı/satın alındı.
- Unity: `ListingsView` (ScrollRect liste, satır = Button, "Günü Bitir" düğmesi, durum mesajı); `GameBootstrap` ekranı UiFlow.Changed'e bağlar. `IGameApi` ve Domain değişmedi.
- "Kalan gün": 1 ve altı "Son gün" (ilan gün sonunda kalkar), aksi halde "N gün kaldı".

## Adım 3: Telefon Detayı (tamamlandı)
- `UiFlow`: `Detail` (ListingDetailViewModel: etiketli satırlar), `OpenListing(id)` (seçer + detaya geçer, tek olay), `OpenSelectedListing()`, `Back()` (seçim korunur), `RequestAppraisal()` / `RequestNegotiation()` (düğmeler hazır; şimdilik yalnızca "bir sonraki adımda eklenecek" der, oyun durumunu DEĞİŞTİRMEZ). Seçili ilan pazardan kalkarsa (ör. satın alınırsa) akış kendiliğinden İlanlar'a döner. `UiScreen.Detail` eklendi.
- Unity: `DetailView` (Geri, başlık, 7 satır, durum mesajı, "Ekspertiz" ve "Pazarlık" düğmeleri); `ListingsView` satır tıklaması detaya geçer, ekranlar `CurrentScreen`'e göre görünür.
- Ekspertiz ve pazarlık Adım 4 ve 5'te `UiFlow` içinde uygulanacak.

## Adım 4: Ekspertiz ekranı (tamamlandı)
- Akış: Telefon Detayı → Ekspertiz → seviye seç → "Ekspertiz Yaptır" → sonuç → Geri (Ekspertiz → Detay → İlanlar). `UiScreen.Appraisal` eklendi.
- `UiFlow`: `OpenAppraisal()`, `SelectLevel(id)`, `PerformAppraisal()` (IGameApi.StartAppraisal), `AppraisalScreen` (AppraisalScreenViewModel: seviyeler, seçili seviye, sonuç, ana düğme yazısı). Sonuç, `GetAppraisals(listingId)` ve `GetRiskCard(resultId, istenen fiyat)` çıktılarının Türkçe satırlarıdır. Ekspertiz kuralı Presentation/Unity'de YOK.
- Ücret: oyundan düşer (nakit üst barda olaylarla güncellenir); aynı ilan + aynı seviye ikinci kez ücret almaz (I5), "Sonucu Göster" yazar. Kilit/cihaz hataları API'den gelir ve Türkçe mesajla gösterilir; durum değişmez.
- Gösterim kararları: kilit durumu ("Kilitli (Gün N'de açılır)") ve ücret yazısı içerikteki veriden gösterilir (karar API'dedir); "Cihaz gerekir" yalnızca bilgidir (cihaz sahipliği için API sorgusu yok). Risk kartı pazarlık olmadığı için İSTENEN FİYATLA hesaplanır ("istenen fiyatla alırsan"); değer aralığı vermeyen seviyede (s0) kart yoktur ve neden yazılır. Bulgu yoksa asla "sorun yok" denmez, "sorun görünmüyor" denir.
- Pazarlık düğmesi hâlâ hazır-ama-uygulanmadı (Adım 5).

## Adım 5: Pazarlık ekranı (tamamlandı)
Ayrıntı: `docs/DAY10_STEP5_SCOPE.md`. Telefon Detayı → Pazarlık → teklif / koz / son fiyat / vazgeç; satın alma `TradeService` tarafından yapılır, nakit/ilan/raf API durumundan okunur ve "Rafa eklendi" mesajı gösterilir. Raf ekranı Adım 6'dadır.
