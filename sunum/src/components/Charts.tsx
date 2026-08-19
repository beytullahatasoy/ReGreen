/**
 * All values taken directly from firerecover_pipeline.py, sinyal_kontrol.py
 * and yangin_dogrulama.py outputs. No invented numbers.
 *
 * Style rules:
 *  - Single accent colour (orange). Classes are ordered so the same colour
 *    is used at different opacity steps, not different hues.
 *  - No filled background bars (no track).
 *  - Numbers placed directly next to markers; no separate legend table.
 */

const OK1 = '#f6b28c'
const OK2 = '#ea7f4c'
const OK3 = '#cf5216'
const IZ = 'rgba(255,255,255,0.09)'
const EKSEN = 'rgba(255,255,255,0.38)'
const ETIKET = 'rgba(255,255,255,0.62)'

function Cerceve({
  baslik, alt, children, not,
}: { baslik: string; alt: string; children: React.ReactNode; not?: string }) {
  return (
    <figure className="m-0 border-t border-white/15 pt-7">
      <figcaption className="mb-7 max-w-[54ch]">
        <h3 className="text-white text-lg sm:text-xl font-medium tracking-[-0.02em] leading-snug">
          {baslik}
        </h3>
        <p className="text-white/50 text-sm mt-2 leading-relaxed">{alt}</p>
      </figcaption>
      {children}
      {not && <p className="text-white/35 text-xs mt-6 leading-relaxed max-w-[60ch]">{not}</p>}
    </figure>
  )
}

/* ---------------------------------------------- 1) annual collapse ---------- */

export function CokusGrafigi() {
  const d = [
    ['2018', 0.625], ['2019', 0.656], ['2020', 0.604], ['2021', 0.594],
    ['2022', 0.186], ['2023', 0.275], ['2024', 0.277], ['2025', 0.336], ['2026', 0.329],
  ] as [string, number][]
  const x = (i: number) => 60 + i * 77.5
  const y = (v: number) => 240 - (v / 0.7) * 200
  const cizgi = d.map(([, v], i) => `${x(i)},${y(v)}`).join(' ')

  return (
    <Cerceve
      baslik="Same point, every summer, nine years"
      alt="A single point at the exact centre of the burn scar. Four years of flat line, then a cliff."
      not="Drought alone doesn't cause such a sudden drop, and both images are cloud-free. The collapse is confined to a 40-day window — the same window the fire occurred in. This is the proof that the method is measuring the right thing."
    >
      <svg viewBox="0 0 720 285" className="w-full h-auto" role="img"
        aria-label="Annual vegetation density 2018–2026: stable between 0.59 and 0.66 from 2018–2021, drops to 0.186 in 2022, recovers to 0.329 by 2026">
        <defs>
          <linearGradient id="dolgu" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={OK2} stopOpacity="0.26" />
            <stop offset="100%" stopColor={OK2} stopOpacity="0" />
          </linearGradient>
        </defs>

        {[0.6, 0.4, 0.2].map((v) => (
          <g key={v}>
            <line x1="60" y1={y(v)} x2="690" y2={y(v)} stroke={IZ} />
            <text x="52" y={y(v) + 4} textAnchor="end" fontSize="11" fill={EKSEN}>
              {v.toFixed(1)}
            </text>
          </g>
        ))}
        <line x1="60" y1="240" x2="690" y2="240" stroke="rgba(255,255,255,0.2)" />

        <polygon points={`${cizgi} ${x(8)},240 ${x(0)},240`} fill="url(#dolgu)" />
        <polyline points={cizgi} fill="none" stroke={OK2} strokeWidth="2"
          strokeLinejoin="round" strokeLinecap="round" />

        {/* fire moment */}
        <line x1={(x(3) + x(4)) / 2} y1="34" x2={(x(3) + x(4)) / 2} y2="240"
          stroke="rgba(255,255,255,0.34)" strokeWidth="1" strokeDasharray="3 4" />
        <text x={(x(3) + x(4)) / 2 + 8} y="46" fontSize="11.5" fill={ETIKET}>
          28 July 2021
        </text>

        {d.map(([yil, v], i) => (
          <g key={yil}>
            <circle cx={x(i)} cy={y(v)} r={i === 3 || i === 4 ? 5 : 3.5}
              fill={i === 3 || i === 4 ? '#fff' : OK2} />
            <text x={x(i)} y="262" textAnchor="middle" fontSize="11" fill={EKSEN}>{yil}</text>
          </g>
        ))}

        <text x={x(3)} y={y(0.594) - 14} textAnchor="middle" fontSize="12" fill="#fff">0.594</text>
        <text x={x(4)} y={y(0.186) + 20} textAnchor="middle" fontSize="12" fill="#fff">0.186</text>
      </svg>
    </Cerceve>
  )
}

