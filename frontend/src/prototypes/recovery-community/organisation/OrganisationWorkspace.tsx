import { useEffect, useMemo, useRef, useState } from "react";
import { aciliyeteGore, toplamlar, useRecoveryZones } from "../../../hooks/useRecoveryZones";
import { communityService } from "../../../services";
import type { FieldActivity } from "../../../types/community";
import { useActivities } from "../data/useActivities";
import { useObservations } from "../data/useObservations";
import { Icon } from "../components/Icons";
import { ObservationReview } from "../components/ObservationReview";
import { WorkspaceShell } from "../components/WorkspaceShell";
import { NewActivityForm } from "./NewActivityForm";
import {
  ACTIVITY_KIND_LABEL, ACTIVITY_STATUS_LABEL, ZoneBrief, ZoneError, ZoneLoading, ZoneRow, hektar, kare,
} from "../components/RealZone";

/**
 * The organisation screen has one job:
 *
 *     "Where do I send a crew today, and what needs my approval?"
 *
 * Zones come from the 53 real fires and the verdict layer. Activities and
 * observations are now real too (backend/ReGreen.Api/Endpoints/CommunityEndpoints.cs):
 * whatever appears here was actually opened by an organisation and actually
 * submitted by a volunteer. The only demo record left on this screen is the
 * 12-month recovery card.
 */
export function OrganisationWorkspace() {
  const { zones, yukleniyor, hata, eksikOzet } = useRecoveryZones();
  const [seciliId, setSeciliId] = useState<string | null>(null);
  const [yonetilen, setYonetilen] = useState<FieldActivity | null>(null);

  // Heaviest intervention load first: the organisation user sees a QUEUE, not
  // a catalogue. The top of the list is where today's attention belongs.
  const sirali = useMemo(() => aciliyeteGore(zones), [zones]);
  const secili = sirali.find((z) => z.fireId === seciliId) ?? sirali[0] ?? null;
  const toplam = useMemo(() => toplamlar(zones), [zones]);

  const observationsState = useObservations({ limit: 100 });
  const bekleyenKanit = observationsState.observations.filter((o) => o.status === "pending").length;

  // Activities are keyed to real fire ids, so only the selected area's
  // activities are shown — not an arbitrary first two.
  const activitiesState = useActivities({ fire_id: secili?.fireId });

  return (
    <WorkspaceShell mode="organisation">
      <main className="rc-main">
        <section className="rc-hero rc-hero--compact">
          <div>
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
              {activitiesState.hata && <p className="rc-form-warning" role="alert">{activitiesState.hata}</p>}
              {!activitiesState.hata && activitiesState.yukleniyor && (
                <p className="rc-section-intro">Loading activities…</p>
              )}
              {!activitiesState.hata && !activitiesState.yukleniyor && activitiesState.activities.length === 0 && (
                <p className="rc-section-intro">
                  No field day is open here yet — volunteers see nothing for this area.
                </p>
              )}
              {!activitiesState.hata && !activitiesState.yukleniyor && activitiesState.activities.length > 0 && (
                <div className="rc-zone-activities">
                  {activitiesState.activities.map((item) => (
                    <button key={item.id} type="button" onClick={() => setYonetilen(item)}>
                      <strong>{ACTIVITY_KIND_LABEL[item.kind]}</strong>
                      <small>{item.scheduled_for} · {item.joined}/{item.capacity} volunteers · {ACTIVITY_STATUS_LABEL[item.status]}</small>
                    </button>
                  ))}
                </div>
              )}
              <NewActivityForm
                fireId={secili.fireId}
                il={secili.il}
                onCreated={() => activitiesState.yenile()}
              />
            </ZoneBrief>
          </div>
        )}

        <section className="rc-section">
          <div className="rc-section-head">
            <div>
              <h2>Review queue <span>İnceleme kuyruğu</span></h2>
            </div>
            <span className="rc-demo-pill">{bekleyenKanit} pending</span>
          </div>
          <ObservationReview
            zones={zones}
            observations={observationsState.observations}
            yukleniyor={observationsState.yukleniyor}
            hata={observationsState.hata}
            onReviewed={observationsState.yenile}
          />
        </section>
      </main>

      {yonetilen && (
        <ActivityManageModal
          activity={yonetilen}
          onClose={() => setYonetilen(null)}
          onUpdated={(guncel) => { setYonetilen(guncel); activitiesState.yenile(); }}
        />
      )}
    </WorkspaceShell>
  );
}

/** Real status toggle — backed by PATCH /api/activities/{id}. */
function ActivityManageModal({
  activity, onClose, onUpdated,
}: { activity: FieldActivity; onClose: () => void; onUpdated: (activity: FieldActivity) => void }) {
  const closeRef = useRef<HTMLButtonElement>(null);
  const [gonderiliyor, setGonderiliyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();
    const onKey = (event: KeyboardEvent) => event.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("keydown", onKey); previous?.focus(); };
  }, [onClose]);

  const acik = activity.status === "open";

  async function durumDegistir() {
    setGonderiliyor(true);
    setHata(null);
    try {
      const guncel = await communityService.updateActivity(activity.id, { status: acik ? "closed" : "open" });
      onUpdated(guncel);
    } catch (reason: unknown) {
      setHata(reason instanceof Error ? reason.message : "Could not update the activity.");
    } finally {
      setGonderiliyor(false);
    }
  }

  return (
    <div className="rc-modal-backdrop" role="presentation" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <section className="rc-modal" role="dialog" aria-modal="true" aria-labelledby="manage-activity-title">
        <button ref={closeRef} className="rc-modal__close" onClick={onClose} aria-label="Close dialog">×</button>
        <p className="rc-modal__kind">{ACTIVITY_KIND_LABEL[activity.kind]}</p>
        <h2 id="manage-activity-title">{activity.title}</h2>
        <p>{activity.description}</p>
        <dl>
          <div><dt>Date</dt><dd>{activity.scheduled_for}</dd></div>
          <div><dt>Meeting point</dt><dd>{activity.meeting_point}</dd></div>
          <div><dt>Capacity</dt><dd>{activity.joined} / {activity.capacity}</dd></div>
          <div><dt>Status</dt><dd>{ACTIVITY_STATUS_LABEL[activity.status]}</dd></div>
          <div><dt>Observations submitted</dt><dd>{activity.observation_count}</dd></div>
        </dl>
        {hata && <p className="rc-form-warning" role="alert">{hata}</p>}
        <button
          className="rc-button rc-button--primary"
          disabled={gonderiliyor || activity.status === "completed"}
          onClick={durumDegistir}
        >
          {acik ? "Close activity" : "Reopen activity"}
        </button>
      </section>
    </div>
  );
}
