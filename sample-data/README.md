# Sample Data

AI tarafının ürettiği veri paketleri. Backend ve frontend bu klasördeki
dosyalara göre geliştirme yapar.

| | |
|---|---|
| Şema sürümü | `1.1` |
| Model sürümü | `rf_v1` |
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

`schema_version` **1.1** ve değişmeyecek. Model geliştirilmeye devam ediyor;
yeni model geldiğinde:

- ✅ Değişecek: `recovery_gap_pred` / `priority_score` **değerleri**, `model_version`
- ❌ Değişmeyecek: sütun isimleri, sütun sayısı, dosya yapısı, formatlar

Yani dosyaları değiştirmek yeterli olacak, kod değişikliği gerekmeyecek.

Şema değişmesi gerekirse `schema_version` `1.2` olur ve **önceden haber verilir**.

---

## `docs/data-contract.md` ile ilişkisi

`docs/data-contract.md` güncel 53 yangınlık paket ve 37.163 hücrenin tamamı
taranarak güncellenmiştir. Alan adı, tip, null davranışı, enum ve doğrulama
kuralları için **tek bağlayıcı kaynak bu dokümandır**.

Bu klasördeki `alan_eslesme.json`, Türkçe açıklamalar ve iç isim ↔ API ismi
eşleşmeleri için yardımcı kaynaktır. Bir uyuşmazlık görülürse
`docs/data-contract.md` esas alınmalıdır.
