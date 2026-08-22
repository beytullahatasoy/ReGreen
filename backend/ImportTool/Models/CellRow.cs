using CsvHelper.Configuration.Attributes;

namespace ImportTool.Models;

/// <summary>
/// {fire_id}_hucreler.csv satırı — docs/data-contract.md §9.1, 19 sütun, sabit sıra.
/// </summary>
public class CellRow
{
    [Name("fire_id")] public string FireId { get; set; } = "";
    [Name("cell_id")] public string CellId { get; set; } = "";
    [Name("lat")] public double Lat { get; set; }
    [Name("lon")] public double Lon { get; set; }
    [Name("tree_cover")] public double TreeCover { get; set; }
    [Name("tree_cover_annual")] public double? TreeCoverAnnual { get; set; }
    [Name("burn_severity_dnbr")] public double BurnSeverityDnbr { get; set; }
    [Name("slope_deg")] public double SlopeDeg { get; set; }
    [Name("elevation_m")] public double? ElevationM { get; set; }
    [Name("road_distance_km")] public double RoadDistanceKm { get; set; }
    [Name("ndvi_before")] public double NdviBefore { get; set; }
    [Name("ndvi_after")] public double NdviAfter { get; set; }
    [Name("ndvi_drop")] public double NdviDrop { get; set; }
    [Name("severity_class")] public string SeverityClass { get; set; } = "";
    [Name("land_cover")] public string? LandCover { get; set; }
    [Name("prediction_status")] public string PredictionStatus { get; set; } = "";
    [Name("recovery_gap_pred")] public double? RecoveryGapPred { get; set; }
    [Name("priority_score")] public double? PriorityScore { get; set; }
    [Name("priority_class")] public string? PriorityClass { get; set; }

    /// <summary>Kaynak dosyadaki 1 tabanlı satır no (header hariç) — hata mesajları için.</summary>
    [Ignore]
    public int SourceLineNumber { get; set; }

    public static readonly string[] ExpectedHeader =
    [
        "fire_id", "cell_id", "lat", "lon", "tree_cover", "tree_cover_annual",
        "burn_severity_dnbr", "slope_deg", "elevation_m", "road_distance_km",
        "ndvi_before", "ndvi_after", "ndvi_drop", "severity_class", "land_cover",
        "prediction_status", "recovery_gap_pred", "priority_score", "priority_class"
    ];
}
