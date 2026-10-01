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
