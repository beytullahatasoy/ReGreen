import type {
  ActivityQuery,
  CreateActivityInput,
  CreateObservationInput,
  FieldActivity,
  FieldObservation,
  Organisation,
  ObservationQuery,
  Volunteer,
} from "../types/community";
import type { ApiProblem } from "../types";
import { ApiError } from "./ApiError";
import type { CommunityService, ReviewObservationInput, UpdateActivityInput } from "./CommunityService";

export class HttpCommunityService implements CommunityService {
  private readonly baseUrl: string;

  constructor(baseUrl: string, private readonly fetcher: typeof fetch = (...args) => fetch(...args)) {
    this.baseUrl = baseUrl.replace(/\/$/, "");
  }

  getOrganisations(): Promise<Organisation[]> {
    return this.request<Organisation[]>("GET", "/api/organisations");
  }

  createVolunteer(alias?: string): Promise<Volunteer> {
    return this.request<Volunteer>("POST", "/api/volunteers", { alias: alias ?? null });
  }

  getVolunteer(id: string): Promise<Volunteer> {
    return this.request<Volunteer>("GET", `/api/volunteers/${encodeURIComponent(id)}`);
  }

  getActivities(query: ActivityQuery = {}): Promise<FieldActivity[]> {
    const params = new URLSearchParams();
    if (query.fire_id) params.set("fire_id", query.fire_id);
    if (query.status) params.set("status", query.status);
    if (query.volunteer_id) params.set("volunteer_id", query.volunteer_id);
    if (query.limit) params.set("limit", String(query.limit));
    return this.request<FieldActivity[]>("GET", this.withQuery("/api/activities", params));
  }

  createActivity(input: CreateActivityInput & { organisation_id?: number }): Promise<FieldActivity> {
    return this.request<FieldActivity>("POST", "/api/activities", input);
  }

  updateActivity(id: number, input: UpdateActivityInput): Promise<FieldActivity> {
    return this.request<FieldActivity>("PATCH", `/api/activities/${id}`, input);
  }

  joinActivity(id: number, volunteerId: string): Promise<FieldActivity> {
    return this.request<FieldActivity>("POST", `/api/activities/${id}/participants`, { volunteer_id: volunteerId });
  }

  leaveActivity(id: number, volunteerId: string): Promise<FieldActivity> {
    return this.request<FieldActivity>(
      "DELETE",
      `/api/activities/${id}/participants/${encodeURIComponent(volunteerId)}`,
    );
  }

  getObservations(query: ObservationQuery = {}): Promise<FieldObservation[]> {
    const params = new URLSearchParams();
    if (query.fire_id) params.set("fire_id", query.fire_id);
    if (query.status) params.set("status", query.status);
    if (query.volunteer_id) params.set("volunteer_id", query.volunteer_id);
    if (query.limit) params.set("limit", String(query.limit));
    return this.request<FieldObservation[]>("GET", this.withQuery("/api/observations", params));
  }

  createObservation(fireId: string, input: CreateObservationInput): Promise<FieldObservation> {
    return this.request<FieldObservation>("POST", `/api/fires/${encodeURIComponent(fireId)}/observations`, input);
  }

  reviewObservation(id: number, input: ReviewObservationInput): Promise<FieldObservation> {
    return this.request<FieldObservation>("PATCH", `/api/observations/${id}`, input);
  }

  private withQuery(path: string, params: URLSearchParams): string {
    const query = params.toString();
    return `${path}${query ? `?${query}` : ""}`;
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    let response: Response;
    try {
      response = await this.fetcher(url, {
        method,
        headers: { Accept: "application/json", ...(body !== undefined ? { "Content-Type": "application/json" } : {}) },
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
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

    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  }
}
