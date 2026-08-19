import type { ReactNode } from 'react'
import Reveal from './Reveal'
import { CokusGrafigi, IzlenceGrafigi, TutarlilikGrafigi } from './Charts'

/* ---------------------------------------------------------------- kabuklar */

function Bolum({ id, children }: { id?: string; children: ReactNode }) {
  return (
    <section id={id} className="px-5 sm:px-8 py-24 sm:py-28 border-t border-white/[0.09]">
      <div className="max-w-6xl mx-auto">{children}</div>
    </section>
  )
}

function Baslik({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <h2 className={`text-white text-[2rem] sm:text-[2.6rem] lg:text-[3rem] font-normal
      leading-[1.09] tracking-[-0.035em] text-balance pb-1 ${className}`}>
      {children}
    </h2>
  )
}

function It({ children }: { children: ReactNode }) {
  return <span className="font-playfair italic leading-[1.15]">{children}</span>
}

function Metin({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <p className={`text-white/60 text-[16.5px] leading-[1.72] max-w-[62ch] ${className}`}>
      {children}
    </p>
  )
}

function V({ children }: { children: ReactNode }) {
  return <strong className="text-white/95 font-medium">{children}</strong>
}

/* ------------------------------------------------------------------ sorun */

export function Sorun() {
  return (
    <Bolum id="sorun">
      <div className="grid lg:grid-cols-12 gap-y-12 gap-x-16">
        <div className="lg:col-span-7">
          <Reveal>
            <Baslik>
              Yangın söndüğünde <It>asıl karar</It> başlıyor
            </Baslik>
          </Reveal>
          <Reveal delay={90}>
            <Metin className="mt-8">
              Türkiye’de her yaz on binlerce hektar orman yanıyor. Alevler söndükten sonra
              ortaya çok daha sessiz bir soru çıkıyor:{' '}
              <V>elde sınırlı ekip, bütçe ve fidan varken hangi bölgeye önce gidilmeli?</V>
            </Metin>
          </Reveal>
          <Reveal delay={140}>
            <Metin className="mt-6">
              Bu karar bugün saha tecrübesiyle veriliyor. Tecrübe değerli, ama yüzlerce
              bölgeyi aynı anda ve aynı ölçütlerle karşılaştırmak insan gözüyle mümkün değil.
              FireRecover karar vermiyor, <V>veriye dayalı bir sıralama öneriyor.</V>
            </Metin>
          </Reveal>
        </div>

        <div className="lg:col-span-5 lg:pt-3">
          <Reveal delay={180}>
            <p className="text-white/40 text-sm mb-6 leading-relaxed">
              Bir bölge iki ayrı nedenden öncelikli olabilir.
            </p>
            <div className="border-t border-white/15">
              <div className="py-6 border-b border-white/[0.09]">
                <h3 className="text-white text-[18px] font-medium tracking-[-0.015em] mb-2">
                  Kendi başına toparlanamaz
                </h3>
                <p className="text-white/55 text-[15px] leading-[1.6]">
                  Dik yamaç, yüksek rakım, kozalağı yanınca geri gelmeyen ibreli orman.
                </p>
              </div>
              <div className="py-6">
                <h3 className="text-white text-[18px] font-medium tracking-[-0.015em] mb-2">
                  Toparlanamazsa bedeli ağır
                </h3>
                <p className="text-white/55 text-[15px] leading-[1.6]">
                  Yakınında köy var. Ağaç örtüsü olmayınca ilk sağanakta toprak aşağı iniyor.
                </p>
              </div>
            </div>
          </Reveal>
        </div>
      </div>
    </Bolum>
  )
}

/* ----------------------------------------------------------------- yöntem */

