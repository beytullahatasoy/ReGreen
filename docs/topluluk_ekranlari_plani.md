# Organisation ve Community ekranları — geliştirme planı

**Sorumlu:** Buğra · **Durum:** plan · **Kapsam:** yalnızca frontend
**Dokunulmayacak:** backend, veritabanı, Expert ekranı, hüküm katmanı

---

## 1 · Mevcut durum — ölçülmüş

İki ekran var, ikisi de `src/prototypes/recovery-community/` altında.

```
toplam kod          350 satır
demo veri           3 bölge · 3 etkinlik · 3 gözlem · 1 iyileşme kaydı
backend bağlantısı  YOK  (entity yok, uç nokta yok)
dil                 yalnızca İngilizce
```

| Ekran | Bölüm | Kelime | Yükseklik |
|---|---|---|---|
| `/organisation` | 7 | 556 | ~4 ekran |
| `/community` | 6 | 560 | ~5 ekran |

**Organisation bölümleri:** hero · 5 sayaç · Recovery Zones · 4 eylem düğmesi ·
Activity management · Observation review · Recovery update

**Community bölümleri:** hero + 5 adımlı akış şeridi · 4 sayaç · 4 filtre +
bölge kartları · etkinlik kartları · etkinlik detayı + 6 alanlı form ·
iyileşme kartı

---

## 2 · Sorunlar

### 2.1 · Bölgeler uydurma ve gerçek veriyle bağı yok

`demoData.ts` içinde `zone-07 · Muğla — Zone 07`, `zone-12 · Antalya — Zone 12`,
`zone-03 · İzmir — Zone 03`. Bunların hiçbiri gerçek değil.

Oysa elimizde **53 gerçek yangın, 37.163 hücre ve her hücre için hüküm** var.
API'de hazır duruyor:

```
/api/fires                  53 yangın · il · bölge · alan · hücre sayısı
/api/fires/{id}/summary     paragraf + sayı bloğu (hüküm dağılımı, güven)
/api/fires/{id}/cells       hücreler
```

**En büyük fırsat bu.** Aynı veriyle çalışan üç ekran olur; şu an ikisi
gerçek veriyle, biri uydurmayla çalışıyor.

### 2.2 · Kalabalık — her şey aynı ağırlıkta

Organisation'da 7 bölüm art arda, hiçbiri diğerinden öne çıkmıyor. Community'de
sayfa açılır açılmaz 4 filtre + 3 bölge kartı + 3 etkinlik kartı + 6 alanlı
form aynı anda görünüyor.

Sonuç: **kullanıcı nereden başlayacağını bilmiyor.** Ekranlar bilgi gösteriyor
ama iş yaptırmıyor.

### 2.3 · Her ekranın tek bir işi yok

Organisation ve Community neredeyse aynı sayfa: ikisinde de Recovery Zones
listesi, ikisinde de `RecoveryUpdateCard`, ikisinde de `SummaryStrip`. Farklı
iki kitleye aynı düzen veriliyor.

### 2.4 · Uyarı etiketleri gürültüye dönüşmüş

Sayfalarda **8 ayrı** prototip rozeti var: *Demo data · Demo workflow ·
Demo filters · no persistence · Non-submitting prototype · Prototype action ·
no data mutation · Prototype monitoring example · DEMO WORKSPACE*.

Dürüst olmak doğru, ama sekiz kez tekrarlamak hem güveni zedeliyor hem yer
kaplıyor. **Ekran başına bir tane** yeter.

### 2.5 · Anlamsız sayılar

`62 registered volunteers`, `18d next milestone`, `Action layer 03`,
`My followed zones 02`. Hiçbiri bir şeyden hesaplanmıyor. Expert ekranındaki
sayaçlar gerçek hüküm dağılımından geliyor — buradakiler uydurma.

### 2.6 · Gönüllü formu çıkmaz sokak

Form dolduruluyor, "Preview submission" deniyor, modal açılıp *"hiçbir şey
gönderilmedi"* diyor. Kullanıcının katkısıyla sonucu arasında hiçbir bağ yok.

### 2.7 · Dil

**Düzeltme:** Planı yazarken Expert ekranını Türkçe sanmıştım; aslında
**İngilizce ana etiket + Türkçe yardımcı** kalıbını kullanıyor
(`Why this priority? / Neden bu öncelik?`). Bu iki ekran da ona uyduruldu:
arayüz metni İngilizce, bölüm başlıklarında Türkçe yardımcı.

API'den gelen hüküm metinleri (`paragraf`, `ozet`) **Türkçe kalıyor** —
onlar model çıktısı, arayüz metni değil.

### 2.8 · Kod bakımı zor

JSX tek satırda, 300+ karakterlik satırlar var. Değiştirmek riskli.

---

## 3 · Tasarım ilkeleri

1. **Her ekranın tek bir işi olacak.** Diğer her şey ikincil.
2. **Uydurma veri yerine gerçek veri.** Üretemediğimiz şeyi göstermeyeceğiz.
3. **Az bölüm, net hiyerarşi.** Ekranda önce ne yapılacağı, sonra ayrıntı.
4. **Tek dürüstlük rozeti.** Ekran başına bir uyarı, tekrar yok.
5. **Expert ekranının tasarım dili.** Aynı renkler, aynı tipografi.
6. **Frontend'de kal.** Backend'e dokunmadan, mevcut uç noktalarla.

---

## 4 · Hedef: her ekranın tek işi

### Organisation
> *"Bugün hangi alana ekip göndereceğim, neyi onaylamam gerekiyor?"*

