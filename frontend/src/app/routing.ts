export type WorkspaceRoute = "expert" | "organisation" | "community";

function normalisePath(pathname: string) {
  const withoutTrailingSlash = pathname.replace(/\/+$/, "");
  return withoutTrailingSlash || "/";
}

export function resolveWorkspaceRoute(pathname: string): WorkspaceRoute {
  const path = normalisePath(pathname);

  if (path === "/organisation" || path === "/prototype/organisation") {
    return "organisation";
  }

  if (path === "/community" || path === "/prototype/volunteer") {
    return "community";
  }

  return "expert";
}
