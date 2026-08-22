namespace ImportTool.Validation;

/// <summary>docs/data-contract.md §4/§6 — 53 yangının tamamı taranarak doğrulanmış gerçek enum'lar.</summary>
public static class ContractEnums
{
    public static readonly HashSet<string> SeverityClasses = ["dusuk", "orta-dusuk", "orta-yuksek", "yuksek"];

    public static readonly HashSet<string> LandCovers =
        ["Agaclik", "Ciplak", "Otlak/calilik", "Su", "Sulak alan", "Tarim", "Yerlesim"];

    public static readonly HashSet<string> PredictionStatuses = ["predicted", "low_severity", "no_data"];

    public static readonly HashSet<string> PriorityClasses = ["COK_YUKSEK", "YUKSEK", "ORTA", "DUSUK"];

    public static readonly HashSet<string> QualityFlags = ["ok", "check"];
}
