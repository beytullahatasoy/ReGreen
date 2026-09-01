import type { RecoveryZone } from "../../../hooks/useRecoveryZones";
import { recoveryUpdate } from "../data/demoData";

/**
 * Twelve-month monitoring card. The assessment result is DEMO — there is no
 * real reassessment flow yet. The area it points at is real: instead of the
 * invented "Izmir - Zone 03" it now shows an actual fire.
 */
export function RecoveryUpdateCard({ zones }: { zones: RecoveryZone[] }) {
  const zone = zones.find((z) => z.fireId === recoveryUpdate.fireId) ?? zones[0];
  if (!zone) return null;

  return (
    <article className="rc-update-card">
      <div className="rc-update-card__head">
        <div>
          <span className="rc-kicker">12-month tracking</span>
          <h3>{zone.il} · {zone.fireId}</h3>
        </div>
        <span className="rc-demo-pill">Assessment result is demo</span>
      </div>

      <div className="rc-update-grid">
        <div>
          <small>Assessment</small>
          <strong>{recoveryUpdate.title}</strong>
          <span className="rc-status rc-status--complete">{recoveryUpdate.status}</span>
        </div>
        <div>
          <small>Recovery trend</small>
          <strong>{recoveryUpdate.trend}</strong>
          <span className="rc-trend">↗ Positive direction</span>
        </div>
      </div>

      <div className="rc-comparison" aria-label="Satellite comparison (illustrative)">
        <div><span>Post-fire baseline</span><i /></div>
        <b>→</b>
        <div className="is-later"><span>Current observation</span><i /></div>
      </div>

      <p className="rc-note">
        Recovery is assessed against satellite data from the same seasonal window
        so season alone cannot explain the change. The panel above is
        illustrative, not a measured comparison.
      </p>
    </article>
  );
}
