# ESNAF – Game Design Document v0.2 (MVP: Yalnızca Telefoncu)

Sürüm: 0.2 · Kapsam: **Sadece MVP (telefoncu) tasarımı** · Durum: Kod yok.
v0.1 (`docs/GDD.md`) genel vizyon ve uzun vade için referans olarak kalır. Çelişki olursa **v0.2 geçerlidir.**

> Sayılar (fiyat, yüzde, tur sayısı) **başlangıç önerisidir.** Dengeyi prototipte ve simülasyonda ayarlayacağız. Bu yüzden hepsi ileride tek bir "denge dosyasında" duracak (Bölüm 16).
> Örneklerdeki tüm hesaplar elle kontrol edildi; rastgele değerler örneklerde sabit alınmıştır. Örnek fiyatlar okunurluk için yuvarlanmıştır.

---

## 0. Kesinleşen kararlar (v0.1 → v0.2)

| # | Karar | Durum |
|---|-------|-------|
| 1 | Gün sistemi: oyun içi gün, "Günü Bitir" ile ilerler. Günlük giriş zorunluluğu, streak, enerji ve bekleme sayacı **yok**. | ✅ |
| 2 | İlk prototip **yalnızca telefoncu**, 2D, UI ağırlıklı. Galeri, kuyumcu, emlak, çalışan, büyük işletme, harita, 3D **yok**. | ✅ |
| 3 | Sektör sırası Telefoncu → Kuyumcu → Galeri → Emlak. Mimari bu sıraya **bağımlı olmayacak** (Bölüm 15). | ✅ |
| 4 | Başlangıç sermayesi 250.000 TL. Kilit açma para + seviye + itibar + görev + başarım + işletme değeri ile (Bölüm 15.3). **Kredi MVP'de yok.** | ✅ |
| 5 | Ekspertiz: güven aralığı korunur. MVP'de **3 seviye** (+ ücretsiz göz muayenesi). | ✅ |
| 6 | Ekspertiz ↔ pazarlık bağlı: bulgular "koz kartı" olur. NPC değerleri gizli, sonuç her seferinde aynı olmaz. | ✅ |
| 7 | Ekonomi MVP'de sade: baz fiyat, durum, yaş, talep, ilan fiyatı, satış fiyatı. Para basma engellenir (Bölüm 10.4). | ✅ |
| 8 | Para defteri: her hareket kayıtlı, oyuncu "paramın nereye gittiğini" görebilir. | ✅ |
| 9 | İlk 7 gün öğretici ama sıkıcı değil, her gün yeni bir mekanik/karar. Eski mekanikler açık kalır. | ✅ |
| 10 | Eğlence ilkesi: "ucuz al pahalı sat" değil, **bul + risk değerlendir + ekspertizi yorumla + pazarlık et + doğru zamanda sat.** | ✅ |
| 11 | 30 günlük sistem ertelendi; önce 7 günlük döngü test edilecek. | ✅ |

### v0.2'de kendi tasarımıma koyduğum kısıtlar ve dürüst notlar

Bunları senden onay/itiraz almak için yazıyorum:

1. **250.000 TL, telefon sektöründe "sermaye kısıtı" olmayacak.** Telefonlar 4.500–46.000 TL. Oyuncu ilk hafta paranın çoğunu kullanamaz. Büyümeyi sınırlayan şeyler para değil: **raf kapasitesi, günlük ilan/müşteri sayısı, ekspertiz maliyeti, kararların kalitesi.** Bu bilinçli bir tercih (para değil beceri kazandırsın). Bunun karşılığında 250.000 TL'nin bir anlamı olmalı: raf/ekipman yatırımları ve sonraki sektör kilitleri (ör. kuyumcu açılışı) bu parayı harcatacak. Sermaye yalnızca "kasa" gibi görünüyorsa oyuncu "para var, hedef yok" hissine kapılabilir. **Prototipte izleyeceğimiz risk.**
2. **Eğlence "ucuz al pahalı sat" olmasın kuralı** mekanik olarak şöyle uygulanıyor: her ürün kâr marjı **ortalama %5–12** ve marjın çoğu **ekspertiz yorumundan + pazarlıktan** geliyor. Hiçbir ilan "otomatik kâr" değil (Bölüm 10.4 ve 16).
3. **IMEI ve garanti MVP dışı.** Hem kapsam küçülüyor hem yasal/mağaza riski azalıyor. Yerine "Kutu/Fatura" var.
4. **Pazarlıkta "düşük teklif spam'i"** (hep en düşük teklif) bir sömürü riski. Önlem: NPC'nin gizli ret fiyatının %90'ının altındaki teklif "hakaret" sayılır (güven ve sabır düşer). İlk 4 günde hakaret **uyarı**dır, ceza yok (öğrenme).
5. **Ekspertiz "her zaman al" refleksine dönmesin:** ücretli ve marjın %3–7'sine denk gelecek şekilde fiyatlandı.

---

## 1. Oyun günü akışı (MVP)

```
SABAH   → Piyasa özeti (haber, talep, günlük hedef önerisi)
        → Günlük ilanlar listelenir
GÜN İÇİ → TİCARET: ilan incele → ekspertiz kararı → pazarlık → al/vazgeç
                   rafa koy/etiket → müşteri gelir → pazarlık → sat
        → İŞLETME İŞLEMLERİ: raf yükseltme, ekipman, tamir (açıldıkça)
AKŞAM   → "Günü Bitir" → gün sonu işlemleri → Gün Sonu Özeti → otomatik kayıt
```

