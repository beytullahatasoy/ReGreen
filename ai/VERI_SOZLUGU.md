# Veri Sözlüğü

Üç dosya var, üçü farklı iş için:

| Dosya | Satır | Ne için |
|---|---|---|
| `turkiye_grid.csv` | 303.153 | **Ham veri.** Filtresiz, tüm sütunlar. Harita ve analiz için |
| `egitim_seti.csv` | 16.074 | **Model için hazır.** Filtrelenmiş, tavanlı, öznitelikler seçilmiş |
| `yangin_ozeti.csv` | 55 | Yangın başına özet ve kullanılabilirlik |

---

## 1 · Kapsam

| | Değer |
|---|---|
| Keşfedilen yangın kaydı | 55 |
| Bağımsız olay (çakışanlar birleştirildi) | 54 |
| **Mekânsal grup** (çapraz doğrulama birimi) | **37** |
| Eğitime uygun yangın | 34 |
| Eğitime uygun mekânsal grup | **27** |
| Yıl aralığı | 2017 - 2024 |
| Hücre boyutu | 250 × 250 m = 6,25 hektar |
| Toplam hücre | 303.153 |
| Yanık hücre | 37.163 |
| Güvenilir etiketli | 17.270 |
| Eğitim seti | 16.074 |

**Yangın değil, mekânsal grup sayın.** Çakışan ve bitişik yangınlar tek gruba
konur; çapraz doğrulamanın gerçek birimi 34 yangın değil **27 gruptur**.

Yangınlar MODIS yanmış alan ürününden otomatik keşfedildi, Türkiye sınırına
kırpıldı, arazi örtüsüne göre filtrelendi (anız yakma elendi), 300 hektar
alt sınırı uygulandı.

---

## 2 · Sütunlar

### Kimlik

| Sütun | Tip | Açıklama |
|---|---|---|
| `yangin_id` | metin | `BOLGE_YIL_SIRA`, örn. `AKD_2021_01`. Keşif kaydı kimliği |
| `olay_id` | metin | Aynı olayın birden çok kaydı varsa ortak kimlik. Şu an tek birleşme: `AKD_2021_07` + `IAN_2021_01` |
| `grup_id` | metin | **GroupKFold anahtarı.** Çakışan ve merkezleri 10 km'den yakın yangınlar aynı değeri alır. 55 yangın → 37 grup |
| `hucre_id` | metin | `{yangin_id}_{sıra}`, satır bazlı benzersiz |
| `bolge` | metin | Akdeniz, Ege, Marmara, Karadeniz, İç/Doğu Anadolu, Güneydoğu |
| `yil` | tam sayı | Yangın yılı |
| `yangin_tarihi` | tarih | MODIS'ten türetilen yanma tarihi |
| `modis_alan_ha` | tam sayı | MODIS'in bildirdiği yangın alanı (yangın başına sabit) |
| `lat`, `lon` | ondalık | Hücre merkezi, WGS84 |

### Topografya (Copernicus DEM 30 m)

| Sütun | Birim | Açıklama |
|---|---|---|
| `yukselti_m` | metre | Deniz seviyesinden yükseklik |
| `egim_derece` | derece | **100 metrede hesaplanıp 250'ye toplandı.** Doğrudan 250 m'de hesaplansa eğim hafife alınır |
| `baki_derece` | 0-360 | Yamacın baktığı yön. Ölçümde anlamsız çıktı, elendi |

### Arazi örtüsü

İki kaynak var ve **ikisi de kalıyor** — tamamlayıcı bilgi taşıyorlar.

| Sütun | Kaynak | Açıklama |
|---|---|---|
| `arazi_kodu`, `arazi_tipi`, `agac_orani` | ESA WorldCover **2021** | Tek yıl. 2017-2020 yangınları için yangın **sonrası** hali gösteriyor |
| `arazi_kodu_y`, `arazi_tipi_y`, `agac_orani_y` | Impact Observatory **yıllık** | Yangından önceki yılın haritası. Tanım gereği doğru zamanda. Kod 0 = **veri yok**, NaN'a çevrildi (§7). Geçerli kodlar: 1 Su, 2 Ağaçlık, 4 Sulak, 5 Tarım, 7 Yerleşim, 8 Çıplak, 11 Otlak/çalılık |
| `lulc_yili` | | Kullanılan yıllık harita yılı |
| `lulc_yangindan_once` | mantıksal | Harita gerçekten yangından önceye mi ait (2017 yangınlarında `False`) |

**Neden ikisi de:** Kısmi korelasyon ölçümü, WorldCover'ın IO sabitlendiğinde
+0,303, IO'nun WorldCover sabitlendiğinde +0,218 kattığını gösterdi. İkisi
farklı tanımlar kullanıyor — WorldCover Akdeniz makiliğini "ağaç" sayıyor,
Impact Observatory "otlak/çalılık" sayıyor. İkisi birden daha çok bilgi veriyor.

