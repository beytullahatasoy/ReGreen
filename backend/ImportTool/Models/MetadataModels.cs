using System.Text.Json.Serialization;

namespace ImportTool.Models;

/// <summary>
/// {fire_id}_metadata.json — docs/data-contract.md §9.3. `required` alanlar eksikse
/// deserialize JsonException fırlatır (bkz. ManifestModels.cs'teki not).
/// </summary>
public record class FireMetadata
{
    [JsonPropertyName("fire_id")] public required string FireId { get; set; }
    [JsonPropertyName("fire_date")] public required DateOnly FireDate { get; set; }
    [JsonPropertyName("province")] public required string Province { get; set; }
    [JsonPropertyName("region")] public required string Region { get; set; }
    [JsonPropertyName("modis_area_ha")] public required double ModisAreaHa { get; set; }
    [JsonPropertyName("cell_size_m")] public required int CellSizeM { get; set; }
    [JsonPropertyName("cell_count")] public required int CellCount { get; set; }
    [JsonPropertyName("burned_area_ha")] public required double BurnedAreaHa { get; set; }
    [JsonPropertyName("crs")] public required string Crs { get; set; }
    [JsonPropertyName("schema_version")] public required string SchemaVersion { get; set; }
    [JsonPropertyName("model_version")] public required string ModelVersion { get; set; }
    [JsonPropertyName("generated_at")] public required DateTimeOffset GeneratedAt { get; set; }
    [JsonPropertyName("quality_flag")] public required string QualityFlag { get; set; }
    // Alan JSON'da zorunlu, değeri null olabilir.
    [JsonPropertyName("quality_note")] public required string? QualityNote { get; set; }
    [JsonPropertyName("in_training_set")] public required bool InTrainingSet { get; set; }
    [JsonPropertyName("out_of_fold_cells")] public required int OutOfFoldCells { get; set; }
    [JsonPropertyName("status_counts")] public required StatusCounts StatusCounts { get; set; }
    [JsonPropertyName("priority_weights")] public required PriorityWeights PriorityWeights { get; set; }
    [JsonPropertyName("priority_thresholds")] public required PriorityThresholds PriorityThresholds { get; set; }
    [JsonPropertyName("normalization_reference")] public required NormalizationReference NormalizationReference { get; set; }
    [JsonPropertyName("has_perimeter")] public required bool HasPerimeter { get; set; }
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
