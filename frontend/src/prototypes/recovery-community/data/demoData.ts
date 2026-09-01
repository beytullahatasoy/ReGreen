import type { RecoveryUpdateDemo } from "../types";

/**
 * ONLY the 12-month recovery update is demo data now — activities and
 * observations come from the real backend (src/types/community.ts,
 * data/useActivities.ts, data/useObservations.ts).
 *
 * The invented "Recovery Zone" list was REMOVED (zone-07 "Muğla — Zone 07"
 * and friends). Zones now come from the 53 real fires through
 * useRecoveryZones(). This record is keyed to a real fire identifier so no
 * fabricated zone name ever reaches the screen.
 */

export const recoveryUpdate: RecoveryUpdateDemo = {
  fireId: "EGE_2024_02",
  title: "Annual recovery assessment",
  status: "Completed",
  trend: "Improving",
};
