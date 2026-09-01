namespace ReGreen.Data.Entities;

/// <summary>
/// Gönüllünün sahada gördüğünü kaydettiği gözlem.
///
/// <b>Kritik kural:</b> gözlem modeli EĞİTMEZ, tahmine girmez, hükmü değiştirmez.
/// Uzmanın kararına destek kanıtıdır; bu yüzden Predictions/CellVerdicts ile
/// hiçbir bağı yoktur ve yalnızca kurum incelemesinden geçerek anlam kazanır.
/// (bkz. docs/topluluk_veri_sozlesmesi.md)
///
/// Fotoğrafın yalnızca ADI saklanır — dosya yükleme ayrı bir iş; kolonun
/// varlığı, yükleme geldiğinde şemanın değişmesine gerek kalmaması içindir.
/// </summary>
public class FieldObservation
{
    public int Id { get; set; }

    public string FireId { get; set; } = null!;
    public Fire Fire { get; set; } = null!;

    /// <summary>Gözlem bir etkinlik sırasında kaydedildiyse dolu.</summary>
    public int? ActivityId { get; set; }
    public FieldActivity? Activity { get; set; }

    public Guid VolunteerId { get; set; }
    public Volunteer Volunteer { get; set; } = null!;

    public string Location { get; set; } = null!;
    public string? PhotoName { get; set; }

    /// <summary>'|' ile ayrılmış "Soru: cevap" çiftleri. En az bir tane olmalı.</summary>
    public string Answers { get; set; } = null!;

    public string? Note { get; set; }

    /// <summary>pending · accepted · needs_clarification · rejected</summary>
    public string Status { get; set; } = null!;

    /// <summary>Kurumun kararına eklediği açıklama — gönüllüye geri döner.</summary>
    public string? ReviewNote { get; set; }

    public int? ReviewedByOrganisationId { get; set; }
    public Organisation? ReviewedByOrganisation { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public static class ObservationStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string NeedsClarification = "needs_clarification";
    public const string Rejected = "rejected";

    public static readonly string[] All = [Pending, Accepted, NeedsClarification, Rejected];
}