export function Yontem() {
  const bant = [
    { d: '0,1 - 0,2', a: 'Çıplak toprak, kül', w: '26%', c: '#6b4a35' },
    { d: '0,2 - 0,4', a: 'Seyrek ot ve çalı', w: '28%', c: '#8f7a3c' },
    { d: '0,4 - 0,7', a: 'Sağlıklı orman', w: '46%', c: '#2f7a35' },
  ]
  return (
    <Bolum id="yontem">
      <Reveal>
        <Baslik className="max-w-[24ch]">
          Doğru cevabı kimse yazmıyor, biz de <It>doğaya sorduk</It>
        </Baslik>
      </Reveal>

      <Reveal delay={90}>
        <Metin className="mt-8">
          Böyle bir sistemi eğitmek için “bu bölgeye 8 puan öncelik verilmeliydi” diyen bir
          kayıt gerekir. Böyle bir kayıt tutulmuyor. Onun yerine{' '}
          <V>doğanın kendi verdiği cevabı okuduk:</V> yangından iki yıl sonra hâlâ
          toparlanamamış olan yer, insan yardımı gereken yerdir.
        </Metin>
      </Reveal>

      <Reveal delay={130}>
        <Metin className="mt-6">
          Bunu uydudan ölçüyoruz. Sağlıklı yapraklar kızılötesi ışığı güçlü yansıtır,
          kırmızıyı yutar. Uydu bu ikisini oranlarsa{' '}
          <V>o noktada ne kadar canlı bitki olduğunu gösteren tek bir sayı</V> çıkıyor.
        </Metin>
      </Reveal>

      <Reveal delay={170}>
        <div className="mt-12">
          <div className="flex h-12 w-full overflow-hidden">
            {bant.map((b) => (
              <div key={b.d} style={{ width: b.w, background: b.c }} />
            ))}
          </div>
          <div className="flex w-full mt-3.5">
            {bant.map((b) => (
              <div key={b.d} style={{ width: b.w }} className="pr-6">
                <div className="text-white text-[15px] font-medium tabular-nums">{b.d}</div>
                <div className="text-white/45 text-[13px] mt-1 leading-snug">{b.a}</div>
              </div>
            ))}
          </div>
        </div>
      </Reveal>

      <Reveal delay={210}>
        <p className="mt-16 text-white text-[21px] sm:text-[25px] leading-[1.45] tracking-[-0.022em] max-w-[56ch] text-balance">
          Sistem “öncelik” denen soyut şeyi değil, ormanın{' '}
          <It>kendi kendine iyileşme kapasitesini</It> öğreniyor. Öncelik bunun tam tersi.
        </p>
      </Reveal>
    </Bolum>
  )
}

/* ------------------------------------------------------- fizibilite testi */

const SORULAR = [
  ['Veri kaynakları', 'Sentinel-2, Copernicus DEM, ESA WorldCover ve OpenStreetMap. Dördü de açık veri; ücret, hesap ya da API anahtarı gerekmiyor.'],
  ['Ölçüm doğruluğu', 'Bitki yoğunluğu dört yıl boyunca sabit, yangın ayında uçuruma düşüyor. Yöntem olayı doğru tarihte tespit ediyor.'],
  ['Ölçeklenebilirlik', 'Üç bölge, 27.970 hücre, uçtan uca beş dakika. Tek makinede, paralel altyapı gerektirmeden.'],
  ['Öğrenilebilir yapı', 'Aynı ilişkiler birbirinden bağımsız üç bölgede de aynı yönde çıkıyor. Model kurmanın önü açık.'],
]

export function Test() {
  return (
    <Bolum id="test">
      <Reveal>
        <div className="text-[#e8702a] text-[13px] font-medium mb-6">Fizibilite testi</div>
        <Baslik className="max-w-[22ch]">
          Model kurulmadan önce <It>dört koşul</It> doğrulandı
        </Baslik>
      </Reveal>
      <Reveal delay={90}>
        <Metin className="mt-8">
          Sistemin kurulabilir olması dört şeye bağlıydı: veriye erişim, ölçümün
          doğruluğu, ölçeklenebilirlik ve veride öğrenilebilir bir yapının varlığı.{' '}
          <V>Dördü de doğrulandı.</V>
        </Metin>
      </Reveal>

      <Reveal delay={130}>
        <div className="mt-14 grid sm:grid-cols-2 gap-x-14 gap-y-10 border-t border-white/15 pt-10">
          {SORULAR.map(([s, c]) => (
            <div key={s}>
              <h3 className="text-white text-[17px] font-medium tracking-[-0.015em] mb-2.5">
                {s}
              </h3>
              <p className="text-white/55 text-[15px] leading-[1.62]">{c}</p>
            </div>
          ))}
        </div>
      </Reveal>

      {/* gercek uydu kanit gorseli */}
      <Reveal delay={170}>
        <figure className="m-0 mt-24">
          <div className="grid sm:grid-cols-2 gap-4">
            {[
              ['/yangin-oncesi.jpg', 'Yangından önce', 'Kapalı orman örtüsü'],
              ['/yangin-sonrasi.jpg', '40 gün sonra', 'Aynı kare, aynı uydu'],
            ].map(([src, t, a]) => (
              <div key={src}>
                <img src={src} alt={`${t}: uydu görüntüsü`}
                  className="w-full h-auto block" loading="lazy" />
                <div className="mt-3 flex items-baseline gap-3">
                  <span className="text-white text-[15px]">{t}</span>
                  <span className="text-white/40 text-[13.5px]">{a}</span>
                </div>
              </div>
            ))}
          </div>
          <figcaption className="text-white/40 text-[14px] mt-6 leading-relaxed max-w-[62ch]">
            Yanık iz çıplak gözle görülüyor. Ama sistemin işi bakmak değil ölçmek, çünkü
            yüzlerce bölgeyi tek tek gözle karşılaştıramazsınız.
          </figcaption>
        </figure>
      </Reveal>

      <Reveal delay={100}>
        <div className="mt-24">
          <CokusGrafigi />
        </div>
      </Reveal>

      <Reveal delay={140}>
        <div className="mt-20 grid grid-cols-2 lg:grid-cols-4 border-t border-white/15">
          {[
            ['27.970', 'incelenen hücre'],
            ['3.269', 'yanmış hücre'],
            ['1.880', 'güvenilir ölçüm'],
            ['5 dk', 'uçtan uca süre'],
          ].map(([s, a]) => (
            <div key={a} className="py-8 pr-6 border-b lg:border-b-0 border-white/[0.09]">
              <div className="text-white text-[2.3rem] font-normal tabular-nums tracking-[-0.045em] leading-none">
                {s}
              </div>
              <div className="text-white/45 text-[13.5px] mt-3 leading-snug">{a}</div>
            </div>
          ))}
        </div>
        <Metin className="mt-8">
          Yanmış araziyi 500 metrelik karelere bölüp her kare için yedi ölçüm topladık:
          yangın şiddeti, eğim, yükselti, ağaç oranı, suya, yola ve yerleşime mesafe.{' '}
          <V>Hiçbir sayı elle girilmedi.</V>
        </Metin>
      </Reveal>

    </Bolum>
  )
}

