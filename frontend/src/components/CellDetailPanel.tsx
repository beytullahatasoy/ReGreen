import type { Cell } from "../types";
import {
  formatDecimal,
  formatDegrees,
  formatKilometers,
  formatMeters,
  predictionStatusLabels,
  priorityClassLabels,
} from "../utils/presentation";
import type { PriorityClassTransition } from "../utils/comparePriorityScenarios";

export function CellDetailPanel({ cell, priorityTransition, onClose }: { cell: Cell | null; priorityTransition: PriorityClassTransition | null; onClose: () => void }) {
  const predicted = cell?.prediction_status === "predicted";
  const priorityClass = cell?.priority_class ? priorityClassLabels[cell.priority_class] : "Not available";

  return <aside className={`detail-panel ${cell ? "detail-panel--open" : ""}`} aria-hidden={!cell}>
    {cell && <>
      <div className="detail-panel__head"><div><p className="eyebrow">Cell Analysis</p><h2>{cell.cell_id}</h2></div><button className="icon-button" onClick={onClose} aria-label="Close cell details">×</button></div>
      <div className="detail-panel__body">
        {predicted && <div className="priority-banner"><strong>{priorityClass}<br />PRIORITY</strong><span>{formatDecimal(cell.priority_score, 2)}</span></div>}
        {cell.prediction_status === "low_severity" && <div className="status-note"><strong>Not Prioritized</strong><br />Burn severity was below the prioritization threshold.</div>}
        {cell.prediction_status === "no_data" && <div className="status-note"><strong>Insufficient Data</strong><br />No recovery or priority result was produced for this cell.</div>}
        {priorityTransition && <div className={`priority-change priority-change--${priorityTransition.direction}`}><strong>Priority changed</strong><span>{priorityClassLabels[priorityTransition.previous]} → {priorityClassLabels[priorityTransition.current]}</span></div>}

        <MetricSection title="Priority" rows={[
          ["Priority Class", predicted ? priorityClass : "Not available"],
          ["Priority Score", predicted ? formatDecimal(cell.priority_score, 2) : "Not available"],
        ]} />
        <MetricSection title="Recovery" rows={[
          ["Prediction Status", predictionStatusLabels[cell.prediction_status]],
          ["Recovery Gap", predicted ? formatDecimal(cell.recovery_gap_pred, 2) : "Not available"],
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
          ["NDVI Before", formatDecimal(cell.ndvi_before, 2)],
          ["NDVI After", formatDecimal(cell.ndvi_after, 2)],
          ["NDVI Drop", formatDecimal(cell.ndvi_drop, 2)],
          ["Tree Cover", formatPercent(cell.tree_cover)],
          ["Annual Tree Cover", cell.tree_cover_annual === null ? "Not available" : formatPercent(cell.tree_cover_annual)],
        ]} />
        <MetricSection title="Technical Details" rows={[
          ["Cell ID", cell.cell_id],
          ["Latitude", formatDecimal(cell.lat, 5)],
          ["Longitude", formatDecimal(cell.lon, 5)],
        ]} />
      </div>
    </>}
  </aside>;
}

function MetricSection({ title, rows }: { title: string; rows: Array<[string, string]> }) {
  return <section className="metric-section"><h3>{title}</h3><dl className="metric-grid">{rows.map(([label, data]) => <div className="metric-row" key={label}><dt>{label}</dt><dd>{data}</dd></div>)}</dl></section>;
}

function humanize(value: string): string {
  return value.split("-").map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`).join(" ");
}

function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`;
}
