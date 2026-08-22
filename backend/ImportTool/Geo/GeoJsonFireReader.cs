using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ImportTool.Geo;

/// <summary>
/// {fire_id}_sinir.geojson okuyucu. Kök nesne tek bir Feature'dır (FeatureCollection
/// DEĞİL) — docs/data-contract.md §7.2. Properties tarafı System.Text.Json ile,
/// geometry tarafı NetTopologySuite ile ayrı ayrı okunur.
/// </summary>
public static class GeoJsonFireReader
{
    private static readonly GeoJsonReader NtsReader = new();

    /// <summary>
    /// Bir Feature JSON metninden (dosyadan değil, DB'de saklanan string'den) sadece
    /// geometry'yi ayrıştırır — docs/import-flow.md §4: mevcut kayıtla karşılaştırma
    /// ham metin değil topolojik/koordinat bazlı yapılmalı (bkz. EntityComparer).
    /// </summary>
    public static Geometry ParseGeometry(string featureJson)
    {
        using var doc = JsonDocument.Parse(featureJson);
        var geometryEl = doc.RootElement.GetProperty("geometry");
        return NtsReader.Read<Geometry>(geometryEl.GetRawText());
    }

    public static FirePerimeter Read(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Feature")
            throw new GeoJsonValidationException($"Kök nesne 'Feature' değil: {path}");

        if (!root.TryGetProperty("properties", out var props))
            throw new GeoJsonValidationException($"'properties' alanı yok: {path}");

        if (!root.TryGetProperty("geometry", out var geometryEl))
            throw new GeoJsonValidationException($"'geometry' alanı yok: {path}");

        var geometryType = geometryEl.GetProperty("type").GetString();
        if (geometryType is not ("Polygon" or "MultiPolygon"))
            throw new GeoJsonValidationException(
                $"geometry.type 'Polygon'/'MultiPolygon' değil, '{geometryType}': {path}");

        Geometry geometry;
        try
        {
            geometry = NtsReader.Read<Geometry>(geometryEl.GetRawText());
        }
        catch (Exception ex)
        {
            throw new GeoJsonValidationException($"Geometry parse edilemedi ({path}): {ex.Message}", ex);
        }

        if (geometry.IsEmpty)
            throw new GeoJsonValidationException($"Geometry boş: {path}");
        if (!geometry.IsValid)
            throw new GeoJsonValidationException($"Geometry topolojik olarak geçersiz: {path}");
        if (geometry.Coordinates.Any(c => !double.IsFinite(c.X) || !double.IsFinite(c.Y)
                                          || c.X is < -180 or > 180 || c.Y is < -90 or > 90))
            throw new GeoJsonValidationException(
                $"Geometry EPSG:4326 koordinat sınırları dışında veya NaN/Infinity içeriyor: {path}");

        var fireId = RequireString(props, "fire_id", path);
        var fireDateStr = RequireString(props, "fire_date", path);
        var province = RequireString(props, "province", path);
        var region = RequireString(props, "region", path);
        var modisAreaHa = RequireNumber(props, "modis_area_ha", path);

        // data-contract §7.2: bu 5 alan dışında başka alan yok. Fazlalık FATAL değil ama
        // sessizce yutulmasın diye burada değil, çağıran validator seviyesinde loglanabilir.

        return new FirePerimeter
        {
            FireId = fireId,
            FireDate = DateOnly.Parse(fireDateStr),
            Province = province,
            Region = region,
            ModisAreaHa = modisAreaHa,
            Geometry = geometry,
            RawGeoJson = json,
        };
    }

    /// <summary>properties nesnesindeki bilinen alan adlarını döner — fazlalık/eksik kontrolü için.</summary>
    public static IReadOnlySet<string> ReadPropertyNames(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var props = doc.RootElement.GetProperty("properties");
        return props.EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    private static string RequireString(JsonElement props, string name, string path)
    {
        if (!props.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            throw new GeoJsonValidationException($"'properties.{name}' string olarak bulunamadı: {path}");
        return el.GetString()!;
    }

    private static double RequireNumber(JsonElement props, string name, string path)
    {
        if (!props.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
            throw new GeoJsonValidationException($"'properties.{name}' sayı olarak bulunamadı: {path}");
        var value = el.GetDouble();
        if (!double.IsFinite(value))
            throw new GeoJsonValidationException($"'properties.{name}' NaN/Infinity olamaz: {path}");
        return value;
    }
}

public class GeoJsonValidationException(string message, Exception? inner = null)
    : Exception(message, inner);
