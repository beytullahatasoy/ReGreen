import Reveal from './Reveal'
import { Bolum, Baslik, It, Metin, V } from './Kabuk'
import { CokusGrafigi, IzlenceGrafigi, TutarlilikGrafigi } from './Charts'

/* ------------------------------------------------------------------ problem */

export function Sorun() {
  return (
    <Bolum id="sorun">
      <div className="grid lg:grid-cols-12 gap-y-12 gap-x-16">
        <div className="lg:col-span-7">
          <Reveal>
            <Baslik>
              When the fire is out, <It>the real decision</It> begins
            </Baslik>
          </Reveal>
          <Reveal delay={90}>
            <Metin className="mt-8">
              Every summer in Turkey, tens of thousands of hectares of forest burn.
              Once the flames are out, a much quieter question emerges:{' '}
              <V>with limited crews, budgets and seedlings — which area should be addressed first?</V>
            </Metin>
          </Reveal>
          <Reveal delay={140}>
            <Metin className="mt-6">
              Today this decision relies on field experience. Experience matters, but
              comparing hundreds of areas simultaneously against the same criteria is
              beyond human capacity.
              ReGreen does not decide — <V>it proposes a data-driven prioritisation.</V>
            </Metin>
          </Reveal>
        </div>

        <div className="lg:col-span-5 lg:pt-3">
          <Reveal delay={180}>
            <p className="text-white/40 text-sm mb-6 leading-relaxed">
              An area can be high-priority for two distinct reasons.
            </p>
            <div className="border-t border-white/15">
              <div className="py-6 border-b border-white/[0.09]">
                <h3 className="text-white text-[18px] font-medium tracking-[-0.015em] mb-2">
                  Cannot recover on its own
                </h3>
                <p className="text-white/55 text-[15px] leading-[1.6]">
                  Steep slope, high elevation, conifer forest whose cones are destroyed by fire.
                </p>
              </div>
              <div className="py-6">
                <h3 className="text-white text-[18px] font-medium tracking-[-0.015em] mb-2">
                  Failure to recover is costly
                </h3>
                <p className="text-white/55 text-[15px] leading-[1.6]">
                  A nearby village. Without tree cover, the first heavy rain brings soil erosion.
                </p>
              </div>
            </div>
          </Reveal>
        </div>
      </div>
    </Bolum>
  )
}

/* ------------------------------------------------------------------ method */

export function Yontem() {
  const bant = [
    { d: '0.1 – 0.2', a: 'Bare soil, ash', w: '26%', c: '#6b4a35' },
    { d: '0.2 – 0.4', a: 'Sparse grass & shrub', w: '28%', c: '#8f7a3c' },
    { d: '0.4 – 0.7', a: 'Healthy forest', w: '46%', c: '#2f7a35' },
  ]
  return (
    <Bolum id="yontem">
      <Reveal>
        <Baslik className="max-w-[24ch]">
          No one wrote the right answer, so we <It>asked nature</It>
        </Baslik>
      </Reveal>

      <Reveal delay={90}>
        <Metin className="mt-8">
          Training this kind of system requires a record saying "this area deserved
          priority score 8." No such record exists. Instead,{' '}
          <V>we read nature's own answer:</V> an area that still hasn't recovered
          two years after a fire is the area that needs human intervention.
        </Metin>
      </Reveal>

      <Reveal delay={130}>
        <Metin className="mt-6">
          We measure this from satellite. Healthy leaves strongly reflect near-infrared
          and absorb red. When the satellite computes the ratio,{' '}
          <V>a single number emerges showing how much live vegetation exists at that point.</V>
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
          The system doesn't learn the abstract concept of "priority." It learns a forest's{' '}
          <It>natural recovery capacity.</It> Priority is exactly the opposite.
        </p>
      </Reveal>
    </Bolum>
  )
}

/* --------------------------------------------------------- feasibility test */