/* ------------------------------------------ 2) severity trajectory ---------- */

export function IzlenceGrafigi() {
  const seri = [
    { ad: 'Lightly burned', renk: OK1, v: [0.532, 0.254, 0.179, 0.224] },
    { ad: 'Moderately burned', renk: OK2, v: [0.572, 0.219, 0.168, 0.227] },
    { ad: 'Severely burned', renk: OK3, v: [0.664, 0.198, 0.172, 0.244] },
  ]
  const X = [80, 265, 450, 635]
  const y = (v: number) => 230 - (v / 0.7) * 190
  const durak = ['before fire', 'just after', 'year 1', 'year 2']

  return (
    <Cerceve
      baslik="Regardless of fire severity, all areas converge at the same point after two years"
      alt="Average of 16,074 cells across 53 fires. Three lines start from very different places and meet at one point."
      not="Year-1 values drop even below the immediate post-fire reading. Delayed death in conifer forest: the tree survives the fire, then dies the following year. Recovery only begins in year two."
    >
      <svg viewBox="0 0 720 275" className="w-full h-auto" role="img"
        aria-label="Three fire severity classes: spread from 0.532 to 0.664 before the fire, converging to 0.224–0.244 after two years">
        {[0.6, 0.4, 0.2].map((v) => (
          <g key={v}>
            <line x1="80" y1={y(v)} x2="680" y2={y(v)} stroke={IZ} />
            <text x="70" y={y(v) + 4} textAnchor="end" fontSize="11" fill={EKSEN}>
              {v.toFixed(1)}
            </text>
          </g>
        ))}
        <line x1="80" y1="230" x2="680" y2="230" stroke="rgba(255,255,255,0.2)" />

        {/* convergence highlight */}
        <circle cx={X[3]} cy={y(0.232)} r="19" fill="none" stroke="rgba(255,255,255,0.22)" />

        {seri.map((s) => (
          <polyline key={s.ad} fill="none" stroke={s.renk} strokeWidth="2.25"
            strokeLinejoin="round" strokeLinecap="round"
            points={s.v.map((v, i) => `${X[i]},${y(v)}`).join(' ')} />
        ))}
        {seri.map((s) => (
          <g key={s.ad}>
            <circle cx={X[0]} cy={y(s.v[0])} r="4.5" fill={s.renk} />
            <text x={X[0] - 12} y={y(s.v[0]) + 4} textAnchor="end" fontSize="12" fill={ETIKET}>
              {s.v[0].toFixed(3)}
            </text>
          </g>
        ))}

        <text x={X[3] + 26} y={y(0.232) + 4} fontSize="12.5" fill="#fff">0.22 – 0.24</text>

        {durak.map((t, i) => (
          <text key={t} x={X[i]} y="254" textAnchor="middle" fontSize="11" fill={EKSEN}>{t}</text>
        ))}
      </svg>

      <div className="flex flex-wrap gap-x-7 gap-y-2 mt-6">
        {seri.map((s) => (
          <span key={s.ad} className="inline-flex items-center gap-2.5 text-sm text-white/60">
            <i className="h-[3px] w-6 shrink-0" style={{ background: s.renk }} />
            {s.ad}
          </span>
        ))}
      </div>
    </Cerceve>
  )
}

/* ----------------------------------------------- 3) slope ---------------- */

