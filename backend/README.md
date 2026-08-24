# Backend

ReGreen'in yangın analizi için veri tabanı, AI teslim paketi import aracı ve
salt-okunur HTTP API'si bu klasörde bulunur.

Sorumlu: Beytullah

## Bileşenler

- `ReGreen.Data` — EF Core veri modeli, `AppDbContext` ve ilk migration
- `ReGreen.Core` — AI referans uygulamasıyla uyumlu öncelik hesaplama kuralları
- `ImportTool` — AI teslim paketini doğrulayan ve SQL Server'a aktaran CLI
- `ReGreen.Api` — yangın listesi, perimeter/marker ve hücre endpoint'leri
- `ImportTool.Tests` / `ReGreen.Api.Tests` — unit ve LocalDB entegrasyon testleri
- `db/schema.sql` — EF Core şemasının çalıştırılabilir SQL karşılığı

## Mevcut Durum

Faz 1/2 çekirdek backend tamamlandı ve test edildi:

- Güncel sample data paketi: 53 yangın, 37.163 hücre
- Import akışı: dry-run, doğrulama, yangın bazlı transaction, idempotency ve JSON rapor
- API: 3 GET endpoint'i, filtreleme, bounding-box ve isteğe bağlı ağırlıklarla anlık öncelik hesabı
- Testler: 113 toplam (63 API + 50 ImportTool), gerçek LocalDB entegrasyon testleri dahil
- Release build: sıfır uyarı ve sıfır hata

HTTP sözleşmesi için [`../docs/api-contract.md`](../docs/api-contract.md), veri
alanları için [`../docs/data-contract.md`](../docs/data-contract.md) esas alınır.

## Yerelde Çalıştırma

Repository kökünden:

```powershell
dotnet build backend/ReGreen.sln --configuration Release
dotnet run --project backend/ReGreen.Api
```

API geliştirme ortamında varsayılan olarak `http://localhost:5066` adresinde
çalışır. OpenAPI belgesi `/openapi/v1.json` yolundadır.

Import paketini DB'ye yazmadan doğrulamak için:

```powershell
dotnet run --project backend/ImportTool -- sample-data/backend-data/manifest.json --dry-run
```

LocalDB entegrasyon testleri opt-in çalışır:

```powershell
$env:REGREEN_RUN_LOCALDB_TESTS='1'
dotnet test backend/ReGreen.sln
```

## Mevcut Kapsamın Dışında

- Kampanya, topluluk, gönüllü ve saha gözlemi modülleri (Faz 3)
- Kimlik doğrulama ve yetkilendirme
- Cloud deployment ve otomatik import tetikleme
- Hücre endpoint'i için pagination
