# Hüküm katmanı — veri sözleşmesi

**Sorumlu:** Buğra (AI) · **Sürüm:** 1.3 · **Dil:** Türkçe

Bu katman, hücre panelinde sayı tablosunun üstünde duracak **düz Türkçe
hükmü** üretir. Mevcut hiçbir şema değişmiyor — bütün çıktı **yeni yan
dosyalarda**.

> `*_hucreler.csv`, `manifest.json` ve `metadata.json` dosyalarına
> **dokunulmadı.** Var olan sözleşme aynen geçerli.

---

## Temel kavram: hüküm ≠ öncelik

İki ayrı eksen, iki ayrı soru:

| | Soru | Neye bağlı | Ağırlık değişince |
|---|---|---|---|
| `priority_score` | **Ne zaman** gidilecek? | ağırlıklara | **değişir** |
| `hukum` | **Gidince ne** yapılacak? | yalnızca ölçümlere | **değişmez** |

Hükmün ağırlıktan bağımsız olması bilinçli: kullanıcı kaydırıcıyı
oynattığında backend'in hükmü yeniden hesaplaması **gerekmiyor**. Bir kez
üretilir, sabit kalır.

---

## İki katman

| Katman | Ölçek | Ne cevaplıyor | Nasıl üretiliyor |
|---|---|---|---|
| **1 · Yangın özeti** | bölge seçilince | "Bu yangında genel durum ne?" | 53 paragraf, statik |
| **2 · Hücre hükmü** | kareye tıklanınca | "Bu karede ne yapılacak?" | kural, hücre başına |

## Dosyalar

```
sample-data/backend-data/
    {fire_id}_hukumler.csv     53 dosya · hücre başına bir satır   (Katman 2)
    hukum_sozlugu.json         sabit metinler, eşikler (2,6 KB)
    yangin_ozetleri.json       53 yangının sayı bloğu  (77 KB)     (Katman 1)
    yangin_metinleri.json      53 paragraf             (45 KB)
```

### `{fire_id}_hukumler.csv`

`cell_id` üzerinden `{fire_id}_hucreler.csv` ile birebir eşleşir.

| Alan | Tip | Açıklama |
|---|---|---|
| `cell_id` | metin | birleştirme anahtarı |
| `hukum` | kod | 7 değerden biri — aşağıdaki tablo |
| `ek_kosullar` | metin | `\|` ile ayrılmış kod listesi, boş olabilir |
| `toparlanma_orani` | 0–1 | yangın öncesi örtünün tahmini geri gelen oranı |
| `tur_onerisi` | metin | virgüllü tür listesi, **boş olabilir** |
| `tetikleyen` | metin | hükmü hangi ölçümün tetiklediği — denetim izi |
| `ozet` | metin | panelin üstünde gösterilecek kısa hüküm (1–2 cümle) |
| `ayrinti` | metin | detay bölümündeki gerekçe ve ek koşullar |
| `zamanlama_notu_var` | bool | sözlükteki `zamanlama_notu` bu hücrede gösterilsin mi |

**Boyut:** toplam 16,5 MB, en büyüğü Manavgat 3,9 MB.
Metni taşımayan sürüm için `python ai/teslim/hukum.py --yaz --kompakt` →
toplam 3,0 MB, en büyüğü 0,72 MB. O sürümde `ozet` ve `ayrinti` yok;
tüketen taraf metni sözlükteki şablonlardan kurar.

---

## Hüküm kodları

Kurallar sırayla denenir, **ilk uyan kazanır**.

| Kod | Başlık | Tetikleyen | Hücre |
|---|---|---|---|
| `KAPSAM_DISI` | Ağaçlandırma kapsamı dışı | arazi = Tarım/Yerleşim/Su/Sulak | 4.998 |
| `SAHA_KONTROL` | Saha kontrolü gerekli | `no_data` | 5 |
| `IZLE` | İzle, müdahale etme | `low_severity` | 15.280 |
| `EROZYON_ONCE` | Önce erozyon kontrolü | toparlanma < 0,35 **ve** eğim ≥ 25° | 824 |
| `DIKIM_ADAYI` | Aktif dikim adayı | toparlanma < 0,35 | 4.838 |
| `ONCELIGE_GORE` | Öncelik sırasına göre değerlendir | 0,35 ≤ toparlanma < 0,50 | 8.875 |
| `GENCLESME_IZLE` | Doğal gençleşmeyi izle | toparlanma ≥ 0,50 | 2.343 |

Toplam **37.163** — her hücre tam olarak bir hüküm alır, boş kalan yok.

> `SAHA_KONTROL` yalnızca 5 çıkıyor ama `no_data` 22 hücrede var: kalan 17
> tanesi tarım/su/yerleşim üzerinde olduğu için `KAPSAM_DISI` önce
> yakalıyor. Sıra kuralı böyle çalışıyor.

## Ek koşul kodları

Hükmün üstüne biner, sıfır veya birkaç tane olabilir.

