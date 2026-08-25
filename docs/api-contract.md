# ReGreen — API Sözleşmesi (api-contract.md)

**Sürüm:** 1.0
**Kapsam:** `backend/ReGreen.Api` — PDF v4.1 §9'daki 3 endpoint. Alan anlamları/tipleri için tek doğru kaynak [`docs/data-contract.md`](./data-contract.md)'dir; bu belge SADECE HTTP sözleşmesini (yol, query parametresi, response zarfı, hata kodu) tanımlar, alan anlamlarını tekrar etmez.
**Kural:** Bu dosya tek doğru kaynaktır. Endpoint davranışı değişecekse önce bu dosya güncellenir, sonra kod.

---

## 1. Genel

- Taban yol: `/api`.
- Tüm response gövdeleri JSON, alan adları `snake_case` (data-contract.md ile birebir).
- Kimlik doğrulama YOK (Faz 1/2 kapsamı, iç ağ).
- CORS: izin verilen origin'ler `Cors:AllowedOrigins` config'inden gelir; wildcard hiçbir ortamda kullanılmaz (bkz. §5).

---

## 2. `GET /api/fires`

Tüm yangınları listeler. **TÜM 53 yangın döner** — `check` kalite bayraklılar filtrelenmez, sadece işaretlenir (data-contract §10).

**Query parametreleri**

| Parametre | Zorunlu | Değer | Açıklama |
| --- | --- | --- | --- |
| `quality_flag` | Hayır | `ok` \| `check` | Verilirse sadece o bayraklı yangınlar döner. Başka değer → `400 INVALID_QUERY_PARAMETER`. |

**Response** — `FireSummaryDto[]`:

```json
[
  {
    "fire_id": "AKD_2021_05",
    "fire_date": "2021-07-29",
    "province": "Adana",
    "region": "Akdeniz",
    "modis_area_ha": 3768,
    "burned_area_ha": 8925.0,
    "cell_count": 1428,
    "has_perimeter": true,
    "marker_lat": 37.005,
    "marker_lon": 35.123,
    "quality_flag": "ok",
    "quality_note": null
  }
]
```

`cell_count`, `Cells` tablosundan anlık `COUNT` ile hesaplanır (`Fires` tablosunda ayrı bir kolon yok).

---

## 3. `GET /api/fires/{fireId}/perimeter`

Yangının dış sınırını (harita için) + marker noktasını döner.

**Response** — `FirePerimeterDto`:

```json
{
  "fire_id": "AKD_2021_05",
  "marker": { "lat": 37.005, "lon": 35.123 },
  "perimeter": {
    "type": "Feature",
    "properties": { "fire_id": "AKD_2021_05", "fire_date": "2021-07-29", "province": "Adana", "region": "Akdeniz", "modis_area_ha": 3768 },
    "geometry": { "type": "MultiPolygon", "coordinates": [ ] }
  }
}
```

`perimeter.geometry.type` **`Polygon` veya `MultiPolygon`** olabilir (data-contract §7.2 — 53 dosyanın 48'i MultiPolygon, 5'i Polygon). `marker`, import sırasında `NetTopologySuite.Geometry.InteriorPoint` ile hesaplanmış, DB'den okunur (centroid DEĞİL).

**Hatalar:** yangın yoksa `404 FIRE_NOT_FOUND`; DB'deki `PerimeterGeoJson` (olmaması gereken şekilde) bozuksa `500 PERIMETER_DATA_CORRUPT`.

---

## 4. `GET /api/fires/{fireId}/cells`

Yangının hücrelerini, model tahminlerini ve öncelik skorlarını döner. **Kare geometri YOK** — kare, hücre merkezinden (`lat`/`lon`) data-contract §7.1 formülüyle frontend'de üretilir.

### 4.1. En güncel ModelRun seçimi

