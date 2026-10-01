# ESNAF – Windows kurulum notu (D:\ESNAF)

Bu repodaki kod ve testler bulut ortamında yazıldı ve **.NET ile çalıştırıldı**. **Unity projesi (ProjectSettings, Packages, sahneler) bulutta oluşturulamadı**, çünkü orada Unity Editor yok ve Unity paket sunucusuna erişim engelli. Unity projesini senin bilgisayarında oluşturup bu repoyla birleştireceksin.

## 1. Ön koşullar (kontrol et)

| Bileşen | Kontrol |
|---------|---------|
| Git | `git --version` |
| Unity Hub + **Unity 6.3 LTS** (6000.3.x) | Unity Hub → Installs. Yoksa Install Editor → 6.3 LTS |
| Android Build Support (+ **OpenJDK, Android SDK & NDK Tools** kutuları) | Unity Hub → Installs → sürüm → Add modules |
| **.NET SDK 8** (testler için) | `dotnet --version` (8.x olmalı) |
| D: sürücüsünde boş alan | Unity + Android modülleri için en az ~25 GB önerilir |

## 2. Unity projesini oluştur ve repoyla birleştir

1. Unity Hub → **New project** → **2D (URP)** şablonu → Ad: `ESNAF` → Konum: `D:\` (Hub `D:\ESNAF` klasörünü oluşturur). Projeyi bir kez açıp kapat.
2. PowerShell:
   ```powershell
   cd D:\ESNAF
   git init
   git remote add origin <repo-url>
   git fetch origin claude/esnaf-mobile-game-k5h94d
   git checkout -B claude/esnaf-mobile-game-k5h94d origin/claude/esnaf-mobile-game-k5h94d
   ```
   Repoda `Assets/_Project`, `docs`, `tools`, `.gitignore` bulunur. Unity'nin oluşturduğu `Assets/Settings`, `Packages`, `ProjectSettings` çakışmaz.
3. Unity'yi aç. `Assets/_Project` altındaki dosyalar için `.meta` dosyaları oluşur.
4. **Window → Package Manager:**
   - **Unity UI (uGUI)** yüklü olmalı (TextMeshPro içindedir; ilk kullanımda "Import TMP Essentials" çıkarsa kabul et).
   - **Newtonsoft Json:** `+` → *Add package by name* → `com.unity.nuget.newtonsoft-json`
   - **Test Framework** yüklü olmalı.
5. Unity'de konsolda **kırmızı hata olmamalı.** Olursa hata metnini ilet.
6. **Window → General → Test Runner → EditMode → Run All.** Beklenen: `Esnaf.Domain.Tests` altında **tüm testler geçer** (Core: 96, Day 9 sonrası toplam: 2592).
7. Unity'nin oluşturduğu dosyaları commit'le (`.meta`, `Packages/manifest.json`, `ProjectSettings/`).

## 3. Testleri Unity'siz çalıştırma

```powershell
cd D:\ESNAF\tools\dotnet-tests
dotnet test Esnaf.sln
```
Beklenen: `Passed! - Failed: 0, Passed: 2592`. Bu, Unity'deki testlerin **aynı dosyalarını** çalıştırır.
