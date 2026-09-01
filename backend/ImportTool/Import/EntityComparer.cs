using ImportTool.Geo;
using ReGreen.Core.Priority;
using ImportTool.Validation;
using ReGreen.Data.Entities;

namespace ImportTool.Import;

/// <summary>
/// docs/import-flow.md §4: kör upsert YOK. Mevcut kayıt varsa sabit alanlar karşılaştırılır,
/// fark varsa yangın reddedilir (insert-or-verify).
/// </summary>
public static class EntityComparer
{
    public static List<string> CompareFire(Fire existing, FireImportData incoming, double markerLat, double markerLon)
    {
        var diffs = new List<string>();
        var meta = incoming.Metadata;

        if (existing.FireDate != DateOnly.FromDateTime(meta.FireDate.ToDateTime(TimeOnly.MinValue)))
            diffs.Add($"FireDate: {existing.FireDate} != {meta.FireDate}");
        if (existing.Province != meta.Province) diffs.Add($"Province: {existing.Province} != {meta.Province}");
        if (existing.Region != meta.Region) diffs.Add($"Region: {existing.Region} != {meta.Region}");
        if (!PriorityCalculator.ApproximatelyEqual(existing.ModisAreaHa, meta.ModisAreaHa))
            diffs.Add($"ModisAreaHa: {existing.ModisAreaHa} != {meta.ModisAreaHa}");
        if (!PriorityCalculator.ApproximatelyEqual(existing.BurnedAreaHa, meta.BurnedAreaHa))
            diffs.Add($"BurnedAreaHa: {existing.BurnedAreaHa} != {meta.BurnedAreaHa}");
        if (existing.CellSizeM != meta.CellSizeM) diffs.Add($"CellSizeM: {existing.CellSizeM} != {meta.CellSizeM}");
        if (existing.HasPerimeter != meta.HasPerimeter) diffs.Add("HasPerimeter farklı");
        if (existing.QualityFlag != meta.QualityFlag) diffs.Add($"QualityFlag: {existing.QualityFlag} != {meta.QualityFlag}");
        if (existing.QualityNote != meta.QualityNote) diffs.Add("QualityNote farklı");
        if (!PriorityCalculator.ApproximatelyEqual(existing.MarkerLat, markerLat, 1e-9))
            diffs.Add($"MarkerLat: {existing.MarkerLat} != {markerLat}");
        if (!PriorityCalculator.ApproximatelyEqual(existing.MarkerLon, markerLon, 1e-9))
            diffs.Add($"MarkerLon: {existing.MarkerLon} != {markerLon}");
        // Ham JSON metni DEĞİL, normalize edilmiş koordinatlar karşılaştırılır —
        // aksi halde aynı geometri sadece whitespace/property sırası değişince
        // yanlışlıkla reddedilir (bkz. import-flow.md §4: "1e-9 derece toleransı").
        var existingGeometry = GeoJsonFireReader.ParseGeometry(existing.PerimeterGeoJson);
        if (!GeometriesEqual(existingGeometry, incoming.Perimeter.Geometry, 1e-9))
            diffs.Add("PerimeterGeoJson geometrisi farklı");

        return diffs;
    }

    internal static bool GeometriesEqual(
        NetTopologySuite.Geometries.Geometry existingGeometry,
        NetTopologySuite.Geometries.Geometry incomingGeometry,
        double tolerance)
    {
        existingGeometry = existingGeometry.Copy();
        incomingGeometry = incomingGeometry.Copy();
        existingGeometry.Normalize();
        incomingGeometry.Normalize();
        return existingGeometry.EqualsExact(incomingGeometry, tolerance);
    }

