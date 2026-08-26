import { afterEach, describe, expect, it, vi } from "vitest";
import { HttpFireService } from "./HttpFireService";

const jsonResponse = (body: unknown, status = 200) => new Response(JSON.stringify(body), {
  status,
  headers: { "Content-Type": "application/json" },
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("HttpFireService", () => {
  it("calls the default fetch function without binding it to the service instance", async () => {
    const contextSensitiveFetch = vi.fn(function (this: unknown) {
      if (this !== undefined) throw new TypeError("Illegal invocation");
      return Promise.resolve(jsonResponse([]));
    });
    vi.stubGlobal("fetch", contextSensitiveFetch);

    const service = new HttpFireService("http://localhost:5066/");

    await expect(service.getFires()).resolves.toEqual([]);
    expect(contextSensitiveFetch).toHaveBeenCalledOnce();
  });

  it("serializes cell filters, weights and bounding box parameters", async () => {
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse({ items: [] }));
    const service = new HttpFireService("http://localhost:5066/", fetcher as unknown as typeof fetch);

    await service.getCells("AKD 2021/01", {
      prediction_status: "predicted",
      priority_class: "YUKSEK",
      weights: { recovery: 0.4, erosion: 0.35, access: 0.25 },
      bbox: { min_lon: 30.1, min_lat: 36.2, max_lon: 30.9, max_lat: 36.8 },
    });

    const requestedUrl = String(fetcher.mock.calls[0]?.[0]);
    const url = new URL(requestedUrl);
    expect(url.pathname).toBe("/api/fires/AKD%202021%2F01/cells");
    expect(Object.fromEntries(url.searchParams)).toEqual({
      prediction_status: "predicted",
      priority_class: "YUKSEK",
      recovery: "0.4",
      erosion: "0.35",
      access: "0.25",
      min_lon: "30.1",
      min_lat: "36.2",
      max_lon: "30.9",
      max_lat: "36.8",
    });
  });

  it("converts a problem response into ApiError", async () => {
    const problem = {
      type: "https://regreen/errors/fire-not-found",
      title: "Fire not found",
      status: 404,
      code: "FIRE_NOT_FOUND" as const,
      detail: "The requested fire does not exist.",
    };
    const fetcher = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => jsonResponse(problem, 404));
    const service = new HttpFireService("http://localhost:5066", fetcher as unknown as typeof fetch);

    await expect(service.getPerimeter("missing")).rejects.toMatchObject({
      name: "ApiError",
      message: problem.detail,
      problem,
    });
  });
});
