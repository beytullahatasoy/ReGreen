import type { RecoveryZone } from "../../../hooks/useRecoveryZones";
import { Icon } from "./Icons";

/**
 * Recovery Zone components backed by REAL data.
 *
 * Every figure here comes from the API (verdict layer + fire summary). The
 * activity and volunteer side is still demo and lives behind DemoNotice.
 *
 * Language follows the Expert screen: English labels, Turkish helper text
 * where it clarifies. Verdict sentences arriving from the API stay Turkish —
 * they are model output, not UI copy.
 */

const sayi = new Intl.NumberFormat("tr-TR");
const ondalik = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 0 });

export function hektar(v: number) {
  return `${ondalik.format(v)} ha`;
}

export function kare(v: number) {
  return sayi.format(v);
}

const CONFIDENCE_LABEL: Record<RecoveryZone["guven"], string> = {
  yuksek: "High confidence",
  orta: "Medium confidence",
  dusuk: "No regional reference",
};

/** One-line zone summary — used in the left-hand queue. */
export function ZoneRow({
  zone, secili, onSelect,
}: { zone: RecoveryZone; secili: boolean; onSelect: () => void }) {
  return (
    <button
      type="button"
      className={`rc-zone-row${secili ? " is-selected" : ""}`}
      onClick={onSelect}
      aria-pressed={secili}
    >
      <span className="rc-zone-row__marker" />
      <span>
        <strong>{zone.il}</strong>
        <small>{zone.fireId} · {hektar(zone.alanHa)}</small>
      </span>
      <span>
        <em>{kare(zone.mudahaleHucre)} cells need action</em>
        <small>
          {zone.erozyonHucre > 0 ? `${kare(zone.erozyonHucre)} erosion first · ` : ""}
          {CONFIDENCE_LABEL[zone.guven]}
        </small>
      </span>
      <Icon name="arrow" />
    </button>
  );
}

/** Verdict distribution as a single bar. */
export function VerdictBar({ zone }: { zone: RecoveryZone }) {
  const toplam = Math.max(zone.hucre, 1);
  const dilim = [
    { ad: "Erosion control first", n: zone.erozyonHucre, renk: "#c4451f" },
    { ad: "Planting candidate", n: zone.mudahaleHucre - zone.erozyonHucre, renk: "#e0762f" },
    { ad: "By priority order", n: zone.siradaHucre, renk: "#c58a12" },
    { ad: "Monitor", n: zone.izlemeHucre, renk: "#438360" },
    { ad: "Out of scope", n: zone.kapsamDisiHucre, renk: "#4c5a52" },
  ].filter((d) => d.n > 0);

  return (
    <div className="rc-verdict-bar">
      <i aria-hidden="true">
        {dilim.map((d) => (
          <b key={d.ad} style={{ width: `${(d.n / toplam) * 100}%`, background: d.renk }} />
        ))}
      </i>
      <ul>
        {dilim.map((d) => (
          <li key={d.ad}>
            <span style={{ background: d.renk }} />
            {d.ad}
            <b>{kare(d.n)}</b>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Selected zone brief — right-hand panel. The paragraph is model output. */
export function ZoneBrief({ zone, children }: { zone: RecoveryZone; children?: React.ReactNode }) {
  return (
    <section className="rc-zone-detail" aria-live="polite">
      <div className="rc-zone-detail__title">
        <div>
          <span className="rc-kicker">{zone.bolge}</span>
          <h2>{zone.il}</h2>
        </div>
        {zone.guven === "dusuk" && <span className="rc-priority">High uncertainty</span>}
      </div>

      <VerdictBar zone={zone} />

      <dl>
        <div><dt>Burned area</dt><dd>{hektar(zone.alanHa)} · {kare(zone.hucre)} cells</dd></div>
        <div><dt>Direct action</dt><dd>{hektar(zone.mudahaleHa)} · {kare(zone.mudahaleHucre)} cells</dd></div>
        <div><dt>Erosion control first</dt><dd>{kare(zone.erozyonHucre)} cells</dd></div>
        <div><dt>By priority order</dt><dd>{kare(zone.siradaHucre)} cells</dd></div>
        <div><dt>Reference fires in region</dt><dd>{zone.bolgedekiReferans}</dd></div>
      </dl>

      {zone.paragraf && (
        <div className="rc-assessment">
          <strong>Fire assessment <span>Yangın özeti</span></strong>
          <p>{zone.paragraf}</p>
        </div>
      )}

      {zone.ozetEksik && (
        <div className="rc-assessment">
          <strong>Summary unavailable</strong>
          <p>The verdict distribution for this fire could not be loaded; only list data is shown.</p>
        </div>
      )}

      {children}
    </section>
  );
}

/** ONE honesty badge per screen, instead of the eight scattered "demo" pills. */
export function DemoNotice({ children }: { children: React.ReactNode }) {
  return (
    <p className="rc-demo-notice">
      <span className="rc-demo-pill">Prototype</span>
      {children}
    </p>
  );
}

/** Loading and error states — required now that these screens depend on the API. */
export function ZoneLoading({ mesaj }: { mesaj: string }) {
  return <div className="rc-empty" role="status">{mesaj}</div>;
}

export function ZoneError({ mesaj }: { mesaj: string }) {
  return (
    <div className="rc-empty rc-empty--error" role="alert">
      <strong>Data could not be loaded</strong>
      <p>{mesaj}</p>
      <p>The API may not be running: <code>dotnet run --project backend/ReGreen.Api</code></p>
    </div>
  );
}