/* --------------------------------------------------------------- bulgular */

export function Bulgular() {
  return (
    <Bolum id="bulgular">
      <Reveal>
        <Baslik className="max-w-[22ch]">
          Test beklemediğimiz <It>bir şey</It> gösterdi
        </Baslik>
      </Reveal>
      <Reveal delay={90}>
        <Metin className="mt-8 mb-14">
          Yangın ne kadar ağır olursa olsun, iki yıl sonra bütün bölgeler aynı bitki
          yoğunluğuna geliyor. Aradaki fark, yangından <V>önce</V> ne kadar zengin
          olduklarında.
        </Metin>
      </Reveal>

      <Reveal delay={60}><IzlenceGrafigi /></Reveal>

      <Reveal delay={100}>
        <p className="my-20 text-white text-[21px] sm:text-[25px] leading-[1.45] tracking-[-0.022em] max-w-[56ch] text-balance">
          Ağır yanan yerler daha zengindi, daha çoğunu kaybetti ve aynı yerde bitti. Yani
          ağır yanan iyi toparlanmıyor, <It>çok daha büyük bir açık veriyor.</It>
        </p>
      </Reveal>

      <Reveal delay={60}><TutarlilikGrafigi /></Reveal>

      <Reveal delay={100}>
        <Metin className="mt-14">
          Asıl önemli olan bu ikinci grafik. Tek bir bölgede görülen örüntü tesadüf olabilir;
          birbirinden yüzlerce kilometre uzaktaki üç bölgede tekrarlanıyorsa{' '}
          <V>modelin tutunabileceği gerçek bir yapı var demektir.</V> Fizibilite testinin
          asıl cevabı da bu.
        </Metin>
      </Reveal>
    </Bolum>
  )
}

/* ----------------------------------------------------------------- sırada */

