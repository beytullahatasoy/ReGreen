import Reveal from './Reveal'
import { Bolum, Baslik, It, Metin } from './Kabuk'

/* ─────────────────────────────────────────────────────────────────────────────
   SLIDE 1 — FROM PRIORITY TO ACTION
   Shows a realistic Recovery Zone interface mockup + flow + event cards
───────────────────────────────────────────────────────────────────────────── */

function StatusPill({ label, active = false }: { label: string; active?: boolean }) {
  return (
    <span className={
      active
        ? 'inline-block bg-[#e8702a] text-white text-[12px] font-medium px-3 py-1 rounded-full'
        : 'inline-block border border-white/20 text-white/60 text-[12px] font-medium px-3 py-1 rounded-full'
    }>
      {label}
    </span>
  )
}

function EventCard({
  type, date, location, filled, total, task,
}: {
  type: string; date: string; location: string;
  filled: number; total: number; task?: string
}) {
  const pct = Math.round((filled / total) * 100)
  return (
    <div className="border border-white/[0.13] rounded-2xl p-5 bg-white/[0.03] flex flex-col gap-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-[#e8702a] text-[11px] font-medium tracking-[0.06em] uppercase mb-1">{type}</div>
          <div className="text-white text-[15px] font-medium tracking-[-0.01em]">{date}</div>
          <div className="text-white/45 text-[13px] mt-0.5">{location}</div>
        </div>
        <div className="text-right shrink-0">
          <div className="text-white text-[22px] font-normal tabular-nums tracking-[-0.04em] leading-none">
            {filled}<span className="text-white/35 text-[14px]">/{total}</span>
          </div>
          <div className="text-white/40 text-[11px] mt-1">volunteers</div>
        </div>
      </div>
      {/* progress bar */}
      <div className="h-[3px] w-full bg-white/[0.09] rounded-full overflow-hidden">
        <div className="h-full bg-[#e8702a] rounded-full" style={{ width: `${pct}%` }} />
      </div>
      {task && (
        <p className="text-white/45 text-[13px] leading-[1.55]">{task}</p>
      )}
      <div>
        <span className="inline-block bg-white text-gray-900 text-[12px] font-semibold px-5 py-2 rounded-full">
          Volunteer
        </span>
      </div>
    </div>
  )
}

