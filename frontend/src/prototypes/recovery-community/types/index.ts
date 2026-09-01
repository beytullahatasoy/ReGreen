/**
 * Types for the DEMO records on the Organisation and Community screens.
 *
 * RecoveryZoneDemo / ZoneStatus / RecoveryStage were REMOVED: zones are no
 * longer invented. They come from real fire data through
 * src/hooks/useRecoveryZones.ts -> RecoveryZone.
 *
 * The three types below cover records that still have no backend: field
 * activities, volunteer observations and recovery updates. Each one is tied
 * to a REAL fire through `fireId`.
 */

export type ActivityStatus = "Open" | "Scheduled" | "In review";

export type ObservationStatus =
  | "Pending"
  | "Reviewed"
  | "Accepted as supporting evidence"
  | "Needs clarification";

export interface FieldActivityDemo {
  id: string;
  type:
    | "Field Assessment"
    | "Controlled Cleanup"
    | "Soil / Erosion Observation"
    | "Vegetation Monitoring"
    | "Expert-Approved Planting Activity";
  date: string;
  /** Real fire identifier — the `fire_id` from /api/fires. */
  fireId: string;
  organisation: string;
  capacity: number;
  joined: number;
  status: ActivityStatus;
  location: string;
  requirements: string[];
  description: string;
  coordinator: string;
}

export interface RecoveryUpdateDemo {
  fireId: string;
  title: string;
  status: "Completed";
  trend: "Improving" | "Stable" | "Needs further expert review";
}

export interface FieldObservationDemo {
  id: string;
  fireId: string;
  submittedBy: string;
  date: string;
  location: string;
  answers: string[];
  note?: string;
  status: ObservationStatus;
}
