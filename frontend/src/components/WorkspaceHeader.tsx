import type { FireSummary } from "../types";

export function WorkspaceHeader({ fire }: { fire: FireSummary | null }) {
  return <header className="header">
    <div className="header__brand"><strong>ReGreen</strong><span className="header__slash">/</span><span>Expert Recovery Workspace</span></div>
    <nav className="header__nav" aria-label="Workspace navigation">
      <a className="is-active" href="/">Expert</a>
      <a href="/organisation">Organisation</a>
      <a href="/community">Community</a>
    </nav>
    <div className="header__context">
      <span>{fire ? `${fire.province} · ${fire.fire_id}` : "No fire selected"}</span>
      <span className={`quality-dot ${fire?.quality_flag === "check" ? "quality-dot--check" : ""}`} />
      <span>{fire?.quality_flag ?? "—"}</span>
    </div>
  </header>;
}
