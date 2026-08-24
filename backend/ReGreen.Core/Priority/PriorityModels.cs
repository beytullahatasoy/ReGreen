using System.Text.Json.Serialization;

namespace ReGreen.Core.Priority;

public record class PriorityWeights
{
    [JsonPropertyName("recovery")] public required double Recovery { get; set; }
    [JsonPropertyName("erosion")] public required double Erosion { get; set; }
    [JsonPropertyName("access")] public required double Access { get; set; }
}

public record class PriorityThresholds
{
    [JsonPropertyName("COK_YUKSEK")] public required double CokYuksek { get; set; }
    [JsonPropertyName("YUKSEK")] public required double Yuksek { get; set; }
    [JsonPropertyName("ORTA")] public required double Orta { get; set; }
}

public record class NormalizationReference
{
    [JsonPropertyName("recovery_gap_pred")] public required NormRange RecoveryGapPred { get; set; }
    [JsonPropertyName("slope_deg")] public required NormRange SlopeDeg { get; set; }
    [JsonPropertyName("road_distance_km")] public required NormRange RoadDistanceKm { get; set; }
}

/// <summary>
/// Min/Max BİLEREK nullable fakat JSON'da zorunlu: oncelik.py'nin referans_cikar() fonksiyonu,
/// bir yangında hiç `predicted` hücre yoksa {"min": null, "max": null} üretir (bu 53
/// yangının hiçbirinde görülmedi ama referans uygulama bunu açıkça destekliyor).
/// </summary>
public record class NormRange
{
    [JsonPropertyName("min")] public required double? Min { get; set; }
    [JsonPropertyName("max")] public required double? Max { get; set; }
}
