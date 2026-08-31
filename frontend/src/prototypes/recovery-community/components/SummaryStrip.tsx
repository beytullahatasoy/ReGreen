import { Icon } from "./Icons";

export interface SummaryItem { label: string; value: string; note: string; icon: "zone" | "activity" | "people" | "observe" | "recovery" | "follow"; }

export function SummaryStrip({ items }: { items: SummaryItem[] }) {
  return <section className="rc-summary" aria-label="Workspace summary">{items.map((item) => <article key={item.label}><Icon name={item.icon}/><div><span>{item.label}</span><strong>{item.value}</strong><small>{item.note}</small></div></article>)}</section>;
}
