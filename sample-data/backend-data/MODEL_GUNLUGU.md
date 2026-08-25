# ReGreen — Model Geliştirme Günlüğü

Denenen her şey, çıkan her sonuç, verilen her karar ve gerekçesi.
Başarısız denemeler dahil — özellikle onlar.

**Son güncelleme:** 21 Ağustos 2026
**Karar:** `ridge_v2` sabitlendi. Grup içi Spearman **+0,686**, 27/27 grup pozitif.

---

## İçindekiler

1. [Problem tanımı ve neden bu metrik](#1-problem-tanımı)
2. [Ölçüm protokolü — LOGO ve neden](#2-ölçüm-protokolü)
3. [Kronoloji — ne zaman ne denendi](#3-kronoloji)
4. [Model ailesi karşılaştırması](#4-model-ailesi-karşılaştırması)
5. [Hiperparametre araması](#5-hiperparametre-araması)
6. [Öznitelik denemeleri](#6-öznitelik-denemeleri)
7. [Farklı öğrenme yöntemleri](#7-farklı-öğrenme-yöntemleri)
8. [Topluluk ve seçim yanlılığı](#8-topluluk-ve-seçim-yanlılığı)
9. [Tavan analizi — neden burada durduk](#9-tavan-analizi)
10. [Kötü gruplar](#10-kötü-gruplar)
11. [Sonuçların somut karşılığı](#11-sonuçların-somut-karşılığı)
12. [Elenen her şeyin listesi](#12-elenen-her-şeyin-listesi)
13. [Sırada ne var](#13-sırada-ne-var)
14. [Yangın büyüklüğü eşiği](#14-yangın-büyüklüğü-eşiği)
15. [Ölçüm hatalarımız — düzeltilenler](#15-ölçüm-hatalarımız--düzeltilenler)

---

# 1. Problem tanımı

## Hedef değişken

```
kalan_acik = NDVI_yangın_öncesi − NDVI_2._yıl
```

İki yıl sonra bitki örtüsünün eski haline göre **ne kadarının hâlâ eksik** olduğu.
Yüksek = kötü = kendi kendine toparlanamamış = müdahale gerekiyor.

**Neden bu hedef:** etiketi biz üretmiyoruz. Uzman anketi, subjektif skor veya elle
etiketleme yok — doğanın 53 yangında iki yılda verdiği fiilî cevap.

## Metrik: neden grup içi Spearman

İki karar aldık, ikisi de sonucu doğrudan etkiliyor.

**1. Sıralama ölçüyoruz, değer değil.** Karar vericinin sorusu "bu hücrenin açığı
0,31 mi 0,34 mü" değil, "hangi 500 hektardan başlayayım". Bu yüzden **Spearman**
(sıra korelasyonu), MSE veya R² değil.

**2. Grup içi ölçüyoruz, havuzlanmış değil.** Grupların hedef ortalamaları
**0,123 ile 0,405** arasında değişiyor — üç kat fark. Bütün hücreleri tek torbaya
atarsak model *"hangi yangın bu"* sorusunu çözünce ödül alır, *"bu yangının içinde
neresi kötü"* sorusunu değil.

Farkı görmek için — **aynı tahminler, farklı metrik**:

| Metrik | Değer |
|---|---|
| Grup içi ortalama (raporladığımız) | **+0,686** |
| Havuzlanmış Spearman | +0,768 |
| Havuzlanmış R² | +0,547 |

Literatürdeki çoğu çalışma havuzlanmış sayıyı verir. İstesek "Spearman 0,77,
R² 0,55" yazabilirdik. Yazmıyoruz — zor ve dürüst olanı seçtik.

## İkinci metrik: top-%20 isabeti

"Bütçe alanın %20'sine yetiyor. Gerçekten en kötü %20'nin ne kadarını yakalıyoruz?"
Ürünün fiilî kullanımı bu.

---

# 2. Ölçüm protokolü

## LeaveOneGroupOut (LOGO)

27 mekânsal grup var. Her seferinde biri tamamen dışarı çıkarılıyor:

```
Sınav  1 : Manavgat çıkar → kalan 26 grupla eğit → Manavgat'ı sor
Sınav  2 : Marmaris çıkar → kalan 26 grupla eğit → Marmaris'i sor
...
Sınav 27
```

Gerçek hayatta olacak şey bu: yeni yangında model orayı hiç görmemiş olacak.

## Neden grup, yangın değil

Yan yana yanan yerler aynı dağ, aynı toprak, aynı ağaç. Birini eğitime birini teste
koyarsak model tahmin etmiş olmaz, **komşusundan hatırlamış** olur (mekânsal sızıntı).

Çakışan veya 10 km'den yakın yangınlar union-find ile tek grupta birleştirildi:
55 yangın → 37 grup, eğitim setinde **27 grup**.

**Sızıntının kapandığının kanıtı:** grup bölmesi R² +0,147, yangın bölmesi +0,142.
Neredeyse aynı. Kapanmasaydı grup bölmesi belirgin düşük çıkardı.

## GroupKFold(5) neden bırakıldı

En büyük grup (Manavgat) verinin **%37,2'si**. 5 katlı bölmede katlar çok dengesiz:

```
kat 1: test 5.977 satır (%37) — 1 grup     ← tek başına Manavgat
kat 2: test 2.499 satır (%15) — 5 grup
kat 3-5: benzer
```

1. kat skorun %37'sini tek yangına bağlıyordu. LOGO'ya geçildi.

## Selection bias kuralı

Bir kez düştük, sonra kural haline getirdik:

> Aynı ölçüm üzerinde hem arama yapıp hem sonuç raporlamak skoru şişirir.
> Seçim yapılıyorsa seçim de dış katın **içinde** olmalı.

Ölçtük: seçim yanlılığı **+0,0164** (bkz. [Bölüm 8](#8-topluluk-ve-seçim-yanlılığı)).
Bu yüzden "kazanç < +0,02 ise tabanla git" kuralını koyduk.

---

# 3. Kronoloji

| Tarih | Ne yapıldı | Sonuç |
|---|---|---|
| 18 Ağu | Veri temizliği (nodata, mükerrer, yeniden yanma) | 305.259 → 303.153 satır |
| 18 Ağu | Eğitim seti, 6 öznitelik | 16.074 satır, 27 grup |
| 19 Ağu | İlk model (RF), teslim paketi `rf_v1` | GroupKFold(5) ile +0,576 |
| 21 Ağu | LOGO'ya geçiş, 7 model ailesi | Ridge kazandı, +0,686 |
| 21 Ağu | `ndvi_dusus` eklendi | **+0,573 → +0,659** (HistGB) |
| 21 Ağu | Hiperparametre araması, iç içe CV | **negatif** (−0,014) |
| 21 Ağu | Öznitelik varyantları + topluluk | görünürde +0,704 |
| 21 Ağu | İç içe doğrulama | **gerçek +0,688**, yanlılık +0,016 |
| 21 Ağu | 5 farklı öğrenme yöntemi | en iyi +0,006 = gürültü |
| 21 Ağu | Tavan analizi | tavan **0,789**, 0,9 imkânsız |
| 21 Ağu | Hedef gürültüsü testi | **gürültü yok** (r = 0,999) |
| 21 Ağu | `ridge_v2` sabitlendi | +0,686, 27/27 |

---

# 4. Model ailesi karşılaştırması

7 aile, hepsi LOGO ile, ayar yok, 7 öznitelik.

| Model | rho | medyan | poz | en kötü | top-%20 | süre |
|---|---|---|---|---|---|---|
| **Ridge (doğrusal)** | **+0,686** | +0,744 | 27/27 | +0,140 | **%52,8** | **0,1 sn** |
| HistGB | +0,659 | +0,745 | 27/27 | **+0,174** | %52,8 | 7,2 sn |
| LightGBM | +0,655 | +0,730 | 27/27 | +0,165 | %52,5 | 2,6 sn |
| RF + eksik bayrağı | +0,653 | +0,711 | 27/27 | +0,161 | %50,0 | 83,9 sn |
| RF taban | +0,651 | +0,711 | 27/27 | +0,162 | %51,7 | 74,4 sn |
| RF + kök ağırlık | +0,642 | +0,729 | 27/27 | +0,106 | %51,7 | 85,5 sn |
| XGBoost | +0,631 | +0,683 | 27/27 | +0,150 | %49,7 | 3,3 sn |
| *dNBR (saha tabanı)* | *+0,377* | *+0,431* | *24/27* | *−0,155* | *%38,8* | — |

## Kararlar ve gerekçeleri

### ✅ Ridge seçildi

Ortalamada birinci, top-%20'de HistGB ile eşit, **90 kat hızlı**.

**Neden doğrusal model ağaçları geçiyor:** LOGO'da model her seferinde hiç
görmediği bir yangına tahmin yapıyor. Ağaçlar eğitim yangınlarının değer
aralıklarına göre bölme öğreniyor; yeni yangın o aralıkların dışına çıkınca
tıkanıyorlar. Doğrusal model düzgün ekstrapole ediyor.

**Ek gerekçe:** açıklanabilirlik. Karar destek sisteminde "bu hücre neden
öncelikli" sorusunun cevabı Ridge'de doğrudan katsayı. Ağaçta SHAP gerekiyor.

**Nüans:** HistGB en kötü grupta daha iyi (+0,174 vs +0,140). Fark 0,034,
gürültü sınırında. Ortalama + hız + açıklanabilirlik Ridge'i öne çıkardı.

### ❌ Grup ağırlıklandırma reddedildi

Manavgat'ın %37 olmasını 1 numaralı risk diye işaretlemiştim. **Ölçüm yalanladı:**

```
ağırlıksız   +0,651
kök ağırlık  +0,642   ↓
ham ağırlık  +0,537   ↓↓   (6 öznitelikli ölçümde)
```

Sebebi: küçük gruplar (33–70 satır) gürültülü, büyük ağırlık verince model
gürültü öğreniyor. Ağırlık aralığı 0,10–18,04 idi — 33 satırlık bir yangın
6.000 satırlık yangın kadar söz sahibi oluyordu.

### ❌ TAVAN (grup başına satır sınırı) reddedildi

Daha önce önerilmişti, ölçüldü: kırpılmış +0,468, kırpılmamış +0,569.
**Dengeleme bir MODEL kararı, VERİ kararı değil** — veriden satır atmak yerine
ağırlık sütunu üretildi (ve o da kullanılmadı, yukarıya bak).

---

# 5. Hiperparametre araması

## İç içe (nested) CV kurulumu

```
DIŞ kat : LOGO — 27 grup, sadece RAPOR eder
   içeride: o grup hariç veride GroupKFold(4) ile parametre ARAR
            → en iyi parametreyi bulur → eğitir → dışarıya tahmin verir
```

**4.320 eğitim** (27 × 40 deneme × 4 iç kat).

## Sonuç: negatif

```
Aşama 1 en iyisi   +0,6860
Ridge ayarlı       +0,6721   (−0,0139)
```

Kendi kuralımız gereği (kazanç < +0,02) **ayarsız model** seçildi.

## İki öğretici detay

**1. Etkileşim terimleri 27 katın 27'sinde de reddedildi.** "Dik yamaç + ağır
yangın birlikte, ayrı ayrı toplamlarından daha mı kötü?" diye sormuştuk.
Cevap net: **hayır**, etkiler toplanabilir. Bu, ağaçların neden fark
yaratamadığını da açıklıyor — yakalayacak etkileşim yok.

**2. İç CV `alpha ≈ 408` seçti, dış ölçümde daha kötü çıktı.** Varsayılan
`alpha = 1` daha iyi. İç aramanın tercihi dışarı taşınmıyor. İç içe CV'nin var
oluş sebebi tam olarak bu — tek katmanlı CV yapsaydık şişmiş sayıyı gerçek
sanacaktık.

---

# 6. Öznitelik denemeleri

## ✅ `ndvi_dusus` — projenin en büyük tek kazancı

```
ndvi_dusus = yangın öncesi NDVI − yangın hemen sonrası NDVI
```

Yangının hemen ardındaki **fiilî bitki kaybı**. Ham veride vardı, eğitim setine
hiç konmamıştı.

| | rho | poz | en kötü | top-%20 |
|---|---|---|---|---|
| 6 öznitelik | +0,573 | 27/27 | +0,104 | %45,9 |
| **7 öznitelik** | **+0,659** | **27/27** | **+0,174** | **%52,8** |

**+0,086** — bütün model denemelerinin toplamının on katı.

### Sızıntı değil, kanıtı

Tek ölçüt: *yangından ~2 hafta sonra bu değer elimizde olur mu?*
`ndvi_oncesi` ve `ndvi_sonrasi` ikisi de uydudan geliyor → evet.
Bilmediğimiz `ndvi_yil2`, o da hedefte.

### Model gerçekten katkı veriyor mu

```
ndvi_dusus tek başına  +0,594
modelle birlikte       +0,659
model katkısı          +0,065   ✓
```

### "dNBR zaten şiddeti ölçmüyor mu?"

Ölçüyor ama farklı bir şeyi: dNBR kızılötesi bandındaki **yanık izini**,
`ndvi_dusus` ise **fiilen kaybolan bitkiyi**. Üst üste bilgi katıyorlar —
dNBR zaten modeldeyken `ndvi_dusus` +0,086 ekliyor.

Öznitelik önemi (3 farklı yöntemde de birinci):

```
HistGB permütasyon   %55,7
Ridge katsayı        %46,4
RF dahili            %43,4
```

## ❌ `ndvi_oncesi` — reddedildi, taftolojik

Eklendiğinde skoru **daha da** yükseltiyordu (+0,103). Kullanmadık:

```
ndvi_oncesi tek başına  +0,679
modelle birlikte        +0,675
model katkısı           −0,004   ✗
```

**Model hiçbir şey katmıyor.** Modeli silip hücreleri yangın öncesi NDVI'ye
göre sıralasan aynı sonucu alırsın.

Sebebi matematiksel: hedef `ndvi_oncesi − ndvi_yil2`. Özniteliği hedefin
içinden veriyoruz. *"Önce çok bitki vardı, o yüzden açık büyük"* bir tahmin
değil, **aritmetik**.

Ayrıca `ndvi_dusus` ile birlikte konunca en kötü grup +0,174 → **−0,030**
düşerek 27/27 tutarlılığı bozuyor.

> Jüride biri bunu fark ederse projeyi bitirir. Ölçtük, gördük, reddettik.

## ❌ Arazi örtüsü sınıfı (orman / çalılık / otlak)

Üç yoldan denendi:

| | rho | fark |
|---|---|---|
| taban | +0,6588 | — |
| + ikili sütunlar | +0,6554 | −0,0034 |
| + kategorik sınıf | +0,6556 | −0,0032 |
| + sadece "ağaçlık mı" | +0,6593 | +0,0005 |

**Sebep:** `agac_orani_y` zaten sınıfın sürekli hali:

```
Ağaçlık        %93,3
Otlak/çalılık  %16,6
Tarım           %6,6
```

**Kesin kanıt:** ağaç örtüsü sütunlarını tamamen çıkarıp yerine sınıfı koyunca
da hiçbir şey değişmiyor (+0,6346 → +0,6329). Sınıf tek başına bile bilgi
taşımıyor; ağaç oranı ise +0,024 katıyor.

**Ek sebep:** yangın içinde baskın sınıfın payı ortalama **%85,8** — sınıf
yangın içinde neredeyse sabit. Metrik grup içi olduğu için sabit sütun katkı
veremez.

Ekolojik sezgi (*"makilik dipten sürer, kızılçam sürmez"*) **doğru** ama o
bilgi zaten `agac_orani` içinde.

## ❌ Yanmamış alana mesafe (tohum kaynağı)

Ekolojide yangın sonrası toparlanmanın klasik belirleyicisi. Hesaplandı
(medyan 0,56 km, %90 dilim 1,27 km, maks 3,55 km):

```
Ridge + tohum_mesafe_km   +0,6817   (taban +0,6823)
grup içi korelasyon       +0,058
işaret tutarlılığı        17/27
```

**Sıfır katkı, işaret bile tutarsız.** Muhtemel sebep: 250 m hücrede ve bizim
yangın boyutlarında hücrelerin çoğu zaten kenara yakın — medyan 560 metre,
tohum yayılma mesafesinin içinde.

## ❌ Yangın içi z-skor öznitelikleri

| Model | fark |
|---|---|
| Ridge + z | **−0,102** |
| HistGB + z | −0,056 |
| HistGB + z + hedef z | **+0,029** ✓ |

Ridge'i mahvetti. Sebebi: z-skor mutlak bilgiyi siliyor, doğrusal model zaten
katsayılarla ölçekleme yapabildiği için kazanç yok, sadece gürültü.
HistGB'de hedef z-skoruyla **birlikte** kullanıldığında işe yaradı.

## ❌ Komşu hücre ortalamaları

Marjinal (+0,002), üstelik en kötü grubu bozuyor (+0,141 → +0,099).
Mekânsal bağlam düşünüldüğü kadar bilgi taşımıyor.

## ❌ Bakı (aspect)

```
ham derece   +0,006
sin/cos      −0,005
```

İkisi de gürültü. Dairesel olma sorunu değilmiş — **gerçekten sinyal yok.**

## ❌ Uzun dönem yağış — zararlı

**−0,110.** Sebep ölçüldü: yangın **içindeki** değişimi 0,076, yani neredeyse
sabit. Grup içi metrikte sabit sütun bilgi taşımaz, sadece modele
ezberlenecek gürültü verir.

## ❌ Su mesafesi, yerleşim mesafesi

+0,006 ve −0,011. İkisi de gürültü.

## ⚠️ Sızıntı sayılanlar (ölçülmedi bile, ilkesel)

| Alan | Sebep |
|---|---|
| `ndvi_yil2` | Hedefin bileşeni |
| `ndvi_yil1` | Yangından 1 yıl sonra ölçülür, tahmin anında yok |
| `yagis_sonrasi_2yil_mm`, `yagis_anomali` | Gelecek yağış, tahmin anında bilinmiyor |

---

# 7. Farklı öğrenme yöntemleri

Model ailesi değil, **öğrenme biçimi** değiştirildi. 16 varyant.

| Yöntem | rho | fark | top-%20 |
|---|---|---|---|
| Ridge ← yangın içi sıra | +0,6916 | +0,006 | %52,5 |
| Lojistik regresyon (en kötü %20) | +0,6896 | +0,004 | %52,3 |
| Huber | +0,6882 | +0,002 | %52,7 |
| Kantil %80 | +0,6862 | +0,000 | %52,8 |
| **Ridge ham hedef (TABAN)** | **+0,6860** | — | **%52,8** |
| ElasticNet | +0,6857 | −0,000 | %52,7 |
| LightGBM monoton ← sıra | +0,6757 | −0,010 | %51,8 |
| HistGB monoton | +0,6715 | −0,015 | %52,9 |
| LightGBM monoton | +0,6706 | −0,015 | **%53,1** |
| HistGB ham | +0,6588 | −0,027 | %52,8 |
| LightGBM sınıflandırıcı | +0,5942 | −0,092 | %48,8 |
| **LGBMRanker (lambdarank)** | **+0,5695** | **−0,117** | %43,6 |

## İki beklenmedik başarısızlık

### LambdaRank çöktü (−0,117)

Mantık sağlamdı: *"sıralama ölçüyorsak sıralama kaybıyla eğitelim."* İşe yaramadı.

**Sebep:** LambdaRank NDCG optimize eder, o da en tepedeki birkaç öğeye ağırlık
verir. Bizde 34 sorgu (yangın) var, aşırı öğreniyor. Ayrıca biz **bütün
sıralamayı** umursuyoruz, sadece tepeyi değil.

### Sınıflandırma çöktü (−0,092)

Mantık: *"ürün metriği zaten en kötü %20 seçmek, direkt onu öğrenelim."*

**Sebep:** hedefi ikiliye çevirmek bilgi atmak demek — %79'luk hücre ile
%21'lik hücre aynı sınıfta oluyor.

> **Ders:** metriğe birebir uyan kayıp fonksiyonu, her zaman daha iyi model
> vermiyor.

## Kısmen işe yarayan: monotonluk kısıtı

İlişkilerin yönünü biliyoruz (hepsi pozitif: eğim artınca kötüleşir, ağaç
örtüsü artınca kötüleşir). Modele dayatınca:

```
HistGB    +0,6588 → +0,6715   (+0,013)
LightGBM  top-%20 %53,1  ← bütün listenin birincisi
```

Görülmemiş yangına genelleme düzeliyor. Ama Ridge'i geçemedi — çünkü **Ridge
zaten doğası gereği monoton.**

---

# 8. Topluluk ve seçim yanlılığı

## Görünürdeki sonuç

16 varyant × 2 model + 3 ağırlık denendi, en iyisi seçildi:

```
Ridge %50 + HistGB %50  →  +0,7042
```

Tabana göre +0,022. Sevindirici görünüyordu.

## Ama seçim, ölçtüğümüz sınavın cevaplarına bakarak yapılmıştı

Doğrulama kuruldu: **seçimin kendisi dış katın içine alındı.**

```
DIŞ kat (LOGO, 27) → sadece RAPOR
   iç kat (GroupKFold 4) → 320 kombinasyon (8×8 varyant × 5 ağırlık)
                           içinden EN İYİSİ SEÇİLİR
   seçilen yapıyla 26 grupla eğitilir → dışarıdakine tahmin
```

## Sonuç

| Ölçüm | rho | en kötü | top-%20 |
|---|---|---|---|
| 1) Sabit taban — hiç seçim yok | +0,6823 | +0,142 | %53,0 |
| 2) **Yanlı** ölçüm | **+0,7042** | +0,153 | %54,4 |
| 3) **Dürüst** ölçüm | **+0,6879** | +0,143 | %54,8 |

```
Seçim yanlılığı  (2 − 3) :  +0,0164   ← sahte kazanç
Gerçek kazanç    (3 − 1) :  +0,0056
```

**Kazancın dörtte üçü sahteydi.** Topluluğun gerçek üstünlüğü +0,006 —
27 grupta ölçüm gürültüsünün altında.

Seçimler kararlıydı (27 katın **24'ünde ağırlık %50**), yani etki gerçek ama
27 grupla güvenle ölçemeyeceğimiz kadar küçük.

**Karar:** topluluk kullanılmıyor. Sade Ridge, aynı performans, çok daha basit
ve açıklanabilir.

---

# 9. Tavan analizi

**Soru:** 0,8–0,9 mümkün mü?
**Cevap:** hayır, ve bu tahmin değil ölçüm.

Üç ayrı üst sınır ölçüldü.

| Ölçüm | rho | Ne demek |
|---|---|---|
| **Şimdiki durum** (görülmemiş yangın) | **+0,682** | gerçek performans |
| Aynı yangın içinde eğit-test *(kasten sızıntılı)* | **+0,789** | ← **üst sınır** |
| ~~Tam ezber (in-sample)~~ | ~~+0,770~~ | **hatalıydı — aşağıya bak** |
| Geleceği bilseydik (2 yıllık yağış + 1. yıl NDVI) | +0,707 | kâhin senaryosu |

## 1. Kopya çeksek bile 0,79

Model aynı yangının %80'iyle eğitilip kalan %20'si tahmin edildi — hiç genelleme
gerekmiyor, aynı yangının kendi ilişkilerini öğreniyor. Mekânsal sızıntı var,
kasten. **0,789.**

## 2. ⚠️ DÜZELTME — "ezber testi" hatalıydı

İlk yazdığımda *"modele cevap anahtarını verdik, yine 0,77'de kaldı, demek
7 özniteliğin sınırı bu"* demiştim. **Yanlıştı.**

O test varsayılan HistGB ile yapılmıştı — varsayılan HistGB düzenlileştirilmiş
(100 iterasyon, 31 yaprak, L2 cezası). Yani ezberlemeye *çalışmıyor* zaten.
Ölçtüğüm şey bilginin sınırı değil, **o modelin düzenlileştirme ayarıydı.**

Kapasiteyi açınca ne oluyor, ölçtük:

| Model | in-sample rho |
|---|---|
| HistGB varsayılan *(ilk testte kullanılan)* | +0,770 |
| **HistGB, kapasite açık** (2000 iter, 255 yaprak, L2=0) | **+1,0000** |
| **Tek karar ağacı, sınırsız derinlik** | **+1,0000** |
| **1-en yakın komşu** (saf ezber) | **+1,0000** |
| RandomForest, `min_samples_leaf=1` | +0,9507 |
| Ridge (doğrusal — ezberleyemez) | +0,6865 |

**"Ezber tavanı" diye bir şey yok.** Yeterli kapasiteli her model in-sample'da
1,00 verir — sadece satırları ezberler. Ezber testi **bilgiyi ölçmez, kapasiteyi
ölçer**. Bu satır tavan kanıtı olarak kullanılamaz.

> RandomForest'ın 0,95'te kalması da öğretici: bootstrap örneklemesi yüzünden her
> ağaç satırların ~%63'ünü görüyor, ortalama alınca tam ezber bozuluyor.

**Anlamlı tavan hangisi:** aynı yangın içi bölme testi (aşağıdaki 1. madde).
Orada model yangının %80'iyle eğitilip **görmediği %20'sini** tahmin ediyor —
gerçek tahmin, ezber değil. **0,789** oradan geliyor ve o sayı geçerli.

## 3. Geleceği bilmek neredeyse hiçbir şey katmıyor

Yangından sonraki 2 yılın yağışı ve 1. yıl NDVI'si verildi — imkânsız bir
avantaj. Kazanç sadece **+0,025**.

> Bu en önemlisi: **eksik olan şey hava durumu değil.** "Kuraklığı bilseydik
> daha iyi tahmin ederdik" hipotezi ölçüldü ve reddedildi.

## 4. Hedefte ölçüm gürültüsü de yok

Hipotez: NDVI ölçümü gürültülüyse cetvel titriyor demektir, daha çok gün alarak
temizlenebilir. Şu an 70 günlük pencerede en az bulutlu **6 günün medyanı**.

Test: aynı pencerenin **iki bağımsız yarısı** (3+3 gün) birbirini ne kadar tutuyor?

| | EGE_2021_08 | AKD_2023_02 |
|---|---|---|
| 6 gün vs 18 gün, Pearson r | 0,9999 | 0,9983 |
| **İki bağımsız yarı, r** | **0,9996** | **0,9981** |
| Fark std (NDVI birimi) | 0,0044 | 0,0059 |

**Gürültü yok.** Ölçüm sapması 0,004–0,006; hedefin tipik değeri 0,35. Gürültü
sinyalin binde biri. Daha çok gün almanın faydası olmayacaktı.

*Çekince: bu test rastgele gün-günlük gürültüyü ölçer. Bütün günlerde ortak
sistematik sapma varsa yakalamaz — ama onu da daha çok gün almak düzeltmezdi.*

## Sonuç: neden 0,79'da tıkanıyor

Elenenler:

- ❌ Ölçüm gürültüsü (r = 0,999)
- ❌ Hava / kuraklık (kâhin testi +0,025)
- ❌ Model kapasitesi (20+ yöntem 0,05 aralığında; ezber testi geçersiz, bkz. düzeltme)
- ❌ Mekânsal bağlam (komşu ve tohum mesafesi sıfır)

Geriye kalan tek açıklama: **250 metrelik bir hücrenin 2 yıl sonra ne kadar
toparlanacağı kısmen biyolojik rastlantı.** Hangi tohum tuttu, hangi kök sürdü,
hangi keçi otladı. Uydudan görülmez.

```
Model                  +0,686
Aynı yangın içi tavan  +0,789
→ ulaşılabilir olanın  %87'sindeyiz
```

---

# 10. Kötü gruplar

En kötü üç: `MAR_2023_01` (+0,140), `AKD_2021_06` (+0,237), `AKD_2023_02` (+0,411).
En iyiler ~+0,88.

## ❌ H1 — Hedef çeşitliliği düşük? Zayıf

`MAR_2023_01`'in hedef std'si en düşük (0,0325). Ama `EGE_2024_05`'in std'si daha
da düşük (0,0293) ve rho'su **+0,677**. Korelasyon +0,218 (p = 0,275), anlamsız.

## ❌ H2 — Dağılım dışı? Reddedildi

`MAR_2023_01` verinin sadece **%0,8'i** dağılım dışı — en düşüklerden.
rho ile dağılım dışılık korelasyonu −0,152 (p = 0,45).

## ✅ H3 — Bölgesel yalnızlık: en güçlü açıklama

```
Bölgesinde YALNIZ olan  (1 grup) : rho ortalama +0,140
Bölgesel desteği olan  (26 grup) : rho ortalama +0,707
```

Marmara'da tek grubumuz var. Dışarı çıkınca eğitimde **hiç Marmara kalmıyor**.

> Uyarı: n = 1. Tek gözlem, istatistiksel iddia olamaz.

## Altında yatan asıl şey

`MAR_2023_01` içindeki ham korelasyonlar:

```
agac_orani     +0,023   (tüm veride +0,542)
dnbr           +0,075   (tüm veride +0,371)
egim_derece    −0,044   (tüm veride +0,262)
ndvi_dusus     +0,274   (tüm veride +0,645)
```

**Hepsi düz.** O yangının içinde ranklanacak yapı yok.

Sebebi görünüyor: hafif bir yangın. `kalan_acik` ortalaması **0,137** (genel 0,346),
dNBR 0,401 (genel 0,518), eğim 10,9° (genel 14,3°). Alçak, yumuşak eğimli, hafif
yanmış ve **her yeri iyi toparlanmış**.

> Model başarısız olmuyor — **o yangında bulunacak bir şey yok.** Her yer
> iyileştiyse "nereden başlayalım" sorusunun zaten cevabı yok.

## Neden düzeltilemiyor

Marmara'da 300 ha eşiğini geçen başka yangın pratikte yok — bir tane ~4.000 ha var
(o da bizde), gerisi 500 / 450 / 300 / 80 ha diye hızla düşüyor. Arasak 1–2 tane
bulunur, bölgesel desteği anlamlı biçimde değiştirmez.

---

# 11. Sonuçların somut karşılığı

+0,686 tek başına bir şey anlatmıyor. Anlaşılır karşılıkları:

## İkili karşılaştırma — en sezgisel hali

> *"Aynı yangından iki hücre veriyorum. Hangisi 2 yıl sonra daha kötü olacak?"*

```
Yazı-tura (rastgele)      50,0%
dNBR (sahadaki yöntem)    62,4%
ReGreen modeli            78,7%   ←
Mükemmel bilgi           100,0%
```

## Bütçe senaryosu

> *"Bütçem alanın %X'ine yetiyor. En kötü %X'in ne kadarını yakalarım?"*

| Bütçe | Rastgele | dNBR | **ReGreen** | Mükemmel |
|---|---|---|---|---|
| %10 | %10 | %26 | **%41** | %100 |
| %20 | %20 | %38 | **%57** | %100 |
| %30 | %30 | %47 | **%67** | %100 |
| %50 | %50 | %62 | **%81** | %100 |

*(Hücre sayısına göre ağırlıklı. Raporladığımız %52,8 ise 27 grubun ağırlıksız
ortalaması — temkinli olan.)*

## Somut yangın: AKD_2021_05 (Antalya, 442 hücre = 2.762 ha)

Bütçe 88 hücre (550 ha):

```
Model seçimi     → gerçekten en kötü 88'in 60'ı içinde  (%68)
dNBR ile         → 51 tane
Rastgele         → ~17 tane
```

Ve önemli detay:

```
Kaçırdığımız 28 hücrenin ortalama açığı : 0,417
Yakaladığımız 60 hücrenin ortalaması    : 0,430
```

**Neredeyse aynı** — model felaket hataları yapmıyor, kaçırdıkları sınırda kalan
hücreler.

Gerçekte en kötü olan hücre, model sıralamasında ortalama **en üstteki %18'lik
dilimde** çıkıyor.

## Dürüst bir sınır: hasar kapsama

> *"Seçtiğim %20, toplam iyileşme açığının yüzde kaçını kapsıyor?"*

| Bütçe | Rastgele | dNBR | ReGreen | **Mükemmel** |
|---|---|---|---|---|
| %20 | %20 | %24 | %25 | **%27** |
| %50 | %50 | %54 | %59 | **%61** |

Fark küçük görünüyor ama **mükemmel bilgi bile %27 yapabiliyor** — hasar alana
yayılmış, keskin "felaket bölge" ayrımı yok.

```
%20 bütçede : (25−20)/(27−20) = mümkün olan kazancın %71'i
%50 bütçede : (59−50)/(61−50) = mümkün olan kazancın %82'si
```

---

# 12. Elenen her şeyin listesi

| Denenen | Sonuç | Karar |
|---|---|---|
| 7 model ailesi | 0,05 aralığında | Ridge |
| Hiperparametre araması (4.320 eğitim) | −0,014 | ayarsız |
| Etkileşim terimleri | 27/27 reddetti | yok |
| Grup ağırlıklandırma (ham / kök / tavan) | −0,009 … −0,114 | yok |
| Satır tavanı (TAVAN=400) | −0,101 | yok |
| 8 öznitelik varyantı | en iyi +0,006 | taban |
| Topluluk (dürüst ölçüm) | +0,006 | yok |
| LambdaRank | −0,117 | yok |
| Sınıflandırma (en kötü %20) | −0,092 | yok |
| Monotonluk kısıtı | +0,013 (ağaçta) | Ridge zaten monoton |
| Huber / Kantil / ElasticNet | ≤ +0,002 | Ridge |
| **`ndvi_dusus`** | **+0,086** | ✅ **eklendi** |
| `ndvi_oncesi` | +0,103 ama taftolojik | ❌ reddedildi |
| Arazi örtüsü sınıfı | −0,003 | ❌ |
| Tohum kaynağı mesafesi | −0,001 | ❌ |
| Yangın içi z-skor (Ridge) | −0,102 | ❌ |
| Komşu ortalamaları | +0,002, en kötü grubu bozuyor | ❌ |
| Bakı (ham ve sin/cos) | ±0,006 | ❌ |
| Uzun dönem yağış | −0,110 | ❌ |
| Su / yerleşim mesafesi | ±0,01 | ❌ |
| Hedef gürültüsü temizleme | gürültü yok (r=0,999) | ❌ gereksiz |

---

# 13. Sırada ne var

Model tarafı bitti. Buradan sonrası **veri toplama** işi:

| Fikir | Beklenen | Maliyet |
|---|---|---|
| Daha çok yangın | Genelleme açığını kapatır, tavan yine 0,79 | sürekli |
| 100 m çözünürlük | 250 m'de 6,25 ha ortalanıyor; ilişkiler keskinleşebilir | boru hattını baştan koşturmak, günler, sonuç garantisiz |
| Toprak verisi (RUSLE K faktörü) | Erozyon bileşenini gerçek modele çevirir | veri kaynağı bulunmalı |
| Ağaç türü / meşcere yaşı | En umut verici yeni bilgi | OGM'de var, açık veri değil |

Bunların hiçbiri hızlı kazanç değil.

## 12 aylık yeniden değerlendirme modeli

Ayrı bir fikir: ürünün 12 aylık takip özelliği için `ndvi_yil1` **kullanılabilir**
(o noktada elimizde olur). v1 için sızıntı, ama "1 yıl sonra güncelleme modeli"
için meşru ve muhtemelen güçlü. Ayrı bir model olarak kurulabilir.

---

# Ekler — üretilebilir dosyalar

Bütün ölçümler tekrar üretilebilir:

| Dosya | Ne yapar |
|---|---|
| `model_egit.py` | 3 aşamalı ana eğitim (aile → ayar → final) |
| `deney_2_oznitelik.py` | Aday öznitelik taraması |
| `deney_3_durustluk.py` | `ndvi_dusus` / `ndvi_oncesi` meşruiyet testi |
| `deney_4_arazi.py` | Arazi örtüsü sınıfı testi |
| `deney_5_gelistirme.py` | Öznitelik varyantları + topluluk |
| `deney_6_dogrulama.py` | İç içe seçim, yanlılık ölçümü |
| `deney_7_kotu_gruplar.py` | Kötü grup tanılaması (H1/H2/H3) |
| `deney_8_yontemler.py` | 16 farklı öğrenme yöntemi |
| `deney_9_somut.py` | Sonuçların anlaşılır karşılığı |
| `deney_10_tavan.py` | Tavan analizi |
| `deney_11_hedef_gurultu.py` | Hedef ölçüm gürültüsü |
| `deney_12_ezber_ve_esik.py` | Ezber kapasitesi + yangın büyüklüğü eşiği |
| `ReGreen_model_egitimi.ipynb` | Colab defteri (aynı akış) |

---

# 14. Yangın büyüklüğü eşiği

## Neden 300 hektar

İki bağımsız sınır var ve ikisi de fiziksel.

### 1. MODIS göremiyor (yangın keşfi)

Yangınları MODIS MCD64A1 yanık alan ürününden buluyoruz. Çözünürlüğü **500 m**,
yani **1 piksel = 25 hektar**.

| Yangın | MODIS pikseli |
|---|---|
| 300 ha | 12,0 |
| 100 ha | 4,0 |
| 50 ha | 2,0 |
| 25 ha | 1,0 |
| **4 ha** *(Türkiye ortalaması)* | **0,2** |

12 pikselin altında yanık alan tespiti güvenilmez oluyor — tek pikselde
karışık sinyal, sınır belirsiz.

### 2. Sıralanacak hücre kalmıyor (asıl kısıt)

250 m ızgarada **1 hücre = 6,25 hektar**:

| Yangın | Hücre |
|---|---|
| 3.000 ha | 480 |
| 1.000 ha | 160 |
| 500 ha | 80 |
| **300 ha** | **48** |
| 200 ha | 32 |
| 100 ha | 16 |

Ve bunların hepsi etiketlenemiyor — bulut, nodata, yeniden yanma eliyor.
Fiilî durum:

| Alan bandı | Yangın | Ort. etiketli hücre | Eğitime giren |
|---|---|---|---|
| 300–500 ha | 16 | **28** | 6/16 |
| 500–1.000 ha | 12 | 62 | 8/12 |
| 1.000–5.000 ha | 18 | 221 | 12/18 |
| 5.000+ ha | 9 | 1.355 | 8/9 |

**300–500 ha bandındaki 16 yangının sadece 6'sı eğitime girebildi**, ortalama
28 etiketli hücreyle. 100 ha'lık bir yangında bu sayı 5–8'e düşerdi —
istatistiksel olarak anlamsız.

## Elimizdeki en küçükler

| Yangın | Alan | Yanık hücre | Etiketli |
|---|---|---|---|
| **EGE_2021_08** | **315 ha** | 67 | **49** |
| IAN_2023_01 | 336 ha | 223 | 34 |
| EGE_2024_11 | 341 ha | 55 | 19 |
| EGE_2024_10 | 345 ha | 53 | 19 |
| GDA_2021_01 | 370 ha | 327 | **8** |
| AKD_2021_11 | 374 ha | 117 | **9** |

`EGE_2021_08` en küçüğümüz: **315 hektar**, 49 etiketli hücre — ve grup içi
rho'su **+0,879**, listenin en iyilerinden. Yani küçük olması modeli
zorlamıyor; yeterli hücre kaldığı sürece çalışıyor.

`GDA_2021_01` (8 etiketli) ve `AKD_2021_11` (9 etiketli) ise eğitime hiç
giremedi — bizim `n ≥ 8` ve `hedef nunique ≥ 3` filtremizin altında kaldılar.

## Grup boyutu modeli etkiliyor mu

| Grup boyutu | Grup sayısı | rho ortalama |
|---|---|---|
| 0–60 hücre | 6 | **+0,804** |
| 60–150 hücre | 6 | +0,659 |
| 150–500 hücre | 9 | +0,563 |
| 500+ hücre | 6 | +0,780 |

Düzenli bir ilişki **yok** — küçük gruplar en iyi ortalamayı veriyor.
(Küçük grupta Spearman daha oynak olduğu için hem en iyi hem en kötü uçlar
oradan çıkabiliyor.)

**Sonuç:** 300 ha sınırı model performansından değil, **veri üretilebilirliğinden**
geliyor. Altına inersek yangın başına ranklanacak hücre kalmıyor.

## Zaten gerek de yok

4 hektarlık bir yangında *"hangi parselden başlayalım"* diye bir soru yok —
ekip gider, bir günde biter. Bizim çözdüğümüz problem **kaynak tahsisi**, o da
ancak ekip/bütçe/fidan yetmediğinde ortaya çıkıyor.

Ve alan olarak kaybımız yok: veri setimizde **en büyük 9 yangın toplam alanın
%71'i**, 300–500 ha bandındaki 16 yangın ise sadece **%3'ü**.

---

# 15. Ölçüm hatalarımız — düzeltilenler

Kayda geçsin diye. Üçü de benim hatamdı, üçü de ölçümle yakalandı.

| Hata | Nasıl anlaşıldı | Düzeltme |
|---|---|---|
| **"Ezber testi 0,77, demek bilginin sınırı bu"** | Varsayılan HistGB düzenlileştirilmiş; kapasite açılınca 1,00 çıkıyor | Bölüm 9'da düzeltildi, tavan kanıtı olarak kullanılmıyor |
| **"Manavgat %37, ağırlık vermeliyiz"** | Ölçüldü: ağırlık verdikçe kötüleşiyor | Ağırlık kullanılmıyor |
| **"Hedefte gürültü var, daha çok gün alalım"** | İki bağımsız yarı r = 0,999 | Hipotez kapatıldı, 53 yangını yeniden çekmekten kurtulduk |
| **"AKD_2020_01'de arazi sınıflandırması hatalı"** | Denetim: %83'ü nodata'ymış | nodata NaN'a çevrildi, ρ −0,341 → +0,513 |
| **"+0,704'e çıktık"** | İç içe doğrulama: +0,0164'ü seçim yanlılığı | Gerçek sayı +0,688 |