**Gün sonu işlemleri (sırayla):**
1. Satılmayan/kaçan müşteriler kaydedilir.
2. Günlük gider (kira/elektrik) defterden düşer.
3. Stok bekleme değer kaybı uygulanır.
4. İlan ömürleri 1 azalır, süresi dolanlar kalkar.
5. Talep endeksleri güncellenir (Gün 5'ten itibaren).
6. XP, görev, başarım kontrolü.
7. **Gün Sonu Özeti** gösterilir.
8. Otomatik kayıt.
9. Ertesi sabah yeni ilanlar üretilir.

Oyuncu "Günü Bitir"e basmadıkça hiçbir şey ilerlemez. Gün içindeki işlem sayısı **sınırsız** (zaman/enerji yok); sınırlayıcılar raf kapasitesi, ilan sayısı ve gelen müşteri sayısıdır.

---

## 2. MVP ana döngüsü (mikro adımlar)

```
İlanı gör → Ürünü incele (görünen bilgi + göz muayenesi)
→ EKSPERTİZ KARARI (yapma / S1 / S2 / S3)
→ Ekspertiz yaptır → Sonuçları değerlendir (bulgular + değer aralığı + risk kartı)
→ Satıcıyla pazarlık (koz kartlarını kullan)
→ Satın al / vazgeç → Envantere ekle
→ Etiket fiyatı belirle, rafa koy → Müşteri gelir
→ Müşteriyle pazarlık (rapor gösterebilirsin) → Sat
→ Kâr/zarar hesabı (ürün bazlı) → Gün sonu
```

**Her adımda gerçek bir karar olmalı (tasarım kuralı #10):**

| Adım | Oyuncunun kararı |
|------|------------------|
| İlan seçme | Hangi ilan zamanıma/paramıma değer? (Bazı ilanlar tuzak, bazıları fırsat) |
| Ekspertiz kararı | Yapmalı mıyım? Hangi seviye? Hangi ücret? |
| Sonuç yorumu | Aralık ve güven ne diyor? Bu fiyata alınır mı? |
| Alış pazarlığı | Ne kadar zorlarım? Hangi kozu ne zaman oynarım? Ne zaman kalkarım? |
| Etiket fiyatı | Yüksek koyup bekleyeyim mi, hızlı satayım mı? |
| Satış pazarlığı | Bu müşteriye taviz vereyim mi, başka müşteri bekleyeyim mi? |
| Gün sonu | Yarın hangi piyasa/ilan durumuna göre hazırlanayım? |

---

## 3. Telefon veri modeli (MVP)

### 3.1 Telefon modeli (Tanım – sabit)

- **Referans durum:** pil ≥ %90, ekran orijinal, kasa %100, kamera sağlam, kutu/fatura yok, yaş 7–12 ay.
- **Baz fiyat** = referans durumdaki, **baz hafızalı**, 7–12 aylık telefonun piyasa değeri (TL).

### 3.2 10 örnek model (kurgusal markalar)

Oyun yılı 2026 kabul edilir. Segment, ekspertiz ücretini belirler: **Giriş** (<8.000), **Orta** (8.000–20.000), **Üst** (>20.000).

| # | Model | Segment | Çıkış | Baz hafıza | **Baz fiyat (TL)** | Hafıza çarpanları | Örnek yaş aralığı (ay) |
|---|-------|---------|-------|-----------|-------------------|--------------------|------------------------|
| 1 | Nova N1 Lite | Giriş | 2023 | 64 GB | **4.500** | 64:1,00 · 128:1,15 | 6–36 |
| 2 | Yıldız Y5 | Giriş | 2022 | 64 GB | **6.000** | 64:1,00 · 128:1,12 | 12–48 |
| 3 | Samsun Vega A3 | Orta | 2023 | 128 GB | **9.500** | 64:0,90 · 128:1,00 · 256:1,15 | 6–36 |
| 4 | Nova N3 Pro | Orta | 2023 | 128 GB | **12.500** | 64:0,90 · 128:1,00 · 256:1,15 | 6–36 |
| 5 | Zirve Z5 | Orta | 2022 | 128 GB | **14.000** | 128:1,00 · 256:1,15 | 12–48 |
| 6 | Yıldız Y8 Plus | Orta | 2023 | 128 GB | **17.500** | 128:1,00 · 256:1,15 | 6–36 |
| 7 | Elma E11 | Orta | 2021 | 64 GB | **19.000** | 64:1,00 · 128:1,10 · 256:1,22 | 24–60 |
| 8 | Samsun Vega S21 | Üst | 2022 | 128 GB | **24.000** | 128:1,00 · 256:1,12 | 12–48 |
| 9 | Elma E13 Pro | Üst | 2023 | 128 GB | **32.000** | 128:1,00 · 256:1,12 · 512:1,28 | 6–36 |
| 10 | Elma E14 Pro Max | Üst | 2024 | 256 GB | **46.000** | 256:1,00 · 512:1,15 · 1 TB:1,30 | 0–18 |

Notlar:
- Marka isimleri kurgusaldır (marka/lisans riski yok).
- Model 10 (amiral) **Gün 5'ten önce** ilanlarda çıkmaz (öğrenme sırasında büyük risk olmasın).
- Modelin ilan sıklığı segment ağırlıklarıyla belirlenir: Giriş %30, Orta %40, Üst %30 (Gün 1–4'te Üst %15).

### 3.3 Telefon örneği (Instance) alanları

| Alan | Değerler | Görünürlük |
|------|----------|-----------|
| Model, hafıza | model tanımından | Görünür |
| Yaş (ay) | model aralığında | Görünür (satıcı beyanı) |
| Pil sağlığı | 45–100 | Gizli (ilanda "iyi" gibi yorum; ekspertizle aralık) |
| Ekran | Orijinal · Çizik · Yan sanayi (değişmiş) · Kırık | Kısmen (çizik/kırık görünür, "değişmiş" gizli) |
| Kasa (kozmetik %) | 40–100 | Kaba görünür (fotoğraf), ekspertizle netleşir |
| Kamera | Sağlam · Lekeli · Arızalı | Gizli |
| Kutu / Fatura | ayrı ayrı var/yok | Görünür |
| Gerçek değer | hesaplanır | Gizli |
| İstenen fiyat | satıcı belirler | Görünür |

Ek MVP dışı (sonra): garanti, IMEI, su hasarı, şarj portu, yazılım durumu.

---

## 4. Değer (fiyat) hesabı — MVP formülü

### 4.1 Referans fiyat (herkese açık)

```
Referans Fiyat (RF) = Baz Fiyat × Hafıza Çarpanı × Yaş Çarpanı × Talep Çarpanı
```
İlanda oyuncuya **RF gösterilir** ("kusursuz durumda piyasa: 28.160 TL"). Oyuncunun işi RF'den **durum düşüşünü** tahmin etmek.

### 4.2 Gerçek değer

```
Gerçek Değer (V) = RF × Pil × Ekran × Kasa × Kamera × Paket
```

**Yaş çarpanı:**

| Yaş (ay) | 0–6 | 7–12 | 13–24 | 25–36 | 37–48 | 49+ |
|----------|-----|------|-------|-------|-------|-----|
| Çarpan | 1,10 | 1,00 | 0,88 | 0,77 | 0,68 | 0,60 |

**Durum çarpanları:**

| Parça | Kural |
|-------|-------|
| Pil | `1 − 0,006 × (90 − pil)` (pil ≥ 90 ise 1,00). Örnek: %78 → 0,928 · %62 → 0,832 |
| Ekran | Orijinal 1,00 · Çizik 0,96 · Yan sanayi 0,88 · Kırık 0,75 |
| Kasa | `0,80 + 0,20 × (kasa/100)`. Örnek: %85 → 0,970 · %55 → 0,910 |
| Kamera | Sağlam 1,00 · Lekeli 0,93 · Arızalı 0,82 |
| Paket | Yok 1,00 · Kutu 1,02 · Fatura 1,02 · İkisi 1,04 |

**Talep çarpanı (MVP):** her model için 0,90–1,10 arası. Gün 1–4: sabit 1,00. Gün 5'ten sonra kurallar Bölüm 10'da.

### 4.3 Kalite varyasyonları (5 durum profili)

Bir ilan üretilirken profil seçilir, sonra değerler aralık içinde rastgele çekilir.

| Profil | Ağırlık | Pil | Ekran | Kasa | Kamera | Not |
|--------|---------|-----|-------|------|--------|-----|
| **A – Temiz** | %25 | 88–98 | Orijinal | 88–100 | Sağlam | Genelde kutulu/faturalı, satıcı pahalı ister |
| **B – Kullanılmış** | %35 | 76–88 | Orijinal/Çizik | 70–88 | Sağlam | En yaygın |
| **C – Yıpranmış** | %20 | 60–76 | Çizik | 50–72 | Sağlam/Lekeli | Ucuz görünür, marj dar |
| **D – Tamirli** | %15 | 70–88 | **Yan sanayi** (satıcı saklar) | 75–90 | Sağlam | "Gizli kusur" tipik ilan |
| **E – Sorunlu** | %5 | 50–68 | Yan sanayi/Kırık | 50–70 | Lekeli/Arızalı | Yalnızca **Gün 5+**; tuzak ilanların çoğu |

**Gün 1–4 güvenlik ağı:** E profili çıkmaz; D profilinde en fazla tek gizli kusur olur. Tek hatanın en kötü sonucu ≈ −%12.

### 4.4 Örnek: Elma E13 Pro, 128 GB, 18 aylık (RF = 32.000 × 1,00 × 0,88 = **28.160**)

| Profil | Pil | Ekran | Kasa | Kamera | Paket | Hesap | **Gerçek değer (V)** |
|--------|-----|-------|------|--------|-------|-------|--------------------|
| A | 94 (1,000) | Orijinal (1,00) | 92 (0,984) | Sağlam | İkisi (1,04) | 28.160 × 0,984 × 1,04 | **28.820** |
| B | 82 (0,952) | Çizik (0,96) | 78 (0,956) | Sağlam | Kutu (1,02) | 28.160 × 0,952 × 0,96 × 0,956 × 1,02 | **25.100** |
| C | 68 (0,868) | Çizik (0,96) | 55 (0,910) | Lekeli (0,93) | Yok | 28.160 × 0,868 × 0,96 × 0,91 × 0,93 | **19.860** |
| D | 78 (0,928) | Yan sanayi (0,88) | 85 (0,970) | Sağlam | Yok | 28.160 × 0,928 × 0,88 × 0,97 | **22.310** |
| E | 62 (0,832) | Kırık (0,75) | 60 (0,920) | Arızalı (0,82) | Yok | 28.160 × 0,832 × 0,75 × 0,92 × 0,82 | **13.260** |

Aynı model ve yaşta gerçek değer **13.260 – 28.820** arasında değişiyor (≈ %46 fark). Ekspertiz ve pazarlığın neden önemli olduğu bu tablodan görülür.

---

## 5. Ekspertiz sistemi (MVP)

### 5.1 Seviyeler ve ücretler

| Seviye | Ad | Açılış | Giriş | Orta | Üst |
|--------|----|--------|-------|------|-----|
| S0 | **Göz muayenesi** | Gün 1 | Ücretsiz | Ücretsiz | Ücretsiz |
| S1 | **Temel kontrol** | Gün 3 | 100 | 200 | 300 |
| S2 | **Ayrıntılı kontrol** | Gün 5 | 350 | 600 | 1.000 |
| S3 | **Profesyonel ekspertiz** (Test cihazı gerekir, 12.000 TL) | Gün 6 | 700 | 1.200 | 2.000 |

Sonradan: S4 Uzman ekspertiz (çalışan/ekipman), IMEI/garanti sorguları.
Ekspertiz ücreti **anlık** ödenir; işlem anında biter (bekleme yok). Ekspertiz sonra **alınan ürünün maliyetine eklenir**; alınmayan ürünün ekspertizi "boşa ekspertiz gideri" olur.

### 5.2 Doğruluk: ne ölçülür?

**Kategorik bulgular** (ekran değişmiş mi, kamera sorunlu mu):

| Seviye | Sorunu yakalama olasılığı | Yanlış alarm olasılığı | Bulgunun güven düzeyi | Kanıt gücü |
|--------|--------------------------|------------------------|-----------------------|------------|
| S0 | Ekran %35 · Kamera %20 | %5 | "Belki" (ipucu) | 0,2 |
| S1 | Ekran %70 · Kamera %60 | %5 | **Düşük** | 0,4 |
| S2 | Ekran %92 · Kamera %90 | %2 | **Orta** | 0,7 |
| S3 | Ekran %99 · Kamera %99 | %0,5 | **Kesin** | 1,0 |

- **Yakalayamazsa** sonuç "Sorun görünmüyor" olur; **"Sorun yok" denmez.** Bu fark oyunun temel risk mekaniğidir.
- **Yanlış alarm**: sağlam parçada bile bulgu çıkabilir ("Ekran değişmiş olabilir – Düşük güven"). Yüksek güvenli bulgu yanlış alarm olmadan çıkmaz (S3 hariç sıfıra yakın).

**Sayısal alanlar** (aralık olarak gösterilir):

| Seviye | Pil aralığı (±puan) | Kasa aralığı (±puan) |
|--------|--------------------|----------------------|
| S0 | (beyan) | ±15 |
| S1 | ±10 | ±10 |
| S2 | ±5 | ±5 |
| S3 | ±2 | ±3 |

Aralığın merkezi gerçek değerden küçük bir rastgele sapmayla kaydırılır (yani aralık her zaman gerçeği ortalamaz).

### 5.3 Değer aralığı nasıl hesaplanır (sade anlatım)

1. Ekspertiz "gözlenen durumu" belirler: yakaladığı kusurlar (güven ağırlığıyla), sayısal alanların orta noktası.
2. **Gözlenen değer** = aynı değer formülü, ama **gözlenen durumla**. Yakalanamayan kusur "yokmuş gibi" hesaba girer. Orta güvenli bir bulgu, kusurun **çarpanının güven-üssü** kadar etkisini yansıtır (örn. ekran 0,88 çarpanı, güven 0,7 → 0,88^0,7 ≈ 0,914).
3. **Aralık** = gözlenen değer × (1 ± yarı genişlik): S1 ±%10 · S2 ±%5 · S3 ±%2,5.
4. Tasarım hedefi (simülasyonla ayarlanacak): gerçek değerin aralık içinde olma oranı **S1 ≈ %85, S2 ≈ %92, S3 ≈ %96**. Yani **yine de yanılma ihtimali var**; risk yönetimi budur.
5. Aynı ilan aynı seviyede tekrar ekspertize sokulursa **aynı sonuç** çıkar (sonuç ilana kilitli). Tekrar denemeyle şansı zorlamak yok. Yüksek seviye = yeni ve daha iyi sonuç.

### 5.4 Çalışılmış örnek: E13 Pro (Profil D, V = 22.310)

Gerçek durum: pil 78, ekran yan sanayi (satıcı saklıyor), kasa 85, kamera sağlam, paket yok. Satıcının ilanda "gösterdiği" değer: 22.310 ÷ 0,88 = **25.350** (ekran orijinalmiş gibi).

| Seviye | Sonuç (oyuncunun gördüğü) | Değer aralığı | Gerçek değer aralıkta mı? |
|--------|---------------------------|---------------|--------------------------|
| S0 | "Ekranın köşesinde hafif renk farkı gibi?" (ipucu) | – | – |
| S1 (200→ üst 300) | Ekran: değişmiş olabilir *(Düşük)* · Pil 68–88 · Kasa 75–95 · Kamera: sorun görünmüyor | **21.700 – 26.500** | Evet, ama çok geniş |
| S1 (kaçırırsa, %30) | Ekran: sorun görünmüyor · Pil 68–88 · Kasa 75–95 | **22.800 – 27.900** | **Hayır!** (gerçek 22.310 aralığın altında kaldı… %15 riskin somut hali) |
| S2 (1.000) | Ekran: değişmiş olabilir *(Orta)* · Pil 73–83 · Kasa 80–90 · Kamera: sorun görünmüyor | **22.000 – 24.300** | Evet |
| S3 (2.000) | Ekran: **değişmiş (Kesin)** · Pil 76–80 · Kasa 82–88 · Kamera sağlam | **21.800 – 22.900** | Evet, dar |

> S1'in kaçırdığı senaryoda değerin 22.800–27.900 çıkması, oyuncuyu **fazla ödemeye** itebilir. Bu, "ekspertiz cevabı vermez, riski gösterir" ilkesinin örneğidir.

### 5.5 Risk kartı (ekspertiz sonrası yardımcı ekran)

Sonuç ekranında oyuncuya teklif için üç senaryo gösterilir (tahmini satış = değer × **1,02**, itibar arttıkça yükselir):

Senin örneğin: değer aralığı **28.000 – 31.000**, teklif **29.000**:

| Senaryo | Gerçek değer | Beklenen satış (×1,02) | Alış | Sonuç |
|---------|--------------|------------------------|------|-------|
| Kötü | 28.000 | 28.560 | 29.000 | **−440** |
| Orta | 29.500 | 30.090 | 29.000 | **+1.090** |
| İyi | 31.000 | 31.620 | 29.000 | **+2.620** |

Ek uyarı: "Gerçek değerin aralık dışında kalma olasılığı ≈ %8 (S2)". Karar oyuncunun. Kart **kesin cevap vermez**; sadece "kârın ne kadar riskli" olduğunu gösterir. Ekspertiz ücreti (ör. 1.000) kararı zaten verildiği için **batık maliyettir**, ama oyuncu ürün maliyetine eklenir ve net kârı düşürür.

### 5.6 Ekspertiz ↔ pazarlık bağlantısı

Her **bulgu** bir **Koz Kartı** olur (Bölüm 7.4). Kartın gücü ekspertiz seviyesinden gelir (0,2–1,0). Yüksek seviye = daha güçlü koz + satışta gösterilebilir **rapor**.

---

## 6. NPC profilleri (en az 10)

Aynı profil hem **satıcı** hem **müşteri** olabilir; oyuncunun karşısındaki rolüne göre farklı sayılar kullanılır.

### 6.1 Satıcı rolü

| # | Ad | Kişilik | İstenen fiyat (ilan değeri × ) | Ret oranı (R) | Sabır (tur) | Değer bilgisi hatası (σ) | Aciliyet | İkna katsayısı | Özellik |
|---|----|---------|-------------------------------|---------------|-------------|--------------------------|----------|----------------|---------|
| 1 | **Kemal Abi** | Pazarlıkçı | 1,20 | 0,96 | 5 | %8 | 0,2 | 0,6 | Çok tur, yavaş taviz verir |
| 2 | **Selin** | Aceleci (taşınıyor) | 1,05 | 0,82 | 3 | %12 | 0,9 | 0,9 | Hızlı kapatır, sıkıştırılırsa gider |
| 3 | **Dr. Murat** | Bilgili | 1,08 | 0,97 | 4 | %3 | 0,3 | 1,0 | Doğru kartı hemen kabul eder; **yanlış kartın cezası ×2** |
| 4 | **Hatice Teyze** | Bilgisiz | 1,10 (kendi hatalı değerine) | 0,92 | 4 | %30 (genelde ~%28 eksik bilir) | 0,4 | 0,8 | Fırsat da tuzak da olabilir |
| 5 | **Berk** | Zengin | 1,15 | 0,98 | 3 | %6 | 0,1 | 0,5 | Israr eden pazarlık güveni düşürür, prestije önem verir |
| 6 | **Ozan** | Sabırsız | 1,05 | 0,90 | 2 | %10 | 0,5 | 0,7 | 1–2 turda karar verir |
| 7 | **Rıza Bey** | Şüpheli | 1,10 | 0,95 | 4 | %8 | 0,3 | 0,5 | S2 isteği güveni −10, S3 isteği −20; **S3'ü %30 ihtimalle reddeder** |
| 8 | **Nermin** | Koleksiyoncu | 1,25 (kutulu/faturalı 2021–22 modeller) | 0,98 | 5 | %4 | 0,1 | 0,6 | Paketli ürünü sever, sıradan ürüne ilgisiz |
| 9 | **Cengiz** | Aceleci-borçlu ("Acil Satış") | 1,02 | 0,78 | 2 | %15 | 1,0 | 0,9 | Kusuru saklama olasılığı %60, **tuzak/fırsat** |
| 10 | **Ayşe Hanım** | Dürüst/güvenilir | 1,10 | 0,94 | 4 | %5 | 0,3 | 0,9 | İlanı dürüst, kusuru söyler (öğretici satıcı) |

Sütun açıklamaları:
- **İstenen fiyat** = satıcının inandığı değer (kusurları saklıyorsa "kusursuz" hali) × çarpan.
- **Ret oranı (R):** satıcının inandığı değerin bu oranından aşağıya **asla** satmaz (R fiyatı gizlidir).
- **σ:** satıcının değeri ne kadar yanlış bildiği (Hatice Teyze gibi bilgisizler büyük hata yapar).
- **İkna katsayısı:** koz kartının satıcıyı ne kadar etkilediği.

### 6.2 Müşteri rolü

| # | Ad | Kişilik | Açılış teklifi (Max'ın %'si) | Değer çarpanı (mRatio) | Sabır | Özellik |
|---|----|---------|-------------------------------|------------------------|-------|---------|
| 1 | Kemal Abi | Pazarlıkçı | 0,80 | 1,00 | 5 | Düşük açar, yavaş yükseltir |
| 2 | Selin | Aceleci | 0,92 | 1,00 | 3 | Hızlı alır, marj düşük |
| 3 | Dr. Murat | Bilgili | 0,90 | 0,97 | 4 | Gerçek değeri bilir, yanlış iddiaya tepki verir |
| 4 | Hatice Teyze | Bilgisiz | 0,85 | 1,00 (hata %30) | 4 | Yanlış değerlendirir (bazen fazla öder) |
| 5 | Berk | Zengin | 0,90 | **1,10** | 3 | En iyi müşteri; Gün 6+ |
| 6 | Ozan | Sabırsız | 0,88 | 1,00 | 2 | Uzatırsan gider |
| 7 | Rıza Bey | Şüpheli | 0,82 | 0,95 | 4 | **Rapor** gösterirsen güven +15 |
| 8 | Nermin | Koleksiyoncu | 0,88 | 1,02 (**paketli üründe ×1,12**) | 5 | Kutulu/faturalı telefona prim ödeyen tek profil |
| 9 | Cengiz | Bütçeli | 0,85 | 0,95 | 3 | Sadece giriş/orta segment |
| 10 | Ayşe Hanım | Güvenilir | 0,90 | 1,00 | 4 | İtibar puanını artırır |

Müşterinin **Max ödeme değeri:** `M = V_müşteri × mRatio × (1 + Dükkân Primi)`
- `V_müşteri` = gerçek değer × (1 ± σ) (bilgisiz müşteri değeri yanlış görür).
- **Dükkân Primi:** başlangıç **%5**, itibarla **%12'ye** kadar çıkar.

---

## 7. Pazarlık sistemi (MVP)

### 7.1 Değişkenler

Her NPC'nin **gizli** değerleri:
- **Ret fiyatı (R):** satıcı için en düşük, müşteri için en yüksek (Max).
- **Sabır:** kaç tur pazarlığa dayanır.
- **Güven (0–100):** başlangıç 50 ± 10 (rastgele). Yüksek güven → daha kolay taviz.
- **Aciliyet (0–1):** yüksekse daha çabuk taviz.
- **Bilgi seviyesi:** kendi değer hatası ve koz kartına tepkisi.
- **"Günün ruh hali":** R'ye ±%3 rastgele kayma (aynı satıcıyla farklı sonuç).

Oyuncunun **görebildiği** şeyler: NPC'nin cümlesi, yüz ifadesi (3 seviyeli **ruh hali göstergesi**), kalan turların yaklaşık sayısı ("sabrı azalıyor"), Gün 4'ten itibaren kişilik ipuçları. Sayılar görünmez.

### 7.2 Tur mantığı (alış; satıcı NPC)

```
Oyuncu teklif O verir (isteğe bağlı: koz kartı oynar)
1) Hakaret kontrolü:  O < 0,90 × R  → güven −15, sabır −1 ekstra
2) NPC yeni fiyatı hesaplar:
      t = 0,20 + 0,30 × (Güven/100) + 0,15 × Aciliyet
      Yeni Fiyat = max( R,  Güncel Fiyat − t × (Güncel Fiyat − R) )
3) O ≥ Yeni Fiyat ise ANLAŞMA, fiyat = Yeni Fiyat (NPC'nin fiyatı)
4) Değilse: O ≥ 0,97 × R  → güven +5 ("yakın teklif")
5) Sabır −1. Sabır 0 ise NPC "Son fiyatım X, al ya da git" der.
```

- **Doğru koz kartı:** güven **+5**. **Yanlış (yanlış alarm) kart:** güven −10, sabır −1 (Bilgili: −20, sabır −2).
- **Müşteri rolünde** aynı motor ters yönde çalışır: müşteri teklifini kendi Max'ına (M) doğru yükseltir; oyuncunun istediği fiyat ≤ müşterinin yeni teklifiyse anlaşma **müşterinin teklifi** üzerinden olur. Oyuncunun istediği fiyat `M × 1,15`'in üstündeyse "çok pahalı": müşteri hakaret sayar (güven −15, sabır −2).
- **Öğretici kolaylık (Gün 1–4):** hakaret uyarı olarak çıkar (ceza yok); Gün 1'de R oranı 0,90'a kilitlenir.

### 7.3 Neden bu model?

- Oyuncu R'yi **bilmez**; sadece tepkilere bakarak tahmin eder (risk).
- Aynı teklif farklı NPC'de farklı sonuç verir (kişilik çeşitliliği).
- Aynı NPC'de farklı gün farklı sonuç verir (ruh hali/güven rastgeleliği).
- Bilgi (ekspertiz) → **R'yi düşürür** (koz kartı), yani ekspertiz gerçekten pazarlık gücü verir.

### 7.4 Koz Kartı sistemi (ekspertiz ↔ pazarlık)

Her ekspertiz bulgusu kart olur: "Ekran değişmiş olabilir *(Orta güven)*". Kart bir pazarlıkta **bir kez** kullanılır.

```
Kart Etkisi (TL) = Sorunun TL değeri × Kanıt gücü × Satıcının ikna katsayısı
Yeni R = R − Kart Etkisi
```
- **Sorunun TL değeri:** kusurun gerçekten götürdüğü değer (örn. yan sanayi ekran: 25.350 − 22.310 = **3.040**).
- **Kanıt gücü:** ekspertiz seviyesi (S0: 0,2 · S1: 0,4 · S2: 0,7 · S3: 1,0).
- **Kart yanlışsa** (kusur aslında yok, yanlış alarm) etkisi **yoktur** ve güven/sabır düşer.
- S3 profesyonel raporla oynanan kartın ikna katsayısı **+0,2** artar.
- Yeni R hiçbir zaman `0,65 × V`'nin altına inmez (adaletli sınır).

### 7.5 Satışta ekspertiz kullanımı: "Rapor göster"

Oyuncu S2/S3 ekspertiz raporunu müşteriye gösterebilir:
- Müşterinin değer hatası σ **yarıya iner** (müşteri gerçek değere yaklaşır; iyi ürünse M artar).
- **Şüpheli** müşteriye güven +15, diğerlerine +10.
- Ürün kusurluysa rapor **M'yi düşürür** (dürüstlük itibarı artırır; oyuncu isterse göstermez).

---

## 8. Sayısal pazarlık senaryoları

Örneklerde rastgele değerler (ruh hali, σ hatası) sabit alınmıştır.

### Senaryo 1 — **Kemal Abi** (zor satıcı), E13 Pro Profil D, V = 22.310

Kurulum: ilan değeri **25.350**, istenen **30.400**, **R₀ = 24.350** (0,96 × 25.350), sabır 5, güven 50, aciliyet 0,2, ikna 0,6.
Oyuncu **S2 (1.000 TL)** yaptırdı: "Ekran değişmiş olabilir (Orta)", aralık **22.000 – 24.300**.

| Tur | Oyuncu | Hesap | NPC | Sabır | Güven |
|-----|--------|-------|-----|-------|-------|
| 1 | Teklif 22.000 (hakaret eşiği 21.915, geçti) | t = 0,20 + 0,15 + 0,03 = 0,38 → 30.400 − 0,38 × 6.050 | Yeni fiyat **28.100** | 4 | 50 |
| 2 | **Koz: "Ekran değişmiş"** + teklif 22.500 | Kart etkisi 3.040 × 0,7 × 0,6 = **1.277** → R = **23.073**. Güven +5. t = 0,395 → 28.100 − 0,395 × 5.028 | Yeni fiyat **26.110** | 3 | 55 → yakın teklif → 60 |
| 3 | Teklif 22.800 | t = 0,41 → 26.110 − 0,41 × 3.042 | **24.870** | 2 | 65 |
| 4 | Teklif 23.000 | t = 0,425 → 24.870 − 0,425 × 1.797 | **24.105** | 1 | 70 |
| 5 | Teklif 23.100 | t = 0,44 → 24.105 − 0,44 × 1.032 | "**Son fiyatım 23.650**" | 0 | – |

Sonuç: Son fiyat **23.650**, oyuncunun S2 aralığının (22.000–24.300) **orta noktasının (23.150) üstünde**, gerçek değer ise **22.310**. **Doğru karar: vazgeç.**
- Vazgeçen oyuncu: yalnızca −1.000 (boşa ekspertiz) kaybeder; kaçındığı zarar ≈ −890 (satış ≈ 22.760 vs alış 23.650), yani **kötü bir alımı ekspertiz sayesinde elemiş olur.**
- Alırsa: net ≈ **−1.900** (−890 zarar −1.000 ekspertiz).
- **S3** ile (kesin kanıt, güç 1,0, rapor ikna +0,2 → 0,8): kart etkisi 3.040 × 1,0 × 0,8 = 2.432, R ≈ **21.920**; yine de Kemal Abi'nin sabrı bitince inebileceği en düşük fiyat ≈ 22.500 civarıdır. Marj dar: **başka ilana geçmek** iyi bir karardır.

### Senaryo 2 — **Hatice Teyze** (bilgisiz, fırsat), Nova N3 Pro 128 GB, 30 ay, Profil B, V = 8.540

RF = 12.500 × 0,77 = 9.625 → V = 9.625 × 0,964 × 0,96 × 0,94 × 1,02 = **8.540**.
Hatice Teyze değeri %28 eksik biliyor: V_s = **6.150**, istenen **6.750**, R = 0,92 × 6.150 = **5.660**, sabır 4, aciliyet 0,4.
Oyuncu **S1 (200)** yaptırdı: değer aralığı ≈ **8.000 – 9.800** ("bu fiyat çok ucuz").

| Tur | Teklif | Hesap | Sonuç |
|-----|--------|-------|-------|
| 1 | 6.000 | t = 0,41 → 6.750 − 0,41 × 1.090 = 6.303 (yeni fiyat), teklif < fiyat. Güven 50 → 55 (yakın teklif) | 6.303 |
| 2 | 6.100 | t = 0,425 → 6.303 − 0,425 × 643 = **6.030**; teklif ≥ fiyat | **Anlaşma 6.030** |

Satış: Pazarlıkçı müşteri (V_müşteri = 8.710, M = 8.710 × 1,00 × 1,05 = **9.150**, açılış 7.320, etiket 9.500):

| Tur | Oyuncunun istediği | Müşterinin yeni teklifi |
|-----|--------------------|-------------------------|
| 1 | 9.300 | 8.015 (t = 0,38) |
| 2 | 9.000 | 8.463 (t = 0,395) |
| 3 | 8.800 | 8.745 (t = 0,41) |
| 4 | 8.750 | 8.917 (t = 0,425) → **anlaşma 8.920** |

Net: 8.920 − 6.030 − 200 = **+2.690** (%44 marj). Çok nadir fırsat: bilgisiz satıcı + iyi ekspertiz.

### Senaryo 3 — **Selin** (aceleci satıcı) → **Berk** (zengin müşteri), Samsun Vega S21 128 GB, 26 ay, V = 17.160

RF = 24.000 × 0,77 = 18.480 → V = 18.480 × 0,94 (pil 80) × 0,95 (kasa 75) × 1,04 (kutu+fatura) = **17.160**.
Selin değeri 17.500 biliyor; istenen **18.400**, **R = 0,82 × 17.500 = 14.350**, sabır 3, aciliyet 0,9. Oyuncu ekspertiz yapmadı (S0: "temiz görünüyor").

| Tur | Teklif | Hesap | Sonuç |
|-----|--------|-------|-------|
| 1 | 15.500 | t = 0,20 + 0,15 + 0,135 = 0,485 → 18.400 − 0,485 × 4.050 | 16.436 (teklif < fiyat); güven +5 = 55 |
| 2 | 15.800 | t = 0,50 → 16.436 − 0,50 × 2.086 = **15.393**; teklif ≥ fiyat | **Anlaşma 15.390** |

Satış (Berk, zengin): V_müşteri = 17.675, M = 17.675 × 1,10 × 1,05 = **20.410**, açılış 18.370 (0,90).

| Tur | Oyuncunun istediği | Müşterinin yeni teklifi |
|-----|--------------------|-------------------------|
| 1 | 20.500 | 19.115 (t = 0,365) |
| 2 | 20.000 | 19.607 (t = 0,38) |
| 3 | 19.600 | 19.924 (t = 0,395) → **anlaşma 19.920** |

Net: 19.920 − 15.390 = **+4.530** (%29). Bu ikili (acil satıcı + zengin müşteri) **nadir** olmalı; ilan/müşteri üretiminde ağırlığı düşük tutulacak.

### Senaryo 4 — **Cengiz** (acil satış tuzağı), Yıldız Y8 Plus 128 GB, 20 ay, Profil E, V = 8.850

RF = 17.500 × 0,88 = 15.400; V = 15.400 × 0,856 (pil 66) × 0,88 (yan sanayi ekran) × 0,93 (kasa 65) × 0,82 (arızalı kamera) = **8.850**.
Cengiz kusurları saklıyor: ilan değeri 8.846 ÷ (0,88 × 0,82) = **12.260**, istenen **12.500** ("ACİL SATIŞ – çok uygun!"), R = 0,78 × 12.260 = 9.560, sabır 2, aciliyet 1,0. Oyuncu ekspertiz yapmadı.

| Tur | Teklif | Hesap | Sonuç |
|-----|--------|-------|-------|
| 1 | 10.800 | t = 0,50 → 12.500 − 0,50 × 2.940 = 11.030; güven +5 | 11.030 |
| 2 | 10.400 | t = 0,515 → 11.030 − 0,515 × 1.470 = **10.273**; teklif ≥ fiyat | **Anlaşma 10.270** |

Satış: tipik müşteri (M ≈ 9.290) → ≈ **8.850**. Net: 8.850 − 10.270 = **−1.420** (−%14).
- **S1 (200)** yaptırsaydı: ekranı %70, kamerayı %60 yakalar (ikisini birden %42, en az birini ≈ %88); koz kartlarıyla R düşer veya oyuncu "değeri düşük" görüp vazgeçerdi.
- Ders: **"Ucuz gibi görünen" ilan ucuz değil.** Bu senaryo Gün 5+ (E profili) ile açılır.

### Senaryo 5 — **Rıza Bey** (şüpheli)

- Oyuncu S2 ister → başlangıç güveni 50 → **40**. S3 ister → **30** ve %30 ihtimalle "Cihazı bu kadar kurcalatmam" diyerek reddeder (ekspertiz olmaz, ücret alınmaz).
- Güven **25'in altına** düşerse satıcı masadan kalkar.
- Çözüm yolları: S1 ile yetin, düşük teklif riskini üstlen, veya başka ilana geç. Rıza Bey'in kişilik ipucu ("Bir şey yapmayacaksan iyi") Gün 4'ten itibaren görünür.

---

## 9. Gün Sonu Sistemi

### 9.1 Ekranlar

1. **Gün Sonu Özeti** (her gün, tek ekran): nakit, kâr/zarar, satılanlar, stok, gider, XP, yeni açılanlar.
2. **Para Defteri** (Gün 3'ten itibaren): tüm hareketler satır satır, filtreli.
3. **Haftalık Rapor** (Gün 7).

### 9.2 Kâr tanımı (basit ve tutarlı)

```
Gün Net Kârı = Σ(Satış Fiyatı − Ürün Maliyeti)  −  Boşa Ekspertiz  −  Günlük Gider
Ürün Maliyeti = Alış Fiyatı + Ekspertiz Ücreti + Tamir/Parça
```
Yatırımlar (raf yükseltme, ekipman) **gider değildir**, "işletme varlığı" olur. Toplam servet = nakit + stok (maliyet değeriyle) + işletme varlıkları.
MVP'de stok maliyetle gösterilir (tahmini piyasa değeri sonradan).

### 9.3 Para defteri satır türleri

`Başlangıç sermayesi · Alış · Satış · Ekspertiz (ürüne bağlı) · Boşa ekspertiz · Tamir/Parça · Günlük gider · Yatırım (raf/ekipman)`
Her satırda: gün, tutar, tür, ilgili ürün/NPC. Oyuncu bir satıra basınca **"neden bu kadar?"** açıklaması görür.

### 9.4 Örnek Gün Sonu Özeti (Gün 4)

```
GÜN 4 – GÜN SONU ÖZETİ
Sabah nakit                                      242.550
─ Yıldız Y8 Plus alış                            −13.600
─ Ekspertiz S1 (Y8 Plus, ürüne eklendi)             −200
─ Ekspertiz S1 (alınmayan ilan, BOŞA)               −200
+ Nova N3 Pro satış                              +10.250
─ Günlük gider (kira/elektrik)                      −500
Akşam nakit                                      238.300

Satılan: Nova N3 Pro   maliyet 9.300 → satış 10.250 → kâr +950
GÜN NET KÂRI   = 950 − 200 (boşa) − 500 (gider)   =  +250
Stok: Y8 Plus (maliyet 13.800)          Toplam servet: 252.100 (+250)
XP +55  ·  Görev: "İlk koz kartı" ✔
```

Kaydırmasız tek ekran + "Detay" düğmesi. Zararlı gün de aynı ekranda görünür (oyuncu neyin kaybettirdiğini görür).

---

## 10. Ekonomi (MVP)

### 10.1 Değişkenler (yalnızca bunlar)

| Değişken | Kaynak |
|----------|--------|
| Ürün baz fiyatı | Model tanımı |
| Durum (pil, ekran, kasa, kamera, paket) | Ürün örneği |
| Yaş | Ürün örneği |
| Talep çarpanı | Model bazlı, 0,90–1,10 |
| İlan fiyatı | Satıcı NPC (istenen fiyat) |
| Satış fiyatı | Pazarlık sonucu |

### 10.2 Talep (Gün 5'ten itibaren canlı)

- Gün 1–4: tüm modeller 1,00.
- Gün 5+: her model için günlük `talep = talep + rastgele(±0,02) + 0,20 × (1,00 − talep)` (ortalamaya çekilir), sınır 0,90–1,10.
- **Oyuncu etkisi (para basmayı önler):** son 5 günde aynı modelden satılan her adet talebi **×0,985** çarpar (en fazla −%10).
- Sabah özeti oyuncuya "yükselişte/düşüşte" okları verir (rakam yok, sade).

### 10.3 Beklenen marj

- Satıcıdan ortalama alış ≈ **V × 0,92–0,97**, müşteriye ortalama satış ≈ **V × 1,02** → brüt marj ≈ **%5–12**.
- Ekspertiz ücreti marjın **%3–7**'sine denk gelir; her ilana S3 yaptıran oyuncu marjı yer.

### 10.4 Para basmayı ve sonsuz zenginliği engelleyen kurallar

| Kural | Etki |
|-------|------|
| Raf kapasitesi (başlangıç 6, Gün 5'te 8) | Aynı anda sınırlı stok |
| Günlük müşteri sayısı: `2 + ⌈raftaki ürün × 0,5⌉` (üst sınır 5, itibar bonusu +1–2) | Sınırsız satış yok |
| Günlük ilan sayısı 3–8 | Sınırsız alım fırsatı yok |
| Ekspertiz ve tamir ücretleri | Sürtünme |
| Günlük gider 500 TL (Gün 3'ten) | Boş oynamak kaybettirir |
| Stok bekleme değer kaybı: Gün 3 sonrası günde **%0,2** (en fazla %6) | Stoku bekletmek pahalı |
| Aynı modele satış baskısı (yukarıda) | Aynı ürünü kopyalayarak zengin olunmaz |
| Aynı NPC'ye aynı ürünü geri satma yok | Arbitraj engeli |
| Müşteri Max'ı ≤ V × 1,25 | Aşırı prim yok |
| Dükkân primi tavanı %12 | İtibarla bile sınırlı |
| Hakaret/sabır kuralları | Düşük teklif spam'i cezalı |

### 10.5 Başlangıç hedefleri (dengeleme rehberi)

- Ürün başına brüt marj: %5–12. Zarar eden işlem oranı: %20–30. "Vazgeçilen ilan" oranı: ≥ %20.
- Günlük net kâr bandı ve ilk hafta hedefleri Bölüm 13'te.

---

## 11. İlan ve müşteri üretimi (MVP)

### 11.1 İlan

| Alan | Kural |
|------|-------|
| Günlük ilan sayısı | Gün 1: 3 · Gün 2: 5 · Gün 3–4: 6 · Gün 5+: 7–8 |
| Ömür | 2–4 gün (süre dolunca kalkar) |
| Satıcı | 10 profilden ağırlıklı seçilir (Gün 1–4'te agresif profiller azaltılır) |
| Ürün | Model (segment ağırlığı) + durum profili (Bölüm 4.3) |
| Etiketler | "Acil satış" (Gün 6+), "Kutulu/Faturalı" gibi görünür bilgiler |
| Garanti | Her gün en az 1 "makul fırsat" ve Gün 5+ en az 1 "tuzak riski" üretilir |

### 11.2 Müşteri

- Her müşteri **bir ürünle** ilgilenir (segment/bütçe eşleşmesi). Etiket `M × 1,15`'in üstündeyse müşteri "çok pahalı" diyerek gider.
- Müşteri profili ağırlıkları: Gün 1–2: yalnız Aceleci/Pazarlıkçı; Gün 4'te Şüpheli/Bilgili; Gün 6'da Zengin/Koleksiyoncu.
- Rafta uyumlu ürün yoksa müşteri gider ("kaçan müşteri" günün özetine yazılır).

---

## 12. İlk 7 gün: görevler ve açılan mekanikler

**Prensip:** her gün **tek büyük yenilik**, eskiler açık kalır. Görevler "yönlendirme"dir; atlanabilir, süre sınırı yok. Öğretici ipuçları kapatılabilir.

### Özet tablo

| Gün | Ad | Yeni mekanik/karar | Yeni kişilik/içerik | Beklenen net kâr bandı* |
|-----|----|--------------------|---------------------|--------------------------|
| 1 | İlk Alım | İlan okuma, alış pazarlığı, etiket, "kabul et/reddet" satış (mini döngü), gün sonu özeti, para defteri özeti | Ayşe Hanım (öğretici), 1 müşteri | **+500 – +1.500** |
| 2 | Pazarlık Ustalığı | Satış pazarlığı (çok tur), fiyat önerisi bandı, ilan yenileme | Kemal Abi, Selin, Ozan (satıcı/müşteri) | **+300 – +1.500** |
| 3 | Gizli Bilgi | **Ekspertiz S1**, güven aralığı, günlük gider (500) başlar, tam **Para Defteri** ekranı | Hatice Teyze | **−300 – +1.200** |
| 4 | Koz Kartı | Bulgu → **koz kartı**, "boşa ekspertiz" kavramı, NPC ruh hali göstergesi, kişilik ipuçları | Rıza Bey, Dr. Murat | **−300 – +1.500** |
| 5 | Piyasa ve Risk | **Ekspertiz S2**, **raf yükseltme (6→8, 15.000 TL)**, sabah piyasa özeti (talep), Profil E (sorunlu) ilanlar başlar, Model 10 çıkar | Cengiz (acil satış tuzağı) | **−500 – +2.500** (yatırım hariç) |
| 6 | Uzmanlık | **Test cihazı (12.000 TL) → S3**, **rapor göster** (satışta), "Acil satış" etiketi | Berk (zengin), Nermin (koleksiyoncu) | **−1.000 – +4.500** |
| 7 | Tamir Kararı | **Tamir/parça değişimi** (ekran, pil), haftalık rapor, ilk başarımlar, **hafta sonu özeti** | Tüm kişilikler açık | **−1.500 – +3.000** |

\* Orta seviyede, makul oynayan oyuncu bandı. Uçlar için Bölüm 14'e bakın.

### Gün 1 – İlk Alım *(süre ≈ 8–10 dk)*
- **Amaç:** Tüm döngüyü sadeleştirilmiş şekilde bir kez yaşamak.
- **Akış:** Hikâye (30 sn, atlanabilir) → tek rehberli ilan: **Yıldız Y5 64 GB, 24 ay** (V ≈ 5.280, istenen 5.800, satıcı Ayşe Hanım, kolay mod R = 4.750) → S0 göz muayenesi (ücretsiz, ipucu: "kutusu var, ekran temiz") → alış pazarlığı (tek tur + karşı teklif) → rafa koy, etiket öner → müşteri gelir (kabul/ret + 1 karşı teklif) → **satış** → gün sonu.
- **Görevler:** (ana) "İlk telefonunu al", "İlk telefonunu sat", (isteğe bağlı) "Ayşe Hanım'dan farklı bir ilana bak".
- **İlan havuzu:** 3 ilan (1 rehberli + 2 dolgu; biri bariz pahalı).
- **Ders:** gizli/görünür bilgi, RF kavramı, fiyat tekliflemek.
- **Koruma:** ilk işlemde zarar yok; ilk *serbest* işlemde küçük zarar mümkün.

### Gün 2 – Pazarlık Ustalığı
- **Yeni:** çok turlu satış pazarlığı, etiket fiyatı seçimi ("önerilen band" gösterilir), müşteri kişilikleri (Aceleci, Pazarlıkçı).
- **Görevler:** "Bir müşteriyle 3 turlu pazarlık yap", "Etiketten en az %3 fazlaya sat", (isteğe bağlı) "Bir satıcıyla pazarlıkta 'hakaret' uyarısı gör ve öğren".
- **İlan havuzu:** 5. Satıcılar: Kemal Abi, Selin, Ozan.
- **Ders:** aceleci ile pazarlıkçıyı farklı oynamak.

### Gün 3 – Gizli Bilgi
- **Yeni:** **Ekspertiz S1**, güven aralığı, günlük gider (500 TL), **Para Defteri** tam ekranı.
- **Görevler:** "Bir ilanda S1 ekspertiz yaptır", "Ekspertiz sonucuna göre alım kararı ver (al ya da vazgeç)", (isteğe bağlı) "Defterde bir gideri incele".
- **Ders:** ekspertiz **cevap değil aralık verir**; ücretin karşılığı **risk azaltmak**.
- **Not:** bu gün ilk kez günlük gider düşer; "gider ekranı" öğretici kısa mesajla açılır.

### Gün 4 – Koz Kartı
- **Yeni:** bulgular koz kartı olur, "boşa ekspertiz", ruh hali göstergesi, kişilik ipuçları, Şüpheli ve Bilgili satıcılar.
- **Görevler:** "Bir koz kartını pazarlıkta kullan", "Şüpheli birine ekspertiz iste", (isteğe bağlı) "Yanlış alarm aldığında karta güvenme" — sistem oyuncuya yanlış alarmın olabileceğini bir kez hatırlatır.
- **Ders:** bilgi → pazarlık gücü; bilgili satıcıya blöf çalışmaz.
- **Koruma:** son gün "hakaret uyarısı"nın ceza vermediği gün.

### Gün 5 – Piyasa ve Risk
- **Yeni:** **S2**, **raf 6 → 8 (15.000 TL yatırım)**, sabah piyasa özeti (talep okları), Profil E ilanları, Model 10, Cengiz (acil satış tuzağı). **Hakaret artık cezalı.**
- **Görevler:** "Talebi yükselen bir modeli sat", "S2 ile bir ilanı incele", (isteğe bağlı) "Bir tuzak ilandan uzak dur (ekspertiz sonrası vazgeç)".
- **Ders:** ucuz görünen ilan her zaman iyi değildir; yatırım kararı (raf).

### Gün 6 – Uzmanlık
- **Yeni:** **Test cihazı (12.000 TL) → S3**, satışta **rapor göster**, "Acil satış" etiketi, zengin/koleksiyoncu müşteriler.
- **Görevler:** "S3 ile bir ürünü incele", "Raporla bir müşteriyi ikna et", (isteğe bağlı) "Kutulu/faturalı telefonu Nermin'e sat".
- **Ders:** yatırım geri dönüşü; doğru müşteriye doğru ürün.

### Gün 7 – Tamir Kararı
- **Yeni:** tamir/parça değişimi (ekran, pil), haftalık rapor, ilk başarımlar.
- **Görevler:** "Kusurlu bir telefonu al, tamir et, sat", "Haftalık raporu incele", (isteğe bağlı) "Bir tamirin zarar getirdiğini gör" (bilinçli deney).
- **Ders:** tamir **her zaman kâr etmez** (Bölüm 12.1); haftalık kararlar ve stratejinin özeti.
- **Kapanış:** hafta özeti + "Sonraki hedef: Lv.5 Esnaf". **Kuyumcu/galeri şimdilik yok**: yalnızca ipucu: "Piyasada altın hareketleri konuşuluyor…" (sadece metin).

### 12.1 Tamir tablosu (Gün 7)

| Tamir | Giriş | Orta | Üst | Sonuç | Süre |
|-------|-------|------|-----|-------|------|
| Ekran değişimi (yan sanayi) | 700 | 1.800 | 3.500 | Ekran çarpanı **0,88** | 1 gün |
| Pil değişimi | 250 | 500 | 900 | Pil **%100** (çarpan 1,00) | 1 gün |
| Kasa yenileme | – | – | – | *(MVP dışı)* | – |

Örnek: **Nova N3 Pro, kırık ekran, Profil C-benzeri, V = 6.540** → yan sanayi ekran (1.800): V ≈ 7.675 (**+1.135**). Tamir tek başına **zarar** getirir (−665). Kâr, **alış fiyatındaki büyük indirimden** gelmelidir: bu yüzden Gün 7 kararı "tamir mantıklı mı?" sorusudur.

### 12.2 XP ve seviye (yalnızca bilgi)

Alım +10 · Satış +20 (+ kâr/100, en fazla +30) · Ekspertiz +5 · Koz kartı kullanımı +10 · Görev +50. Lv.2: 100 · Lv.3: 250 · Lv.4: 450 · Lv.5 "Esnaf": 700 XP. Gün 7'de tipik Lv.3–4.

---

## 13. İlk 7 gün ekonomik değerler ve örnek oyuncu

### 13.1 Sabit ekonomik değerler

| Kalem | Değer |
|-------|-------|
| Başlangıç sermayesi | 250.000 TL |
| Günlük gider | Gün 1–2: 0 · Gün 3+: **500 TL** |
| Raf kapasitesi | 6 (Gün 5'te 15.000 TL ile 8) |
| Test cihazı | 12.000 TL (Gün 6) |
| Ekspertiz | Bölüm 5.1 |
| Tamir | Bölüm 12.1 |
| Dükkân primi | %5 (itibarla %12'ye) |
| Günlük ilan | 3 · 5 · 6 · 6 · 7–8 |
| Günlük müşteri | 2–5 |

### 13.2 Örnek oyuncu: "Dengeli oyuncu", 7 günlük defter

| Gün | Alışlar | Ekspertiz | Satışlar (maliyet → satış) | Gider | Gün net kâr | Akşam nakit | Stok (maliyet) |
|-----|---------|-----------|----------------------------|-------|------------|-------------|----------------|
| 1 | Yıldız Y5 4.800 | – | Y5: 4.800 → **5.750** | 0 | **+950** | 250.950 | 0 |
| 2 | Nova N1 Lite 3.700 · Vega A3 7.900 | – | N1 Lite: 3.700 → **4.350** | 0 | **+650** | 243.700 | 7.900 |
| 3 | Nova N3 Pro 9.100 | S1 200 | Vega A3: 7.900 → **8.650** | 500 | **+250** | 242.550 | 9.300 |
| 4 | Yıldız Y8 Plus 13.600 | S1 200 (+200 boşa) | N3 Pro: 9.300 → **10.250** | 500 | **+250** | 238.300 | 13.800 |
| 5 | Vega S21 15.390 · Zirve Z5 12.300 | S2 600 | Y8 Plus: 13.800 → **14.900** | 500 | **+600** | 209.410 | 28.290 |
| 6 | Elma E11 16.500 | S3 1.200 | Vega S21: 15.390 → **19.920** · Z5: 12.900 → **13.600** | 500 | **+4.730** | 212.730 | 17.700 |
| 7 | N3 Pro (kırık) 4.800 + tamir 1.800 | S1 200 | E11: 17.700 → **18.900** | 500 | **+700** | 224.330 | 6.800 |

Not: Gün 5'te raf yatırımı −15.000, Gün 6'da ekipman −12.000 (nakitten çıkar, gider sayılmaz; servet değişmez).

**Hafta sonu:**
- Satış toplamı **96.320** · Satılan ürün maliyeti **85.490** → **brüt kâr 10.830** (%12,7).
- − boşa ekspertiz 200 − günlük gider 2.500 → **net kâr +8.130**.
- Toplam servet: 224.330 (nakit) + 6.800 (stok) + 27.000 (raf+ekipman) = **258.130** (%+3,3).
- Gün 6'daki Selin → Berk ikilisi (+4.530) şanslı bir denk gelmeydi; onsuz hafta ≈ **+3.600**.

Dürüst not: ilk hafta %3'lük büyüme "az" görünebilir. Bu, sermaye kısıtsız + öğrenme haftası + yatırım haftası olduğu için bilinçli. Prototip testlerinde "oyuncu bunu yavaş buluyor mu?" sorusunu **açıkça ölçeceğiz**. Gerekirse günlük müşteri/ilan sayısı veya marjlar ayarlanır.

### 13.3 Oyuncu tipleri ve ilk hafta net kâr aralıkları (tahmin)

| Oyuncu tipi | Davranış | Tahmini ilk hafta net | Not |
|-------------|----------|------------------------|-----|
| **Dengeli** | Riskli ürüne S1/S2, ucuza kaçmaz | **+4.000 – +14.000** (tipik ≈ +8.000) | Yukarıdaki örnek |
| **Temkinli** | Yalnızca net fırsatı alır, sık vazgeçer | **+2.000 – +8.000** | Az işlem, düşük varyans |
| **Ekspertiz fanatiği** | Her ilanda S2/S3 | **−1.000 – +6.000** | Ekspertiz marjı yer; zarar riski düşük |
| **Kumarbaz** | Ekspertiz yok, çok işlem, acil satışa atlar | **−12.000 – +16.000** (ort. ≈ +1.500) | Yüksek varyans; Gün 5+ tuzaklara düşer |
| **Pasif** | Neredeyse işlem yapmaz | **−2.500 – 0** | Yalnızca gider |
| **Lüks odaklı** (sadece üst segment) | Büyük tutar, aynı % marj | **+2.000 – +18.000** | Mutlak rakamlar büyük, risk de büyük |

**Tasarım güvence hedefi:** Hiçbir oyuncu ilk haftada, **kendi savurgan/kumarbaz kararı olmadan**, sermayesinin %10'undan fazlasını (25.000 TL) kaybetmemeli. Aşırı kötü durumda bile pratik alt sınır ≈ **−20.000 (−%8)** olacak şekilde ilan/kişilik/Gün 1–4 koruması tasarlanmıştır.

### 13.4 Eğlence testi: "prototip başarılı mı?" ölçütleri

| Ölçüt | Hedef |
|-------|-------|
| Gün başına oturum süresi | 6–12 dk |
| Oyuncuların ekspertizi isteyerek kullanma oranı (Gün 4+) | ≥ %60 |
| Görülen ilanların vazgeçilme oranı | %20–40 |
| Zarar eden işlem oranı | %20–30 |
| Koz kartı kullanan pazarlık oranı (Gün 5+) | ≥ %40 |
| "Ne yapacağımı bilmiyorum" sıkıntısı (anket) | < %15 |
| 7. günün sonunda "8. günü oynamak isterim" | ≥ %70 |
| "Bir karar verdim" hissi (anket) | Her işlemde ≥ %80 |

---

## 14. Nasıl test edeceğiz (ekonomi simülasyonu)

Kodlamadan sonra ilk iş: **arayüzsüz simülasyon.** Yapay oyuncu tipleri (Bölüm 13.3) 1000 kez 7 gün oynar; ilk hafta net kâr dağılımı Bölüm 13.3 ile karşılaştırılır; sapma varsa denge dosyası ayarlanır. Tasarım kararı: **Denge, koda değil veri dosyasına yazılır** (Bölüm 16).

---

## 15. Sektör bağımsız ortak altyapı (Karar #3 ve #4)

### 15.1 Ortak "Ticaret Çekirdeği" kavramları

Telefon sadece ilk **içerik paketi**. Çekirdek şunları sektörden bağımsız bilir:

| Kavram | Sade açıklama | Telefon | Altın (sonra) | Araç (sonra) | Emlak (sonra) |
|--------|---------------|---------|----------------|---------------|----------------|
| **Ürün Tanımı** | Şablon | Model | Ürün türü (bilezik, çeyrek…) | Marka-model-yıl | Bölge/tip |
| **Ürün Örneği** | Somut ürün | Pil %78 telefon | 22 ayar 14 gram bilezik | 2019, 85.000 km araç | 3+1, 12 yıllık daire |
| **Nitelik (Attribute)** | Ölçülebilir/kategorik alan | Pil, ekran, kasa | Ayar, gram, işçilik | Km, tramer, boya | m², kat, bina yaşı |
| **Gizlilik + Gözlem** | Hangi alan gizli, nasıl ölçülür | Ekspertiz tablosu | Ayar/ağırlık testi | Boya/motor testi | Tapu/ekspertiz |
| **Değer Formülü** | Çarpan zinciri | RF × durum × paket | Gram × fiyat + işçilik | Baz × km × hasar | m² × bölge × durum |
| **Ekspertiz Kuralı** | Seviyeler ve tespit olasılığı | S1–S3 | Test cihazları | Usta/ekspertiz | Uzman raporu |
| **Piyasa Endeksi** | Talep/arz | Model talebi | Altın fiyatı | Araç endeksi | Bölge endeksi |
| **Satış Kanalı** | Nasıl satılır | Raf | Vitrin | Galeri | İlan/kiralama |

- **Pazarlık, ilan pazarı, defter, kayıt, ekspertiz mantığı** ürünün ne olduğunu bilmez; sadece "nitelik listesi + değer formülü + gizlilik kuralı" ile çalışır.
- Telefon, bu altyapının ilk **sektör paketi**. Yeni sektör = yeni veri tabloları + küçük kural sınıfları.

### 15.2 Ortaklaşa kullanılacak sistemler

Envanter · Para defteri · Pazarlık motoru · Ekspertiz motoru · İlan üretici · Müşteri üretici · XP/görev/başarım · Kayıt sistemi. **Hiçbiri "telefon" kelimesini bilmemeli.**

### 15.3 Sektör kilidi açma: birden çok ölçüt

Kilit açma bir **koşul listesidir** (veriyle tanımlanır, koda gömülmez). Örnek koşul türleri:

`Oyuncu seviyesi · İşletme seviyesi · İtibar · Tamamlanan görev(ler) · Başarım(lar) · Nakit/servet eşiği · İşletme değeri`

Örnek (yalnızca taslak; MVP sonrası ayarlanacak):
| Sektör | Örnek koşullar |
|--------|----------------|
| Kuyumcu | Lv.10 + itibar ≥ X + "Usta Ekspertizci" başarımı + servet ≥ Y |
| Galeri | Lv.15 + Kuyumcu tanımlı + işletme değeri ≥ Z + 10 görev |
| Emlak | Lv.20 + iki sektör + itibar ≥ W |

Bu MVP'de **hiç kilit yok** (yalnız telefoncu). Ancak "açılım kuralı" altyapısı Gün 1–7 mekanik açılımlarında zaten kullanılır (örn. "Gün ≥ 5 **veya** ekspertiz sayısı ≥ 8" → S2). Böylece gerçek sektör kilitleri aynı sistemden geçer.

### 15.4 Kredi/borç

MVP'de **yok**. Oyuncu temel ticaret döngüsünü öğrendikten sonra (MVP sonrası) "kredi" bir açılım kuralı ile açılır. Kayıt ve defter yapısı buna hazır olacak şekilde tasarlanır (defter türleri genişletilebilir).

---

## 16. Veri dosyaları ve "Denge Dosyası" (kod öncesi hazırlık)

Şu sayılar kodda değil veri dosyalarında duracak. Tasarım değişikliği = dosya değişikliği:

| Dosya | İçerik |
|-------|--------|
| `phone_models` | Bölüm 3.2 tablosu (model, baz fiyat, hafıza çarpanları, yaş aralığı) |
| `condition_profiles` | Bölüm 4.3 profil ağırlıkları ve aralıkları |
| `value_tables` | Yaş çarpanı, ekran/kamera/paket çarpanları, pil/kasa formülü katsayıları |
| `appraisal_levels` | Bölüm 5 (ücret, tespit/yanlış alarm olasılıkları, aralık genişlikleri, kanıt gücü) |
| `npc_profiles` | Bölüm 6 (satıcı + müşteri sayıları) |
| `negotiation_rules` | t formülünün katsayıları, hakaret eşiği, güven değişimleri, kart kuralları |
| `economy_constants` | Günlük gider, raf kapasitesi, dükkân primi, bekleme kaybı, talep parametreleri |
| `day_unlocks` | Gün 1–7 açılım kuralları, görevler, ilan havuzu ayarları |
| `tutorial_scripts` | Gün 1 rehberli ilan/dialog |

---

## 17. Liste A — MVP'de **kesinlikle yapılacaklar**

1. Gün sistemi: sabah özeti → ilanlar → ticaret → işletme → **Günü Bitir** → gün sonu özeti.
2. Telefon veri modeli: 10 model, hafıza/yaş/durum, gizli alanlar, değer formülü.
3. İlan pazarı: günlük ilan üretimi, ilan ömrü, 10 satıcı profili, durum profilleri (A–E).
4. Göz muayenesi (S0) + **Ekspertiz S1, S2, S3** (güven aralığı, kategorik bulgu, yanlış alarm, kilitli sonuç).
5. **Risk kartı** (kötü/orta/iyi senaryo).
6. **Alış pazarlığı**: gizli R, sabır, güven, aciliyet, ruh hali göstergesi, hakaret kuralı.
7. **Koz kartı sistemi** (bulgu → pazarlık gücü).
8. Envanter (raf kapasitesi 6→8), etiket fiyatı, rafa koyma.
9. Müşteri üretimi + **satış pazarlığı** + **rapor göster**.
10. **Para defteri** (satır satır), gün sonu özeti, haftalık rapor.
11. Günlük gider, stok bekleme kaybı, talep (Gün 5+), satış baskısı (para basma engelleri).
12. Tamir/parça değişimi (ekran, pil) — Gün 7.
13. XP + seviye (Lv.1–5), Gün 1–7 görevleri, ilk başarımlar (küçük liste).
14. Otomatik kayıt/yükleme (Save sistemi çekirdeği).
15. Öğretici akışı (Gün 1–7 açılım), atlanabilir ipuçları.
16. Sade 2D UI: gri kutular/ikon, listeler, düğmeler, tek ana eylem/ekran.
17. Denge dosyaları + **arayüzsüz ekonomi simülasyonu** (Bölüm 14).
18. Sektör-bağımsız çekirdek soyutlamaları (Ürün tanımı/örneği, nitelik, gözlem, değer formülü) — yalnızca telefonla doldurulmuş hâlde.

## 18. Liste B — MVP'de **kesinlikle yapılmayacaklar**

1. Galeri, kuyumcu, emlak, altın/araç/ev ürünleri.
2. Çalışanlar, maaşlar, deneyim/yetenek.
3. Büyük işletme sistemi, şubeler, harita, 3D dünya, karmaşık dükkân dekorasyonu.
4. **Kredi/borç**, faiz.
5. Arz-talep simülasyonunun karmaşık hali, piyasa trendleri, ekonomik olay sistemi, haber sistemi (yalnızca basit talep okları).
6. IMEI, garanti, su hasarı, yazılım kilidi.
7. S4 (uzman) ekspertiz, mini oyunlar, animasyonlu ekspertiz.
8. Koleksiyon sistemi, nadir ürün mekanikleri.
9. 30 günlük ilerleme sistemi (Gün 8+).
10. Monetization: reklam, kozmetik, satın alma.
11. Bulut kaydı, çoklu slot, sosyal özellikler, liderlik tabloları.
12. Çok dil (yalnızca TR; metinler yine de tabloda).
13. Enerji, streak, gerçek zamanlı bekleme, günlük giriş ödülü.
14. Ses/müzik cilası, görsel kalite yükseltme.

## 19. Liste C — MVP sonrası yapılacaklar (öneri sırası)

1. **MVP eğlence testi sonrası denge turu** (7 gün hissi; ölçütler Bölüm 13.4).
2. Gün 8–30 ilerleme sistemi (7 günlük döngü onaylanırsa).
3. Koleksiyon ve nadir telefonlar, başarımlar.
4. Piyasa sistemi: arz-talep, trendler, yeni model çıkışı olayı ve haber/söylenti sistemi.
5. Çalışanlar: Satış danışmanı → Ekspertizci → Teknisyen.
6. **Kredi/borç sistemi** (temel döngü oturduktan sonra).
7. **Kuyumcu** sektörü (mimarinin ilk gerçek sınavı).
8. Galeri, ardından Emlak (bölge sistemi, kira).
9. Çok slotlu kayıt, bulut kaydı.
10. Kozmetik ürünler, isteğe bağlı ödüllü reklam (rahatlık amaçlı, doğrudan TL yok).
11. Ses/görsel kalite, mağaza hazırlığı (kurgusal marka → hukuki kontrol).
12. EN dil ve diğer diller.

---

## 20. Sonraki adım (kod öncesi son kontrol)

v0.2'yi onaylarsan şu sırayla ilerleriz (**önce hâlâ kod yok**, sadece küçük kararlar):

1. Bölüm 13.2 örnek haftası ve 13.3 aralıkları sana **çok yavaş/çok hızlı** görünüyor mu? (Denge hissi.)
2. Gün 3 için "Para Defteri tam ekranı" mı, yoksa Gün 1'den mi açılsın? (Şu an özet Gün 1, tam ekran Gün 3.)
3. Kredi hazırlığı için defter türlerinin genişletilebilir olması yeterli mi?
4. **v0.3 (teknik):** Unity mimarisi ve **ilk 2 haftalık geliştirme planı** (hangi sınıf/klasör/test hangi sırayla), veri dosyası formatı (JSON vs ScriptableObject kararı), ve simülasyon aracının tasarımı.