const SORULAR = [
  ['Data sources', 'Sentinel-2, Copernicus DEM, ESA WorldCover and OpenStreetMap. All four are open data — no cost, no account, no API key required.'],
  ['Measurement accuracy', 'Vegetation density is stable for four years, then collapses in the fire month. The method detects the event on the correct date.'],
  ['Scalability', '53 fires nationwide, 303,153 cells, seven fire seasons — on a single machine, no parallel infrastructure.'],
  ['Learnable structure', 'The same relationships appear in the same direction across 27 independent spatial groups. The path to building a model is clear.'],
]

export function Test() {
  return (
    <Bolum id="test">
      <Reveal>
        <div className="text-[#e8702a] text-[13px] font-medium mb-6">Feasibility study</div>
        <Baslik className="max-w-[22ch]">
          Before building the model, <It>four conditions</It> were verified
        </Baslik>
      </Reveal>
      <Reveal delay={90}>
        <Metin className="mt-8">
          The system's viability depended on four things: data access, measurement
          accuracy, scalability, and the existence of a learnable structure in the data.{' '}
          <V>All four were confirmed.</V>
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

      <Reveal delay={170}>
        <figure className="m-0 mt-24">
          <div className="grid sm:grid-cols-2 gap-4">
            {[
              ['/yangin-oncesi.jpg', 'Before the fire', 'Closed forest canopy'],
              ['/yangin-sonrasi.jpg', '40 days later', 'Same frame, same satellite'],
            ].map(([src, t, a]) => (
              <div key={src}>
                <img src={src} alt={`${t}: satellite image`}
                  className="w-full h-auto block" loading="lazy" />
                <div className="mt-3 flex items-baseline gap-3">
                  <span className="text-white text-[15px]">{t}</span>
                  <span className="text-white/40 text-[13.5px]">{a}</span>
                </div>
              </div>
            ))}
          </div>
          <figcaption className="text-white/40 text-[14px] mt-6 leading-relaxed max-w-[62ch]">
            The burn scar is visible to the naked eye. But the system's job is not to look
            — it's to measure, because you cannot compare hundreds of areas by eye.
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
            ['53', 'fires, nationwide'],
            ['303,153', 'cells analysed'],
            ['37,163', 'burned cells'],
            ['16,074', 'reliable measurements'],
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
          The burned land was divided into 250-metre grid cells — 6.25 hectares each.
          For each cell we collected six measurements: fire severity, slope, elevation,
          two independent tree-cover readings and distance to the nearest road. Three
          further candidates were tested and dropped because we could not measure any
          contribution.{' '}
          <V>Not a single number was entered manually.</V>
        </Metin>
      </Reveal>

    </Bolum>
  )
}

/* ----------------------------------------------------------------- findings */

export function Bulgular() {
  return (
    <Bolum id="bulgular">
      <Reveal>
        <Baslik className="max-w-[22ch]">
          The test revealed <It>something unexpected</It>
        </Baslik>
      </Reveal>
      <Reveal delay={90}>
        <Metin className="mt-8 mb-14">
          Regardless of how severe the fire was, all areas converge to the same
          vegetation density two years later. The difference lies in how rich they were{' '}
          <V>before</V> the fire.
        </Metin>
      </Reveal>

      <Reveal delay={60}><IzlenceGrafigi /></Reveal>

      <Reveal delay={100}>
        <p className="my-20 text-white text-[21px] sm:text-[25px] leading-[1.45] tracking-[-0.022em] max-w-[56ch] text-balance">
          Severely burned areas were richer, lost more, and ended up in the same place.
          So severe burning doesn't just mean slow recovery —{' '}
          <It>it means a far larger deficit.</It>
        </p>
      </Reveal>

      <Reveal delay={60}><TutarlilikGrafigi /></Reveal>

      <Reveal delay={100}>
        <Metin className="mt-14">
          The second graph is what matters most. A pattern found in a single fire could
          be coincidence; when it repeats across 27 spatial groups spread over seven
          fire seasons,{' '}
          <V>there is a real structure the model can learn from.</V> Overlapping and
          neighbouring fires are treated as one group, so no area is ever used for both
          training and testing.
        </Metin>
      </Reveal>
    </Bolum>
  )
}

