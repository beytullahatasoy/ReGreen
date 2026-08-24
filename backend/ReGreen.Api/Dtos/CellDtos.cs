using System.Text.Json.Serialization;

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
/// `/api/fires/{id}/cells` yanıt zarfı. `count` = `items.Length` (bu sürümde
/// pagination yok — ileride eklenirse ayrı bir `total_count` alanı gelir).
/// </summary>
public record class CellsResponseDto(
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("model_run_id")] int ModelRunId,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("crs")] string Crs,
    [property: JsonPropertyName("cell_size_m")] int CellSizeM,
    [property: JsonPropertyName("applied_weights")] AppliedWeightsDto AppliedWeights,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] IReadOnlyList<CellDto> Items);
