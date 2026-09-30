# ESNAF – v0.3: Unity Geliştirme Hazırlık Dokümanı (Teknik Mimari)

Sürüm: 0.3 · **Durum: TASARIM FİNAL — 21 nihai karar Bölüm 14'te. Uygulama açıklamaları (UA1–UA3) Bölüm 15'te ve ilgili satırlarla çelişirse Bölüm 15 geçerlidir.**
Bu doküman **son büyük tasarım aşamasıdır.** Bundan sonra yeni özellik eklenmez; kodlamaya geçilir.
Kapsam: yalnızca **telefoncu MVP**. Kod yok; sadece mimari, plan ve test tasarımı.
Öncelik sırası: v0.3 > v0.2 > v0.1. v0.2'nin **13.2 ve 13.3** bölümleri bu dokümanın Bölüm 1'i ile değiştirilmiştir. Diğer tüm v0.2 kuralları geçerlidir.

> Sade dil notu: Bu dokümanda teknik terimler ilk geçtiği yerde açıklanır. "Not:" satırları başlangıç seviyesi geliştirici içindir.

---

## 0. Kesinleşen kararlar

### 0.1 Senin kararların

| # | Karar | Uygulandığı yer |
|---|-------|-----------------|
| 1 | İlk hafta sermaye büyümesi **%5–10** (çok iyi oyuncu biraz fazla, kötü oyuncu zarar edebilir, büyük servet yok). Müşteri sayısı artırılmadan, marj ve karar kalitesiyle. | Bölüm 1 |
| 2 | Para Defteri **özeti Gün 1**, **tam detay ekranı Gün 3**. Filtre/sektör analizi sonra. | Bölüm 2 |
| 3 | Defter türleri genişletilebilir; **kredi MVP'de yok**, mimaride yalnızca eklenti noktaları var. | Bölüm 2.3, 4.8 |

### 0.2 Bu dokümanda kesinleştirdiğim teknik kararlar

