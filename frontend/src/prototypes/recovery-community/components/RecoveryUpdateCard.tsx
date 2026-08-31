import { recoveryUpdate, zones } from "../data/demoData";

export function RecoveryUpdateCard() {
  const zone = zones.find((item) => item.id === recoveryUpdate.zoneId)!;
  return <article className="rc-update-card">
    <div className="rc-update-card__head"><div><span className="rc-kicker">12-month tracking</span><h3>{zone.name}</h3></div><span className="rc-demo-pill">Prototype monitoring example</span></div>
    <div className="rc-update-grid"><div><small>Assessment</small><strong>{recoveryUpdate.title}</strong><span className="rc-status rc-status--complete">{recoveryUpdate.status}</span></div><div><small>Recovery trend</small><strong>{recoveryUpdate.trend}</strong><span className="rc-trend">↗ Positive direction</span></div></div>
    <div className="rc-comparison" aria-label="Prototype satellite comparison"><div><span>Post-fire baseline</span><i /></div><b>→</b><div className="is-later"><span>Current observation</span><i /></div></div>
    <p className="rc-note">Comparable seasonal satellite observations can be used to evaluate how recovery changes over time. This prototype example does not claim scientific certainty.</p>
  </article>;
}
