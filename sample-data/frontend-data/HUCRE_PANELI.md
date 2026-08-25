# Hücre Detay Paneli — İçerik Spesifikasyonu

Haritada bir hücreye tıklanınca açılacak panelin ne göstereceği.
Bütün alanlar `{fire_id}_hucreler.csv` ve `{fire_id}_metadata.json` içinde var.

> **Şema sürümü:** 1.1 · **Hücre boyutu:** 250 m × 250 m · **CRS:** EPSG:4326

---

## Önce: üç farklı hücre durumu var

`prediction_status` alanı üç değer alabiliyor ve **her biri farklı panel ister.**
Hepsini aynı şekilde göstermek sistemin en önemli özelliğini bozar.

| Durum | Ne demek | Panel |
|---|---|---|
| `predicted` | Model bu hücre için tahmin üretti | **Tam panel** |
| `low_severity` | Yeterince yanmamış, önceliklendirmeye girmiyor | Kısa panel |
| `no_data` | Girdi eksik, tahmin yok | Uyarı paneli |

Örnek dağılım (Manavgat, AKD_2021_01): 4.917 `predicted` · 4.130 `low_severity` · 1 `no_data`

---

# 1. `predicted` — tam panel

## Bölüm A · Başlık

```
┌─────────────────────────────────────────┐
│  ÇOK YÜKSEK ÖNCELİK          0,81       │
│  Bu yangındaki 4.917 hücre içinde 12.   │
└─────────────────────────────────────────┘
```

| Gösterilecek | Alan | Not |
|---|---|---|
| Öncelik sınıfı | `priority_class` | `COK_YUKSEK` / `YUKSEK` / `ORTA` / `DUSUK` |
| Skor | `priority_score` | 0–1, iki ondalık yeter |
| Sıra | *hesapla* | Aynı yangının `predicted` hücreleri içinde skora göre sıra |

Sınıf renkleri `metadata.priority_thresholds`'tan gelsin, **koda gömme**:
`COK_YUKSEK ≥ 0,75` · `YUKSEK ≥ 0,50` · `ORTA ≥ 0,25` · altı `DUSUK`

## Bölüm B · NEDEN bu öncelik? ⭐ *panelin en önemli kısmı*

Ürünün adı "karar destek" — kullanıcı skorun **nereden geldiğini** görmeli.
Skoru üç bileşene ayır ve bar olarak göster:

```
İyileşme açığı    ████████████████░░░░  %53   (0,429)
Erozyon riski     ████████░░░░░░░░░░░░  %27   (0,222)
Ulaşılabilirlik   ██████░░░░░░░░░░░░░░  %20   (0,162)
```

### ⚠️ Veri iki yoldan gelebilir — hesap değişiyor

**Yol 1 — Bu klasördeki dosyalar (mock/geliştirme)**
`{fire_id}_metadata.json` içinde `normalization_reference` var, aşağıdaki
formülle kırılımı kendin hesaplayabilirsin.

**Yol 2 — Backend API (`GET /api/fires/{fireId}/cells`)**
API `normalization_reference` **döndürmüyor** (bkz. `ReGreen.Api/Dtos/CellDtos.cs`,
`CellsResponseDto`). Yani API'den gelen veriyle bu kırılımı hesaplayamazsın.

API'nin yaptığı şey farklı: ağırlıkları query parametresi olarak alıp
(`?recovery=0.6&erosion=0.2&access=0.2`) `priority_score` ve `priority_class`'ı
**sunucu tarafında yeniden hesaplıyor** ve `applied_weights` alanını döndürüyor.
Bu iyi bir tasarım — normalizasyon referansı sabit kalıyor, sadece ağırlık
değişiyor.

**Ama "neden bu öncelik" panelinde üç bileşeni ayrı ayrı gösterebilmek için
`normalization_reference` gerekiyor.** İki çözüm var:

1. Backend `CellsResponseDto`'ya `normalization_reference` eklesin
   (üç alan: `recovery_gap_pred`, `slope_deg`, `road_distance_km` için min/max).
   Zaten DB'de `ModelRuns`/`Fires` tarafında duruyor, sadece cevaba eklenmesi
   gerekiyor. **Önerilen çözüm bu.**
