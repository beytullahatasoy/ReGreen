import { useCallback, useState } from "react";
import { CellDetailPanel } from "../../components/CellDetailPanel";
import { ControlPanel } from "../../components/ControlPanel";
import { type MapLayer } from "../../components/Legend";
import { MapWorkspace } from "../../components/MapWorkspace";
import { WorkspaceHeader } from "../../components/WorkspaceHeader";
import { useFireWorkspace } from "../../hooks/useFireWorkspace";
import type { Cell } from "../../types";

export function ExpertWorkspace() {
  const data = useFireWorkspace();
  const [layer, setLayer] = useState<MapLayer>("priority");
  const [controlsOpen, setControlsOpen] = useState(false);
  const selectCell = useCallback((cell: Cell) => data.setSelectedCell(cell), [data.setSelectedCell]);

  return <main className="workspace">
    <WorkspaceHeader fire={data.selectedFire} />
    <div className="workspace__body">
      <ControlPanel fires={data.fires} selectedFire={data.selectedFire} selectedFireId={data.selectedFireId} onFireChange={data.selectFire}
        layer={layer} onLayerChange={setLayer} priorities={data.priorityClasses} onTogglePriority={data.togglePriority}
        statuses={data.predictionStatuses} onToggleStatus={data.toggleStatus} weights={data.weights} onWeightChange={data.setWeights}
        onResetWeights={data.resetWeights} cellsResponse={data.cellsResponse} open={controlsOpen} isMock={(import.meta.env.VITE_SERVICE_MODE ?? "mock") === "mock"}
        comparison={data.scenarioComparison} onClearComparison={data.clearScenarioComparison} />
      <MapWorkspace perimeter={data.perimeter} cells={data.visibleCells} cellSizeM={data.cellsResponse?.cell_size_m ?? 250}
        layer={layer} loading={data.loading} loadingMessage={data.loadingMessage} error={data.error}
        selectedCell={data.selectedCell} scenarioHighlights={data.scenarioHighlights} scenarioFeedback={data.scenarioFeedback}
        onSelectCell={selectCell} />
      <button className="mobile-panel-toggle" onClick={() => setControlsOpen((open) => !open)} aria-label="Toggle controls">Controls</button>
      <CellDetailPanel cell={data.selectedCell} cellsResponse={data.cellsResponse} priorityTransition={data.selectedCell && data.scenarioComparison ? data.scenarioComparison.transitions.get(data.selectedCell.cell_id) ?? null : null} onClose={() => data.setSelectedCell(null)} />
    </div>
  </main>;
}
