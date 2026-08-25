import type { CellsResponse, FireSummary, PredictionStatus, PriorityClass, PriorityWeights } from "../types";
import { predictionStatusLabels, priorityClassLabels } from "../utils/presentation";
import type { PriorityScenarioComparison } from "../utils/comparePriorityScenarios";
import type { MapLayer } from "./Legend";

const priorities: Array<[PriorityClass, string]> = [["COK_YUKSEK", "#b42318"], ["YUKSEK", "#d75b20"], ["ORTA", "#c58a12"], ["DUSUK", "#438360"]];
const statuses: PredictionStatus[] = ["predicted", "low_severity", "no_data"];

interface Props {
  fires: FireSummary[]; selectedFire: FireSummary | null; selectedFireId: string;
  onFireChange: (id: string) => void; layer: MapLayer; onLayerChange: (layer: MapLayer) => void;
  priorities: Set<PriorityClass>; onTogglePriority: (value: PriorityClass) => void;
  statuses: Set<PredictionStatus>; onToggleStatus: (value: PredictionStatus) => void;
  weights: PriorityWeights | null; onWeightChange: (weights: PriorityWeights) => void; onResetWeights: () => void;
  cellsResponse: CellsResponse | null; open: boolean; isMock: boolean;
  comparison: PriorityScenarioComparison | null; onClearComparison: () => void;
}

export function ControlPanel(props: Props) {
  const updateWeight = (key: keyof PriorityWeights, value: number) => {
    if (props.weights) props.onWeightChange({ ...props.weights, [key]: value });
  };
  return <aside className={`control-panel ${props.open ? "control-panel--open" : ""}`}>
    <div className="control-panel__scroll">
      <div className="control-panel__identity"><p className="eyebrow">ReGreen</p><h1>Post-Fire<br />Recovery</h1></div>
      <section className="control-section"><h2 className="control-section__title">01 · Fire Area</h2>
        <select className="field-select" value={props.selectedFireId} onChange={(event) => props.onFireChange(event.target.value)} aria-label="Fire area">
          {props.fires.map((fire) => <option value={fire.fire_id} key={fire.fire_id}>{fire.province} · {fire.fire_id}</option>)}
        </select>
        {props.selectedFire && <div className="fire-meta"><span>Province</span><strong>{props.selectedFire.province}</strong><span>Region</span><strong>{props.selectedFire.region}</strong><span>Date</span><strong>{props.selectedFire.fire_date}</strong></div>}
      </section>
      <section className="control-section"><h2 className="control-section__title">02 · Map Layer</h2>
        <div className="segmented">{([['priority','Priority'],['severity','Burn Severity'],['status','Prediction Status']] as Array<[MapLayer,string]>).map(([value,label]) => <button key={value} aria-pressed={props.layer === value} onClick={() => props.onLayerChange(value)}>{label}</button>)}</div>
      </section>
      <section className="control-section"><h2 className="control-section__title">03 · Priority Class</h2><div className="check-list">
        {priorities.map(([value,color]) => <label className="check-row" key={value}><input type="checkbox" checked={props.priorities.has(value)} onChange={() => props.onTogglePriority(value)} /><span className="class-swatch" style={{ background: color }} />{priorityClassLabels[value]}</label>)}
      </div></section>
      <section className="control-section"><h2 className="control-section__title">04 · Prediction Status</h2><div className="check-list">
        {statuses.map((value) => <label className="check-row" key={value}><input type="checkbox" checked={props.statuses.has(value)} onChange={() => props.onToggleStatus(value)} />{predictionStatusLabels[value]}</label>)}
      </div></section>
      <section className="control-section"><div className="section-heading"><h2 className="control-section__title">05 · Priority Scenario</h2><button className="text-button" type="button" onClick={props.onResetWeights} disabled={!props.weights}>Reset to default</button></div>
        <p className="section-description">Adjust how intervention priorities are calculated.</p>
        {([['recovery','Recovery Need'],['erosion','Erosion Risk'],['access','Accessibility']] as Array<[keyof PriorityWeights,string]>).map(([key,label]) => <div className="weight" key={key}><label className="weight__label"><span>{label}</span><output>{props.weights ? `${Math.round(props.weights[key] * 100)}%` : "—"}</output></label><input aria-label={`${label} weight`} type="range" min="0" max="1" step="0.05" value={props.weights?.[key] ?? 0} disabled={!props.weights} onChange={(event) => updateWeight(key, Number(event.target.value))} /></div>)}
        <p className="weight-note">Scenario values are normalized by the priority service.</p>
        {props.comparison && <ScenarioImpact comparison={props.comparison} onClear={props.onClearComparison} />}
      </section>
    </div>
    <div className="technical"><strong>QUALITY</strong> · {props.selectedFire?.quality_flag ?? "—"}<br />{props.isMock ? <><strong>DEMO DATA</strong><br /></> : <><strong>MODEL RUN</strong> · {props.cellsResponse?.model_run_id ?? "—"}<br /></>}<strong>GENERATED</strong> · {props.cellsResponse ? new Date(props.cellsResponse.generated_at).toLocaleDateString() : "—"}</div>
  </aside>;
}

function ScenarioImpact({ comparison, onClear }: { comparison: PriorityScenarioComparison; onClear: () => void }) {
  const weights = (value: PriorityWeights) => `Recovery ${Math.round(value.recovery * 100)}% · Erosion ${Math.round(value.erosion * 100)}% · Access ${Math.round(value.access * 100)}%`;
  return <div className="scenario-impact" aria-live="polite">
    <div className="scenario-impact__head"><strong>Scenario Impact</strong><button type="button" onClick={onClear}>Clear comparison</button></div>
    <div className="scenario-impact__counts">
      <span><b>{comparison.increased.toLocaleString()}</b> cells <i>↑</i> priority</span>
      <span><b>{comparison.decreased.toLocaleString()}</b> cells <i>↓</i> priority</span>
      <span><b>{comparison.unchanged.toLocaleString()}</b> cells unchanged</span>
      <span><b>{comparison.changed.toLocaleString()}</b> cells changed</span>
    </div>
    <div className="scenario-snapshot"><span>Previous</span><p>{weights(comparison.previousWeights)}</p><span>Current</span><p>{weights(comparison.currentWeights)}</p></div>
  </div>;
}