Kurum kullanıcısı bir **iş kuyruğu** görmeli, katalog değil.

### Community
> *"Nereye gidebilirim, ne yapabilirim?"*

Vatandaş **tek bir sonraki adım** görmeli, form yığını değil.

---

## 5 · Ekran planları

### 5.1 · Organisation — 7 bölüm → 3

```
1  BUGÜN            karar bekleyen işler  (kuyruk)
2  ALANLARIM        yürüttüğüm bölgeler   (portföy, sıkıştırılmış)
3  KANIT KUYRUĞU    inceleme bekleyen gözlemler
```

**Çıkanlar:** dev hero (ekranın üçte biri), 4 eylem düğmesi şeridi (hepsi
modal açıp "hiçbir şey olmadı" diyor), ayrı Activity management bölümü
(bölge detayına taşınacak), ayrı Recovery update bölümü (portföy kartında
satır olacak).

**Sayaçlar 5 → 3** ve hepsi gerçek veriden:

| Sayaç | Kaynak |
|---|---|
| Müdahale adayı alan | `hukum_dagilimi.EROZYON_ONCE + DIKIM_ADAYI` × 6,25 ha |
| Öncelik sırası bekleyen | `hukum_dagilimi.ONCELIGE_GORE` |
| İnceleme bekleyen kanıt | gözlem listesi (demo, açıkça işaretli) |

### 5.2 · Community — 6 bölüm → 3

```
1  YAKINIMDA NE VAR    tek filtre (il) + bölge kartları
2  SEÇTİĞİM ETKİNLİK   detay + katıl
3  KATKIMIN SONUCU     12 ay sonra ne oldu
```

**Çıkanlar:** 5 adımlı akış şeridi (dekoratif, iş yaptırmıyor), 4 sayaç
(hepsi uydurma), 4 filtre → 1'e iner.

**Gözlem formu açılışta görünmeyecek** — ancak bir etkinliğe katılınca
açılacak. Şu an sayfanın ortasında duruyor ve kimsenin işine yaramıyor.

---

## 6 · Veri stratejisi

Üç kademe, hepsi açıkça işaretli:

| Kademe | Ne | Kaynak |
|---|---|---|
| **Gerçek** | bölge, il, alan, hüküm dağılımı, güven, paragraf | `/api/fires`, `/api/fires/{id}/summary` |
| **Türetilmiş** | müdahale alanı (ha), aciliyet sırası | hüküm sayımlarından hesap |
| **Demo** | etkinlikler, gönüllüler, gözlemler | `demoData.ts` — rozetli |

Bir "Recovery Zone" artık gerçek bir yangın olacak. Kart şöyle diyecek:

```
Antalya · Manavgat            YÜKSEK ÖNCELİK
56.550 ha · 9.048 kare
753 kare doğrudan müdahale adayı · 233'ünde önce erozyon kontrolü
Akdeniz'de 13 referans yangını — tahmin güveni yüksek
```

Hepsi ölçülmüş. Uydurma tek satır yok.

> **Not:** Gerçek "alt bölge" (bir yangının içindeki bitişik kare kümesi)
> henüz üretilmiyor — fizibilitesi ölçüldü (521 alan, medyan 6,2 ha) ama
> kod yazılmadı. Bu turda **yangın seviyesinde** kalıyoruz; alt bölge
> sonraki iş.

---

## 7 · Adım adım uygulama

| # | Adım | Çıktı | Risk |
|---|---|---|---|
| 1 | Kodu okunur hale getir | JSX satırlarını aç, bileşenleri ayır | düşük |
| 2 | Gerçek veri katmanı | `useRecoveryZones()` — API'den yangınları çeker, hüküm sayımlarını hesaplar | orta |
| 3 | Ortak bileşenler | `ZoneCard`, `StatRow`, `DemoBadge` — iki ekranda da kullanılacak | düşük |
| 4 | Organisation yeniden kur | 3 bölüm, gerçek sayaçlar, iş kuyruğu | orta |
| 5 | Community yeniden kur | 3 bölüm, tek filtre, koşullu form | orta |
| 6 | ~~Türkçeleştir~~ → **İngilizce** | Expert ekranı İngilizce; ona uyduruldu | düşük |
| 7 | Test | mevcut `routing.test.ts` bozulmasın + yeni veri katmanı testi | düşük |

**Bugün 1–3 bitecek**, sonra 4 ve 5 sırayla.

Her adımdan sonra `npm run build` ve `npm test` çalıştırılacak; ekran
tarayıcıda kontrol edilecek.

---

## 8 · Kapsam dışı

- Backend uç noktası, entity, migration — **Beytullah'ın alanı**
- Gerçek kullanıcı hesabı, kayıt, oturum
- Gerçek dosya yükleme
- Expert ekranı ve hüküm katmanı — çalışıyor, dokunulmayacak
- Alt bölge kümeleme — ayrı iş, fizibilitesi ölçüldü

## 9 · Riskler

**Prototip ekranların "gerçek" sanılması.** Etkinlik ve gönüllü verisi hâlâ
demo. Tek ama net bir rozet kalacak; kaldırılmayacak.

**Expert ekranını bozmak.** Ortak CSS değişkenleri var. `recovery-community.css`
kendi ad alanında (`rc-` öneki) kalacak, global stile dokunulmayacak.

**API bağımlılığı.** Bu ekranlar şu an API'siz çalışıyor; gerçek veriye
bağlanınca API kapalıyken boş kalırlar. Hata durumu ve yükleniyor durumu
baştan yazılacak.
