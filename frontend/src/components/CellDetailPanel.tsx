import type { Cell, CellsResponse, CellVerdict, HukumSozlugu, NormRange } from "../types";
import {
  formatDecimal,
  formatDegrees,
  formatKilometers,
  formatMeters,
  landCoverLabels,
  predictionStatusLabels,
  priorityClassLabels,
  severityClassLabels,
} from "../utils/presentation";
import type { PriorityClassTransition } from "../utils/comparePriorityScenarios";

export function CellDetailPanel({ cell, cellsResponse, isAutoSelected, priorityTransition, verdict, hukumSozlugu, verdictLoading, onClose }: { cell: Cell | null; cellsResponse: CellsResponse | null; isAutoSelected: boolean; priorityTransition: PriorityClassTransition | null; verdict: CellVerdict | null; hukumSozlugu: HukumSozlugu | null; verdictLoading: boolean; onClose: () => void }) {
  const predicted = cell?.prediction_status === "predicted";
  const priorityClass = cell?.priority_class ? priorityClassLabels[cell.priority_class] : "Not calculated";
  const predictionUnavailableReason = cell ? unavailablePredictionReason(cell.prediction_status) : "Not calculated";
  const contributions = cell && predicted && cellsResponse ? priorityContributions(cell, cellsResponse) : null;
  const verdictDictionary = verdict && hukumSozlugu?.surum === verdict.hukum_version ? hukumSozlugu : null;
  // Doğru sözlük sürümü yüklenemediğinde güvenli tarafta kal: örnek tür önerisini uyarısız gösterme.
  const speciesUnapproved = verdictDictionary?.tur_tablosu.onaylandi !== true;

  return <aside className={`detail-panel ${cell ? "detail-panel--open" : ""}`} aria-hidden={!cell}>
    {cell && <>
      <div className="detail-panel__head"><div><p className="eyebrow">Cell Analysis</p><h2>{cell.cell_id}</h2></div><button className="icon-button" onClick={onClose} aria-label="Close cell details">×</button></div>
      <div className="detail-panel__body">
        {predicted && <div className="priority-banner"><strong>{priorityClass}<br />PRIORITY</strong><span>{formatDecimal(cell.priority_score, 2)}</span></div>}
        {predicted && isAutoSelected && <p className="auto-select-badge">Highest priority in this fire</p>}
        {verdict && <div className="verdict-card"><strong>{hukumTitle(verdict.hukum, verdictDictionary)}</strong><p>{verdict.ozet}</p></div>}
        {verdictLoading && <div className="status-note"><strong>Decision loading</strong><br />The model-linked field recommendation is being loaded.</div>}
        {cell.prediction_status === "low_severity" && <div className="status-note"><strong>Not Prioritized</strong><br />This cell did not satisfy the combined eligibility rule: dNBR ≥ 0.27 and NDVI drop ≥ 0.20. No model-based priority was calculated.</div>}
        {cell.prediction_status === "no_data" && <div className="status-note"><strong>Insufficient Data</strong><br />Insufficient data for model prediction and prioritization.</div>}
        {priorityTransition && <div className={`priority-change priority-change--${priorityTransition.direction}`}><strong>Priority changed</strong><span>{priorityClassLabels[priorityTransition.previous]} → {priorityClassLabels[priorityTransition.current]}</span></div>}

        {contributions && <ContributionSection contributions={contributions} score={cell.priority_score} />}
        {verdict && <section className="verdict-detail">
          <h3>Decision Detail</h3>
          <p>{verdict.ayrinti}</p>
          {verdict.tur_onerisi && <p className="verdict-species"><strong>Suggested species:</strong> {verdict.tur_onerisi}{speciesUnapproved && <span className="verdict-warning">Example data — not an approved guide</span>}</p>}
          <p className="verdict-trigger">{verdict.tetikleyen}</p>
        </section>}
        <details className="technical-disclosure">
          <summary>Technical measurements <span>Teknik ölçümler</span></summary>
          <MetricSection title="Recovery" rows={[
            ["Prediction Status", predictionStatusLabels[cell.prediction_status]],
            ["Estimated 2-Year Recovery Gap", predicted ? formatDecimal(cell.recovery_gap_pred, 2) : predictionUnavailableReason],
            ["Interpretation", predicted ? "Higher means poorer expected recovery" : predictionUnavailableReason],
          ]} />
          <MetricSection title="Fire Impact" rows={[
            ["Burn Severity", severityClassLabels[cell.severity_class]],
            ["dNBR", formatDecimal(cell.burn_severity_dnbr, 2)],
          ]} />
          <MetricSection title="Terrain & Access" rows={[
            ["Slope", formatDegrees(cell.slope_deg)],
            ["Elevation", formatMeters(cell.elevation_m)],
            ["Road Distance", formatKilometers(cell.road_distance_km)],
            ["Land Cover", cell.land_cover ? landCoverLabels[cell.land_cover] : "Missing in source data"],
          ]} />
          <MetricSection title="Vegetation" rows={[
            ["Pre-fire NDVI", formatDecimal(cell.ndvi_before, 2), "Sentinel-2 pre-fire median NDVI composite, 70 to 3 days before the fire date."],
            ["Post-fire NDVI", formatDecimal(cell.ndvi_after, 2), "Sentinel-2 post-fire median NDVI composite, 20 to 95 days after the fire date."],
            ["NDVI Drop", formatDecimal(cell.ndvi_drop, 2), "Pre-fire NDVI minus post-fire NDVI; one of the seven ridge_v2 model inputs."],
            ["Tree Cover", formatPercent(cell.tree_cover)],
            ["Annual Tree Cover", cell.tree_cover_annual === null ? "Missing in source data" : formatPercent(cell.tree_cover_annual)],
          ]} />
          <MetricSection title="Technical Details" rows={[
            ["Cell ID", cell.cell_id],
            ["Latitude", formatDecimal(cell.lat, 5)],
            ["Longitude", formatDecimal(cell.lon, 5)],
            ["Model Version", cellsResponse?.model_version ?? "Unknown"],
          ]} />
        </details>
        {verdict?.zamanlama_notu_var && verdictDictionary?.zamanlama_notu && <p className="verdict-footnote">{verdictDictionary.zamanlama_notu}</p>}
      </div>
    </>}
  </aside>;
}

