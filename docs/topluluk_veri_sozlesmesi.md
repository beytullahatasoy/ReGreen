# Topluluk katmanı — backend veri sözleşmesi

**Frontend:** Buğra · **Backend:** Beytullah · **Durum:** frontend hazır, backend bekliyor

Organisation (`/organisation`) ve Community (`/community`) ekranları yeniden
kuruldu. **Bölge tarafı artık gerçek veriyle çalışıyor** — mevcut uç noktalardan
besleniyor, yeni bir şey gerekmiyor.

Geriye üç kayıt türü kalıyor ve bunların backend'i yok: **etkinlik, gönüllü
katılımı, saha gözlemi.** Bu belge onların sözleşmesi.

---

## 1 · Şu an ne çalışıyor

Ekranlar `src/hooks/useRecoveryZones.ts` üzerinden **var olan** iki uç noktayı
kullanıyor:

```
GET /api/fires                 53 yangın · il · bölge · alan · hücre sayısı
GET /api/fires/{id}/summary    hüküm dağılımı + güven + paragraf
```

Bunlardan türetilen alanlar (frontend'de hesaplanıyor, backend'e iş düşmüyor):

| Alan | Hesap |
|---|---|
| `mudahaleHucre` | `EROZYON_ONCE + DIKIM_ADAYI` |
| `mudahaleHa` | `mudahaleHucre × 6,25` |
| `izlemeHucre` | `IZLE + GENCLESME_IZLE` |
| `guven` | `sayi_blogu.guven.seviye` |

