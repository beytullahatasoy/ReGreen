using System.Text.Json.Serialization;
using ReGreen.Core.Priority;

namespace ReGreen.Api.Dtos;

/// <summary>
/// docs/data-contract.md §3/§4/§6 — hücre detayı. Kare geometri BİLEREK yok: kare,
/// merkezden (§7.1 formülü) frontend'de üretilir; backend her ağırlık değişiminde
/// tam geometriyi yeniden döndürmez (PDF v4.1'de bulunan tasarım zaafı, bkz. plan).
/// </summary>
public record class CellDto(
    [property: JsonPropertyName("cell_id")] string CellId,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("tree_cover")] double TreeCover,
    [property: JsonPropertyName("tree_cover_annual")] double? TreeCoverAnnual,
    [property: JsonPropertyName("burn_severity_dnbr")] double BurnSeverityDnbr,
    [property: JsonPropertyName("slope_deg")] double SlopeDeg,
    [property: JsonPropertyName("elevation_m")] double? ElevationM,
    [property: JsonPropertyName("road_distance_km")] double RoadDistanceKm,
    [property: JsonPropertyName("ndvi_before")] double NdviBefore,
    [property: JsonPropertyName("ndvi_after")] double NdviAfter,
    [property: JsonPropertyName("ndvi_drop")] double NdviDrop,
    [property: JsonPropertyName("severity_class")] string SeverityClass,
    [property: JsonPropertyName("land_cover")] string? LandCover,
    [property: JsonPropertyName("prediction_status")] string PredictionStatus,
    [property: JsonPropertyName("recovery_gap_pred")] double? RecoveryGapPred,
    [property: JsonPropertyName("priority_score")] double? PriorityScore,
    [property: JsonPropertyName("priority_class")] string? PriorityClass);

public record class AppliedWeightsDto(
    [property: JsonPropertyName("recovery")] double Recovery,
    [property: JsonPropertyName("erosion")] double Erosion,
    [property: JsonPropertyName("access")] double Access);

/// <summary>
/// API'ye özel, NON-NULLABLE min/max. `ReGreen.Core.Priority.NormRange` bilerek nullable
/// (metadata.json'da oncelik.py'nin teorik "referans yok" durumunu okuyabilmek için) ama
/// FireValidator §3.3.4 artık import zamanında null'ı reddediyor — DB'ye ulaşan her
/// ModelRun'ın normalizasyon aralığı garanti dolu. Wire sözleşmesi bunu yansıtır.
/// </summary>
public record class NormRangeDto(
    [property: JsonPropertyName("min")] double Min,
    [property: JsonPropertyName("max")] double Max);

public record class NormalizationReferenceDto(
    [property: JsonPropertyName("recovery_gap_pred")] NormRangeDto RecoveryGapPred,
    [property: JsonPropertyName("slope_deg")] NormRangeDto SlopeDeg,
    [property: JsonPropertyName("road_distance_km")] NormRangeDto RoadDistanceKm);

/// <summary>
/// `/api/fires/{id}/cells` yanıt zarfı. `count` = `items.Length` (bu sürümde
/// pagination yok — ileride eklenirse ayrı bir `total_count` alanı gelir).
/// </summary>
public record class CellsResponseDto(
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("model_run_id")] int ModelRunId,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("crs")] string Crs,
    [property: JsonPropertyName("cell_size_m")] int CellSizeM,
    [property: JsonPropertyName("applied_weights")] AppliedWeightsDto AppliedWeights,
    [property: JsonPropertyName("normalization_reference")] NormalizationReferenceDto NormalizationReference,
    [property: JsonPropertyName("priority_thresholds")] PriorityThresholds PriorityThresholds,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] IReadOnlyList<CellDto> Items);
