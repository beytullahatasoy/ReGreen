import { ExpertWorkspace } from "../features/fire-workspace/ExpertWorkspace";
import { RecoveryCommunityPrototype } from "../prototypes/recovery-community";
import { resolveWorkspaceRoute } from "./routing";

export function App() {
  const route = resolveWorkspaceRoute(window.location.pathname);

  if (route === "organisation" || route === "community") {
    return <RecoveryCommunityPrototype mode={route === "community" ? "volunteer" : "organisation"} />;
  }

  return <ExpertWorkspace />;
}
