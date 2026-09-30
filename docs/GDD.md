# ESNAF – Game Design Document (GDD) ve Teknik Mimari

Sürüm: 0.1 (taslak) · Durum: Kod yok, yalnızca tasarım. Birlikte netleştireceğiz.

> Okuma notu: Teknik terimler sade dille açıklanır. "Not:" ile başlayan kutular başlangıç seviyesi geliştirici için ek açıklamadır.

---

## 0. Önce dürüst bir analiz: Tasarımdaki sorunlar ve çelişkiler

Kod yazmadan önce çözmemiz gereken noktalar. Her birinin altında önerim var. Aksini istersen değiştiririz.

| # | Sorun | Neden sorun? | Önerim |
|---|-------|--------------|--------|
| 1 | **Kapsam devasa.** 4 sektör + emlak kiralama + çalışanlar + piyasa simülasyonu + 8 NPC kişiliği. | Tek/az kişilik ekip için "her şey birden" projeyi bitirmez. | **Dikey dilim (vertical slice) ilkesi:** İlk sürüm SADECE telefoncu. Diğer sektörler aynı altyapıya "eklenti" olarak girer (bkz. Bölüm 1 ve 13). |
| 2 | **"Her gün yeni içerik" ile "yapay zorlama yok" çelişebilir.** Gün = gerçek gün mü, oyun günü mü? Gerçek gün olursa "her gün gir" baskısı (streak) yaratır. | Bu tam olarak kaçınmak istediğin yapay zorlama. | **"Gün" = oyun içi gün.** Oyuncu "Günü Bitir" tuşuna basınca ilerler. Yeni içerik *oyun günü/başarı eşiğine* bağlı açılır, gerçek zamana değil. Bekleme sayaçı, enerji, "yarın gel" yok. |
| 3 | **Ölçek uyuşmazlığı.** 250.000 TL ile telefon alınır ama araba (500 bin–2 milyon) ve ev (milyonlar) uzak. | Galeri/emlak "kilit açma" anında oyuncu sermaye duvarına çarpar. | Kilit açma **seviye + servet** eşiğine bağlı olsun. Emlak için ara basamaklar: küçük dükkân, garaj, arsa, **kiralık dükkân kurma**. Ev fiyatları için "hisse/ortaklık" veya **kredi (borç)** sistemi düşünülebilir (borç zaten para sisteminde var). |
| 4 | **Enflasyon/TL sayı ölçeği.** Gerçek TL fiyatları hızla eskir. | Oyun 1 yıl sonra "gerçekçi olmayan fiyat" hissi verir. | Tüm fiyatlar veride **baz fiyat × ekonomi çarpanı** olsun. Gerçek dünyaya uyum çarpanı değiştirerek sağlanır, kod değişmez. |
| 5 | **Gerçek marka/model isimleri.** Apple, Samsung, BMW vb. | Marka/lisans ve mağaza reddi riski. | Prototipte **kurgusal markalar** (ör. "Elma", "Samsun", "Yıldız Mobile"). Sonra hukuki karar verilir. |
| 6 | **IMEI/kayıt dışı/çalıntı telefon.** | Yasadışı işi "nasıl yapılır" gösteren içerik mağaza politikalarına takılabilir. | IMEI durumu sadece **risk etiketi** olsun: "Temiz / Kayıtsız (kullanılamaz, değeri çok düşük) / Şüpheli". Yasadışılığı özendirme yok; sonuç her zaman kötü. |
| 7 | **Rastgele olaylar vs oyuncu kontrolü.** | Ani fiyat çöküşü "haksızlık" hissi verir. | Olaylar **önceden haber verir** ("Pazarda söylenti: Yeni model geliyor, 3 gün sonra"). Oyuncu hazırlanabilir. Etkiler sınırlı yüzdeyle. |
| 8 | **Kuyumcuda gizli bilgi azlığı.** Altın gram×fiyat hesabı olduğu için belirsizlik az. | Ekspertiz/pazarlık heyecanı düşer. | Belirsizlik: **ayar sahteliği, ağırlık farkı, taş çıkarma, işçilik değeri, antika/koleksiyon değeri.** Test aletleri ekspertiz ekipmanı olur. |
| 9 | **Ekspertiz sıkıcı bir "bekleme"ye dönüşebilir.** | Mobilde her ürün için uzun işlem sıkar. | Ekspertiz **tek dokunuşla, kademeli** olsun (bkz. Bölüm 6). Sonuç anında, maliyet para/zaman dilimi. Gereksiz animasyon yok. |
| 10 | **Sekiz NPC kişiliği + çok ürün = içerik kalabalığı.** | Dengelemesi zor. | Kişilik = **birkaç sayı** (sabır, bilgi, zenginlik, şüphe...). Yeni kişilik eklemek veri eklemektir, kod değil. Prototipte 4 kişilikle başla. |
| 11 | **Adil monetization + uzun ekonomi.** | Ödüllü reklamla "para verme" bile dengeyi bozabilir. | Ödüllü reklam **sadece rahatlık** versin (ekstra ilan yenileme, ekspertiz indirimi ipucu) ve **günlük sınırlı**. Doğrudan para/TL ödülü yok. Asıl gelir: kozmetik (dükkân dekoru, avatar) + reklamsız paket. |
| 12 | **Mobilde kayıt güvenliği.** Uygulama her an kapatılabilir. | Kayıp ilerleme = kullanıcı kaybı. | Her "gün sonu" + önemli işlemden sonra **otomatik kayıt**, yedekli dosya (bkz. Bölüm 11). |
| 13 | **Oyuncu davranışı fiyatı etkiler** ama oyun **tek oyunculu/çevrimdışı**. | Oyuncu bir modeli çok alıp satınca fiyat "gerçekten" etkilenmeli mi? | Evet ama **yerel/hafif**: "Bölge/kategori talep" değeri, oyuncunun satışlarıyla küçük oranda oynar ve zamanla normale döner. Sunucu gerekmez. |

**Kritik karar (senden onay istiyorum):**
1. Oyun **gün bazlı, sıra tabanlı (turn-based)** olsun. Gerçek zamanlı akış yok. Bu hem mobil pil hem kod basitliği hem test edilebilirlik açısından en iyisi.
2. Prototip = **yalnızca telefoncu**, 2D arayüz (UI ağırlıklı, oyun sahnesi yok).

---

## 1. Oyunun genel mimarisi

### 1.1 Büyük resim (sade anlatım)

