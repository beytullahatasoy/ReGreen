# ReGreen — Veri Sözleşmesi (data-contract.md)

**Sürüm:** 1.7
**Kapsam:** AI (Buğra) → Backend (Beytullah) → Frontend (Zeynep). ReGreen Zone / Campaign / Community / Volunteer / Field Observation bu sözleşmenin kapsamı DIŞINDA (Faz 3).
**Dayanak:** Bu belge, gerçek `ReGreen_AI_teslim_v1.zip` paketi (53 yangın, 37.163 hücre) tek tek açılıp doğrulandıktan sonra yazılmıştır. Referans kaynaklar: `alan_eslesme.json`, `oncelik.py`, `OKUBENI.md`. **1.2 sürümünde**, ekip arkadaşının teslim ettiği `sample-data/` paketindeki 53 yangının TAMAMI (37.163 satır) satır satır script ile tekrar tarandı — alan adları, null sayıları, enum değerleri ve sayısal aralıklar gerçek veriyle karşılaştırıldı (bkz. §12).
**Kural:** Bu dosya tek doğru kaynaktır. Alan adı/tip/anlam konusunda anlaşmazlık çıkarsa buraya bakılır, kimse kendi hafızasından isim üretmez. Değişiklik gerekiyorsa önce bu dosya PR ile güncellenir, sonra kod yazılır.

---

## 1. Genel İlkeler

- İç isimler (AI'nın eğitim setinde kullandığı Türkçe adlar) ile API isimleri (backend/frontend'in kullandığı İngilizce adlar) FARKLIDIR. Bu belge sadece **API isimlerini** (İngilizce) tanımlar — Türkçe iç isimler `alan_eslesme.json`'da, backend/frontend'i ilgilendirmez.
- Değerler iç veri ile API çıktısında **birebir aynıdır**, sadece isim ve kapsam (hangi hücrelerin dahil olduğu) farklıdır.
- Hücre boyutu: **250 × 250 m** (6,25 ha). ~~500 × 500 m~~ eski/yanlış varsayımdır, kullanılmaz.
- Toplam kapsam (doğrulanmış): **53 yangın, 37.163 hücre**. ~~55 yangın~~ eski varsayımdır (55 keşfedilen yangının 2'sinde filtre sonrası yanık hücre kalmadığı için final teslimde 53 var).
- **Kabul edilen aralık / Örnek değer kolonları hakkında:** Aşağıdaki tablolardaki "Gözlenen aralık" değerleri 53 yangının 37.163 satırının TAMAMI taranarak çıkarılmıştır (min/max, script ile). Bunlar sözleşmesel bir üst/alt sınır değil, **teslim edilen gerçek paketten ölçülen** referans aralıklardır — yeni bir model teslimatında biraz kayabilir. Sabit doğrulama sınırı gereken yerlerde (`recovery_gap_pred`, `priority_score`) ayrıca "Doğrulama kuralı" belirtilmiştir (§6.4).

---

## 2. Kimlik Alanları

| API alanı   | Tip    | Null? | Açıklama                                                                                           | Kabul edilen değer / aralık                                                                           | Örnek değer          | DB karşılığı        |
| ----------- | ------ | ----- | -------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- | -------------------- | ------------------- |
| `fire_id`   | string | Hayır | Yangın kimliği, ör. `AKD_2021_04`                                                                  | Kalıp: `{BOLGE}_{YIL}_{SIRA}`. Görülen bölge kodları: `AKD`, `EGE`, `DAN`, `GDA`, `IAN`, `KAR`, `MAR` | `AKD_2021_05`        | `Fires.FireId` (PK) |
| `cell_id`   | string | Hayır | Hücre kimliği. **Global benzersiz** (37.163/37.163 doğrulandı). Kalıp: `{fire_id}_{6 haneli sıra}` | —                                                                                                     | `AKD_2021_05_000160` | `Cells.CellId` (PK) |
| `lat`       | float  | Hayır | Hücre **MERKEZİ**, EPSG:4326                                                                       | Gözlenen: 36,12982 – 40,42768 (Türkiye sınırları)                                                     | `37.41129`           | `Cells.CenterLat`   |
| `lon`       | float  | Hayır | Hücre **MERKEZİ**, EPSG:4326                                                                       | Gözlenen: 26,24525 – 44,38754 (Türkiye sınırları)                                                     | `35.58044`           | `Cells.CenterLon`   |
| `province`  | string | Hayır | İl                                                                                                 | Türkiye il adları (Türkçe karakterli, ör. `Muğla`, `İzmir`)                                           | `Adana`              | `Fires.Province`    |
| `region`    | string | Hayır | Bölge                                                                                              | Gözlenen: `Akdeniz`, `Ege`, `Marmara`, `Karadeniz`, `Dogu Anadolu`, `Ic Anadolu`                      | `Akdeniz`            | `Fires.Region`      |
| `fire_date` | date   | Hayır | Yangın tarihi                                                                                      | ISO 8601, `YYYY-MM-DD`                                                                                | `2021-07-29`         | `Fires.FireDate`    |

### 2.1. Yangın Düzeyi Alanları (hücrede tekrarlanmaz)

`alan_eslesme.json`'daki `yangin_duzeyi` grubu ile örtüşür. Hücre CSV'sinde YOKTUR. **Ama üç dosyada da BİREBİR AYNI ŞEKİLDE tekrarlanmaz** — hangi alanın nerede bulunduğu alan alan farklıdır, aşağıdaki tablonun "Nerede bulunur" kolonuna bakın. Özet: `fire_id`/`fire_date`/`province`/`region` üçünde de var; `modis_area_ha` sadece geojson + metadata'da (manifest'te YOK); `burned_area_ha`/`cell_count`/`has_perimeter` sadece manifest + metadata'da (geojson'da YOK, bkz. §7.2'deki 5 alanlık `Feature.properties` listesi).

| API alanı                                    | Tip   | Null? | Açıklama                                                                                                                                                                                                                     | Kabul edilen değer / aralık                            | Örnek değer            | Nerede bulunur                                                                                 |
| -------------------------------------------- | ----- | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ | ---------------------- | ---------------------------------------------------------------------------------------------- |
| `fire_id`, `fire_date`, `province`, `region` | —     | Hayır | §2 ile aynı, yangın bazında                                                                                                                                                                                                  | §2 ile aynı                                            | §2 ile aynı            | geojson properties, manifest `fires[]`, metadata                                               |
| `modis_area_ha`                              | float | Hayır | MODIS uydu verisine göre **bağımsız ölçülen** yanık alan (hektar). AI'ın kendi dNBR yönteminden hesapladığı `burned_area_ha` ile FARKLI kaynaktan gelir — ikisi arasındaki büyük fark `quality_flag = check` sebebidir (§10) | Gözlenen: 315 – 57.716 ha (53 yangının tamamı tarandı) | `3768` (AKD_2021_05)   | geojson properties, metadata.json — **contract'ta önceki sürümde tanımsızdı, 1.2 ile eklendi** |
| `burned_area_ha`                             | float | Hayır | AI'ın kendi hesapladığı yanık alan (hektar), `cell_count × 6,25 ha` ile tutarlı                                                                                                                                              | —                                                      | `8925.0` (AKD_2021_05) | manifest `fires[]`, metadata.json                                                              |
| `cell_count`                                 | int   | Hayır | Yangının toplam hücre sayısı                                                                                                                                                                                                 | 37 – 9048 (bu pakette gözlenen)                        | `1428` (AKD_2021_05)   | manifest `fires[]`, metadata.json                                                              |
| `has_perimeter`                              | bool  | Hayır | Yangın sınırı (`_sinir.geojson`) mevcut mu                                                                                                                                                                                   | 53 yangının 53'ünde de `true` (bu paket için)          | `true`                 | manifest `fires[]`, metadata.json                                                              |

> **Not:** `modis_area_ha` ve `burned_area_ha` isim olarak birbirine benzediği için karıştırılmamalı: biri dış referans (MODIS), diğeri AI'ın kendi ürettiği alan. İkisini aynı alanmış gibi göstermeyin.

---

## 3. Model Girdisi — 7 Öznitelik (Ridge Regression'a girer)

Algoritma `ModelRuns.ModelVersion`'a göre değişebilir (bu paket: `ridge_v2`, önceki paket `rf_v1`'de Random Forest kullanılıyordu) — burada "model" ifadesi genel olarak o an aktif regresyon modelini kasteder, koda gömülü bir varsayım değildir.

