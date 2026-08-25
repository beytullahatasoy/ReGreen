# Sample Data

AI tarafının ürettiği veri paketleri. Backend ve frontend bu klasördeki
dosyalara göre geliştirme yapar.

| | |
|---|---|
| Şema sürümü | `1.1` |
| Model sürümü | `ridge_v2` |
| Hücre boyutu | 250 m × 250 m (6,25 ha) |
| Koordinat sistemi | EPSG:4326 (WGS84) |

---

## İki klasör var, karıştırma

### `backend-data/` — 53 yangın, tam veri seti (7,6 MB)

Backend'in üzerine kuracağı **gerçek** veri. Örnek değil.

```
backend-data/
  manifest.json          ← ÖNCE BUNU OKU
  alan_eslesme.json      iç isim ↔ API ismi sözlüğü
  oncelik.py             öncelik formülünün referans uygulaması
  OKUBENI.md             ne var, nasıl kullanılır

  {fire_id}_hucreler.csv       hücre verisi + model çıktısı
  {fire_id}_sinir.geojson      yangın sınırı (poligon)
  {fire_id}_metadata.json      ağırlıklar, eşikler, normalizasyon referansı
```

53 yangın × 3 dosya = 159 dosya. Toplam **37.163 hücre**.

### `frontend-data/` — 3 örnek yangın (2,1 MB)

Frontend'in arayüzü kurarken mocklaması için küçük bir alt küme.
**Gerçek veri backend API'sinden gelecek.**

```
frontend-data/
  HUCRE_PANELI.md        ← hücreye tıklanınca ne gösterilecek, tam spec
  ornek_hucreler.json    üç durumun her birinden birer hücre, mock'a hazır
  alan_eslesme.json
  manifest.json          3 yangına indirilmiş
  OKUBENI.md

  AKD_2021_01  9.048 hücre  → harita performansı, yoğun görünüm
  AKD_2021_05  1.428 hücre  → üç durumu da içeriyor (8 tane no_data)
  EGE_2024_10     53 hücre  → kenar durum, az veride panel nasıl duruyor
```

---

## Herkesin bilmesi gereken üç kural

### 1. `prediction_status` panel/filtre tipini belirler

| Değer | Anlamı | Ne yapılmalı |
|---|---|---|
| `predicted` | Model tahmin üretti | Sıralamaya girer, tam panel |
| `low_severity` | Yeterince yanmamış | Gösterilir, önceliklendirilmez |
| `no_data` | Girdi eksik | Tahmin gösterilmez |

> ⚠️ `low_severity` hücrelerde `priority_class` alanı `DUSUK`, `priority_score`
> alanı `0.0` geliyor. Bunlar *değerlendirilip düşük bulunmuş* değil,
> **hiç değerlendirilmemiş**. `predicted` + `DUSUK` olanlardan ayrı göster.
> Önce `prediction_status`'a bak, `priority_class`'a sonra.

### 2. Ağırlıkları ve eşikleri koda gömme

Hepsi her yangının `_metadata.json` dosyasında geliyor:

```json
"priority_weights":    { "recovery": 0.5, "erosion": 0.3, "access": 0.2 },
"priority_thresholds": { "COK_YUKSEK": 0.75, "YUKSEK": 0.5, "ORTA": 0.25 },
"normalization_reference": { ... }
```

Kullanıcının ağırlıkları değiştirebilmesi ürünün ana özelliği — koda gömülürse
o özellik ölür.

`normalization_reference` de aynı şekilde: skorlar yangın içinde normalize
ediliyor. Bir alt küme çekip min/max'ı yeniden hesaplarsan skorlar kayar.

### 3. `recovery_gap_pred` YÜKSEK = KÖTÜ

Eski haline dönememiş demek. Tersine çevirip "recovery_score" diye
gösterme — harita ters çıkar. Renk skalası: yüksek değer = kırmızı.

---

## Sürüm politikası

`schema_version` **1.1** ve değişmeyecek. Model 21.08.2026'da `rf_v1` -> `ridge_v2`
güncellendi; **sütunlar değişmedi**, sadece değerler. Yeni model geldiğinde de:

- ✅ Değişecek: `recovery_gap_pred` / `priority_score` **değerleri**, `model_version`
- ❌ Değişmeyecek: sütun isimleri, sütun sayısı, dosya yapısı, formatlar

