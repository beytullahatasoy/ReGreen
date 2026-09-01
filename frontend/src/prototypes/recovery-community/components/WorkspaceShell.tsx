import type { ReactNode } from "react";

/**
 * Organisation ve Community ekranlarının ortak kabuğu.
 *
 * Eskiden PrototypeShell'di ve başlıkta "Demo workspace", altta "Prototype
 * data is clearly labelled throughout this workspace" yazıyordu. İkisi de
 * artık doğru değil: bu ekranlardaki alanlar, etkinlikler, katılımlar ve
 * gözlemlerin hepsi gerçek veritabanı kayıtları.
 */
export function WorkspaceShell({
  mode, children,
}: { mode: "organisation" | "volunteer"; children: ReactNode }) {
  return (
    <div className="rc-shell">
      <header className="rc-header">
        <a className="rc-brand" href="/" aria-label="ReGreen Expert Workspace">
          <span className="rc-brand__logo"><img src="/brand/regreen-logo-source.png" alt="" /></span>
          <span><strong>ReGreen</strong><small>Recovery intelligence</small></span>
        </a>
        <nav aria-label="Workspace navigation">
          <a href="/">Expert</a>
          <a className={mode === "organisation" ? "is-active" : ""} href="/organisation">Organisation</a>
          <a className={mode === "volunteer" ? "is-active" : ""} href="/community">Community</a>
        </nav>
      </header>

      {children}

      <footer className="rc-footer">
        <span>ReGreen · From priority to long-term recovery</span>
        <span>
          {mode === "volunteer"
            ? "Field days are posted by verified forestry organisations."
            : "Areas and verdicts come from the satellite model; activities and evidence from the field."}
        </span>
      </footer>
    </div>
  );
}
