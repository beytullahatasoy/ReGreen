/**
 * Types for the DEMO records still on the Organisation and Community screens.
 *
 * RecoveryZoneDemo / ZoneStatus / RecoveryStage were REMOVED: zones are no
 * longer invented. They come from real fire data through
 * src/hooks/useRecoveryZones.ts -> RecoveryZone.
 *
 * Field activities and volunteer observations were ALSO removed from here:
 * both now come from the real backend — see src/types/community.ts and
 * data/useActivities.ts / data/useObservations.ts. Only the 12-month
 * recovery-update card still has no backend.
 */

export interface RecoveryUpdateDemo {
  fireId: string;
  title: string;
  status: "Completed";
  trend: "Improving" | "Stable" | "Needs further expert review";
}
