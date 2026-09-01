# ReGreen — API Sözleşmesi (api-contract.md)

**Sürüm:** 1.1
**Kapsam:** `backend/ReGreen.Api` — PDF v4.1 §9'daki 3 endpoint (§2-§4) + docs/hukum_sozlesmesi.md'deki "hüküm katmanı" 3 endpoint'i (§5-§7, v1.1 ile eklendi). Alan anlamları/tipleri için tek doğru kaynak [`docs/data-contract.md`](./data-contract.md) ve [`docs/hukum_sozlesmesi.md`](./hukum_sozlesmesi.md)'dir; bu belge SADECE HTTP sözleşmesini (yol, query parametresi, response zarfı, hata kodu) tanımlar, alan anlamlarını tekrar etmez.
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

## 5. `GET /api/fires/{fireId}/cells/{cellId}/hukum`

Bir hücrenin **hükmünü** döner. Aktif ağırlıklardan bağımsızdır; ancak model tahminini kullandığı için `/cells` ile aynı en güncel `ModelRun` seçilir.

### 5.1. Seçim kuralı

Sıra: önce yangın var mı, sonra hücre o yangına ait mi, sonra hücrenin hükmü var mı. Üçü de ayrı hata koduna sahiptir (bkz. §8) — `CELL_NOT_FOUND` ile `CELL_VERDICT_NOT_FOUND` KARIŞTIRILMAZ: biri "böyle bir hücre yok", diğeri "hücre var ama henüz hükmü içe aktarılmamış" (ör. yangının `_hukumler.csv` teslimatı henüz yapılmadı).

### 5.2. Response — `CellVerdictDto`

```json
{
  "cell_id": "AKD_2021_01_020514",
  "model_run_id": 53,
  "model_version": "ridge_v2",
  "generated_at": "2026-08-23T16:13:19+00:00",
  "hukum_version": "1.2",
  "hukum": "EROZYON_ONCE",
  "ek_kosullar": ["AGIR_YANMIS"],
  "toparlanma_orani": 0.2727,
  "tur_onerisi": "kızılçam, fıstıkçamı",
  "tetikleyen": "toparlanma=0.27 egim=36.0>=25 <0.35",
  "ozet": "Önce erozyon kontrolü. Toparlanma tahmini zayıf ve eğim 36,0° — dikimden önce toprak tutma önlemi gerekiyor.",
  "ayrinti": "Modelin tahminine göre yangın öncesi örtünün ancak %27 kadarı iki yılda geri gelecek. ...",
  "zamanlama_notu_var": true
}
```

- `hukum` — 7 değerden biri: `KAPSAM_DISI`, `SAHA_KONTROL`, `IZLE`, `EROZYON_ONCE`, `DIKIM_ADAYI`, `ONCELIGE_GORE`, `GENCLESME_IZLE`.
- `ek_kosullar` — 0..6 elemanlı dizi (DB'de `\|` ile ayrılmış tek string olarak saklanır, API'de dizi olarak açılır; boşsa `[]`, asla `null` değil).
- `toparlanma_orani`/`tur_onerisi` — `null` olabilir (data-contract §9.4 ile aynı null kuralı).
- `tur_onerisi` **onaylanmamış örnek veridir** — `GET /api/hukum-sozlugu`'daki `tur_tablosu.onaylandi` `false` olduğu sürece UI bu alanı kaynak notu olmadan göstermemeli (bkz. §7).
- `zamanlama_notu_var` `true` ise sabit `zamanlama_notu` metni `GET /api/hukum-sozlugu`'dan okunur (hücre başına tekrarlanmaz).

### 5.3. Hatalar

| `code` | HTTP | Ne zaman |
| --- | --- | --- |
| `FIRE_NOT_FOUND` | 404 | `{fireId}` hiçbir yangına ait değil |
| `CELL_NOT_FOUND` | 404 | Yangın var, ama `{cellId}` o yangında yok |
| `CELL_VERDICT_NOT_FOUND` | 404 | Hücre var, ama henüz hükmü içe aktarılmamış (bkz. §5.1) |

---

## 6. `GET /api/fires/{fireId}/summary`

Bölge seçilince gösterilecek yangın özetini (Katman 1 anlatı) döner — tek paragraf + paragrafın dayandığı ham sayı bloğu (docs/hukum_sozlesmesi.md "Katman 1 — yangın özeti").

### 6.1. Seçim kuralı