| # | Konu | Karar |
|---|------|-------|
| T1 | Unity sürümü | Kurulum anındaki **en güncel Unity LTS**. Proje bir sürüme sabitlenir, MVP bitene kadar güncellenmez. |
| T2 | Şablon | **2D (URP)**, UI ağırlıklı. |
| T3 | UI teknolojisi | **uGUI + TextMeshPro** (UI Toolkit değil). Başlangıç için daha çok kaynak/örnek var. |
| T4 | Kod katmanları | Oyun kuralları **Unity'den bağımsız düz C#** olarak yazılır. Unity'ye bağlı kod yalnızca içerik yükleme ve UI/sahne katmanındadır. |
| T5 | Assembly sayısı | **5** (Core, Domain, Persistence, Content, App) + test assembly'leri. Fazlası yok. |
| T6 | Oyun kurallarına giriş | **Komut/Sorgu arayüzü (`IGameApi`)**. UI, simülasyon aracı ve testler oyunu aynı kapıdan oynar. |
| T7 | Sistemler arası iletişim | Durum değişikliği: **doğrudan çağrı (constructor'la verilen servisler)**. Bildirim: **EventBus** (UI, görev, XP için). Singleton yok. |
| T8 | Rastgelelik | Kendi **PCG tabanlı, seed'li** üretecimiz; sistem başına ayrı akış. `UnityEngine.Random` ve `System.Random` **yasak**. |
| T9 | Para | `long` TL (kuruş yok). Fiyatlar **10 TL'ye yuvarlanır**; iç hesaplar `double`. |
| T10 | **İçerik verisi** | **JSON birincil kaynak** (senin tercihinden sapıyorum, Bölüm 4.1'de gerekçe). ScriptableObject yalnızca Unity varlıkları (ikon, ses, tema) ve katalog için. |
| T11 | Oyuncu kaydı | **JSON** (Newtonsoft.Json), sürümlü, checksum'lı, yedekli, atomik yazma. |
| T12 | Kayıt referansları | Kayıt **yalnızca ID + sayı** tutar; tanımlar kayda kopyalanmaz. İçerik ID'leri **asla silinmez/yeniden adlandırılmaz.** |
| T13 | Sahneler | **3 sahne:** Boot, MainMenu, Game. Ekranlar prefab panelidir; her ekran için sahne yok. |
| T14 | Simülasyon aracı | Unity'siz çalışan **.NET konsol uygulaması**; aynı `Domain` kodunu ve aynı JSON dosyalarını kullanır. |
| T15 | Test | NUnit (Unity Test Framework ile aynı). Kural testleri Unity açmadan **`dotnet test`** ile de çalışır. |
| T16 | Gün sonu | Sıralı **adım boru hattı** (`IDayEndStep`). Faiz/taksit gibi sistemler sonra yeni adım olarak takılır. |
| T17 | Pazarlık kaydı | Pazarlık ortasında uygulama kapanırsa **kaldığı yerden devam eder** (pazarlık durumu kayda girer). |
| T18 | Dil | Yalnızca TR; ama tüm metinler **anahtar + tablo** (koda gömülmez). |
| T19 | Kod stili | C# 9 uyumlu, `record`/`init` yok (Unity uyumu), sınıf başına tek dosya, `PascalCase` genel, `_camelCase` özel alanlar. |
| T20 | Ortam | Bu bulut ortamında **Unity ve dotnet kurulu değil.** Unity'yi kendi bilgisayarında kuracaksın; ben `dotnet` kurabilirsem Domain testlerini burada çalıştırıp sonucu raporlarım (kurulamazsa bunu açıkça söylerim). |

> **Not (T10 hakkında):** Bu tek kararda tercihinden saptım. Sebep: ekonomi simülasyon aracının (Bölüm 5) Unity olmadan çalışabilmesi ve tasarım verilerinin git'te okunur/karşılaştırılabilir olması. ScriptableObject'ler Unity dışında okunamaz. Senin "editörde rahat düzenleme" ihtiyacını doğrulama aracı ve tablo şablonlarıyla karşılıyorum (Bölüm 4.1). Kesin itirazın varsa **kodlamaya başlamadan önce** söyle; sonradan değiştirmek pahalı.

---

## 1. Denge yaması: ilk hafta hızı (%5–10)

### 1.1 Sorunun kaynağı

v0.2 örneğindeki oyuncu 7 günde yalnızca **8 satış** yaptı (%33 müşteri dönüşümü) ve %3,3 büyüdü. Sorun marjın düşüklüğünden çok **fırsat sayısının ve işlem hacminin düşük kalması**ydı. Müşteri sayısı formülüne dokunmuyorum.

### 1.2 Değişiklikler (tümü veri dosyasında)

| # | Değişiklik | Eski (v0.2) | Yeni (v0.3) | Etki |
|---|------------|-------------|-------------|------|
| P1 | Günlük **makul fırsat** garantisi (gerçek ret fiyatı ≤ 0,90 × değer olan ilan) | ≥ 1 | **≥ 2** (Gün 1: 1 rehberli) | Ekspertiz ve pazarlık ustası daha çok kâr bulur |
| P2 | Gün 1–4 satıcı ağırlığı: Aceleci, Bilgisiz, Dürüst | eşit | **%45** | Öğrenme haftasında adil fırsat |
| P3 | **Yüksek fırsat kotası** (ret oranı < 0,85 olan satıcı, "jackpot") | – | **≤ 1/gün** (Gün 6+: ≤ 2) | Kolay para basmayı sınırlar |
| P4 | **Zengin/koleksiyoncu müşteri kotası** | – | **≤ 1/gün** | Jackpot satışı sınırlar |
| P5 | Tuzak garantisi | Gün 5+: ≥ 1 | Gün 5+: **≥ 1**, Gün 6+: **1–2** | Ekspertizin değerini korur |
| P6 | Marj hedefleri (brüt) | %5–12 | Disiplinli oyuncu **%12–15**, ortalama oyuncu **%8–11**, dikkatsiz **< %5** | Bölüm 1.4 |

Değişmeyenler: müşteri sayısı formülü (`2 + ⌈raf × 0,5⌉`, üst sınır 5), dükkân primi (%5–12), gider (500), bekleme kaybı (%0,2/gün), satış baskısı (×0,985), tüm para basma engelleri (v0.2 10.4).

> **Kural:** Hiçbir "her zaman kâr" stratejisi olmamalı. Fırsat kotası, müşteri kotası ve satış baskısı birlikte bunu sağlar. Bunu simülasyon aracı sayısal olarak doğrulayacak (Bölüm 5.6).

### 1.3 Yeni örnek hafta: "Aktif dengeli oyuncu" (11 satış, 7 gün)

Gider Gün 3'ten itibaren 500 TL. Ekspertiz ücretleri ürün maliyetine eklenir (boşa olan gider sayılır). Raf yatırımı ve test cihazı gider değildir, varlıktır. Fiyatlar v0.2 senaryolarındaki NPC sonuçlarıyla uyumludur (Gün 5–6'daki Vega S21 alış 15.390 ve satış 19.920 Senaryo 3'ten).

| Gün | Alışlar (maliyet dahil ekspertiz) | Satışlar (maliyet → satış) | Gider | Gün net | Akşam nakit | Stok (maliyet) |
|-----|-----------------------------------|----------------------------|-------|---------|-------------|----------------|
| 1 | Y5 4.800 | Y5: 4.800 → 5.750 | 0 | **+950** | 250.950 | 0 |
| 2 | N1 Lite 3.700 · Vega A3 7.900 · Y5 5.100 | N1 Lite: 3.700 → 4.400 · Y5: 5.100 → 5.900 | 0 | **+1.500** | 244.550 | 7.900 |
| 3 | N3 Pro 9.100+S1 200 · Z5 12.000+S1 200 | Vega A3: 7.900 → 9.000 | 500 | **+600** | 231.550 | 21.500 |
| 4 | Y8 Plus 13.600+S1 200 · *boşa S1 200* | N3 Pro: 9.300 → 10.600 · Z5: 12.200 → 13.700 | 500 | **+2.100** | 241.350 | 13.800 |
| 5 | Vega S21 15.390 · E11 16.000+S2 600 · *raf yatırımı −15.000* | Y8 Plus: 13.800 → 15.700 | 500 | **+1.400** | 209.560 | 31.990 |
| 6 | E13 Pro (A) 25.900+S3 2.000 · Vega A3 8.000+S1 200 · *test cihazı −12.000* | S21: 15.390 → 19.920 · E11: 16.600 → 18.700 | 500 | **+6.130** | 199.580 | 36.100 |
| 7 | N3 Pro (kırık) 4.800+S1 200+tamir 1.800 | E13 Pro: 27.900 → 29.900 · Vega A3: 8.200 → 9.300 | 500 | **+2.600** | 231.480 | 6.800 |

Hesap kontrolü (elle doğrulandı):
- Satış toplamı **142.870** · satılanların maliyeti **124.890** → **brüt kâr 17.980 (%14,4)**.
- − boşa ekspertiz 200 − gider 2.500 → **net +15.280**.
- Toplam servet: 231.480 nakit + 6.800 stok + 27.000 (raf+ekipman) = **265.280 → %+6,1**. ✔ hedef aralığında.
- Müşteri dönüşümü: 11 satış / ≈ 24 müşteri ≈ %46 (müşteri sayısı formülü değişmedi).
- Gün 6'daki Selin→Berk ikilisi (+4.530) **kotayla sınırlı jackpot**. Onsuz aynı oyuncu ≈ **+10.750 (%4,3)**. Yani şans olmadan da hedefe yaklaşır, şansla aralığın ortasına çıkar. Simülasyon (Bölüm 5) bu dağılımı doğrulayacak.

### 1.4 İlk hafta hedefleri (v0.2 13.3'ün yerine geçer)

Hedef ölçüsü **toplam servet değişimi** (250.000 TL üzerinden).

| Oyuncu tipi | Davranış | 7 gün servet değişimi | Yüzde |
|-------------|----------|------------------------|-------|
| **Kusursuz oyun (tahmini üst uç)** | Tüm fırsatları doğru okur | ≈ +30.000 | **≈ %12** |
| **Çok iyi** | Fırsatları büyük oranda bulur | +22.000 … +30.000 | %9–12 |
| **Aktif dengeli** | Riskli ilana S1/S2, seçici | **+12.500 … +25.000** (tipik ≈ +15.000) | **%5–10** |
| **Temkinli** | Az ve emin işlem | +5.000 … +12.000 | %2–5 |
| **Ekspertiz fanatiği** | Her ilanda S2/S3 | 0 … +10.000 | %0–4 |
| **Kumarbaz** | Ekspertizsiz, tuzaklara atlar | −20.000 … +25.000 (ort. ≈ +3.000) | −%8 … +%10 |
| **Kötü kararlar** | Aşırı pahalı alım, ekspertizsiz | −20.000 … 0 | −%8 … 0 |
| **Pasif** | Neredeyse işlem yapmaz | −2.500 … 0 | −%1 … 0 |

**Alarm eşikleri (simülasyon, Bölüm 5.6):** Herhangi bir strateji ilk haftada ortalama **> %12** veya p90 **> %15** kazanıyorsa simülatör **inceleme alarmı** verir (olası para basma açığı şüphesi). Bu, oyuncuya konmuş bir kazanç sınırı değildir; oyunda kâr tavanı uygulanmaz. Kötü strateji **−%8'in altına** iniyorsa "haksız ceza" incelemesi açılır.

### 1.5 Günlük net kâr bantları (aktif dengeli oyuncu, güncellenmiş)

| Gün | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|-----|---|---|---|---|---|---|---|
| Net kâr | +500…+1.500 | +800…+2.500 | −300…+1.800 | 0…+3.000 | −500…+3.000 | −1.000…+7.000 | −500…+4.000 |

---

## 2. Para Defteri ve genişletilebilirlik

### 2.1 Kademeli açılış

| Gün | Ekran | İçerik |
|-----|-------|--------|
| **1** | **Gün Sonu Özeti içinde "Para Özeti" bloğu** | Sabah nakit, toplam gelir (satış), toplam gider (alış, ekspertiz, gider), net kâr, akşam nakit. Tek ekran, satır yok. |
| **3** | **Para Defteri (tam ekran)** | Tüm işlem satırları (gün, tür, tutar, ürün/NPC), gün gün geriye kaydırma, "neden bu kadar?" açıklaması |
| İleride | Filtre, sektör bazlı analiz, kâr grafiği | MVP dışı |

Her iki ekran **aynı veriden** beslenir (`TransactionRecord` listesi). Özet, kayıtlardan hesaplanır; ayrıca saklanmaz.

### 2.2 Kâr ve servet tanımı

v0.2 9.2 kuralları aynen geçerli: `Net Kâr = Σ(satış − ürün maliyeti) − boşa ekspertiz − gider`, `Ürün Maliyeti = alış + ekspertiz + tamir`. Servet = nakit + stok (maliyet) + işletme varlıkları.

### 2.3 Kredi hazırlığı (uygulanmayacak, yalnızca yer bırakılacak)

MVP'de **kredi, borç, taksit, faiz, varlık finansmanı yok.** Sonradan eklenmeleri için yalnızca şu **eklenti noktaları** bırakılır (boş sınıf/alan yaratmıyoruz; sadece mimariyi buna uygun kuruyoruz):

| Eklenti noktası | MVP'de ne var? | Sonra ne eklenecek? |
|-----------------|-----------------|--------------------|
| **`TransactionType` = veri tablosu** (`transaction_types.json`) | 8 tür (alış, satış, ekspertiz…) | `loan_disbursement`, `loan_repayment`, `interest`, `installment` satırları eklenir |
| **`IDayEndStep` boru hattı** | Gider, bekleme kaybı, talep, ilan ömrü adımları | `InterestAccrualStep`, `InstallmentCollectionStep` eklenir |
| **`IWealthContributor` arayüzü** | Nakit, stok, işletme varlığı katkıda bulunur | `LiabilityContributor` (borç, negatif katkı) eklenir |
| **`SaveVersion` + migration** | v1 | v2 "Liabilities" alanını ekleyen migrasyon |
| **Defter satırında `Category` ve `Account`** | Category: `trade/expense/investment`; Account: `cash` | Category: `financing`; Account: `loan:<id>` |
| **Açılım kuralları (unlock rules)** | Görev/seviye/gün koşulları | "Kredi" koşulu (kredi açılışı) |

Gerçek kredi hesaplamaları ve banka/kredi NPC'si **v0.3 kapsamı dışıdır.**

---

## 3. Unity mimarisi

### 3.1 Katmanlar ve assembly'ler

**Assembly (asmdef)** = Unity'de kodu "paketlere" bölen dosya. Bir paket yalnızca izin verdiğin paketlerin kodunu görebilir. Böylece yanlışlıkla kural kodundan UI koduna bağımlılık kurulamaz.

```
┌──────────────────────────────────────────────────────────────┐
│  Esnaf.App        (Unity) Bootstrap, Sahneler, UI, Ses       │  ← UnityEngine görür
├──────────────────────────────────────────────────────────────┤
│  Esnaf.Content    (Unity) JSON yükleme, SO katalog, ikonlar  │  ← UnityEngine görür
├──────────────────────────────────────────────────────────────┤
│  Esnaf.Persistence (düz C#) Save/Load, migrasyon, yedek      │
├──────────────────────────────────────────────────────────────┤
│  Esnaf.Domain     (düz C#) TÜM OYUN KURALLARI + GameSession  │  ← Test edilir, simüle edilir
├──────────────────────────────────────────────────────────────┤
│  Esnaf.Core       (düz C#) Money, Random, EventBus, Id, Log  │
└──────────────────────────────────────────────────────────────┘
```

| Assembly | UnityEngine görür mü? | Bağımlı olduğu | İçerik |
|----------|-----------------------|----------------|--------|
| `Esnaf.Core` | **Hayır** (`noEngineReferences`) | – | Temel yapı taşları |
| `Esnaf.Domain` | **Hayır** | Core | Sistemler, durum sınıfları, tanım sınıfları, `GameSession`, `IGameApi` |
| `Esnaf.Persistence` | **Hayır** | Core, Domain | Serileştirme, migrasyon, yedekleme, dosya soyutlaması |
| `Esnaf.Content` | Evet | Core, Domain | `TextAssetContentSource` (TextAsset → metin), katalog SO, editör menüsü (UA1) |
| `Esnaf.App` | Evet | hepsi | Composition root, sahneler, UI, ses, cihaz servisleri |
| `Esnaf.Domain.Tests` | Hayır | Core, Domain | Kural testleri |
| `Esnaf.Persistence.Tests` | Hayır | Core, Domain, Persistence | Kayıt testleri |
| `Esnaf.App.PlayTests` | Evet | hepsi | Az sayıda UI duman testi |

> **Not:** "noEngineReferences" işaretli assembly'ler Unity olmadan `dotnet` ile de derlenir. Simülasyon aracı ve hızlı testler bundan yararlanır. Bu MVP'nin **en önemli mimari kararıdır.**

### 3.2 Sistemler ve sorumlulukları

Her sistem `Esnaf.Domain` içinde ayrı klasör/namespace'tir (UI hariç). Sistem = düz C# sınıf(lar); sahip olduğu durumu (state) yönetir.

| Sistem | Sorumluluk | Sahip olduğu durum | Kullandığı sistemler | Yayınladığı olaylar |
|--------|-----------|--------------------|-----------------------|----------------------|
| **Core** | Para, ID üretimi, seed'li rastgelelik, EventBus, günlükleme arayüzü | (sistem başına `RngState`) | – | – |
| **Products** | Ürün tanımları, ürün örnekleri, **değer hesabı**, durum profilleri, örnek üretici | `InstanceStore` | Core, Content'ten gelen tanımlar | – |
| **NPC** | NPC tanımları (10 profil), NPC hafızası (kaç kez görüştük, kime sattık) | `NpcStateStore` | Core | – |
| **Market** | İlan üretimi (fırsat/tuzak kotaları), ilan ömrü, ilan listesi | `MarketState` | Products, NPC, Economy (talep), Core | `ListingsGenerated`, `ListingExpired` |
| **Appraisal** | Ekspertiz seviyeleri, tespit/yanlış alarm, aralık hesabı, kilitli sonuç, risk kartı, koz kartı üretimi | `KnowledgeState` (ekspertiz sonuçları) | Products, Economy (ücret), Core | `AppraisalCompleted` |
| **Negotiation** | Pazarlık motoru (alış ve satış), durum makinesi, koz kartı etkileri. **Saf hesap:** Ürün/NPC bilmez, kendisine verilen "setup" ile çalışır | `NegotiationState` (aktif pazarlık) | Core | `NegotiationStarted`, `OfferMade`, `NegotiationEnded` |
| **Inventory** | Raf/stok, raf kapasitesi, maliyet tabanı (cost basis), etiket fiyatları | `InventoryState` | Products, Economy | `ItemAddedToShelf`, `ItemPriced` |
| **Economy** | Nakit, **defter (Ledger)**, talep endeksleri, satış baskısı, servet hesabı, gider kuralları | `EconomyState` | Core | `TransactionRecorded`, `CashChanged` |
| **Business** | Dükkân (raf kapasitesi, ekipman, itibar-prim bağlantısı), tamir, müşteri üretimi | `BusinessState` | Products, Inventory, Economy, NPC, Core | `CustomerArrived`, `ItemRepaired`, `UpgradePurchased` |
| **Trade** (orkestratör) | "Alış/Satış/Ekspertiz yap" iş akışları: Market + Appraisal + Negotiation + Inventory + Economy'yi sırayla çağırır | – | hepsi | `ListingPurchased`, `ItemSold` |
| **Time/Day** | Gün sayacı, **gün sonu boru hattı** (`IDayEndStep`) | `TimeState` | Economy, Inventory, Market, Business | `DayStarted`, `DayEnded` |
| **Progression** | XP, seviye, açılım kuralları, görevler (Gün 1–7), başarımlar, **öğretici yönetmeni** | `PlayerState`, `ProgressState` | Olaylar (dinler) | `XpGained`, `LevelUp`, `FeatureUnlocked`, `QuestCompleted` |
| **Save** (Persistence) | Kayıt/yükleme/yedek/migrasyon | – | Domain (durum toplama) | `GameSaved`, `GameLoaded` |
| **Events** | Tipli senkron EventBus | – | – | – |
| **UI** (App) | Ekranlar, sunucular (presenter), navigasyon | Yalnızca görsel durum | `IGameApi`, EventBus | – |
| **Content** | JSON ayrıştırma + doğrulama (`ContentParser/Validator/Database`, **Domain'de**), Unity tarafı: TextAsset okuma, ikon/ses eşlemesi (UA1) | – | Domain tanım tipleri | – |

**`GameSession`** bütün sistemleri bir arada tutan **tek nesnedir** (oyunun "kasası"). Oyun başlarken veya kayıt yüklenirken kurulur; `IGameApi`'yi sunar.

### 3.3 İletişim kuralları (en önemli bölüm)

| Kural | Açıklama |
|-------|----------|
| **K1: Komut/Sorgu kapısı** | UI ve simülasyon oyuna yalnızca **`IGameApi`** üzerinden dokunur. UI hiçbir sistemin iç sınıfına doğrudan erişmez. |
| **K2: Komut = niyet** | `StartAppraisal(listingId, level)`, `MakeOffer(offer)`, `BuyListing(...)`, `SetPrice(item, price)`, `EndDay()`… Her komut `Result` döner (başarı/hata nedeni). |
| **K3: Sorgu = salt okunur** | `GetListings()`, `GetInventory()`, `GetLedgerSummary()`… Durum değiştirmez. Döndürülen nesneler **kopyadır/salt okunur görünümdür.** |
| **K4: Durum değişikliği doğrudan çağrıyla** | Bir sistem başka bir sistemin durumunu değiştirecekse onun **arayüzünü** çağırır (constructor'a verilmiş). Örn. `Trade` → `Economy.Spend(...)`. |
| **K5: EventBus yalnızca bildirim** | "Bir şey oldu" der. Oyun **doğruluğu** olaylara bağlı olmaz. Olaylara tepki verenler: UI, Progression (XP/görev), öğretici, analitik. |
| **K6: Olay sırası deterministiktir** | Dinleyiciler composition root'ta **sabit sırayla** kaydolur. Olay işleme senkrondur, gecikme yoktur. |
| **K7: Singleton yok** | Global `Instance` yok. Bağımlılıklar constructor'la verilir. (Test ve simülasyon için şart.) |
| **K8: Zaman = oyun günü** | Gerçek saat (`DateTime.Now`) kural kodunda **yasak.** Yalnızca kayıt meta verisinde kullanılır. |
| **K9: Rastgelelik = enjekte** | Her sistem kendi `IRandom` akışını alır. `UnityEngine.Random` yasak. |
| **K10: Tanımlar değişmez** | Tanım (definition) nesneleri yüklendikten sonra **salt okunurdur.** |

**Bağımlılık matrisi** (satır, sütunu kullanabilir mi?):

| ↓ kullanır → | Core | Products | NPC | Economy | Inventory | Appraisal | Negotiation | Market | Business | Trade | Time | Progression |
|--------------|:----:|:--------:|:---:|:-------:|:---------:|:---------:|:-----------:|:------:|:--------:|:-----:|:----:|:-----------:|
| **Products** | ✔ | | | | | | | | | | | |
| **NPC** | ✔ | | | | | | | | | | | |
| **Economy** | ✔ | | | | | | | | | | | |
| **Inventory** | ✔ | ✔ | | ✔ | | | | | | | | |
| **Appraisal** | ✔ | ✔ | | ✔ | | | | | | | | |
| **Negotiation** | ✔ | | | | | | | | | | | |
| **Market** | ✔ | ✔ | ✔ | ✔ | | | | | | | | |
| **Business** | ✔ | ✔ | ✔ | ✔ | ✔ | | | | | | | |
| **Trade** | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | | | |
| **Time** | ✔ | | | ✔ | ✔ | | | ✔ | ✔ | | | |
| **Progression** | ✔ | | | | | | | | | | | (olay dinler) |

Boş hücre = **kullanamaz.** Döngüsel bağımlılık yoktur. Ör. `Negotiation` kimseyi bilmez; `Economy` kimseyi bilmez.

### 3.4 Gün sonu boru hattı (`IDayEndStep`)

Sıra sabittir; her adım tek iş yapar:

| # | Adım | Sahibi |
|---|------|--------|
| 1 | Kaçan müşterileri kaydet | Business |
| 2 | Günlük gider (Gün 3+) | Economy |
| 3 | Stok bekleme değer kaybı | Inventory |
| 4 | İlan ömrü −1, süresi bitenleri kaldır | Market |
| 5 | Talep endeksleri güncelle (Gün 5+) | Economy |
| 6 | XP/görev/başarım kontrolü | Progression (olay ile) |
| 7 | Yeni günü başlat, yeni ilanlar üret | Time + Market |
| 8 | Otomatik kayıt isteği | Save |

Yeni adım eklemek = sınıf yazıp listeye eklemek. Mevcut adımlar değişmez.

### 3.5 Olay kataloğu (MVP)

`DayStarted, DayEnded, ListingsGenerated, ListingExpired, AppraisalCompleted, NegotiationStarted, OfferMade, NegotiationEnded, ListingPurchased, ItemAddedToShelf, ItemPriced, CustomerArrived, ItemSold, ItemRepaired, UpgradePurchased, TransactionRecorded, CashChanged, XpGained, LevelUp, FeatureUnlocked, QuestCompleted, AchievementUnlocked, GameSaved, GameLoaded`

Yeni olay = yeni küçük sınıf. UI yalnızca ilgilendiği olaylara abone olur.

---

## 4. Veri mimarisi

### 4.1 JSON mu, ScriptableObject mi? (değerlendirme ve karar)

| Kriter | ScriptableObject (SO) | JSON | Karar |
|--------|----------------------|------|-------|
| Unity dışında okunabilir mi (simülasyon, CI, `dotnet test`) | **Hayır** | **Evet** | JSON |
| Git'te okunur/karşılaştırılır mı | Zor (Unity YAML, GUID'ler) | **Kolay** | JSON |
| Toplu düzenleme (10 model, 10 NPC, tablolar) | Zahmetli (envanter tek tek) | **Kolay** (tablo/elektronik tablodan) | JSON |
| Tasarımcı dostu düzenleme (Inspector) | **Evet** | Elle yazım, yazım hatası riski | SO'nun avantajı |
| Tip güvenliği/doğrulama | Otomatik (alan tipleri) | Kendi doğrulamamız gerekir | Doğrulama aracıyla çöz |
| Görsel/ses referansı | **Evet** | Hayır | SO (katalog) |
| Saf C# ile kullanım | Hayır | **Evet** | JSON |
| Denge deneyi (parametre taraması) | Zor | **Kolay** (dosya/override) | JSON |
| Mobil çalışma zamanı | Hızlı | Yükleme sırasında parse (küçük veri, ihmal edilebilir) | Eşit |

**Karar: Hibrit, JSON birincil.**

1. **İçerik/denge verisi = JSON** (modeller, NPC'ler, çarpan tabloları, ekspertiz kuralları, ekonomi sabitleri, görevler). Dosyalar `Assets/_Project/Content/Data/` içinde; Unity'ye **`TextAsset`** olarak girer.
2. **ScriptableObject = yalnızca Unity varlık bağlama:** `ContentCatalog` SO, ID → ikon/sprite/ses eşlemesini tutar (ör. `phone.elma_e13_pro` → ikon). Sayısal/kural verisi SO'da olmaz.
3. **Oyuncu verisi = JSON kayıt** (Bölüm 6).
4. **Yazım hatası riskine karşı:** Editörde `Esnaf → İçeriği Doğrula` menüsü; simülasyon aracı da her çalışmada doğrular; `content_id_manifest.json` ile ID silinmesi engellenir. Şablon (boş örnek) dosyaları `docs/content-templates/` altında.
5. **Bir SO'yu tamamen bırakmadığımızın notu:** İleride sıra "görsel içerik zenginleşince" SO'lar artacaktır; sayısal veri JSON'da kalır.

### 4.2 Tanım (Definition) ve Durum (State) ayrımı

```
DEĞİŞMEZ (Tanım)                            DEĞİŞEN (Durum) → KAYDA GİRER
─────────────────────────                   ───────────────────────────────
ProductDefinition  (Elma E13 Pro)    ◄─ID── ProductInstance  (Pil %78, ekran değişmiş…)
NPCDefinition      (Kemal Abi)       ◄─ID── NPCState         (görüşme sayısı, kime sattık)
(değer/ekspertiz tabloları, sabitler)       AppraisalResult  (ekspertiz çıktısı)
                                            MarketListing    (ilan)
                                            NegotiationState (aktif pazarlık)
                                            PlayerState / BusinessState / EconomyState
                                            TransactionRecord (defter satırı)
```

**Altın kural:** Durum, tanımı **yalnızca ID ile** gösterir (`"definitionId": "phone.elma_e13_pro"`). Tanımın alanları kayda **kopyalanmaz.** Bir modelin baz fiyatını denge için değiştirirsen eski kayıtlar da yeni fiyatı kullanır.

### 4.3 Tipler ve sahiplik

| Tip | Tür | Kayda girer mi? | Sahibi | Ana alanlar |
|-----|-----|-----------------|--------|-------------|
| `ProductDefinition` | Tanım | Hayır | Content/Products | id, sektör, ad, marka, segment, çıkış yılı, baz fiyat, hafıza seçenekleri, yaş aralığı, ikon anahtarı |
| `ProductInstance` | **Durum** | **Evet** | Products (`InstanceStore`) | instanceId, definitionId, hafıza, yaş (ay), **gerçek nitelikler** (pil, ekran, kasa, kamera, kutu, fatura), gizlilik bayrakları, kaynak (satıcı NPC, ilan, gün), **alış fiyatı, maliyet tabanı**, konum (Market/Inventory/Sold) |
| `MarketListing` | Durum | Evet | Market | listingId, instanceId, sellerNpcId, istenen fiyat, açıklama etiketleri, ömür (kalan gün), **gizli:** satıcı ret fiyatı R, satıcının "inandığı değer", fırsat/tuzak işareti |
| `NPCDefinition` | Tanım | Hayır | Content/NPC | id, ad, kişilik, satıcı/müşteri sayıları (v0.2 6.1, 6.2) |
| `NPCState` | Durum | Evet | NPC | npcId, görüşme sayısı, oyuncuya sattığı/aldığı ürün ID'leri (arbitraj engeli), ilişki notları |
| `AppraisalResult` | Durum | Evet | Appraisal | resultId, instanceId/listingId, seviye, gün, ödenen ücret, bulgular (parça, ifade, güven, **yanlış alarm mı: gizli**), sayısal aralıklar, değer aralığı (min–max), kullanılan seed |
| `NegotiationState` | Geçici + Kayıt | **Evet (T17)** | Negotiation | rol (alış/satış), tur, sabır, güven, güncel NPC fiyatı, oyuncunun teklifleri, kullanılan kartlar, **gizli R**, sonuç |
| `PlayerState` | Durum | Evet | Progression | ad, seviye, XP, itibar, açılan özellikler, tamamlanan görevler/başarımlar, öğretici durumu |
| `BusinessState` | Durum | Evet | Business | dükkân seviyesi, raf kapasitesi, sahip olunan ekipman, raf yuvaları (instanceId + etiket fiyatı), günlük gider parametresi referansı |
| `EconomyState` | Durum | Evet | Economy | nakit, **defter**, model başına talep endeksi, model başına son 5 gün satış sayacı |
| `TransactionRecord` | Durum | Evet | Economy | id, gün, tür (ID), **tutar (işaretli)**, kategori, hesap, ürün/NPC referansı, açıklama anahtarı + parametreler, işlem sonrası bakiye |
| `KnowledgeState` | Durum | Evet | Appraisal | `instanceId → AppraisalResult` listesi (oyuncunun "bildikleri") |
| `TimeState` | Durum | Evet | Time | gün no, oyun tohumu (seed), rastgele akış durumları |
| `ProgressState` | Durum | Evet | Progression | görev durumları, gün açılım durumu |

**Gizli bilgi ilkesi:** `ProductInstance` **gerçeği** tutar; oyuncunun **bildikleri** `KnowledgeState`/`AppraisalResult`'tadır. UI yalnızca ikincisini gösterir. Gerçek değer hiçbir yere kaydedilmez; her seferinde formülle **hesaplanır.**

### 4.4 Örnek: Tanım vs Örnek (senin örneğin)

**`ProductDefinition`** (içerik dosyasında, `phone_models.json`):

```json
{
  "id": "phone.elma_e13_pro",
  "sector": "phone",
  "name": "Elma E13 Pro",
  "brand": "Elma",
  "segment": "upper",
  "releaseYear": 2023,
  "basePrice": 32000,
  "baseStorageGb": 128,
  "storageOptions": [ { "gb": 128, "mult": 1.00 }, { "gb": 256, "mult": 1.12 }, { "gb": 512, "mult": 1.28 } ],
  "ageMonths": { "min": 6, "max": 36 }
}
```

**`ProductInstance`** (kayıt dosyasında, oyuncunun envanterindeki telefon):

```json
{
  "instanceId": 1042,
  "definitionId": "phone.elma_e13_pro",
  "storageGb": 128,
  "ageMonths": 18,
  "attributes": {
    "battery": 78,
    "screen": "replaced_aftermarket",
    "body": 82,
    "camera": "ok",
    "box": false,
    "invoice": false
  },
  "provenance": { "sellerNpcId": "npc.kemal", "listingId": 311, "acquiredDay": 4 },
  "purchasePrice": 27000,
  "costBasis": 27000,
  "location": "inventory"
}
```

**Değer** kayıtta **yoktur.** Formül (v0.2 Bölüm 4) hesaplar: RF = 32.000 × 1,00 × 0,88 = 28.160 → V = 28.160 × 0,928 × 0,88 × 0,964 × 1,00 × 1,00 = **22.170**.
Bu örnekte oyuncu ekspertizsiz **27.000** ödemiş; gerçek değer 22.170. Yani **−4.830** "fazla ödeme" (oyunun zarar örneği).

### 4.5 Kimlik, para ve rastgelelik kuralları

| Konu | Kural |
|------|-------|
| **Tanım ID'leri** | Metin (`phone.elma_e13_pro`, `npc.kemal`). **Asla silinmez/yeniden adlandırılmaz** (`content_id_manifest.json` ile denetlenir); kaldırılacaksa `"deprecated": true`. |
| **Örnek ID'leri** | Tamsayı (`long`), tek bir sayaçtan (`IdGenerator`); sayaç kayıtta saklanır. |
| **Para** | `Money` = `long` TL. Kuruş yok. Negatif olabilir (gider). |
| **Yuvarlama** | NPC'nin **iç fiyatı** `double`; **gösterilen/anlaşılan fiyat** `round10` (en yakın 10 TL, yarım yukarı). Tüm "golden test" değerleri `round10` ile karşılaştırılır. |
| **Rastgelelik** | `IRandom` (PCG). Ana seed → **adlandırılmış akışlar** türetir: `market`, `npc`, `appraisal`, `negotiation`, `customer`. Bir sistemde fazladan rastgele çağrı, diğer sistemlerin sonucunu değiştirmez. Akış durumları kayıtta saklanır (yükleyince aynı gelecek). |
| **Ekspertiz kilidi** | Ekspertiz seed'i = `hash(masterSeed, instanceId, level)`. Aynı ürün + aynı seviye = aynı sonuç (yeniden deneme yok). |
| **Zaman** | Gün numarası tamsayı; gerçek zaman kural kodunda yok. |

### 4.6 İçerik dosyaları (`Assets/_Project/Content/Data/`)

| Dosya | İçerik | v0.2 kaynağı |
|-------|--------|--------------|
| `phone_models.json` | 10 model | Bölüm 3.2 |
| `condition_profiles.json` | A–E profilleri | 4.3 |
| `value_tables.json` | Yaş/ekran/kamera/paket çarpanları, pil/kasa katsayıları | 4.2 |
| `appraisal_levels.json` | S0–S3: ücret, tespit/yanlış alarm, aralık, kanıt gücü | 5.1–5.2 |
| `npc_profiles.json` | 10 NPC (satıcı + müşteri) | 6 |
| `negotiation_rules.json` | t formülü katsayıları, hakaret eşiği, güven kuralları, kart kuralları | 7 |
| `economy_constants.json` | Gider, kapasite, prim, bekleme kaybı, talep parametreleri, kotalar (P1–P5) | 10 + 1.2 |
| `repair_table.json` | Tamir ücretleri ve etkileri | 12.1 |
| `transaction_types.json` | Defter türleri (genişletilebilir) | 2.3 |
| `progression.json` | XP, seviye eşikleri, açılım kuralları | 12.2 |
| `quests.json` | Gün 1–7 görevleri | 12 |
| `tutorial_scripts.json` | Öğretici metin anahtarları, Gün 1 rehberli ilan | 12 |
| `strings_tr.json` | Tüm arayüz metinleri (anahtar → metin) | – |
| `content_id_manifest.json` | Bugüne kadar var olmuş tüm ID'ler | – |

Her dosyanın en üstünde `"schemaVersion": 1` bulunur. **Doğrulama** şunları denetler: zorunlu alanlar, aralıklar (yüzdeler 0–100, çarpanlar > 0), ID benzersizliği, referans bütünlüğü (NPC → geçerli kişilik), manifest kuralı.

### 4.7 Kod stili özeti (T19)

- Sınıf başına tek dosya; ad = dosya adı.
- Alanlar ve durum sınıfları **düz veri**; iş kuralı sistem sınıflarında.
- Metot başına tek iş; iç içe 3+ seviye `if` yok.
- Hata: istisna yerine `Result` (beklenen hatalar için); istisna yalnızca programcı hatasında.
- Her sistemin herkese açık yüzeyi **küçük arayüz** (`IAppraisalService` vb.).

### 4.8 Kredi genişletme noktalarının veri tarafı

Bölüm 2.3'teki noktaların veri karşılığı: `transaction_types.json` yeni satırlar; `EconomyState` sürüm 2'de `liabilities` listesi; `IDayEndStep`/`IWealthContributor` yeni sınıflar. **MVP'de bunlar yazılmaz.**

---

## 5. Ekonomi simülasyon aracı (tasarım)

### 5.1 Amaç

Bu araç **oyunun ekonomisini oyuncusuz** çalıştırıp denge sorularını sayıyla cevaplar. Kod yazmadan önce tasarımdır; **14. günlük planda Gün 10'da** ilk sürümü yapılır.

| Soru | Araç nasıl cevaplar? | Karar ölçütü |
|------|----------------------|---------------|
| Oyuncu ortalama ne kadar kazanıyor? | "Aktif dengeli" bot, 1.000 tohum × 7 gün, servet değişimi ortalaması ve p10/p50/p90 | %5–10 (Bölüm 1.4) |
| Çok iyi oyuncu ne kadar kazanıyor? | "Uzman" bot | %9–12 |
| Kötü oyuncu ne kadar kaybediyor? | "Kumarbaz" ve "Kötü kararlar" botları | −%8'in altına inmez |
| 7 günde sermaye ne kadar değişiyor? | Tüm botlar için 7. gün servet dağılımı raporu | Tablo 1.4 |
| 30 günde sektör açmak mümkün mü? | 30 günlük koşu; "açılım kuralı" (parametre, ör. servet ≥ X ve Lv ≥ Y) sağlanan gün dağılımı | p50 bot, hedef günden önce sağlar mı? |
| Para basma açığı var mı? | **Sömürü botları** (5.4) ve alarm eşikleri | Ort. > %12 veya p90 > %15 = alarm |
| Sonsuz aynı ürünle sistem kırılıyor mu? | **Tekli ürün botu** ve satış baskısı raporu | ROI, döngü sayısı arttıkça **düşmeli** |

### 5.2 Mimari

```
tools/EconomySim/           (.NET konsol projesi, Unity'siz)
 ├─ Program.cs               komut satırı: senaryo yükle, çalıştır, rapor yaz
 ├─ Bots/                    oyuncu stratejileri (IBot)
 ├─ Scenarios/*.json         senaryo dosyaları
 ├─ Reports/                 CSV / JSON / Markdown çıktı
 └─ Baselines/*.json         referans sonuçlar (regresyon)
        │ (aynı kaynak dosyalar)
        ▼
Assets/_Project/Scripts/Core + Domain   ← Unity ile aynı kod
Assets/_Project/Content/Data/*.json      ← Unity ile aynı içerik
```

- Araç `GameSession`'ı **başsız (headless)** kurar ve botları **`IGameApi`** komutlarıyla oynatır (K1). UI'ın yaptığı her şeyi bot da yapar.
- Bot, oyunu bozacak özel kısayol kullanamaz; yalnızca oyuncunun görebildiği sorguları kullanır (gizli gerçek yok).

### 5.3 Bot stratejileri (oyuncu tipleri)

| Bot | Davranış |
|-----|----------|
| `IdleBot` | Hiçbir şey yapmaz (gider etkisini ölçer) |
| `RandomBot` | Rastgele ilan, rastgele teklif |
| `CarefulBot` | Küçük fırsatları alır; S1/S2 kullanır; sık vazgeçer |
| `BalancedBot` | Risk kartı: orta senaryo kâr ≥ eşik ve kötü senaryo zararı ≤ sınır ise alır |
| `AppraisalMaxBot` | Her ilanda en yüksek seviye ekspertiz |
| `GamblerBot` | Ekspertizsiz, "acil satış" ve "ucuz" etiketlerine atlar |
| `ExpertBot` | Bilgili oyuncu: kartı doğru kullanır, doğru müşteriye doğru fiyat |
| `BadBot` | Aşırı pahalı alım, ekspertizsiz, etiketi çok yüksek |

**Beceri parametreleri** (0–1): değer tahmini hatası, teklif agresifliği, kart kullanma isabeti, etiket fiyatı seçimi. Botlar bu parametrelerle üretilir; "iyi/orta/kötü oyuncu" spektrumu birkaç sayı oynayarak çıkar.

### 5.4 Sömürü (exploit) botları

Bunlar **oyunu kırmaya çalışan** botlardır; başarılı olurlarsa denge bozuktur:

| Sömürü botu | Deneyi |
|-------------|--------|
| `MonoculturBot` | Hep aynı modeli al-sat |
| `LowballSpamBot` | Hep en düşük teklif (hakaret sınırına yakın) |
| `ArbitrageBot` | Aceleci satıcıdan al, zengin müşteriye sat |
| `SameNpcBot` | Aynı NPC'lerle tekrar tekrar işlem |
| `ReportAbuseBot` | Rapor göster mekaniğini her satışta kullan |
| `HoardBot` | Rafa maksimum doldur, fiyat baskısı beklet |
| `QuitRerollBot` | Pazarlıktan çık/yeniden başla (T17 ve ekspertiz kilidi bunu engellemeli) |

**Kural:** Hiçbir sömürü botu, dengeli botun ortalamasını **%20'den fazla** geçmemeli; geçerse ilgili kural (kota, baskı, kilit) sıkılaştırılır.

### 5.5 Senaryo ve parametre değiştirme

- **Senaryo dosyası** (JSON): kaç gün, kaç tohum, hangi botlar, hangi parametre geçersiz kılmaları.
- **Parametre geçersiz kılma:** komut satırından veya senaryo dosyasından, içerik dosyasındaki bir sayıyı **dosyayı değiştirmeden** değiştirir: `--set economy.shop_premium=0.06`.
- **Tarama (sweep):** bir parametreyi aralıkta gezdirir: `--sweep negotiation.t_base=0.15..0.25:0.02`. Her değer için tüm botlar çalışır; sonuç tablo olarak çıkar.
- **Tohum kümesi:** sabit (tekrarlanabilir) veya rastgele; sonuçlar tohumla aynen tekrarlanır.

### 5.6 Çıktılar ve kabul ölçütleri

**Rapor (Markdown + CSV):**

| Bölüm | İçerik |
|-------|--------|
| Özet | Bot başına 7/14/30 gün servet değişimi (ort., p10, p50, p90), iflas oranı |
| İşlem | Gün başına işlem, brüt marj dağılımı, zarar eden işlem oranı, ekspertiz harcaması |
| Davranış | Vazgeçilen ilan oranı, koz kartı kullanma oranı, pazarlık tur ortalaması |
| Sömürü | Sömürü botları / dengeli bot oranı, ROI'nin işlem sayısıyla değişimi |
| 30 gün | Belirlenen açılım kuralını sağlama günü dağılımı |
| Alarm | Aşılan eşikler (kırmızı) |

**Kabul ölçütleri (MVP çıkış şartı):** Bölüm 1.4 tablosu, alarm eşikleri, sömürü kuralı; ek olarak "iflas" (nakit + stok < 20.000) oranı dengeli botta **< %1**.

**Regresyon:** Kabul edilen sonuçlar `Baselines/` altına yazılır; sonraki denge değişikliğinde ortalama ±%10'dan fazla sapma **uyarı**, ±%25 **hata** olur. Böylece bir değişiklik ekonomiyi sessizce bozamaz.

### 5.7 Teknik notlar

- Deterministik: aynı tohum + aynı komut dizisi = aynı sonuç (Bölüm 4.5). Komut günlüğü ile hata **yeniden oynatılır (replay).**
- Hız hedefi: 1.000 tohum × 7 gün < 60 sn (tek çekirdek).
- Araç `Domain` içinde Unity API kullanılmadığı için derlenir; kural "noEngineReferences" bunu otomatik zorlar.

---

## 6. Save/Load sistemi

### 6.1 Dosya düzeni

Konum: Unity'nin `Application.persistentDataPath` yolu (yalnızca `Esnaf.App` bilir, `Persistence`'a soyutlama ile verilir).

```
persistentDataPath/saves/
  slot0.json        güncel kayıt
  slot0.bak1.json   bir önceki başarılı kayıt
  slot0.bak2.json   ondan önceki
  slot0.tmp         yazılmakta olan (yarım kalırsa yok sayılır)
  slot0.corrupt-YYYYMMDD-HHMMSS.json   bozuk bulunan kayıt (silinmez, sonra incelenir)
settings.json       ses/dil ayarları (kayıttan bağımsız)
```

MVP: **1 slot** (çoklu slot MVP dışı).

### 6.2 Kayıt biçimi

```json
{
  "header": {
    "format": "esnaf-save",
    "saveVersion": 1,
    "appVersion": "0.1.0",
    "contentSchemaVersion": 1,
    "createdAtUtc": "…", "savedAtUtc": "…",
    "playTimeSeconds": 5230,
    "checksum": "sha256:…",
    "preview": { "day": 4, "cash": 238300, "level": 2, "wealth": 252100 }
  },
  "payload": {
    "time": { … }, "rng": { … }, "player": { … }, "economy": { … },
    "inventory": { … }, "business": { … }, "market": { … },
    "instances": [ … ], "npcStates": [ … ], "knowledge": [ … ],
    "progress": { … }, "activeNegotiation": null
  }
}
```

- `checksum`: `payload` metninin SHA-256 özeti (bozulmayı fark etmek için; hile önleme **değildir**).
- `preview`: ana menüde "Devam Et" kartı için; yükleme yapmadan okunabilir.

### 6.3 Yazma (atomik kayıt)

1. Durumu topla (`Capture`), JSON üret, checksum hesapla.
2. `slot0.tmp` dosyasına yaz, diske boşalt (flush).
3. `tmp`'yi **geri oku ve doğrula** (parse + checksum).
4. Doğruysa: `bak1 → bak2`, `slot0 → bak1`, `tmp → slot0` (taşıma/`Replace`).
5. Herhangi bir adım başarısız olursa: `slot0` **dokunulmaz**, hata günlüğe yazılır, oyuncuya "kayıt yapılamadı" uyarısı (kritik ise).

Uygulama yazarken çökerse `slot0` sağlam kalır (çünkü yalnızca en son adımda değişir).

### 6.4 Ne zaman kaydedilir? (otomatik kayıt tetikleyicileri)

Gün sonu tamamlanınca · alım/satım anlaşması · ekspertiz sonrası · yatırım (raf/ekipman) · ana menüye dönüş · **uygulama arka plana alınırken/kapanırken** (`OnApplicationPause`/`OnApplicationQuit`; pazarlık ortasındaysa `activeNegotiation` da yazılır, T17).
Gün sonu boru hattı **tek işlemdir:** yarıda kalırsa kayıt bir önceki güvenli noktadan geri yüklenir.

### 6.5 Yükleme akışı

```
Yükle
 ├─ slot0.json var mı? ── hayır ── Yedek var mı? ── hayır ── "Kayıt yok" → Yeni oyun
 ├─ Parse edilebilir + checksum doğru + sürüm okunabilir mi?
 │     evet → Migrasyon zinciri (gerekirse) → Doğrulama (referanslar) → Oyun kurulur
 │     hayır → slot0'ı `.corrupt-…` olarak yeniden adlandır
 │              → bak1 dene → bak2 dene
 │                    bulundu → oyuncuya "Son kayıt bozuktu, önceki kayıt yüklendi (Gün X)" 
 │                    hiçbiri → "Kayıt bozuk" ekranı: [Yeni oyun] [Bozuk dosyayı sakla]
 └─ Referans doğrulama: tüm definitionId'ler içerikte var mı? Yoksa: hata + onarım politikası (6.7)
```

### 6.6 Sürümleme ve migrasyon

- `saveVersion`: bir tamsayı; **kayıt biçimi** değiştikçe artar (içerik/denge değişince artmaz; denge değişikliği kayıt uyumunu bozmaz çünkü kayıt yalnızca ID+sayı).
- **Migrasyon zinciri:** `IMigration { int From; int To; void Apply(JObject payload); }`. Kayıt önce `JObject` olarak okunur; sürüm 1→2→3… sırayla dönüştürülür, sonra tipli nesneye çevrilir.
- **İleri uyumsuzluk:** `saveVersion` uygulamanın desteklediğinden **büyükse** yüklenmez: "Bu kayıt daha yeni sürümle oluşturuldu, oyunu güncelle."
- **Her sürüm için kalıcı test dosyası:** `Tests/Fixtures/save_v1.json`… Her yeni sürümde eski fikstürler yeni koda yüklenip doğrulanır.
- MVP'de yalnızca sürüm **1** vardır; ama migrasyon çerçevesi ve test düzeneği **başta** kurulur (sahte v0→v1 migrasyonuyla test edilir).

### 6.7 İçerik değişince kayıt (uyumluluk)

| Durum | Politika |
|-------|----------|
| Yeni model/NPC eklendi | Sorun yok. |
| Sayı (fiyat, çarpan) değişti | Sorun yok (kayıt yalnızca ID+nitelik). |
| ID silindi/yeniden adlandırıldı | **Yasak** (manifest denetimi). Kaldırma = `deprecated`. |
| Yine de bilinmeyen ID bulundu | Oyuncuya hata göstermeden: ürün "Bilinmeyen ürün" yerine **otomatik iade** (alış fiyatı kadar nakit + defter satırı "İçerik değişikliği iadesi") ve günlüğe uyarı. |

### 6.8 Yeni oyun ve yükleme davranışı

| Akış | Davranış |
|------|----------|
| **Yeni oyun** | Kayıt varsa "Mevcut kayıt silinecek, emin misin?" onayı (mevcut kayıt `.bak` olarak **bir süre saklanır**). Ana tohum rastgele üretilir (geliştirici modunda elle girilebilir). 250.000 TL, Gün 1, öğretici başlar, ilk otomatik kayıt Gün 1 başında. |
| **Devam et** | Kayıt yüklenir; `preview` ile ana menüde Gün/nakit gösterilir. |
| **Kayıt silme** | Ayarlar içinde, çift onaylı; yedekler de silinir. |
| **Ayarlar** | Ayrı `settings.json`; kayıt bozulsa da ayar kaybolmaz. |

### 6.9 Sınırlar ve sonraya bırakılanlar

Şifreleme/hile önleme, bulut kaydı (iCloud/Google), çoklu slot: **MVP dışı.** Beklenen kayıt boyutu < 300 KB (küçük veri; sıkıştırma gerekmez).

---

## 7. Unity sahne yapısı

### 7.1 Karar: 3 sahne

| Sahne | Görevi |
|-------|--------|
| **Boot** | Uygulama başlar: içerik yüklenir ve doğrulanır, ayarlar okunur, kayıt var mı bakılır, `MainMenu`'ye geçer. Görsel: sadece logo. |
| **MainMenu** | Yeni Oyun / Devam Et / Ayarlar. |
| **Game** | Bütün oyun. `GameSession` burada yaşar. Ekranlar prefab panelleri olarak tek Canvas'ta gösterilir. |

### 7.2 Neden "ekran başına sahne" değil?

Senin örnek listenin (PhoneMarket, Shop, Inventory, Negotiation, DaySummary) tamamı **`Game` sahnesinin içindeki ekranlar (panel/prefab)** olur. Gerekçe:
- Sahne geçişi durumu (seçili ilan, açık pazarlık) kaybettirir ve mobilde yavaştır.
- Tek `GameSession` ve tek EventSystem ile daha az hata.
- Prefab panelleri ile test ve iterasyon hızlanır.

Gerekirse ileride **additive** (üst üste) sahne eklenebilir; MVP'de gerek yok.

### 7.3 `Game` sahnesi hiyerarşisi

```
Game (Scene)
├─ [Bootstrap]        GameBootstrap (GameSession kurar, kayıt yükler)
├─ Canvas (Screen Space - Overlay, 1080×1920 ref.)
│   ├─ SafeArea
│   │   ├─ HudBar             Gün · Para · Günü Bitir
│   │   ├─ ScreenHost         aktif ekran(lar)
│   │   └─ BottomNav          Pazar · Dükkân · Defter · Görevler
│   ├─ ModalLayer             pazarlık, ekspertiz sayfası, onay pencereleri
│   └─ ToastLayer             bildirimler
├─ EventSystem
└─ AudioRoot (MVP'de boş/basit)
```

---

## 8. UI mimarisi (mobil dikey)

### 8.1 Genel ilkeler

- Referans çözünürlük **1080×1920**, `Canvas Scaler`: *Scale With Screen Size*, eşleşme 0,5. Güvenli alan (çentik/gesture bar) için `SafeArea` bileşeni.
- **Tek ana eylem** buton/ekran; dokunma hedefi **≥ 48 dp (~ 132 px)**.
- Ana eylemler alt yarıda (tek elle kullanım); "bottom sheet" kalıbı.
- Android geri tuşu: `ScreenManager` yığınından bir ekran geri.
- MVP görselleri: düz renkli kutular, basit ikonlar, okunaklı yazı. Süs yok.

### 8.2 Ekran envanteri ve türleri

| Ekran | Tür | Açılış |
|-------|-----|--------|
| Ana menü | (ayrı sahne) | Başlangıç |
| **HUD** (Gün, Para, Günü Bitir) | Kalıcı üst çubuk | Gün 1 |
| **Pazar** (ilan listesi) | Ana sekme | Gün 1 |
| **Ürün Detay** | Tam ekran, Pazar'dan | Gün 1 |
| **Ekspertiz** | Alt sayfa (bottom sheet) | Gün 3 (S1), Gün 5 (S2), Gün 6 (S3) |
| **Pazarlık** | Tam ekran modal (alış ve satış aynı ekran) | Gün 1 |
| **Dükkân** (envanter + etiket + gelen müşteriler) | Ana sekme | Gün 1 |
| **Satış** (müşteri pazarlığı) | Pazarlık ekranının satış modu | Gün 1 (basit), Gün 2 (tam) |
| **Gün Sonu** | Tam ekran özet | Gün 1 |
| **Para Defteri** | Ana sekme (detay) | Gün 3 |
| **Görevler** | Ana sekme | Gün 1 (tek görev), Gün 3+ tam |
| Tamir, Yatırım | Dükkân içinde alt sayfa | Gün 5, 6, 7 |
| Ayarlar/Duraklat | Modal | Gün 1 |

### 8.3 Ekran bağlantı akışı

```
        ┌──────────────────────────── HUD (Gün · Para · [Günü Bitir]) ──────────────────────────┐
        │                                                                                         │
 [Pazar] ──tıkla──► [Ürün Detay] ──► ekspertiz kararı ──► [Ekspertiz sayfası] ──► sonuç/risk kartı
                         │                                                                │
                         └──────────────► [Pazarlık (alış)] ◄── koz kartları ─────────────┘
                                                 │ anlaşma
                                                 ▼
                        [Dükkân: yeni ürün] ──► etiket fiyatı ──► raf
                                                 │
   müşteri gelir (Dükkân'da "Müşteri" kartı) ────► [Pazarlık (satış)] ◄── "Rapor göster"
                                                 │ satış
                                                 ▼
                                   [Günü Bitir] ─► [Gün Sonu] ─► (Gün N+1 sabah özeti: Pazar)
   [Defter] (HUD'daki para dokunuşuyla veya sekme) · [Görevler] (sekme)
```

### 8.4 Ekran taslakları (dikey, sadeleştirilmiş)

```
HUD + PAZAR                    ÜRÜN DETAY                       EKSPERTİZ (alt sayfa)
┌─────────────────────┐       ┌─────────────────────┐          ┌─────────────────────┐
│ Gün 4  238.300 TL [Bitir]│    │ ‹  Elma E13 Pro      │          │ (ürün detay arkada) │
├─────────────────────┤       │ 128GB · 18 ay        │          ├─────────────────────┤
│ İlanlar (6)         │       │ İstenen: 30.400 TL   │          │ Ekspertiz seç:      │
│ ┌─────────────────┐ │       │ Referans: 28.160 TL  │          │ ○ Göz (ücretsiz)    │
│ │E13 Pro 30.400   │ │       │ Görünen: kutu yok…   │          │ ● S1  300 TL        │
│ │Kemal · 3 gün    │ │       │ Bilinmeyen: pil,     │          │ ○ S2 1.000 TL       │
│ └─────────────────┘ │       │  ekran, kamera…      │          │ ○ S3 (kilitli)      │
│ ┌─────────────────┐ │       │                      │          │ Ne öğrenirsin: …    │
│ │Y8 Plus 12.500   │ │       │ [Ekspertiz Yap]      │          │ [Yaptır −300 TL]    │
│ │Cengiz · ACİL    │ │       │ [Pazarlığa Başla]    │          └─────────────────────┘
│ └─────────────────┘ │       └─────────────────────┘
├─────────────────────┤
│Pazar│Dükkân│Defter│Görev│
└─────────────────────┘

PAZARLIK (alış)                 DÜKKÂN                           GÜN SONU
┌─────────────────────┐       ┌─────────────────────┐          ┌─────────────────────┐
│ Kemal Abi  😐 ▮▮▮▯  │       │ Raf 3/6             │          │ GÜN 4 ÖZETİ         │
│ Fiyat: 26.110       │       │ ┌─────────────────┐ │          │ Nakit 238.300       │
│ Değer aralığı:      │       │ │Y8 Plus  13.800  │ │          │ Satış +10.250       │
│  22.000–24.300      │       │ │Etiket: [15.700] │ │          │ Alış −13.600        │
│ Kozlar: [Ekran ⚠]   │       │ └─────────────────┘ │          │ Ekspertiz −400      │
│ Teklifin: [ 22.500 ]│       │ Müşteriler (2)      │          │ Gider −500          │
│ ◄──────●─────►      │       │ ┌─────────────────┐ │          │ NET KÂR +250        │
│ [Teklif Ver][Vazgeç]│       │ │Berk 😀 ilgileniyor│ │          │ Görev ✔  XP +55     │
└─────────────────────┘       │ └─────────────────┘ │          │ [Detay] [Sonraki Gün]│
                              └─────────────────────┘          └─────────────────────┘
```

Mizaç göstergesi (😐 ▮▮▮▯) NPC'nin ruh hali ve sabrını **niteliksel** gösterir; sayılar görünmez (v0.2 7.1).

### 8.5 UI teknik yapısı

| Bileşen | Sorumluluk |
|---------|-----------|
| `ScreenManager` | Ekran yığını: `Push(screen, args)`, `Pop()`, Android geri tuşu, sekme geçişi |
| `IScreen` (arayüz) | `OnShow(args)`, `OnHide()`; her ekran bir prefab |
| **Presenter** (ekran başına) | `IGameApi`'den sorgular, komut yollar, EventBus'a abone olur; **kural içermez** |
| `View` (MonoBehaviour) | Yalnızca görüntü/dokunma; presenter'a olay iletir |
| `UiTheme` (SO) | Renk, font, boşluk (görsel kimlik sonradan tek yerden değişir) |
| Yeniden kullanılan bileşenler | `ListingCard`, `MoneyLabel`, `PrimaryButton`, `BottomSheet`, `MoodMeter`, `RangeBar`, `ConfirmDialog` |
| **Yerelleştirme** | `Loc.Get("key")` → `strings_tr.json` |

Kural: UI **hiçbir hesap yapmaz** (değer, fiyat, kâr). Gerekli her sayı `IGameApi` sorgusundan gelir.

---

## 9. Test stratejisi

### 9.1 Test türleri

| Tür | Araç | Ne test eder? | Ne zaman çalışır? |
|-----|------|---------------|-------------------|
| **Birim (kural)** | NUnit EditMode / `dotnet test` | Her sistemin hesapları | Her değişiklikte (saniyeler) |
| **Altın sayı (golden)** | Aynı | v0.2 tablo ve senaryolarındaki **sayılar** aynen | Her değişiklikte |
| **Değişmez kurallar (invariant)** | Aynı, çok tohumlu | Sürekli doğru olması gerekenler (aşağıda) | Her değişiklikte |
| **İstatistik** | Aynı, 10.000 deneme | Ekspertiz kapsama oranı, profil dağılımı | Günlük |
| **Kayıt** | Aynı | Gidiş-dönüş, bozuk dosya, migrasyon | Her değişiklikte |
| **Simülasyon (denge)** | EconomySim | Bölüm 5 | Denge değişikliğinde |
| **PlayMode duman** | Unity PlayMode | UI açılır, temel akış çalışır | Günlük |
| **Manuel test listesi** | İnsan | His, okunurluk, mobil cihaz | Her iki hafta / MVP sonu |

### 9.2 Değişmez kurallar (her testte doğrulanır)

| # | Kural |
|---|-------|
| I1 | **Nakit = 250.000 + Σ(defter tutarları)** (her an) |
| I2 | Servet = nakit + stok maliyeti + varlıklar (kimlik eşitliği) |
| I3 | Raf doluluk ≤ kapasite |
| I4 | Anlaşma fiyatı satıcı için `[R, istenen]` aralığında; müşteri için `≤ M` |
| I5 | Ekspertiz sonucu aynı ürün+seviyede **aynıdır** |
| I6 | Aynı tohum + aynı komutlar → aynı durum özeti (hash) |
| I7 | Yükle(Kaydet(durum)) = durum |
| I8 | Ürün örneği tam olarak **bir** konumdadır (Market/Envanter/Satıldı) |
| I9 | Negatif fiyat, negatif stok, negatif sabır yok |
| I10 | İçerik ID'leri manifestte var; referanslar çözülür |

### 9.3 Deterministik örnek senaryolar

Aritmetik testler **denge testi değildir**; yalnızca hesabın doğruluğunu sınar.

**Senaryo T1 (senin örneğin):**

| Adım | Nakit | Envanter | Net kâr |
|------|-------|----------|---------|
| Başlangıç | 250.000 | 0 | 0 |
| 27.000 TL telefon alındı | **223.000** | **1 telefon** (maliyet 27.000) | 0 |
| 35.000 TL'ye satıldı | **258.000** | 0 | **+8.000** |

**Senaryo T2 (ekspertizli):** S2 (üst segment) 1.000 TL → alış 27.000: nakit **222.000**, maliyet tabanı **28.000**. 35.000'e satış → nakit **257.000**, net kâr **+7.000**.

**Senaryo T3 (boşa ekspertiz):** S1 (üst) 300 TL yaptırıldı, alınmadı → nakit **249.700**, envanter 0, gün net kâr **−300** (boşa ekspertiz).

**Senaryo T4 (gider):** Gün 3 sonu, işlem yok → nakit **249.500**, gün net kâr **−500**. Gün 2 sonu: gider **0**.

**Senaryo T5 (yatırım):** Raf yükseltme 15.000 TL → nakit −15.000, **servet değişmez**, raf kapasitesi 6 → 8, net kâr etkisi 0.

**Senaryo T6 (v0.2 haftası):** Bölüm 1.3 tablosundaki her gün için nakit, stok ve gün net kâr değerleri **aynen** doğrulanır (toplam +15.280, servet 265.280).

### 9.4 Sistem bazında test kriterleri

| Sistem | Test | Beklenen |
|--------|------|----------|
| **Money/Round** | `round10(26.115)` | 26.120; `round10(23.651)` = 23.650 |
| **Random** | Aynı seed, ilk 5 değer; farklı seed | Aynı/farklı; akışlar birbirini etkilemez |
| **EventBus** | Abone ol/çık, sıra | Kayıt sırasıyla çağrılır; çıkan çağrılmaz |
| **Content** | 10 model yüklenir; bozuk dosya | Doğrulama hata verir (yinelenen ID, negatif fiyat, eksik alan) |
| **Products (değer)** | E13 Pro 18 ay, profil A/B/C/D/E | **28.820 / 25.100 / 19.860 / 22.310 / 13.260** |
| **Products (üretim)** | 10.000 örnek | Nitelikler aralık içinde; profil dağılımı ±%2; modelin yaş aralığına uyulur |
| **Economy** | Senaryo T1–T5 | Yukarıdaki değerler |
| **Inventory** | Raf 6 doluyken 7. ürün | Reddedilir; maliyet tabanı = alış + ekspertiz + tamir |
| **Market** | 1.000 gün üret | Her gün ≥ 2 fırsat; Gün 5+ ≥ 1 tuzak; jackpot ≤ 1/gün; ilan ömrü 2–4 |
| **Appraisal** | E13 D profili, S1/S2/S3 | Bulgu ve aralık tablosu (v0.2 5.4) tohumla sabit; S1 kaçırma oranı 10.000 denemede %30 ±1,5 |
| **Appraisal (kapsama)** | 10.000 deneme | Gerçek değer aralık içinde: S1 %85 ±3, S2 %92 ±3, S3 %96 ±3 |
| **Negotiation (alış)** | Senaryo 1 (Kemal), 2 (Hatice), 3 (Selin), 4 (Cengiz) | Her turun fiyatı v0.2 tablosuyla (`round10`) aynı; sonuç aynı |
| **Negotiation (kurallar)** | Hakaret, yanlış kart, sabır bitişi | Güven/sabır değişimleri 7.2'ye göre; "Son fiyat" mesajı |
| **Negotiation (satış)** | Senaryo 2 (Hatice ürünü, 8.920) ve 3 (Berk, 19.920) | Aynen |
| **Business/Customers** | Raf 6 | Günlük müşteri ≤ 5; uyumsuz ürüne müşteri ilgilenmez |
| **Economy (baskı)** | Aynı modeli 3 sat | Talep çarpanı 0,985³ ≈ 0,955 |
| **Anti-arbitraj** | Aynı NPC'ye aynı ürünü geri satma | Reddedilir |
| **Time/Day** | Gün sonu adımları | Sıra sabit; Gün 1–2 gider 0, Gün 3+ 500 |
| **Progression** | XP 100/250/450/700; Gün 1–7 açılışları | Seviye 2/3/4/5; açılış tablosu (v0.2 12) |
| **Repair** | N3 Pro kırık ekran (V=6.540) yan sanayi | V ≈ 7.675, tamir 1.800 → net ≈ **−665 (±10)** |
| **Save** | Gidiş-dönüş; RNG devamı | Durum hash'i eşit; yüklemeden sonraki 5 rastgele değer, kaydetmeden devam edenle aynı |
| **Save (bozuk)** | Yarım JSON, yanlış checksum | `bak1`'e düşer, `.corrupt` dosyası oluşur |
| **Save (sürüm)** | Sahte v0 fikstürü; v99 dosya | Migrasyon çalışır; v99 reddedilir (mesaj) |
| **Save (atomik)** | `tmp` yazılırken kesinti simülasyonu | `slot0` değişmez |
| **API** | Geçersiz komutlar (yetersiz nakit, dolu raf) | `Result` hatası, **durum değişmez** |
| **UI (PlayMode)** | Komut dizisiyle al→sat | Ekranlar açılır; para HUD'da güncellenir |

### 9.5 Kabul testi ("MVP bitti mi?")

1. Tüm birim, golden, invariant, kayıt testleri **yeşil**.
2. Simülasyon: Bölüm 1.4 tablosu ve alarm eşikleri **tutuyor**; sömürü botları sınırı aşmıyor.
3. Bir cihazda Gün 1–7 baştan sona **kesintisiz** oynanıyor; kapat-aç kayıp yok.
4. Eğlence ölçütleri (v0.2 13.4) ilk oyuncu testiyle **ölçüldü** (başarı hedefi ayrı).

---

## 10. İlk 14 geliştirme günü

**"Geliştirme günü"** = 4–6 saatlik odaklı çalışma oturumu. Her günün sonunda **çalışan bir şey** olur. Sürekli kural: **önce test, sonra ekran.** Gün sonunda hedef gerçekleşmediyse ertesi güne kaydırılır ve kesme listesi (Bölüm 10.3) devreye girer.

### 10.1 Günlük plan

| Gün | Yapılacak sistem | Ana dosyalar | Bağımlılık | Test kriteri | Gün sonunda çalışan özellik |
|-----|------------------|--------------|-----------|--------------|------------------------------|
| **1** | **Proje kurulumu + Core** | `.gitignore`, `Esnaf.Core.asmdef`, `Esnaf.Domain.asmdef` (+ Tests), `Money.cs`, `Round.cs`, `IRandom.cs`, `PcgRandom.cs`, `RngStreams.cs`, `EventBus.cs`, `Result.cs`, `IdGenerator.cs` | – | `round10(26.115)=26.120`; aynı seed aynı sayı; akışlar bağımsız; EventBus sırası | Unity projesi açılır, testler yeşil |
| **2** | **İçerik hattı** | `ProductDefinition.cs`, `ContentModels/*.cs`, `ContentParser.cs`, `ContentValidator.cs`, `ContentDatabase.cs` (hepsi Domain), `TextAssetContentSource.cs` (Content), `phone_models.json`, `value_tables.json`, `content_id_manifest.json`, `ContentCatalog` SO | Gün 1 | 10 model yüklenir; bozuk dosya hata verir; manifest denetimi | Editörde "İçeriği Doğrula" yeşil |
| **3** | **Ürün örneği + değer hesabı** | `ProductInstance.cs`, `ValueCalculator.cs`, `ConditionProfiles.cs`, `InstanceGenerator.cs`, `condition_profiles.json` | Gün 2 | E13 Pro A/B/C/D/E = 28.820/25.100/19.860/22.310/13.260; 10.000 örnek geçerli | Konsol/test ile "ürün üret, değerini hesapla" |
| **4** | **Ekonomi + defter + envanter** | `EconomyState.cs`, `Ledger.cs`, `TransactionRecord.cs`, `transaction_types.json`, `InventoryState.cs`, `WealthCalculator.cs` (`IWealthContributor`) | Gün 3 | Senaryo T1–T5; I1, I2, I3 | Alış/satış (sabit fiyatla) nakit ve defteri doğru günceller |
| **5** | **NPC + pazar + gün döngüsü** | `NPCDefinition.cs`, `NpcStateStore.cs`, `npc_profiles.json`, `MarketState.cs`, `ListingGenerator.cs`, `TimeState.cs`, `IDayEndStep.cs` + adımlar, `GameSession.cs`, `IGameApi.cs` (ilk komutlar) | Gün 4 | 1.000 gün: ≥2 fırsat, ≥1 tuzak (Gün 5+), jackpot kotası; gün sonu sırası; I6 (determinizm) | "Günü Bitir" ilanları yeniler, gider düşer (komutla) |
| **6** | **Ekspertiz** | `AppraisalService.cs`, `AppraisalResult.cs`, `KnowledgeState.cs`, `RiskCard.cs`, `appraisal_levels.json` | Gün 5 | v0.2 5.4 tablosu; S1 kaçırma %30±1,5; kapsama testleri; I5 (kilit); ücret maliyete eklenir/boşa gider | Ekspertiz komutu çalışır, bulgu+aralık+risk kartı döner |
| **7** | **Pazarlık motoru (alış)** | `NegotiationEngine.cs`, `NegotiationState.cs`, `NegotiationSetup.cs`, `Cards.cs`, `negotiation_rules.json`, `TradeService.BuyFlow` | Gün 6 | Senaryo 1–4 tur tur `round10`; hakaret, yanlış kart, sabır; I4 | Komutlarla alış pazarlığı baştan sona (koz kartı dahil) |
| **8** | **Satış, müşteri, fiyat baskısı** | `CustomerGenerator.cs`, `SellFlow`, satış modu pazarlık, `DemandModel.cs`, `economy_constants.json`, anti-arbitraj | Gün 7 | Hatice satışı 8.920, Berk 19.920; müşteri ≤5; baskı 0,985ⁿ; NPC geri satma reddi | Al → rafa koy → sat → kâr, komutlarla tamamen çalışır |
| **9** | **Save/Load** | `SaveData` DTO'ları, `SaveSerializer.cs`, `SaveStorage.cs` (`ISaveStorage`), `MigrationRunner.cs`, `Backup`, `CorruptHandling`, `Fixtures/save_v1.json` | Gün 8 | Gidiş-dönüş I7; RNG devamı; bozuk→bak1; sürüm reddi; atomiklik | Durum kaydedilir/yüklenir, yeniden başlayınca aynı oyun devam eder |
| **10** | **Ekonomi simülasyonu v0** | `tools/EconomySim/` (`Program.cs`, `IBot.cs`, 4 bot: Idle, Careful, Balanced, Gambler; rapor yazıcı), ilk senaryo | Gün 9 | 1.000 tohum×7 gün < 60 sn; rapor üretir; sıralama Idle < Gambler < Careful < Balanced (ortalama) | **İlk denge raporu**; Bölüm 1.4 ile karşılaştırılır, gerekirse denge dosyası ayarlanır |
| **11** | **İlerleme + öğretici + tamir** | `ProgressionService.cs`, `UnlockRules.cs`, `QuestService.cs`, `TutorialDirector.cs`, `RepairService.cs`, `progression.json`, `quests.json`, `repair_table.json`, `day_unlocks` | Gün 10 | Gün 1–7 açılım zamanları; XP eşikleri; tamir örneği −665±10 | Komutlarla 7 günlük akış, açılımlarla birlikte (başsız) oynanır |
| **12** | **UI I: iskelet + Pazar + Envanter** | `Boot`, `MainMenu`, `Game` sahneleri, `GameBootstrap.cs`, `ScreenManager.cs`, `UiTheme`, `HudPresenter`, `MarketScreen`, `ProductDetailScreen`, `ShopScreen` (envanter/etiket), `strings_tr.json` | Gün 11 | PlayMode duman: menü→oyun→pazar→ürün; para HUD'da | Telefonda/cihazda **alış** ve **rafa koyma** ekrandan yapılır |
| **13** | **UI II: Ekspertiz + Pazarlık + Satış + Gün Sonu + Defter** | `AppraisalSheet`, `NegotiationScreen` (iki mod), `CustomerList`, `DaySummaryScreen`, `LedgerScreen`, `QuestScreen`, `MoodMeter`, `RangeBar` | Gün 12 | PlayMode: al→sat→gün sonu akışı; golden değerler ekranda | **Tüm ticaret döngüsü ekrandan** oynanır |
| **14** | **Entegrasyon + cila + cihaz testi** | Öğretici akışı bağlama (Gün 1–7), kayıt/yükleme ekranı, ayarlar, hata ayıklama, Android derleme, denge ayarı (simülasyonla), hata düzeltme | Gün 13 | Kabul testi (Bölüm 9.5), Gün 1–7 kesintisiz | **Cihazda oynanabilir MVP** |

### 10.2 Zorluk dağılımı ve dürüst risk

- **Gün 7 (pazarlık)** ve **Gün 13 (UI II)** en riskli günlerdir. Pazarlık motoru testle çözülürse UI kolaydır; tersini yaparsak zaman kaybederiz. Bu yüzden **UI'dan önce motorlar ve testler biter.**
- 14 gün **iddialı** bir plan; başlangıç seviyesinde tek geliştirici için **süre uzayabilir**. Tahmin: 14 odaklı gün, gerçekte **3–4 takvim haftası** olabilir.
- Her gün sonunda **git commit** ve gün başında **önceki günün testleri** çalıştırılır (kırılma erken görünür).

### 10.3 Geride kalırsak "kesme listesi" (sırayla)

1. Başarımlar (yalnızca görev sistemi kalır)
2. S3 ve test cihazı (Gün 6 mekaniği Gün 7'ye kayar veya çıkar)
3. Tamir sistemi
4. Ekonomi simülasyonunun botları (yalnızca 2 bot kalır)
5. Ayarlar ekranı (yalnızca ses aç/kapa)

**Kesilmez (MVP çekirdeği):** ürün/ekspertiz/pazarlık/envanter/satış/defter/gün/kayıt.

---

## 11. MVP sınırı (nihai)

### 11.1 MVP'de YAPILACAK

| Alan | Kapsam |
|------|--------|
| Sektör | **Telefoncu** (10 model, 5 durum profili) |
| **Ekspertiz** | Göz muayenesi + S1, S2, S3; güven aralığı; yanlış alarm; risk kartı |
| **Pazarlık** | Alış ve satış; 10 NPC profili; gizli değerler; koz kartı; rapor göster |
| **2. el pazar** | Günlük ilan üretimi, fırsat/tuzak kotaları, ilan ömrü |
| **Envanter** | Raf (6→8), etiket fiyatı, maliyet tabanı |
| **Satış** | Müşteri üretimi, satış pazarlığı, talep ve fiyat baskısı |
| **Ekonomi** | Sade formül (baz, durum, yaş, talep), para basma engelleri |
| **Para defteri** | Gün 1 özet, Gün 3 detay, genişletilebilir tür tablosu |
| **Gün sistemi** | Sabah özeti → ticaret → gün sonu; gün sonu boru hattı |
| **Save/Load** | Sürümlü, yedekli, atomik, bozuk kayıt kurtarma, 1 slot |
| **İlk 7 gün ilerleme** | Gün 1–7 açılımları, görevler, öğretici, XP/Lv.1–5 |
| Tamir | Ekran/pil (Gün 7) |
| Altyapı | 5 assembly, `IGameApi`, EventBus, simülasyon aracı v0, test paketi |
| Platform | Android build (iOS derleme sonra doğrulanır) |

### 11.2 MVP'de YAPILMAYACAK

Galeri · Kuyumcu · Emlak · **Çalışanlar** · **Kredi/borç/taksit/faiz** · Kompleks harita · 3D dünya · **Online multiplayer** · **Oyuncular arası gerçek zamanlı pazar** · Büyük işletme/şube sistemi · IMEI/garanti · S4 ekspertiz · Koleksiyon/nadir ürün · Piyasa trendleri ve ekonomik olay/haber sistemi · Gün 8+ ilerleme · Monetization (reklam, kozmetik, satın alma) · Bulut kaydı ve çoklu slot · Ses/müzik cilası · Çok dilli destek · Enerji/streak/gerçek zamanlı bekleme · Filtre ve sektör bazlı defter analizi.

### 11.3 MVP sonrası (sıra)

1. **Eğlence testi + denge turu** (simülasyon + gerçek oyuncu)
2. Gün 8–30 ilerleme sistemi
3. Piyasa: arz-talep, trendler, olay/haber
4. Çalışanlar
5. **Kredi/borç** (Bölüm 2.3 eklenti noktalarıyla)
6. **Kuyumcu** (ortak çekirdeğin ilk gerçek sınavı)
7. Galeri, Emlak
8. Koleksiyon, başarım genişletme
9. Çoklu slot, bulut kaydı
10. Kozmetik, ödüllü reklam (rahatlık), mağaza hazırlığı, ses/görsel cila

---

## 12. İlk kodlama sırası

### 12.1 Senin yapacağın hazırlık (Unity açmadan önce, ~1 saat)

1. Bilgisayarına **Unity Hub** ve güncel **Unity LTS** (Android Build Support ile) kur.
2. **.NET SDK** kur (simülasyon aracı ve hızlı testler için; sürüm sabitleyeceğiz).
3. Bu depoyu klonla. Unity projesini **depo kök dizininde** oluşturacağız (`Assets/`, `Packages/`, `ProjectSettings/`, yanında `docs/`, `tools/`).
4. Unity Hub'da **2D (URP)** şablonuyla yeni proje oluştur; **klasör = depo kökü**. Unity ilk açılışta `Assets/`, `Packages/`, `ProjectSettings/` ve `Library/` klasörlerini yaratır.
5. Paket yöneticisinde ekle: **TextMeshPro** (ilk açılışta iste), **Newtonsoft Json** (`com.unity.nuget.newtonsoft-json`), **Test Framework** (varsayılan).

### 12.2 İlk oluşturacağımız dosyalar (sırayla)

> **Unity'de ilk olarak `Assets/_Project/Scripts/Core/Esnaf.Core.asmdef` dosyasını oluşturacağız** (`noEngineReferences: true`). Hemen ardından `Money.cs` ve `MoneyTests.cs`.

| Sıra | Dosya | Amaç | İlk test |
|------|-------|------|----------|
| 0 | `.gitignore` (Unity) | `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `UserSettings/` git'e girmesin | – |
| **1** | **`Assets/_Project/Scripts/Core/Esnaf.Core.asmdef`** | Unity'den bağımsız temel paket | Derlenir |
| 2 | `Assets/_Project/Scripts/Core/Money.cs` | Para tipi (`long` TL) ve `round10` | `Money_Round10_...` |
| 3 | `Assets/_Project/Tests/EditMode/Esnaf.Domain.Tests.asmdef` + `MoneyTests.cs` | İlk yeşil test | 26.115 → 26.120 |
| 4 | `Assets/_Project/Scripts/Core/IRandom.cs`, `PcgRandom.cs`, `RngStreams.cs` | Seed'li rastgelelik | Aynı seed → aynı sayı |
| 5 | `Assets/_Project/Scripts/Core/EventBus.cs` | Bildirim sistemi | Abone/çıkış sırası |
| 6 | `Assets/_Project/Scripts/Core/Result.cs`, `IdGenerator.cs` | Hata dönüşü, ID sayacı | Kısa testler |
| 7 | `Assets/_Project/Scripts/Domain/Esnaf.Domain.asmdef` | Kural paketi | Derlenir |
| 8 | `ProductDefinition.cs` + `phone_models.json` (Gün 2) | İçerik hattı başlangıcı | 10 model yüklenir |

Bu sıra **Gün 1 ve Gün 2**'nin başıdır. Her dosya için ben kodu yazarım, sen Unity'de açıp testleri çalıştırırsın (Test Runner → EditMode → Run All) ve sonucu bana iletirsin.

### 12.3 Çalışma şeklimiz

- Her gün: (1) planı okuruz, (2) ben sınıfları ve testleri yazarım, (3) sen Unity'de açar, testleri çalıştırırsın, (4) hataları birlikte düzeltiriz, (5) commit.
- Ben burada `dotnet` kurabilirsem `Esnaf.Core/Domain` testlerini kendim çalıştırırım (T20). Kurulamazsa **testleri senin çalıştırdığını varsayarım ve bunu açıkça belirtirim.**
- Yeni özellik isteği çıkarsa: Bölüm 11.3'e eklenir, MVP'yi **etkilemez.**

---

## 13. Tasarımın donması

- **v0.3 tamamlandığında tasarım dondurulur.** MVP kapsamı Bölüm 11'dir. Değişiklik yalnızca (a) kod yazarken bulunan teknik zorunluluk, (b) simülasyon/oyun testinin gösterdiği denge hatası için yapılır ve bu doküman güncellenir.
- Değişiklik türleri: **denge sayısı** (serbest, JSON dosyası), **kural değişikliği** (Bölüm 3.3 K1–K10'a uyduğu sürece serbest), **kapsam artışı** (yasak, Bölüm 11.3'e yazılır).
- Açık riskler (izlenecek): (1) 14 gün iddialı, (2) pazarlık motoru "formülik" hissettirebilir (his testi gerekir), (3) 250.000 TL'nin anlamı (yatırım hedefleri ve sonraki sektör kilitleri), (4) hedef Android/iOS cihaz performansı (UI ağırlıklı olduğu için düşük risk).

---

## 14. Nihai kararlar (FINAL, 21 madde)

1. MVP ilk sektörü: telefon ticareti.
2. Başlangıç sermayesi: 250.000 TL.
3. İlk hafta hedefi: yaklaşık %5–10 sermaye büyümesi.
4. %12 üzeri getiri, ekonomi simülatöründe **inceleme alarmıdır**; oyuncuya konmuş kesin bir kazanç sınırı değildir.
5. Gün 1'de gün sonu ledger özeti, Gün 3'te tam ledger ekranı.
6. Kredi/borç MVP dışı; yalnızca ileride eklenebilmesi için mimari extension point'leri.
7. Core / Domain / Persistence / Content / App assembly yapısı.
8. Oyun kuralları Unity'den bağımsız düz C# Domain katmanında.
9. `IGameApi`, oyun sistemlerine erişimin tek giriş noktası.
10. Seed'li PCG RNG.
11. Para `long` TL; fiyatlar 10 TL'ye yuvarlanır.
12. İçerik/tasarım verileri JSON.
13. ScriptableObject yalnızca Unity'ye özgü asset/catalog eşlemesi için.
14. 3 Unity scene: Boot, MainMenu, Game.
15. UI: uGUI + TextMeshPro, dikey 1080×1920.
16. Save: version + checksum + atomic write + 2 backup + migration.
17. Pazarlık sırasında uygulama kapanırsa kaldığı yerden devam.
18. Ekonomi simülasyon aracı: Unity'siz .NET konsol uygulaması, aynı Domain kodunu kullanır.
19. Simülatörde normal oyuncu botlarının yanında exploit/sömürü botları da bulunur.
20. MVP dışı sistemler MVP sonrası listesinde kalır.
21. 27.000 → 35.000 TL örneği yalnızca aritmetik testidir; denge testi olarak kullanılmaz.

---

## 15. Uygulama açıklamaları (yeni özellik değil; çelişki/belirsizlik giderme)

**UA1: JSON ayrıştırma ve doğrulama Domain'dedir.** Bölüm 3.1'de `Esnaf.Content` Unity'ye bağlıydı; simülatör (Bölüm 5.2) ise yalnızca Core+Domain kullanır. İkisi birlikte çalışsın diye:
- `ContentParser`, `ContentValidator`, `ContentDatabase` → **`Esnaf.Domain`** (metin girer, tanım nesneleri çıkar; dosya/Unity bilmez).
- `Esnaf.Content` (Unity) yalnızca: TextAsset'i metne çevirip Domain'e verme (`TextAssetContentSource`), `ContentCatalog` SO (ID → sprite/ses), editör "İçeriği Doğrula" menüsü (Domain doğrulayıcısını çağırır).
- Simülatör aynı JSON dosyalarını diskten okuyup aynı `ContentParser`'a verir.
- `Newtonsoft.Json`, `Domain` ve `Persistence` için izinli tek harici bağımlılıktır (Unity'de package'ın precompiled DLL'i, .NET'te NuGet).

**UA2: Kayıt, `IGameApi` üzerinden geçmez.** `IGameApi` UI/simülatör/test komut-sorgu kapısıdır. `Persistence`, `GameSession`'ın dar bir kayıt yüzeyini (`Capture()` / `Restore(data)`) kullanır; App bu yüzeyi yalnızca `SaveService` üzerinden çağırır. `IGameApi`'ye durum yazma yetkisi eklenmez.

**UA3: Pazarlık golden testleri ±10 TL toleranslıdır.** v0.2/v0.3 tablolarındaki pazarlık ara fiyatları `round10` ile gösterim yuvarlamasıdır (ör. iç değer 26.115 → 26.120; tabloda 26.110 yazılı olabilir). Bu yüzden: (a) değer, ekspertiz ve defter golden sayıları (28.820, 25.100, … , +15.280, 265.280) **tam eşitlikle** test edilir; (b) pazarlık tur fiyatları ve anlaşma fiyatları tablodaki değerle **±10 TL** toleransla karşılaştırılır; (c) motor Gün 7'de yazılınca kurallardan üretilen kesin değerler elle doğrulanıp `Tests/Fixtures/negotiation_golden.json` olarak sabitlenir ve sonraki testler bu dosyaya karşı çalışır.