Yani dosyaları değiştirmek yeterli olacak, kod değişikliği gerekmeyecek.

Şema değişmesi gerekirse `schema_version` `1.2` olur ve **önceden haber verilir**.

---

## Model performansı

Bütün sayılar **out-of-fold**: model o yangını hiç görmeden tahmin etti
(LeaveOneGroupOut, 27 mekânsal grup).

| Metrik | Değer |
|---|---|
| Grup içi Spearman | **+0,686** |
| Pozitif grup | **27/27** |
| top-%20 isabet | **%52,8** (dNBR tabanı %38,8 · rastgele %20) |
| İkili karşılaştırma doğruluğu | **%75,8** (dNBR %62,4 · yazı-tura %50) |

Aynı sayılar `backend-data/manifest.json` içinde `model_performance` altında.
Arayüzde göstermek isterseniz **oradan okuyun, elle kopyalamayın**.

Modelin nasıl seçildiği, denenip elenen 20'den fazla yöntem ve neden burada
durulduğu: **`MODEL_GUNLUGU.md`** (iki klasörde de var).

## Backend'den bir istek: `normalization_reference`

`GET /api/fires/{fireId}/cells` cevabı (`CellsResponseDto`) şu an
`normalization_reference` döndürmüyor.

Frontend, hücre panelinde öncelik skorunun **üç bileşene** ayrılmış halini
göstermek istiyor — sistemin "karar destek" iddiasının görünür olduğu yer burası:

```
İyileşme açığı    ████████████████░░░░  %53   (0,429)
Erozyon riski     ████████░░░░░░░░░░░░  %27   (0,222)
Ulaşılabilirlik   ██████░░░░░░░░░░░░░░  %20   (0,162)
                                        toplam 0,813
```

Bu kırılım için her bileşenin normalize edilmiş değeri gerekiyor, o da
`normalization_reference` olmadan hesaplanamıyor. Toplam skor gösterilebiliyor
ama **neden o skor olduğu gösterilemiyor.**

İstenen ek — üç alan, cevabın kökünde (hücre başına değil, yangın başına sabit):

```json
"normalization_reference": {
  "recovery_gap_pred": { "min": 0.1243, "max": 0.5417 },
  "slope_deg":         { "min": 0.0513, "max": 50.4633 },
  "road_distance_km":  { "min": 0.0032, "max": 2.9278 }
}
```

Değerler **zaten DB'de** — `FireImporter` bunları `NormRecoveryGapMin/Max`,
`NormSlopeMin/Max`, `NormRoadMin/Max` olarak yazıyor. Yeni hesap yok, sadece
cevaba eklenmesi gerekiyor.

> Ağırlık parametreleriyle (`?recovery=&erosion=&access=`) sunucu tarafında
> yeniden hesaplama zaten doğru çalışıyor ve iyi bir tasarım — normalizasyon
> referansı sabit kalıyor, sadece ağırlık değişiyor. Bu istek onun yerine
> geçmiyor, üstüne ekleniyor.

Detay: `frontend-data/HUCRE_PANELI.md` → "Bölüm B · NEDEN bu öncelik?"

---

---

## `docs/data-contract.md` ile ilişkisi

`docs/data-contract.md` güncel 53 yangınlık paket ve 37.163 hücrenin tamamı
taranarak güncellenmiştir. Alan adı, tip, null davranışı, enum ve doğrulama
kuralları için **tek bağlayıcı kaynak bu dokümandır**.

Bu klasördeki `alan_eslesme.json`, Türkçe açıklamalar ve iç isim ↔ API ismi
eşleşmeleri için yardımcı kaynaktır. Bir uyuşmazlık görülürse
`docs/data-contract.md` esas alınmalıdır.

> ⚠️ **`ridge_v2` ile güncellenmesi gereken yer:** sözleşmenin §3 başlığı
> *"Model Girdisi — 6 Öznitelik (Random Forest'a girer)"*. Artık **7 öznitelik**
> (`ndvi_drop` eklendi) ve model **Ridge**. `ndvi_drop` sütunu zaten teslimde
> vardı, sadece gösterim sayılıyordu — **CSV şeması değişmedi**, sadece o
> sütunun modele girip girmediği değişti. §3 ve §6.4'teki gözlenen aralıklar
> da yeni modelle biraz kayar.
