import { useEffect, useMemo, useRef, useState } from "react";
import { ApiError, fireService } from "../services";
import type { Cell, CellsResponse, CellVerdict, FireNarrative, FirePerimeter, FireSummary, HukumSozlugu, PredictionStatus, PriorityClass, PriorityWeights } from "../types";
import { comparePriorityScenarios, type PriorityScenarioComparison } from "../utils/comparePriorityScenarios";

export function useFireWorkspace() {
  const [fires, setFires] = useState<FireSummary[]>([]);
  const [selectedFireId, setSelectedFireId] = useState("");
  const [perimeter, setPerimeter] = useState<FirePerimeter | null>(null);
  const [cellsResponse, setCellsResponse] = useState<CellsResponse | null>(null);
  const [selectedCell, setSelectedCell] = useState<Cell | null>(null);
  const [weights, setWeights] = useState<PriorityWeights | null>(null);
  const [defaultWeights, setDefaultWeights] = useState<PriorityWeights | null>(null);
  const [priorityClasses, setPriorityClasses] = useState<Set<PriorityClass>>(new Set(["COK_YUKSEK", "YUKSEK", "ORTA", "DUSUK"]));
  const [predictionStatuses, setPredictionStatuses] = useState<Set<PredictionStatus>>(new Set(["predicted", "low_severity", "no_data"]));
  const [loading, setLoading] = useState(true);
  const [loadingMessage, setLoadingMessage] = useState("Loading recovery analysis…");
  const [error, setError] = useState<string | null>(null);
  const [scenarioFeedback, setScenarioFeedback] = useState<string | null>(null);
  const [scenarioComparison, setScenarioComparison] = useState<PriorityScenarioComparison | null>(null);
  const [scenarioHighlights, setScenarioHighlights] = useState<{ increased: Set<string>; decreased: Set<string> }>({ increased: new Set(), decreased: new Set() });
  const [hukumSozlugu, setHukumSozlugu] = useState<HukumSozlugu | null>(null);
  const [fireNarrative, setFireNarrative] = useState<FireNarrative | null>(null);
  const [selectedCellVerdict, setSelectedCellVerdict] = useState<CellVerdict | null>(null);
  const [verdictLoading, setVerdictLoading] = useState(false);
  const previousResponseRef = useRef<CellsResponse | null>(null);

  useEffect(() => {
    let active = true;
    fireService.getFires().then((items) => {
      if (!active) return;
      setFires(items);
      setSelectedFireId(items[0]?.fire_id ?? "");
    }).catch((reason: unknown) => active && setError(reason instanceof Error ? reason.message : "Fire areas could not be loaded."))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!selectedFireId) return;
    let active = true;
    setLoading(true); setLoadingMessage(weights ? "Updating priority scenario…" : "Loading recovery analysis…");
    setError(null);
    if (!weights) setSelectedCell(null);
    const timer = window.setTimeout(() => {
      Promise.all([
        fireService.getPerimeter(selectedFireId),
        fireService.getCells(selectedFireId, weights ? { weights } : undefined),
      ]).then(([nextPerimeter, nextCells]) => {
        if (!active) return;
        if (weights && previousResponseRef.current?.fire_id === selectedFireId) {
          const comparison = comparePriorityScenarios(
            previousResponseRef.current.items,
            nextCells.items,
            previousResponseRef.current.applied_weights,
            nextCells.applied_weights,
          );
          setScenarioComparison(comparison);
          setScenarioHighlights({ increased: comparison.increasedCellIds, decreased: comparison.decreasedCellIds });
          setScenarioFeedback(`Priority scenario updated${comparison.changed ? ` · ${comparison.changed.toLocaleString()} cells changed priority` : ""}`);
        }
        previousResponseRef.current = nextCells;
        setPerimeter(nextPerimeter); setCellsResponse(nextCells);
        setSelectedCell((current) => current ? nextCells.items.find((cell) => cell.cell_id === current.cell_id) ?? current : null);
        if (!weights) setDefaultWeights(nextCells.applied_weights);
      }).catch((reason: unknown) => active && setError(reason instanceof Error ? reason.message : "Analysis data could not be loaded."))
        .finally(() => active && setLoading(false));
    }, 180);
    return () => { active = false; window.clearTimeout(timer); };
  }, [selectedFireId, weights]);

  useEffect(() => {
    let active = true;
    fireService.getHukumSozlugu()
      .then((sozluk) => { if (active) setHukumSozlugu(sozluk); })
      .catch((reason: unknown) => {
        if (!active) return;
        setHukumSozlugu(null);
        console.error("Decision dictionary could not be loaded; species data remains unapproved.", reason);
      });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!selectedFireId) { setFireNarrative(null); return; }
    let active = true;
    setFireNarrative(null);
    fireService.getFireNarrative(selectedFireId).then((narrative) => {
      if (active) setFireNarrative(narrative);
    }).catch((reason: unknown) => {
      if (!active) return;
      setFireNarrative(null);
      const isKnownMissingNarrative = reason instanceof ApiError && reason.problem.code === "FIRE_NARRATIVE_NOT_FOUND";
      if (!isKnownMissingNarrative) console.error("Fire narrative could not be loaded.", reason);
    });
    return () => { active = false; };
  }, [selectedFireId]);

  useEffect(() => {
    if (!selectedCell || !selectedFireId) { setSelectedCellVerdict(null); setVerdictLoading(false); return; }
    let active = true;
    setSelectedCellVerdict(null);
    setVerdictLoading(true);
    fireService.getCellVerdict(selectedFireId, selectedCell.cell_id).then(async (verdict) => {
      if (hukumSozlugu?.surum !== verdict.hukum_version) {
        try {
          const sozluk = await fireService.getHukumSozlugu(verdict.hukum_version);
          if (active) setHukumSozlugu(sozluk);
        } catch (reason: unknown) {
          if (active) console.error("Versioned decision dictionary could not be loaded.", reason);
        }
      }
      if (active) setSelectedCellVerdict(verdict);
    }).catch((reason: unknown) => {
      if (!active) return;
      setSelectedCellVerdict(null);
      const isKnownMissingVerdict = reason instanceof ApiError && (reason.problem.code === "CELL_VERDICT_NOT_FOUND" || reason.problem.code === "CELL_NOT_FOUND");
      if (!isKnownMissingVerdict) console.error("Cell verdict could not be loaded.", reason);
    }).finally(() => { if (active) setVerdictLoading(false); });
    return () => { active = false; };
  }, [selectedFireId, selectedCell?.cell_id]);

  useEffect(() => {
    if (!scenarioFeedback) return;
    const timer = window.setTimeout(() => setScenarioFeedback(null), 2200);
    return () => window.clearTimeout(timer);
  }, [scenarioFeedback]);

  useEffect(() => {
    if (scenarioHighlights.increased.size === 0 && scenarioHighlights.decreased.size === 0) return;
    const timer = window.setTimeout(() => setScenarioHighlights({ increased: new Set(), decreased: new Set() }), 1050);
    return () => window.clearTimeout(timer);
  }, [scenarioHighlights]);

  const selectFire = (fireId: string) => {
    setWeights(null);
    setDefaultWeights(null);
    setCellsResponse(null);
    setPerimeter(null);
    previousResponseRef.current = null;
    setScenarioFeedback(null);
    setScenarioComparison(null);
    setScenarioHighlights({ increased: new Set(), decreased: new Set() });
    setSelectedFireId(fireId);
  };

  const selectedFire = fires.find((fire) => fire.fire_id === selectedFireId) ?? null;
  const visibleCells = useMemo(() => (cellsResponse?.items ?? []).filter((cell) =>
    predictionStatuses.has(cell.prediction_status) &&
    (cell.priority_class === null || priorityClasses.has(cell.priority_class)),
  ), [cellsResponse, predictionStatuses, priorityClasses]);

  const togglePriority = (value: PriorityClass) => setPriorityClasses((current) => {
    const next = new Set(current); next.has(value) ? next.delete(value) : next.add(value); return next;
  });
  const toggleStatus = (value: PredictionStatus) => setPredictionStatuses((current) => {
    const next = new Set(current); next.has(value) ? next.delete(value) : next.add(value); return next;
  });

  return { fires, selectedFire, selectedFireId, selectFire, perimeter, cellsResponse, visibleCells,
    selectedCell, setSelectedCell, weights: weights ?? cellsResponse?.applied_weights ?? null, defaultWeights, setWeights,
    resetWeights: () => defaultWeights && setWeights({ ...defaultWeights }), priorityClasses, togglePriority,
    clearScenarioComparison: () => {
      setScenarioComparison(null);
      setScenarioHighlights({ increased: new Set(), decreased: new Set() });
    },
    predictionStatuses, toggleStatus, loading, loadingMessage, error, scenarioFeedback, scenarioComparison, scenarioHighlights,
    hukumSozlugu, fireNarrative, selectedCellVerdict, verdictLoading };
}
