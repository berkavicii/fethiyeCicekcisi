---
name: verify
description: FethiyeCicekcisi web uygulamasını çalıştırıp değişiklikleri uçtan uca doğrulama tarifi
---

# FethiyeCicekcisi doğrulama tarifi

## Ön koşul
- Lokal Postgres 5432'de çalışıyor olmalı (`Test-NetConnection 127.0.0.1 -Port 5432`).
  Bağlantı: appsettings.json → `Host=127.0.0.1;Database=fethiyecicekcisi;Username=postgres`.
- DB seed'li gelir (DbSeeder); ~10 ürün, "tisort" kategori slug'ı mevcut.

## Derle + çalıştır
```powershell
dotnet build src/FethiyeCicekcisi.Web/FethiyeCicekcisi.Web.csproj --nologo -v q
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:ASPNETCORE_URLS='http://localhost:5237'
dotnet run --project src/FethiyeCicekcisi.Web/FethiyeCicekcisi.Web.csproj --no-build --no-launch-profile
```
Arka planda başlat; ~5-10 sn içinde http://localhost:5237 yanıt verir.

## Sürülecek akışlar
- Ürün listesi: `/urunler` (sayfada "N parça" toplam sayıyı verir — filtre testlerinde bunu karşılaştır)
- Filtreler query string ile: `?kategori=1`, `?ara=...`, `?sirala=price_asc`, `?beden=L`
- Kategori slug rotası: `/urunler/kategori/tisort`
- Ürün detay: `/urunler/<slug>` (beden butonları "Size Selection" yorumunun altında)

## Admin paneli sürme
- Giriş: `admin@fethiyecicekcisi.com` / `Admin@123456` (seed'li). Form `/hesap/giris`, antiforgery ister:
  1. GET `/hesap/giris` (`-SessionVariable s`), `__RequestVerificationToken` değerini regex'le al
  2. POST aynı adrese `Email/Password/RememberMe/__RequestVerificationToken` body'siyle (`-WebSession $s`)
  3. Sonrası `-WebSession $s` ile `/admin/...` sayfaları çekilebilir
- POST eden her admin aksiyonu (ekle/sil) o sayfanın kendi antiforgery token'ını ister.
- Kategoriler: `/admin/kategoriler`, ekle `/admin/kategoriler/ekle`, sil POST `/admin/kategoriler/sil/{id}`

## Dikkat
- `dotnet run`'ı arka plan görevi olarak durdurmak (TaskStop) çocuk `FethiyeCicekcisi.Web.exe`'yi öldürmeyebilir;
  yetim süreç DLL'leri kilitleyip sonraki build'i MSB3026 ile bozar. Durdurduktan sonra kontrol et:
  `Get-NetTCPConnection -LocalPort 5237` → sahibi varsa `Stop-Process -Id <pid> -Force`.
- `Invoke-WebRequest -UseBasicParsing` yeterli; JS gerekmez, sayfalar sunucu taraflı.
- Regex ile tek eşleşme dönerse PowerShell string verir, dizi değil — `@()` ile sar.
