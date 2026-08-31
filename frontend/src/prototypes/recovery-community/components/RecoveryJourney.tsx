import { useId } from "react";
import type { RecoveryStage } from "../types";

const stages: Array<{ title: RecoveryStage; detail: string }> = [
  { title: "Prioritised", detail: "Recovery need identified for expert attention" },
  { title: "Expert Reviewed", detail: "Evidence reviewed by an expert or verified organisation" },
  { title: "Recovery Zone", detail: "A trackable operational zone is established" },
  { title: "Field Action", detail: "Approved activities and participation are coordinated" },
  { title: "Monitoring", detail: "Comparable observations support progress review" },
  { title: "Recovery Update", detail: "A reviewed monitoring result is published" },
];

export function RecoveryJourney({ currentStage = "Expert Reviewed", compact = false }: { currentStage?: RecoveryStage; compact?: boolean }) {
  const titleId = useId();
  const currentIndex = stages.findIndex((stage) => stage.title === currentStage);
  return <section className={`rc-journey${compact ? " rc-journey--compact" : ""}`} aria-labelledby={titleId}>
    <div className="rc-section-head"><div><span className="rc-kicker">What is happening now?</span><h2 id={titleId}>Recovery journey</h2></div><span className="rc-demo-pill">Prototype workflow</span></div>
    <ol>{stages.map((stage, index) => <li key={stage.title} className={`${index < currentIndex ? "is-reached" : ""}${index === currentIndex ? " is-current" : ""}`} aria-current={index === currentIndex ? "step" : undefined}><span className="rc-journey__dot">{index < currentIndex ? "✓" : String(index + 1).padStart(2, "0")}</span><div><strong>{stage.title}</strong><p>{stage.detail}</p>{index === currentIndex && <em>Current stage</em>}</div></li>)}</ol>
  </section>;
}
