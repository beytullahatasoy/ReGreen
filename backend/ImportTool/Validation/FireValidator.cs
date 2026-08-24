using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using ImportTool.Geo;
using ImportTool.Models;
using ReGreen.Core.Priority;

namespace ImportTool.Validation;

/// <summary>
/// Bir yangının tüm dosya/tutarlılık doğrulamasını yapar — docs/import-flow.md §3.2-§3.6.
/// İlk hata o yangın için işlemi bitirir (spesifikasyonla tutarlı).
/// `crossFireSeenCellIds`, aynı çalıştırma içinde (dry-run dahil) farklı yangınların
/// aynı cell_id'yi iddia etmesini yakalamak için TÜM yangınlar arasında paylaşılır.
/// </summary>
public class FireValidator(string manifestDir, Manifest manifest, Dictionary<string, string> crossFireSeenCellIds)
{
    public FireValidationResult Validate(ManifestFireEntry entry)
    {
        // §3.1.5 (yangın bazında uygulanışı) — files_per_fire şablonlarını çöz.
        FireFilePaths paths;
        try
        {
            paths = ResolveFilePaths(entry.FireId);
        }
        catch (Exception ex)
        {
            return FireValidationResult.Failed("FILE_PATH_RESOLUTION_ERROR", ex.Message);
        }

        // §3.2.1 — dosya yokluğu → skipped
        var missing = new[] { paths.CsvPath, paths.GeoJsonPath, paths.MetadataPath }
            .Where(p => !File.Exists(p)).ToList();
        if (missing.Count > 0)
            return FireValidationResult.Skipped(
                $"Dosya eksik: {string.Join(", ", missing.Select(Path.GetFileName))}");

        // §3.2.2 — parse hataları → failed
        FireMetadata metadata;
        FirePerimeter perimeter;
        List<CellRow> rows;
        try
        {
            metadata = JsonSerializer.Deserialize<FireMetadata>(File.ReadAllText(paths.MetadataPath))
                ?? throw new JsonException("metadata.json boş/geçersiz.");
            ValidateMetadataShape(metadata);
        }
        catch (Exception ex)
        {
            return FireValidationResult.Failed("METADATA_PARSE_ERROR", $"{Path.GetFileName(paths.MetadataPath)}: {ex.Message}");
        }

        try
        {
            perimeter = GeoJsonFireReader.Read(paths.GeoJsonPath);
        }
        catch (Exception ex)
        {
            return FireValidationResult.Failed("GEOJSON_INVALID", $"{Path.GetFileName(paths.GeoJsonPath)}: {ex.Message}");
        }

        try
        {
            rows = ReadCsv(paths.CsvPath, out var headerError);
            if (headerError is not null)
                return FireValidationResult.Failed("CSV_HEADER_MISMATCH", headerError);
        }
        catch (Exception ex)
        {
            return FireValidationResult.Failed("CSV_PARSE_ERROR", $"{Path.GetFileName(paths.CsvPath)}: {ex.Message}");
        }

        // §3.3 — metadata/manifest çapraz tutarlılığı
        var metaCheck = ValidateMetadataConsistency(entry, metadata);
        if (metaCheck is not null) return metaCheck;

        // §3.5 — GeoJSON/metadata tutarlılığı
        var geoCheck = ValidateGeoJsonConsistency(entry, metadata, perimeter, paths.GeoJsonPath);
        if (geoCheck is not null) return geoCheck;

        // §3.4 — CSV satır doğrulamaları (dosya-içi VE çalışma-içi cell_id benzersizliği dahil)
        var csvCheck = ValidateCsvRows(entry, metadata, rows);
        if (csvCheck is not null) return csvCheck;

        // §3.6 — priority yeniden hesaplama + sınıflandırma
        var priorityCheck = ValidatePriority(rows, metadata);
        if (priorityCheck is not null) return priorityCheck;

        // Başarısız bir yangının cell_id'leri paylaşılan çalışma durumuna sızmasın.
        // Ortak sözlüğe ancak yangının tüm doğrulamaları geçtikten sonra kaydet.
        var registrationCheck = RegisterCellIds(entry, rows);
        if (registrationCheck is not null) return registrationCheck;

        return FireValidationResult.Success(new FireImportData
        {
            FireId = entry.FireId,
            Metadata = metadata,
            Perimeter = perimeter,
            CellRows = rows,
        });
    }