### Erişim ve maruziyet (OpenStreetMap)

| Sütun | Birim | Açıklama |
|---|---|---|
| `yol_mesafe_km` | km | En yakın yola uzaklık. Müdahalenin uygulanabilirliği |
| `yerlesim_mesafe_km` | km | En yakın köy/kasaba/şehre uzaklık |
| `su_mesafe_km` | km | En yakın dere/gölete uzaklık. **Ölçümde anlamsız, elendi** |

Bölge başına üç toplu Overpass sorgusu, sonra `cKDTree` ile en yakın komşu.
Maliyet hücre sayısından bağımsız.

### Yangın şiddeti (Sentinel-2)

| Sütun | Açıklama |
|---|---|
| `dnbr` | Yangın öncesi NBR eksi sonrası NBR. Şiddet ölçüsü |
| `dndvi` | Aynı hesabın NDVI versiyonu |
| `siddet_sinifi` | USGS eşikleri: yanmamış / düşük / orta-düşük / orta-yüksek / yüksek |

### NDVI serisi (Sentinel-2, medyan kompozit)

| Sütun | Pencere |
|---|---|
| `ndvi_oncesi` | Yangın − 70 gün … − 3 gün |
| `ndvi_sonrasi` | Yangın + 20 … + 95 gün |
| `ndvi_yil1` | Yangın + 330 … + 400 gün (aynı mevsim) |
| `ndvi_yil2` | Yangın + 695 … + 765 gün (aynı mevsim) |

Her pencerede en temiz 6 **gün** seçiliyor, aynı günün komşu uydu karoları
mozaikleniyor, sonra günler arası medyan alınıyor. Bulut maskesi SCL bandından.

### İklim (Open-Meteo)

| Sütun | Açıklama |
|---|---|
| `yagis_sonrasi_2yil_mm` | Yangından sonraki 730 günün toplam yağışı |
| `yagis_uzun_ort_mm` | 2010-2020 yıllık ortalama |
| `yagis_anomali` | İkisinin oranı. **Ölçümde anlamsız, elendi** |

### Bayraklar

| Sütun | Açıklama |
|---|---|
| `gecerli_hucre` | Bütün katmanlar veri getirmiş mi |
| `bitki_vardi` | Yangın öncesi NDVI ≥ 0,20 **ve** arazi sınıfı su değil. Denizin yanmış görünmesini engelleyen fiziksel tutarlılık kuralı |
| `yanik` | dNBR ≥ 0,10 **ve** bitki vardı **ve** geçerli |
| `ndvi_dusus` | `ndvi_oncesi − ndvi_sonrasi`. Etiketin paydası |
| `etiket_gecerli` | Yanık **ve** dNBR ≥ 0,27 **ve** NDVI düşüşü ≥ 0,20 **ve** yeniden yanmamış |
| `etiket_tam` | Yangın + 765 gün bugünden önce mi. 2024 yangınlarının bir kısmında `False` |
| `egitime_uygun` | Yangın düzeyinde: ≥ 30 etiketli hücre **ve** etiket tam **ve** alan uyuşmazlığı yok |
| `yeniden_yandi` | Hücre, 2 yıllık iyileşme penceresi içinde tekrar yandı mı. `True` ise etiket geçersiz sayıldı |
| `kalite_uyari` | Otomatik kontrollerin bulduğu sorunlar, boşsa temiz |

### Hedef değişkenler

| Sütun | Formül | Kullanım |
|---|---|---|
| `kalan_acik` | `ndvi_oncesi − ndvi_yil2` | **Birincil hedef.** "Bu hücre eski hâlinden hâlâ ne kadar uzakta" |
| `iyilesme_yil2` | `(ndvi_yil2 − ndvi_sonrasi) / ndvi_dusus` | Alternatif. Kullanılmıyor, aşağıda sebebi |

---

## 3 · Neden `kalan_acik`, neden oranlı tanım değil

Oranlı tanım denendi ve **yangın şiddetiyle matematiksel olarak bağlı** çıktı:

```
spearman(dNBR, ndvi_dusus) = 0,818
```

`ndvi_dusus` oranın paydası. dNBR paydayla bu kadar ilişkiliyse, dNBR ile
oranlı etiket arasındaki korelasyon kendiliğinden doğar — ekolojik bulgu değil.

Nitekim iki tanım zıt sonuç veriyordu: oranlı tanımda "şiddetli yanan daha iyi
toparlanıyor" (ρ = +0,49), açık tanımında "şiddetli yanan daha büyük açık
veriyor" (ρ = +0,37). İkincisi doğru olan.

**Bedeli:** `ndvi_oncesi` artık hedefin bileşeni olduğu için öznitelik olarak
kullanılamıyor.

---

## 4 · Ölçülmüş öznitelik gücü

Temizlenmiş veride, **grup bazında** tutarlılık testi. "24/27" = ilişki 27
mekânsal grubun 24'ünde aynı yönde çıktı. `p`, işaret testinin değeri.