Önce yangın var mı, sonra o yangının anlatı girdisi var mı (`yangin_metinleri.json`/`yangin_ozetleri.json`'da `{fireId}` anahtarı). İkisi ayrı hata kodu (bkz. §8).

### 6.2. Response — `FireNarrativeDto`

```json
{
  "fire_id": "AKD_2021_01",
  "model_run_id": 53,
  "model_version": "ridge_v2",
  "generated_at": "2026-08-23T16:13:19+00:00",
  "narrative_version": "1.0",
  "paragraf": "Antalya, 29 Temmuz 2021. 56.550 hektarlık alan, 9.048 hücre. ...",
  "profil": "karisik",
  "onaylandi": true,
  "uretim": "sablon (deterministik)",
  "sayi_blogu": {
    "fire_id": "AKD_2021_01",
    "il": "Antalya",
    "bolge": "Akdeniz",
    "buyukluk": { "hucre": 9048, "alan_ha": 56550.0, "tahminli_hucre": 4917 },
    "hukum_dagilimi": { "KAPSAM_DISI": 913, "EROZYON_ONCE": 233, "...": "..." }
  }
}
```

- `profil` — 6 değerden biri: `yogun_mudahale`, `karisik`, `kendi_toparlaniyor`, `dik_arazi`, `belirsiz`, `kapsam_dar` — hangi anlatı kalıbının kullanıldığını söyler (bkz. docs/hukum_sozlesmesi.md "Anlatı profili").
- `sayi_blogu` — **opak JSON**, `perimeter`'daki teknikle aynı (`JsonDocument.Parse(...).RootElement.Clone()`) aynen geçirilir; alt alanları bu belge TEK TEK tanımlamaz (data-contract'ın "hücre CSV'si" gibi sabit bir sözleşmesi yok, AI ekibinin `yangin_ozetleri.json` çıktısı neyse odur). Frontend paragraf yerine kendi görselini üretmek isterse bu bloktan yararlanabilir.
- `uretim` — üretim yöntemi (şu an sabit `"sablon (deterministik)"`); ileride bir dil modeliyle yeniden yazılırsa değişebilir, ama çalışma anında API asla dil modeli çağırmaz — metin her zaman DB'den statik okunur.

### 6.3. Hatalar

| `code` | HTTP | Ne zaman |
| --- | --- | --- |
| `FIRE_NOT_FOUND` | 404 | `{fireId}` hiçbir yangına ait değil |
| `FIRE_NARRATIVE_NOT_FOUND` | 404 | Yangın var, ama henüz anlatı özeti içe aktarılmamış |

---

## 7. `GET /api/hukum-sozlugu`

Hüküm katmanının GLOBAL (yangına özgü olmayan) sabit sözlüğünü döner — hüküm/ek koşul başlıkları, eşikler, tür tablosu onay bayrağı. Frontend'in hüküm koduna göre renk/rozet/başlık göstermesi veya `ek_kosullar` şablon cümlelerini doldurması için gereken TEK kaynak; hücre başına tekrar tekrar çekilmez (uygulama açılışında bir kez alınıp önbelleklenmesi ÖNERİLİR).

### 7.1. Seçim kuralı

Opsiyonel `surum` query parametresi verilirse o sürüm, verilmezse `ImportedAt DESC, Surum DESC` sırasındaki en güncel sözlük döner. Frontend, hüküm yanıtındaki `hukum_version` ile aynı sözlüğü istemelidir.

### 7.2. Response — ham JSON passthrough

Response, DB'de saklanan `hukum_sozlugu.json` içeriğinin **aynen** geçirilmiş hâlidir (DTO yok, `JsonElement` olarak `Results.Ok(...)`'a sarılır):

```json
{
  "surum": "1.2",
  "dil": "tr",
  "hukumler": [
    { "kod": "KAPSAM_DISI", "baslik": "Ağaçlandırma kapsamı dışı", "sira": 0 }
  ],
  "ek_kosullar": {
    "AGIR_YANMIS": "Yangın {siddet_adi} sınıfında (dNBR {dnbr}); toprak yüzeyi büyük olasılıkla açıkta."
  },
  "zamanlama_notu": "Ölçümlerimizde 1. yıl değerleri ...",
  "esikler": { "toparlanma_zayif": 0.35, "toparlanma_iyi": 0.5, "egim_dik_derece": 25.0 },
  "tur_tablosu": { "kaynak": "ÖRNEK TABLO — OGM tür-yetişme ortamı rehberi ile değiştirilecek", "surum": "0.1-ornek", "onaylandi": false },
  "aciklama": "..."
}
```

