import "./recovery-community.css";
import { OrganisationWorkspace } from "./organisation/OrganisationWorkspace";
import { VolunteerWorkspace } from "./volunteer/VolunteerWorkspace";

export function RecoveryCommunityPrototype() {
  return window.location.pathname === "/prototype/volunteer" ? <VolunteerWorkspace /> : <OrganisationWorkspace />;
}
