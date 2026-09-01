import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const createVolunteer = vi.fn();
const getVolunteer = vi.fn();
class MockApiError extends Error {
  constructor(readonly problem: { status: number }) { super("API error"); }
}
vi.mock("../../../services", () => ({
  ApiError: MockApiError,
  communityService: { createVolunteer, getVolunteer },
}));

/** No jsdom in this project's test setup — a minimal in-memory localStorage is enough here. */
class BellekDeposu implements Pick<Storage, "getItem" | "setItem" | "removeItem" | "clear"> {
  private veri = new Map<string, string>();
  getItem(key: string) { return this.veri.get(key) ?? null; }
  setItem(key: string, value: string) { this.veri.set(key, value); }
  removeItem(key: string) { this.veri.delete(key); }
  clear() { this.veri.clear(); }
}

describe("volunteerIdentity", () => {
  let depo: BellekDeposu;

  beforeEach(() => {
    depo = new BellekDeposu();
    vi.stubGlobal("window", { localStorage: depo });
    createVolunteer.mockReset();
    getVolunteer.mockReset();
    vi.resetModules();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("registers once and remembers the identity across calls", async () => {
    createVolunteer.mockResolvedValue({ id: "v-1", alias: "Volunteer V-1", created_at: "2026-01-01T00:00:00Z" });
    const { ensureVolunteer, getCachedVolunteerId } = await import("./volunteerIdentity");

    expect(getCachedVolunteerId()).toBeNull();
    const first = await ensureVolunteer();
    const second = await ensureVolunteer();

    expect(first).toEqual(second);
    expect(createVolunteer).toHaveBeenCalledOnce();
    expect(getCachedVolunteerId()).toBe("v-1");
  });

  it("does not fire a second registration while the first is still in flight", async () => {
    let resolveFirst!: (v: { id: string; alias: string; created_at: string }) => void;
    createVolunteer.mockReturnValue(new Promise((resolve) => { resolveFirst = resolve; }));
    const { ensureVolunteer } = await import("./volunteerIdentity");

    const a = ensureVolunteer();
    const b = ensureVolunteer();
    resolveFirst({ id: "v-2", alias: "Volunteer V-2", created_at: "2026-01-01T00:00:00Z" });

    await expect(a).resolves.toEqual(await b);
    expect(createVolunteer).toHaveBeenCalledOnce();
  });

  it("validates and reuses the identity already stored in localStorage", async () => {
    const existing = { id: "v-existing", alias: "Volunteer V-existing", created_at: "2026-01-01T00:00:00Z" };
    depo.setItem(
      "regreen.volunteer.v1",
      JSON.stringify(existing),
    );
    getVolunteer.mockResolvedValue(existing);
    const { ensureVolunteer, getCachedVolunteerId } = await import("./volunteerIdentity");

    expect(getCachedVolunteerId()).toBe("v-existing");
    await expect(ensureVolunteer()).resolves.toMatchObject({ id: "v-existing" });
    expect(getVolunteer).toHaveBeenCalledWith("v-existing");
    expect(createVolunteer).not.toHaveBeenCalled();
  });

  it("replaces a stale stored identity when the backend returns 404", async () => {
    depo.setItem(
      "regreen.volunteer.v1",
      JSON.stringify({ id: "v-stale", alias: "Old", created_at: "2026-01-01T00:00:00Z" }),
    );
    getVolunteer.mockRejectedValue(new MockApiError({ status: 404 }));
    createVolunteer.mockResolvedValue({ id: "v-new", alias: "New", created_at: "2026-09-01T00:00:00Z" });
    const { ensureVolunteer, getCachedVolunteerId } = await import("./volunteerIdentity");

    await expect(ensureVolunteer()).resolves.toMatchObject({ id: "v-new" });
    expect(createVolunteer).toHaveBeenCalledOnce();
    expect(getCachedVolunteerId()).toBe("v-new");
  });

  it("does not create a duplicate volunteer for a transient validation failure", async () => {
    depo.setItem(
      "regreen.volunteer.v1",
      JSON.stringify({ id: "v-existing", alias: "Existing", created_at: "2026-01-01T00:00:00Z" }),
    );
    getVolunteer.mockRejectedValue(new Error("network unavailable"));
    const { ensureVolunteer } = await import("./volunteerIdentity");

    await expect(ensureVolunteer()).rejects.toThrow("network unavailable");
    expect(createVolunteer).not.toHaveBeenCalled();
  });
});