Bir yangının birden fazla teslimatı (ModelRun'ı) olabilir. Kullanılan kural, **deterministik**:

```
ORDER BY GeneratedAt DESC, Id DESC
LIMIT 1
```

`GeneratedAt` eşitse (aynı anda iki farklı `model_version` teslim edilmişse) `Id`'si büyük olan (sonradan eklenen) kazanır. Yangın var ama hiç ModelRun'ı yoksa → `404 MODEL_RUN_NOT_FOUND`.

### 4.2. Ağırlık parametreleri — `recovery`, `erosion`, `access`

**Kural: ya üçü birden, ya hiçbiri.** Biri veya ikisi verilirse `400 INVALID_PRIORITY_WEIGHTS`.

- **Hiçbiri verilmezse:** DB'deki `DefaultPriorityScore`/`DefaultPriorityClass` (dosyadan gelen, import sırasında hesaplanmış) aynen döner — yeniden hesap YOK. `applied_weights`, ModelRun'ın normalize edilmiş varsayılan ağırlığıdır.
- **Üçü de verilirse:** Değerler sonlu, negatif olmayan sayı olmalı ve toplamları sıfırdan büyük olmalı (aksi `400 INVALID_PRIORITY_WEIGHTS`). Backend toplamı 1 olacak şekilde normalize eder, **sadece `prediction_status == "predicted"`** hücreler için `priority_score`/`priority_class`'ı anlık yeniden hesaplar (`normalization_reference` — ModelRun'dan gelen min/max — SABİT kalır, sadece ağırlık değişir). `low_severity` (`0.0`/`DUSUK`) ve `no_data` (`null`/`null`) hücreler **DEĞİŞMEZ**. Ağırlıklar DB'ye asla yazılmaz.

```
GET /api/fires/AKD_2021_05/cells                                  → varsayılan ağırlık, DB skorları
GET /api/fires/AKD_2021_05/cells?recovery=0.8&erosion=0.1&access=0.1  → anlık yeniden hesap
GET /api/fires/AKD_2021_05/cells?recovery=0.8                      → 400 INVALID_PRIORITY_WEIGHTS
```

### 4.3. Bounding box — `min_lon`, `min_lat`, `max_lon`, `max_lat`

**Kural: ya dördü birden, ya hiçbiri.** Biri/ikisi/üçü verilirse `400 INVALID_BOUNDING_BOX`.

Verilirse: `-180 ≤ min_lon < max_lon ≤ 180` ve `-90 ≤ min_lat < max_lat ≤ 90` şartı aranır (aksi yine `400 INVALID_BOUNDING_BOX`). Hücrenin `lat`/`lon` MERKEZİ bu kutunun içindeyse (`>=`/`<=`, sınırlar dahil) döner. Filtre SQL sorgusunun içinde uygulanır.

### 4.4. Diğer filtreler

| Parametre | Değer | Ne zaman uygulanır |
| --- | --- | --- |
| `prediction_status` | `predicted` \| `low_severity` \| `no_data` | SQL sorgusunda |
| `priority_class` | `COK_YUKSEK` \| `YUKSEK` \| `ORTA` \| `DUSUK` | Bellekte, özel ağırlıkla yeniden hesaplama SONRASI (çünkü nihai sınıf isteğe bağlı olarak anlık hesaba bağlı) |

Geçersiz değer → `400 INVALID_QUERY_PARAMETER`.

### 4.5. Response — `CellsResponseDto`

```json
{
  "fire_id": "AKD_2021_05",
  "model_run_id": 42,
  "model_version": "ridge_v2",
  "generated_at": "2026-08-23T16:13:19+00:00",
  "crs": "EPSG:4326",
  "cell_size_m": 250,
  "applied_weights": { "recovery": 0.5, "erosion": 0.3, "access": 0.2 },
  "normalization_reference": {
    "recovery_gap_pred": { "min": 0.1228, "max": 0.4554 },
    "slope_deg": { "min": 0.5991507, "max": 16.264103 },
    "road_distance_km": { "min": 0.02827684, "max": 1.9335308 }
  },
  "priority_thresholds": { "COK_YUKSEK": 0.75, "YUKSEK": 0.5, "ORTA": 0.25 },
  "count": 1428,
  "items": [
    {
      "cell_id": "AKD_2021_05_000160",
      "lat": 37.41129,
      "lon": 35.58044,
      "tree_cover": 0.0,
      "tree_cover_annual": 0.0,
      "burn_severity_dnbr": 0.3068124,
      "slope_deg": 5.2742534,
      "elevation_m": 264.05188,
      "road_distance_km": 0.6179377,
      "ndvi_before": 0.46812958,
      "ndvi_after": 0.19532657,
      "ndvi_drop": 0.272803,
      "severity_class": "orta-dusuk",
      "land_cover": "Tarim",
      "prediction_status": "predicted",
      "recovery_gap_pred": 0.1917,
      "priority_score": 0.3312,
      "priority_class": "ORTA"
    }
  ]
}
```

