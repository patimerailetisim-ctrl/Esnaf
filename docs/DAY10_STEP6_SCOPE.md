# GÜN 10 — ADIM 6: Doğrudan satın alma + Raf ekranı (kapsam)

## Karar (kullanıcı onaylı, GDD'de YOKTU)
"Satın Al" pazarlıksız alıştır. GDD v0.3'te ve `IGameApi`'de böyle bir komut yoktu; kullanıcı **yeni bir pazarlıksız alış komutu** eklenmesini onayladı. Raf ekranı **salt okunur** olacak (fiyat etiketi/`SetPrice` ve müşteri satışı YOK).

## Domain / IGameApi değişikliği (tek ve küçük)
- `IGameApi.BuyListing(long listingId) → Result<Money>` (döner değer ödenen fiyat). `GameApi` → `TradeService.BuyNow`.
- `TradeService.BuyNow`: ilanı **istenen fiyattan** alır; alış hattı pazarlıktakiyle AYNI (`InventoryService.Acquire`: defter kaydı, raf, bekleyen ekspertiz ücretinin maliyete eklenmesi). Sonra ilan pazardan kalkar, satıcı "oyuncuya sattı" diye kaydedilir (`RecordSoldToPlayer`), müşteri gelişleri yenilenir (`RefreshArrivals`), `ListingPurchased` yayınlanır.
- Pazarlık yoktur: `NegotiationStarted/OfferMade/NegotiationEnded` yayınlanmaz, satıcı karşılaşması (`RecordEncounter`) sayılmaz, rastgelelik KULLANILMAZ (RNG akışları değişmez), yeni kayıt (save) alanı YOK.
- Hatalar (başarısızlıkta hiçbir durum değişmez): `negotiation.in_progress` (pazarlık ya da satış sürerken yapılamaz), `listing.unknown`, `inventory.full`, `cash.insufficient`.
- Mevcut pazarlık/satış/Day 5–9 davranışı DEĞİŞMEDİ; yalnızca ekleme yapıldı (mevcut hiçbir test değiştirilmedi). Save v1, Save/Load ve içerik JSON'u aynen.

## Presentation / Unity
- `UiFlow.BuyNow()` (yalnızca Telefon Detayı'nda): API'ye gönderir; başarıda İlanlar'a döner ve "Satın alındı: … — … ₺. Rafa eklendi (n/6)." gösterir, hatada Türkçe neden (nakit, raf dolu, pazarlık sürüyor…) ve ekran/durum değişmez. Detay ekranında "Satın Al — fiyat" düğmesi (Ekspertiz ve Pazarlık'ın yanında).
- `UiFlow.OpenShelf()` (yalnızca İlanlar'dan), `ShelfScreen`, `ShelfButtonText` ("Raf (n/6)"), `Back` Raf → İlanlar. `ShelfScreenViewModel/ShelfItemRowViewModel`: model adı + maliyet tabanı (`StockLine`; ekspertiz ücreti dahil, API'nin verdiği gibi), doluluk, boşken "Rafta ürün yok." Raf, rafa ürün eklenince (`ItemAddedToShelf`) kendiliğinden güncellenir.
- Unity: `ShelfView` (yeni), `DetailView` (Satın Al düğmesi), `ListingsView` (Raf düğmesi), `GameBootstrap`.

## Yapılmayanlar
Fiyat etiketi (`SetPrice`), müşteri/satış akışı, gün sonu ekonomisi, ürün ayrıntıları (depolama/yaş) raf satırlarında (`StockLine` taşımıyor), TMP, Save/Load.

## Test
Domain: `BuyListingTests` (satın alma, defter/özet, ekspertiz maliyeti, olaylar ve sıra, NPC kaydı, karşılaşma sayılmaz, tekrar alınamaz, bilinmeyen ilan, yetersiz nakit — düşük sermayeli içerikle —, raf dolu, pazarlık sürerken, birden çok alış, müşteri gelişi, RNG'ye dokunmaz, gün bitirme, determinizm ve kayıt/yükleme). Presentation: `UiFlowBuyAndShelfTests`, `TurkishTextsTests`, `ListingDetailViewModelTests`. Mutation: pymut + elle mutantlar (hepsi öldürüldü, `TradeService.BuyNow` ve `UiFlow` için).
