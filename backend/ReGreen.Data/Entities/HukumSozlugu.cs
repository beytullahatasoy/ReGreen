namespace ReGreen.Data.Entities;

/// <summary>
/// hukum_sozlugu.json'un ham içeriği — yangına özgü değildir, sürüm başına bir satırdır.
/// Hüküm/ek koşul başlıkları, eşikler ve tür tablosu onay bayrağı burada saklanır.
/// Kaynak: docs/hukum_sozlesmesi.md, docs/db-schema.md §9. `Fires.PerimeterGeoJson`
/// deseniyle aynı şekilde opak JSON blob olarak saklanır, API'de aynen geçirilir.
/// </summary>
public class HukumSozlugu
{
    public string Surum { get; set; } = null!;
    public string JsonIcerik { get; set; } = null!;
    public DateTimeOffset ImportedAt { get; set; }
    public List<CellVerdict> CellVerdicts { get; set; } = [];
}
