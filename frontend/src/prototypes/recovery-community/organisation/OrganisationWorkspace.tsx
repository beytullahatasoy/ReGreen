import { useState } from "react";
import { activities, observations, zones } from "../data/demoData";
import type { RecoveryZoneDemo } from "../types";
import { Icon } from "../components/Icons";
import { ObservationReview } from "../components/ObservationReview";
import { PrototypeModal } from "../components/PrototypeModal";
import { PrototypeShell } from "../components/PrototypeShell";
import { RecoveryUpdateCard } from "../components/RecoveryUpdateCard";
import { SummaryStrip } from "../components/SummaryStrip";
import { ZoneDetailModal } from "../components/ZoneDetailModal";

export function OrganisationWorkspace() {
  const [selected, setSelected] = useState<RecoveryZoneDemo | null>(null);
  const [action, setAction] = useState<string | null>(null);
  const summary = [
    { label: "Active Recovery Zones", value: "03", note: "Across 2 regions", icon: "zone" as const },
    { label: "Activities in progress", value: "03", note: "Demo workflow", icon: "activity" as const },
    { label: "Registered volunteers", value: "62", note: "Across open activities", icon: "people" as const },
    { label: "Awaiting review", value: String(observations.filter(o=>o.status==="Pending").length).padStart(2,"0"), note: "Supporting evidence", icon: "observe" as const },
    { label: "Next milestone", value: "18d", note: "Monitoring review", icon: "recovery" as const },
  ];
  return <PrototypeShell mode="organisation"><main className="rc-main">
    <section className="rc-hero"><div><span className="rc-kicker rc-kicker--orange">Organisation recovery operations</span><h1>From prioritised recovery need<br/>to verified field action.</h1><p>Coordinate expert-reviewed Recovery Zones, approved activities, supporting field evidence and long-term monitoring.</p></div><div className="rc-hero__stat"><span>Action layer</span><strong>03</strong><small>demo Recovery Zones</small></div></section>
    <SummaryStrip items={summary}/>
    <section className="rc-section"><div className="rc-section-head"><div><span className="rc-kicker">Operational portfolio</span><h2>Recovery Zones</h2></div><span className="rc-demo-pill">Demo data</span></div><div className="rc-zone-operations">{zones.map(zone=>{const zoneActivities=activities.filter(a=>a.zoneId===zone.id);return <article key={zone.id}><div className="rc-zone-operation__head"><div><span className="rc-priority">{zone.priority}</span><h3>{zone.name}</h3><p>{zone.province} · {zone.region}</p></div><span className="rc-status">{zone.status}</span></div><p className="rc-zone-operation__why">{zone.why}</p><div className="rc-progress"><span><small>{zone.stage}</small><b>{zone.progress}%</b></span><i><b style={{width:`${zone.progress}%`}}/></i></div><dl><div><dt>Assigned organisation</dt><dd>{zone.organisation}</dd></div><div><dt>Available activities</dt><dd>{zoneActivities.length}</dd></div><div><dt>Next action</dt><dd>{zone.nextMilestone}</dd></div><div><dt>Last update</dt><dd>{zone.lastUpdate}</dd></div></dl><button onClick={()=>setSelected(zone)}>Open Recovery Zone <Icon name="arrow"/></button></article>})}</div></section>
    <section className="rc-actions" aria-label="Organisation prototype actions">{([["Create Activity","activity"],["Review Observations","observe"],["Update Zone Status","zone"],["View Recovery Report","report"]] as const).map(([label,icon])=><button key={label} onClick={()=>setAction(label)}><Icon name={icon}/><span><strong>{label}</strong><small>Prototype action · no data mutation</small></span><Icon name="arrow"/></button>)}</section>
    <section className="rc-section"><div className="rc-section-head"><div><span className="rc-kicker">Approved coordination</span><h2>Activity management</h2></div><span className="rc-demo-pill">Demo workflow</span></div><div className="rc-activity-table">{activities.map(item=>{const zone=zones.find(z=>z.id===item.zoneId)!;return <article key={item.id}><div><span className="rc-status">{item.status}</span><h3>{item.type}</h3><p>{zone.name} · {item.date}</p></div><div><small>Coordinator</small><strong>{item.coordinator}</strong><span>{item.organisation}</span></div><div><small>Volunteer capacity</small><strong>{item.joined} / {item.capacity}</strong><span>{item.capacity-item.joined} places available</span></div><button onClick={()=>setAction(`Manage ${item.type}`)}>Manage →</button></article>})}</div></section>
    <ObservationReview onAction={setAction}/>
    <section className="rc-section"><div className="rc-section-head"><div><span className="rc-kicker">Monitoring outcome</span><h2>Recovery update</h2></div></div><RecoveryUpdateCard/></section>
  </main>{selected&&<ZoneDetailModal zone={selected} onClose={()=>setSelected(null)}/>} {action&&<PrototypeModal title={action} message="Prototype — backend integration is not connected. This action does not change, persist or submit any data." onClose={()=>setAction(null)}/>}</PrototypeShell>;
}
