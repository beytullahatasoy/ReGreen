import type { CellsQuery, CellsResponse, CellVerdict, FireListQuery, FireNarrative, FirePerimeter, FireSummary, HukumSozlugu } from "../types";

export interface FireService {
  getFires(query?: FireListQuery): Promise<FireSummary[]>;
  getPerimeter(fireId: string): Promise<FirePerimeter>;
  getCells(fireId: string, query?: CellsQuery): Promise<CellsResponse>;
  getCellVerdict(fireId: string, cellId: string): Promise<CellVerdict>;
  getFireNarrative(fireId: string): Promise<FireNarrative>;
  getHukumSozlugu(surum?: string): Promise<HukumSozlugu>;
}