| Öznitelik | ρ (havuz) | Tutarlılık | p | Karar |
|---|---|---|---|---|
| `agac_orani_y` | +0,556 | 24/27 | 5e-05 | **kullan** |
| `agac_orani` | +0,542 | 24/27 | 5e-05 | **kullan** |
| `dnbr` | +0,371 | 24/27 | 5e-05 | **kullan** |
| `yukselti_m` | +0,272 | 20/27 | 0,019 | kullan |
| `egim_derece` | +0,262 | 21/27 | 0,006 | kullan |
| `yol_mesafe_km` | +0,105 | 20/27 | 0,019 | kullan |
| `yerlesim_mesafe_km` | +0,126 | 19/27 | 0,052 | **ele** (aşağıda) |
| `su_mesafe_km` | −0,085 | 13/27 | 1,00 | **ele** |
| `baki_derece` | −0,061 | 21/27 | 0,006 | **ele** (büyüklük yok) |
| `yagis_anomali` | +0,078 | yangın düzeyi | 0,62 | **ele** |

`agac_orani_y` +0,510'dan +0,556'ya çıktı; sebebi nodata düzeltmesi (§6).

### Elenenleri geri koyunca ne oluyor

İşaret testi sınırda kalanlar için karar modele bırakılmadı, **ölçüldü**.
Her aday tam modele eklenip 5 katlı `GroupKFold` ile değerlendirildi:

| Eklenen | R² farkı | Grup içi ρ farkı |
|---|---|---|
| `yerlesim_mesafe_km` | −0,017 | +0,009 |
| `su_mesafe_km` | −0,044 | +0,006 |
| `baki_derece` | +0,014 | +0,000 |

Üçü de gürültü sınırında. R² dalgalanmasının kendisi ±0,04 (3 kat × 3 tohum
tekrarlı ölçüm), yani bu farklar sıfırdan ayırt edilemiyor. Üçü de elendi.

### Arazi sınıfı neden öznitelik değil

`arazi_kodu_y` sayısal kodu (Ağaçlık=2, Otlak=11) modele **doğrudan
verilmemeli** — model "Otlak > Ağaçlık" diye yapay bir sıralama görür.
İkili sütunlara açıp ölçtük:

| | R² | Grup içi ρ |
|---|---|---|
| Tam model | +0,172 | +0,484 |
| Arazi ikilileri çıkarılmış | +0,173 | +0,484 |

Katkı tam sıfır. Taşıdığı bilgi zaten `agac_orani` ve `agac_orani_y` içinde.
Sütunlar veride referans olarak duruyor, öznitelik listesinde yok.

Eğim ilişkisi dilim dilim monotonik artıyor: 0,276 → 0,316 → 0,347 → 0,368 → 0,379.

---

## 5 · Tavan yerine ağırlık

