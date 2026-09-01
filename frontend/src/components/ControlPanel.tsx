import type { CellsResponse, FireNarrative, FireSummary, PredictionStatus, PriorityClass, PriorityWeights } from "../types";
import { predictionStatusLabels, priorityClassLabels } from "../utils/presentation";
import type { PriorityScenarioComparison } from "../utils/comparePriorityScenarios";
import type { MapLayer } from "./Legend";

const priorities: Array<[PriorityClass, string]> = [["COK_YUKSEK", "#b42318"], ["YUKSEK", "#d75b20"], ["ORTA", "#c58a12"], ["DUSUK", "#438360"]];
const statuses: PredictionStatus[] = ["predicted", "low_severity", "no_data"];
const scenarioPresets: Array<{ id: string; label: string; description: string; weights: PriorityWeights | null }> = [
  { id: "balanced", label: "Balanced", description: "Varsayılan denge", weights: null },
  { id: "recovery", label: "Recovery", description: "Toparlanma odaklı", weights: { recovery: 0.7, erosion: 0.2, access: 0.1 } },
  { id: "erosion", label: "Erosion", description: "Erozyon odaklı", weights: { recovery: 0.3, erosion: 0.6, access: 0.1 } },
  { id: "access", label: "Access", description: "Saha erişimi odaklı", weights: { recovery: 0.4, erosion: 0.2, access: 0.4 } },
];

interface Props {
  fires: FireSummary[]; selectedFire: FireSummary | null; selectedFireId: string;
  onFireChange: (id: string) => void; layer: MapLayer; onLayerChange: (layer: MapLayer) => void;
  priorities: Set<PriorityClass>; onTogglePriority: (value: PriorityClass) => void;
  statuses: Set<PredictionStatus>; onToggleStatus: (value: PredictionStatus) => void;
  weights: PriorityWeights | null; defaultWeights: PriorityWeights | null;
  onWeightChange: (weights: PriorityWeights) => void; onResetWeights: () => void;
  cellsResponse: CellsResponse | null; open: boolean; isMock: boolean;
  comparison: PriorityScenarioComparison | null; onClearComparison: () => void;
  fireNarrative: FireNarrative | null;
}

export function ControlPanel(props: Props) {
  const firesByProvince = groupFiresByProvince(props.fires);
  const updateWeight = (key: keyof PriorityWeights, value: number) => {
    if (props.weights) props.onWeightChange({ ...props.weights, [key]: value });
  };
  const applyPreset = (weights: PriorityWeights | null) => {
    if (weights) props.onWeightChange({ ...weights });
    else props.onResetWeights();
  };

  return <aside className={`control-panel ${props.open ? "control-panel--open" : ""}`}>
    <div className="control-panel__scroll">
      <div className="control-panel__identity"><p className="eyebrow">ReGreen</p><h1>Post-Fire<br />Recovery</h1></div>
      <section className="control-section"><h2 className="control-section__title">01 · Fire Area</h2>
        <select className="field-select" value={props.selectedFireId} onChange={(event) => props.onFireChange(event.target.value)} aria-label="Fire area">
          {firesByProvince.map(([province, fires]) => <optgroup label={province} key={province}>
            {fires.map((fire) => <option value={fire.fire_id} key={fire.fire_id}>{fire.fire_id} · {fire.fire_date}</option>)}
          </optgroup>)}
        </select>
        {props.selectedFire && <div className="fire-meta"><span>Province</span><strong>{props.selectedFire.province}</strong><span>Region</span><strong>{props.selectedFire.region}</strong><span>Date</span><strong>{props.selectedFire.fire_date}</strong></div>}
        <FireSnapshot narrative={props.fireNarrative} />
        {props.fireNarrative && <details className="disclosure fire-assessment">
          <summary>Fire assessment <span>Yangın özeti</span></summary>
          <p className="fire-narrative">{props.fireNarrative.paragraf}</p>
        </details>}
      </section>
      <section className="control-section"><h2 className="control-section__title">02 · Map View</h2>
        <div className="segmented">{([['priority','Priority'],['severity','Burn Severity'],['status','Prediction Status']] as Array<[MapLayer,string]>).map(([value,label]) => <button key={value} aria-pressed={props.layer === value} onClick={() => props.onLayerChange(value)}>{label}</button>)}</div>
        <details className="disclosure filter-disclosure">
          <summary>Map filters <span>Harita filtreleri</span></summary>
          <div className="filter-group"><strong>Priority class</strong><div className="check-list">
            {priorities.map(([value,color]) => <label className="check-row" key={value}><input type="checkbox" checked={props.priorities.has(value)} onChange={() => props.onTogglePriority(value)} /><span className="class-swatch" style={{ background: color }} />{priorityClassLabels[value]}</label>)}
          </div></div>
          <div className="filter-group"><strong>Prediction status</strong><div className="check-list">
            {statuses.map((value) => <label className="check-row" key={value}><input type="checkbox" checked={props.statuses.has(value)} onChange={() => props.onToggleStatus(value)} />{predictionStatusLabels[value]}</label>)}
          </div></div>
        </details>
      </section>
      <section className="control-section"><div className="section-heading"><h2 className="control-section__title">03 · Planning Scenario</h2><button className="text-button" type="button" onClick={props.onResetWeights} disabled={!props.weights}>Reset</button></div>
        <p className="section-description">Choose what matters most in the field plan. <span>Saha planında öne çıkacak ihtiyacı seçin.</span></p>
        <div className="scenario-presets">
          {scenarioPresets.map((preset) => {
            const target = preset.weights ?? props.defaultWeights;
            return <button type="button" key={preset.id} aria-pressed={sameWeights(props.weights, target)} disabled={!target} onClick={() => applyPreset(preset.weights)}>
              <strong>{preset.label}</strong><span>{preset.description}</span>
            </button>;
          })}
        </div>
        <details className="disclosure advanced-scenario">
          <summary>Advanced weights <span>Gelişmiş ayarlar</span></summary>
          {([['recovery','Recovery Need'],['erosion','Erosion Risk'],['access','Accessibility']] as Array<[keyof PriorityWeights,string]>).map(([key,label]) => <div className="weight" key={key}><label className="weight__label"><span>{label}</span><output>{props.weights ? `${Math.round(props.weights[key] * 100)}%` : "—"}</output></label><input aria-label={`${label} weight`} type="range" min="0" max="1" step="0.05" value={props.weights?.[key] ?? 0} disabled={!props.weights} onChange={(event) => updateWeight(key, Number(event.target.value))} /></div>)}
          <p className="weight-note">Values are normalized automatically. · Değerler otomatik olarak toplam %100'e normalize edilir.</p>
        </details>
        {props.comparison && <ScenarioImpact comparison={props.comparison} onClear={props.onClearComparison} />}
      </section>
    </div>
    <div className="technical"><strong>QUALITY</strong> · {props.selectedFire?.quality_flag ?? "—"}<br />{props.isMock && <><strong>DEMO DATA</strong><br /></>}<strong>MODEL</strong> · {props.cellsResponse?.model_version ?? "—"}<br /><strong>GENERATED</strong> · {props.cellsResponse ? new Date(props.cellsResponse.generated_at).toLocaleDateString() : "—"}</div>
  </aside>;
}