Oyunu **katmanlara** ayırıyoruz. Üst katman alt katmanı kullanır, alt katman üst katmanı bilmez. Böylece bir yeri değiştirince her yer bozulmaz.

```
┌───────────────────────────────────────────────┐
│  4. SUNUM KATMANI (UI / Ekranlar / Ses)       │  Oyuncunun gördüğü
├───────────────────────────────────────────────┤
│  3. OYUN SİSTEMLERİ (Ekspertiz, Pazarlık...)  │  Kurallar
├───────────────────────────────────────────────┤
│  2. ÇEKİRDEK (Zaman, Para, Envanter, Olaylar) │  Herkesin kullandığı temel
├───────────────────────────────────────────────┤
│  1. VERİ (Ürün tanımları, NPC, fiyat tabloları│  İçerik (kodsuz değiştirilebilir)
│     + Kayıt dosyaları)                        │
└───────────────────────────────────────────────┘
```

> **Not (sade dil):** "Katman" = kod klasörlerinin sorumluluk sırası. Ekranı (UI) değiştirmek pazarlık kurallarını bozmamalı.

### 1.2 Temel mimari kararlar

| Karar | Seçim | Sebep |
|-------|-------|-------|
| Zaman modeli | Gün bazlı, oyuncu "günü bitirir" | Basit, test edilir, baskı yok |
| Sahne yapısı | **Tek ana sahne + UI panelleri** | Mobilde hafif; 3D/dünya gezisi gerekmez |
| Kod düzeni | Modüller (Assembly Definition) | Derleme hızlı, bağımlılık kontrollü |
| Veri | **Tanımlar (ScriptableObject/JSON)** ile **Durum (Save verisi)** ayrı | Ürün "şablonu" ile oyuncunun elindeki "somut ürün" karışmasın |
| İletişim | **Olay sistemi (Event Bus)** | Sistemler birbirini doğrudan tanımadan haberleşir |
| Rastgelelik | Tek merkezden, **seed'li** rastgele sayı üreteci | Aynı seed = aynı sonuç → hata ayıklama ve test kolaylaşır |
| Test | Saf C# mantık sınıfları (Unity'ye bağımlı değil) | Unity açmadan test edilir |
| Sektörler | Ortak "Sektör" arayüzü, her sektör bir eklenti | Kuyumcu/galeri sonradan eklenir |

### 1.3 En önemli kavram: Şablon vs Örnek (Definition vs Instance)

- **Tanım (Definition):** "Bu modelin adı X, çıkış yılı 2022, baz fiyatı 20.000 TL." Değişmez, tasarımcı içeriği. (ScriptableObject/JSON)
- **Örnek (Instance):** "Şu an pazardaki, pil sağlığı %78, ekranı değişmiş X telefon." Oyun sırasında üretilir, kaydedilir.

Bu ayrımı yapmazsak veri içerik ile kayıt birbirine girer. **Kesin kural.**

### 1.4 Ana modüller

| Modül | Sorumluluk |
|-------|-----------|
| `Core.Time` | Gün sayacı, "günü bitir", gün olayları |
| `Core.Economy` | Para, işlem kaydı (defter), borç, servet hesabı |
| `Core.Inventory` | Ürün örnekleri, stok, sergileme |
| `Core.Events` | Oyun içi olay dağıtımı (Event Bus) |
| `Core.Random` | Seed'li rastgele sayı |
| `Core.Save` | Kayıt/yükleme, sürümleme |
| `Systems.Market` | El pazarı: ilan üretme, ilan ömrü |
| `Systems.Appraisal` | Ekspertiz |
| `Systems.Negotiation` | Pazarlık motoru + NPC kişilikleri |
| `Systems.Pricing` | Fiyat/piyasa simülasyonu |
| `Systems.Progression` | XP, seviye, görev, başarım, kilit açma |
| `Systems.Business` | İşletme, mağaza seviyesi, çalışan |
| `Sectors.Phone / Car / Gold / Estate` | Sektöre özel ürün kuralları |
| `UI.*` | Ekranlar (Pazar, Dükkân, Envanter, Pazarlık...) |

Bağımlılık kuralı: `Sectors` → `Systems` → `Core`. `Core` hiçbir şeyi tanımaz. `UI` hepsini dinler ama kurallar UI içinde yazılmaz.

---

## 2. Ana oyun döngüsü

### 2.1 Mikro döngü (bir işlem, 1–3 dakika)

```
İlan seç → İncele (görünen bilgiler) → [İsteğe bağlı] Ekspertiz
→ Değer tahmini yap → Pazarlık → Al / Vazgeç
```

### 2.2 Orta döngü (bir oyun günü, 5–10 dakika)

```
Sabah: Günün ilanları + haberler + günlük hedef
→ 3–6 alım fırsatı değerlendir
→ Stoktaki ürünleri sergile / fiyatla / (tamir et)
→ Gelen müşterilerle satış pazarlığı
→ "Günü bitir": gider öde, ilan süresi azalır, piyasa güncellenir, kayıt alınır
→ Gün özeti (kâr/zarar, XP, yeni açılanlar)
```

### 2.3 Makro döngü (hafta/ay)

```
Sermaye büyür → Seviye atlar → Yeni ekipman/çalışan/mağaza
→ Yeni sektör açılır → Daha yüksek riskli, daha yüksek marjlı işler
→ Servet hedefleri → Şirket/holding
```

### 2.4 Döngüyü "eğlenceli" yapan üç karar noktası

1. **Almadan önce:** Ekspertiz yaptırayım mı (para/zaman) yoksa riske mi gireyim?
2. **Pazarlıkta:** Ne kadar zorlayayım? Satıcı kaçarsa fırsat gider.
3. **Satışta:** Hemen düşük kârla mı sat, yoksa beklerken stok maliyeti/piyasa riski mi taşı?

Bu üç karar her sektörde aynı kalır; sektör sadece "hangi bilgi gizli, hangi ekipman ölçer" kısmını değiştirir. **Tüm sektörler aynı iskeleti kullanır.**

### 2.5 Kâr kaynakları ve para sürtünmeleri (ekonomiyi anlaşılır tutmak için)

Oyuncu şunlardan para kazanır: ucuz alıp pahalı satmak, ekspertizle gizli değeri bulmak, tamir/parça ile değer artırmak, doğru zamanda satmak.
Para sürtünmeleri (sürekli kâr edip şişmeyi engeller): kira/gider, ekspertiz ücreti, tamir maliyeti, stok bekleme değer kaybı, komisyon/vergi, yanlış alım.

