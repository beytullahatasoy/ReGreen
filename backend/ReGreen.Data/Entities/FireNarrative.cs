namespace ReGreen.Data.Entities;

/// <summary>
/// Bir yangının Katman 1 anlatısı — bölge seçildiğinde okunacak tek paragraf + onu
/// üreten sayı bloğu. Anlatı model çıktılarından üretildiği için ModelRun başına bir satırdır.
/// yangın özeti"), docs/db-schema.md §9. `SayiBlogu`, yangın_ozetleri.json'daki
/// `yanginlar[fire_id]` girdisinin ham JSON'udur — `Fires.PerimeterGeoJson`'daki gibi
/// opak blob olarak saklanır, API üzerinden JsonElement olarak aynen geçirilir.
/// </summary>
public class FireNarrative
{
    public string FireId { get; set; } = null!;
    public Fire Fire { get; set; } = null!;
    public int ModelRunId { get; set; }
    public ModelRun ModelRun { get; set; } = null!;
    public string NarrativeVersion { get; set; } = null!;

    public string Paragraf { get; set; } = null!;
    public string Profil { get; set; } = null!;
    public bool Onaylandi { get; set; }
    public string Uretim { get; set; } = null!;
    public string SayiBlogu { get; set; } = null!;
}
