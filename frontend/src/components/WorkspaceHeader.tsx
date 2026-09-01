import type { FireSummary } from "../types";

export function WorkspaceHeader({ fire }: { fire: FireSummary | null }) {
  return <header className="header">
    <a className="header__brand" href="/" aria-label="ReGreen Expert Workspace">
      <span className="header__brand-logo"><img src="/brand/regreen-logo-source.png" alt="" /></span>
      <span><strong>ReGreen</strong><small>Expert recovery workspace</small></span>
    </a>
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
