# GÜN 10 — ADIM 5: Pazarlık ekranı (kapsam)

Mevcut alış/pazarlık sistemi Unity arayüzüne bağlandı. **Yeni pazarlık kuralı, para hesabı veya NPC kararı yazılmadı**: bütün sonuçlar `IGameApi` / `TradeService` / `NegotiationEngine`'den gelir; Presentation yalnızca komutu gönderir ve dönen durumu Türkçe gösterir.

## Bağlanan mevcut API'ler
`StartNegotiation(listingId)`, `GetNegotiation()` (parametresiz: tek açık pazarlık vardır), `MakeOffer(Money)`, `MakeOfferWithCard(Money, appraisalId, cardIndex)`, `AcceptFinalPrice()`, `WalkAway()`; okumak için `GetListings()`, `GetInventory()`, `GetCash()`, `GetDay()`. `IGameApi`, Domain, Core, Persistence, Save/Load, içerik JSON'u DEĞİŞMEDİ.

## Eklenenler
- **Presentation (Unity'siz, test edilir):** `UiScreen.Negotiation`; `UiFlow`: `OpenNegotiation`, `SetOfferText`, `AdjustOffer`, `SelectCard`, `SubmitOffer`, `SubmitCardOffer`, `AcceptFinalPrice`, `WalkAway`, `NegotiationScreen`; `NegotiationScreenViewModel` / `NegotiationCardRowViewModel`; `NegotiationScreenBuilder`; `TurkishTexts` (pazarlık satırları, faz/ruh hali/sabır, koz kartı, satıcı cevabı, satın alma/vazgeçme mesajları, teklif kutusu hataları). Telefon Detayı'ndaki "Pazarlık" düğmesi artık gerçek ekranı açar (`RequestNegotiation` yer tutucusu kalktı). `offer.invalid` mesajı "pozitif ve 10 ₺'nin katı olmalı" diye güncellendi.
- **Unity:** `NegotiationPanelView` (kaydırmalı bilgi/koz alanı, teklif kutusu + −100/−10/+10/+100, Teklif Ver, Koz Kullan, Son Fiyatı Kabul Et, Vazgeç, Geri; uzun satırlar sarılır), `UiBuilder.CreateIntegerField`, `GameBootstrap` bağlantısı.

## Davranışlar ve kaynakları
- **Açılış:** ilan için pazarlık açık değilse `StartNegotiation`; açıksa (Geri ile çıkılmışsa) YENİ başlatılmadan kaldığı yerden sürer. Başka ilanın pazarlığı sürüyorsa API reddeder (`negotiation.in_progress`) ve Türkçe mesaj gösterilir; eski pazarlık başka ilanın ekranında görünmez.
- **Satıcının cevabı:** API'nin döndürdüğü fiyatın önceki fiyatla karşılaştırmasından anlatılır (karşı teklif / direniyor / son fiyat). Faz, ruh hali, sabır ve "gücendi" bilgisi `NegotiationView`'dan gelir.
- **Satın alma:** teklif satıcının fiyatına ulaşınca ya da son fiyat kabul edilince `TradeService` satın alır: nakit, ilan listesi, raf ve üst bar API durumundan okunur; mesaj "Satın alındı: … — … ₺. Rafa eklendi (n/6)". Alınan ilan listeden düşer, tekrar alınamaz (`listing.unknown`). Yetersiz nakit (`cash.insufficient`), raf dolu, kart hataları API'den gelir, durum değişmez, pazarlık açık kalır.
- **Vazgeç:** `WalkAway`: mevcut sistem davranışı olarak ilan pazardan kalkar ve bekleyen ekspertiz ücreti gider yazılır; nakit değişmez. Mesaj bunu söyler.
- **Koz:** pazarlıktaki kartlar API görünümünden listelenir; seçilen kart `MakeOfferWithCard` ile gider; kullanılmış kart ve tekrar oynama hatası API'den gelir.
- **Teklif kutusu:** yalnızca rakam, 1…1.000.000.000 TL; boş/harf/negatif/sıfır/çok büyük girişte API'ye gitmeden Türkçe hata. 10 ₺'nin katı olma ve diğer kurallar API'dedir. +/− düğmeleri yalnızca kutunun yazısını düzenler.
- **Gün sonu:** pazarlık sürerken gün bitmez (mevcut kural); Listeler ekranındaki "Günü Bitir" Türkçe mesaj verir.

## Bu adımda yapılmayanlar
Raf/envanter ekranı (yalnızca durum doğru güncellenir ve mesaj gösterilir), müşteri/satış, gün sonu ekonomisi, yeni pazarlık kuralı, Save/Load, TMP. Koz kartının "etkili/yanlış alarm" sonucu `NegotiationView`'da yoktur, bu yüzden ekranda gösterilmez (yalnızca kartın kullanıldığı ve fiyat değişimi).

## Test
Bkz. rapor: `UiFlowNegotiationTests` (açılış, devam, başka ilan, teklif, karşı teklif, son fiyat, satın alma, tekrar alamama, yetersiz nakit — düşük sermayeli içerikle —, vazgeçme, koz, teklif kutusu doğrulaması, ekran dışı komutlar, satır uzunluğu) ve `TurkishTextsTests`. Kayıtlı mutation: pymut + elle mutantlar; eşdeğer olanlar sadeleştirilerek kaldırıldı. Satıcı fiyatının hiç oynamadığı bir Active tur gerçek içerikte oluşmaz (27.000 teklifte 0), `Reply` mantığı bu yüzden doğrudan birim testle sınanır.