| API alanı            | Tip   | Null?                                                       | Açıklama                                                                                | Kabul edilen değer / aralık                                                                                       | Örnek değer | DB karşılığı             |
| -------------------- | ----- | ----------------------------------------------------------- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------- | ------------------------ |
| `tree_cover`         | float | Hayır                                                       | Ağaç örtü oranı                                                                         | Gözlenen: 0,0 – 1,0 (oran)                                                                                        | `0.0`       | `Cells.TreeCover`        |
| `tree_cover_annual`  | float | **Evet** — 37.163 hücrenin **1.681'inde NULL** (doğrulandı) | Yıllık ağaç örtü oranı. Eksikse model `tree_cover`'dan doldurur, zorunlu girdi sayılmaz | Gözlenen (dolu satırlarda): 0,0 – 1,0                                                                             | `0.0`       | `Cells.TreeCoverAnnual`  |
| `burn_severity_dnbr` | float | Hayır                                                       | Yanma şiddeti (dNBR)                                                                    | Gözlenen: 0,10 – 1,14                                                                                             | `0.3068124` | `Cells.BurnSeverityDnbr` |
| `slope_deg`          | float | Hayır                                                       | Eğim (derece)                                                                           | Gözlenen: 0,0 – 79,06                                                                                             | `5.2742534` | `Cells.SlopeDeg`         |
| `elevation_m`        | float | **Evet** — 37.163 hücrenin **22'sinde NULL** (doğrulandı)   | Yükselti. Eksikse `prediction_status = no_data` tetiklenir (§6.1)                       | Gözlenen: −0,28 – 2299,37 (deniz seviyesine yakın hücrelerde küçük negatif değer DEM gürültüsüdür, hata değildir) | `264.05188` | `Cells.ElevationM`       |
| `road_distance_km`   | float | Hayır                                                       | Yola mesafe                                                                             | Gözlenen: 0,0009 – 5,21                                                                                           | `0.6179377` | `Cells.RoadDistanceKm`   |
| `ndvi_drop`          | float | Hayır                                                       | **Üç rollü:** (1) `ridge_v2`'den itibaren modele GİRER — önceki paket (`rf_v1`) sadece gösterim/eşik değişkeni olarak kullanıyordu, (2) `prediction_status` kural motorunda eşik değişkeni (§6.1), (3) UI'da gösterilir | Gözlenen: **−0,12 – 0,74**. Nadiren negatif olabilir (yangın sonrası ölçümde NDVI'nin hafifçe yüksek çıkması — ölçüm zamanlaması/gürültü kaynaklı, hata değil) | `0.272803`  | `Cells.NdviDrop`         |

Bu 7 alan **hücrenin sabit özelliğidir** — model tekrar çalışsa bile değişmez, bu yüzden DB'de `Predictions` değil `Cells` tablosunda tutulur.

---

## 4. Model Girdisi DEĞİL — Gösterim / Kural Alanları

> `ndvi_drop` burada YOK — `ridge_v2`'den itibaren model girdisi olduğu için §3'e taşındı (üç rolünden ikisi hâlâ burayla ilgili: kural motoru eşiği ve UI gösterimi, ama tablo tekilliği için tek yerde tutuluyor).

| API alanı        | Tip           | Null?                                                                 | Rol                                                                                                                  | Kabul edilen değer / aralık                                                                                                                                                                                                                                | Örnek değer  | DB karşılığı          |
| ---------------- | ------------- | --------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------ | --------------------- |
| `ndvi_before`    | float         | Hayır                                                                 | SADECE gösterim. Hedef değişkenin bileşeni olduğu için modele hiç verilmez                                           | Gözlenen: 0,20 – 0,89                                                                                                                                                                                                                                      | `0.46812958` | `Cells.NdviBefore`    |
| `ndvi_after`     | float         | Hayır                                                                 | SADECE gösterim                                                                                                      | Gözlenen: 0,03 – 0,80                                                                                                                                                                                                                                      | `0.19532657` | `Cells.NdviAfter`     |
| `severity_class` | string (enum) | Hayır                                                                 | SADECE gösterim                                                                                                      | **4 değer** (53 yangının TAMAMI tarandı): `dusuk`, `orta-dusuk`, `orta-yuksek`, `yuksek` (`sample-data/frontend-data/HUCRE_PANELI.md`'deki eski 3 değerli varsayım düzeltildi — bkz. §12 1.6) | `orta-dusuk` | `Cells.SeverityClass` |
| `land_cover`     | string (enum) | **Evet** — 1.681 hücrede NULL (`tree_cover_annual` ile aynı satırlar) | SADECE gösterim                                                                                                      | **7 değer** (53 yangının TAMAMI tarandı): `Agaclik`, `Ciplak`, `Otlak/calilik`, `Su`, `Sulak alan`, `Tarim`, `Yerlesim` — önceki sürümde hiç listelenmemişti                                                                                               | `Tarim`      | `Cells.LandCover`     |

**Kullanım örneği (hücre detay paneli):** `ndvi_before` → `ndvi_after` iki noktalı karşılaştırma: "yangın öncesi 0,65 → sonrası 0,12".

---

## 5. Kaldırılan Alanlar — API'ye HİÇ girmez

| İç alan               | Sebep                                                         |
| --------------------- | ------------------------------------------------------------- |
| `water_distance`      | 27 gruptan 13'ünde aynı yönde (p=1,00) — yazı-tura, katkı yok |
| `settlement_distance` | Geri konarak ölçüldü: R² −0,017 — katkı yok                   |
| `aspect` (bakı)       | Büyüklük yok, gürültü sınırında                               |

Bu alanlar iç veri setinde (`A_turkiye_grid_ornek.csv` vb.) durmaya devam eder ama import script bunları görse bile atlar, DB'ye hiç yazmaz. Sample data paketindeki 19 sütunlu CSV'lerde bu alanlar zaten yok — doğrulandı.

Ayrıca, **sadece model eğitiminde** kullanılan ve API'ye asla çıkmayan alanlar: `kalan_acik` (hedef değişken), `grup_id`, `grup_agirlik`, `olay_id`, `yil`, `etiket_gecerli`, `egitime_uygun`, `ndvi_yil1`, `ndvi_yil2`, `yagis_sonrasi_2yil_mm`, `iyilesme_yil2`. Bunlar `egitim_seti.csv`'de bulunur, backend'e hiç gitmez.

---

## 6. Model Çıktısı — 4 Bileşen

**Terminoloji netliği:** `prediction_status`, modelin çıktısı DEĞİLDİR — dNBR + NDVI eşiğine dayanan bir **kural motoru** kararıdır. Modelin (bu paket: Ridge Regression, `model_version = ridge_v2`) gerçek tahmini sadece `recovery_gap_pred`'dir.

| Bileşen                  | API alanı           | Ne tür          | Kim üretir                                              | Ne zaman                      |
| ------------------------ | ------------------- | --------------- | ------------------------------------------------------- | ----------------------------- |
| Tahmin Uygunluk Kontrolü | `prediction_status` | Kural motoru    | AI (Buğra)                                              | Bir kez, dosyada gelir        |
| Recovery Model           | `recovery_gap_pred` | ML tahmini      | AI (Buğra)                                              | Bir kez, dosyada gelir        |
| Priority Engine          | `priority_score`    | Formül (§8)     | AI (varsayılan ağırlık) + Backend (özel ağırlık, anlık) | Dosyada + istek anında        |
| Classification           | `priority_class`    | Eşik uygulaması | Aynı formülün eşik uygulaması                           | `priority_score` ile birlikte |

### 6.1. `prediction_status` — enum, 3 değer (doğrulandı: CSV'lerde başka değer yok)

| Değer          | Koşul (`oncelik.py: durum_belirle()`)                                                                               | Anlamı                                              | Örnek `cell_id`      |
| -------------- | ------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- | -------------------- |
| `predicted`    | `burn_severity_dnbr ≥ 0.27` VE `ndvi_drop ≥ 0.20` VE zorunlu girdi tam                                              | Model güvenle tahmin üretti                         | `AKD_2021_01_032026` |
| `low_severity` | Girdi tam ama yukarıdaki eşiğin altında                                                                             | Hafif yanmış, model bu aralığı eğitimde hiç görmedi | `AKD_2021_01_061483` |
| `no_data`      | Zorunlu girdilerden biri (`tree_cover`, `burn_severity_dnbr`, `slope_deg`, `elevation_m`, `road_distance_km`) eksik | Gerçekten bilinmiyor                                | `AKD_2021_01_011955` |

Örnek teslimde dağılım (37.163 hücre, script ile yeniden sayıldı — birebir tuttu): `predicted` %46,5 (17.274) · `low_severity` %53,5 (19.867) · `no_data` %0,06 (22).

### 6.2. Duruma göre alan davranışı — KESİN TABLO

| `prediction_status` | `recovery_gap_pred` | `priority_score` | `priority_class` |
| ------------------- | ------------------- | ---------------- | ---------------- |
| `predicted`         | dolu                | hesaplanır       | eşikten atanır   |
| `low_severity`      | **null**            | **0.0**          | **DUSUK**        |
| `no_data`           | null                | null             | null             |

> **Kritik kural:** Backend, `low_severity`/`no_data` için KENDİ yorumunu katıp priority üretmez. Bu değerler AI tarafından zaten dolu/null olarak geliyor. `low_severity`'de skor boş bırakılmıyor çünkü hafif yanmış alan operasyonel olarak zaten en düşük öncelik — 0.0 verilince sıralamada en alta düşer ve backend hiçbir yorum yapmak zorunda kalmaz.

37.163 satırın tamamı script ile tarandı: `recovery_gap_pred` null sayısı = 19.889 (`low_severity` 19.867 + `no_data` 22, tam eşleşiyor) · `priority_score`/`priority_class` null sayısı = 22 (sadece `no_data`) · `low_severity` satırlarında `priority_score` her zaman literal `0.0`, boş değil.

### 6.3. `priority_class` — enum, sabit eşik

```
priority_score ≥ 0.75  →  COK_YUKSEK
priority_score ≥ 0.50  →  YUKSEK
priority_score ≥ 0.25  →  ORTA
priority_score <  0.25  →  DUSUK
```

Eşikler her `{fire_id}_metadata.json`'da (`priority_thresholds`) ayrıca kayıtlıdır — yukarıdaki varsayılan değerler değişebilir, backend bunu metadata'dan okumalı, koda gömmemeli (bkz. §8.3). `priority_thresholds` JSON'ının key isimleri `COK_YUKSEK`, `YUKSEK`, `ORTA`'dır (`DUSUK` için ayrı key yok, "diğerleri" anlamına gelir) — doğrulandı, tüm `_metadata.json` dosyalarında birebir bu üç key var.

Doğrulanmış `priority_class` enum değerleri (53 yangın, 37.163 satır tarandı — başka değer yok): `COK_YUKSEK`, `YUKSEK`, `ORTA`, `DUSUK`.

### 6.4. Değer aralıkları ve doğrulama

| Alan                | Gözlenen aralık (53 yangın, tam tarama) | Doğrulama kuralı                      | Not                                                                                |
| ------------------- | --------------------------------------- | ------------------------------------- | ---------------------------------------------------------------------------------- |
| `recovery_gap_pred` | 0,11 – 0,6546 (`ridge_v2`; `rf_v1`'de 0,1187 – 0,576 idi) | `-0.5 ≤ x ≤ 1.5` dışını reddet, logla | **Yön: yüksek = KÖTÜ** (eski hâline dönememiş). `predicted` dışında HER ZAMAN null |
| `priority_score`    | 0,0 – 0,9754 (`ridge_v2`; `rf_v1`'de 0,0 – 0,9683 idi)    | `Math.Clamp(x, 0, 1)`                 | Teorik aralık 0–1, bu paketteki gerçek maksimum 0,9754                             |

**Frontend uyarısı:** `recovery_gap_pred` üzerinden sürekli bir ısı haritası gösterilecekse renk skalası **0–0,7** aralığına göre kurulmalı (0–1 kurulursa dar bant nedeniyle bütün hücreler aynı tonda görünür). Bu SADECE opsiyonel Recovery Gap ısı haritası için geçerli — kategorik Öncelik Haritası (`priority_class` renklendirmesi) bu skalayı hiç kullanmaz.

---

## 7. Geometri Sözleşmesi

### 7.1. Hücre — merkezden üretim

AI sadece hücre **merkezini** (`lat`, `lon`) verir. Kare, backend'de anlık üretilir:

```python
d_lat = 125 / 110540
d_lon = 125 / (111320 * cos(radians(lat)))   # enleme göre değişir, SABİT YAZILMAZ
```

Hata payı en kötü 2,1 m (250 m hücrede %1'in altında, görünmez). Kareler UTM'de üretilip WGS84'e çevrilirken 0,3–0,9 derece dönüyor ama bu sapma ihmal edilebilir.

> **Uyarı:** Hangi hücrelerin VAR OLDUĞU (`cell_id` listesi) min/max enlem-boylamdan türetilemez — hücre listesi yanık maskesine göre filtreleniyor, sadece AI'dan gelen liste kullanılır.

### 7.2. Yangın sınırı (perimeter)

- Geometri tipi **SABİT DEĞİL**: 53 dosyanın tamamı kontrol edildi — 48'i `MultiPolygon`, 5'i düz `Polygon` (`AKD_2021_03`, `AKD_2021_08`, `AKD_2021_11`, `EGE_2022_03`, `EGE_2024_09`). Backend DTO/parser **ikisini de kabul etmeli**.
- `{fire_id}_sinir.geojson` bir tek `Feature`'dır (FeatureCollection değil). `Feature.properties` içinde §2.1'de tanımlanan yangın düzeyi alanları bulunur: `fire_id`, `fire_date`, `province`, `region`, `modis_area_ha` — bu beşi dışında başka alan yok (53 dosyanın tamamında doğrulandı).
- Marker/merkez noktası için **centroid kullanılmaz** — çok parçalı şekillerde centroid parçaların arasına, yanmamış bir noktaya düşebilir. Bunun yerine `representative_point()` mantığı kullanılır: .NET karşılığı NetTopologySuite'in `Geometry.InteriorPoint` özelliği, hem `Polygon` hem `MultiPolygon` için çalışır ve her zaman geometrinin içinde kalır.
- Nokta sayısı bu ölçekte (~1000 civarı) basitleştirme gerektirmiyor.

---

## 8. Öncelik (Priority) Formülü

### 8.1. Kim, ne zaman

| Kim                 | Ne zaman                                                                   | Neden                                              |
| ------------------- | -------------------------------------------------------------------------- | -------------------------------------------------- |
| AI (Buğra)          | Dosya üretilirken, VARSAYILAN ağırlıkla                                    | Sistem kutudan çıkar çıkmaz çalışsın               |
| Backend (Beytullah) | Kullanıcı arayüzde ağırlık kaydırıcısını oynattığında, AYNI FORMÜLLE anlık | Model yeniden koşmaz, sadece formül tekrar çalışır |

Referans uygulama: `oncelik.py`. Backend bunu C#'a birebir portlamalı.

### 8.2. Formül

```
Normalizasyon YANGIN İÇİNDE yapılır (Türkiye geneli DEĞİL),
sadece prediction_status == "predicted" olan hücreler referans alınır:

    n(x) = (x - min) / (max - min)     0-1 arasına kırpılır (Math.Clamp)

    priority_score = 0.50 * n(recovery_gap_pred)
                   + 0.30 * n(slope_deg)
                   + 0.20 * (1 - n(road_distance_km))

Son terim TERS çünkü yola YAKIN olan avantajlı.
Ağırlıklar her zaman toplamı 1 olacak şekilde normalize edilir
(kullanıcı 0.5/0.5/0.5 girse bile 0.333/0.333/0.333'e çevrilir).
```

`priority_weights` JSON'ının key isimleri, yukarıdaki formülün terimleriyle şöyle eşleşir (tüm `_metadata.json` ve `manifest.json` dosyalarında doğrulandı):

| JSON key   | Formüldeki terim                      | Varsayılan değer |
| ---------- | ------------------------------------- | ---------------- |
| `recovery` | `n(recovery_gap_pred)` katsayısı      | `0.5`            |
| `erosion`  | `n(slope_deg)` katsayısı              | `0.3`            |
| `access`   | `(1 - n(road_distance_km))` katsayısı | `0.2`            |

### 8.3. `normalization_reference` — kritik kural

Her `{fire_id}_metadata.json`'da `normalization_reference` alanı var (`recovery_gap_pred`, `slope_deg`, `road_distance_km` için min/max). Gerçek örnek (`AKD_2021_05_metadata.json`, `ridge_v2`):

```json
"normalization_reference": {
  "recovery_gap_pred": { "min": 0.1228, "max": 0.4554 },
  "slope_deg":         { "min": 0.5991507, "max": 16.264103 },
  "road_distance_km":  { "min": 0.02827684, "max": 1.9335308 }
}
```

`slope_deg`/`road_distance_km` aralıkları hücre-sabiti özniteliklere dayandığı için modelden bağımsız AYNI kalır (`rf_v1` ile de birebir aynıydı); `recovery_gap_pred` aralığı ise model çıktısı olduğu için `rf_v1`'de `{ "min": 0.1796, "max": 0.4305 }` idi, `ridge_v2` ile değişti.

> **KESİNLİKLE:** Kullanıcı ağırlığı değiştirdiğinde backend bu min/max'ı **SABİT TUTAR**, sadece ağırlıkları değiştirir. Aksi halde her kaydırıcı hareketinde hücreler farklı bir referans tabanıyla ölçeklenir ve sıralama tutarsız hale gelir.
>
> **Kapsam:** Bu sabitlik SADECE **aynı ModelRun** içinde geçerlidir. Buğra yeni bir `model_version` (ör. `rf_v2`) ile teslimat yaparsa, o teslimat kendi `normalization_reference`'ını taşır — eski `ModelRun` bozulmaz.

**Doğrulama:** 53 yangının 37.163 hücresinin TAMAMINDA, `oncelik.py` + `normalization_reference` ile `priority_score` bağımsız olarak yeniden hesaplandı — dosyadaki değerle fark **tam 0.0** çıktı.

### 8.4. C# portu için 4 kural

1. `normalization_reference` bir kez metadata'dan okunur, ağırlık değişse bile sabit kalır (§8.3)
2. `n(x)` hesabından sonra sonuç 0-1 arasına kırpılır: `Math.Clamp(x, 0, 1)`
3. Ağırlıklar her zaman toplamı 1 olacak şekilde normalize edilir
4. `low_severity`/`no_data` hücreler İÇİN backend HİÇBİR hesap yapmaz — bu değerler zaten dosyada dolu/null geliyor (§6.2)

---

## 9. Dosya Formatları (AI Teslim Paketi)

```
teslim/
  manifest.json                    ← tüm yangınların listesi, import script BUNU okur
  {fire_id}_hucreler.csv           ← asıl teslim formatı (CSV, geometri yok)
  {fire_id}_sinir.geojson          ← yangın dış sınırı (Polygon / MultiPolygon)
  {fire_id}_metadata.json          ← sürüm, ağırlık, eşik, normalizasyon referansı
```

`{fire_id}_hucreler.geojson` (geometrili hâl) **final teslimde YOK** — AI tarafı QGIS'te bakmak için üretmeye devam ediyor ama backend'e gitmiyor.

### 9.1. `{fire_id}_hucreler.csv` — sütun sırası

```
fire_id, cell_id, lat, lon, tree_cover, tree_cover_annual, burn_severity_dnbr,
slope_deg, elevation_m, road_distance_km, ndvi_before, ndvi_after, ndvi_drop,
severity_class, land_cover, prediction_status, recovery_gap_pred, priority_score,
priority_class
```

19 sütun, sırası ve sayısı 53 dosyanın tamamında birebir aynı — doğrulandı. Örnek satır (`AKD_2021_05_hucreler.csv`, `ridge_v2`, `predicted` durumunda, tüm alanları dolu):

```
AKD_2021_05,AKD_2021_05_000160,37.41129,35.58044,0.0,0.0,0.3068124,5.2742534,
264.05188,0.6179377,0.46812958,0.19532657,0.272803,orta-dusuk,Tarim,predicted,
0.1917,0.3312,ORTA
```

(`rf_v1`'de aynı satır `recovery_gap_pred=0.1914, priority_score=0.2511` idi — diğer tüm alanlar hücre-sabiti olduğu için değişmedi.)

`low_severity` durumunda `recovery_gap_pred` boş bırakılır (CSV'de iki virgül yan yana), `priority_score` yine de `0.0` yazılır:

```
AKD_2021_05,AKD_2021_05_000084,37.41279,35.61438,0.0,0.0,0.11145562,4.5906806,
244.41595,1.0561873,0.2325567,0.13155854,0.10099816,dusuk,Tarim,low_severity,
,0.0,DUSUK
```

### 9.2. `manifest.json` — alanlar

Gerçek dosya (`backend-data/manifest.json`) üzerinden doğrulanmış tam alan listesi:

| Alan                                                    | Tip                 | Null? | Açıklama                                                                                                                                                                                                             | Örnek değer                                                                        |
| ------------------------------------------------------- | ------------------- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `project`                                               | string              | Hayır | Sabit `"ReGreen / FireRecover"`                                                                                                                                                                                      | `"ReGreen / FireRecover"`                                                          |
| `generated_at`                                          | datetime (ISO 8601) | Hayır | Paketin üretildiği an                                                                                                                                                                                                | `"2026-08-23T16:13:19+00:00"` (bu paket; `rf_v1`'de `"2026-08-19T17:22:55+00:00"` idi) |
| `model_version`                                         | string              | Hayır | `ModelRuns` tablosuna gider. Model değişse de şema/kolon adları AYNI kalır — bkz. §12 1.6                                                                                                                            | `"ridge_v2"` (bu paket; önceki paket `"rf_v1"` idi)                                |
| `features` *(opsiyonel)*                                | string[]            | Eksik olabilir; varsa null olamaz | Modele giren 7 özniteliğin İÇ (Türkçe) adları — sadece bilgi amaçlı, backend deserialize etmez, `alan_eslesme.json` ile API adlarına eşlenir. **`ridge_v2` ile ilk kez ortaya çıktı** — `rf_v1`'de bu key hiç yoktu, aynı `schema_version="1.1"` altında (bkz. not aşağıda) | `["agac_orani", "agac_orani_y", "dnbr", "egim_derece", "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]` |
| `model_performance` *(opsiyonel)*                       | object              | Eksik olabilir; varsa null olamaz | Model performans metrikleri — yalnızca bilgi amaçlıdır, backend deserialize etmez; elle kopyalamayı önlemek için `ridge_v2` ile eklendi. **`ridge_v2` ile ilk kez ortaya çıktı**, `rf_v1`'de yoktu. Alt alan sözleşmesi aşağıdadır. | `{"within_fire_spearman_mean": 0.686, "top20_hit_rate": 0.5281, ...}`              |
| `schema_version`                                        | string              | Hayır | Backend'in desteklediği sürüm mü kontrol edilir (şu an `"1.1"` — bu, veri paketinin şema sürümüdür, bu contract belgesinin sürümünden BAĞIMSIZDIR)                                                                   | `"1.1"`                                                                            |
| `cell_size_m`                                           | int                 | Hayır | Sabit `250`                                                                                                                                                                                                          | `250`                                                                              |
| `crs`                                                   | string              | Hayır | Sabit `"EPSG:4326"`                                                                                                                                                                                                  | `"EPSG:4326"`                                                                      |
| `fire_count`, `total_cells`                             | int                 | Hayır | 53, 37.163 — import script bunlarla kendi topladığı sayıyı çapraz kontrol eder                                                                                                                                       | `53`, `37163`                                                                      |
| `training_rows`, `training_groups`                      | int                 | Hayır | 16.074 / 27 — sadece bilgi amaçlı, backend'i ilgilendirmez                                                                                                                                                           | `16074`, `27`                                                                      |
| `priority_weights`                                      | object              | Hayır | Üst seviyede de tekrarlanır — paketin varsayılan ağırlığı (bkz. §8.2). Her yangının kendi `_metadata.json`'unda AYNI değerler tekrar var; bir yangın için hangisi kullanılacaksa `_metadata.json`'daki esas alınmalı | `{"recovery":0.5,"erosion":0.3,"access":0.2}`                                      |
| `priority_thresholds`                                   | object              | Hayır | Üst seviyede de tekrarlanır — paketin varsayılan eşiği (bkz. §6.3). Her yangının kendi `_metadata.json`'unda AYNI değerler tekrar var; bir yangın için hangisi kullanılacaksa `_metadata.json`'daki esas alınmalı    | `{"COK_YUKSEK":0.75,"YUKSEK":0.5,"ORTA":0.25}`                                     |
| `files_per_fire`                                        | string[]            | Hayır | Her yangın için beklenen 3 dosyanın adı kalıbı                                                                                                                                                                       | `["{fire_id}_hucreler.csv", "{fire_id}_sinir.geojson", "{fire_id}_metadata.json"]` |
| `fires[].fire_id`, `.fire_date`, `.province`, `.region` | —                   | Hayır | §2 ile aynı, yangın bazında tekrar                                                                                                                                                                                   | §2 ile aynı                                                                        |
| `fires[].cell_count`                                    | int                 | Hayır | §2.1                                                                                                                                                                                                                 | `1428` (AKD_2021_05)                                                               |
| `fires[].burned_area_ha`                                | float               | Hayır | §2.1                                                                                                                                                                                                                 | `8925.0` (AKD_2021_05)                                                             |
| `fires[].quality_flag`                                  | string enum         | Hayır | `"ok"` (49) veya `"check"` (4) — §10                                                                                                                                                                                 | `"ok"`                                                                             |
| `fires[].has_perimeter`                                 | bool                | Hayır | §2.1                                                                                                                                                                                                                 | `true`                                                                             |
| `fires[].status_counts`                                 | object              | Hayır | Yangın başına `predicted`/`low_severity`/`no_data` sayısı — import doğrulaması için. Değeri 0 olan durum objede hiç anahtar olarak bulunmayabilir (ör. `no_data` hiç yoksa key hiç yazılmaz)                         | `{"low_severity":978,"predicted":442,"no_data":8}`                                 |

> **Önceki sürümde eksikti:** `project`, `generated_at`, `model_version`, `schema_version`, `cell_size_m`, `crs`, üst seviye `priority_weights`/`priority_thresholds`, ve `fires[]` altındaki `fire_date`/`province`/`region`/`cell_count`/`burned_area_ha`/`has_perimeter` hiç belgelenmemişti. 1.2 ile eklendi. `features`/`model_performance` ise paketin kendisinde `ridge_v2` ile ilk kez ortaya çıktı, 1.6 ile belgelendi.
>
> **"Opsiyonel" işaretli alanlar hakkında:** Bu tablodaki `Null?` kolonu "JSON değeri `null` olabilir mi" sorusuna cevap verir, "key her zaman var mı" sorusuna değil. `features`/`model_performance`, `rf_v1` paketinde (AYNI `schema_version="1.1"` altında) hiç YOKTU — yani `schema_version="1.1"` tek başına bu iki key'in varlığını garanti etmez. Backend bu iki alanı zaten deserialize etmediği için (sadece bilgi amaçlı) pratik bir risk yok, ama Buğra ileride `schema_version`'ı yeni zorunlu alan eklerken bump etme alışkanlığı edinirse bu belirsizlik ortadan kalkar.

#### 9.2.1. `model_performance` alt alanları (`ridge_v2`)

Bu nesne bilgi amaçlıdır ve ImportTool tarafından DB'ye yazılmaz. Alanın kendisi
manifest'te bulunmayabilir; bulunduğunda aşağıdaki alt alanlar null olamaz. Oran ve
korelasyon değerleri JSON `number`, grup/sayı alanları JSON `integer` tipindedir.

| Alan | Tip | Açıklama | Kabul edilen değer/aralık | Null / missing davranışı | Bu pakette örnek |
| --- | --- | --- | --- | --- | --- |
| `metric_note` | string | Ölçümlerin nasıl üretildiğini açıklayan insan-okur not | Boş olmayan metin | `model_performance` varsa zorunlu, null olamaz | `"Butun degerler out-of-fold: ..."` |
| `group_count` | int | Değerlendirilen yangın/mekânsal grup sayısı | `>= 1` | Zorunlu, null olamaz | `27` |
| `within_fire_spearman_mean` | float | Grup içi Spearman korelasyonlarının ortalaması | `-1 <= x <= 1` | Zorunlu, null olamaz | `0.686` |
| `within_fire_spearman_median` | float | Grup içi Spearman korelasyonlarının medyanı | `-1 <= x <= 1` | Zorunlu, null olamaz | `0.7438` |
| `positive_groups` | int | Spearman değeri pozitif olan grup sayısı | `0 <= x <= group_count` | Zorunlu, null olamaz | `27` |
| `worst_group` | float | En düşük grup içi Spearman değeri | `-1 <= x <= 1`; `worst_group <= best_group` | Zorunlu, null olamaz | `0.1401` |
| `best_group` | float | En yüksek grup içi Spearman değeri | `-1 <= x <= 1`; `best_group >= worst_group` | Zorunlu, null olamaz | `0.8788` |
| `top20_hit_rate` | float | Modelin en yüksek %20 tahminindeki gerçek yüksek değer isabet oranı | `0 <= x <= 1` | Zorunlu, null olamaz | `0.5281` |
| `top20_hit_rate_dnbr_baseline` | float | Aynı ölçümün dNBR referans yöntemi sonucu | `0 <= x <= 1` | Zorunlu, null olamaz | `0.3879` |
| `top20_hit_rate_random` | float | Aynı ölçümün rastgele seçim referansı | `0 <= x <= 1` | Zorunlu, null olamaz | `0.2` |
| `pairwise_accuracy` | float | İkili hücre sıralamalarında doğru yön oranı | `0 <= x <= 1` | Zorunlu, null olamaz | `0.7583` |

`features` alanı mevcutsa bu paket için tam olarak yedi benzersiz, null olmayan
string taşır; sırası model eğitimindeki kolon sırasıdır. Bu alanların eklenmesi
ImportTool girdisini veya DB şemasını değiştirmez.

### 9.3. `{fire_id}_metadata.json` — alanlar

Gerçek dosyalar (`AKD_2021_05_metadata.json` — `ok`, `DAN_2021_01_metadata.json` — `check`) üzerinden doğrulanmış tam alan listesi:

| Alan                                         | Tip                 | Null?    | Açıklama                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | Kabul edilen değer / aralık                                                   | Örnek değer                                                                                                        |
| -------------------------------------------- | ------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `fire_id`, `fire_date`, `province`, `region` | —                   | Hayır    | §2 ile aynı                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | §2 ile aynı                                                                   | §2 ile aynı                                                                                                        |
| `modis_area_ha`                              | float               | Hayır    | §2.1                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | Gözlenen: 315 – 57.716 ha                                                     | `3768` (AKD_2021_05)                                                                                               |
| `cell_size_m`                                | int                 | Hayır    | Sabit `250`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | Sabit `250`                                                                   | `250`                                                                                                              |
| `cell_count`                                 | int                 | Hayır    | §2.1                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | 37 – 9048 (bu pakette gözlenen)                                               | `1428` (AKD_2021_05)                                                                                               |
| `burned_area_ha`                             | float               | Hayır    | §2.1                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | —                                                                             | `8925.0` (AKD_2021_05)                                                                                             |
| `crs`                                        | string              | Hayır    | Sabit `"EPSG:4326"`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  | Sabit `"EPSG:4326"`                                                           | `"EPSG:4326"`                                                                                                      |
| `schema_version`                             | string              | Hayır    | `ModelRuns` tablosuna gider. Backend'in desteklediği sürümle karşılaştırılır                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | Bu pakette sabit `"1.1"`                                                      | `"1.1"`                                                                                                            |
| `model_version`                              | string              | Hayır    | `ModelRuns` tablosuna gider                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | Bu pakette 53 dosyanın 53'ünde de `"ridge_v2"` (önceki paket: `"rf_v1"`)      | `"ridge_v2"`                                                                                                       |
| `generated_at`                               | datetime (ISO 8601) | Hayır    | `ModelRuns` tablosuna gider                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | ISO 8601, UTC offsetli                                                        | `"2026-08-23T16:13:19+00:00"`                                                                                      |
| `quality_flag`                               | string enum         | Hayır    | `"ok"` veya `"check"` — §10                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | `"ok"` (49) / `"check"` (4)                                                   | `"ok"`                                                                                                             |
| `quality_note`                               | string              | **Evet** | Çoğunlukla sadece `check` iken dolu, ama **istisna doğrulandı**: `AKD_2021_11` metadata'sında `quality_flag: "ok"` olduğu halde `quality_note: "etiketli hucre az (9)"` dolu geliyor. Yani "not doluysa check'tir" varsayımı YANLIŞ — backend panel/liste mantığını `quality_flag`'e göre kursun, `quality_note`'un dolu/boş olmasına göre değil                                                                                                                                                                                                                                                     | —                                                                             | `"alan uyusmuyor (MODIS 552 ha, biz 4,437 ha)"` (DAN_2021_01, check) / `"etiketli hucre az (9)"` (AKD_2021_11, ok) |
| `in_training_set`                            | bool                | Hayır    | Bu yangın modelin eğitim setinde miydi (34 yangın `true`, 19 yangın `false` — 53 dosyanın tamamı tarandı). **Önceki sürümde hiç belgelenmemişti.** `true` ise bu yangında **bir kısım** hücre için tahmin **kat dışı (out-of-fold)** üretilmiştir — modelin o hücreleri görmemiş versiyonundan (`GroupKFold`, mekânsal grup dışarıda bırakılarak). `false` ise tüm veriyle eğitilmiş modelden, doğrudan dış örnek tahmini. ⚠️ Kaç hücrenin kat-dışı olduğu bu alandan DEĞİL, `out_of_fold_cells` alanından okunur — `true` olması "TÜM predicted hücreler OOF" anlamına gelmez (bkz. altındaki alan) | `true` / `false`                                                              | `true`                                                                                                             |
| `out_of_fold_cells`                          | int                 | Hayır    | **Kat-dışı (out-of-fold) tahmin üretilen eğitim hücresi/satırı sayısıdır** — bu, `prediction_status == "predicted"` sayısıyla AYNI METRİK DEĞİLDİR ve birebir eşit olması garanti edilmez (53 dosyanın tamamı tarandı, 2 istisna: `AKD_2021_04`'te OOF **762** > predicted **759**; `EGE_2019_03`'te OOF **39** < predicted **46**). `in_training_set = false` ise her zaman `0`. 53 yangının `out_of_fold_cells` toplamı `manifest.json`'daki `training_rows` (16.074) ile birebir tutuyor — doğrulandı. **Önceki sürümde hiç belgelenmemişti**                                                     | 0 – 4917 (gözlenen; `in_training_set=false` ise her zaman `0`)                | `762` (AKD_2021_04)                                                                                                |
| `status_counts`                              | object              | Hayır    | §9.2 ile aynı yapı, bu yangına özel                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  | Key'ler: `predicted`/`low_severity`/`no_data` (0 olan key hiç yazılmayabilir) | `{"low_severity":978,"predicted":442,"no_data":8}`                                                                 |
| `priority_weights`                           | object              | Hayır    | Key'ler: `recovery`, `erosion`, `access` (§8.2)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | Toplamı 1'e normalize edilecek şekilde kullanılır                             | `{"recovery":0.5,"erosion":0.3,"access":0.2}`                                                                      |
| `priority_thresholds`                        | object              | Hayır    | Key'ler: `COK_YUKSEK`, `YUKSEK`, `ORTA` (§6.3)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | 0–1 arası, artan sırada                                                       | `{"COK_YUKSEK":0.75,"YUKSEK":0.5,"ORTA":0.25}`                                                                     |
| `normalization_reference`                    | object              | Hayır    | §8.3 — backend'in slider hesabı için ZORUNLU. Key'ler: `recovery_gap_pred`, `slope_deg`, `road_distance_km`, her biri `{min, max}`                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Her `{min,max}` çifti kendi alanının gözlenen aralığına yakın olmalı          | §8.3'teki `AKD_2021_05` örneği                                                                                     |
| `has_perimeter`                              | bool                | Hayır    | §2.1                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | 53/53 `true` (bu pakette)                                                     | `true`                                                                                                             |

> **Önceki sürümde eksikti:** `modis_area_ha`, `cell_size_m`, `cell_count`, `burned_area_ha`, `crs`, `in_training_set`, `out_of_fold_cells`, `has_perimeter`, `status_counts` bu tabloda hiç yer almıyordu — sadece `model_version`, `generated_at`, `schema_version`, `normalization_reference`, `priority_weights`, `priority_thresholds`, `quality_flag`, `quality_note` vardı. 1.2 ile tam alan listesine tamamlandı. `in_training_set` / `out_of_fold_cells` özellikle önemli: bunlar olmadan backend, bir tahminin kat-dışı mı yoksa doğrudan mı üretildiğini ayırt edemez (bkz. yukarıdaki açıklama).

### 9.4. `{fire_id}_hukumler.csv` — sütun sırası

**Kaynak:** [`docs/hukum_sozlesmesi.md`](./hukum_sozlesmesi.md) — AI ekibinin ayrı, opsiyonel "hüküm katmanı" teslimatı. `{fire_id}_hucreler.csv`/`manifest.json`/`{fire_id}_metadata.json`'a DOKUNMAZ, tamamen yeni bir yan dosya; `manifest.json`'un `files_per_fire` listesinde BİLEREK yok.

```
cell_id, hukum, ek_kosullar, toparlanma_orani, tur_onerisi, tetikleyen, ozet,
ayrinti, zamanlama_notu_var
```

9 sütun, sabit sıra. `cell_id` üzerinden `{fire_id}_hucreler.csv` ile **BİREBİR (1:1)** eşleşir — her hücrenin tam olarak bir hüküm satırı vardır, ne eksik ne fazla.

| Alan | Tip | Null? | Açıklama | Kabul edilen değer / aralık | Örnek değer |
| --- | --- | --- | --- | --- | --- |
| `cell_id` | string | Hayır | Birleştirme anahtarı, §2 ile aynı kalıp | §2 ile aynı | `AKD_2021_01_020514` |
| `hukum` | string enum | Hayır | Kural motorunun sıralı kararı — **hüküm, `priority_score`'dan BAĞIMSIZDIR**: ağırlık değişse bile hüküm değişmez (bkz. docs/hukum_sozlesmesi.md "hüküm ≠ öncelik") | 7 değer: `KAPSAM_DISI`, `SAHA_KONTROL`, `IZLE`, `EROZYON_ONCE`, `DIKIM_ADAYI`, `ONCELIGE_GORE`, `GENCLESME_IZLE` | `EROZYON_ONCE` |
| `ek_kosullar` | string | **Evet** | `\|` ile ayrılmış 0..6 kod, hükmün üstüne biner | `ERISIM_ZOR`, `ESKIDEN_ORMAN_DEGIL`, `SEYREK_ORTU`, `DIK_YAMAC`, `AGIR_YANMIS`, `DUSUK_GUVEN` (herhangi bir birleşimi, veya boş) | `AGIR_YANMIS\|DIK_YAMAC` |
| `toparlanma_orani` | float | **Evet** | `(ndvi_before - recovery_gap_pred) / ndvi_before` — yangın öncesi örtünün tahmini geri gelen oranı | 0–1 | `0.2727` |
| `tur_onerisi` | string | **Evet** | Virgüllü tür listesi — **sistemin çıkarımı DEĞİL**, dışarıdan verilen bir yetişme-ortamı tablosundan okunuyor. `hukum_sozlugu.json → tur_tablosu.onaylandi = false` olduğu sürece ÖRNEK/onaysız veridir, UI kaynak notu olmadan göstermemeli | Serbest metin veya boş | `"kızılçam, fıstıkçamı"` |
| `tetikleyen` | string | Hayır | Denetim izi — hükmü hangi ölçümün/eşiğin tetiklediği, her zaman dolu | Serbest metin | `"toparlanma=0.27 egim=36.0>=25 <0.35"` |
| `ozet` | string | Hayır | Panelin üstünde gösterilecek 1-2 cümlelik hüküm metni, her zaman dolu | Serbest metin (Türkçe) | `"Önce erozyon kontrolü. Toparlanma tahmini zayıf ve eğim 36,0°..."` |
| `ayrinti` | string | Hayır | Detay bölümündeki uzun gerekçe, her zaman dolu | Serbest metin (Türkçe) | `"Modelin tahminine göre yangın öncesi örtünün ancak %27 kadarı..."` |
| `zamanlama_notu_var` | bool | Hayır | `true` ise `hukum_sozlugu.json → zamanlama_notu` sabit dipnotu bu hücrede gösterilmeli — metin hücre başına TEKRARLANMAZ | `True` / `False` | `True` |

**Doğrulama (ImportTool, bkz. docs/import-flow.md §3.8):** `hukum` 7 bilinen kodun dışına çıkarsa, `ek_kosullar`'daki herhangi bir kod 6 bilinen kodun dışına çıkarsa veya `toparlanma_orani` `0..1` aralığı dışındaysa yangın reddedilir (`HUKUM_INVALID_VALUE`). `cell_id` kümesi `_hucreler.csv`'ninkiyle birebir eşleşmezse (fazla/eksik/tekrarlı) `HUKUM_CELL_MISMATCH`.

### 9.5. `hukum_sozlugu.json` / `yangin_ozetleri.json` / `yangin_metinleri.json`

Üçü de `{fire_id}_hukumler.csv`'nin AKSİNE **yangın başına değil, paket genelinde** bulunur (manifest'in yanında, tek dosya) ve import sırasında yangın döngüsünden ÖNCE bir kez okunur (bkz. docs/import-flow.md §3.8). Detaylı alan sözleşmesi için AI ekibinin kendi belgesi [`docs/hukum_sozlesmesi.md`](./hukum_sozlesmesi.md) tek doğru kaynaktır — burada sadece backend'in bu dosyaları nasıl gördüğü özetlenir:

| Dosya | Kapsam | Backend nasıl saklar |
| --- | --- | --- |
| `hukum_sozlugu.json` | **GLOBAL**, yangına özgü değil (~2,6 KB) — 7 hükmün başlığı/sırası, 6 ek koşulun şablon cümlesi, `zamanlama_notu` sabit metni, eşikler (`esikler`), tür tablosunun onay bayrağı (`tur_tablosu.onaylandi`) | `HukumSozlugu` tablosunda TEK global satır, ham JSON olarak (`JsonIcerik`) — alan alan modellenmez, `GET /api/hukum-sozlugu` ile aynen geçirilir |
| `yangin_ozetleri.json` | `yanginlar[fire_id]` ile anahtarlanmış, HER yangın için bir "sayı bloğu" (büyüklük, şiddet/arazi/hüküm dağılımı, erişim, güven — bkz. hukum_sozlesmesi.md "Katman 1") | Her yangının kendi bloğu `FireNarratives.SayiBlogu`'na ham JSON olarak verbatim yazılır (`Fires.PerimeterGeoJson` ile AYNI "opak blob" deseni) |
| `yangin_metinleri.json` | `yanginlar[fire_id]` ile anahtarlanmış, HER yangın için `{paragraf, profil, kaynak, onaylandi}` + dosyanın ÜST seviyesinde paket-geneli `uretim` alanı | `paragraf`/`profil`/`onaylandi` → `FireNarratives`'in aynı adlı kolonları; üst seviye `uretim` her yangının `FireNarratives.Uretim`'ine kopyalanır (`kaynak` alanı ayrıca saklanmaz, `uretim` ile kavramsal olarak örtüşür) |

**Opsiyonellik:** Üçü de manifest'in yanında bulunmayabilir — bulunmazlarsa ilgili tablo(lar) hiç yazılmaz, import hata vermez (backward-compatible no-op). `yangin_ozetleri.json`/`yangin_metinleri.json`'dan sadece biri varsa da aynı şekilde: o yangın için anlatı atlanır.

---

## 10. Kalite Bayrakları

53 yangının 49'u `ok`, 4'ü `check`. `check` olanların hepsinde `quality_note` dolu ve açıklayıcı (ör. `"alan uyusmuyor (MODIS 552 ha, biz 4,437 ha)"` — `DAN_2021_01`). `check` bayraklı 4 yangın: `DAN_2021_01`, `GDA_2021_01`, `IAN_2023_01`, `IAN_2024_01`.

- `/api/fires` listesinde TÜM 53 yangın gösterilir, `check` olanlar filtrelenmez — sadece görsel olarak işaretlenir.
- Kullanıcı isterse `check` olanları filtreleyebilir ama varsayılan görünüm hepsini gösterir.

---

## 11. DB Şeması Özeti (güncel referans: db-schema v1.2; başlangıç: Teknik Mimari v4.1 §7)

| Tablo         | İçerik                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Fires`       | Yangın kimliği, perimeter, marker (`representative_point`/`InteriorPoint`), quality flag/note, `modis_area_ha`/`burned_area_ha` ve `has_perimeter` (§2.1). PDF v4.1'de eksik olan `HasPerimeter`, güncel `docs/db-schema.md` v1.2 ve `backend/db/schema.sql` içinde eklenmiştir; mevcut paket 53/53 perimeter içerdiği için şema bunu `CHECK (HasPerimeter = 1)` ve `PerimeterGeoJson NOT NULL` ile sınırlar |
| `Cells`       | Hücre merkezi + TÜM sabit özellikler (§3, §4) — CSV silinse bile veri kaybolmaz                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `ModelRuns`   | **Her yangın + teslimat kombinasyonu bir satır.** `normalization_reference` yangına özel olduğu için tek AI teslimatı 53 ayrı satır üretir; `ModelVersion`+`GeneratedAt` bunları aynı teslimata bağlar. PDF v4.1'de eksik olan `InTrainingSet`/`OutOfFoldCells`, güncel `docs/db-schema.md` v1.2 ve `backend/db/schema.sql` içinde eklenmiştir. `UNIQUE(FireId, ModelVersion, GeneratedAt)` aynı yangın teslimatının çoğalmasını engeller |
| `Predictions` | KATMAN 1 çıktısı (`prediction_status`, `recovery_gap_pred`) + AI'ın varsayılan priority'si. Insert-only                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `CellVerdicts` | Hüküm katmanı (§9.4), `(CellId, ModelRunId, HukumSozluguSurum)` başına bir satır. Ağırlık kaydırıcısından bağımsız; model ve hüküm motoru sürümüne bağlıdır |
| `FireNarratives` | Hüküm katmanı (§9.5), `(ModelRunId, NarrativeVersion)` başına opsiyonel yangın özeti + ham sayı bloğu |
| `HukumSozlugu` | Hüküm katmanı (§9.5), sürüm başına global sözlük; eski sürümler korunur |

`ModelRuns` ve `Predictions` üzerinde `UNIQUE` kısıtları vardır (aynı paket iki kez import edilirse veri çoğalmaz). `CellVerdicts`/`FireNarratives`/`HukumSozlugu` de aynı insert-or-verify felsefesini izler (bkz. docs/db-schema.md §9, docs/import-flow.md §3.8/§4).

---

## 12. Değişiklik Günlüğü

| Sürüm | Değişiklik                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.0   | İlk taslak (örnek veriyle, 500m varsayımı, `recovery_score`/`recovery_class` adları)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 1.1   | Gerçek 53 yangın/37.163 hücre teslimatıyla doğrulandı. 250m, `cell_id`, `recovery_gap_pred`/`priority_class`, `normalization_reference`, `quality_flag`, Polygon/MultiPolygon ayrımı, `ndvi_drop` üç rollü tanım — bu belge                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 1.2   | `sample-data/` paketindeki 53 yangının TAMAMI (37.163 satır) script ile yeniden tarandı ve bu belge güncel gerçek veriyle hizalandı: `severity_class` gerçekte 4 değerli (`dusuk`/`orta-dusuk`/`orta-yuksek`/`yuksek`, önceki varsayım 3 değerliydi), `land_cover` enum'u (7 değer) ilk kez listelendi, `modis_area_ha` fire-düzeyi alanı olarak ilk kez tanımlandı (§2.1 yeni), `manifest.json`/`{fire_id}_metadata.json` alan tabloları eksiksiz hale getirildi (`in_training_set`, `out_of_fold_cells`, `cell_size_m`, `cell_count`, `burned_area_ha`, `crs`, `has_perimeter`, `status_counts`, üst seviye `project` dahil), `priority_weights`/`priority_thresholds` JSON key isimleri (`recovery`/`erosion`/`access`, `COK_YUKSEK`/`YUKSEK`/`ORTA`) ilk kez yazıldı, tüm sayısal alanlara gerçek veriden ölçülmüş "Kabul edilen değer/aralık" ve "Örnek değer" kolonları eklendi. Önceki `docs/data-contract.md` (taslak sürüm) bu belgeyle değiştirildi — artık tek ve bağlayıcı kaynak budur.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 1.3   | Teknik Mimari Planı v4.1 (PDF) ile çapraz okuma sırasında bulunan 3 gerçek hata düzeltildi: (1) §11'deki "Bir AI teslimatı = bir satır" ifadesi yanlıştı — PDF'nin gerçek DDL'i (`ModelRuns.FireId` FK + `UNIQUE(FireId, ModelVersion, GeneratedAt)`) yangın başına bir `ModelRuns` satırı üretir, `normalization_reference` fire'a özel olduğu için bu doğru tasarımdır; ifade buna göre düzeltildi. (2) `modis_area_ha` "gözlenen aralık" değeri 53 dosyanın TAMAMI script ile taranarak yeniden ölçüldü: önceki `~35–60.000` yanlıştı, gerçek değer **315–57.716 ha**. (3) `quality_note`'un "sadece `check` iken dolu" olduğu varsayımı yanlıştı — `AKD_2021_11` istisnası (`quality_flag: "ok"` iken `quality_note` dolu) 53 dosyanın tamamı taranarak doğrulandı, açıklama düzeltildi. Bu üç madde PDF v4.1'in mimari tasarımını değiştirmez, sadece bu belgenin PDF'i yanlış özetlediği yerleri düzeltir. **Not:** Aynı çapraz okumada PDF v4.1'in kendisinde de gerçek boşluklar bulundu (`ModelRuns` DDL'inde `in_training_set`/`out_of_fold_cells` kolonlarının hiç olmaması, bazı DB kolonlarının gerçek veriye göre gereksiz `NULL`/nullable olması, PDF'nin §3.7 ile §8'inin "geçersiz `recovery_gap_pred`'i reddet mi uyar mı" konusunda birbiriyle çelişmesi, `/cells` endpoint'inin her ağırlık değişiminde tam hücre geometrisini yeniden döndürmesi) — bunlar PDF'nin kendi revizyonunu (v4.2) gerektirir, bu belgenin kapsamı dışındadır.                                                                                                                                                                                                                                                                                                                                            |
| 1.4   | İkinci çapraz okuma turunda 4 madde netleştirildi: (1) §2.1'in giriş cümlesi "üç dosyada da birebir aynı şekilde tekrarlanır" gibi okunuyordu — yanlıştı, çünkü `modis_area_ha` manifest'te YOK, `burned_area_ha`/`cell_count`/`has_perimeter` ise geojson'da YOK; cümle tablo ile tutarlı hale getirildi (tablonun kendisi zaten doğruydu). (2) §11'deki `Fires` ve `ModelRuns` satırları, PDF v4.1'in MEVCUT DDL'i ile bu belgenin HEDEFLEDİĞİ (v4.2) alanları karıştırıyordu — `has_perimeter` ve `in_training_set`/`out_of_fold_cells` şu an PDF DDL'inde YOK, açıkça "v4.2 ile eklenecek hedef alan" olarak etiketlendi; `modis_area_ha`/`burned_area_ha` ise zaten mevcut DDL'de olduğu için ayrıca belirtildi. (3) `in_training_set` alanının açıklaması fazla geniş konuşuyordu ("o yangının hücreleri için tahmin kat dışı üretilmiştir") — 53 dosyanın tamamı tarandı, `in_training_set=true` olan yangınlarda `out_of_fold_cells` çoğunlukla `predicted` sayısına eşit ama HER ZAMAN değil (2 istisna: `AKD_2021_04` 762/759, `EGE_2019_03` 39/46); açıklama bu kesinliğe göre düzeltildi. (4) §9.2 (`manifest.json`) tablosuna `Null?`/`Örnek değer`, §9.3 (`metadata.json`) tablosuna `Kabul edilen değer/aralık`/`Örnek değer` kolonları eklenerek §2/§3/§4 ile aynı formata getirildi. **Son doğrulama turu (aynı 1.4 içinde, henüz yayımlanmadığı için ayrı sürüm açılmadı):** `out_of_fold_cells` açıklamasındaki "kaç `predicted` hücrenin OOF olduğunu KESİN belirtir" ifadesi fazla iddialıydı (OOF sayısı predicted'i geçebiliyor, ör. `AKD_2021_04`) — "kat-dışı tahmin üretilen eğitim hücresi/satırı sayısı, `predicted` ile birebir eşit olması garanti değil" şeklinde kesinleştirildi; §9.3'teki `quality_note` satırında `Null?`/`Açıklama` hücreleri arasında eksik olan ayırıcı karakter eklenerek bozuk kolon hizası düzeltildi; §9.2'de `priority_weights`/`priority_thresholds` tek satırda birleşikken her biri kendi doğru örneğiyle ayrı satıra bölündü; `files_per_fire` örneği geçersiz `["...", ...]` yerine gerçek manifest'teki 3 elemanlı geçerli JSON diziyle değiştirildi. |
| 1.5   | Güncel DB/import hizalaması: `Fires.HasPerimeter`, `ModelRuns.InTrainingSet` ve `ModelRuns.OutOfFoldCells` artık güncel şemada uygulanmış alanlar olarak §11'e işlendi; bağlayıcı import davranışı `docs/import-flow.md` v1.1'e taşındı. |
| 1.6   | Model `rf_v1` → `ridge_v2` teslimatıyla hizalandı (şema/kolon adları AYNI kaldı, sadece değerler ve algoritma değişti). §3 başlığı "6 Öznitelik (Random Forest)" → "7 Öznitelik (Ridge Regression)"; `ndvi_drop` artık modele girdiği için §4'ten §3'e taşındı, rol açıklaması güncellendi. §6'daki "Random Forest" referansları genel "model" ifadesine çevrildi (algoritma `ModelRuns.ModelVersion`'a bağlı, koda gömülü değil). §6.4'teki `recovery_gap_pred` (0,1187–0,576 → 0,11–0,6546) ve `priority_score` (0,0–0,9683 → 0,0–0,9754) aralıkları 53 yangının 37.163 satırı yeniden taranarak güncellendi — diğer 6 öznitelik (`tree_cover` .. `ndvi_drop`) hücre-sabiti olduğu için DEĞİŞMEDİ, bu da doğrulandı. §9.2'ye `ridge_v2` ile ilk kez ortaya çıkan `features` (iç öznitelik adları) ve `model_performance` (Spearman/top-20 isabet metrikleri, elle kopyalamayı önlemek için eklenmiş) alanları eklendi; `model_version` örnek değerleri §9.2/§9.3'te `ridge_v2`'ye güncellendi. Ayrıca API katmanında `CellsResponseDto`'ya `normalization_reference`, `model_version` ve `priority_thresholds` eklendi (ilki Buğra'nın somut isteğiydi — frontend'in öncelik skorunu 3 bileşene ayırarak göstermesi için gerekliydi; diğer ikisi aynı gözden geçirmede eklenen tamamlayıcı alanlar — `priority_thresholds` olmadan frontend `priority_class` renklendirmesi için `{fire_id}_metadata.json`'a ayrıca erişmek zorunda kalıyordu). Veri zaten `ModelRun`'da vardı, sadece response'a taşındı — bkz. `docs/api-contract.md` §4.5. `normalization_reference`'ın `min`/`max`'ı artık API'de HER ZAMAN sayı: `FireValidator` §3.3.4'e null-reddi eklendi (önceden `FireImporter` null'ı sessizce `0`'a çeviriyordu, DB kolonları zaten NOT NULL olduğu için "gerçek 0" ile "referans yok" ayrımı kayboluyordu — bkz. `ReGreen.Core.Priority.NormRange` XML doc'u), API katmanı da ayrı non-nullable `NormalizationReferenceDto` tipiyle bunu yansıtıyor. |

| 1.6 (devam) | İkinci bir gözden geçirme turunda ek gerçek hatalar bulundu ve düzeltildi: `docs/api-contract.md` §4.5 ile `data-contract.md` §7/§8.3/§9.2 örneklerindeki eski `rf_v1` sayıları güncellendi; donmuş PriorityCalculator test örnekleri gerçek `ridge_v2` değerleriyle değiştirildi; `OKUBENI.md`, üç `alan_eslesme.json` kopyası, frontend manifest toplamı ve `HUCRE_PANELI.md` hizalandı. Son kontrolde frontend bileşen formülü backend ile aynı clamp/degenerate-range kurallarına getirildi, API için `applied_weights` kullanımı açıklandı, `model_performance` alt alanları şemalandırıldı ve yeni response alanları OpenAPI regresyon testiyle korumaya alındı. |
| 1.7 | Hüküm katmanı eklendi (docs/hukum_sozlesmesi.md, AI ekibinin ayrı opsiyonel yan-teslimatı): `{fire_id}_hukumler.csv` sütun sözleşmesi (§9.4, yeni) ve `hukum_sozlugu.json`/`yangin_ozetleri.json`/`yangin_metinleri.json`'ın backend'de nasıl saklandığının özeti (§9.5, yeni) eklendi; §11 DB şeması özetine `CellVerdicts`/`FireNarratives`/`HukumSozlugu` satırları eklendi. Mevcut hiçbir alan/tablo/enum değişmedi — tamamen katmanlı ekleme. |

---

## 13. Referans Dosyalar

- `sample-data/backend-data/alan_eslesme.json` — iç isim ↔ API isim eşleşmesi (Türkçe kaynak)
- `sample-data/backend-data/oncelik.py` — öncelik formülünün referans Python uygulaması
- `sample-data/backend-data/OKUBENI.md` — AI teslim paketinin okuma kılavuzu
- [`docs/hukum_sozlesmesi.md`](./hukum_sozlesmesi.md) — hüküm katmanının (§9.4/§9.5) veri sözleşmesi ve semantiği
- `sample-data/frontend-data/HUCRE_PANELI.md` — hücre detay paneli spesifikasyonu
- `sample-data/README.md` — backend-data / frontend-data klasör ayrımı ve sürüm politikası
- Teknik Mimari Planı v4.1 (PDF) — DB şeması, API tasarımı, import akışı, frontend renklendirme kuralları
