export type ZoneStatus = "Under Expert Assessment" | "Approved for Field Action" | "Active" | "Monitoring" | "Completed";
export type ActivityStatus = "Open" | "Scheduled" | "In review";
export type RecoveryStage = "Prioritised" | "Expert Reviewed" | "Recovery Zone" | "Field Action" | "Monitoring" | "Recovery Update";
export type ObservationStatus = "Pending" | "Reviewed" | "Accepted as supporting evidence" | "Needs clarification";

export interface RecoveryZoneDemo {
  id: string;
  name: string;
  province: string;
  region: string;
  priority: "High Priority" | "Medium Priority";
  status: ZoneStatus;
  interventionStatus: string;
  organisation: string;
  lastUpdate: string;
  sourceFire: string;
  why: string;
  assessment: string;
  stage: RecoveryStage;
  progress: number;
  nextMilestone: string;
  monitoringStatus: string;
  activityAvailable: boolean;
  priorityReasons: { label: "Recovery" | "Terrain / slope" | "Accessibility"; description: string }[];
  followed?: boolean;
}

export interface FieldActivityDemo {
  id: string;
  type: "Field Assessment" | "Controlled Cleanup" | "Soil / Erosion Observation" | "Vegetation Monitoring" | "Expert-Approved Planting Activity";
  date: string;
  zoneId: string;
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
  zoneId: string;
  title: string;
  status: "Completed";
  trend: "Improving" | "Stable" | "Needs further expert review";
}

export interface FieldObservationDemo {
  id: string;
  zoneId: string;
  submittedBy: string;
  date: string;
  location: string;
  answers: string[];
  note?: string;
  status: ObservationStatus;
}