Eğitim setinde bir zamanlar grup başına **400 hücre** tavanı vardı. Amacı en
büyük grubun (verinin %37'si) modeli ezmesini önlemekti. **Ölçtük ve tersi
çıktı** — tavan zarar veriyordu:

| Kurulum | Grup içi ρ | En kötü grup | Satır |
|---|---|---|---|
| Tavan 400 | +0,468 | −0,069 | 5.522 |
| **Tavansız** | **+0,569** | −0,007 | **16.074** |
| Tavansız + grup ağırlığı | +0,490 | **+0,067** | 16.074 |

(3 tohum × 3 kat tekrarlı ölçüm, std 0,011-0,016 — farklar gürültünün çok üstünde.)

Sebep: değerlendirme zaten **grup başına ayrı ayrı** yapılıyor, yani büyük
grubun hakimiyeti metriği şişiremiyor. Tavan yalnızca eğitim verisini kırpıyordu.
Dengeleme bir **model kararıdır, veri kararı değil**.

Bu yüzden veriden satır atmıyoruz; `grup_agirlik` sütununu veriyoruz:

```
w = N / (grup_sayısı × grup_boyutu)
```

Her grup toplamda eşit ağırlık taşır. `sample_weight=df["grup_agirlik"]` ile
kullanın.

**Hangisini seçmeli:** ağırlık ortalamayı düşürür ama en kötü grubu düzeltir
(−0,007 → +0,067). "Her yerde çalışsın" önemliyse ağırlığı kullanın,
"ortalamada iyi olsun" önemliyse kullanmayın. Tavanı ise kullanmayın.

---

## 6 · Sıfırlar ve boş değerler

### Ne kadar var

| Sütun | Boş (NaN) | `=0` | `=1` |
|---|---|---|---|
| `agac_orani` | 0 | 519 (%3,2) | 5.813 (%36) |
| `agac_orani_y` | **701 (%4,4)** | 868 (%5,4) | 8.044 (%50) |
| `dnbr`, `egim_derece`, `yol_mesafe_km` | 0 | 0 | — |
| `yukselti_m` | 3 | 0 | — |

Ağaç oranı dışındaki sütunlarda ne boş değer ne de şüpheli sıfır var.

### `0` gerçek bir ölçüm mü, "veri yok" mu → **gerçek**

Üç bağımsız kanıt:

**1. Sıfır grubu, boş gruptan farklı davranıyor.** Aynı şey olsalardı hedef
ortalamaları da aynı olurdu:

| `agac_orani_y` | n | Hedef ortalaması |
|---|---|---|
| boş (NaN) | 701 | 0,339 |
| `=0` | 868 | **0,270** |

**2. İkinci kaynak doğruluyor.** `agac_orani = 0` olan hücrelerde bağımsız
kaynak `agac_orani_y` ortalaması **0,155** — o da "neredeyse ağaç yok" diyor.

**3. İlişki monotonik.** Kod olsaydı sıçrama olurdu, gradyan var:

```
agac_orani = 0        → hedef 0,279
             0-0,5    → 0,287
             0,5-0,99 → 0,317
             = 1      → 0,412
```

Yani `0` = "burada ağaç yoktu" (otlak, çalılık, tarım), `1` = "hücrenin tamamı
ormandı". İkisi de meşru uç değer.

### `0` ile `1` arasındaki ölçek sorun çıkarır mı → **hayır**

Ağaç tabanlı modeller (HistGB, LightGBM, XGBoost, RandomForest) yalnızca
sıralamaya bakar, ölçekten etkilenmez. Doğrusal modelde de fark çıkmadı:

| Model | Grup içi ρ |
|---|---|
| Ridge, ölçeklenmiş | +0,538 |
| Ridge, ölçeksiz | +0,539 |
| HistGB | **+0,562** |

Bütün öznitelikler zaten aynı büyüklük mertebesinde değil (`yukselti_m` 1.800'e
kadar çıkıyor), ama bu da bir sorun yaratmıyor. Yine de **doğrusal model veya
sinir ağı kullanacaksanız `StandardScaler` ekleyin** — zararı yok, bazı
kurulumlarda yakınsamayı hızlandırır.

### Boş değerleri ne yapmalı → **dokunmayın**

Dört yöntem denendi, hepsi aynı çıktı (3 tohum × 3 kat, std 0,009-0,016):

| Yöntem | Grup içi ρ |
|---|---|
| Boş bırak (ağaç modeli kendi halleder) | +0,562 |
| Diğer kaynakla doldur | +0,560 |
| Doldur + eksik bayrağı ekle | +0,561 |
| Medyanla doldur | +0,566 |

Fark yok. **Ağaç modeli kullanıyorsanız hiçbir şey yapmayın.**

**Doğrusal model / sinir ağı kullanacaksanız** NaN kabul etmezler, doldurmak
zorundasınız — en mantıklısı diğer kaynakla doldurmak:

```python
X["agac_orani_y"] = X["agac_orani_y"].fillna(X["agac_orani"])
X["yukselti_m"]  = X["yukselti_m"].fillna(X["yukselti_m"].median())
```

**UYARI — `dropna()` KULLANMAYIN.** Boş değerler rastgele dağılmamış, sadece
**3 grupta** toplanmış (`AKD_2020_01` 393, `AKD_2021_04` 283, `AKD_2023_02` 25).
Satırları atarsanız o üç grubu sakatlarsınız; `AKD_2020_01` verisinin çoğunu
kaybeder. Doldurun ya da olduğu gibi bırakın.

### Gerçek sınır: doygunluk

`agac_orani`'nın %36'sı, `agac_orani_y`'nin %50'si tam **1,0**. Öznitelik
"sık orman" ile "çok sık orman"ı ayıramıyor — tavana dayanmış.

Bu gerçek bir çözünürlük kaybı ama felaket değil: `agac_orani = 1` olan
hücrelerin içinde hedef hâlâ 0,03 ile 0,71 arasında değişiyor (std 0,095, tüm
veride 0,106). Yani o grupta ayrımı `dnbr`, `egim_derece` ve `yukselti_m`
yapıyor. Daha iyi bir ağaç yoğunluğu ölçüsü modelin tavanını yükseltirdi;
elimizdeki kaynaklarda yok.

---

## 7 · Yapılan temizlik (`veri_temizle.py`)

Veri seti bir denetimden geçti ve beş sorun düzeltildi. Hepsi ölçülerek
doğrulandı.

**1. Nodata gerçek veri sanılıyordu.** `arazi_kodu_y == 0` bir sınıf değil,
"veri yok" demek — Impact Observatory kodları 1,2,4,5,7,8,9,10,11'dir. Bu
hücrelerde `agac_orani_y` 0,000 yazıyordu; model bunu "hiç ağaç yok" diye
okuyordu, doğrusu "bilmiyoruz". 12.101 satır etkilenmişti, en ağırı
AKD_2020_01 (%63). Düzeltmeden sonra o yangında ρ **−0,341 → +0,513**,
genelde **+0,510 → +0,556**.

**2. Bir yangın iki kez sayılıyordu.** `AKD_2021_07` (28.07.2021) ve
`IAN_2021_01` (29.07.2021): merkezleri 0,1 km, %62 çakışma. Aynı yangının
bölge sınırından ikiye bölünmüş hâli. Tek olayda birleştirildi — İç Anadolu
"bölgesi" böylece kayboldu, çünkü hiç yoktu.

**3. Yeniden yanan hücrelerin etiketi geçersizdi.** 2 yıllık iyileşme
penceresi içinde tekrar yanan hücrede `ndvi_yil2` iyileşmeyi değil ikinci
yangını ölçer. 2.154 hücre işaretlendi, bunlardan 7'si etiketliydi ve
etiketleri iptal edildi.

**4. Mekânsal sızıntı.** 20 yangın çifti hücre paylaşıyordu, biri %100
(`EGE_2019_03` ↔ `EGE_2021_01` aynı alan). `GroupKFold(yangin_id)` bunları
ayrı gruplar sayıp aynı araziyi hem eğitime hem teste koyuyordu. Çakışan ve
merkezleri 10 km'den yakın yangınlar tek `grup_id`'ye toplandı: 55 yangın →
**37 mekânsal grup**.

**5. Yinelenen hücre.** Birleşen kayıtlarda aynı yer zemini iki satırdı;
2.106 satır tekilleştirildi (dNBR'si yüksek olan tutuldu).

---

## 8 · Bilinen sınırlar

**2021 baskın — hangi sayıyı söylediğinize dikkat.** Tavan kalktığı için satır
bazlı pay yükseldi:

| Ölçüm | 2021 payı |
|---|---|
| Satır bazında | **%85** |
| Grup bazında (27 grubun kaçı) | %52 |
| `grup_agirlik` ile ağırlıklı | %56 |

Sunumda **grup bazlı %52**'yi söyleyin — çapraz doğrulamanın birimi grup,
satır değil. "%85" doğru ama yanıltıcı: tek bir büyük yangının hücre sayısını
yansıtıyor, bağımsız örnek sayısını değil.

Türkiye'de 2021 gibi bir yangın yazı başka olmadı; 139 bin hektar yandı. Bu
bizim eksiğimiz değil, veri gerçeği. Daha dengeli olmanın tek yolu daha eski
veriye inmek, ama Sentinel-2 2017'de başlıyor.

**Bölge dağılımı dengesiz.** 34 uygun yangının 33'ü Ege ve Akdeniz'de,
1'i Marmara (Çanakkale). Sebep coğrafya: Türkiye'nin orman yangını yükü kıyı kuşağında
yoğunlaşmış. "Türkiye'nin her bölgesinde çalışıyor" **denemez**.

**Neden İç Anadolu, Karadeniz, Doğu Anadolu yok.** İki ayrı sebep var ve
ikisi de ölçüldü.

**(a) İç bölgelerde 300 ha üstü ORMAN yangını neredeyse yok.** Keşif tüm
Türkiye'de 2.174 yanık küme buldu. Marmara ve İç Anadolu'da 300 ha üstü
**190 küme** elendi; %95'i ağaç örtüsü **%10'un altında** ve baskın sınıfı
**tarım** (ortalama bileşim: %93 tarım, %5 otlak, %1 ağaç). Bunlar orman
yangını değil, **anız yakma**.

Eşik keyfi de değil: 0,40-0,45 aralığında **tek bir küme bile yok**. Dağılım
iki uçlu — bir küme ya %45'ten fazla ormanlık, ya %10'dan az. Gri bölge yok.

| Bölge | ≥300 ha aday | Geçen | Oran |
|---|---|---|---|
| Ege | 45 | 30 | %67 |
| Akdeniz | 131 | 16 | %12 |
| Marmara | 93 | 3 | %3 |
| İç Anadolu | 104 | 3 | %3 |
| Güneydoğu | 1.020 | 1 | %0,1 |

Güneydoğu'daki 1.020 kümenin en büyükleri 100.000 hektarı aşıyor ve %98'i
tarım — Şanlıurfa/Diyarbakır ovalarındaki anız yakma. Filtre doğru çalışıyor.

**(b) Bursa gibi gerçek orman yangınları var ama bizim tabanımızın altında.**
Bursa çevresinde (39,8-40,5 K / 28,5-30,0 D) 2017-2024 boyunca 300 ha üstü
**tek bir aday** çıktı. Türkiye'de ortalama orman yangını ~4 hektar; MODIS
500 m çözünürlükte ve küçük yangınları göremiyor. Yani "Bursa'da yangın
olmadı" değil, **"MODIS'in gördüğü büyüklükte olmadı"**.

Kalan elenme sebepleri:

| Yangın | Bölge | Sebep |
|---|---|---|
| `KAR_2024_01` | Karadeniz | 234 etiketli hücre hazır, ama 2 yıllık pencere **2026-09-20**'de kapanıyor |
| `IAN_2023_01` | İç Anadolu | Bizim alan MODIS'in 4,15 katı (sınır 4,0) |
| `IAN_2024_01` | İç Anadolu | 4,24 katı |
| `DAN_2021_01` | Doğu Anadolu | 8,04 katı |
| `GDA_2021_01` | Güneydoğu | 5,52 katı, ayrıca 8 etiketli hücre |
| `IAN_2021_01` | Adana (Akdeniz) | `AKD_2021_07` ile aynı yangın, §7 |

**Bölge etiketleri düzeltildi.** Eskiden kaba dikdörtgen kurallarla atanıyordu
ve "İç Anadolu" geri kalan her şeyi toplayan bir çöp kutusuydu: Adana'daki
yangın (37,63 K) "lat < 37,6" kuralının 3 km kuzeyinde kaldığı için İç Anadolu
sayılmıştı. Artık her yangının merkezi için Nominatim'den **gerçek il** alınıp
il→bölge eşlemesi uygulanıyor (`bolge_duzelt.py`). 55 yangının 4'ü değişti:

| Yangın | Eski | Yeni | İl |
|---|---|---|---|
| `IAN_2021_01` | İç Anadolu | **Akdeniz** | Adana |
| `IAN_2024_01` | İç Anadolu | **Ege** | Uşak |
| `AKD_2021_10` | Akdeniz | **Ege** | Muğla |
| `GDA_2021_01` | Güneydoğu | **Doğu Anadolu** | Hakkâri |

`yangin_id` önekleri (`AKD_`, `IAN_`) **değiştirilmedi** — kimlik değiştirmek
bütün parça dosyalarını kırardı. Doğru bölge `bolge` ve `il` sütunlarında.
Düzeltmeden sonra keşifte İç Anadolu'da **1** yangın kalıyor (Eskişehir,
336 ha) ve o da alan uyuşmazlığına takılıyor.

Alan oranı Ege ve Akdeniz'de medyan **1,0-1,1** (yani MODIS'le uyumlu), iç
bölgelerde **4-8**. Bu bir eşik tesadüfü değil, sistematik bir fark: dNBR
tabanlı yanık sınırımız bozkır/step örtüsünde fazla alan işaretliyor.
Eşiği gevşetmek 72 hücre kazandırır ama güvenilmez veri getirir. Doğru çözüm
iç bölgeler için yanık tespitini ayrı kalibre etmek — yapılmadı.