    public static List<string> CompareCell(Cell existing, Models.CellRow row)
    {
        var diffs = new List<string>();

        void CheckDouble(string name, double a, double b)
        {
            if (!PriorityCalculator.ApproximatelyEqual(a, b)) diffs.Add($"{name}: {a} != {b}");
        }

        void CheckNullableDouble(string name, double? a, double? b)
        {
            if (a is null && b is null) return;
            if (a is null || b is null || !PriorityCalculator.ApproximatelyEqual(a.Value, b.Value))
                diffs.Add($"{name}: {a} != {b}");
        }

        CheckDouble(nameof(Cell.CenterLat), existing.CenterLat, row.Lat);
        CheckDouble(nameof(Cell.CenterLon), existing.CenterLon, row.Lon);
        CheckDouble(nameof(Cell.TreeCover), existing.TreeCover, row.TreeCover);
        CheckNullableDouble(nameof(Cell.TreeCoverAnnual), existing.TreeCoverAnnual, row.TreeCoverAnnual);
        CheckDouble(nameof(Cell.BurnSeverityDnbr), existing.BurnSeverityDnbr, row.BurnSeverityDnbr);
        CheckDouble(nameof(Cell.SlopeDeg), existing.SlopeDeg, row.SlopeDeg);
        CheckNullableDouble(nameof(Cell.ElevationM), existing.ElevationM, row.ElevationM);
        CheckDouble(nameof(Cell.RoadDistanceKm), existing.RoadDistanceKm, row.RoadDistanceKm);
        CheckDouble(nameof(Cell.NdviBefore), existing.NdviBefore, row.NdviBefore);
        CheckDouble(nameof(Cell.NdviAfter), existing.NdviAfter, row.NdviAfter);
        CheckDouble(nameof(Cell.NdviDrop), existing.NdviDrop, row.NdviDrop);
        if (existing.SeverityClass != row.SeverityClass) diffs.Add($"SeverityClass: {existing.SeverityClass} != {row.SeverityClass}");
        if (existing.LandCover != row.LandCover) diffs.Add($"LandCover: {existing.LandCover} != {row.LandCover}");

        return diffs;
    }

    public static List<string> CompareCellVerdict(
        CellVerdict existing, Models.HukumRow row, string hukumSozluguSurum)
    {
        var diffs = new List<string>();

        void CheckNullableDouble(string name, double? a, double? b)
        {
            if (a is null && b is null) return;
            if (a is null || b is null || !PriorityCalculator.ApproximatelyEqual(a.Value, b.Value))
                diffs.Add($"{name}: {a} != {b}");
        }

        if (existing.Hukum != row.Hukum) diffs.Add($"Hukum: {existing.Hukum} != {row.Hukum}");
        if (existing.HukumSozluguSurum != hukumSozluguSurum)
            diffs.Add($"HukumSozluguSurum: {existing.HukumSozluguSurum} != {hukumSozluguSurum}");
        if (existing.EkKosullar != row.EkKosullar) diffs.Add($"EkKosullar: {existing.EkKosullar} != {row.EkKosullar}");
        CheckNullableDouble(nameof(CellVerdict.ToparlanmaOrani), existing.ToparlanmaOrani, row.ToparlanmaOrani);
        if (existing.TurOnerisi != row.TurOnerisi) diffs.Add($"TurOnerisi: {existing.TurOnerisi} != {row.TurOnerisi}");
        if (existing.Tetikleyen != row.Tetikleyen) diffs.Add($"Tetikleyen: {existing.Tetikleyen} != {row.Tetikleyen}");
        if (existing.Ozet != row.Ozet) diffs.Add($"Ozet: {existing.Ozet} != {row.Ozet}");
        if (existing.Ayrinti != row.Ayrinti) diffs.Add($"Ayrinti: {existing.Ayrinti} != {row.Ayrinti}");
        if (existing.ZamanlamaNotuVar != row.ZamanlamaNotuVar)
            diffs.Add($"ZamanlamaNotuVar: {existing.ZamanlamaNotuVar} != {row.ZamanlamaNotuVar}");

        return diffs;
    }

    public static List<string> CompareFireNarrative(FireNarrative existing, Validation.FireNarrativeInput incoming)
    {
        var diffs = new List<string>();

        if (existing.Paragraf != incoming.Paragraf) diffs.Add("Paragraf farklı");
        if (existing.Profil != incoming.Profil) diffs.Add($"Profil: {existing.Profil} != {incoming.Profil}");
        if (existing.Onaylandi != incoming.Onaylandi) diffs.Add($"Onaylandi: {existing.Onaylandi} != {incoming.Onaylandi}");
        if (existing.Uretim != incoming.Uretim) diffs.Add($"Uretim: {existing.Uretim} != {incoming.Uretim}");
        if (existing.NarrativeVersion != incoming.NarrativeVersion)
            diffs.Add($"NarrativeVersion: {existing.NarrativeVersion} != {incoming.NarrativeVersion}");
        if (existing.SayiBlogu != incoming.SayiBlogu) diffs.Add("SayiBlogu farklı");

        return diffs;
    }
}