    private readonly record struct FireFilePaths(string CsvPath, string GeoJsonPath, string MetadataPath);

    /// <summary>manifest.files_per_fire şablonlarını {fire_id} ile doldurup path-traversal'a karşı doğrular.</summary>
    private FireFilePaths ResolveFilePaths(string fireId)
    {
        string? csv = null, geo = null, meta = null;
        foreach (var template in manifest.FilesPerFire)
        {
            var fileName = template.Replace("{fire_id}", fireId);
            var resolved = ResolveSafePath(fileName);
            if (fileName.EndsWith("_hucreler.csv", StringComparison.Ordinal)) csv = resolved;
            else if (fileName.EndsWith("_sinir.geojson", StringComparison.Ordinal)) geo = resolved;
            else if (fileName.EndsWith("_metadata.json", StringComparison.Ordinal)) meta = resolved;
        }

        if (csv is null || geo is null || meta is null)
            throw new InvalidOperationException(
                $"manifest.files_per_fire beklenen 3 kalıbı (_hucreler.csv/_sinir.geojson/_metadata.json) üretmedi: " +
                $"[{string.Join(", ", manifest.FilesPerFire)}]");

        return new FireFilePaths(csv, geo, meta);
    }

    private string ResolveSafePath(string fileName)
    {
        var fullDir = Path.GetFullPath(manifestDir);
        var fullPath = Path.GetFullPath(Path.Combine(fullDir, fileName));
        var relative = Path.GetRelativePath(fullDir, fullPath);
        if (Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidOperationException($"Path traversal engellendi: {fileName}");
        return fullPath;
    }

    private static void ValidateMetadataShape(FireMetadata meta)
    {
        if (string.IsNullOrWhiteSpace(meta.FireId) || string.IsNullOrWhiteSpace(meta.Province)
            || string.IsNullOrWhiteSpace(meta.Region) || string.IsNullOrWhiteSpace(meta.Crs)
            || string.IsNullOrWhiteSpace(meta.SchemaVersion) || string.IsNullOrWhiteSpace(meta.ModelVersion)
            || string.IsNullOrWhiteSpace(meta.QualityFlag))
            throw new JsonException("Metadata içindeki zorunlu string alanlar null/boş olamaz.");

        if (meta.StatusCounts is null || meta.PriorityWeights is null || meta.PriorityThresholds is null
            || meta.NormalizationReference is null
            || meta.NormalizationReference.RecoveryGapPred is null
            || meta.NormalizationReference.SlopeDeg is null
            || meta.NormalizationReference.RoadDistanceKm is null)
            throw new JsonException("Metadata içindeki zorunlu nesne alanları null olamaz.");

        if (!double.IsFinite(meta.ModisAreaHa) || !double.IsFinite(meta.BurnedAreaHa))
            throw new JsonException("Metadata içindeki sayısal alanlar NaN/Infinity olamaz.");
        if (meta.CellSizeM <= 0 || meta.CellCount < 0 || meta.OutOfFoldCells < 0
            || meta.StatusCounts.Predicted < 0 || meta.StatusCounts.LowSeverity < 0 || meta.StatusCounts.NoData < 0)
            throw new JsonException("Metadata sayaçları negatif, cell_size_m ise sıfır/negatif olamaz.");
    }

    private static List<CellRow> ReadCsv(string path, out string? headerError)
    {
        headerError = null;
        var config = new CsvConfiguration(CultureInfo.InvariantCulture);
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);

        // CsvHelper varsayılan olarak boş CSV alanını string.Empty'ye çevirir, null'a değil.
        // land_cover/priority_class gibi NULLABLE string alanlarda data-contract'ın null
        // davranışıyla eşleşmesi için boş string'i açıkça null sayıyoruz. Bu, fire_id/
        // cell_id/severity_class/prediction_status gibi ZORUNLU string alanları da
        // etkiler ama data-contract'a göre bunlar asla boş gelmemeli — boş gelirse
        // null olup aşağıdaki alan bazlı kontrollerin (§3.4) hepsi CliArgumentException
        // yerine kontrollü bir "failed" sonucu üretecek şekilde null-check'lidir.
        csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(string.Empty);

        csv.Read();
        csv.ReadHeader();
        var actualHeader = csv.HeaderRecord ?? [];
        if (!actualHeader.SequenceEqual(CellRow.ExpectedHeader))
        {
            headerError = $"Beklenen: [{string.Join(",", CellRow.ExpectedHeader)}], " +
                          $"Gelen: [{string.Join(",", actualHeader)}]";
            return [];
        }

        var rows = new List<CellRow>();
        var lineNumber = 1;
        while (csv.Read())
        {
            lineNumber++;
            var row = csv.GetRecord<CellRow>();
            row.SourceLineNumber = lineNumber;
            rows.Add(row);
        }
        return rows;
    }

