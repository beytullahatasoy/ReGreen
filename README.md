# ReGreen

ReGreen, Huawei ICT Competition – Innovation Track kapsamında geliştirilen, yangın sonrası doğal toparlanma sürecini analiz eden ve uzmanlara müdahale önceliği konusunda karar desteği sunan bir sistemdir.

Proje aynı zamanda uzman tarafından uygun görülen alanların vatandaş katılımına açılabildiği bir kampanya ve topluluk yapısını da hedeflemektedir.

## Proje Yapısı

- `ai/` → Yapay zekâ modeli ve veri çalışmaları
- `backend/` → API, veritabanı ve sistem entegrasyonu
- `frontend/` → Uzman Paneli ve Topluluk arayüzleri
- `docs/` → Proje dokümantasyonu ve ortak standartlar
- `sample-data/` → Ortak test ve örnek veriler

## Ekip

- **Buğra** — Yapay Zekâ
- **Beytullah** — Backend & Cloud
- **Zeynep** — Frontend, GIS & Kullanıcı Deneyimi

## Ortak Veri Sözleşmesi

AI, backend ve frontend arasında ortak alan adları ve veri formatları kullanılacaktır.

Detaylı veri sözleşmesi:

`docs/data-contract.md`

## Geliştirme Süreci

Geliştirme görev bazlı branch'ler üzerinden yürütülecektir.

Örnek:

- `ai/recovery-baseline`
- `backend/project-setup`
- `frontend/expert-map`

Tamamlanan çalışmalar Pull Request üzerinden `main` branch'ine birleştirilecektir.