**Varyansın yarısı yangınlar arası.** Grup dengeli hesapla **%54'ü** yangın
ortalamaları arasında, %46'sı yangın içinde. (Satır ağırlıklı hesap %22 verir,
çünkü en büyük grup tek başına verinin %37'si ve genel ortalamayı kendine
çekiyor — dengeli olan doğru sayıdır.)

Model kolay yolu seçerse "bu yangının ortalaması ne" diye öğrenir. Birincil
metriğin grup içi sıralama olmasının sebebi bu (§9).

**`dnbr` sansürlü.** Etiket filtresi yüzünden minimum tam 0,270. Model düşük
şiddetli yanığı hiç görmüyor, o aralıkta tahmin edemez.

**2017 yangınlarında arazi örtüsü kirli.** Impact Observatory 2017'de
başlıyor, o yılın yangınları için "yangından önceki yıl" yok. 4 yangını
etkiliyor, `lulc_yangindan_once = False` ile işaretli.

**2022-2024 yangınları sistematik olarak farklı.** Kalan açık 0,15,
2017-2021'de 0,35 (Mann-Whitney p < 0,0001). Bu yangınların dNBR'si de daha
düşük (0,391 vs 0,497), yani fark kısmen gerçek — daha hafif yanmışlar.
Yıl **öznitelik olarak modele konmamalı**, değerlendirmede ele alınmalı.