export function Sirada() {
  return (
    <Bolum id="sirada">
      <div className="grid lg:grid-cols-12 gap-x-16 gap-y-12">
        <div className="lg:col-span-5">
          <Reveal>
            <Baslik>
              Sırada <It>tahmin</It> var
            </Baslik>
          </Reveal>
          <Reveal delay={90}>
            <Metin className="mt-8">
              Buraya kadar her şey geçmişi ölçmek. Ama bu yaz yanan bir orman için iki yıl
              bekleyemeyiz. <V>Modelin tek işi bu bekleyişi ortadan kaldırmak:</V> yangından
              hemen sonra eğime, yüksekliğe, ağaç oranına ve yola bakıp iki yıl sonrasını
              bugünden tahmin etmek.
            </Metin>
          </Reveal>
        </div>

        <div className="lg:col-span-7">
          <Reveal delay={130}>
            <div className="border-t border-white/15">
              {[
                ['Hafta 2', 'Model ve doğrulama',
                  'Sınav şu: iki bölgede öğren, hiç görmediğin üçüncü bölgede test et. Gerçek hayatta karşılaşacağı durum bu.'],
                ['Hafta 3', 'Harita ve açıklama',
                  'Harita üzerinde öncelik sıralaması ve her bölge için düz Türkçe gerekçe.'],
              ].map(([h, b, m]) => (
                <div key={h} className="grid sm:grid-cols-12 gap-x-8 gap-y-2 py-7 border-b border-white/[0.09]">
                  <div className="sm:col-span-3 text-[#e8702a] text-[14px] tabular-nums">{h}</div>
                  <div className="sm:col-span-9">
                    <h3 className="text-white text-[18px] font-medium tracking-[-0.02em] mb-2">{b}</h3>
                    <p className="text-white/55 text-[15px] leading-[1.62]">{m}</p>
                  </div>
                </div>
              ))}
            </div>
          </Reveal>

          <Reveal delay={170}>
            <p className="mt-9 text-white/45 text-[14.5px] leading-[1.65] max-w-[58ch]">
              <span className="text-white/70">Bilinen sınırlar.</span> Üç bölge de aynı
              yaza ait, dolayısıyla mevsimsel genelleme farklı yıllardan veriyle
              doğrulanmalı. Yanık alanların bir bölümünde idari müdahale yapılmış;
              oradaki ölçüm saf doğal iyileşmeyi yansıtmıyor.
            </p>
          </Reveal>
        </div>
      </div>

      <Reveal delay={200}>
        <div className="mt-24 border-t border-white/15 pt-9">
          <h3 className="text-white text-[19px] font-medium tracking-[-0.02em] mb-2">
            Huawei Cloud üzerinde çalışacak mimari
          </h3>
          <Metin className="mb-9">
            Fizibilite testi hız için yerel makinede koştu. Sistemin buluta taşınan
            hâli dört parçadan oluşuyor.
          </Metin>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-x-8 gap-y-8">
            {[
              ['OBS', 'Ham uydu çıktıları', 'Nesne depolama. Her yangın için üretilen katmanlar burada tutuluyor.'],
              ['GaussDB', 'Öznitelik tablosu', 'Hücre başına ölçümler ve üretilen öncelik skorları.'],
              ['ModelArts', 'Model eğitimi', 'Random Forest eğitimi ve yeni yangınlar için tahmin.'],
              ['Huawei LLM', 'Açıklama katmanı', 'Skoru düz Türkçeye çeviriyor. Karar vermiyor, gerekçe yazıyor.'],
            ].map(([ad, rol, aciklama]) => (
              <div key={ad} className="border-t border-[#e8702a]/30 pt-4">
                <div className="text-[#e8702a] text-[15px] font-medium tracking-[-0.01em]">
                  {ad}
                </div>
                <div className="text-white text-[14px] mt-1.5">{rol}</div>
                <p className="text-white/50 text-[13.5px] leading-[1.6] mt-2.5">
                  {aciklama}
                </p>
              </div>
            ))}
          </div>
        </div>
      </Reveal>
    </Bolum>
  )
}

/* ------------------------------------------------------------------ footer */

export function Footer() {
  return (
    <footer className="px-5 sm:px-8 py-16 border-t border-white/[0.09]">
      <div className="max-w-6xl mx-auto grid lg:grid-cols-12 gap-x-16 gap-y-8">
        <div className="lg:col-span-5">
          <div className="flex items-center gap-2.5 mb-5">
            <svg width="22" height="22" viewBox="0 0 256 256" fill="#ffffff" aria-hidden="true">
              <path d="M 256 256 L 128 256 L 0 128 L 128 128 Z M 256 128 L 128 128 L 0 0 L 128 0 Z" />
            </svg>
            <span className="text-white text-xl font-playfair italic leading-[1.15]">
              FireRecover
            </span>
          </div>
          <p className="text-white/45 text-[14.5px] leading-[1.65]">
            Huawei ICT Competition, Innovation Track. Üç kişilik öğrenci ekibi.
          </p>
        </div>
        <div className="lg:col-span-7">
          <p className="text-white/45 text-[14.5px] leading-[1.65] max-w-[62ch]">
            Sayfadaki bütün sayılar projenin kendi kodundan üretildi ve yeniden
            çalıştırılabilir. Sistem karar vermez, sıralama önerir ve gerekçesini yazar.
          </p>
          <p className="text-white/28 text-[12.5px] leading-[1.6] mt-5 max-w-[62ch]">
            Veri kaynakları: Copernicus Sentinel-2 ve Copernicus DEM, ESA WorldCover,
            OpenStreetMap, Microsoft Planetary Computer.
          </p>
        </div>
      </div>
    </footer>
  )
}
