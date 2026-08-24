using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReGreen.Api.Dtos;

/// <summary>docs/data-contract.md §2/§2.1 — `/api/fires` liste elemanı.</summary>
public record class FireSummaryDto(
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("fire_date")] DateOnly FireDate,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("region")] string Region,
    [property: JsonPropertyName("modis_area_ha")] double ModisAreaHa,
    [property: JsonPropertyName("burned_area_ha")] double BurnedAreaHa,
    [property: JsonPropertyName("cell_count")] int CellCount,
    [property: JsonPropertyName("has_perimeter")] bool HasPerimeter,
    [property: JsonPropertyName("marker_lat")] double MarkerLat,
    [property: JsonPropertyName("marker_lon")] double MarkerLon,
    [property: JsonPropertyName("quality_flag")] string QualityFlag,
    [property: JsonPropertyName("quality_note")] string? QualityNote);

public record class MarkerDto(
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon);

/// <summary>docs/data-contract.md §7.2 — `/api/fires/{id}/perimeter` yanıtı.</summary>
public record class FirePerimeterDto(
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("marker")] MarkerDto Marker,
    [property: JsonPropertyName("perimeter")] JsonElement Perimeter);
