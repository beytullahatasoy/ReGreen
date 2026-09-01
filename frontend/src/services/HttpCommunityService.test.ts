import { describe, expect, it, vi } from "vitest";
import { HttpCommunityService } from "./HttpCommunityService";

const jsonResponse = (body: unknown, status = 200) => new Response(JSON.stringify(body), {
  status,
  headers: { "Content-Type": "application/json" },
});

describe("HttpCommunityService", () => {
  it("serializes activity query filters", async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse([]));
    const service = new HttpCommunityService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await service.getActivities({ fire_id: "AKD 2021/01", status: "open", limit: 5 });

    const url = new URL(String(fetcher.mock.calls[0]?.[0]));
    expect(url.pathname).toBe("/api/activities");
    expect(Object.fromEntries(url.searchParams)).toEqual({ fire_id: "AKD 2021/01", status: "open", limit: "5" });
    expect(fetcher.mock.calls[0]?.[1]).toMatchObject({ method: "GET" });
  });

  it("posts a JSON body when joining an activity", async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse({}));
    const service = new HttpCommunityService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await service.joinActivity(7, "8f14e...volunteer");

    const [url, init] = fetcher.mock.calls[0]!;
    expect(String(url)).toBe("http://localhost:5066/api/activities/7/participants");
    expect(init).toMatchObject({ method: "POST", headers: { "Content-Type": "application/json" } });
    expect(JSON.parse(init!.body as string)).toEqual({ volunteer_id: "8f14e...volunteer" });
  });

  it("URL-encodes the fire id and volunteer id path segments", async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse({}));
    const service = new HttpCommunityService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await service.createObservation("AKD 2021/01", {
      volunteer_id: "v1", activity_id: null, location: "x", photo_name: null, answers: ["a"],
    });
    await service.leaveActivity(3, "vol/2");

    expect(new URL(String(fetcher.mock.calls[0]?.[0])).pathname).toBe("/api/fires/AKD%202021%2F01/observations");
    expect(new URL(String(fetcher.mock.calls[1]?.[0])).pathname).toBe("/api/activities/3/participants/vol%2F2");
  });

  it("sends PATCH for review with the review payload", async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse({}));
    const service = new HttpCommunityService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await service.reviewObservation(42, { status: "accepted", review_note: "looks good" });

    const [url, init] = fetcher.mock.calls[0]!;
    expect(String(url)).toBe("http://localhost:5066/api/observations/42");
    expect(init).toMatchObject({ method: "PATCH" });
    expect(JSON.parse(init!.body as string)).toEqual({ status: "accepted", review_note: "looks good" });
  });

  it("converts a problem response into ApiError", async () => {
    const problem = {
      type: "https://regreen/errors/activity-full",
      title: "Etkinlik kontenjanı dolu",
      status: 409,
      code: "ACTIVITY_FULL" as const,
      detail: "5 kimlikli etkinliğin 10 kişilik kontenjanı doldu.",
    };
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse(problem, 409));
    const service = new HttpCommunityService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await expect(service.joinActivity(5, "v1")).rejects.toMatchObject({
      name: "ApiError",
      message: problem.detail,
      problem,
    });
  });

  it("reports a clear setup message when the backend cannot be reached", async () => {
    const fetcher = vi.fn(async () => { throw new TypeError("Failed to fetch"); });
    const service = new HttpCommunityService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await expect(service.getOrganisations()).rejects.toThrow(
      "Backend API could not be reached at http://localhost:5066. Start ReGreen.Api and verify the frontend API URL.",
    );
  });
});
