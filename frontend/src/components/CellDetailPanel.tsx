import type { Cell, CellsResponse, NormRange } from "../types";
import {
  formatDecimal,
  formatDegrees,
  formatKilometers,
  formatMeters,
  predictionStatusLabels,
  priorityClassLabels,
} from "../utils/presentation";
import type { PriorityClassTransition } from "../utils/comparePriorityScenarios";

export function CellDetailPanel({ cell, cellsResponse, priorityTransition, onClose }: { cell: Cell | null; cellsResponse: CellsResponse | null; priorityTransition: PriorityClassTransition | null; onClose: () => void }) {
  const predicted = cell?.prediction_status === "predicted";
  const priorityClass = cell?.priority_class ? priorityClassLabels[cell.priority_class] : "Not available";
  const contributions = cell && predicted && cellsResponse ? priorityContributions(cell, cellsResponse) : null;

  return <aside className={`detail-panel ${cell ? "detail-panel--open" : ""}`} aria-hidden={!cell}>
    {cell && <>
      <div className="detail-panel__head"><div><p className="eyebrow">Cell Analysis</p><h2>{cell.cell_id}</h2></div><button className="icon-button" onClick={onClose} aria-label="Close cell details">×</button></div>
      <div className="detail-panel__body">
        {predicted && <div className="priority-banner"><strong>{priorityClass}<br />PRIORITY</strong><span>{formatDecimal(cell.priority_score, 2)}</span></div>}
        {cell.prediction_status === "low_severity" && <div className="status-note"><strong>Not Prioritized</strong><br />This cell did not satisfy the combined eligibility rule: dNBR ≥ 0.27 and NDVI drop ≥ 0.20. No model-based priority was calculated.</div>}
        {cell.prediction_status === "no_data" && <div className="status-note"><strong>Insufficient Data</strong><br />Insufficient data for model prediction and prioritization.</div>}
        {priorityTransition && <div className={`priority-change priority-change--${priorityTransition.direction}`}><strong>Priority changed</strong><span>{priorityClassLabels[priorityTransition.previous]} → {priorityClassLabels[priorityTransition.current]}</span></div>}

        <MetricSection title="Priority" rows={[
          ["Priority Class", predicted ? priorityClass : "Not available"],
          ["Priority Score", predicted ? formatDecimal(cell.priority_score, 2) : "Not available"],
        ]} />
        {contributions && <MetricSection title="Why This Priority?" rows={[
          ["Recovery component", formatContribution(contributions.recovery, cell.priority_score), "Based on the model's estimated two-year recovery gap."],
          ["Slope component", formatContribution(contributions.erosion, cell.priority_score), "Slope-based deterministic priority component."],
          ["Road-access component", formatContribution(contributions.access, cell.priority_score), "Closer road access increases the operational accessibility component."],
        ]} />}
        <MetricSection title="Recovery" rows={[
          ["Prediction Status", predictionStatusLabels[cell.prediction_status]],
          ["Estimated 2-Year Recovery Gap", predicted ? formatDecimal(cell.recovery_gap_pred, 2) : "Not available"],
          ["Interpretation", predicted ? "Higher means poorer expected recovery" : "Not available"],
        ]} />
        <MetricSection title="Fire Impact" rows={[
          ["Burn Severity", humanize(cell.severity_class)],
          ["dNBR", formatDecimal(cell.burn_severity_dnbr, 2)],
        ]} />
        <MetricSection title="Terrain & Access" rows={[
          ["Slope", formatDegrees(cell.slope_deg)],
          ["Elevation", formatMeters(cell.elevation_m)],
          ["Road Distance", formatKilometers(cell.road_distance_km)],
          ["Land Cover", cell.land_cover ?? "Not available"],
        ]} />
        <MetricSection title="Vegetation" rows={[
          ["Pre-fire NDVI", formatDecimal(cell.ndvi_before, 2), "Sentinel-2 pre-fire median NDVI composite, 70 to 3 days before the fire date."],
          ["Post-fire NDVI", formatDecimal(cell.ndvi_after, 2), "Sentinel-2 post-fire median NDVI composite, 20 to 95 days after the fire date."],
          ["NDVI Drop", formatDecimal(cell.ndvi_drop, 2), "Pre-fire NDVI minus post-fire NDVI; one of the seven ridge_v2 model inputs."],
          ["Tree Cover", formatPercent(cell.tree_cover)],
          ["Annual Tree Cover", cell.tree_cover_annual === null ? "Not available" : formatPercent(cell.tree_cover_annual)],
        ]} />
        <MetricSection title="Technical Details" rows={[
          ["Cell ID", cell.cell_id],
          ["Latitude", formatDecimal(cell.lat, 5)],
          ["Longitude", formatDecimal(cell.lon, 5)],
          ["Model Version", cellsResponse?.model_version ?? "Not available"],
        ]} />
      </div>
    </>}
  </aside>;
}

function MetricSection({ title, rows }: { title: string; rows: Array<[string, string, string?]> }) {
  return <section className="metric-section"><h3>{title}</h3><dl className="metric-grid">{rows.map(([label, data, help]) => <div className="metric-row" key={label} title={help}><dt>{label}{help ? " ⓘ" : ""}</dt><dd>{data}</dd></div>)}</dl></section>;
}

function humanize(value: string): string {
  return value.split("-").map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`).join(" ");
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

function formatContribution(value: number, score: number | null): string {
  const share = score && score > 0 ? Math.round((value / score) * 100) : 0;
  return `${formatDecimal(value, 3)} (${share}% of score)`;
}
