# Frontend

ReGreen kullanıcı arayüzleri ve harita çalışmaları bu klasörde yürütülür.

Sorumlu: Zeynep

## Kapsam

- Uzman Paneli
- Harita ve grid görselleştirme
- Topluluk arayüzü
- Kampanya ekranları
- Kullanıcı deneyimi

## Durum

Geliştirme aşamasında.

## Backend API

Backend hazır ve çalışıyor — model/veri tarafı henüz kesinleşmemiş olsa da API sözleşmesi sabit, geliştirmeye şimdiden başlanabilir.

- **Sözleşme:** [`docs/api-contract.md`](../docs/api-contract.md) — 3 endpoint, tüm query parametreleri, hata kodları, DTO örnekleri.
- **Yerel çalıştırma:** `dotnet run --project backend/ReGreen.Api` → varsayılan `http://localhost:5066` (bkz. `backend/ReGreen.Api/Properties/launchSettings.json`). `/openapi/v1.json`'dan makine-okunur şema da alınabilir (sadece Development ortamında).
- **Test verisi:** Yerel LocalDB'de (`ReGreen` veritabanı) gerçek 53 yangın/37.163 hücrelik veri hazır ve dolu duruyor — Buğra yeni bir model teslimatı yapana kadar bu veriyle geliştirilebilir. Yeni teslimat geldiğinde SADECE veriler değişecek, API sözleşmesi (alan adları, endpoint'ler, hata formatı) değişmeyecek.
- **CORS:** `http://localhost:5173` ve `http://localhost:3000` şu an izinli (bkz. `backend/ReGreen.Api/appsettings.Development.json`). Farklı bir port kullanıyorsan Beytullah'a haber ver, tek satırlık bir ekleme.
