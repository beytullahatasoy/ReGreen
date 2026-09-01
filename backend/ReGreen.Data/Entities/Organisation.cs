namespace ReGreen.Data.Entities;

/// <summary>
/// Saha etkinliği açan kurum (belediye, OGM birimi, dernek). Gönüllü katkısının
/// "doğrulanmış bir kurum tarafından yürütülüyor" güvencesini taşıyan kayıt.
///
/// Kimlik doğrulama (auth) henüz YOK — Organisation ekranı tek bir kurum olarak
/// çalışır. Oturum katmanı geldiğinde bu tablo olduğu gibi kalır, üzerine yalnızca
/// kullanıcı-kurum eşlemesi eklenir.
/// </summary>
public class Organisation
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ContactEmail { get; set; }

    /// <summary>Etkinlik açma yetkisi doğrulanmış mı.</summary>
    public bool Verified { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<FieldActivity> Activities { get; set; } = [];
}
