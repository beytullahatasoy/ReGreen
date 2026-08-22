using NetTopologySuite.Geometries;

namespace ImportTool.Geo;

/// <summary>{fire_id}_sinir.geojson'dan okunan tek Feature — docs/data-contract.md §7.2.</summary>
public class FirePerimeter
{
    public required string FireId { get; init; }
    public required DateOnly FireDate { get; init; }
    public required string Province { get; init; }
    public required string Region { get; init; }
    public required double ModisAreaHa { get; init; }
    public required Geometry Geometry { get; init; }
    public required string RawGeoJson { get; init; }

    /// <summary>
    /// Marker: centroid DEĞİL, InteriorPoint — çok parçalı şekillerde her zaman
    /// geometrinin içinde kalır (data-contract §7.2).
    /// </summary>
    public Point ComputeMarker() => Geometry.InteriorPoint;
}
