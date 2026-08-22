using System.Text.Json.Serialization;

namespace ImportTool.Models;

/// <summary>
/// manifest.json — docs/data-contract.md §9.2. `required` alanlar System.Text.Json'a
/// JSON'da gerçekten mevcut olmalarını zorunlu kılar — eksikse deserialize JsonException
/// fırlatır, sessizce "" / 0 / false'a düşmez (bkz. import-flow.md §3.1.1).
/// </summary>
public record class Manifest
{
    [JsonPropertyName("project")] public required string Project { get; set; }
    [JsonPropertyName("generated_at")] public required DateTimeOffset GeneratedAt { get; set; }
    [JsonPropertyName("model_version")] public required string ModelVersion { get; set; }
    [JsonPropertyName("schema_version")] public required string SchemaVersion { get; set; }
    [JsonPropertyName("cell_size_m")] public required int CellSizeM { get; set; }
    [JsonPropertyName("crs")] public required string Crs { get; set; }
    [JsonPropertyName("fire_count")] public required int FireCount { get; set; }
    [JsonPropertyName("total_cells")] public required int TotalCells { get; set; }
    [JsonPropertyName("training_rows")] public required int TrainingRows { get; set; }
    [JsonPropertyName("training_groups")] public required int TrainingGroups { get; set; }
    [JsonPropertyName("priority_weights")] public required PriorityWeights PriorityWeights { get; set; }
    [JsonPropertyName("priority_thresholds")] public required PriorityThresholds PriorityThresholds { get; set; }
    [JsonPropertyName("files_per_fire")] public required List<string> FilesPerFire { get; set; }
    [JsonPropertyName("fires")] public required List<ManifestFireEntry> Fires { get; set; }
}

public record class ManifestFireEntry
{
    [JsonPropertyName("fire_id")] public required string FireId { get; set; }
    [JsonPropertyName("fire_date")] public required DateOnly FireDate { get; set; }
    [JsonPropertyName("province")] public required string Province { get; set; }
    [JsonPropertyName("region")] public required string Region { get; set; }
    [JsonPropertyName("cell_count")] public required int CellCount { get; set; }
    [JsonPropertyName("burned_area_ha")] public required double BurnedAreaHa { get; set; }
    [JsonPropertyName("quality_flag")] public required string QualityFlag { get; set; }
    [JsonPropertyName("has_perimeter")] public required bool HasPerimeter { get; set; }
    [JsonPropertyName("status_counts")] public required StatusCounts StatusCounts { get; set; }
}

/// <summary>
/// 0 olan durum key olarak hiç yazılmayabilir — data-contract §9.2. Bu yüzden ALT
/// alanlar `required` DEĞİL (StatusCounts nesnesinin kendisi üst seviyede required).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record class StatusCounts
{
    [JsonPropertyName("predicted")] public int Predicted { get; set; }
    [JsonPropertyName("low_severity")] public int LowSeverity { get; set; }
    [JsonPropertyName("no_data")] public int NoData { get; set; }

    public int Total => Predicted + LowSeverity + NoData;
}

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
