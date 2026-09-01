import "./recovery-community.css";
import { OrganisationWorkspace } from "./organisation/OrganisationWorkspace";
import { VolunteerWorkspace } from "./volunteer/VolunteerWorkspace";

export function RecoveryCommunityPrototype({ mode }: { mode: "organisation" | "volunteer" }) {
  return mode === "volunteer" ? <VolunteerWorkspace /> : <OrganisationWorkspace />;
}
