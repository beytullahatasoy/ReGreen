namespace ReGreen.Data.Entities;

/// <summary>
/// Bir hücrenin hükmü (verdict) — "gidince ne yapılacak" sorusunun kural motoru cevabı.
/// Öncelik ağırlıklarından bağımsızdır; ancak recovery_gap_pred kullandığı için ModelRun'a
/// bağlıdır. Aynı hücre yeni bir model teslimatında farklı bir hüküm alabilir.
/// </summary>
public class CellVerdict
{
    public string CellId { get; set; } = null!;
    public string FireId { get; set; } = null!;
    public Cell Cell { get; set; } = null!;
    public int ModelRunId { get; set; }
    public ModelRun ModelRun { get; set; } = null!;
    public string HukumSozluguSurum { get; set; } = null!;
    public HukumSozlugu HukumSozlugu { get; set; } = null!;

    public string Hukum { get; set; } = null!;
    public string? EkKosullar { get; set; }
    public double? ToparlanmaOrani { get; set; }
    public string? TurOnerisi { get; set; }
    public string Tetikleyen { get; set; } = null!;
    public string Ozet { get; set; } = null!;
    public string Ayrinti { get; set; } = null!;
    public bool ZamanlamaNotuVar { get; set; }
}
