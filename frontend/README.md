# ReGreen Frontend

React, TypeScript, Vite ve MapLibre tabanlı ReGreen kullanıcı arayüzü.

Sorumlu: Zeynep

## Kapsam

- Uzman Paneli
- Harita ve grid görselleştirme
- Topluluk arayüzü
- Kampanya ekranları
- Kullanıcı deneyimi

## Durum

Geliştirme aşamasında.

## Çalıştırma

```bash
npm ci
npm run dev
```

## Doğrulama

```bash
npm run typecheck
npm test
npm run build
```

Servis regresyon testleri gerçek API istek üretimini, ProblemDetails hata dönüşünü,
mock ağırlık doğrulamasını ve güncel 53 yangınlık `ridge_v2` paketini kapsar.

## Veri kaynağı

`.env.example` dosyasını `.env.local` olarak kopyalayın.

- `VITE_SERVICE_MODE=mock`: Backend olmadan temel mock servis
- `VITE_SERVICE_MODE=http`: Gerçek ReGreen API
- `VITE_API_BASE_URL`: API origin'i; yerel backend için `http://localhost:5066`

UI bileşenleri veri kaynağına doğrudan erişmez. Tüm erişim `FireService`
arayüzü üzerinden `MockFireService` veya `HttpFireService` ile yapılır.

## Backend API

Backend ve `ridge_v2` veri paketi hazırdır; API sözleşmesi sabit olduğu için gerçek
API veya mock veri kaynağıyla geliştirme yapılabilir.

- **Sözleşme:** [`docs/api-contract.md`](../docs/api-contract.md) — 3 endpoint, tüm query parametreleri, hata kodları, DTO örnekleri.
- **Kurulum (ilk kez, ~5 dakika):** API'nin verisi (53 yangın/37.163 hücre) repository'yle birlikte GELMEZ — LocalDB kurulu bir Windows makinesinde kendi kopyanı kurman gerekiyor. Adımlar [`backend/README.md`](../backend/README.md#yerel-veritabanını-sıfırdan-kurma)'de ("Yerel veritabanını sıfırdan kurma"): `dotnet ef database update` + `dotnet run --project backend/ImportTool -- sample-data/backend-data/manifest.json`. Bunlardan sonra `dotnet run --project backend/ReGreen.Api` ile API ayağa kalkar, varsayılan `http://localhost:5066` (bkz. `backend/ReGreen.Api/Properties/launchSettings.json`). `/openapi/v1.json`'dan makine-okunur şema da alınabilir (sadece Development ortamında).
- **Veri kararlılığı:** Bir kere kurduktan sonra, Buğra yeni bir model teslimatı yapana kadar aynı veriyle geliştirilebilir. Yeni teslimat geldiğinde SADECE veriler değişecek (yeniden import gerekir), API sözleşmesi (alan adları, endpoint'ler, hata formatı) değişmeyecek.
- **CORS:** `http://localhost:5173` ve `http://localhost:3000` şu an izinli (bkz. `backend/ReGreen.Api/appsettings.Development.json`) — henüz gerçek dev port'un doğrulanmadı, bu iki yaygın Vite/CRA varsayılanı. Farklı bir port kullanıyorsan Beytullah'a haber ver, tek satırlık bir ekleme.
