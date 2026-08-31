import { ExpertWorkspace } from "../features/fire-workspace/ExpertWorkspace";
import { RecoveryCommunityPrototype } from "../prototypes/recovery-community";

export function App() {
  if (window.location.pathname.startsWith("/prototype/")) {
    return <RecoveryCommunityPrototype />;
  }

  return <ExpertWorkspace />;
}
