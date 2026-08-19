# ReGreen · Sunum

Yangın sonrası rehabilitasyon önceliklendirme projesinin tanıtım sunumu.
Tek sayfalık bir web sitesi — projektörde açılıp yukarıdan aşağı kaydırılarak
anlatılıyor. Süre: 5-7 dakika.

React 18 + TypeScript + Vite + Tailwind CSS.

---

## Çalıştırma

Node.js 18 veya üstü gerekiyor ([nodejs.org](https://nodejs.org)).

```bash
cd sunum
npm install
npm run dev
```

Terminalde çıkan adresi açın (genelde `http://localhost:5173`).
Dosyayı kaydettiğinizde tarayıcı kendiliğinden yenilenir, elle yenilemeye
gerek yok.

Sunum günü için derlenmiş sürüm:

```bash
npm run build      # dist/ klasörünü üretir
npm run preview    # derlenmiş hâli yerelde açar
```

`npm run build` aynı zamanda TypeScript denetimi yapar. Hata verirse
sunumda da bozuk çalışır, önce onu düzeltin.

---

## Hangi dosyada ne var

| Dosya | İçerik |
|---|---|
| `src/App.tsx` | Bölümlerin sırası. Bölüm eklemek/çıkarmak için burası |
| `src/components/Nav.tsx` | Üstteki menü. Bölüm ekledinizde `BOLUMLER` dizisine de ekleyin |
| `src/components/Hero.tsx` | Açılış ekranı, fare/dokunmayla açılan spot ışığı efekti |
| `src/components/Sections.tsx` | **Sunumun metinlerinin çoğu burada.** Sorun, Yöntem, Test, Bulgular, Sırada |
| `src/components/Charts.tsx` | Grafikler. Değerler pipeline çıktılarından birebir alındı |
| `src/components/Reveal.tsx` | Kaydırınca içeriğin yumuşakça belirmesi |
| `src/index.css` | Genel stiller ve Tailwind girişi |
| `public/` | Görseller. Buraya koyduğunuz dosyaya `/dosya-adi.jpg` diye erişilir |

**Metin değiştirecekseniz** doğrudan `Sections.tsx`'e gidin, bölüm başlıkları
Türkçe (`Sorun`, `Yontem`, `Test`, `Bulgular`, `Sirada`).

---

## Dikkat edilecek iki şey

**1. Açılış görselleri dışarıdan geliyor.** `Hero.tsx` içindeki `BG_IMAGE_1`
ve `BG_IMAGE_2` bir CDN adresine bakıyor. İnternet yoksa ya da adres ölürse
açılış ekranı boş kalır. Sunumdan önce mutlaka internetli bir ortamda bir kez
açıp kontrol edin. Garanti istiyorsanız görselleri indirip `public/` içine
koyun ve adresleri `/dosya-adi.png` olarak değiştirin.

**2. Grafiklerdeki sayılar uydurma değil.** `Charts.tsx` başındaki nota göre
bütün değerler pipeline çıktılarından alındı. Sayı değiştirecekseniz veriden
teyit alın — sunumda "bu sayı nereden geliyor" sorusu gelirse cevabımız
olmalı.

---

## Bölüm sırası

1. **Açılış** — projenin tek cümlelik vaadi
2. **Sorun** — yangından sonra nereden başlanacağına nasıl karar veriliyor
3. **Yöntem** — uydu verisinden ızgara üretimi, doğanın kendi cevabını etiket
   olarak kullanma
4. **Fizibilite testi** — gerçekten çalışıyor mu, kanıt
5. **Bulgular** — ölçülen sonuçlar
6. **Sırada** — yol haritası
