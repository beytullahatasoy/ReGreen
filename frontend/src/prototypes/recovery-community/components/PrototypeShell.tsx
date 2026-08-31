import type { ReactNode } from "react";

export function PrototypeShell({ mode, children }: { mode: "organisation" | "volunteer"; children: ReactNode }) {
  return <div className="rc-shell">
    <header className="rc-header"><a className="rc-brand" href="/" aria-label="ReGreen Expert Workspace"><span className="rc-brand__mark">R</span><span><strong>ReGreen</strong><small>Recovery intelligence</small></span></a>
      <nav aria-label="Workspace navigation"><a className={mode === "organisation" ? "is-active" : ""} href="/prototype/organisation">Organisation</a><a className={mode === "volunteer" ? "is-active" : ""} href="/prototype/volunteer">Volunteer</a><a href="/">Expert ↗</a></nav>
      <div className="rc-header__meta"><span className="rc-live-dot" /> Frontend prototype</div>
    </header>{children}
    <footer className="rc-footer"><span>ReGreen · From priority to long-term recovery</span><span>Prototype data is clearly labelled throughout this workspace.</span></footer>
  </div>;
}
