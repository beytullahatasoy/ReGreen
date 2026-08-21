# ReGreen — Frontend Örnek Paketi

Arayüzü geliştirirken kullanman için 3 örnek yangın.
**Gerçek veri API'den gelecek** — bunlar mocklamak için.

---

## Klasörde ne var

| Dosya | Ne işe yarar |
|---|---|
| `HUCRE_PANELI.md` | **Önce bunu oku.** Hücreye tıklanınca ne gösterileceğinin tam spesifikasyonu |
| `ornek_hucreler.json` | Üç durumun her birinden birer hücre — mock için hazır |
| `alan_eslesme.json` | Alan isimleri sözlüğü (iç ↔ API) |
| `manifest.json` | Yangın listesi ve genel ayarlar |
| `{fire_id}_hucreler.csv` | Hücre verisi |
| `{fire_id}_sinir.geojson` | Yangın sınırı — haritada poligon |
| `{fire_id}_metadata.json` | Ağırlıklar, eşikler, normalizasyon referansı |

## Seçilen yangınlar

### `AKD_2021_01` — 9.048 hücre
Manavgat - en buyuk yangin, 9.048 hucre. Harita performansi ve yogun hucre gorunumu icin.

### `AKD_2021_05` — 1.428 hücre
Orta boy, 1.428 hucre. UC DURUMU DA iceriyor (442 predicted, 978 low_severity, 8 no_data).

### `EGE_2024_10` — 53 hücre
En kucuk, 53 hucre. Az veride panel/harita nasil duruyor - kenar durum testi.

---

## Üç kural

1. **Önce `prediction_status`'a bak.** Panel tipini o belirliyor, 
   `priority_class` değil. `low_severity` hücrelerde `priority_class` 
   `DUSUK` geliyor ama bunlar *değerlendirilip düşük bulunmuş* değil, 
   *hiç değerlendirilmemiş*. Ayrı renk kullan.

2. **Eşikleri ve ağırlıkları `metadata.json`'dan oku**, koda gömme. 
   Kullanıcının ağırlıkları değiştirebilmesi ürünün ana özelliği.

3. **`recovery_gap_pred` YÜKSEK = KÖTÜ.** Tersine çevirip "iyileşme 
   skoru" diye gösterme, harita ters çıkar. Renk skalası: yüksek = kırmızı.

---

Soru olursa Buğra'ya yaz. Alan isimleri veya biçim değişirse 
haber verilecek — sessizce değişmeyecek.