**Hücre içi otokorelasyon.** 250 metrede komşu hücreler birbirine çok
benziyor. Rastgele `KFold` bu yüzden R² +0,591 verir; doğrusu +0,395. Aradaki
fark tamamen sahtedir.

---

## 9 · Modelde kullanım

```python
import pandas as pd
from sklearn.model_selection import GroupKFold

df = pd.read_csv("egitim_seti.csv")

OZ = ["agac_orani", "agac_orani_y", "dnbr",
      "egim_derece", "yukselti_m", "yol_mesafe_km"]

X = df[OZ]
y = df["kalan_acik"]
gruplar = df["grup_id"]          # yangin_id DEGIL
w = df["grup_agirlik"]           # istege bagli, bkz. bolum 5

for egitim, test in GroupKFold(n_splits=5).split(X, y, gruplar):
    ...
```

Öznitelik listesini elle yazmayın gerekmiyorsa — `egitim_seti_meta.json`
içindeki `oznitelikler` alanı tek doğru kaynaktır.

### Üç kural

**Grup anahtarı `grup_id`.** `yangin_id` kullanmayın, rastgele `KFold` hiç
kullanmayın. Ölçüldü:

| Bölme | R² |
|---|---|
| `grup_id` | **+0,395** ← doğru |
| `yangin_id` | +0,361 |
| rastgele `KFold` | +0,591 ← sahte |