---

## 3. Oyuncunun ilk 30 dakikası

Amaç: Oyuncu 30 dakika içinde **döngünün tamamını 2–3 kez** yaşasın, kaybederse bile "ben hata yaptım" desin, "oyun haksız" dememeli.

| Dakika | Ne olur | Öğretilen |
|--------|---------|-----------|
| 0–2 | Kısa açılış: "Dayın dükkânı devretti, 250.000 TL cebinde." Dükkân ekranı. | Hikâye + para |
| 2–5 | **Rehberli ilk ilan:** Pazarda tek ilan, satıcı ürünü açıkça tanımlıyor. | İlan okuma, görünen/gizli bilgi |
| 5–8 | **İlk ekspertiz (ücretsiz, eğitici):** Pil %78, ekran değişmiş görünür. | Ekspertiz neyi açığa çıkarır |
| 8–12 | **İlk pazarlık (alış):** Kolay NPC (Aceleci). 3 seçenek: teklif ver / zam iste / vazgeç. | Pazarlık temeli, "son fiyat" |
| 12–15 | Ürünü **sergile**, etiket fiyatı belirle (öneri gösterilir). | Fiyatlama |
| 15–20 | İlk müşteri gelir, **satış pazarlığı.** İlk kâr! | Kâr, XP, ilk görev tamamlandı |
| 20–25 | **Serbest tur:** 3 ilan. Biri bariz tuzak (örn. yüksek fiyat + kutu/fatura yok). Ekspertiz artık ücretli. | Risk vs ekspertiz maliyeti |
| 25–28 | **Günü bitir** ilk kez: gün özeti, gider, XP. | Gün döngüsü, gün sonu raporu |
| 28–30 | Seviye 2 + **ilk hedef:** "Bu hafta 10.000 TL kâr" ve bir ipucu: "Yakında yeni model çıkacağı söyleniyor." | Uzun vadeli hedef + piyasa haberi |

Tasarım kuralları (ilk 30 dk):
- Oyuncu ilk işte **zarar etmesin** ama ilk *serbest* işte küçük zarar yapabilsin (öğrenme).
- İlk 10 dakikada **pop-up yağmuru yok**: her yeni mekanik tek seferde tek ipucu.
- Ekran başına **tek ana eylem** butonu.
- Öğretici ipuçları **atlanabilir**, Ayarlar'dan tekrar açılır.

---

## 4. İlk 30 günlük ilerleme sistemi