    private FireValidationResult? ValidateMetadataConsistency(ManifestFireEntry entry, FireMetadata meta)
    {
        if (meta.FireId != entry.FireId)
            return FireValidationResult.Failed("FIRE_ID_MISMATCH",
                $"manifest fire_id={entry.FireId}, metadata fire_id={meta.FireId}");

        if (meta.SchemaVersion != manifest.SchemaVersion)
            return FireValidationResult.Failed("SCHEMA_VERSION_MISMATCH",
                $"manifest.schema_version={manifest.SchemaVersion}, metadata.schema_version={meta.SchemaVersion}");

        if (meta.FireDate != entry.FireDate || meta.Province != entry.Province || meta.Region != entry.Region)
            return FireValidationResult.Failed("MANIFEST_METADATA_MISMATCH",
                "fire_date/province/region manifest ile metadata arasında farklı.");

        if (meta.CellCount != entry.CellCount)
            return FireValidationResult.Failed("CELL_COUNT_MISMATCH",
                $"manifest.cell_count={entry.CellCount}, metadata.cell_count={meta.CellCount}");

        if (!PriorityCalculator.ApproximatelyEqual(meta.BurnedAreaHa, entry.BurnedAreaHa))
            return FireValidationResult.Failed("BURNED_AREA_MISMATCH",
                $"manifest.burned_area_ha={entry.BurnedAreaHa}, metadata.burned_area_ha={meta.BurnedAreaHa}");

        if (meta.QualityFlag != entry.QualityFlag)
            return FireValidationResult.Failed("QUALITY_FLAG_MISMATCH",
                $"manifest.quality_flag={entry.QualityFlag}, metadata.quality_flag={meta.QualityFlag}");

        if (meta.HasPerimeter != entry.HasPerimeter)
            return FireValidationResult.Failed("HAS_PERIMETER_MISMATCH", "manifest/metadata has_perimeter farklı.");

        if (!ContractEnums.QualityFlags.Contains(meta.QualityFlag))
            return FireValidationResult.Failed("QUALITY_FLAG_INVALID", $"Bilinmeyen quality_flag: {meta.QualityFlag}");

        var statusMismatch = meta.StatusCounts.Predicted != entry.StatusCounts.Predicted
            || meta.StatusCounts.LowSeverity != entry.StatusCounts.LowSeverity
            || meta.StatusCounts.NoData != entry.StatusCounts.NoData;
        if (statusMismatch)
            return FireValidationResult.Failed("STATUS_COUNTS_MISMATCH",
                "manifest.status_counts ile metadata.status_counts farklı.");

        // §3.3.2 — daha önce eksik olan paket/teslimat düzeyi alanların manifest↔metadata tutarlılığı
        if (meta.ModelVersion != manifest.ModelVersion)
            return FireValidationResult.Failed("MODEL_VERSION_MISMATCH",
                $"manifest.model_version={manifest.ModelVersion}, metadata.model_version={meta.ModelVersion}");

        if (meta.GeneratedAt != manifest.GeneratedAt)
            return FireValidationResult.Failed("GENERATED_AT_MISMATCH",
                $"manifest.generated_at={manifest.GeneratedAt:O}, metadata.generated_at={meta.GeneratedAt:O}");

        if (meta.CellSizeM != manifest.CellSizeM)
            return FireValidationResult.Failed("CELL_SIZE_MISMATCH",
                $"manifest.cell_size_m={manifest.CellSizeM}, metadata.cell_size_m={meta.CellSizeM}");

        if (meta.Crs != manifest.Crs)
            return FireValidationResult.Failed("CRS_MISMATCH",
                $"manifest.crs={manifest.Crs}, metadata.crs={meta.Crs}");

        if (meta.Crs != "EPSG:4326")
            return FireValidationResult.Failed("CRS_INVALID", $"metadata.crs='EPSG:4326' olmalı, gelen: '{meta.Crs}'.");

        if (!WeightsEqual(meta.PriorityWeights, manifest.PriorityWeights))
            return FireValidationResult.Failed("PRIORITY_WEIGHTS_MISMATCH",
                "manifest.priority_weights ile metadata.priority_weights farklı — hangisi kullanılacak belirsiz.");

        if (!ThresholdsEqual(meta.PriorityThresholds, manifest.PriorityThresholds))
            return FireValidationResult.Failed("PRIORITY_THRESHOLDS_MISMATCH",
                "manifest.priority_thresholds ile metadata.priority_thresholds farklı — hangisi kullanılacak belirsiz.");

        // §3.3.4 — normalization_reference min<=max
        var norm = meta.NormalizationReference;
        foreach (var (name, range) in new[]
                 {
                     ("recovery_gap_pred", norm.RecoveryGapPred),
                     ("slope_deg", norm.SlopeDeg),
                     ("road_distance_km", norm.RoadDistanceKm),
                 })
        {
            if (range.Min is { } min && !double.IsFinite(min)
                || range.Max is { } max && !double.IsFinite(max))
                return FireValidationResult.Failed("NON_FINITE_NUMBER",
                    $"normalization_reference.{name} NaN/Infinity içeremez.");
            if (range.Min is not null && range.Max is not null && range.Min > range.Max)
                return FireValidationResult.Failed("NORM_RANGE_INVALID", $"normalization_reference.{name}: min > max");
        }

        // §3.3.5 — in_training_set / out_of_fold_cells
        if (meta.OutOfFoldCells < 0)
            return FireValidationResult.Failed("OOF_NEGATIVE", "out_of_fold_cells negatif olamaz.");
        if (!meta.InTrainingSet && meta.OutOfFoldCells != 0)
            return FireValidationResult.Failed("OOF_INCONSISTENT",
                "in_training_set=false iken out_of_fold_cells 0 olmalı.");

        // §3.1.6 (yangın düzeyinde tekrar) — ağırlık/eşik temel kuralları. Not: yukarıdaki
        // (259. satır) PRIORITY_WEIGHTS_MISMATCH kontrolü nedeniyle bu satıra normal
        // pipeline'da SADECE metadata.priority_weights == manifest.priority_weights
        // olduğunda ulaşılır — yani manifest zaten geçersizse Orchestrator zaten
        // ManifestValidator'da FATAL vermiş olur, buraya hiç gelinmez. Yine de bilerek
        // burada tutuluyor: FireValidator ileride manifest kontrolünden BAĞIMSIZ
        // çağrılırsa (ör. tek bir yangının yeniden doğrulanması) sessizce geçersiz veri
        // kabul etmesin diye.
        var w = meta.PriorityWeights;
        var wTotal = w.Recovery + w.Erosion + w.Access;
        if (!double.IsFinite(w.Recovery) || !double.IsFinite(w.Erosion) || !double.IsFinite(w.Access)
            || w.Recovery < 0 || w.Erosion < 0 || w.Access < 0
            // Her bileşen tek başına sonlu olsa bile toplamları taşıp Infinity'ye
            // yuvarlanabilir (ör. 1e308 + 1e308) — "toplam <= 0" tek başına bunu yakalamaz.
            || !double.IsFinite(wTotal) || wTotal <= 0)
            return FireValidationResult.Failed("WEIGHTS_INVALID", "priority_weights negatif olamaz, toplamı sonlu ve pozitif olmalı.");

        var t = meta.PriorityThresholds;
        if (!double.IsFinite(t.Orta) || !double.IsFinite(t.Yuksek) || !double.IsFinite(t.CokYuksek)
            || !(t.Orta >= 0 && t.Orta < t.Yuksek && t.Yuksek < t.CokYuksek && t.CokYuksek <= 1))
            return FireValidationResult.Failed("THRESHOLDS_INVALID", "priority_thresholds: 0 <= ORTA < YUKSEK < COK_YUKSEK <= 1 sağlanmalı.");

        return null;
    }