**Birincil metrik grup içi Spearman.** R² değil. Projenin vaadi "hangi
parselden başlayalım" sıralaması; R² yangınlar arası ortalama farkını ödüllendirir,
bizim işimiz o değil. Taban model: **grup içi ρ = +0,584, medyan +0,695,
27 grubun 26'sında pozitif**.

Metriklerin tam listesi, bugünkü yöntemle karşılaştırma ve öncelik skorunun
nasıl hesaplandığı **§10**'da.

**`lat`, `lon`, `yil`, `bolge` öznitelik değildir.** Model konumu ve yılı
ezberler. Veride haritalama ve filtreleme için duruyorlar.

### Sağlık kontrolü

```
python veri_dogrula.py
```

Bütünlüğü (nodata artığı, yinelenen satır, aynı yerin iki grupta olması,
sızıntı sütunu) denetler, üç bölme yöntemini karşılaştırır, ablasyon çalıştırır.
Veri setine dokunulduğunda tekrar çalıştırın.

---

## 10 · Model hedefi, metrikler ve öncelik skoru

Ekipten iki soru geldi, ikisi aynı mimarinin iki katmanı: *"Hedef sütun ne,
performans metriğimiz ne olacak?"* ve *"Öncelik skoru kalktı mı, kaldırdıysak
şimdi nasıl yapacağız?"*

### 10.1 · Hedef: `kalan_acik` — regresyon, sınıflandırma değil

```
kalan_acik = ndvi_oncesi − ndvi_yil2
```

**"Yangından 2 yıl sonra bu hücre eski hâline ne kadar dönememiş olacak."**

| Değer | Anlamı | Karar |
|---|---|---|
| Yüksek (0,5+) | Kendi kendine toparlanmıyor | **Müdahale et** |
| Düşük (0,1) | Doğa kendi hallediyor | Para harcama |

Veride: ortalama 0,346 · std 0,106 · aralık −0,05 ile 0,73.

**Neden öncelik değil de bu?** Çünkü ölçülebilir tek şey bu. Kimse 16.074
hücreye elle "öncelik 7/10" yazmadı — ama **doğa hepsine kendi cevabını
yazdı** ve uydu 2 yıl sonra geri gelip ölçtü. Etiketimiz bu.

Tanımın neden oranlı değil de farklı olduğu §3'te.

### 10.2 · Metrikler — sıralaması önemli

**Birincil: grup içi Spearman ρ.** R² değil.

Ürün mutlak sayı değil. Ormancıya "bu hücrenin NDVI açığı 0,42 olacak" demek
işe yaramaz; **"şu 200 hektardan başla"** demek işe yarar. Ürün bir
**sıralama**, o hâlde metrik de sıralama metriği olmalı.

**İkincil: Top-%20 isabeti.** Modelin "en kötü %20" dediği hücrelerin kaçı
gerçekten en kötü %20'de. Karar açısından en anlamlı sayı bu, çünkü bütçe
zaten ancak o kadarına yetiyor. Rastgele seçimde bu değer %20 çıkar.

**Üçüncül:** MAE (NDVI biriminde, yorumlanabilir), R², kaç grupta ρ pozitif.

Değerlendirme daima `GroupKFold(grup_id)` ile — sebebi §9'da.

### 10.3 · Ölçülen sonuçlar: bugünkü yöntemi yeniyor muyuz

Asıl soru bu. Bugün sahada "en şiddetli yanan yerden başla" deniyor; onu
taban kabul edip karşılaştırdık.

| Yöntem | Grup içi ρ | Pozitif | Top-%20 isabet |
|---|---|---|---|
| Rastgele | −0,045 | 9/27 | %17 |
| **Sadece dNBR** (bugün yapılan) | +0,377 | 24/27 | **%39** |
| Sadece ağaç oranı | +0,430 | 24/27 | %36 |
| **RandomForest** | **+0,576** | 26/27 | **%47** |
| HistGradientBoosting | +0,583 | 26/27 | %47 |

`GroupKFold(grup_id, n_splits=5)`, 6 öznitelik, eksikler dolduruldu.

*(Küçük fark: `veri_dogrula.py` +0,584 raporlar, buradaki tabloda +0,583 —
tek sebep NaN işlemi. Orada boş bırakılıyor, burada dolduruldu. Fark gürültü
sınırında, bkz. §6.)*

**Projenin tek cümlelik savunması:** aynı bütçeyle şiddet sıralamasına göre
**%20 daha fazla doğru parsel** (%39 → %47).