export function RecoveryZone() {
  return (
    <Bolum id="recovery">
      <Reveal>
        <div className="text-[#e8702a] text-[13px] font-medium mb-6">From priority to action</div>
        <Baslik className="max-w-[22ch]">
          A prioritised area becomes a <It>Recovery Zone</It>
        </Baslik>
      </Reveal>
      <Reveal delay={80}>
        <Metin className="mt-8">
          ReGreen's ranking doesn't stop at a map pin. A high-priority area becomes a
          trackable Recovery Zone — reviewed by a verified organisation, open for
          volunteer action.
        </Metin>
      </Reveal>

      {/* Flow strip */}
      <Reveal delay={110}>
        <div className="mt-10 flex flex-wrap items-end gap-x-3 gap-y-4">
          {[
            'AI Priority',
            'Recovery Zone',
            'Expert / Org.',
            'Volunteer Action',
            'Field Data',
            '12-Month Tracking',
          ].map((ad, i, arr) => (
            <div key={ad} className="flex items-end gap-3">
              <div className="border-t border-[#e8702a]/30 pt-3 min-w-[8rem]">
                <div className="text-[#e8702a] text-[11px] font-medium tabular-nums">
                  {String(i + 1).padStart(2, '0')}
                </div>
                <div className="text-white text-[14px] font-medium tracking-[-0.015em] mt-1 pr-2">
                  {ad}
                </div>
              </div>
              {i < arr.length - 1 && (
                <span className="hidden sm:block text-white/25 text-base pb-1">→</span>
              )}
            </div>
          ))}
        </div>
      </Reveal>

      {/* Zone mockup + event cards */}
      <Reveal delay={150}>
        <div className="mt-16 grid lg:grid-cols-12 gap-x-10 gap-y-10">

          {/* Left — Zone interface mockup */}
          <div className="lg:col-span-5">
            <p className="text-white/35 text-[11px] tracking-[0.07em] uppercase mb-4">
              regreen.io / zones / mugla-07
            </p>
            <div className="border border-white/[0.13] rounded-2xl p-6 bg-white/[0.02]">
              <div className="flex items-start justify-between gap-4 mb-5">
                <div>
                  <div className="text-white/40 text-[11px] tracking-[0.07em] uppercase mb-1">Recovery Zone</div>
                  <div className="text-white text-[20px] font-medium tracking-[-0.02em]">Muğla — Zone 07</div>
                </div>
                <StatusPill label="HIGH PRIORITY" active />
              </div>

              <div className="space-y-0 border-t border-white/10">
                {[
                  ['Current Status', 'Under Expert Assessment'],
                  ['Intervention Status', 'Preparing for Field Action'],
                  ['Partner Organisation', 'Verified Organisation'],
                  ['Rehabilitation', 'Process initiated by organisation'],
                ].map(([k, v]) => (
                  <div key={k} className="flex justify-between gap-4 py-3.5 border-b border-white/[0.07]">
                    <span className="text-white/45 text-[13px]">{k}</span>
                    <span className="text-white text-[13px] font-medium text-right max-w-[16ch]">{v}</span>
                  </div>
                ))}
              </div>

              <div className="mt-5 flex flex-wrap gap-2.5">
                <button className="bg-[#e8702a] hover:bg-[#d2611f] text-white text-[13px] font-medium px-5 py-2.5 rounded-full transition-colors">
                  Follow This Zone
                </button>
                <button className="bg-white hover:bg-gray-100 text-gray-900 text-[13px] font-semibold px-5 py-2.5 rounded-full transition-colors">
                  Volunteer
                </button>
              </div>
              <p className="text-white/30 text-[12px] mt-3 leading-relaxed">
                No account required. Minimum info collected only when needed.
              </p>
            </div>
          </div>

          {/* Right — Event cards */}
          <div className="lg:col-span-7 flex flex-col gap-4">
            <p className="text-white/35 text-[11px] tracking-[0.07em] uppercase mb-1">
              Activities open in this zone
            </p>
            <EventCard
              type="Reforestation Activity"
              date="October 17"
              location="Muğla — Recovery Zone 07"
              filled={31}
              total={45}
            />
            <EventCard
              type="Clean-up Activity"
              date="November 8"
              location="Recovery Zone 07"
              filled={18}
              total={30}
            />
            <div className="border border-white/[0.13] rounded-2xl p-5 bg-white/[0.03]">
              <div className="text-[#e8702a] text-[11px] font-medium tracking-[0.06em] uppercase mb-2">Field Observation Task</div>
              <div className="text-white text-[15px] font-medium tracking-[-0.01em] mb-3">Collect supporting field observations for experts</div>
              <div className="space-y-1.5 mb-4">
                {[
                  'Photograph the designated observation point',
                  'Complete a short expert-prepared form',
                ].map((t) => (
                  <div key={t} className="flex items-start gap-2 text-white/55 text-[13px]">
                    <span className="text-[#e8702a] mt-0.5 shrink-0">–</span>
                    {t}
                  </div>
                ))}
              </div>
              <p className="text-white/30 text-[12px] mb-4 leading-relaxed">
                Observations are supporting field data reviewed by the organisation — not automatically accepted as scientific ground truth.
              </p>
              <span className="inline-block bg-white text-gray-900 text-[12px] font-semibold px-5 py-2 rounded-full">
                View Task
              </span>
            </div>
          </div>
        </div>
      </Reveal>
    </Bolum>
  )
}

/* ─────────────────────────────────────────────────────────────────────────────
   SLIDE 2 — FOLLOW THE RECOVERY
   Timeline + result card mockup + citizen science note
───────────────────────────────────────────────────────────────────────────── */

function TimelineStep({
  label, sub, accent = false, last = false,
}: { label: string; sub: string; accent?: boolean; last?: boolean }) {
  return (
    <div className="flex gap-4">
      <div className="flex flex-col items-center">
        <div className={`w-2.5 h-2.5 rounded-full shrink-0 mt-1 ${accent ? 'bg-[#e8702a]' : 'bg-white/30'}`} />
        {!last && <div className="w-px flex-1 bg-white/[0.09] mt-1.5" />}
      </div>
      <div className={`pb-7 ${last ? '' : ''}`}>
        <div className={`text-[15px] font-medium tracking-[-0.015em] ${accent ? 'text-[#e8702a]' : 'text-white'}`}>
          {label}
        </div>
        <div className="text-white/45 text-[13.5px] mt-0.5 leading-snug">{sub}</div>
      </div>
    </div>
  )
}

