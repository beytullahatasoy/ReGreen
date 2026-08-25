import type { CellsQuery, CellsResponse, FireListQuery, FirePerimeter, FireSummary } from "../types";

export interface FireService {
  getFires(query?: FireListQuery): Promise<FireSummary[]>;
  getPerimeter(fireId: string): Promise<FirePerimeter>;
  getCells(fireId: string, query?: CellsQuery): Promise<CellsResponse>;
}