    private static bool WeightsEqual(PriorityWeights a, PriorityWeights b) =>
        PriorityCalculator.ApproximatelyEqual(a.Recovery, b.Recovery)
        && PriorityCalculator.ApproximatelyEqual(a.Erosion, b.Erosion)
        && PriorityCalculator.ApproximatelyEqual(a.Access, b.Access);

    private static bool ThresholdsEqual(PriorityThresholds a, PriorityThresholds b) =>
        PriorityCalculator.ApproximatelyEqual(a.CokYuksek, b.CokYuksek)
        && PriorityCalculator.ApproximatelyEqual(a.Yuksek, b.Yuksek)
        && PriorityCalculator.ApproximatelyEqual(a.Orta, b.Orta);

    private static FireValidationResult? ValidateGeoJsonConsistency(
        ManifestFireEntry entry, FireMetadata meta, FirePerimeter perimeter, string geoJsonPath)
    {
        if (perimeter.FireId != entry.FireId)
            return FireValidationResult.Failed("GEOJSON_FIRE_ID_MISMATCH",
                $"geojson fire_id={perimeter.FireId}, beklenen={entry.FireId}");

        if (perimeter.FireDate != entry.FireDate || perimeter.Province != entry.Province || perimeter.Region != entry.Region)
            return FireValidationResult.Failed("GEOJSON_MANIFEST_MISMATCH",
                $"geojson fire_date/province/region ({perimeter.FireDate}/{perimeter.Province}/{perimeter.Region}) " +
                $"manifest ile uyuşmuyor ({entry.FireDate}/{entry.Province}/{entry.Region}).");

        if (!PriorityCalculator.ApproximatelyEqual(perimeter.ModisAreaHa, meta.ModisAreaHa))
            return FireValidationResult.Failed("MODIS_AREA_MISMATCH",
                $"geojson.modis_area_ha={perimeter.ModisAreaHa}, metadata.modis_area_ha={meta.ModisAreaHa}");

        if (!meta.HasPerimeter)
            return FireValidationResult.Failed("HAS_PERIMETER_FALSE_UNSUPPORTED",
                "Bu şema HasPerimeter=false'u desteklemiyor (bkz. docs/db-schema.md §2 notu).");

        // data-contract §7.2 — Feature.properties'te BU 5 alan dışında başka alan olmamalı.
        var expectedProps = new HashSet<string> { "fire_id", "fire_date", "province", "region", "modis_area_ha" };
        var actualProps = GeoJsonFireReader.ReadPropertyNames(geoJsonPath);
        var unexpected = actualProps.Except(expectedProps).ToList();
        if (unexpected.Count > 0)
            return FireValidationResult.Failed("GEOJSON_UNEXPECTED_PROPERTY",
                $"geojson properties'te beklenmeyen alan(lar): {string.Join(", ", unexpected)}");

        return null;
    }

