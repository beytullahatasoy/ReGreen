# ReGreen — AI Teslim Paketi

**Bu örnek değil, gerçek veri.** 53 yangın, 37.163 hücre, model çıktısı dolu.

| | |
|---|---|
| Yangın | 53 |
| Toplam hücre | 37.163 |
| Hücre boyutu | 250 × 250 m (6,25 ha) |
| Koordinat sistemi | EPSG:4326 |
| Model sürümü | `ridge_v2` |
| Şema sürümü | `1.1` |

Keşfedilen 55 yangının 2'sinde yanık hücre kalmadığı için 53 tane var.

---

## Model performansı

Bütün sayılar **out-of-fold**: model o yangını hiç görmeden tahmin etti
(LeaveOneGroupOut, 27 mekânsal grup).

| Metrik | Değer |
|---|---|
| Grup içi Spearman (ortalama) | **+0,686** |
| Grup içi Spearman (medyan) | +0,744 |
| Pozitif grup | **27/27** |
| En kötü grup | +0,140 |
| En iyi grup | +0,879 |
| top-%20 isabet | **%52,8** |
| — dNBR (saha yöntemi) tabanı | %38,8 |
| — rastgele seçim | %20,0 |
| İkili karşılaştırma doğruluğu | **%75,8** |

**İkili karşılaştırma** en anlaşılır olanı: aynı yangından iki hücre verildiğinde
hangisinin 2 yıl sonra daha kötü durumda olacağını %75,8 doğrulukla biliyor.
Sahadaki yöntem %62,4, yazı-tura %50.

Aynı sayılar `manifest.json` içinde `model_performance` altında da var —
arayüzde göstermek isterseniz oradan okuyun, elle kopyalamayın.

Modelin nasıl seçildiği, denenip elenen 20+ yöntem ve neden burada durulduğu:
**`MODEL_GUNLUGU.md`**

---

## Klasör yapısı

```
teslim/
  manifest.json                  ← ÖNCE BUNU OKU
  alan_eslesme.json              iç isim ↔ API ismi sözlüğü
  oncelik.py                     öncelik formülünün referans uygulaması
  OKUBENI.md                     bu dosya

  {fire_id}_hucreler.csv         asıl veri — her satır bir hücre
  {fire_id}_sinir.geojson        yangın dış sınırı (MultiPolygon)
  {fire_id}_metadata.json        o yangına ait sürüm/kalite bilgisi
```

**Import script'i `manifest.json`'u okuyup döngüye girsin, klasör taramasın.**
Böylece yarım kalmış veya bozuk bir dosyayı yanlışlıkla almaz.

`manifest.json` içindeki `fires` dizisinde her yangın için `fire_id`,
`fire_date`, `province`, `region`, `cell_count`, `burned_area_ha`,
`quality_flag`, `has_perimeter` ve `status_counts` var.

---

## Sütunlar (19)

### Kimlik (4)
`fire_id`, `cell_id`, `lat`, `lon`

`cell_id` **global olarak benzersiz** — 37.163 satır, 37.163 eşsiz kimlik.
Primary key olarak kullanabilirsin. Kalıp: `{fire_id}_{6 haneli}`.

`lat`/`lon` hücrenin **merkezi**. Kareyi buradan üret:

```python
d_lat = 125 / 110540
d_lon = 125 / (111320 * cos(radians(lat)))   # enleme göre değişir
```