2. Ya da bileşen kırılımı gösterilmez, sadece toplam skor gösterilir.
   Ürünün en değerli kısmını kaybettirir, tercih edilmez.

> Bu, Buğra ile Beytullah arasında konuşulacak bir madde. Zeynep'in
> paneli tasarlarken 1. seçeneğe göre ilerlemesi doğru olur.

### Hesabı (formül sabit)

```js
const ref = metadata.normalization_reference
const w   = metadata.priority_weights

const n = (v, k) => {
  const {min, max} = ref[k]
  return max > min ? (v - min) / (max - min) : 0
}

const iyilesme = w.recovery * n(cell.recovery_gap_pred, 'recovery_gap_pred')
const erozyon  = w.erosion  * n(cell.slope_deg,         'slope_deg')
const ulasim   = w.access   * (1 - n(cell.road_distance_km, 'road_distance_km'))

// iyilesme + erozyon + ulasim === cell.priority_score
```

**Doğrulanmış örnek** (AKD_2021_01_032026):

| Bileşen | Ham değer | Normalize | Ağırlık | Katkı |
|---|---|---|---|---|
| İyileşme açığı | 0,4826 | 0,858 | ×0,50 | **0,429** |
| Erozyon (eğim) | 37,27° | 0,738 | ×0,30 | **0,222** |
| Ulaşılabilirlik | 0,556 km | 0,189 | ×0,20 *(ters)* | **0,162** |
| | | | **Toplam** | **0,8129** |

Dosyadaki `priority_score` = 0,8129. Birebir tutuyor.

> ⚠️ **`normalization_reference`'ı metadata'dan oku, kendin hesaplama.**
> Kullanıcı haritada bir bölgeyi filtrelerse ve sen min/max'ı görünen
> hücrelerden hesaplarsan skorlar değişir, sıralama bozulur.

> ⚠️ **Ulaşılabilirlik terside:** yola yakın olmak **iyi**, o yüzden `1 − n()`.

## Bölüm C · Ölçümler

```
Tahmini iyileşme açığı   0,48        ← yüksek = kötü
Yangın şiddeti           yüksek      (dNBR 0,75)
Eğim                     37°
Yükselti                 318 m
Ağaç örtüsü              %100
Yola mesafe              0,56 km
Arazi tipi               Ağaçlık
```

| Etiket | Alan | Biçim |
|---|---|---|
| Tahmini iyileşme açığı | `recovery_gap_pred` | 2 ondalık |
| Yangın şiddeti | `severity_class` + `burn_severity_dnbr` | metin + parantezde sayı |
| Eğim | `slope_deg` | tam sayı + `°` |
| Yükselti | `elevation_m` | tam sayı + ` m` |
| Ağaç örtüsü | `tree_cover_annual` | yüzde |
| Yola mesafe | `road_distance_km` | 2 ondalık + ` km` |
| Arazi tipi | `land_cover` | metin |

> 🔴 **YÖN UYARISI:** `recovery_gap_pred` **YÜKSEK = KÖTÜ** (eski haline
> dönememiş). Tersine çevirip "iyileşme skoru" diye gösterme, harita ters çıkar.
> Renk skalası da buna göre: yüksek değer = kırmızı.

## Bölüm D · Bitki örtüsü değişimi

```
Yangın öncesi NDVI   0,70   ████████████████████
Yangın sonrası NDVI  0,25   ███████
                            ↓ %64 kayıp
```

`ndvi_before`, `ndvi_after`, `ndvi_drop`. Basit bir çubuk ya da mini grafik.
Kullanıcıya "burada gerçekten ne oldu" hissini veren kısım burası.

## Bölüm E · Teknik (katlanabilir)

`cell_id` · `lat` / `lon` · `fire_id` · alan 6,25 ha (250 m × 250 m sabit) ·
`metadata.model_version` · `metadata.generated_at`

---

# 2. `low_severity` — kısa panel