    private FireValidationResult? ValidateCsvRows(
        ManifestFireEntry entry, FireMetadata meta, List<CellRow> rows)
    {
        if (rows.Count != meta.CellCount)
            return FireValidationResult.Failed("ROW_COUNT_MISMATCH",
                $"CSV {rows.Count} satır, metadata.cell_count={meta.CellCount}");

        var seenCellIdsInFile = new HashSet<string>(rows.Count);
        int predicted = 0, lowSeverity = 0, noData = 0;
        var cellIdPrefix = entry.FireId + "_";

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.FireId))
                return FireValidationResult.Failed("ROW_FIRE_ID_MISSING", $"Satır {row.SourceLineNumber}: fire_id boş.");
            if (row.FireId != entry.FireId)
                return FireValidationResult.Failed("ROW_FIRE_ID_MISMATCH",
                    $"Satır {row.SourceLineNumber}: fire_id={row.FireId}, beklenen={entry.FireId}");

            if (string.IsNullOrEmpty(row.CellId))
                return FireValidationResult.Failed("ROW_CELL_ID_MISSING", $"Satır {row.SourceLineNumber}: cell_id boş.");

            if (!row.CellId.StartsWith(cellIdPrefix, StringComparison.Ordinal) || row.CellId.Length != cellIdPrefix.Length + 6
                || !row.CellId[cellIdPrefix.Length..].All(char.IsDigit))
                return FireValidationResult.Failed("CELL_ID_FORMAT",
                    $"Satır {row.SourceLineNumber}: cell_id formatı geçersiz: {row.CellId}");

            if (!seenCellIdsInFile.Add(row.CellId))
                return FireValidationResult.Failed("CELL_ID_DUPLICATE",
                    $"Satır {row.SourceLineNumber}: cell_id dosya içinde tekrarlı: {row.CellId}");

            // §3.4.5 — paylaşılan sözlüğü burada değiştirme; yangın daha sonraki bir
            // doğrulamada başarısız olabilir. Yalnızca mevcut sahipliği kontrol et.
            lock (crossFireSeenCellIds)
            {
                if (crossFireSeenCellIds.TryGetValue(row.CellId, out var owner) && owner != entry.FireId)
                    return FireValidationResult.Failed("CELL_ID_CROSS_FIRE_CONFLICT_IN_RUN",
                        $"Satır {row.SourceLineNumber}: cell_id={row.CellId} bu çalıştırmada zaten '{owner}' yangınına ait.");
            }

            var numericFields = new (string Name, double? Value)[]
            {
                ("lat", row.Lat), ("lon", row.Lon), ("tree_cover", row.TreeCover),
                ("tree_cover_annual", row.TreeCoverAnnual), ("burn_severity_dnbr", row.BurnSeverityDnbr),
                ("slope_deg", row.SlopeDeg), ("elevation_m", row.ElevationM),
                ("road_distance_km", row.RoadDistanceKm), ("ndvi_before", row.NdviBefore),
                ("ndvi_after", row.NdviAfter), ("ndvi_drop", row.NdviDrop),
                ("recovery_gap_pred", row.RecoveryGapPred), ("priority_score", row.PriorityScore),
            };
            var nonFinite = numericFields.FirstOrDefault(x => x.Value is { } value && !double.IsFinite(value));
            if (nonFinite != default)
                return FireValidationResult.Failed("NON_FINITE_NUMBER",
                    $"Satır {row.SourceLineNumber}: {nonFinite.Name} NaN/Infinity olamaz.");

            if (row.Lat is < -90 or > 90 || row.Lon is < -180 or > 180)
                return FireValidationResult.Failed("COORDINATE_OUT_OF_RANGE",
                    $"Satır {row.SourceLineNumber}: EPSG:4326 koordinatı aralık dışı (lat={row.Lat}, lon={row.Lon}).");

            if (string.IsNullOrEmpty(row.SeverityClass) || !ContractEnums.SeverityClasses.Contains(row.SeverityClass))
                return FireValidationResult.Failed("SEVERITY_CLASS_INVALID",
                    $"Satır {row.SourceLineNumber}: bilinmeyen severity_class: {row.SeverityClass}");

            if (row.LandCover is not null && !ContractEnums.LandCovers.Contains(row.LandCover))
                return FireValidationResult.Failed("LAND_COVER_INVALID",
                    $"Satır {row.SourceLineNumber}: bilinmeyen land_cover: {row.LandCover}");

            if (string.IsNullOrEmpty(row.PredictionStatus) || !ContractEnums.PredictionStatuses.Contains(row.PredictionStatus))
                return FireValidationResult.Failed("PREDICTION_STATUS_INVALID",
                    $"Satır {row.SourceLineNumber}: bilinmeyen prediction_status: {row.PredictionStatus}");

            if (row.RecoveryGapPred is { } rgp && (rgp < -0.5 || rgp > 1.5))
                return FireValidationResult.Failed("RECOVERY_GAP_OUT_OF_RANGE",
                    $"Satır {row.SourceLineNumber}: recovery_gap_pred aralık dışı: değer={rgp}, izin verilen=-0.5..1.5");

            if (row.PriorityScore is { } ps && (ps < 0 || ps > 1))
                return FireValidationResult.Failed("PRIORITY_SCORE_OUT_OF_RANGE",
                    $"Satır {row.SourceLineNumber}: priority_score aralık dışı: değer={ps}");

            // §6.2 KESİN TABLO — DB'deki CK_Predictions_StatusConsistency ile birebir
            var statusOk = row.PredictionStatus switch
            {
                "predicted" => row.RecoveryGapPred is not null && row.PriorityScore is not null && row.PriorityClass is not null,
                "low_severity" => row.RecoveryGapPred is null && row.PriorityScore == 0.0 && row.PriorityClass == "DUSUK",
                "no_data" => row.RecoveryGapPred is null && row.PriorityScore is null && row.PriorityClass is null,
                _ => false,
            };
            if (!statusOk)
                return FireValidationResult.Failed("STATUS_CONSISTENCY_VIOLATION",
                    $"Satır {row.SourceLineNumber}: prediction_status={row.PredictionStatus} için alan kombinasyonu §6.2 ile uyuşmuyor.");

            if (row.PriorityClass is not null && !ContractEnums.PriorityClasses.Contains(row.PriorityClass))
                return FireValidationResult.Failed("PRIORITY_CLASS_INVALID",
                    $"Satır {row.SourceLineNumber}: bilinmeyen priority_class: {row.PriorityClass}");

            switch (row.PredictionStatus)
            {
                case "predicted": predicted++; break;
                case "low_severity": lowSeverity++; break;
                case "no_data": noData++; break;
            }
        }

        if (predicted != meta.StatusCounts.Predicted || lowSeverity != meta.StatusCounts.LowSeverity || noData != meta.StatusCounts.NoData)
            return FireValidationResult.Failed("CSV_STATUS_DISTRIBUTION_MISMATCH",
                $"CSV dağılımı predicted={predicted}/low_severity={lowSeverity}/no_data={noData}, " +
                $"metadata status_counts ile uyuşmuyor.");

        return null;
    }

    private FireValidationResult? RegisterCellIds(ManifestFireEntry entry, List<CellRow> rows)
    {
        lock (crossFireSeenCellIds)
        {
            foreach (var row in rows)
            {
                if (crossFireSeenCellIds.TryGetValue(row.CellId, out var owner) && owner != entry.FireId)
                    return FireValidationResult.Failed("CELL_ID_CROSS_FIRE_CONFLICT_IN_RUN",
                        $"Satır {row.SourceLineNumber}: cell_id={row.CellId} bu çalıştırmada zaten '{owner}' yangınına ait.");
            }
            foreach (var row in rows)
                crossFireSeenCellIds[row.CellId] = entry.FireId;
        }
        return null;
    }

    private static FireValidationResult? ValidatePriority(List<CellRow> rows, FireMetadata meta)
    {
        var predictedRows = rows.Where(r => r.PredictionStatus == "predicted").ToList();
        var norm = meta.NormalizationReference;

        // §3.3.4 — normalization_reference'ın CSV'deki gerçek predicted min/max'ıyla tutarlılığı.
        // ÜÇ alanın da (sadece recovery_gap_pred değil) kontrol edilmesi gerekiyor.
        var fieldChecks = new (string Name, NormRange Range, Func<CellRow, double> Selector)[]
        {
            ("recovery_gap_pred", norm.RecoveryGapPred, r => r.RecoveryGapPred!.Value),
            ("slope_deg", norm.SlopeDeg, r => r.SlopeDeg),
            ("road_distance_km", norm.RoadDistanceKm, r => r.RoadDistanceKm),
        };

        foreach (var (name, range, selector) in fieldChecks)
        {
            if (predictedRows.Count == 0) break;
            var actualMin = predictedRows.Min(selector);
            var actualMax = predictedRows.Max(selector);
            if (!PriorityCalculator.ApproximatelyEqual(actualMin, range.Min ?? double.NaN, 1e-6) ||
                !PriorityCalculator.ApproximatelyEqual(actualMax, range.Max ?? double.NaN, 1e-6))
            {
                return FireValidationResult.Failed("NORM_REFERENCE_MISMATCH",
                    $"normalization_reference.{name} ({range.Min}..{range.Max}) CSV'deki gerçek " +
                    $"predicted min/max'la ({actualMin}..{actualMax}) uyuşmuyor.");
            }
        }

        foreach (var row in predictedRows)
        {
            var expectedScore = PriorityCalculator.ComputeScore(
                row.RecoveryGapPred!.Value, row.SlopeDeg, row.RoadDistanceKm, norm, meta.PriorityWeights);

            // İki taraf da zaten 4 ondalığa yuvarlanmış (ComputeScore içinde) — burada sadece
            // kayan noktalı temsil gürültüsüne tolerans tanınır, iş kuralı toleransı DEĞİL.
            if (!PriorityCalculator.ApproximatelyEqual(expectedScore, row.PriorityScore!.Value, 1e-9))
                return FireValidationResult.Failed("PRIORITY_SCORE_RECALC_MISMATCH",
                    $"Satır {row.SourceLineNumber} ({row.CellId}): yeniden hesaplanan skor={expectedScore}, " +
                    $"CSV'deki={row.PriorityScore}");

            var expectedClass = PriorityCalculator.Classify(expectedScore, meta.PriorityThresholds);
            if (expectedClass != row.PriorityClass)
                return FireValidationResult.Failed("PRIORITY_CLASS_RECALC_MISMATCH",
                    $"Satır {row.SourceLineNumber} ({row.CellId}): beklenen sınıf={expectedClass}, CSV'deki={row.PriorityClass}");
        }

        return null;
    }
}
