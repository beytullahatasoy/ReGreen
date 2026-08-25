import type { Cell, PriorityClass, PriorityWeights } from "../types";

const priorityRank: Record<PriorityClass, number> = {
  DUSUK: 0,
  ORTA: 1,
  YUKSEK: 2,
  COK_YUKSEK: 3,
};

export interface PriorityClassTransition {
  cellId: string;
  previous: PriorityClass;
  current: PriorityClass;
  direction: "increased" | "decreased";
}

export interface PriorityScenarioComparison {
  previousWeights: PriorityWeights;
  currentWeights: PriorityWeights;
  increased: number;
  decreased: number;
  unchanged: number;
  changed: number;
  increasedCellIds: Set<string>;
  decreasedCellIds: Set<string>;
  transitions: Map<string, PriorityClassTransition>;
}

export function comparePriorityScenarios(
  previousCells: Cell[],
  currentCells: Cell[],
  previousWeights: PriorityWeights,
  currentWeights: PriorityWeights,
): PriorityScenarioComparison {
  const previousById = new Map(previousCells
    .filter(isComparable)
    .map((cell) => [cell.cell_id, cell.priority_class as PriorityClass]));

  const increasedCellIds = new Set<string>();
  const decreasedCellIds = new Set<string>();
  const transitions = new Map<string, PriorityClassTransition>();
  let unchanged = 0;

  for (const cell of currentCells) {
    if (!isComparable(cell)) continue;
    const previous = previousById.get(cell.cell_id);
    if (!previous) continue;
    const current = cell.priority_class as PriorityClass;
    const difference = priorityRank[current] - priorityRank[previous];
    if (difference === 0) {
      unchanged += 1;
      continue;
    }
    const direction = difference > 0 ? "increased" : "decreased";
    const target = direction === "increased" ? increasedCellIds : decreasedCellIds;
    target.add(cell.cell_id);
    transitions.set(cell.cell_id, { cellId: cell.cell_id, previous, current, direction });
  }

  return {
    previousWeights,
    currentWeights,
    increased: increasedCellIds.size,
    decreased: decreasedCellIds.size,
    unchanged,
    changed: increasedCellIds.size + decreasedCellIds.size,
    increasedCellIds,
    decreasedCellIds,
    transitions,
  };
}

function isComparable(cell: Cell): boolean {
  return cell.prediction_status === "predicted" && cell.priority_class !== null;
}
