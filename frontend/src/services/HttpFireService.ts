import type { ApiProblem, CellsQuery, CellsResponse, CellVerdict, FireListQuery, FireNarrative, FirePerimeter, FireSummary, HukumSozlugu } from "../types";
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

  getCellVerdict(fireId: string, cellId: string): Promise<CellVerdict> {
    return this.get<CellVerdict>(this.url(`/api/fires/${encodeURIComponent(fireId)}/cells/${encodeURIComponent(cellId)}/hukum`));
  }

  getFireNarrative(fireId: string): Promise<FireNarrative> {
    return this.get<FireNarrative>(this.url(`/api/fires/${encodeURIComponent(fireId)}/summary`));
  }

  getHukumSozlugu(surum?: string): Promise<HukumSozlugu> {
    const params = new URLSearchParams();
    if (surum) params.set("surum", surum);
    return this.get<HukumSozlugu>(this.url("/api/hukum-sozlugu", params));
  }

  private url(path: string, params?: URLSearchParams): string {
    const query = params?.toString();
    return `${this.baseUrl}${path}${query ? `?${query}` : ""}`;
  }

  private async get<T>(url: string): Promise<T> {
    let response: Response;
    try {
      response = await this.fetcher(url, { method: "GET", headers: { Accept: "application/json" } });
    } catch (cause) {
      throw new Error(`Backend API could not be reached at ${this.baseUrl || "the current origin"}. Start ReGreen.Api and verify the frontend API URL.`, { cause });
    }

    if (!response.ok) {
      const contentType = response.headers.get("Content-Type") ?? "";
      if (contentType.includes("application/problem+json") || contentType.includes("application/json")) {
        const problem = await response.json() as ApiProblem;
        throw new ApiError(problem);
      }
      throw new Error(`Backend API request failed with HTTP ${response.status} (${response.statusText || "unknown error"}).`);
    }

    return (await response.json()) as T;
  }
}