> **53 istek sorunu.** Şu an her yangın için ayrı `/summary` çağrılıyor
> (8'erli gruplar hâlinde, ~1–2 sn). Çalışıyor ama ideal değil.
> **İstek:** toplu bir uç nokta — aşağıda §4.

---

## 2 · Kurulacak varlıklar

Üçü de bir **gerçek yangına** `FireId` ile bağlı. Uydurma bölge kimliği yok;
`FireId` doğrudan `Fires.FireId` yabancı anahtarı.

### `FieldActivity` — saha etkinliği

| Alan | Tip | Not |
|---|---|---|
| `Id` | `int` | PK |
| `FireId` | `string(32)` | FK → `Fires.FireId` |
| `Type` | `string(64)` | aşağıdaki 5 değerden biri |
| `Date` | `DateOnly` | etkinlik tarihi |
| `Organisation` | `string(120)` | düzenleyen doğrulanmış kurum |
| `Capacity` | `int` | toplam kontenjan |
| `Joined` | `int` | katılan sayısı · `0 ≤ Joined ≤ Capacity` |
| `Status` | `string(24)` | `Açık` · `Planlandı` · `İncelemede` |
| `Location` | `string(200)` | buluşma noktası |
| `Requirements` | `string(500)` | `\|` ile ayrılmış liste |
| `Description` | `string(500)` | |
| `Coordinator` | `string(120)` | |

`Type` değerleri: `Saha değerlendirmesi` · `Kontrollü temizlik` ·
`Toprak / erozyon gözlemi` · `Bitki örtüsü izleme` · `Uzman onaylı dikim etkinliği`

### `FieldObservation` — gönüllü gözlemi

| Alan | Tip | Not |
|---|---|---|
| `Id` | `int` | PK |
| `FireId` | `string(32)` | FK → `Fires.FireId` |
| `CellId` | `string(64)?` | **opsiyonel** — biliniyorsa hangi kare |
| `SubmittedBy` | `string(64)` | takma ad; kişisel veri tutulmuyor |
| `SubmittedAt` | `DateTimeOffset` | |
| `Lat` / `Lon` | `double?` | konum etiketi |
| `Answers` | `string(500)` | `\|` ile ayrılmış yapılandırılmış yanıtlar |
| `Note` | `string(500)?` | serbest metin |
| `Status` | `string(48)` | `Beklemede` · `İncelendi` · `Destekleyici kanıt olarak kabul edildi` · `Açıklama gerekli` |
| `ReviewedBy` | `string(120)?` | inceleyen kurum |
| `ReviewedAt` | `DateTimeOffset?` | |

> **Kritik kural.** Gözlem **modeli eğitmiyor**, tahmine girmiyor, hüküm
> katmanını değiştirmiyor. Yalnızca uzmanın kararını destekleyen kanıt. Bu
> ekranlarda yazılı; şemada da `Predictions` / `CellVerdicts` ile ilişkisi
> olmaması gerekiyor.

### `RecoveryUpdate` — 12 aylık izleme kaydı

| Alan | Tip | Not |
|---|---|---|
| `Id` | `int` | PK |
| `FireId` | `string(32)` | FK → `Fires.FireId` |
| `Title` | `string(120)` | |
| `Status` | `string(24)` | `Tamamlandı` |
| `Trend` | `string(32)` | `Yükseliyor` · `Sabit` · `Uzman incelemesi gerekli` |
| `AssessedAt` | `DateTimeOffset` | |

---

## 3 · Uç noktalar

Mevcut `HukumEndpoints` kalıbına birebir uyuyor: `Produces<T>` +
`ProducesProblem(404)` + `ProducesProblem(503)`, hatalar `ApiProblems` üzerinden.

### Okuma — ilk turda bunlar yeter

```
GET /api/fires/{fireId}/activities          → FieldActivityDto[]
GET /api/activities?status=Açık&limit=20    → FieldActivityDto[]   (Community listesi)
GET /api/fires/{fireId}/observations        → FieldObservationDto[]
GET /api/observations?status=Beklemede      → FieldObservationDto[] (Organisation kuyruğu)
GET /api/fires/{fireId}/recovery-update     → RecoveryUpdateDto
```

### Yazma — ikinci tur

```
POST  /api/activities/{id}/join             → { joined, capacity }
POST  /api/fires/{fireId}/observations      → FieldObservationDto
PATCH /api/observations/{id}                → durum güncelle (kurum)
```

Yeni hata kodları (`ApiProblems`'e eklenecek):

| Kod | HTTP | Ne zaman |
|---|---|---|
| `ACTIVITY_NOT_FOUND` | 404 | etkinlik yok |
| `ACTIVITY_FULL` | 409 | `Joined == Capacity` iken katılım |
| `OBSERVATION_NOT_FOUND` | 404 | gözlem yok |
| `OBSERVATION_INVALID_STATUS` | 400 | bilinmeyen durum değeri |

### Frontend bunları beklemeye hazır

Gözlem döngüsü arayüzde **çalışır durumda**: gönüllü formu doldurup
gönderiyor, kayıt Organisation'daki inceleme kuyruğunun en üstüne düşüyor,
kurum kabul ediyor ya da açıklama istiyor, sonuç gönüllünün ekranına dönüyor.

Backend hazır olmadığı için kayıtlar şimdilik **yalnızca tarayıcıda**
(`localStorage`, anahtar `regreen.observations.v1`) tutuluyor ve iki ekranda da
bu açıkça yazıyor. Tüm depolama tek bir dosyada:

```
frontend/src/prototypes/recovery-community/data/observationService.ts
```

Uç noktalar gelince **yalnızca bu dosya** değişecek; ekranlar aynı dört
metodu çağırmaya devam edecek:

| Servis metodu | Karşılığı |
|---|---|
| `list()` | `GET /api/observations` |
| `submit(input)` | `POST /api/fires/{fireId}/observations` |
| `review(id, status)` | `PATCH /api/observations/{id}` |
| `subscribe(fn)` | — (istemci tarafı; yerine yeniden çekme) |

`ObservationInput` alanları DTO ile birebir eşleşiyor: `fireId`, `activityId`,
`location`, `photoName`, `answers[]`, `note`. Fotoğrafın **yalnızca adı**
taşınıyor — gerçek dosya yükleme ayrı iş.

---

## 4 · Bir istek: toplu özet

Şu an Organisation ekranı açılırken **53 ayrı** `/summary` isteği gidiyor.
Tek bir uç nokta bunu kapatır:

```
GET /api/fires/summaries        → FireNarrativeDto[]   (tümü, tek istek)
```

Ya da daha hafifi — ekranların gerçekte ihtiyacı olan tek şey bu:

```
GET /api/fires/verdict-totals
[
  { "fire_id": "AKD_2021_01",
    "hukum_dagilimi": { "EROZYON_ONCE": 233, "DIKIM_ADAYI": 520, ... },
    "guven": { "seviye": "yuksek", "bolgedeki_referans_yangini": 13 } },
  ...
]
```

İkincisi tercihim: `paragraf` alanları taşınmıyor, yanıt küçük kalıyor.
Frontend tarafında değişecek tek yer `useRecoveryZones.ts` — tek fonksiyon.

---

## 5 · Frontend nerede duruyor

```
src/hooks/useRecoveryZones.ts          gerçek veri katmanı  (+ test)
src/prototypes/recovery-community/
    components/RealZone.tsx            gerçek bölge bileşenleri
    components/ObservationReview.tsx   gözlem kuyruğu   (demo veri)
    components/RecoveryUpdateCard.tsx  izleme kartı     (demo veri)
    data/demoData.ts                   ← uç noktalar gelince BURASI silinecek
    organisation/OrganisationWorkspace.tsx
    volunteer/VolunteerWorkspace.tsx
```

Bağlantı geldiğinde değişecek yer az: `demoData.ts` yerine servis çağrısı,
tipler `src/types/api.ts`'e taşınır. Bileşenler aynı kalır — hepsi zaten prop
alıyor.

## 6 · Kaldırılanlar

Bu turda silinen ölü kod: `ZoneDetailModal.tsx`, `RecoveryJourney.tsx`,
`SummaryStrip.tsx`, ve `demoData.ts` içindeki **uydurma bölge listesi**
(`zone-07 · Muğla — Zone 07` vb.). Artık ekranda uydurma bölge adı görünmüyor.

`types/index.ts` içinden `RecoveryZoneDemo`, `ZoneStatus`, `RecoveryStage`
kaldırıldı — bölge tipi artık `hooks/useRecoveryZones.ts` → `RecoveryZone`.
