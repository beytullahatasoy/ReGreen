import type { RecoveryZone } from "../../../hooks/useRecoveryZones";
import { observationService, type FieldObservation } from "../data/observationService";
import { useObservations } from "../data/useObservations";

/**
 * Review queue for volunteer observations — the organisation's second job.
 *
 * Two kinds of record appear here and they behave differently on purpose:
 *
 *   local  submitted through the Community screen in THIS browser. It is
 *          actionable: accepting it or asking for clarification really changes
 *          its status, and the volunteer sees that change on their own screen.
 *   demo   a seeded sample. Read-only, because pretending to decide on a
 *          record nobody submitted would be theatre.
 *
 * The area each observation belongs to is real: the fire matching `fireId` is
 * resolved from the verdict layer, so no invented zone name appears.
 *
 * The heading and badge come from the parent section.
 */
export function ObservationReview({ zones }: { zones: RecoveryZone[] }) {
  const observations = useObservations();

  return (
    <>
      <p className="rc-section-intro">
        Volunteer observations <strong>do not train the model.</strong> They are
        reviewed as supporting field evidence for the expert's decision and never
        enter the dataset unaccepted.
      </p>

      {observations.length === 0 ? (
        <div className="rc-empty">No observation is waiting for review.</div>
      ) : (
        <div className="rc-observation-list">
          {observations.map((item) => (
            <ObservationCard
              key={item.id}
              item={item}
              zone={zones.find((z) => z.fireId === item.fireId)}
            />
          ))}
        </div>
      )}
    </>
  );
}

function ObservationCard({
  item, zone,
}: { item: FieldObservation; zone: RecoveryZone | undefined }) {
  const karar = item.status !== "Pending";
  const duzenlenebilir = item.origin === "local";

  return (
    <article className={item.origin === "local" ? "is-live" : undefined}>
      <div>
        <span className={`rc-status${karar ? " rc-status--complete" : ""}`}>{item.status}</span>
        <h3>{zone ? `${zone.il} · ${zone.fireId}` : item.fireId}</h3>
        <p>{item.submittedBy} · {item.displayDate}</p>
        {item.origin === "local" && (
          <small className="rc-origin">Submitted in this browser</small>
        )}
      </div>

      <div>
        <strong>Location</strong>
        <p>{item.location}</p>
        {item.photoName && <small>Photo: {item.photoName}</small>}
      </div>

      <div>
        <strong>Answers</strong>
        <p>{item.answers.join(" · ")}</p>
        {item.note && <small>Note: {item.note}</small>}
      </div>

      {duzenlenebilir ? (
        <div className="rc-review-actions">
          <button
            type="button"
            className="rc-button rc-button--primary"
            disabled={item.status === "Accepted as supporting evidence"}
            onClick={() => observationService.review(item.id, "Accepted as supporting evidence")}
          >
            Accept as evidence
          </button>
          <button
            type="button"
            className="rc-button"
            disabled={item.status === "Needs clarification"}
            onClick={() => observationService.review(item.id, "Needs clarification")}
          >
            Ask for clarification
          </button>
        </div>
      ) : (
        <p className="rc-review-actions rc-review-actions--readonly">
          Sample record · read-only
        </p>
      )}
    </article>
  );
}