export function EgimGrafigi() {
  const d = [
    ['0–5°', 0.276, 883], ['5–10°', 0.316, 3933], ['10–15°', 0.347, 4718],
    ['15–20°', 0.368, 3357], ['20–25°', 0.379, 1883], ['25°+', 0.380, 1300],
  ] as [string, number, number][]
  const x = (i: number) => 90 + i * 108
  const y = (v: number) => 195 - ((v - 0.25) / 0.16) * 150

  return (
    <Cerceve
      baslik="Steeper slopes recover less"
      alt="Vertical axis: remaining vegetation deficit two years on. Higher is worse."
      not="16,074 labelled cells across 53 fires. The relationship is monotonic across every bin and holds in the same direction in 21 of 27 spatial groups."
    >
      <svg viewBox="0 0 720 250" className="w-full h-auto" role="img"
        aria-label="Remaining vegetation deficit by slope class: rises from 0.276 at 0–5° to 0.380 above 25°">
        {[0.30, 0.35].map((v) => (
          <g key={v}>
            <line x1="90" y1={y(v)} x2="660" y2={y(v)} stroke={IZ} />
            <text x="80" y={y(v) + 4} textAnchor="end" fontSize="11" fill={EKSEN}>
              {v.toFixed(2)}
            </text>
          </g>
        ))}

        <polyline points={d.map(([, v], i) => `${x(i)},${y(v)}`).join(' ')}
          fill="none" stroke="rgba(255,255,255,0.22)" strokeWidth="1.5" />

        {d.map(([ad, v, n], i) => (
          <g key={ad}>
            <line x1={x(i)} y1={y(v)} x2={x(i)} y2="200" stroke={IZ} />
            <circle cx={x(i)} cy={y(v)} r="6" fill={OK2} />
            <text x={x(i)} y={y(v) - 14} textAnchor="middle" fontSize="12" fill="#fff">
              {v.toFixed(3)}
            </text>
            <text x={x(i)} y="220" textAnchor="middle" fontSize="11.5" fill={ETIKET}>{ad}</text>
            <text x={x(i)} y="237" textAnchor="middle" fontSize="10.5" fill="rgba(255,255,255,0.28)">
              {n} cells
            </text>
          </g>
        ))}
      </svg>
    </Cerceve>
  )
}

/* -------------------------------- 4) cross-region consistency ---------- */

export function TutarlilikGrafigi() {
  // Olculen degerler: 16.074 etiketli hucre, 27 mekansal grup.
  // rho = havuzlanmis Spearman. tutarli = kac grupta ayni yonde cikti.
  const satir = [
    { ad: 'Tree cover (annual)', rho: 0.556, tutarli: 24 },
    { ad: 'Tree cover (2021)', rho: 0.542, tutarli: 24 },
    { ad: 'Fire severity', rho: 0.371, tutarli: 24 },
    { ad: 'Elevation', rho: 0.272, tutarli: 20 },
    { ad: 'Slope', rho: 0.262, tutarli: 21 },
    { ad: 'Distance to road', rho: 0.105, tutarli: 20 },
  ]
  const TOPLAM = 27
  const x = (v: number) => 210 + (v / 0.6) * 400
  const y = (i: number) => 46 + i * 40

  return (
    <Cerceve
      baslik="Does the same relationship hold across every fire?"
      alt="Bar length: strength of the relationship with the recovery deficit. Right-hand figure: how many of the 27 spatial groups agree on the direction."
      not="Overlapping and neighbouring fires are merged into one spatial group, so the count is 27 rather than 53. Three further variables — distance to water, distance to settlement and aspect — were tested the same way and dropped: their contribution could not be separated from noise."
    >
      <svg viewBox="0 0 720 290" className="w-full h-auto" role="img"
        aria-label="Relationship strength of six variables. Tree cover 0.556 agreeing in 24 of 27 groups, down to distance to road 0.105 in 20 of 27">
        {[0, 0.2, 0.4, 0.6].map((v) => (
          <g key={v}>
            <line x1={x(v)} y1="26" x2={x(v)} y2="266" stroke={IZ} />
            <text x={x(v)} y="284" textAnchor="middle" fontSize="11" fill={EKSEN}>
              {v === 0 ? '0' : v.toFixed(1)}
            </text>
          </g>
        ))}

        {satir.map((s, i) => (
          <g key={s.ad}>
            <text x="192" y={y(i) + 4} textAnchor="end" fontSize="13" fill="rgba(255,255,255,0.8)">
              {s.ad}
            </text>
            <rect x={x(0)} y={y(i) - 8} width={x(s.rho) - x(0)} height="16"
              fill={s.tutarli >= 24 ? OK3 : s.tutarli >= 21 ? OK2 : OK1} />
            <text x={x(s.rho) + 10} y={y(i) + 4} fontSize="12" fill="#fff">
              {s.rho.toFixed(3)}
            </text>
            <text x="700" y={y(i) + 4} textAnchor="end" fontSize="11.5"
              fill="rgba(255,255,255,0.45)">
              {s.tutarli} / {TOPLAM}
            </text>
          </g>
        ))}
      </svg>

      <p className="text-white/45 text-[13.5px] mt-5 leading-relaxed">
        The right-hand column is the honest one. A strong average means little if the
        relationship flips direction from one fire to the next — these six hold.
      </p>
    </Cerceve>
  )
}