RandomForest ile HistGradientBoosting arasında anlamlı fark yok (0,576 vs
0,583). RandomForest'ı seçmek savunulabilir — açıklaması daha kolay ve
`feature_importances_` doğrudan sunulabilir.

### 10.4 · Öncelik skoru kalkmadı, ikiye ayrıldı

Öncelik skoru **model hedefi olmaktan çıkarıldı**, ortadan kalkmadı. Üç
sebeple:

**1. Yer gerçeği yok.** Öncelik etiketli tek bir hücre bile yok. Uydurup
eğitseydik model bizim uydurduğumuzu öğrenirdi. "Önceliği nasıl
doğruladınız?" sorusuna cevabımız olmazdı. İyileşme açığını
doğrulayabiliyoruz, önceliği doğrulayamayız.

**2. Öncelik olgu değil, değer yargısı.** *"Bu hücre kendi kendine düzelmez"*
bir **olgu** — ölçülür. *"Bu hücre önce ele alınmalı"* bir **tercih** — su
havzası mı koruyorsun, erozyonu mu durduruyorsun, bütçen ne? Bunlar OGM'nin
kararı, NDVI'dan öğrenilmez.

**3. İçine gömersen değiştiremezsin.** Ayrı tutunca ağırlık değişince sonucun
nasıl değiştiği canlı görülür.

### 10.5 · Üç katmanlı mimari

```
KATMAN 1 — MODEL  (veriden öğrenilir, doğrulanabilir)
    girdi : 6 öznitelik
    çıktı : tahmini iyileşme açığı
    ölçüm : grup içi ρ = +0,58 · top-%20 = %47
                    ↓
KATMAN 2 — ÖNCELİK SKORU  (şeffaf formül, öğrenilmez)
    skor = w₁ · iyileşme_açığı      ← modelden
         + w₂ · erozyon_riski        ← egim_derece
         + w₃ · erişim_kolaylığı     ← yol_mesafe_km
         + w₄ · koruma_değeri        ← havza/SİT verisi (HENÜZ YOK)
                    ↓
KATMAN 3 — SINIF  (yalnızca harita gösterimi)
    skorun YANGIN İÇİ yüzdelikleri
    → Çok yüksek / Yüksek / Orta / Düşük
```

Ağırlıklar arayüzde kaydırıcı; varsayılan w₁ = 0,5 · w₂ = 0,3 · w₃ = 0,2.

Sınıflar modelin çıktısı **değil**, skorun yüzdelik dilimleri. Yangın içinde
göreli, çünkü bütçe yangın bazında ayrılıyor.

**Not:** Veride duran `siddet_sinifi` sütunu bu sınıflar değildir — o
dNBR'den türetilmiş şiddet sınıfıdır ve model için sızıntıdır (§9).

### 10.6 · Neden baştan sınıflandırma yapmıyoruz

"Yüksek/orta/düşük" diye 3 sınıflı model kursak: eşikleri biz uydururuz,
model bizim uydurduğumuz eşiği öğrenir, aradaki bilgi kaybolur ve eşik
değişince yeniden eğitmek gerekir.

Regresyon yapıp sonradan dilimlemek üçünü de çözüyor — **eşik politikadır,
veri değil.**

### 10.7 · Bilinen zayıflık: `egim_derece` iki yerde

`egim_derece` hem modelde öznitelik, hem skorda erozyon terimi. Çift sayım
gibi görünüyor ama iki farklı iş yapıyor: modelde *iyileşmeyi tahmin ediyor*,
skorda *toprak kaybı riskini temsil ediyor*.

Savunulabilir ama sorulabilir. Bilerek yapıldı; sunumda hazırlıklı olun.

### 10.8 · Özet tablo

| | Ne | Nereden gelir |
|---|---|---|
| Model hedefi | `kalan_acik` (regresyon) | Doğanın 2 yıllık cevabı |
| Birincil metrik | Grup içi Spearman | Ürün sıralama olduğu için |
| Karar metriği | Top-%20 isabet | Bütçe ancak %20'ye yetiyor |
| Öncelik skoru | Model + politika ağırlıkları | Modelden **sonra**, şeffaf formül |
| Sınıflar | Skorun yangın içi yüzdelikleri | Yalnızca harita gösterimi |

---

## 11 · Üretim sırası

```
python yangin_kesif.py      # MODIS'ten yangınları bul
python pipeline_turkiye.py  # katmanları çek, grid üret
python arazi_duzelt.py      # yıllık arazi örtüsü ekle
python veri_temizle.py      # nodata, çakışma, yeniden yanma, grup kimliği
python veri_son_hal.py      # etiket_tam / egitime_uygun bayrakları
python egitim_seti.py       # modele hazır alt küme
python veri_dogrula.py      # sağlık kontrolü
```

`veri_temizle.py` `turkiye_grid.csv` üzerine yazar; yedeği
`onbellek/turkiye_grid_yedek.csv`.
