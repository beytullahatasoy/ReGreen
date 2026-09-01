namespace ReGreen.Data.Entities;

/// <summary>
/// Gönüllünün bir etkinliğe katılımı. Bileşik anahtar (ActivityId, VolunteerId)
/// aynı gönüllünün aynı etkinliğe iki kez yazılmasını DB seviyesinde engeller —
/// endpoint'teki kontrol ağ tekrarına (double-submit) karşı tek başına yetmez.
/// </summary>
public class ActivityParticipant
{
    public int ActivityId { get; set; }
    public FieldActivity Activity { get; set; } = null!;

    public Guid VolunteerId { get; set; }
    public Volunteer Volunteer { get; set; } = null!;

    public DateTimeOffset JoinedAt { get; set; }
}