- **Kritik alan:** `tur_tablosu.onaylandi`. `false` olduğu sürece `CellVerdictDto.tur_onerisi`'nin gösterildiği HER yerde bu uyarı da gösterilmelidir (bkz. §5.2, docs/hukum_sozlesmesi.md "Tür önerisi — dikkat").
- `hukumler[]` — 7 kod için `{kod, baslik, sira}`; `sira`, UI'da hüküm listesinin gösterim SIRASINI verir (öncelik SIRALAMASI değil — hüküm önceliksiz bir kategoridir).
- `ek_kosullar` — kod → Türkçe şablon cümle sözlüğü (`{yol}`, `{egim}` gibi yer tutucular içerir; `CellVerdictDto.ek_kosullar`'daki kodlarla eşlenir).

### 7.3. Hatalar

| `code` | HTTP | Ne zaman |
| --- | --- | --- |
| `HUKUM_SOZLUGU_NOT_FOUND` | 404 | Global satır hiç içe aktarılmamış — ImportTool çalıştıktan sonra normal koşulda GERÇEKLEŞMEMESİ gereken bir durum |

---

## 8. Hata sözleşmesi (RFC 7807 `ProblemDetails`)

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
| `CELL_NOT_FOUND` | 404 | Yangın var, ama `{cellId}` o yangında yok (bkz. §5) |
| `CELL_VERDICT_NOT_FOUND` | 404 | Hücre var, ama henüz hükmü içe aktarılmamış (bkz. §5) |
| `FIRE_NARRATIVE_NOT_FOUND` | 404 | Yangın var, ama henüz anlatı özeti içe aktarılmamış (bkz. §6) |
| `HUKUM_SOZLUGU_NOT_FOUND` | 404 | Global hüküm sözlüğü hiç içe aktarılmamış (bkz. §7) |
| `DB_UNAVAILABLE` | 503 | Veritabanına bağlanılamadı / bağlantı sorgu sırasında koptu |
| `UNEXPECTED_ERROR` | 500 | Yukarıdakilerin hiçbiri değil — gerçekten beklenmeyen bir istisna (genel `AddProblemDetails()` fallback'i). Endpoint'in kendi ürettiği bir `code` varsa bu ASLA onun üzerine yazmaz. |

---

## 9. Sağlık kontrolü (health check)

Auth/veri sözleşmesi dışında, ops/monitoring için:

| Endpoint | Ne kontrol eder | Başarılı | Başarısız |
| --- | --- | --- | --- |
| `GET /health/live` | Süreç ayakta mı (DB'ye HİÇ dokunmaz) | `200 { "status": "healthy" }` | — (süreç çökmüşse zaten yanıt vermez) |
| `GET /health/ready` | Süreç ayakta VE DB'ye bağlanabiliyor mu | `200 { "status": "healthy" }` | `503 { "status": "unhealthy" }` |

`/live` "restart gerekir mi" sorusuna, `/ready` "trafik alabilir mi" sorusuna cevap verir — DB geçici olarak kesildiğinde `/live` yine `200` döner (uygulama sağlıklı, sadece DB'ye erişemiyor), `/ready` `503` döner.

---

## 10. Performans ve sıkıştırma

`/cells`, en büyük yangında (`AKD_2021_01`, 9048 hücre) sıkıştırılmamış ~4 MB JSON döner. Response compression (Brotli/Gzip, `CompressionLevel.Fastest`) açık — auth/gizli veri olmadığı için BREACH/CRIME riski yok, HTTPS için de güvenle etkin.

Sayı **iddia değil, ölçüm**: `backend/scripts/perf-check.ps1` — kendi makinende `dotnet run --project backend/ReGreen.Api` çalışırken çalıştırıp tekrar üretebilirsin (curl.exe kullanır, Windows 10 1803+/11'de hazır gelir — Windows PowerShell 5.1'in `Invoke-WebRequest`/`HttpClient`'i büyük gövdelerde onlarca kat yavaş/yanıltıcı ölçüm verdiği için BİLEREK kullanılmaz, script'in başındaki not'ta ölçülerek belgelendi).

Bu ortamda ölçülen (10 istek ortalaması, 3 ısınma isteği sonrası, `dotnet run --configuration Release`):

| Senaryo | Gecikme (ort/min/max) | Sıkıştırılmamış | Sıkıştırılmış | Kazanç |
| --- | --- | --- | --- | --- |
| `AKD_2021_01/cells` (tam, 9048 hücre) | 66 / 57 / 80 ms | 4.028.958 bayt | 1.044.330 bayt | %74 |
| Aynı + bounding box (1495 hücre) | 22 / 19 / 27 ms | 665.360 bayt | 175.332 bayt | %74 |

Sıkıştırma yanıt boyutunu küçültür ama frontend'in **9048 hücreyi işleme maliyetini ortadan kaldırmaz** — harita viewport'una göre bounding box kullanmak (yukarıdaki ikinci satır) hâlâ önerilir, ikisi birbirini tamamlıyor.

---

## 11. Bağlantı ve ortam

- Bağlantı dizesi önceliği: `REGREEN_CONNECTION_STRING` env var → `appsettings.json`'daki `ConnectionStrings:Default` → localdb geliştirme fallback'i (ImportTool ile aynı konvansiyon).
- Migration API başlangıcında OTOMATİK çalıştırılmaz — şema `dotnet ef database update --project backend/ReGreen.Data` ile elle uygulanır.
- CORS origin'leri `Cors:AllowedOrigins` (appsettings) dizisinden gelir; geliştirmede `appsettings.Development.json`'da açıkça listelenir (frontend'in gerçek dev port'una göre güncellenmeli, şu an `http://localhost:5173`/`http://localhost:3000` yer tutucu olarak yazılı).
- OpenAPI: `/openapi/v1.json` (sadece `Development` ortamında).

---

## 12. Referans

- [`docs/data-contract.md`](./data-contract.md) — alan adı/tip/anlam (tek doğru kaynak).
- [`docs/hukum_sozlesmesi.md`](./hukum_sozlesmesi.md) — hüküm katmanının (§5-§7) veri sözleşmesi ve semantiği.
- [`docs/db-schema.md`](./db-schema.md) — DB şeması, CHECK/composite FK kısıtları.
- [`docs/import-flow.md`](./import-flow.md) — import pipeline kuralları.
