# AI

ReGreen yapay zekâ ve veri çalışmaları.

**Sorumlu:** Buğra
**Model sürümü:** `ridge_v2` · **Şema sürümü:** `1.1`

Bu klasör **üretim mantığını** tutar. Üretilen veri `sample-data/backend-data/`
altında; ham uydu verisi ve ara dosyalar (yüzlerce MB) repoya girmiyor.

---

## Ne yapıyoruz

Yangın söndükten sonra elde sınırlı ekip, bütçe ve fidan varken **hangi bölgeye
önce gidilmeli** sorusuna veriye dayalı bir sıralama öneriyoruz.

```
kalan_acik = NDVI_yangın_öncesi − NDVI_2._yıl
```

Yani *iki yıl sonra bitki örtüsünün ne kadarı hâlâ eksik.* Etiketi biz
üretmiyoruz — uzman anketi veya elle etiketleme yok, doğanın 53 yangında iki
yılda verdiği fiilî cevap.

---

## Klasörler

| Klasör | Ne var |
|---|---|
| `boru_hatti/` | Yangın keşfi (MODIS) → Sentinel-2 kompozit → ızgara + öznitelik çıkarımı |
| `veri/` | Temizlik, doğrulama, eğitim seti üretimi |
| `model/` | Model eğitimi (3 aşama) ve Colab defteri |
| `deneyler/` | 12 ölçüm script'i — denenen ve elenen her şey |
| `teslim/` | Öncelik formülü, teslim paketi üretimi, ön kontrol |

---

## Kurulum

```bash
python -m venv .venv
.venv\Scripts\activate            # Windows
source .venv/bin/activate          # Linux / macOS
pip install -r ai/requirements.txt
```

Python **3.10.11** ile doğrulandı. Sürümler `requirements.txt`'te sabitli.

## Veri kökü

Bütün scriptler tek bir veri kökünden okuyup oraya yazar. Tanımı
`ai/yollar.py` içinde, varsayılanı `ai/data/`:

```
ai/data/
   ham/        boru hattının ürettiği ham ızgara ve parçalar
   ara/        turkiye_grid.csv, egitim_seti.csv, yanginlar.json
   cikti/      model dosyaları, ölçüm sonuçları
   onbellek/   uydu ve yağış önbelleği
```

Bu klasör **git'e girmez** — `turkiye_grid.csv` tek başına ~106 MB.
Başka bir yerde tutmak istersen:

```bash
set REGREEN_AI_DATA=D:\regreen_veri         # Windows
export REGREEN_AI_DATA=/veri/regreen        # Linux / macOS
```

Nerede olduğunu görmek için: `python ai/yollar.py`

## Çalıştırma sırası

Komutlar **repo kökünden** çalıştırılır. Her adım bir öncekinin çıktısını
`ai/data/` üzerinden alır — aradaki dosyaları elle taşımak gerekmez.

```bash
# 1. Yangın keşfi — MODIS'ten >=300 ha yangınları bul
python ai/boru_hatti/yangin_kesif.py          # -> ara/yanginlar.json

# 2. Öznitelik çıkarımı — her yangın için 250 m ızgara  (SAATLER surer)
python ai/boru_hatti/pipeline_turkiye.py      # -> ara/turkiye_grid.csv, ham/parcalar_250m/

# 3. Temizlik — nodata, mükerrer, yeniden yanma, mekansal grup
python ai/veri/veri_temizle.py                # -> ara/turkiye_grid.csv (temiz)
python ai/veri/veri_son_hal.py                # -> etiket gecerliligi

# 4. Eğitim seti — 16.074 satır, 7 öznitelik, 27 grup
python ai/veri/egitim_seti.py                 # -> ara/egitim_seti.csv

# 5. Model — aile karşılaştırma -> ayar -> final   (~6 dk, 16 cekirdek)
python ai/model/model_egit.py                 # -> cikti/regreen_model.joblib

# 6. Teslim paketi — 53 yangın
python ai/teslim/teslim_uret.py               # -> sample-data/backend-data/

# 7. Ön kontrol — backend doğrulayıcı kurallarına karşı
python ai/teslim/teslim_onkontrol.py
```

> **Hazır veriyle başlamak:** 1-3. adımlar uydu verisi çekiyor, saatler
> sürer. Elinde üretilmiş `egitim_seti.csv` varsa `ai/data/ara/` altına
> koyup doğrudan 5. adımdan devam edebilirsin.

Eksik girdi olursa scriptler hangi dosyanın nerede beklendiğini ve hangi
adımın onu ürettiğini yazan bir hata verir — sessizce patlamaz.

### Ölçümleri tekrar üretmek

`ai/deneyler/` altındaki script'ler `ara/egitim_seti.csv` okuyup sonuçları
`cikti/` altına yazar. Hepsi bağımsız çalışır:

```bash
python ai/deneyler/deney_2_oznitelik.py       # aday oznitelik taramasi
python ai/deneyler/deney_6_dogrulama.py       # ic ice secim, yanlilik olcumu
python ai/deneyler/deney_10_tavan.py          # tavan analizi
```

---

## Veri kaynakları

Hepsi açık ve doğrulanabilir, hiçbiri satın alınmadı.

| Katman | Kaynak | Çözünürlük |
|---|---|---|
| Bitki örtüsü (NDVI) | Sentinel-2 L2A, Microsoft Planetary Computer | 10 m |
| Yangın şiddeti (dNBR) | Sentinel-2 B8A/B12 | 20 m |
| Yangın keşfi | MODIS MCD64A1 | 500 m |
| Eğim / yükselti | Copernicus DEM | 30 m |
| Ağaç örtüsü | ESA WorldCover + Impact Observatory | 10 m |
| Yol mesafesi | OpenStreetMap | vektör |