| Kod | Eşik | Hücre |
|---|---|---|
| `AGIR_YANMIS` | şiddet sınıfı yüksek / orta-yüksek | 9.886 |
| `ESKIDEN_ORMAN_DEGIL` | arazi ≠ Ağaçlık | 9.648 |
| `ERISIM_ZOR` | yola mesafe ≥ 2 km | 2.813 |
| `DUSUK_GUVEN` | bölgede referans yangını az | 2.523 |
| `DIK_YAMAC` | eğim ≥ 25° (hüküm zaten erozyon değilse) | 2.293 |
| `SEYREK_ORTU` | yangın öncesi ağaç örtüsü < %30 | 792 |

---

## Toparlanma oranı

```
toparlanma_orani = (ndvi_before − recovery_gap_pred) / ndvi_before
```

*"Yangın öncesi örtünün yüzde kaçı iki yılda geri geliyor."*

Ham `recovery_gap_pred` tek başına yorumlanamıyordu: 0,30'luk açık,
yangın öncesi 0,40 olan yerde felaket, 0,80 olan yerde normal. Oran bunu
düzeltiyor ve doğrudan cümleye yazılabiliyor.

Teslim edilen 17.274 tahminli hücrede: medyan **0,40** · %25 = 0,33 ·
%75 = 0,46. Eşikler bu dağılıma göre seçildi.

---

## Eşikler nereden geldi

**İlke: fiziksel iddia mutlak eşik, sıralama iddiası göreli eşik.**

Göreli eşik kullanmadık çünkü yangınların eğim dağılımları birbirine hiç
benzemiyor — yangınların 75. yüzdelik eğimi **4° ile 27°** arasında
değişiyor. Göreli olsaydı düz bir yangında 5°'lik kareye "dik yamaç,
erozyon riski" derdik.

> Düz bir yangında hiç `EROZYON_ONCE` çıkmaması **hata değil, doğru cevap.**

Bütün eşikler `hukum_sozlugu.json` → `esikler` altında okunabilir durumda.

---

## Tür önerisi — dikkat

`tur_onerisi` alanı **sistemin çıkarımı değil.** Dışarıdan verilen bir
yetişme ortamı tablosundan okunuyor (`ai/teslim/yetisme_ortami.json`).

Şu an yüklü olan tablo **örnektir**:

```json
"kaynak": "ÖRNEK TABLO — OGM tür-yetişme ortamı rehberi ile değiştirilecek",
"onaylandi": false
```

**Arayüzde tür önerisi gösterilecekse kaynak notu da gösterilmeli.**
`hukum_sozlugu.json` → `tur_tablosu.onaylandi` alanı `false` olduğu sürece
"örnek veri" uyarısı görünmeli.

Gerçek rehber geldiğinde değişen tek şey o JSON dosyası — kod aynı kalır.

---

## Katman 1 — yangın özeti

`yangin_metinleri.json` → `yanginlar[fire_id].paragraf`

Bölge seçildiğinde okunacak tek paragraf. 517–1.086 karakter, medyan 728.

```
Antalya, 29 Temmuz 2021. 56.550 hektarlık alan, 9.048 hücre. Yanan yerin
%70'i ormanlık, medyan eğim 10,7°, 1 ile 1.604 metre arasında uzanıyor.
Hücrelerin %36'sında yangın hafif kalmış... Modelin iki yıllık iyileşme
görünümü 0,335 — 53 yangının medyanının (0,310) üstünde... 753 hücre
doğrudan müdahale adayı, bunların 233 tanesinde dikimden önce erozyon
kontrolü gerekiyor... Akdeniz bölgesinde 13 referans yangınımız olduğu
için buradaki tahminlerin güveni yüksek.
```

`yangin_ozetleri.json` ise paragrafın dayandığı **sayı bloğu** — büyüklük,
şiddet dağılımı, hüküm dağılımı, arazi, erişim, görünüm, güven. Arayüz
isterse paragraf yerine bu bloktan kendi görselini üretebilir.

### Anlatı profili

53 yangını aynı cümle sırasıyla anlatmak yanlış olurdu; her birinin **asıl
mesajı farklı**. `profil` alanı hangi anlatının kullanıldığını söylüyor:

| Profil | Yangın | Paragraf neyle açılıyor |
|---|---|---|
| `yogun_mudahale` | 13 | müdahale yükü — "alanın önemli bir bölümü kendi başına kapanmayacak" |
| `karisik` | 10 | baskın örüntü yok, kararı öncelik sıralaması veriyor |
| `kendi_toparlaniyor` | 10 | asıl bulgu müdahale gerekmemesi — "kaynak buraya değil" |
| `dik_arazi` | 8 | eğim kısıtı, erozyon ve makineli çalışma |
| `belirsiz` | 7 | önce belirsizlik: bu bölgede öğrenilmiş örnek yok |
| `kapsam_dar` | 5 | yanan alanın çoğu orman değil, karar daralan alan üzerinden |

Bu, süsleme değil: bir yangında okuyucuya önce "buraya kaynak ayırma"
denmeli, diğerinde "bu tahminlere temkinli yaklaş" denmeli.

### Sistem kendi belirsizliğini söylüyor

7 yangın "düşük güven" bölgesinde ve paragrafları bunu açıkça yazıyor.
Marmara örneği:

