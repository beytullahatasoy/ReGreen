import type {
  ActivityQuery,
  CreateActivityInput,
  CreateObservationInput,
  FieldActivity,
  FieldObservation,
  Organisation,
  ObservationQuery,
  ObservationStatus,
  Volunteer,
} from "../types/community";

export interface UpdateActivityInput {
  status?: FieldActivity["status"];
  capacity?: number;
}

export interface ReviewObservationInput {
  status: ObservationStatus;
  review_note?: string;
  organisation_id?: number;
}

/** Organisation/Community screens — docs/topluluk_veri_sozlesmesi.md, backend/ReGreen.Api/Endpoints/CommunityEndpoints.cs */
export interface CommunityService {
  getOrganisations(): Promise<Organisation[]>;

  createVolunteer(alias?: string): Promise<Volunteer>;
  getVolunteer(id: string): Promise<Volunteer>;

  getActivities(query?: ActivityQuery): Promise<FieldActivity[]>;
  createActivity(input: CreateActivityInput & { organisation_id?: number }): Promise<FieldActivity>;
  updateActivity(id: number, input: UpdateActivityInput): Promise<FieldActivity>;
  joinActivity(id: number, volunteerId: string): Promise<FieldActivity>;
  leaveActivity(id: number, volunteerId: string): Promise<FieldActivity>;

  getObservations(query?: ObservationQuery): Promise<FieldObservation[]>;
  createObservation(fireId: string, input: CreateObservationInput): Promise<FieldObservation>;
  reviewObservation(id: number, input: ReviewObservationInput): Promise<FieldObservation>;
}
