namespace ReGreen.Data.Entities;

/// <summary>
/// Bir kurumun belirli bir yangın alanı için açtığı saha etkinliği.
///
/// Yangına bağlıdır (<see cref="FireId"/>): etkinlik her zaman hüküm katmanının
/// değerlendirdiği gerçek bir alan için açılır, serbest bir konum için değil.
/// Kontenjan kontrolü DB seviyesinde değil endpoint'te yapılır — katılım sayısı
/// <see cref="Participants"/> üzerinden sayılır, ayrı bir sayaç kolonu tutulmaz
/// (sayaç ile satırların ayrışması mümkün olmasın diye).
/// </summary>
public class FieldActivity
{
    public int Id { get; set; }

    public string FireId { get; set; } = null!;
    public Fire Fire { get; set; } = null!;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    /// <summary>Etkinlik türü — <c>ActivityKinds</c> içindeki değerlerden biri.</summary>
    public string Kind { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    public DateOnly ScheduledFor { get; set; }
    public string MeetingPoint { get; set; } = null!;
    public int Capacity { get; set; }

    /// <summary>'|' ile ayrılmış katılım koşulları — <c>EkKosullar</c> ile aynı kalıp.</summary>
    public string? Requirements { get; set; }

    /// <summary>open · scheduled · closed · completed</summary>
    public string Status { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public List<ActivityParticipant> Participants { get; set; } = [];
    public List<FieldObservation> Observations { get; set; } = [];
}

/// <summary>Etkinlik ve gözlem kayıtlarının sözlük değerleri — DB CHECK kısıtlarıyla birebir.</summary>
public static class ActivityKinds
{
    public const string Planting = "planting";
    public const string Cleanup = "cleanup";
    public const string ErosionObservation = "erosion_observation";
    public const string VegetationMonitoring = "vegetation_monitoring";
    public const string FieldAssessment = "field_assessment";

    public static readonly string[] All =
        [Planting, Cleanup, ErosionObservation, VegetationMonitoring, FieldAssessment];
}

public static class ActivityStatuses
{
    public const string Open = "open";
    public const string Scheduled = "scheduled";
    public const string Closed = "closed";
    public const string Completed = "completed";

    public static readonly string[] All = [Open, Scheduled, Closed, Completed];

    /// <summary>Yalnızca bu durumlarda yeni katılım alınır.</summary>
    public static bool AcceptsParticipants(string status) => status is Open or Scheduled;
}
