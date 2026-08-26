import type { ApiProblem, CellsQuery, CellsResponse, FireListQuery, FirePerimeter, FireSummary } from "../types";
import { ApiError } from "./ApiError";
import type { FireService } from "./FireService";

export class HttpFireService implements FireService {
  private readonly baseUrl: string;

  constructor(baseUrl: string, private readonly fetcher: typeof fetch = (...args) => fetch(...args)) {
    this.baseUrl = baseUrl.replace(/\/$/, "");
  }

  getFires(query: FireListQuery = {}): Promise<FireSummary[]> {
    const params = new URLSearchParams();
    if (query.quality_flag) params.set("quality_flag", query.quality_flag);
    return this.get<FireSummary[]>(this.url("/api/fires", params));
  }

  getPerimeter(fireId: string): Promise<FirePerimeter> {
    return this.get<FirePerimeter>(this.url(`/api/fires/${encodeURIComponent(fireId)}/perimeter`));
  }

  getCells(fireId: string, query: CellsQuery = {}): Promise<CellsResponse> {
    const params = new URLSearchParams();
    if (query.prediction_status) params.set("prediction_status", query.prediction_status);
    if (query.priority_class) params.set("priority_class", query.priority_class);
    if (query.weights) {
      params.set("recovery", String(query.weights.recovery));
      params.set("erosion", String(query.weights.erosion));
      params.set("access", String(query.weights.access));
    }
    if (query.bbox) {
      params.set("min_lon", String(query.bbox.min_lon));
      params.set("min_lat", String(query.bbox.min_lat));
      params.set("max_lon", String(query.bbox.max_lon));
      params.set("max_lat", String(query.bbox.max_lat));
    }
    return this.get<CellsResponse>(this.url(`/api/fires/${encodeURIComponent(fireId)}/cells`, params));
  }

  private url(path: string, params?: URLSearchParams): string {
    const query = params?.toString();
    return `${this.baseUrl}${path}${query ? `?${query}` : ""}`;
  }

  private async get<T>(url: string): Promise<T> {
    const response = await this.fetcher(url, { method: "GET", headers: { Accept: "application/json" } });
    if (!response.ok) throw new ApiError((await response.json()) as ApiProblem);
    return (await response.json()) as T;
  }
}