`d_lon`'u sabit yazma. Izgara UTM'de üretildi; merkezden yeniden üretmenin
hatası en kötü **2,1 m** (250 m hücrede %1'in altında), görünmez.

### Model girdisi (6)
`tree_cover`, `tree_cover_annual`, `burn_severity_dnbr`, `slope_deg`,
`elevation_m`, `road_distance_km`

### Sadece gösterim (5)
`ndvi_before`, `ndvi_after`, `ndvi_drop`, `severity_class`, `land_cover`

Modele **girmez**. Hücre detay panelinde hasarı göstermek için
("yangın öncesi 0,65 → sonrası 0,12").

### Model çıktısı (4)
`prediction_status`, `recovery_gap_pred`, `priority_score`, `priority_class`

---

## Duruma göre hangi alan dolu

| `prediction_status` | Sayı | `recovery_gap_pred` | `priority_score` | `priority_class` |
|---|---|---|---|---|
| `predicted` | 17.274 (%46) | dolu | hesaplanır | eşikten |
| `low_severity` | 19.867 (%53) | **null** | **0.0** | **DUSUK** |
| `no_data` | 22 (%0,1) | null | null | null |

`low_severity` = hafif yanmış, model bu şiddet aralığını eğitimde hiç
görmedi. Skoru boş bırakmıyoruz çünkü operasyonel karşılığı zaten "en düşük
öncelik" — 0.0 verince sıralamada en alta düşüyor ve backend yorum yapmak
zorunda kalmıyor.

Haritada `low_severity` gri/soluk, `no_data` ayrı bir tonla gösterilsin;
kırmızı-sarı skalasına sokulmasın.

---

## Değer aralıkları

```
recovery_gap_pred : 0.119 - 0.576      (bu pakette ölçülen gerçek aralık)
priority_score    : 0.000 - 0.968
priority_class    : DUSUK 20.379 · YUKSEK 8.518 · ORTA 7.837 · COK_YUKSEK 407
```

**`recovery_gap_pred` YÜKSEK = KÖTÜ** (eski hâline dönememiş). Ters çevirip
"recovery_score" deme, harita iyileşen yerleri kırmızı gösterir.

Doğrulamada `-0.5 ≤ x ≤ 1.5` dışını reddet ve logla. Renk skalasını
**0–0.7** aralığına kur, yoksa bütün hücreler aynı tonda çıkar.

---

## Öncelik hesabı

`oncelik.py` referans uygulamadır. Backend aynı hesabı kullanıcının seçtiği
ağırlıklarla tekrar çalıştırır — model yeniden koşmaz, kaydırıcı anlık olur.

Normalizasyon **yangın içinde**, sadece `predicted` hücreler referans:

```
n(x) = (x - min) / (max - min)          0-1 arasına kırpılır

priority_score = 0.50 * n(recovery_gap_pred)
               + 0.30 * n(slope_deg)
               + 0.20 * (1 - n(road_distance_km))
```

Son terim ters, çünkü yola **yakın** olan avantajlı. Ağırlıklar her zaman
toplamı 1 olacak şekilde normalize edilir.

```
>= 0.75   COK_YUKSEK
>= 0.50   YUKSEK
>= 0.25   ORTA
<  0.25   DUSUK
```

Kullanılan ağırlık ve eşikler her `metadata.json` ve `manifest.json` içinde
kayıtlı.

**Normalizasyon neden yangın içinde:** bütçe yangın bazında ayrılıyor, soru
"bu yangında nereden başlayalım". Yangınlar arası karşılaştırma yapmıyoruz.

### ⚠️ Alt küme üzerinde çalışıyorsan: `normalization_reference` kullan

Normalizasyonun min/max'ı **yangının tüm `predicted` hücrelerinden** gelir.
Elinde sadece bir alt küme varsa (test için 30 hücre gibi) kendi min/max'ını
hesaplarsan **farklı sonuç çıkar** — ölçtük, fark 0,41'e kadar çıkabiliyor.

Bu yüzden her `metadata.json` içinde kullanılan değerler kayıtlı:

```json
"normalization_reference": {
  "recovery_gap_pred": { "min": 0.2025, "max": 0.3936 },
  "slope_deg":         { "min": 10.950567, "max": 31.078306 },
  "road_distance_km":  { "min": 0.06847494, "max": 2.121611 }
}
```

Bunları kullanınca alt küme üzerinde bile sonuç **birebir** aynı çıkıyor
(8 yangında test edildi, fark `0.00e+00`).

`oncelik.py` bunu destekliyor:

```python
# alt küme + metadata referansı  → birebir aynı sonuç
oncelik_hesapla(alt_kume, referans=meta["normalization_reference"])

# tam yangın verisi elinde  → referans vermeye gerek yok
oncelik_hesapla(tam_veri)
```

Referansı **ağırlık değişse bile aynı tut**. Ağırlık değiştirmek skorun
bileşimini değiştirir, normalizasyon tabanını değil — yoksa aynı hücre
kullanıcı kaydırıcıyı oynattıkça farklı tabanla ölçeklenir ve sıralama
tutarsız olur.

---

## Tahminlerin dürüstlüğü — önemli

Eğitimde kullanılan yangınlara doğrudan model tahmini vermek **iyimser**
olurdu (model o hücreleri görmüş). Bu yüzden:

- **Eğitimde olan hücreler** → kat dışı (out-of-fold) tahmin. `GroupKFold`
  ile, hücrenin kendi mekânsal grubu dışarıda bırakılarak eğitilmiş
  modelden.
- **Eğitimde olmayan hücreler** → tüm veriyle eğitilmiş modelden, gerçek
  dış örnek.

Yani gördüğün sayılar sahada göreceğimiz sayılarla aynı zorlukta.
Her yangının `metadata.json`'unda `in_training_set` ve `out_of_fold_cells`
alanları bunu belirtiyor.

Model: RandomForest, 400 ağaç, 16.074 satır / 27 mekânsal grup ile eğitildi.

---

## Kalite bayrağı

`quality_flag` iki değer alıyor:

- `ok` — 49 yangın
- `check` — 4 yangın. Bizim çıkardığımız yanık alan MODIS'in bildirdiğinden
  4-8 kat büyük. Hepsi iç bölge yangını; dNBR tabanlı yanık sınırımız
  bozkır/step örtüsünde şişiyor.

`check` olanları listede göster ama kullanıcı filtreleyebilsin.

---

## Bilinen sınırlar

**Türkiye dışında çalışmaz.** Model Türkiye yangınlarıyla eğitildi.

**Veri Ege ve Akdeniz ağırlıklı.** Bu bir eksik değil — Türkiye'de 300 ha
üstü yanık alanların iç bölgelerdeki %95'i orman yangını değil anız yakma,
ölçtük.

**`low_severity` hücrelerde erozyon riski değerlendirilmiyor.** Hafif yanmış
ama çok dik bir hücre teoride müdahale gerektirebilir; v1 bunu yakalamıyor.

---

## Yeniden üretim

```
python teslim_uret.py
```

Model yeniden eğitilir, `model.joblib` ve `teslim/` klasörü baştan yazılır.
Sürüm numarası değişirse `manifest.json` ve her `metadata.json` otomatik
güncellenir.