```
┌─────────────────────────────────────────┐
│  Önceliklendirilmedi                    │
│  Yangın şiddeti eşiğin altında          │
└─────────────────────────────────────────┘

Yangın şiddeti    düşük  (dNBR 0,12)
Eğim              27°
Ağaç örtüsü       %44
Arazi tipi        Yerleşim
```

**Göstermeyeceklerin:** `recovery_gap_pred` (zaten `NaN`), öncelik bileşenleri.

> ⚠️ **Dikkat — bu hücrelerde `priority_class` alanı `DUSUK`, `priority_score`
> alanı `0.0` geliyor.** Bunları "düşük öncelikli" diye gösterme! Değerlendirilip
> düşük bulunmuş değiller, **hiç değerlendirilmemişler.** Panelde ve haritada
> `predicted` + `DUSUK` olanlardan **ayrı renk** kullan (mesela gri / taralı).
>
> Ayrım kuralı: önce `prediction_status`'a bak, `priority_class`'a sonra.

---

# 3. `no_data` — uyarı paneli

```
┌─────────────────────────────────────────┐
│  ⚠  Veri yetersiz                       │
│  Bu hücre için tahmin üretilemedi       │
└─────────────────────────────────────────┘
```

Eldeki ölçümleri göster, tahmin ve öncelik alanlarını **hiç gösterme**.

> Bu davranış projenin metodolojik iddiasının bir parçası: bilmediğimiz yere
> "bilmiyorum" diyoruz. Boş bırakmak yerine 0 göstermek bu iddiayı bozar.

---

# Alan referansı

CSV'deki 19 sütunun tamamı:

| Alan | Tip | Aralık | Panelde |
|---|---|---|---|
| `fire_id` | metin | — | teknik |
| `cell_id` | metin | — | teknik |
| `lat` / `lon` | ondalık | Türkiye sınırları | teknik |
| `tree_cover` | oran | 0–1 | — *(çok yıllık, `_annual` tercih et)* |
| `tree_cover_annual` | oran | 0–1 | ölçümler |
| `burn_severity_dnbr` | ondalık | ~0–1,3 | ölçümler |
| `slope_deg` | derece | 0–~60 | ölçümler + neden |
| `elevation_m` | metre | 0–~2500 | ölçümler |
| `road_distance_km` | km | 0–~30 | ölçümler + neden |
| `ndvi_before` | ondalık | −1–1 | değişim |
| `ndvi_after` | ondalık | −1–1 | değişim |
| `ndvi_drop` | ondalık | — | değişim |
| `severity_class` | metin | dusuk/orta/yuksek | ölçümler |
| `land_cover` | metin | Ağaçlık / Otlak-çalılık / Tarım / … | ölçümler |
| `prediction_status` | metin | predicted / low_severity / no_data | **panel seçimi** |
| `recovery_gap_pred` | ondalık | ~0,1–0,6 · NaN olabilir | ölçümler + neden |
| `priority_score` | ondalık | 0–1 | başlık + neden |
| `priority_class` | metin | COK_YUKSEK / YUKSEK / ORTA / DUSUK | başlık |

---

# Üç kural

1. **Önce `prediction_status`'a bak.** Panel tipini o belirliyor, `priority_class` değil.
2. **Eşikleri ve ağırlıkları metadata'dan oku.** Kullanıcının ağırlıkları
   değiştirebilmesi ürünün temel özelliği — koda gömülürse o özellik ölür.
3. **`recovery_gap_pred` yüksek = kötü.** Renk ve sıralama buna göre.

---

# Sonradan eklenebilecekler

Şu an veride yok, istenirse üretebiliriz:

- **Komşu hücrelerle karşılaştırma** — "bu hücre çevresindeki 8 hücreden daha kötü"
- **Güven aralığı** — modelin bu tahminden ne kadar emin olduğu
- **Öznitelik katkısı (SHAP)** — sadece öncelik bileşenleri değil, modelin
  kendi içindeki 7 özniteliğin bu tahmine katkısı

İlk ikisi kolay, üçüncüsü model tarafında ek iş. İhtiyaç olursa söyleyin.