export function GonulluYolculugu() {
  return (
    <Bolum id="gonullu">
      <Reveal>
        <div className="text-[#e8702a] text-[13px] font-medium mb-6">Follow the recovery</div>
        <Baslik className="max-w-[22ch]">
          Volunteers are <It>active participants,</It> not passive supporters
        </Baslik>
      </Reveal>
      <Reveal delay={80}>
        <Metin className="mt-8">
          ReGreen is not a separate volunteering platform. It connects AI-determined
          priority to real field action — and lets anyone follow how the area recovers
          over time.
        </Metin>
      </Reveal>

      {/* Four core actions */}
      <Reveal delay={110}>
        <div className="mt-10 grid grid-cols-2 sm:grid-cols-4 gap-px bg-white/[0.07] rounded-2xl overflow-hidden border border-white/[0.07]">
          {[
            ['Follow', 'Track a Recovery Zone with no account needed'],
            ['Volunteer', 'Join activities opened by verified organisations'],
            ['Contribute Field Data', 'Photos, observations and short expert forms'],
            ['See the Recovery', 'View zone status updates after 12 months'],
          ].map(([h, d]) => (
            <div key={h} className="bg-[#0b0b0c] p-5">
              <div className="text-[#e8702a] text-[13px] font-medium mb-2">{h}</div>
              <p className="text-white/50 text-[13px] leading-[1.55]">{d}</p>
            </div>
          ))}
        </div>
      </Reveal>

      {/* Timeline + Result card */}
      <Reveal delay={140}>
        <div className="mt-16 grid lg:grid-cols-12 gap-x-12 gap-y-12">

          {/* Timeline */}
          <div className="lg:col-span-4">
            <p className="text-white/35 text-[11px] tracking-[0.07em] uppercase mb-6">Recovery journey</p>
            <TimelineStep label="Post-Fire" sub="Initial satellite assessment" />
            <TimelineStep label="Prioritisation" sub="Recovery Zone created by ReGreen" accent />
            <TimelineStep label="Field Action" sub="Organisation + volunteers active" />
            <TimelineStep label="12 Months" sub="Same seasonal period — re-analysis" accent />
            <TimelineStep label="Recovery Update" sub="Results published on zone page" last />
          </div>

          {/* Result card mockup */}
          <div className="lg:col-span-8">
            <p className="text-white/35 text-[11px] tracking-[0.07em] uppercase mb-4">
              regreen.io / zones / mugla-07 / recovery
            </p>
            <div className="border border-white/[0.13] rounded-2xl p-6 bg-white/[0.02]">
              <div className="text-white/40 text-[11px] tracking-[0.07em] uppercase mb-1">Your followed zone</div>
              <div className="text-white text-[18px] font-medium tracking-[-0.02em] mb-4">Muğla — Zone 07</div>

              <div className="grid sm:grid-cols-2 gap-3 mb-5">
                {[
                  ['Annual Recovery Assessment', 'Completed'],
                  ['Recovery Trend', 'Improving'],
                ].map(([k, v]) => (
                  <div key={k} className="border border-white/[0.09] rounded-xl px-4 py-3">
                    <div className="text-white/40 text-[12px] mb-1">{k}</div>
                    <div className="text-white text-[15px] font-medium">{v}</div>
                  </div>
                ))}
              </div>

              <div className="text-white/35 text-[11px] tracking-[0.07em] uppercase mb-3">Satellite Comparison</div>
              <div className="grid grid-cols-2 gap-3 mb-5">
                {[
                  ['Post-Fire', '#3d2015'],
                  ['12 Months Later', '#1e3d1a'],
                ].map(([label, bg]) => (
                  <div key={label}>
                    <div
                      className="w-full h-24 rounded-xl flex items-end p-3"
                      style={{ background: `linear-gradient(135deg, ${bg}cc, ${bg}44)`, border: '1px solid rgba(255,255,255,0.08)' }}
                    >
                      <span className="text-white/60 text-[11px] font-medium">{label}</span>
                    </div>
                  </div>
                ))}
              </div>

              <p className="text-white/35 text-[12px] leading-relaxed mb-4">
                Recovery is assessed using same-season satellite data to avoid seasonal variation.
                Individual contributions are not attributed as percentages.
              </p>

              <span className="inline-block border border-white/20 text-white/70 text-[12px] font-medium px-5 py-2 rounded-full">
                View Recovery Report
              </span>
            </div>
          </div>
        </div>
      </Reveal>

      {/* Citizen science note */}
      <Reveal delay={80}>
        <div className="mt-16 pt-10 border-t border-white/15 grid lg:grid-cols-12 gap-x-12 gap-y-6">
          <div className="lg:col-span-5">
            <h3 className="text-white text-[18px] font-medium tracking-[-0.015em] mb-2">
              Satellite data + field observations
            </h3>
            <p className="text-white/55 text-[15px] leading-[1.62]">
              Volunteers can also contribute to the data layer — not just show up for events.
              Expert-prepared tasks make this simple.
            </p>
          </div>
          <div className="lg:col-span-7 grid sm:grid-cols-3 gap-x-6 gap-y-4">
            {[
              ['Geotagged photo', 'From a designated observation point'],
              ['Simple field form', 'Short questions prepared by experts'],
              ['Reviewed by org', 'Supporting data — not auto-accepted as fact'],
            ].map(([h, d]) => (
              <div key={h} className="border-t border-[#e8702a]/30 pt-4">
                <div className="text-white text-[14px] font-medium tracking-[-0.01em] mb-1.5">{h}</div>
                <p className="text-white/45 text-[13px] leading-[1.55]">{d}</p>
              </div>
            ))}
          </div>
        </div>
      </Reveal>

      {/* Core message strip */}
      <Reveal delay={80}>
        <p className="mt-14 text-white/40 text-[13.5px] leading-[1.65] tracking-[0.05em] uppercase">
          Data → Priority → Action → Community → Recovery
        </p>
      </Reveal>
    </Bolum>
  )
}