function FireSnapshot({ narrative }: { narrative: FireNarrative | null }) {
  const numbers = narrative?.sayi_blogu;
  if (!numbers?.buyukluk || !numbers.hukum_dagilimi) return null;
  const distribution = numbers.hukum_dagilimi;
  const action = (distribution.EROZYON_ONCE ?? 0) + (distribution.DIKIM_ADAYI ?? 0);
  const queued = distribution.ONCELIGE_GORE ?? 0;
  const natural = (distribution.GENCLESME_IZLE ?? 0) + (distribution.IZLE ?? 0);
  const coverage = numbers.buyukluk.hucre > 0 ? Math.round(numbers.buyukluk.tahminli_hucre / numbers.buyukluk.hucre * 100) : 0;
  return <div className="fire-snapshot" aria-label="Fire decision summary">
    <SnapshotCard value={action} label="Direct action" help="Doğrudan müdahale" />
    <SnapshotCard value={queued} label="Priority list" help="Öncelik sırası" />
    <SnapshotCard value={natural} label="Monitor" help="Doğal süreç" />
    <SnapshotCard value={`${coverage}%`} label="Coverage" help={`${numbers.buyukluk.tahminli_hucre}/${numbers.buyukluk.hucre} tahmin`} />
  </div>;
}

function SnapshotCard({ value, label, help }: { value: number | string; label: string; help: string }) {
  return <div className="snapshot-card"><strong>{value}</strong><span>{label}</span><small>{help}</small></div>;
}

function groupFiresByProvince(fires: FireSummary[]): Array<[string, FireSummary[]]> {
  const groups = new Map<string, FireSummary[]>();
  fires.forEach((fire) => groups.set(fire.province, [...(groups.get(fire.province) ?? []), fire]));
  return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b, "tr"));
}

function sameWeights(current: PriorityWeights | null, target: PriorityWeights | null): boolean {
  if (!current || !target) return false;
  return (Object.keys(current) as Array<keyof PriorityWeights>)
    .every((key) => Math.abs(current[key] - target[key]) < 1e-9);
}

function ScenarioImpact({ comparison, onClear }: { comparison: PriorityScenarioComparison; onClear: () => void }) {
  const weights = (value: PriorityWeights) => `Recovery ${Math.round(value.recovery * 100)}% · Erosion ${Math.round(value.erosion * 100)}% · Access ${Math.round(value.access * 100)}%`;
  return <div className="scenario-impact" aria-live="polite">
    <div className="scenario-impact__head"><strong>Scenario Impact · Senaryo etkisi</strong><button type="button" onClick={onClear}>Clear</button></div>
    <div className="scenario-impact__counts">
      <span><b>{comparison.increased.toLocaleString()}</b> cells <i>↑</i> priority</span>
      <span><b>{comparison.decreased.toLocaleString()}</b> cells <i>↓</i> priority</span>
      <span><b>{comparison.unchanged.toLocaleString()}</b> cells unchanged</span>
    </div>
    <div className="scenario-snapshot"><span>Previous</span><p>{weights(comparison.previousWeights)}</p><span>Current</span><p>{weights(comparison.currentWeights)}</p></div>
  </div>;
}
