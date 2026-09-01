import { describe, expect, it } from "vitest";
import { resolveWorkspaceRoute } from "./routing";

describe("resolveWorkspaceRoute", () => {
  it.each([
    ["/", "expert"],
    ["/unknown", "expert"],
    ["/organisation", "organisation"],
    ["/organisation/", "organisation"],
    ["/community", "community"],
  ] as const)("resolves %s to %s", (pathname, expected) => {
    expect(resolveWorkspaceRoute(pathname)).toBe(expected);
  });

  it("keeps the former prototype URLs working", () => {
    expect(resolveWorkspaceRoute("/prototype/organisation")).toBe("organisation");
    expect(resolveWorkspaceRoute("/prototype/volunteer")).toBe("community");
  });
});