> Marmara bölgesindeki tek referans yangını bu — doğrulamada bu yangın
> dışarıda bırakıldığında eğitimde bölgeden hiç örnek kalmıyor. Ölçtüğümüz
> sıralama başarısı burada **+0,140**'a düşüyor (bölgesel desteği olan
> yangınlarda +0,707). Tahminler yön gösterici, saha doğrulaması gerekli.

Bu cümle otomatik üretiliyor, elle yazılmadı.

### Paragraf dil modeliyle yeniden yazılabilir

Şu an `"uretim": "sablon (deterministik)"`. İstenirse sayı blokları bir dil
modeline verilip paragraflar yeniden yazdırılabilir — **ama dil modeli
yalnızca sayı bloğunu görür**, başka hiçbir şeye erişmez, dolayısıyla
uydurma olgu giremez. Yeniden yazılan metin okunup onaylandıktan sonra
aynı dosyaya konur ve `uretim` alanı güncellenir.

Her iki durumda da **üründe çalışma anında dil modeli yok** — metin statik
dosyadan okunuyor.

---

## Zeynep için — panelde nasıl görünmeli

Mevcut panelin **en üstüne**, öncelik rozetinin hemen altına:

```
┌────────────────────────────────────────────┐
│  Medium · PRIORITY                    0,43 │
├────────────────────────────────────────────┤
│  ← BURAYA:  ozet                           │
│     "Öncelik sırasına göre değerlendir.     │
│      Kısmi toparlanma bekleniyor..."       │
├────────────────────────────────────────────┤
│  WHY THIS PRIORITY?    (mevcut kırılım)    │
│  RECOVERY / FIRE IMPACT / TERRAIN ...      │
│                                            │
│  ← DETAY BÖLÜMÜNE:  ayrinti                │
│     + tur_onerisi (varsa, kaynak notuyla)  │
│     + tetikleyen  (istenirse, denetim izi) │
└────────────────────────────────────────────┘
```

`zamanlama_notu_var` doğruysa, sözlükteki `zamanlama_notu` panelin altında
tek bir dipnot olarak. Her hücrede aynı metin — tekrar etmesin diye
hücreye yazılmadı.

Hüküm koduna göre renk/rozet vermek istersen `hukum_sozlugu.json` →
`hukumler` dizisi kod, başlık ve sıra veriyor.

## Beytullah için — entegrasyon

Üç seçenek var, karar senin:

1. **Ayrı tablo** — `CellVerdicts(cell_id, hukum, ek_kosullar, ...)`,
   `Cells` ile 1-1. Mevcut şema hiç değişmez.
2. **Cells'e sütun** — `ozet` ve `ayrinti` metin alanları eklenir.
   Basit ama `/cells` yanıtını ~600 bayt/hücre şişirir; Manavgat için
   9.048 hücrelik listede fark edilir.
3. **Ayrı uç nokta** — `/api/fires/{id}/cells/{cellId}/hukum`.
   **Önerdiğim bu:** harita görünümü metne ihtiyaç duymuyor, metin
   yalnızca hücreye tıklandığında gerekiyor.

`--kompakt` sürümü 2. seçeneği de ucuzlatıyor (metin yerine kod taşır).

---

## Üretme ve doğrulama

```bash
python ai/teslim/hukum.py --yaz              # Katman 2: 53 dosya + sözlük
python ai/teslim/yangin_ozeti_uret.py --yaz  # Katman 1: sayı bloğu + paragraf
python ai/teslim/hukum_kontrol.py            # 14 kontrol, ikisini birden
```

Kontroller: kapsama · ölü kural yok · doldurulmamış yer tutucu yok ·
notun yeri · tekrarlanabilirlik · eşik sınırları · hüküm sırası · altın
örnekler · tür önerisi kapsamı · oran aralığı · özet kapsamı · paragraf
biçimi · sayı tutarlılığı · güven uyarısı · uydurma sayı koruması.

**Uydurma sayı koruması (14):** paragrafta geçen her sayım değeri, o
yangının sayı bloğundan türeyebilmek zorunda. Metin ileride bir dil
modeliyle yeniden yazılırsa uydurduğu rakam bu testten geçemez —
denendi, sahte bir hücre sayısı enjekte edildi ve yakalandı.

Tek bir yangının bloğunu ve paragrafını görmek için:

```bash
python ai/teslim/yangin_ozeti_uret.py --goster AKD_2021_01
```

**Metin değiştirmek için kod değiştirmek gerekmiyor** —
`ai/teslim/cumleler.json` yeterli.

---

## Neden kural, neden dil modeli değil

Aynı hücreye iki kez bakan uzman **aynı cümleyi** görmeli, ve *"bunu neden
söyledin"* sorusunun cevabı bir kurala kadar izlenebilmeli. `tetikleyen`
alanı tam olarak bunun için var:

```
toparlanma=0.22 egim=35.4>=25 <0.35
```

Dil modeli bunların ikisini de veremiyor. Üretimde dil katmanı eklenirse
kararı yine kural verecek, dil modeli yalnızca **hesaplanmış olanı**
anlatacak.
