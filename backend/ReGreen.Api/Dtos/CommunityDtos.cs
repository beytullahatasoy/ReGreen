using System.Text.Json.Serialization;

namespace ReGreen.Api.Dtos;

/// <summary>
/// docs/topluluk_veri_sozlesmesi.md — Organisation ve Community ekranlarının veri
/// sözleşmesi. Alan adları diğer DTO'larla aynı kalıpta: snake_case.
///
/// Sözlük değerleri (<c>kind</c>, <c>status</c>) API'de İNGİLİZCE ve makine
/// okunur döner (<c>erosion_observation</c>, <c>needs_clarification</c>); ekranda
/// gösterilecek metin frontend'in işi. Hüküm katmanının Türkçe kodlarından
/// (<c>EROZYON_ONCE</c>) farklı olmalarının sebebi bu: onlar model çıktısı, bunlar
/// uygulama durumu.
/// </summary>
public record class OrganisationDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("contact_email")] string? ContactEmail,
    [property: JsonPropertyName("verified")] bool Verified);

public record class VolunteerDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

/// <summary>Yeni gönüllü kaydı. <c>alias</c> boş bırakılırsa sunucu üretir.</summary>
public record class CreateVolunteerRequest(
    [property: JsonPropertyName("alias")] string? Alias);

/// <summary>
/// Saha etkinliği. <c>joined</c>, katılımcı SATIRLARINDAN sayılır — ayrı bir sayaç
/// kolonu tutulmaz ki sayaç ile gerçek katılım birbirinden ayrışamasın.
/// </summary>
public record class FieldActivityDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("organisation_id")] int OrganisationId,
    [property: JsonPropertyName("organisation")] string Organisation,
    [property: JsonPropertyName("organisation_verified")] bool OrganisationVerified,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("scheduled_for")] DateOnly ScheduledFor,
    [property: JsonPropertyName("meeting_point")] string MeetingPoint,
    [property: JsonPropertyName("capacity")] int Capacity,
    [property: JsonPropertyName("joined")] int Joined,
    [property: JsonPropertyName("requirements")] string[] Requirements,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("observation_count")] int ObservationCount);

public record class CreateActivityRequest(
    [property: JsonPropertyName("fire_id")] string? FireId,
    [property: JsonPropertyName("organisation_id")] int? OrganisationId,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("scheduled_for")] DateOnly? ScheduledFor,
    [property: JsonPropertyName("meeting_point")] string? MeetingPoint,
    [property: JsonPropertyName("capacity")] int? Capacity,
    [property: JsonPropertyName("requirements")] string[]? Requirements);

/// <summary>Yalnızca verilen alanlar güncellenir; <c>null</c> = dokunma.</summary>
public record class UpdateActivityRequest(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("capacity")] int? Capacity);

public record class JoinActivityRequest(
    [property: JsonPropertyName("volunteer_id")] Guid? VolunteerId);

public record class FieldObservationDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("activity_id")] int? ActivityId,
    [property: JsonPropertyName("activity_title")] string? ActivityTitle,
    [property: JsonPropertyName("volunteer_id")] Guid VolunteerId,
    [property: JsonPropertyName("volunteer_alias")] string VolunteerAlias,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("photo_name")] string? PhotoName,
    [property: JsonPropertyName("answers")] string[] Answers,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("review_note")] string? ReviewNote,
    [property: JsonPropertyName("reviewed_by")] string? ReviewedBy,
    [property: JsonPropertyName("submitted_at")] DateTimeOffset SubmittedAt,
    [property: JsonPropertyName("reviewed_at")] DateTimeOffset? ReviewedAt);

public record class CreateObservationRequest(
    [property: JsonPropertyName("volunteer_id")] Guid? VolunteerId,
    [property: JsonPropertyName("activity_id")] int? ActivityId,
    [property: JsonPropertyName("location")] string? Location,
    [property: JsonPropertyName("photo_name")] string? PhotoName,
    [property: JsonPropertyName("answers")] string[]? Answers,
    [property: JsonPropertyName("note")] string? Note);

public record class ReviewObservationRequest(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("review_note")] string? ReviewNote,
    [property: JsonPropertyName("organisation_id")] int? OrganisationId);