- `cell_size_m`, **`Fires.CellSizeM`'den okunur** (bu paket için hep `250`, sabit kodlanmamıştır).
- `crs` sabit `"EPSG:4326"` (DB'de ayrı alan yok, import katmanı garanti eder).
- `model_version` — `ModelRun.ModelVersion` (ör. `"ridge_v2"`). `model_run_id`'nin insan-okunur karşılığı; frontend UI'da hangi model teslimatının aktif olduğunu göstermek için.
- `applied_weights` HER ZAMAN dolu — kullanıcı ağırlığı ya da ModelRun'ın normalize edilmiş varsayılanı.
- `normalization_reference` yangın başına SABİT — `ModelRun`'dan gelir (`NormRecoveryGapMin/Max`, `NormSlopeMin/Max`, `NormRoadDistMin/Max`), ağırlık parametreleri (§4.2) değişse de değişmez. Frontend'in öncelik skorunu 3 bileşene (iyileşme açığı / erozyon / ulaşılabilirlik) ayırarak göstermesi için gerekli. **`min`/`max` HER ZAMAN sayı, asla `null` değil** — `ReGreen.Core.Priority.NormRange` (metadata.json ayrıştırması ve dahili yeniden-hesaplama için) bilerek nullable, ama import zamanında (`FireValidator` §3.3.4) null gelen bir `normalization_reference` reddedilir, DB'ye hiç ulaşmaz. Bu alan API'ye özel non-nullable `NormalizationReferenceDto` ile döner.
- `priority_thresholds` yangın başına SABİT — `ModelRun.ThresholdVeryHigh/High/Medium`'dan gelir (§6.3 ile aynı `COK_YUKSEK`/`YUKSEK`/`ORTA` anahtarları). Frontend `priority_class` renklendirmesini API üzerinden yapıyorsa `{fire_id}_metadata.json`'a ayrıca erişmesine gerek kalmaz.
- `count = items.Length`. Bu sürümde pagination YOK. İleride eklenirse `count` anlamı değişmez, ayrı bir `total_count` alanı eklenir.

---

## 5. Hata sözleşmesi (RFC 7807 `ProblemDetails`)

Tüm hatalar aynı gövde şeklinde döner, `code` alanı programatik ayrım için:

```json
{
  "type": "https://regreen/errors/invalid-priority-weights",
  "title": "Geçersiz öncelik ağırlıkları",
  "status": 400,
  "code": "INVALID_PRIORITY_WEIGHTS",
  "detail": "recovery, erosion ve access birlikte verilmelidir veya hiçbiri verilmemelidir."
}
```

| `code` | HTTP | Ne zaman |
| --- | --- | --- |
| `FIRE_NOT_FOUND` | 404 | `{fireId}` hiçbir yangına ait değil |
| `MODEL_RUN_NOT_FOUND` | 404 | Yangın var, ama hiç ModelRun'ı yok (henüz import edilmemiş) |
| `INVALID_PRIORITY_WEIGHTS` | 400 | 1/2 ağırlık verilmiş, ya da değerler negatif/NaN/Infinity/toplam ≤ 0 |
| `INVALID_BOUNDING_BOX` | 400 | 1/2/3 bbox parametresi verilmiş, ya da değerler NaN/Infinity, `min >= max`, ya da EPSG:4326 sınırı dışı |
| `INVALID_QUERY_PARAMETER` | 400 | Geçersiz `quality_flag` / `prediction_status` / `priority_class`, ya da sayısal olması gereken bir parametreye (`recovery`/`erosion`/`access`/`min_lon`/`min_lat`/`max_lon`/`max_lat`) sayısal olmayan bir değer verilmiş (ör. `?recovery=abc`) |
| `PERIMETER_DATA_CORRUPT` | 500 | DB'deki `PerimeterGeoJson` parse edilemedi (olmaması gereken durum, savunma amaçlı) |
| `DB_UNAVAILABLE` | 503 | Veritabanına bağlanılamadı / bağlantı sorgu sırasında koptu |
| `UNEXPECTED_ERROR` | 500 | Yukarıdakilerin hiçbiri değil — gerçekten beklenmeyen bir istisna (genel `AddProblemDetails()` fallback'i). Endpoint'in kendi ürettiği bir `code` varsa bu ASLA onun üzerine yazmaz. |

---

## 6. Sağlık kontrolü (health check)

Auth/veri sözleşmesi dışında, ops/monitoring için:

| Endpoint | Ne kontrol eder | Başarılı | Başarısız |
| --- | --- | --- | --- |
| `GET /health/live` | Süreç ayakta mı (DB'ye HİÇ dokunmaz) | `200 { "status": "healthy" }` | — (süreç çökmüşse zaten yanıt vermez) |
| `GET /health/ready` | Süreç ayakta VE DB'ye bağlanabiliyor mu | `200 { "status": "healthy" }` | `503 { "status": "unhealthy" }` |

`/live` "restart gerekir mi" sorusuna, `/ready` "trafik alabilir mi" sorusuna cevap verir — DB geçici olarak kesildiğinde `/live` yine `200` döner (uygulama sağlıklı, sadece DB'ye erişemiyor), `/ready` `503` döner.

---

## 7. Performans ve sıkıştırma

`/cells`, en büyük yangında (`AKD_2021_01`, 9048 hücre) sıkıştırılmamış ~4 MB JSON döner. Response compression (Brotli/Gzip, `CompressionLevel.Fastest`) açık — auth/gizli veri olmadığı için BREACH/CRIME riski yok, HTTPS için de güvenle etkin.

Sayı **iddia değil, ölçüm**: `backend/scripts/perf-check.ps1` — kendi makinende `dotnet run --project backend/ReGreen.Api` çalışırken çalıştırıp tekrar üretebilirsin (curl.exe kullanır, Windows 10 1803+/11'de hazır gelir — Windows PowerShell 5.1'in `Invoke-WebRequest`/`HttpClient`'i büyük gövdelerde onlarca kat yavaş/yanıltıcı ölçüm verdiği için BİLEREK kullanılmaz, script'in başındaki not'ta ölçülerek belgelendi).

Bu ortamda ölçülen (10 istek ortalaması, 3 ısınma isteği sonrası, `dotnet run --configuration Release`):

| Senaryo | Gecikme (ort/min/max) | Sıkıştırılmamış | Sıkıştırılmış | Kazanç |
| --- | --- | --- | --- | --- |
| `AKD_2021_01/cells` (tam, 9048 hücre) | 66 / 57 / 80 ms | 4.028.958 bayt | 1.044.330 bayt | %74 |
| Aynı + bounding box (1495 hücre) | 22 / 19 / 27 ms | 665.360 bayt | 175.332 bayt | %74 |

Sıkıştırma yanıt boyutunu küçültür ama frontend'in **9048 hücreyi işleme maliyetini ortadan kaldırmaz** — harita viewport'una göre bounding box kullanmak (yukarıdaki ikinci satır) hâlâ önerilir, ikisi birbirini tamamlıyor.

---

## 8. Bağlantı ve ortam

- Bağlantı dizesi önceliği: `REGREEN_CONNECTION_STRING` env var → `appsettings.json`'daki `ConnectionStrings:Default` → localdb geliştirme fallback'i (ImportTool ile aynı konvansiyon).
- Migration API başlangıcında OTOMATİK çalıştırılmaz — şema `dotnet ef database update --project backend/ReGreen.Data` ile elle uygulanır.
- CORS origin'leri `Cors:AllowedOrigins` (appsettings) dizisinden gelir; geliştirmede `appsettings.Development.json`'da açıkça listelenir (frontend'in gerçek dev port'una göre güncellenmeli, şu an `http://localhost:5173`/`http://localhost:3000` yer tutucu olarak yazılı).
- OpenAPI: `/openapi/v1.json` (sadece `Development` ortamında).

---

## 9. Referans

- [`docs/data-contract.md`](./data-contract.md) — alan adı/tip/anlam (tek doğru kaynak).
- [`docs/db-schema.md`](./db-schema.md) — DB şeması, CHECK/composite FK kısıtları.
- [`docs/import-flow.md`](./import-flow.md) — import pipeline kuralları.
