import { useMemo, useState } from "react";
import { aciliyeteGore, toplamlar, useRecoveryZones } from "../../../hooks/useRecoveryZones";
import { activities } from "../data/demoData";
import { useObservations } from "../data/useObservations";
import { Icon } from "../components/Icons";
import { ObservationReview } from "../components/ObservationReview";
import { PrototypeModal } from "../components/PrototypeModal";
import { PrototypeShell } from "../components/PrototypeShell";
import {
  DemoNotice, ZoneBrief, ZoneError, ZoneLoading, ZoneRow, hektar, kare,
} from "../components/RealZone";

/**
 * The organisation screen has one job:
 *
 *     "Where do I send a crew today, and what needs my approval?"
 *
 * The previous version had 7 sections (hero, 5 counters, zone catalogue, 4
 * action buttons, activity table, observation review, recovery card) and none
 * stood out from the others. Now there are two jobs: an area queue and an
 * evidence queue.
 *
 * Zones are NO LONGER INVENTED — they come from the 53 real fires and the
 * verdict layer. Activities and observations are still demo, labelled as such.
 */
export function OrganisationWorkspace() {
  const { zones, yukleniyor, hata, eksikOzet } = useRecoveryZones();
  const [seciliId, setSeciliId] = useState<string | null>(null);
  const [action, setAction] = useState<string | null>(null);

  // Heaviest intervention load first: the organisation user sees a QUEUE, not
  // a catalogue. The top of the list is where today's attention belongs.
  const sirali = useMemo(() => aciliyeteGore(zones), [zones]);
  const secili = sirali.find((z) => z.fireId === seciliId) ?? sirali[0] ?? null;
  const toplam = useMemo(() => toplamlar(zones), [zones]);
  const observations = useObservations();
  const bekleyenKanit = observations.filter((o) => o.status === "Pending").length;
  // Activities are keyed to real fire ids, so only the selected area's
  // activities are shown — not an arbitrary first two.
  const bolgeEtkinlikleri = activities.filter((a) => a.fireId === secili?.fireId);

  return (
    <PrototypeShell mode="organisation">
      <main className="rc-main">
        <section className="rc-hero rc-hero--compact">
          <div>
            <span className="rc-kicker rc-kicker--orange">Organisation · field operations</span>
            <h1>Where should a crew go today?</h1>
            <p>
              All 53 fires ranked by intervention load from the verdict layer.
              The top of the queue is where attention is needed first.
            </p>
          </div>
        </section>

        {!yukleniyor && !hata && (
          <section className="rc-summary" aria-label="Portfolio summary">
            <article>
              <Icon name="zone" />
              <div>
                <span>Direct action</span>
                <strong>{hektar(toplam.mudahaleHa)}</strong>
                <small>{kare(toplam.mudahaleHucre)} cells · {zones.length} fires</small>
              </div>
            </article>
            <article>
              <Icon name="activity" />
              <div>
                <span>Erosion control first</span>
                <strong>{kare(toplam.erozyonHucre)}</strong>
                <small>stabilise before planting</small>
              </div>
            </article>
            <article>
              <Icon name="observe" />
              <div>
                <span>Awaiting priority call</span>
                <strong>{kare(toplam.siradaHucre)}</strong>
                <small>decided by budget and order</small>
              </div>
            </article>
            <article>
              <Icon name="recovery" />
              <div>
                <span>High-uncertainty regions</span>
                <strong>{String(toplam.dusukGuven).padStart(2, "0")}</strong>
                <small>field verification required</small>
              </div>
            </article>
          </section>
        )}

        {hata && <div className="rc-section"><ZoneError mesaj={hata} /></div>}
        {yukleniyor && <div className="rc-section"><ZoneLoading mesaj="Loading verdict distribution for 53 fires…" /></div>}

        {!yukleniyor && !hata && secili && (
          <div className="rc-workspace-grid">
            <section className="rc-panel rc-zone-list" aria-label="Intervention queue">
              <div className="rc-section-head">
                <div>
                  <span className="rc-kicker">By intervention load</span>
                  <h2>Area queue <span>Alan kuyruğu</span></h2>
                </div>
              </div>
              <div className="rc-zone-stack">
                {sirali.slice(0, 12).map((zone) => (
                  <ZoneRow
                    key={zone.fireId}
                    zone={zone}
                    secili={zone.fireId === secili.fireId}
                    onSelect={() => setSeciliId(zone.fireId)}
                  />
                ))}
              </div>
              <p className="rc-section-intro">
                Showing the 12 heaviest of {zones.length} fires.
                {eksikOzet > 0 && ` ${eksikOzet} summaries could not be loaded.`}
              </p>
            </section>

            <ZoneBrief zone={secili}>
              {bolgeEtkinlikleri.length > 0 && (
                <>
                  <DemoNotice>Activities in this area are demo. Measurements are real.</DemoNotice>
                  <div className="rc-zone-activities">
                    {bolgeEtkinlikleri.map((item) => (
                      <button key={item.id} type="button" onClick={() => setAction(`Manage ${item.type}`)}>
                        <strong>{item.type}</strong>
                        <small>{item.date} · {item.joined}/{item.capacity} volunteers</small>
                      </button>
                    ))}
                  </div>
                </>
              )}
            </ZoneBrief>
          </div>
        )}

        <section className="rc-section">
          <div className="rc-section-head">
            <div>
              <span className="rc-kicker">Supporting field evidence</span>
              <h2>Review queue <span>İnceleme kuyruğu</span></h2>
            </div>
            <span className="rc-demo-pill">{bekleyenKanit} pending · stored in this browser only</span>
          </div>
          <ObservationReview zones={zones} />
        </section>
      </main>

      {action && (
        <PrototypeModal
          title={action}
          message="Activity management has no backend connection yet, so this action does nothing. Area measurements are real, and volunteer observations are stored in this browser."
          onClose={() => setAction(null)}
        />
      )}
    </PrototypeShell>
  );
}
