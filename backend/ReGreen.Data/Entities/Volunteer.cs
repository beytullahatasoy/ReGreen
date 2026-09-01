namespace ReGreen.Data.Entities;

/// <summary>
/// Saha gönüllüsü. Parola YOK, e-posta doğrulaması YOK: kayıt ilk katılımda
/// sunucu tarafında üretilir ve kimliği (<see cref="Id"/>) tarayıcıda saklanır.
///
/// Bunun sebebi bilinçli: katılım ve gözlem kayıtlarının kime ait olduğunu
/// bilmek için hesap sistemi kurmak gerekmiyor. Gerçek oturum katmanı geldiğinde
/// bu tabloya yalnızca bir kullanıcı bağlantısı eklenir; katılım ve gözlem
/// kayıtları taşınmadan çalışmaya devam eder.
/// </summary>
public class Volunteer
{
    public Guid Id { get; set; }

    /// <summary>Ekranda görünen ad. Kullanıcı verir; verilmezse üretilir.</summary>
    public string Alias { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public List<ActivityParticipant> Participations { get; set; } = [];
    public List<FieldObservation> Observations { get; set; } = [];
}
