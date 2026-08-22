namespace ReGreen.Data.Entities;

/// <summary>
/// KATMAN 1 çıktısı. Insert-only, model tekrar çalışınca yeni satır.
/// FireId burada DENORMALİZE saklanır (composite FK ile hücre/model-run'ın aynı
/// yangına ait olmasını DB seviyesinde garanti eder — bkz. docs/db-schema.md §1/§5).
/// </summary>
public class Prediction
{
    public int Id { get; set; }

    public string FireId { get; set; } = null!;
    public string CellId { get; set; } = null!;
    public Cell Cell { get; set; } = null!;
    public int ModelRunId { get; set; }
    public ModelRun ModelRun { get; set; } = null!;

    public string PredictionStatus { get; set; } = null!;
    public double? RecoveryGapPred { get; set; }
    public double? DefaultPriorityScore { get; set; }
    public string? DefaultPriorityClass { get; set; }
}
