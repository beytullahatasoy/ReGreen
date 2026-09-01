/**
 * Organisation ve Community ekranlarının API tipleri — backend'deki
 * ReGreen.Api/Dtos/CommunityDtos.cs ile birebir.
 *
 * Sözlük değerleri (kind, status) API'de makine okunur İngilizce gelir
 * ("erosion_observation", "needs_clarification"); ekranda gösterilecek metin
 * frontend'in işidir. Hüküm katmanının Türkçe kodlarıyla (EROZYON_ONCE)
 * karıştırılmamalı: onlar model çıktısı, bunlar uygulama durumu.
 */

export interface Organisation {
  id: number;
  name: string;
  contact_email: string | null;
  verified: boolean;
}

export interface Volunteer {
  id: string;
  alias: string;
  created_at: string;
}

export type ActivityKind =
  | "planting"
  | "cleanup"
  | "erosion_observation"
  | "vegetation_monitoring"
  | "field_assessment";

export type ActivityStatus = "open" | "scheduled" | "closed" | "completed";

export interface FieldActivity {
  id: number;
  fire_id: string;
  province: string;
  organisation_id: number;
  organisation: string;
  organisation_verified: boolean;
  kind: ActivityKind;
  title: string;
  description: string;
  /** ISO tarih (YYYY-MM-DD). */
  scheduled_for: string;
  meeting_point: string;
  capacity: number;
  /** Katılımcı satırlarından sayılır — ayrı bir sayaç kolonu yok. */
  joined: number;
  requirements: string[];
  status: ActivityStatus;
  observation_count: number;
}

export interface CreateActivityInput {
  fire_id: string;
  kind: ActivityKind;
  title: string;
  description: string;
  scheduled_for: string;
  meeting_point: string;
  capacity: number;
  requirements: string[];
}

export type ObservationStatus =
  | "pending"
  | "accepted"
  | "needs_clarification"
  | "rejected";

export interface FieldObservation {
  id: number;
  fire_id: string;
  province: string;
  activity_id: number | null;
  activity_title: string | null;
  volunteer_id: string;
  volunteer_alias: string;
  location: string;
  photo_name: string | null;
  answers: string[];
  note: string | null;
  status: ObservationStatus;
  review_note: string | null;
  reviewed_by: string | null;
  submitted_at: string;
  reviewed_at: string | null;
}

export interface CreateObservationInput {
  volunteer_id: string;
  activity_id: number | null;
  location: string;
  photo_name: string | null;
  answers: string[];
  note?: string;
}

export interface ActivityQuery {
  fire_id?: string;
  status?: ActivityStatus;
  limit?: number;
}

export interface ObservationQuery {
  fire_id?: string;
  status?: ObservationStatus;
  volunteer_id?: string;
  limit?: number;
}