**Prensip:** "Gün N" bir *oyun günü* ve içerik **eşik + gün** ile açılır (bkz. 0. bölüm #2). Zorunlu günlük görev yok; hedefler *öneridir*, atlanabilir. Kaçırılan içerik kaybolmaz.

### 4.1 Taslak yol haritası (detaylandırmaya açık)

| Gün | Yeni açılan | Odak |
|-----|-------------|------|
| 1 | Pazar, ekspertiz (temel), pazarlık (alış) | Öğrenme |
| 2 | Satış pazarlığı, müşteri | İlk kâr |
| 3 | Görevler + günlük hedef | Yönlendirme |
| 4 | **Parça değişimi/tamir** (ekran, pil) | Değer artırma |
| 5 | Piyasa haberi (söylenti) sistemi | Olay okuma |
| 6 | NPC kişilikleri: Pazarlıkçı, Şüpheli | Pazarlık çeşitliliği |
| 7 | **Haftalık özet + ilk başarımlar**, mağaza gideri (kira) | Ritim |
| 8–9 | Stok limiti artırma (raf) | Kapasite kararı |
| 10 | **Ekspertiz cihazı** (kademe 2) | Ekipman yatırımı |
| 11 | Koleksiyon: nadir ürün ilanları | Avcılık |
| 12 | Bilgili & Zengin müşteri | Yüksek marjlı satış |
| 13 | **Acil satış** ilan türü (fırsat/risk) | Risk-ödül |
| 14 | 2. hafta özet, **Lv.5 "Esnaf"** hedefi, mağaza yükseltme | Büyüme |
| 15 | Borç/kredi (küçük, açık faiz) | Kaldıraç kararı |
| 16 | Piyasa: yeni model çıkışı olayı (eski modeller düşer) | Piyasa okuma |
| 17 | Koleksiyoncu NPC + nadir telefonlar | Uzmanlaşma |
| 18 | **İlk çalışan:** Satış danışmanı | Delegasyon |
| 19 | Günlük görev çeşitleri (kategori bazlı) | Çeşitlilik |
| 20 | Çalışan #2: Ekspertizci (daha doğru ekspertiz) | Doğruluk |
| 21 | 3. hafta özet, bölgesel talep (mahalle) | Konum önemi |
| 22 | **Gümüş/altın haber teaser'ı:** "Kuyumcu açılışı yakın" | Yeni sektör beklentisi |
| 23–24 | Büyük müşteri olayı (toplu alım) | Stok planı |
| 25 | Mağaza seviye 2 (büyük dükkân) | Büyüme |
| 26 | Şube/ikinci dükkân ön izlemesi | Uzun vade |
| 27 | Başarım ve koleksiyon ekranı | Hedefler |
| 28 | **Lv.10 "Usta" hedefi**, 4. hafta özet | Kilometre taşı |
| 29 | Sektör seçimi: **Oto Galeri veya Kuyumcu** (önce hangisi?) | Anlamlı seçim |
| 30 | **Sektör kilidi açılır + 30 gün özet raporu + "Hikâye Perdesi 1" sonu** | Kapanış ve yeni hedef |

### 4.2 Bu sistemi nasıl yapay olmaktan çıkarırız
- Her gün "yeni bir şey" şart değil; **haftada 1 büyük, aradaki günler küçük** açılım.
- Açılımlar bir **"İlerleme tablosu"** ekranında görünür ve isteyen ilerler.
- Oyuncu hızlıysa: eşik = seviye/servet. Yavaşsa: gün eşiği. **İkisinden biri** açar.
- Streak/ceza yok.

---

## 5. Telefoncu sisteminin detaylı tasarımı

### 5.1 Telefon örneği (Instance) alanları

**Sabit (model tanımından gelir):** marka, model, çıkış yılı, segment (giriş/orta/üst), baz fiyat, depolama seçenekleri, RAM seçenekleri.

**Örnekte değişken:**

| Alan | Değerler | Gizli mi? |
|------|----------|-----------|
| Hafıza / RAM | Modelin seçeneklerinden | Görünür |
| Yaş (ay) | 0–72 | Görünür (beyan) |
| Pil sağlığı | % (40–100) | Gizli (ilanda "iyi" yazar) |
| Ekran | Orijinal / Yan sanayi / Çizik / Kırık | Kısmen |
| Kasa | Kozmetik % (0–100) | Görünür (fotoğraf) fakat kaba |
| Kamera | Sağlam / Lekeli / Arızalı | Gizli |
| Parça değişimi | Liste (ekran, pil, kasa...) | Gizli |
| Garanti | Yok / Kalan ay | Kısmen |
| Kutu / Fatura | Var / Yok | Görünür |
| IMEI durumu | Temiz / Kayıtsız / Şüpheli | Gizli |
| Gerçek değer | Hesaplanır | Gizli |
| İstenen fiyat | Satıcı belirler | Görünür |

Ek (sonra): Şarj portu, Face/Touch ID, su hasarı, yazılım kilidi, batarya döngü sayısı.

### 5.2 Gerçek değer nasıl hesaplanır (sade formül mantığı)

```
Gerçek Değer = Baz Piyasa Fiyatı (model + hafıza, güncel)
             × Yaş Çarpanı
             × Pil Çarpanı
             × Ekran Çarpanı
             × Kasa Çarpanı
             × Kamera Çarpanı
             × Eksik/Ek Paket Çarpanı (kutu, fatura, garanti)
             × IMEI Çarpanı
```

Her çarpan veri tablosundan gelir ve **ağırlıkları bir dosyada** durur. Denge ayarı kod değil veri değişikliği.

### 5.3 Satıcının istediği fiyat
```
İstenen Fiyat = Gerçek Değer × (1 + Şişirme Payı)
```
Şişirme payı: satıcının kişiliğine, aceleliğine ve bilgisine göre belirlenir.
- Bilgisiz satıcı: gerçek değeri bilmez → bazen **ucuz** ister (fırsat) bazen aşırı pahalı.
- Bilgili satıcı: değere yakın ister.
- Aceleci: düşük ister, pazarlıkta kolay.

### 5.4 Dükkân (Telefoncu) özellikleri

| Özellik | Açıklama |
|---------|----------|
| Raf/Vitrin kapasitesi | Aynı anda sergilenebilecek ürün sayısı (başta 6) |
| Depo kapasitesi | Stok sınırı (başta 10) |
| Günlük gider | Kira + elektrik (+ maaşlar) |
| İtibar | Doğru fiyat/dürüst satış itibar artırır → daha fazla ve daha iyi müşteri |
| Ekipman | Ekspertiz cihazı, tamir tezgâhı |

### 5.5 Müşteri akışı
- Her gün, itibar + vitrin kalitesi + konuma bağlı **müşteri sayısı** gelir.
- Müşteri bir ürünle ilgilenir; ürün tipine ve bütçeye göre NPC kişiliği belirlenir.
- Etiket fiyatı çok yüksekse müşteri gelmez/gitmez; çok düşükse hızlı satılır ama kâr kaybı.
- Ürün vitrindeki süre uzadıkça **bekleme maliyeti** (değer kaybı + fırsat maliyeti).

### 5.6 Tamir / parça değişimi
- Ekran/pil değişimi: maliyet + süre (1 gün), sonuç değeri artırır. **Kâr marjı garanti değil**: parça maliyeti > değer artışı olabilir.
- Yan sanayi parça ucuz ama değeri düşük tutar. Karar oyuncunun.
- Teknisyen çalışan gelince süre ve maliyet düşer.

### 5.7 Piyasa etkileri (telefona özgü)
Yeni model çıkışı → aynı markanın 1–2 önceki neslinin fiyatı düşer (haber öncesi söylenti verilir). Sezonluk talep (okul, tatil).

---

## 6. Ekspertiz sisteminin tasarımı

### 6.1 Temel fikir
Ekspertiz = **gizli bilgiyi belirli bir doğrulukla açığa çıkarma**. Yüzde yüz doğru olmak zorunda değil (gelişmiş ekipman/çalışan doğruluğu artırır).

### 6.2 Kademeli ekspertiz (mobilde hızlı olsun diye)

| Kademe | Ad | Maliyet | Ne açar | Doğruluk |
|--------|----|---------|---------|----------|
| 0 | Göz muayenesi | Ücretsiz | Görünen bilgi + kaba ipuçları ("ekran hafif renk farkı var") | Düşük (yorum yeteneği) |
| 1 | Temel kontrol | Düşük | Pil aralığı, ekran orijinal mi (muhtemel), kutu/fatura | Orta |
| 2 | Cihazla test | Orta | Pil % (±3), kamera, parça değişimi listesi | Yüksek |
| 3 | Tam ekspertiz (usta/ekipman) | Yüksek | Tüm alanlar, IMEI durumu | Çok yüksek |

### 6.3 Doğruluk mantığı (sade anlatım)
```
Gözlenen Değer = Gerçek Değer ± Hata
Hata büyüklüğü = f(Kademe, Ekipman seviyesi, Çalışan doğruluğu, Oyuncu ustalığı)
```
- Kademe 3 + iyi çalışan: hata çok küçük.
- Kademe 1: sayı yerine **aralık** görünür ("Pil %70–85").
Böylece oyuncu belirsizliği de görür; "kesin sonuç" yerine "güven aralığı" verilir.

### 6.4 Neden ilginç?
- Ekspertiz her zaman **maliyet karşılığıdır**; ucuz telefonda pahalı ekspertiz mantıksız, pahalı ürün için şart.
- Oyuncu **sadece bazı alanları** seçerek ekspertiz yaptırabilir (örn. yalnız pil ve ekran). Her alanın ücreti ayrı, arayüzde "işaretle ve yaptır".
- Satıcı ekspertize itiraz edebilir (şüpheli NPC): "Cihazı vermem" → bilgi açığa çıkmaz. Ekspertiz bir **sosyal risk** da olabilir.

### 6.5 Ekspertiz sonrası çıktı ekranı
Basit kart: alan, sonuç, güven (renk/nokta), **Tahmini Değer Aralığı** ve "Bu fiyata alırsan ortalama kâr ≈ X TL" ipucu (ipucu seviyesi çalışan/ekipmana bağlı).

### 6.6 Kaçınılacaklar
- Zorunlu, uzun bekleme yok.
- Mini oyun ilk sürümde yok (sonradan opsiyonel: "ekranı çevir/çiz" gibi).

---

## 7. Pazarlık sisteminin tasarımı

### 7.1 Tasarım hedefi
Mobil için **3 dokunuşta bir tur**, ama arkasında akıllı bir model. Sonuç: oyuncu "ustalık" hisseder, ezber tek çözüm olmaz.

### 7.2 Temel model (her iki yön için aynı motor)

Her NPC'nin gizli iki fiyatı var:
- **Ret Fiyatı (Reservation):** Bu fiyattan kötüsünü asla kabul etmez (alışta en yüksek, satışta en düşük).
- **Hedef Fiyat:** Memnun olacağı fiyat.

Ve iki duygu değişkeni:
- **Sabır (Patience):** Her turda azalır.
- **Güven/Memnuniyet (Mood):** Makul tekliflerde artar, saçma tekliflerde düşer.

Pazarlık sırası:
```
Oyuncu teklif verir → NPC değerlendirir:
  Kabul | Karşı teklif | Ret (sabır azalır) | Masayı terk (sabır bitti)
```

### 7.3 Oyuncunun seçenekleri (her turda)
| Eylem | Etki |
|-------|------|
| **Teklif ver** (slider: düşük ↔ makul) | Temel eylem |
| **Argüman kullan** ("Ekran değişmiş", "Kutu yok") | Doğruysa NPC'nin ret fiyatını düşürür; yanlışsa güven düşer |
| **Nazik ol / Sert ol** (ton) | Kişiliğe göre etki değişir |
| **Son teklif** ("Bu son") | Riskli: kabul ya da masayı terk |
| **Vazgeç** | Çık |

> **Not:** "Argüman" özelliği ekspertizle bağlantı kurar: ekspertizle öğrendiğin gerçekler pazarlıkta koz olur. Bu, iki sistemi doğal olarak bağlar.

### 7.4 NPC kişilikleri (sayısal profiller)

| Kişilik | Sabır | Bilgi | Esneklik | Not |
|---------|-------|-------|----------|-----|
| Pazarlıkçı | Yüksek | Orta | Düşük | Çok tur, ilk teklife çok yüksek çıkar |
| Aceleci | Düşük | Orta | Yüksek | Hızlı kapatır, düşük fiyata razı, sıkıştırırsan kaçar |
| Bilgili | Orta | Yüksek | Orta | Yanlış argümanı yemez, gerçek değere yakın |
| Bilgisiz | Orta | Düşük | Değişken | Değeri yanlış bilir; fırsat ve tuzak |
| Zengin | Orta | Değişken | Fiyata az duyarlı | Kalite/itibara bakar, indirim istemez |
| Sabırsız | Çok düşük | Orta | Orta | 1–2 tur, uzatırsa gider |
| Şüpheli | Orta | Orta | Düşük | Ekspertizi/testi sevmez; güven düşünce terk |
| Koleksiyoncu | Yüksek | Yüksek (nadir) | Düşük | Nadir ürüne premium öder, sıradana ilgisiz |

Her kişilik veride birkaç sayıdır: `sabır`, `bilgi`, `esneklik`, `fiyat duyarlılığı`, `şüphe`, `zenginlik`. Yeni kişilik eklemek = yeni veri kaydı.

### 7.5 Oyuncunun NPC'yi "okuması"
- NPC ilk cümlesi ve davranışı ipucu verir ("Acelem var" = Aceleci).
- **Kişilik başta tam görünmez;** ipuçlarından tahmin edilir (deneyim/itibar/çalışan bunu ortaya çıkarabilir).

### 7.6 Basit tut kuralları
- Tur sayısı üst sınır: 5–6.
- Sayıları gizlemek yerine **göstergeler** (sabır barı, ısı göstergesi) göster.
- Otomatik "makul teklif" butonu (yeni oyuncu için).

### 7.7 Satışta pazarlık (yön tersi)
Motor aynı; alıcı NPC ret fiyatını (alışta en yüksek ödeyeceği) kullanır. Etiket fiyatı ilk çekimi belirler.

---

## 8. 2. El pazarının tasarımı

### 8.1 İlan (Listing) yapısı

| Alan | Açıklama |
|------|----------|
| Satıcı (NPC) | Kişilik, aciliyet, bilgi seviyesi |
| Ürün örneği | Sektöre özel (telefon, araba, takı, ev) |
| İstenen fiyat | Satıcı belirler |
| Açıklama | Bazen abartılı/yanlış |
| İlan süresi | Kalan gün; süre bitince ilan kalkar ya da fiyat düşürür |
| Gizli bilgiler | Ekspertizle açılır |
| Pazarlık payı | Satıcının ret fiyatı ile istenen arasındaki fark (gizli) |
| Etiketler | Acil satış, Nadir, Fırsat, Şüpheli gibi (bazı doğru, bazı yanıltıcı) |

### 8.2 İlan üretimi (nasıl "hep yeni ürün" görülür?)

```
Her gün, "Üretici" şu girdilerle ilan sayısı/tipini belirler:
- Oyuncunun açtığı sektörler
- Piyasa durumu (arz-talep)
- Aktif olaylar (acil satış dalgası vb.)
- Oyuncu seviyesi (daha yüksek kademe ilan)
- Seed'li rastgelelik
```
- Üretici tanımlara bakar, örnekleri **rastgele ama kurallı** üretir (ör. 3 yıllık modelde pil %60–90 dağılımı).
- **Adalet için:** Her gün en az 1 karlı fırsat, en az 1 tuzak garantisi (ayarlanabilir).
- Ilan sayısı sınırlı (ekran sayfası başına 8–12) → kaydırma yorgunluğu yok.

### 8.3 Pazar çeşitleri
| Alan | Sektör | Açılış |
|------|--------|--------|
| Mahalle pazarı | Telefon | Başta |
| Sahibinden tarzı ilanlar | Telefon/Araba/Ev | Seviyeye göre |
| Acil satış panosu | Hepsi | Gün 13 |
| Nadir ürün ilanı | Hepsi | Gün 11 |
| Toptan (Toplu ilan) | Telefon/Altın | Sonra |

### 8.4 Ürün akışı
- Alınan ürünler → Envanter (Instance korunur).
- Ürünü **satışa koyma** iki yolla: vitrin (müşteri gelir) veya pazara ilan (uzun sürer, potansiyel yüksek).

### 8.5 Filtre/arama
Mobilde basit: kategori sekmesi, fiyat sıralaması, "Fırsatlar" filtresi. Fazla filtre yok.

---

## 9. Galeri, kuyumcu ve emlak sistemlerinin birbirine bağlanması

### 9.1 Ortak iskelet (en önemli tasarım fikri)

Tüm sektörler **aynı arayüzü** uygular (sade dil: "aynı sözleşme"):

```
Sektör = {
  Ürün örneği üretme kuralı,
  Gizli alanlar listesi,
  Ekspertiz kademeleri ve ölçtüğü alanlar,
  Değer hesaplama kuralı,
  Piyasa endeksi (fiyat bağımlılığı),
  Satış kanalları (vitrin, ilan, kiralama)
}
```
Pazar, Ekspertiz, Pazarlık, Envanter ve Ekonomi sistemleri **sektörün ne olduğunu bilmeden** çalışır. Yeni sektör eklemek = yeni "Sektör paketi" yazmak.

### 9.2 Sektörlerin bağlantı noktaları

| Bağ | Nasıl çalışır |
|-----|---------------|
| **Ortak sermaye** | Tek nakit havuzu; işletmeler arası para aktarımı vardır |
| **Altın ↔ Telefon/Araba** | Altın fiyatı güçlü düşükse "takas" NPC'leri altınla ödeme teklif eder |
| **Galeri ↔ Kuyumcu** | Araba satarken müşteri altınla öder / takı alım-satım geliri araç alımına sermaye olur |
| **Emlak ↔ Galeri** | Emlak ofisinin gösterdiği bölge/gelir seviyesi, o bölgenin araç talebini ve fiyatını etkiler |
| **Emlak ↔ Kuyumcu** | Bölge zenginliği takı talebini ve NPC bütçesini etkiler |
| **Emlak ↔ Tüm işletmeler** | Kiracı olarak dükkân mülkünüz varsa **kira gideri düşer**, mülk piyasası kâr getirir |
| **İtibar (ortak)** | Tek "Esnaf İtibarı" + sektör alt itibarları. Sektör 2'nin başlangıç müşterileri sektör 1 itibarına göre |
| **Bölge sistemi** | Tüm sektörlerin ortak "bölge" verisi: gelişmişlik, nüfus, gelir, talep. Emlak bu değeri değiştirir |
| **Çapraz görev** | "Altın sat, aracın peşinatını öde" gibi zincir görevler |
| **Ortak çalışan havuzu** | Genel roller (muhasebeci, satış) sektörler arası, uzmanlar (kuyumcu ustası, ekspertizci) sektör bazlı |

### 9.3 Sektör kilidi açma
| Sektör | Önerilen kilit | Neden |
|--------|----------------|-------|
| Telefoncu | Başta | Ucuz, hızlı, öğretici |
| Kuyumcu | Lv.10 + belirli servet | Deterministik, düşük karmaşıklık, iyi 2. sektör |
| Oto Galeri | Lv.15 + daha yüksek servet | Yüksek sermaye ve gizli alanlar (hasar, tramer) |
| Emlak | Lv.20+ (servet + borç imkânı) | Yüksek sermaye, uzun süreli, kira geliri |

> **Öneri / değişiklik:** Senin sıralaman "Telefoncu, Galeri, Kuyumcu, Emlak". **Kuyumcuyu galeriden önce** öneririm: telefonculuktan sonra ölçek sıçraması daha yumuşak, kural seti daha küçük. Gün 29'daki seçim bunu oyuncuya bırakır (galeri erken açılabilir ama yüksek sermaye gerektirir).

### 9.4 Emlak özel notları
- **Ev satın alma/satma/kiralama:** üç dönem: alış (pazarlık, tapu masrafı), tutma (kira geliri, bakım, vergi), satış.
- Tapu masrafı, harç, komisyon "sürtünme" olarak açıkça gösterilir.
- Kira geliri "günlük/haftalık" pasif gelir. Boş dönem ve kiracı riski var.
- Emlak yatırımı **yavaş ama istikrarlı**; ticaret **hızlı ama riskli** → oyuncu portföy dengesi kurar.

---

## 10. Ekonomi ve fiyat sisteminin tasarımı

### 10.1 Fiyat katmanları (sade)

```
Nihai Piyasa Değeri = Baz Fiyat (tanım) 
                    × Genel Ekonomi Çarpanı
                    × Sektör Endeksi
                    × Model/Ürün Endeksi
                    × Bölge Çarpanı (gerekirse)
                    × Durum Çarpanı (ekspertiz alanları)
```

- **Baz fiyat:** Tanım dosyasında.
- **Ekonomi çarpanı:** Tüm oyunun fiyat ölçeği (enflasyon ayarı).
- **Sektör endeksi:** Sektörün genel seviyesi (arz-talep + olaylar).
- **Ürün endeksi:** Tek modelin özel durumu (yeni model çıktı → düşer).
- **Bölge çarpanı:** Emlak/galeri/kuyumcu için.

### 10.2 Arz-talep (basit ama işe yarar)
Her ürün grubu için iki sayı: **Arz** ve **Talep**.
```
Fiyat Baskısı = (Talep − Arz) / Referans
Günlük endeks değişimi = Fiyat Baskısı × Hassasiyet + Küçük Rastgele Gürültü
```
- Endeks **bir bantta** kalır (örn. baz fiyatın ±40%) ve yavaşça **ortalamaya döner** (mean reversion). Böylece ekonomi çökmez ya da patlamaz.
- Oyuncu etkisi: çok sayıda aynı ürün alırsa **talep** artar → fiyat hafifçe yükselir; çok satarsa arz artar → düşer. Etki küçük ve geçici.

### 10.3 Olay sistemi
Olay = veri kaydı (tanım):

| Alan | Örnek |
|------|-------|
| Ad | Yeni Model Çıktı |
| Tetikleyici | Zamanlanmış / Rastgele / Koşullu |
| Duyuru süresi | Kaç gün önceden haber |
| Etkilenen endeksler | Telefon-Model X: −%10; X-1: −%5 |
| Süre | 7 gün, sonra normale dönüş |
| Etki tavanı | Tek olay etkisi ± %15 (gibi) |
| Kilit koşulları | Seviye, sektör açık mı? |

**Oyuncu kontrolünü korumak için:**
1. Duyuru + süre: hazırlanma imkânı.
2. Etki üst sınırı.
3. Aynı anda en fazla 2–3 büyük olay.
4. Oyuncuya **çıkış yolu** (indirim yap, ürünü takasa koy, stoktan kurtul).

Olay örnekleri (senin listen): yeni telefon çıktı, araç fiyatları yükseldi, altın yükseldi, bölge değer kazandı, büyük müşteri geldi, acil satış ilanı, nadir ürün ortaya çıktı.

### 10.4 Para sistemi
Ayrı takip edilen değerler:

| Değer | Tanım |
|-------|-------|
| Nakit | Eldeki para |
| Stok Değeri | Envanterin **tahmini piyasa** değeri (oyuncunun bildiği tahmin, gerçek değil) |
| İşletme Değeri | Mağaza, ekipman, itibar (formülle) |
| Mülk Değeri | Emlak, araç vb. |
| Borç | Krediler + faiz |
| Toplam Servet | Nakit + Stok + İşletme + Mülk − Borç |
| Kâr | Gün/hafta/toplam, sektör bazlı |

**Önemli:** "Stok değeri"ni iki şekilde tutmak: *Alış maliyeti* ve *tahmini piyasa değeri*. Oyuncu yalnızca ekspertizle bildiği kadarını görür. Gerçek değer gizli kalır.

### 10.5 Defter (Ledger)
Her para hareketi "defter" kaydına düşer: tarih, tutar, tür (alış, satış, gider, maaş, faiz...), ilgili ürün/işletme. Kâr raporları bu defterden hesaplanır. **Hataları izlemek ve test yazmak için altın kural.**

### 10.6 Denge hedefleri (başlangıç önerileri, sonra test edilir)
- Başlangıç: iyi oynayan oyuncu ilk gün ortalama **%5–12 kâr marjı**/ürün, günde 3–5 ürün.
- Kötü karar: −%10 ile −%25 zarar (ama tek hata oyunu bitirmez).
- Bankrupt koruması: nakit ~0 ve stok yoksa "acil kredi" veya "dayı yardımı" (bir defalık) → oyun kilitlenmez.

---

## 11. Save sisteminin tasarımı

### 11.1 İlkeler
1. Kayıt = **sadece Durum verisi** (Instance'lar, sayılar, kimlikler). Tanımlar kayda **kopyalanmaz**; sadece **ID ile referans** edilir.
2. Kayıt biçimi: **JSON** (okunur, hata ayıklaması kolay). Gerekirse sonra sıkıştırma/şifreleme.
3. **Sürüm numarası** (`saveVersion`) + **migrasyon** (eski kaydı yeni formata çevirme). Oyun güncellenince eski kayıtlar bozulmasın.
4. **Atomik yazma:** önce geçici dosyaya yaz → doğrula → yedeğe kopyala → asıl dosyayı değiştir. Yazarken uygulama kapanırsa eski kayıt kalır.
5. Otomatik kayıt: **gün sonu**, sektör kilidi, büyük satın alma, uygulama arka plana atılırken (`OnApplicationPause`).
6. 3 slot (opsiyonel) + 1 otomatik yedek.

### 11.2 Kaydedilecekler

| Grup | İçerik |
|------|--------|
| Meta | Sürüm, oluşturma/son oynama tarihi, oyun günü, RNG seed/durumu |
| Oyuncu | Ad, XP, seviye, itibarlar |
| Para | Nakit, borçlar, defter özeti (tüm defter opsiyonel/kısaltılmış) |
| Envanter | Ürün örnekleri (Instance verileri, tanım ID'leri) |
| İşletmeler | Sahip olunanlar, seviye, ekipman, vitrin/raf durumu |
| Mülkler | Ev/araç/dükkân, kira durumu |
| Çalışanlar | Kimlik, rol, yetenekler, maaş |
| Pazar | Aktif ilanlar (veya sadece seed + gün ile yeniden üretilebilir) |
| Piyasa | Endeks değerleri, aktif olaylar |
| İlerleme | Görevler, başarımlar, açık sektörler, açılan içerik, öğretici durumu |
| Ayarlar | Ses, dil, bildirim (ayrı dosya olabilir) |

### 11.3 Yapı önerisi (sade)
```
SaveData
 ├─ header (sürüm, tarih)
 ├─ player
 ├─ finance
 ├─ inventory[]
 ├─ businesses[]
 ├─ properties[]
 ├─ employees[]
 ├─ market (listings[], indices)
 ├─ progression
 └─ rng
```
Her modül `Capture()` (durumunu ver) ve `Restore()` (durumu yükle) arayüzü sağlar → yeni sistem eklemek kolay, merkezi dosya şişmez.

### 11.4 Sonraki adımlar (ilk sürümde değil)
- Bulut kaydı (Apple/Google), cihaz değişimi.
- Hile önleme (çevrimdışı tek oyunculu için düşük öncelik).

---

## 12. Unity proje klasör yapısı

**Öneri:** Unity LTS sürümü, **2D (URP 2D)** ya da UI ağırlıklı, **Unity UI Toolkit veya uGUI** (başlangıç için uGUI/TextMeshPro daha çok kaynak/örnek).

```
Assets/
├─ _Project/
│   ├─ Scripts/
│   │   ├─ Core/                    (Asmdef: Esnaf.Core)
│   │   │   ├─ Time/
│   │   │   ├─ Economy/
│   │   │   ├─ Inventory/
│   │   │   ├─ Events/
│   │   │   ├─ Random/
│   │   │   ├─ Save/
│   │   │   └─ Utils/
│   │   ├─ Systems/                 (Asmdef: Esnaf.Systems)
│   │   │   ├─ Market/
│   │   │   ├─ Appraisal/
│   │   │   ├─ Negotiation/
│   │   │   ├─ Pricing/
│   │   │   ├─ Progression/
│   │   │   ├─ Business/
│   │   │   └─ Staff/
│   │   ├─ Sectors/                 (Asmdef: Esnaf.Sectors)
│   │   │   ├─ Phone/
│   │   │   ├─ Car/                 (sonra)
│   │   │   ├─ Gold/                (sonra)
│   │   │   └─ Estate/              (sonra)
│   │   ├─ Bootstrap/               (Oyun başlatma, bağlama)
│   │   └─ UI/                      (Asmdef: Esnaf.UI)
│   │       ├─ Screens/
│   │       ├─ Components/
│   │       └─ Presenters/
│   ├─ Data/
│   │   ├─ Definitions/             (ScriptableObject / JSON)
│   │   │   ├─ Phones/
│   │   │   ├─ NpcArchetypes/
│   │   │   ├─ Events/
│   │   │   ├─ Quests/
│   │   │   ├─ Achievements/
│   │   │   └─ Businesses/
│   │   ├─ Balance/                 (fiyat/denge tabloları)
│   │   └─ Localization/            (TR öncelikli, EN sonra)
│   ├─ Art/       (UI sprite'ları, ikonlar, fontlar)
│   ├─ Audio/
│   ├─ Prefabs/
│   ├─ Scenes/    (Boot, Main)
│   └─ Settings/  (URP, Input, Quality)
├─ Tests/
│   ├─ EditMode/  (saf mantık testleri)
│   └─ PlayMode/  (UI akış testleri, az)
└─ ThirdParty/
Packages/ ProjectSettings/
docs/  (GDD, karar kayıtları)
```

Kurallar:
- **Assembly Definition** ile bağımlılık yönü zorlanır (Core hiçbir şeyi görmez).
- Kural/mantık sınıfları `MonoBehaviour` olmasın (düz C# sınıfı) → test edilir.
- UI'da "Presenter" deseni: Ekran sadece görüntüler, kararları sistem verir.
- Dil metinleri koddan çıkar (Localization tablosu); baştan Türkçe ama sabit metin gömme.
- İsimlendirme: sınıflar `PascalCase`, özel alanlar `_camelCase`, dosya = sınıf adı.

> **Not (sade dil):** `MonoBehaviour` Unity'nin sahnedeki nesnelere bağlanan bileşeni. Kural mantığını buradan **uzak** tutarsak Unity'yi açmadan test edebiliriz.

---

## 13. Hangi sistemleri önce geliştirmeliyiz?

### 13.1 Aşamalar (her aşama sonunda **oynanabilir** bir şey olsun)

| Aşama | Süre (tahmini, tek geliştirici) | İçerik | Bitince oynanabilir mi? |
|-------|-------------------------------|--------|------------------------|
| **0. Hazırlık** | 1 hafta | Unity projesi, klasörler, Asmdef, Git, test altyapısı | Boş sahne |
| **1. Çekirdek** | 2 hafta | Zaman (gün), para+defter, envanter, event bus, RNG | Konsol/log ile "gün ilerler, para değişir" |
| **2. Telefon + Değer** | 1–2 hafta | Telefon tanımı/örneği, gerçek değer formülü, ilan üretici (basit) | İlan listesi görülür |
| **3. Pazarlık (basit)** | 2 hafta | Ret fiyatı, sabır, 3 eylem, 3 kişilik | Alış-satış yapılır |
| **4. Ekspertiz (kademe 0–2)** | 1–2 hafta | Gizli alanlar, hata aralığı, ücret | Çekirdek döngü tamam |
| **5. Dükkân ve satış** | 1–2 hafta | Vitrin, müşteri akışı, gün sonu, gider | **İlk oynanabilir prototip (MVP çekirdeği)** |
| **6. Kayıt/Yükleme** | 1 hafta | Save/Load, otomatik kayıt, sürümleme | Oyun kalıcı |
| **7. UI cilası + Tutorial (30 dk)** | 2 hafta | Ekranlar, ilk 30 dk akış | **Prototip test edilebilir (arkadaşlara ver)** |
| **8. İlerleme** | 2 hafta | XP/seviye, görev, başarım, 30 gün iskeleti | Uzun soluklu oyun |
| **9. Piyasa + Olaylar** | 2 hafta | Arz-talep, endeksler, olay sistemi, haberler | Dinamik ekonomi |
| **10. Çalışanlar + Ekipman** | 2 hafta | Satış/Ekspertiz çalışanı, ekipman | Büyüme derinliği |
| **11. Sektör #2 (Kuyumcu)** | 3–4 hafta | İlk "eklenti" testi | Mimarinin sınavı |
| **12. Galeri, Emlak** | 4–6 hafta her biri | Bölge sistemi, kira | Tam kapsam |
| **13. Cila, ses, kozmetik, monetization, mağaza hazırlığı** | Sürekli | | |

### 13.2 Neden bu sıra?
1. **Ekonomi çekirdeği ve veri ayrımı** en risklidir; hatalı kurulursa sonradan taşımak çok zordur.
2. **Eğlenceyi erken doğrula:** 5. aşamada oyun eğlenceli değilse, geri kalan sistemlerin (piyasa, sektörler) yapılmasının anlamı yok. Bu, prototipin **kırılma noktası**.
3. **Save sistemi geç değil, erken:** Veri şekli oturduktan hemen sonra (6. aşama). Geç kalırsak her değişiklikte kayıt bozulur.
4. **Kuyumcu ikinci sektör olarak "mimari testidir":** Ortak arayüz gerçekten çalışıyorsa yeni sektör çok az çekirdek değişikliği ister; çalışmıyorsa erken öğreniriz.
5. Grafik ve cila **sona**; prototipte gri kutular ve basit ikonlar yeterli.

### 13.3 Riskler ve önlemler
| Risk | Önlem |
|------|-------|
| Denge (ekonomi) tutmaz | Tüm sayılar veri dosyasında; **simülasyon testi** (oyuncusuz 1000 gün çalıştır, servet grafiğine bak) |
| Kapsam şişmesi | Her aşama sonunda "kes/ertele" toplantısı; MVP'yi koru |
| Pazarlık sıkıcı | Erken (3. aşama) 5 kişiyle test; 3 dokunuş kuralı |
| Mobil performans | Tek sahne, UI havuzlama (pooling), büyük listelerde sanal kaydırma |

---

## 14. Sonraki adımlar (senden istediklerim)

Aşağıdakileri netleştirelim, ardından bu belgenin **v0.2**'sini çıkarırım (ve sadece o zaman kodlamaya geçilir):

1. **Zaman modeli:** Gün bazlı/sıra tabanlı onaylıyor musun? (Öneri: evet)
2. **Prototip kapsamı:** Sadece telefoncu, 2D UI ağırlıklı mı? (Öneri: evet)
3. **Sektör sırası:** Telefoncu → **Kuyumcu** → Galeri → Emlak. Uygun mu?
4. **Marka isimleri:** Kurgusal markalar mı kullanalım? (Öneri: evet)
5. **Hedef platform detayı:** Unity sürümü (LTS), hedef minimum cihaz, dil (TR öncelikli?).
6. **Üzerinde en çok detay istediğin bölüm:** Telefon formülleri mi, pazarlık motoru mu, 30 günlük yol haritası mı?

Sonraki taslakta örnek bir telefon tablosu (10 model), örnek pazarlık senaryosu (adım adım sayılarla) ve ilk 7 günün tam görev listesi olacak.
