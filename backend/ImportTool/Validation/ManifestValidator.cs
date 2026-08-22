using ImportTool.Models;

namespace ImportTool.Validation;

/// <summary>docs/import-flow.md §3.1 — manifest düzeyinde FATAL kontroller.</summary>
public static class ManifestValidator
{
    public const string SupportedSchemaVersion = "1.1";

    /// <summary>Hata varsa (kod, mesaj) döner, yoksa null.</summary>
    public static (string Code, string Message)? Validate(Manifest manifest)
    {
        if (manifest is null)
            return ("MANIFEST_NULL", "manifest.json null olamaz.");
        if (string.IsNullOrWhiteSpace(manifest.Project)
            || string.IsNullOrWhiteSpace(manifest.ModelVersion)
            || string.IsNullOrWhiteSpace(manifest.SchemaVersion)
            || string.IsNullOrWhiteSpace(manifest.Crs))
            return ("MANIFEST_REQUIRED_VALUE_NULL",
                "manifest içindeki zorunlu string alanlar null/boş olamaz.");
        if (manifest.PriorityWeights is null || manifest.PriorityThresholds is null
            || manifest.FilesPerFire is null || manifest.Fires is null
            || manifest.Fires.Any(f => f is null))
            return ("MANIFEST_REQUIRED_VALUE_NULL",
                "manifest içindeki zorunlu nesne/liste alanları null olamaz.");

        if (manifest.SchemaVersion != SupportedSchemaVersion)
            return ("UNSUPPORTED_SCHEMA_VERSION",
                $"manifest.schema_version='{manifest.SchemaVersion}' desteklenmiyor (beklenen: '{SupportedSchemaVersion}').");

        var duplicateIds = manifest.Fires
            .GroupBy(f => f.FireId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
            return ("DUPLICATE_FIRE_ID", $"manifest içinde tekrar eden fire_id: {string.Join(", ", duplicateIds)}");

        if (manifest.FireCount != manifest.Fires.Count)
            return ("FIRE_COUNT_MISMATCH",
                $"manifest.fire_count={manifest.FireCount}, fires[] uzunluğu={manifest.Fires.Count}");

        if (manifest.CellSizeM <= 0 || manifest.FireCount < 0 || manifest.TotalCells < 0
            || manifest.TrainingRows < 0 || manifest.TrainingGroups < 0)
            return ("MANIFEST_NUMERIC_VALUE_INVALID", "Manifest sayaçları negatif, cell_size_m ise sıfır/negatif olamaz.");

        var invalidFire = manifest.Fires.FirstOrDefault(f =>
            string.IsNullOrWhiteSpace(f.FireId) || string.IsNullOrWhiteSpace(f.Province)
            || string.IsNullOrWhiteSpace(f.Region) || string.IsNullOrWhiteSpace(f.QualityFlag)
            || f.StatusCounts is null || f.CellCount < 0 || !double.IsFinite(f.BurnedAreaHa)
            || f.StatusCounts is { Predicted: < 0 } || f.StatusCounts is { LowSeverity: < 0 }
            || f.StatusCounts is { NoData: < 0 } || f.StatusCounts?.Total != f.CellCount);
        if (invalidFire is not null)
            return ("MANIFEST_FIRE_INVALID", "fires[] içinde null/boş zorunlu alan veya geçersiz sayısal değer var.");

        var sumCells = manifest.Fires.Sum(f => f.CellCount);
        if (manifest.TotalCells != sumCells)
            return ("TOTAL_CELLS_MISMATCH",
                $"manifest.total_cells={manifest.TotalCells}, fires[].cell_count toplamı={sumCells}");

        if (manifest.Crs != "EPSG:4326")
            return ("CRS_INVALID", $"manifest.crs='EPSG:4326' olmalı, gelen: '{manifest.Crs}'.");

        var w = manifest.PriorityWeights;
        if (!double.IsFinite(w.Recovery) || !double.IsFinite(w.Erosion) || !double.IsFinite(w.Access)
            || w.Recovery < 0 || w.Erosion < 0 || w.Access < 0 || w.Recovery + w.Erosion + w.Access <= 0)
            return ("WEIGHTS_INVALID", "manifest.priority_weights negatif olamaz, toplamı pozitif olmalı.");

        var t = manifest.PriorityThresholds;
        if (!double.IsFinite(t.Orta) || !double.IsFinite(t.Yuksek) || !double.IsFinite(t.CokYuksek)
            || !(t.Orta >= 0 && t.Orta < t.Yuksek && t.Yuksek < t.CokYuksek && t.CokYuksek <= 1))
            return ("THRESHOLDS_INVALID", "manifest.priority_thresholds: 0 <= ORTA < YUKSEK < COK_YUKSEK <= 1 sağlanmalı.");

        return null;
    }
}