/* ----------------------------------------------------------------- what's next */

export function Sirada() {
  return (
    <Bolum id="sirada">
      <div className="grid lg:grid-cols-12 gap-x-16 gap-y-12">
        <div className="lg:col-span-5">
          <Reveal>
            <Baslik>
              The model <It>is trained</It>
            </Baslik>
          </Reveal>
          <Reveal delay={90}>
            <Metin className="mt-8">
              Everything above measures the past. But we cannot wait two years for a
              forest that burned this summer.{' '}
              <V>The model's sole purpose is to remove that wait:</V> from six
              measurements available within days of a fire, it predicts what the land
              will look like two years on.
            </Metin>
          </Reveal>
          <Reveal delay={130}>
            <Metin className="mt-6">
              Today, standard practice is to start with the most severely burned area.
              We measured that baseline and beat it.
            </Metin>
          </Reveal>
        </div>

        <div className="lg:col-span-7">
          <Reveal delay={130}>
            <div className="border-t border-white/15">
              {[
                ['Done', 'Model & validation',
                  'Random Forest, trained on 16,074 labelled cells. Validated by holding out entire spatial groups — never the same area in training and testing. Ranking accuracy within a fire: 0.58 Spearman, positive in 26 of 27 groups.'],
                ['Done', 'Beating current practice',
                  'Asked to name the worst-affected fifth of a fire, ranking by burn severity alone is right 39% of the time. The model is right 47% — a fifth more correct parcels for the same budget.'],
                ['Now', 'Map & explanation',
                  'A priority ranking on the map and plain-language reasoning for every parcel. 53 fires and 37,163 cells are already handed over to the backend.'],
              ].map(([h, b, m]) => (
                <div key={b} className="grid sm:grid-cols-12 gap-x-8 gap-y-2 py-7 border-b border-white/[0.09]">
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
              <span className="text-white/70">Known limitations.</span> 2021 accounts for
              roughly half the spatial groups — no other Turkish fire season resembled it.
              The data is concentrated in the Aegean and Mediterranean, because that is where
              Turkey's forest-fire burden sits: we measured that 95% of large burn scars
              inland are stubble burning, not forest fire. And some burned areas received
              administrative intervention, so those measurements are not pure natural recovery.
            </p>
          </Reveal>
        </div>
      </div>

      <Reveal delay={200}>
        <div className="mt-24 border-t border-white/15 pt-9">
          <h3 className="text-white text-[19px] font-medium tracking-[-0.02em] mb-2">
            Architecture to run on Huawei Cloud
          </h3>
          <Metin className="mb-9">
            The feasibility study ran locally for speed. The cloud-deployed system
            consists of four components.
          </Metin>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-x-8 gap-y-8">
            {[
              ['OBS', 'Raw satellite outputs', 'Object storage. All layers generated for each fire event are stored here.'],
              ['GaussDB', 'Feature table', 'Per-cell measurements and the generated priority scores.'],
              ['ModelArts', 'Model training', 'Random Forest training and prediction for new fire events.'],
              ['Huawei LLM', 'Explanation layer', 'Converts scores into plain language. It doesn\'t decide — it writes the reasoning.'],
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
              ReGreen
            </span>
          </div>
          <p className="text-white/45 text-[14.5px] leading-[1.65]">
            Huawei ICT Competition, Innovation Track. Three-person student team.
          </p>
        </div>
        <div className="lg:col-span-7">
          <p className="text-white/45 text-[14.5px] leading-[1.65] max-w-[62ch]">
            All figures on this page were produced by the project's own code and are
            fully reproducible. The system doesn't decide — it ranks, recommends and
            writes its reasoning.
          </p>
          <p className="text-white/28 text-[12.5px] leading-[1.6] mt-5 max-w-[62ch]">
            Data sources: Copernicus Sentinel-2, Copernicus DEM, ESA WorldCover,
            OpenStreetMap, Microsoft Planetary Computer.
          </p>
        </div>
      </div>
    </footer>
  )
}
