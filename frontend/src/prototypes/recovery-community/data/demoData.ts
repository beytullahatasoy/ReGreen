import type { FieldActivityDemo, FieldObservationDemo, RecoveryUpdateDemo, RecoveryZoneDemo } from "../types";

export const zones: RecoveryZoneDemo[] = [
  {
    id: "zone-07", name: "Muğla — Zone 07", province: "Muğla", region: "Aegean Region", priority: "High Priority",
    status: "Under Expert Assessment", interventionStatus: "Preparing for Field Action", organisation: "Verified Organisation",
    lastUpdate: "14 Oct, 09:30", sourceFire: "ReGreen fire record · Muğla",
    why: "High recovery attention is indicated by the current ReGreen priority assessment. Field action still requires expert and verified organisation review.",
    assessment: "Technical assessment in progress. No field intervention has been automatically approved.", followed: true,
    stage: "Expert Reviewed", progress: 32, nextMilestone: "Verified organisation field-readiness review", monitoringStatus: "Baseline recorded · monitoring not started", activityAvailable: true,
    priorityReasons: [{ label: "Recovery", description: "Expected recovery remains below the reference condition." }, { label: "Terrain / slope", description: "Terrain increases relative erosion attention." }, { label: "Accessibility", description: "Road access supports a feasible expert field check." }],
  },
  {
    id: "zone-12", name: "Antalya — Zone 12", province: "Antalya", region: "Mediterranean Region", priority: "High Priority",
    status: "Approved for Field Action", interventionStatus: "Activity planning", organisation: "Mediterranean Forest Initiative",
    lastUpdate: "12 Oct, 16:10", sourceFire: "ReGreen fire record · Antalya",
    why: "The area was prioritised for expert review and has since been approved for a coordinated field response.",
    assessment: "Verified organisation review complete; activity planning may proceed.",
    stage: "Field Action", progress: 58, nextMilestone: "Complete scheduled field assessment", monitoringStatus: "Post-action monitoring window scheduled", activityAvailable: true,
    priorityReasons: [{ label: "Recovery", description: "Recovery need remains the main priority contributor." }, { label: "Terrain / slope", description: "Slope conditions warrant structured field observation." }, { label: "Accessibility", description: "Access conditions support coordinated activity." }],
  },
  {
    id: "zone-03", name: "İzmir — Zone 03", province: "İzmir", region: "Aegean Region", priority: "Medium Priority",
    status: "Monitoring", interventionStatus: "Post-action monitoring", organisation: "Aegean Ecology Network",
    lastUpdate: "08 Oct, 11:45", sourceFire: "ReGreen fire record · İzmir",
    why: "This zone remains in the monitoring workflow following an expert-reviewed field action.",
    assessment: "Monitoring window active; supporting observations are being reviewed.", followed: true,
    stage: "Monitoring", progress: 78, nextMilestone: "Comparable-season satellite reassessment", monitoringStatus: "12-month reassessment in preparation", activityAvailable: false,
    priorityReasons: [{ label: "Recovery", description: "The zone remains in the recovery monitoring workflow." }, { label: "Terrain / slope", description: "Terrain is considered during observation review." }, { label: "Accessibility", description: "Access information supports repeat monitoring visits." }],
  },
];

export const activities: FieldActivityDemo[] = [
  { id: "activity-1", type: "Expert-Approved Planting Activity", date: "October 17", zoneId: "zone-07", organisation: "Verified Organisation", capacity: 45, joined: 31, status: "Open", location: "Muğla · designated meeting point", requirements: ["Outdoor clothing", "Minimum age 18", "Safety briefing attendance"], description: "Support a planting activity approved for this zone after expert and organisation review.", coordinator: "Derya Kaya · Field Coordinator" },
  { id: "activity-2", type: "Soil / Erosion Observation", date: "October 20", zoneId: "zone-07", organisation: "Verified Organisation", capacity: 16, joined: 9, status: "Open", location: "Muğla · Observation Point B", requirements: ["Camera-enabled phone", "Short field orientation"], description: "Record observable ground conditions through an expert-prepared form.", coordinator: "Emre Akın · Observation Lead" },
  { id: "activity-3", type: "Controlled Cleanup", date: "October 24", zoneId: "zone-12", organisation: "Mediterranean Forest Initiative", capacity: 30, joined: 22, status: "Scheduled", location: "Antalya · Zone 12 access point", requirements: ["Closed shoes", "Safety briefing attendance"], description: "Join a verified organisation’s supervised post-fire cleanup activity.", coordinator: "Selin Aras · Activity Coordinator" },
];

export const recoveryUpdate: RecoveryUpdateDemo = { zoneId: "zone-03", title: "Annual Recovery Assessment", status: "Completed", trend: "Improving" };

export const observations: FieldObservationDemo[] = [
  { id: "obs-01", zoneId: "zone-07", submittedBy: "Volunteer V-014", date: "13 Oct", location: "Observation Point B · geotagged", answers: ["Vegetation visible: sparse", "Signs of erosion: possible", "Ground condition: dry"], note: "Loose surface material visible near the marked point.", status: "Pending" },
  { id: "obs-02", zoneId: "zone-12", submittedBy: "Volunteer V-008", date: "11 Oct", location: "Northern access point · geotagged", answers: ["Vegetation visible: mixed", "Accessibility issue: none observed"], status: "Needs clarification" },
  { id: "obs-03", zoneId: "zone-03", submittedBy: "Volunteer V-021", date: "06 Oct", location: "Monitoring Point A · geotagged", answers: ["Vegetation visible: mixed", "Signs of erosion: not observed"], status: "Accepted as supporting evidence" },
];
