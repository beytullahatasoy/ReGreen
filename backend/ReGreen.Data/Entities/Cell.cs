namespace ReGreen.Data.Entities;

/// <summary>
/// Bir hücre. Sabit özellikler (model girdisi + gösterim) burada durur — model tekrar
/// çalışsa bile değişmez. Kaynak: docs/data-contract.md §3/§4, docs/db-schema.md §3.
/// </summary>
public class Cell
{
    public string CellId { get; set; } = null!;
    public string FireId { get; set; } = null!;
    public Fire Fire { get; set; } = null!;

    public double CenterLat { get; set; }
    public double CenterLon { get; set; }

    public double TreeCover { get; set; }
    public double? TreeCoverAnnual { get; set; }
    public double BurnSeverityDnbr { get; set; }
    public double SlopeDeg { get; set; }
    public double? ElevationM { get; set; }
    public double RoadDistanceKm { get; set; }

    public double NdviBefore { get; set; }
    public double NdviAfter { get; set; }
    public double NdviDrop { get; set; }
    public string SeverityClass { get; set; } = null!;
    public string? LandCover { get; set; }

    public List<Prediction> Predictions { get; set; } = [];

    /// <summary>Model teslimatı başına opsiyonel hüküm.</summary>
    public List<CellVerdict> Verdicts { get; set; } = [];
}