function ContributionSection({ contributions, score }: { contributions: NonNullable<ReturnType<typeof priorityContributions>>; score: number | null }) {
  const rows = [
    ["Recovery need", "Toparlanma ihtiyacı", contributions.recovery],
    ["Erosion risk", "Erozyon riski", contributions.erosion],
    ["Field access", "Saha erişimi", contributions.access],
  ] as const;
  return <section className="contribution-section">
    <h3>Why this priority? <span>Neden bu öncelik?</span></h3>
    {rows.map(([label, help, value]) => {
      const share = contributionShare(value, score);
      return <div className="contribution-row" key={label}>
        <div className="contribution-row__label"><span>{label}<small>{help}</small></span><strong>{share}%</strong></div>
        <div className="contribution-bar" aria-label={`${label}: ${share}%`}><span style={{ width: `${share}%` }} /></div>
      </div>;
    })}
  </section>;
}

function hukumTitle(kod: CellVerdict["hukum"], hukumSozlugu: HukumSozlugu | null): string {
  return hukumSozlugu?.hukumler.find((entry) => entry.kod === kod)?.baslik ?? kod;
}

function unavailablePredictionReason(status: Cell["prediction_status"]): string {
  return status === "low_severity"
    ? "Not calculated — below eligibility threshold"
    : "Not calculated — insufficient input data";
}

function MetricSection({ title, rows }: { title: string; rows: Array<[string, string, string?]> }) {
  return <section className="metric-section"><h3>{title}</h3><dl className="metric-grid">{rows.map(([label, data, help]) => <div className="metric-row" key={label} title={help}><dt>{label}{help ? " ⓘ" : ""}</dt><dd>{data}</dd></div>)}</dl></section>;
}

function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`;
}

function normalize(value: number, range: NormRange): number {
  if (range.max - range.min < 1e-9) return 0.5;
  return Math.min(1, Math.max(0, (value - range.min) / (range.max - range.min)));
}

function priorityContributions(cell: Cell, response: CellsResponse) {
  const recoveryGap = cell.recovery_gap_pred;
  if (recoveryGap === null) return null;
  const ref = response.normalization_reference;
  const weights = response.applied_weights;
  return {
    recovery: weights.recovery * normalize(recoveryGap, ref.recovery_gap_pred),
    erosion: weights.erosion * normalize(cell.slope_deg, ref.slope_deg),
    access: weights.access * (1 - normalize(cell.road_distance_km, ref.road_distance_km)),
  };
}

function contributionShare(value: number, score: number | null): number {
  return score && score > 0 ? Math.max(0, Math.min(100, Math.round((value / score) * 100))) : 0;
}