Kendi ürettiğimiz kısım: bu katmanları 250 m'lik ortak ızgaraya oturtup her
hücre için yangın öncesi–sonrası–1. yıl–2. yıl NDVI zaman serisi çıkarmak.
Türkiye için böyle hazır bir veri seti yok.

---

## Veri seti

```
53 yangın · 2017–2024 · 7 bölge
303.153 hücre (250 × 250 m)
 37.163 yanık hücre
 16.074 etiketli hücre  ← model bununla eğitiliyor
     27 mekânsal grup
```

Yan yana yanan yerler aynı dağ, aynı toprak, aynı ağaç. Birini eğitime birini
teste koyarsak model tahmin etmiş olmaz, komşusundan hatırlamış olur. Bu yüzden
çakışan ve 10 km'den yakın yangınlar tek **mekânsal grupta** birleştirildi ve
bir grup asla bölünmüyor.

---

## Öznitelikler (7)

| Öznitelik | Ne anlatıyor |
|---|---|
| `agac_orani` | Bölge normalde ne kadar ormanlık |
| `agac_orani_y` | Yangın yılındaki ağaç örtüsü |
| `dnbr` | Yangın şiddeti — kızılötesi yanık izi |
| `egim_derece` | Erozyon ve tohum tutunma riski |
| `yukselti_m` | İklim / vejetasyon kuşağı |
| `ndvi_dusus` | Yangın hemen ardındaki fiilî bitki kaybı |
| `yol_mesafe_km` | Müdahale edilebilirlik |

Elenen adaylar ve gerekçeleri `veri/egitim_seti.py` içindeki `ELENEN`
sözlüğünde — bakı, su mesafesi, yerleşim mesafesi, uzun dönem yağış, arazi
örtüsü sınıfı, tohum kaynağı mesafesi. Hepsi ölçüldü, katkısı çıkmadı.

`ndvi_oncesi` ayrı bir durum: skoru yükseltiyordu ama **taftolojik** —
hedef `ndvi_oncesi − ndvi_yil2`, yani özniteliği hedefin içinden vermek
oluyor. Reddedildi.

---

## Sonuçlar

Bütün sayılar **out-of-fold**: model o yangını hiç görmeden tahmin etti
(LeaveOneGroupOut, 27 mekânsal grup).

| Metrik | Değer |
|---|---|
| Grup içi Spearman | **+0,686** (medyan +0,744) |
| Pozitif grup | **27/27** |
| top-%20 isabet | **%52,8** — dNBR tabanı %38,8 · rastgele %20 |
| İkili karşılaştırma doğruluğu | **%75,8** — dNBR %62,4 · yazı-tura %50 |

**En anlaşılır hali:** aynı yangından iki hücre verildiğinde hangisinin iki yıl
sonra daha kötü durumda olacağını %75,8 doğrulukla biliyor. Sahada şu an
kullanılan yangın şiddeti yöntemi %62,4'te kalıyor.

Aynı sayılar `sample-data/backend-data/manifest.json` içinde
`model_performance` altında — arayüzde gösterilecekse oradan okunmalı.

### Neden burada durduk

Tavan ölçüldü. Aynı yangının %80'iyle eğitip görmediği %20'sini tahmin
ettirsek bile **0,789** çıkıyor. Geleceği bilseydik (2 yıllık yağış + 1. yıl
NDVI) kazanç sadece +0,025. Hedefte ölçüm gürültüsü de yok (iki bağımsız yarı
r = 0,999).

Yani ulaşılabilir olanın **%87'sindeyiz**. Kalan boşluk model eksikliği değil —
250 metrelik bir alanın iki yıl sonra ne kadar toparlanacağı kısmen biyolojik
rastlantı.

Denenen ve elenen 20'den fazla yöntem, her ölçümün sayısı ve neden burada
durulduğu: **`MODEL_GUNLUGU.md`**

---

## Öncelik skoru

```
priority_score = 0,50 × n(iyileşme açığı)
               + 0,30 × n(eğim)
               + 0,20 × (1 − n(yola mesafe))
```

`n()` yangın içi min-max normalizasyon, sadece `predicted` hücreler üzerinden.
Ağırlıklar **sabit değil** — kullanıcı değiştirebiliyor, `metadata.json`'dan
okunuyor. Referans uygulama: `teslim/oncelik.py` (backend'deki
`PriorityCalculator.cs` bunun portu).

---

## Belgeler

| Dosya | Ne |
|---|---|
| `MODEL_GUNLUGU.md` | Denenen her şey, her sonuç, her karar ve gerekçesi — 15 bölüm |
| `VERI_SOZLUGU.md` | Alan alan veri sözlüğü, temizlik adımları, kalite notları |
| `../docs/data-contract.md` | Alan adı / tip / anlam için bağlayıcı sözleşme |
| `../sample-data/README.md` | Backend ve frontend için veri paketleri |

---

## Gereksinimler

```
python 3.10+
numpy pandas scipy scikit-learn
lightgbm xgboost joblib
rasterio shapely pystac-client planetary-computer
```

Boru hattı Microsoft Planetary Computer'a bağlanıyor (kimlik doğrulama
gerekmiyor). Ham veri çekimi saatler sürer; `sample-data/` altındaki çıktılar
zaten üretilmiş halde.
