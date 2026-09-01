import { useState } from "react";
import type { RecoveryZone } from "../../../hooks/useRecoveryZones";
import { communityService } from "../../../services";
import type { FieldObservation, ObservationStatus } from "../../../types/community";
import { tarih } from "./RealZone";

const STATUS_LABEL: Record<ObservationStatus, string> = {
  pending: "Pending",
  accepted: "Accepted as supporting evidence",
  needs_clarification: "Needs clarification",
  rejected: "Rejected",
};

interface Props {
  zones: RecoveryZone[];
  observations: FieldObservation[];
  yukleniyor: boolean;
  hata: string | null;
  onReviewed: () => void;
}

/**
 * Review queue for volunteer observations — the organisation's second job.
 *
 * Every record here is real (GET /api/observations): a volunteer submitted
 * it through the Community screen, it is reviewed here, and the decision is
 * visible back on the volunteer's screen the next time they load it.
 *
 * The area each observation belongs to is real: the fire matching `fire_id`
 * is resolved from the verdict layer, so no invented zone name appears.
 *
 * The heading and badge come from the parent section.
 */
export function ObservationReview({ zones, observations, yukleniyor, hata, onReviewed }: Props) {
  return (
    <>
      <p className="rc-section-intro">
        Volunteer observations <strong>do not train the model.</strong> They are
        reviewed as supporting field evidence for the expert's decision and never
        enter the dataset unaccepted.
      </p>

      {hata && <div className="rc-empty rc-empty--error" role="alert">{hata}</div>}
      {!hata && yukleniyor && <div className="rc-empty" role="status">Loading review queue…</div>}

      {!hata && !yukleniyor && (
        observations.length === 0 ? (
          <div className="rc-empty">No observation is waiting for review.</div>
        ) : (
          <div className="rc-observation-list">
            {observations.map((item) => (
              <ObservationCard
                key={item.id}
                item={item}
                zone={zones.find((z) => z.fireId === item.fire_id)}
                onReviewed={onReviewed}
              />
            ))}
          </div>
        )
      )}
    </>
  );
}

function ObservationCard({
  item, zone, onReviewed,
}: { item: FieldObservation; zone: RecoveryZone | undefined; onReviewed: () => void }) {
  const [gonderiliyor, setGonderiliyor] = useState<ObservationStatus | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const karar = item.status !== "pending";

  async function kararVer(status: ObservationStatus) {
    setGonderiliyor(status);
    setHata(null);
    try {
      await communityService.reviewObservation(item.id, { status });
      onReviewed();
    } catch (reason: unknown) {
      setHata(reason instanceof Error ? reason.message : "Karar kaydedilemedi.");
    } finally {
      setGonderiliyor(null);
    }
  }

  return (
    <article className="is-live">
      <div>
        <span className={`rc-status${karar ? " rc-status--complete" : ""}`}>{STATUS_LABEL[item.status]}</span>
        <h3>{zone ? `${zone.il} · ${zone.fireId}` : item.fire_id}</h3>
        <p>{item.volunteer_alias} · {tarih(item.submitted_at)}</p>
        {item.activity_title && <small className="rc-origin">{item.activity_title}</small>}
      </div>

      <div>
        <strong>Location</strong>
        <p>{item.location}</p>
        {item.photo_name && <small>Photo: {item.photo_name}</small>}
      </div>

      <div>
        <strong>Answers</strong>
        <p>{item.answers.join(" · ")}</p>
        {item.note && <small>Note: {item.note}</small>}
      </div>

      <div className="rc-review-actions">
        <button
          type="button"
          className="rc-button rc-button--primary"
          disabled={item.status === "accepted" || gonderiliyor !== null}
          onClick={() => kararVer("accepted")}
        >
          Accept as evidence
        </button>
        <button
          type="button"
          className="rc-button"
          disabled={item.status === "needs_clarification" || gonderiliyor !== null}
          onClick={() => kararVer("needs_clarification")}
        >
          Ask for clarification
        </button>
      </div>
      {hata && <p className="rc-form-warning" role="alert">{hata}</p>}
    </article>
  );
}
