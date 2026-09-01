import type { FieldActivityDemo, FieldObservationDemo, RecoveryUpdateDemo } from "../types";

/**
 * ONLY activities, observations and the recovery update are demo data.
 *
 * The invented "Recovery Zone" list was REMOVED (zone-07 "Muğla — Zone 07"
 * and friends). Zones now come from the 53 real fires through
 * useRecoveryZones(). The records below are keyed to real fire identifiers so
 * no fabricated zone name ever reaches the screen.
 *
 * There is no backend for these three lists; the screens label them as demo.
 * Endpoint contract: docs/topluluk_veri_sozlesmesi.md
 */

export const activities: FieldActivityDemo[] = [
  {
    id: "activity-1",
    type: "Expert-Approved Planting Activity",
    date: "17 October",
    fireId: "EGE_2021_02",
    organisation: "Verified Organisation",
    capacity: 45,
    joined: 31,
    status: "Open",
    location: "Muğla · designated meeting point",
    requirements: ["Outdoor clothing", "Minimum age 18", "Safety briefing attendance"],
    description: "A planting activity approved for this area after expert and organisation review.",
    coordinator: "Derya Kaya · Field Coordinator",
  },
  {
    id: "activity-2",
    type: "Soil / Erosion Observation",
    date: "20 October",
    fireId: "EGE_2021_02",
    organisation: "Verified Organisation",
    capacity: 16,
    joined: 9,
    status: "Open",
    location: "Muğla · Observation Point B",
    requirements: ["Camera-enabled phone", "Short field orientation"],
    description: "Record observable ground conditions through a short expert-prepared form.",
    coordinator: "Emre Akın · Observation Lead",
  },
  {
    id: "activity-3",
    type: "Controlled Cleanup",
    date: "24 October",
    fireId: "AKD_2021_01",
    organisation: "Mediterranean Forest Initiative",
    capacity: 30,
    joined: 22,
    status: "Scheduled",
    location: "Antalya · Manavgat access point",
    requirements: ["Closed shoes", "Safety briefing attendance"],
    description: "Join a verified organisation's supervised post-fire cleanup activity.",
    coordinator: "Selin Aras · Activity Coordinator",
  },
];

export const recoveryUpdate: RecoveryUpdateDemo = {
  fireId: "EGE_2024_02",
  title: "Annual recovery assessment",
  status: "Completed",
  trend: "Improving",
};

export const observations: FieldObservationDemo[] = [
  {
    id: "obs-01",
    fireId: "EGE_2021_02",
    submittedBy: "Volunteer V-014",
    date: "13 Oct",
    location: "Observation Point B · geotagged",
    answers: ["Vegetation: sparse", "Erosion signs: possible", "Ground: dry"],
    note: "Loose surface material visible near the marked point.",
    status: "Pending",
  },
  {
    id: "obs-02",
    fireId: "AKD_2021_01",
    submittedBy: "Volunteer V-008",
    date: "11 Oct",
    location: "Northern access point · geotagged",
    answers: ["Vegetation: mixed", "Access issue: none observed"],
    status: "Needs clarification",
  },
  {
    id: "obs-03",
    fireId: "EGE_2024_02",
    submittedBy: "Volunteer V-021",
    date: "6 Oct",
    location: "Monitoring Point A · geotagged",
    answers: ["Vegetation: mixed", "Erosion signs: not observed"],
    status: "Accepted as supporting evidence",
  },
];
