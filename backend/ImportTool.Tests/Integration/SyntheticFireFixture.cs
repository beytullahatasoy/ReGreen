using System.Globalization;
using System.Text.Json;
using ReGreen.Core.Priority;

namespace ImportTool.Tests.Integration;

/// <summary>
/// Testler için gerçekçi, kendi içinde tutarlı, sentetik bir "AI teslim paketi" üretir
/// (manifest.json + {fire_id}_hucreler.csv/_sinir.geojson/_metadata.json). Sample data
/// dosyalarını KOPYALAMAZ/DEĞİŞTİRMEZ — tamamen izole, geçici bir dizine yazar.
/// PriorityCalculator kullanılarak üretilen skorlar, calculator'ın kendisini test etmek
/// için DEĞİL (bu PriorityCalculatorTests'te gerçek CSV değerleriyle yapılıyor) — DB
/// yazma/idempotency/insert-or-verify davranışını test etmek için kendinden-tutarlı
/// bir fixture üretmek içindir.
/// </summary>
public class SyntheticFireFixture
{
    public string Dir { get; }
    public string FireId { get; }
    public string ManifestPath => Path.Combine(Dir, "manifest.json");

    private static readonly (double Recovery, double Slope, double Road)[] PredictedInputs =
    [
        (0.20, 5.0, 1.0),
        (0.40, 15.0, 0.2),
    ];

    public SyntheticFireFixture(string fireId = "TESTF_2026_01", DateTimeOffset? generatedAt = null)
    {
        FireId = fireId;
        Dir = Path.Combine(Path.GetTempPath(), "regreen-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        Write(generatedAt ?? DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
    }

    private void Write(DateTimeOffset generatedAt)
    {
        var weights = new { recovery = 0.5, erosion = 0.3, access = 0.2 };
        var thresholds = new { COK_YUKSEK = 0.75, YUKSEK = 0.5, ORTA = 0.25 };

        var recoveryMin = PredictedInputs.Min(p => p.Recovery);
        var recoveryMax = PredictedInputs.Max(p => p.Recovery);
        var slopeMin = PredictedInputs.Min(p => p.Slope);
        var slopeMax = PredictedInputs.Max(p => p.Slope);
        var roadMin = PredictedInputs.Min(p => p.Road);
        var roadMax = PredictedInputs.Max(p => p.Road);

        // Anonim nesne + JsonPropertyName-uyumlu snake_case alan adları kullanılıyor —
        // FireMetadata.NormalizationReference deserialize edebilsin diye (bkz. MetadataModels.cs).
        var norm = new
        {
            recovery_gap_pred = new { min = recoveryMin, max = recoveryMax },
            slope_deg = new { min = slopeMin, max = slopeMax },
            road_distance_km = new { min = roadMin, max = roadMax },
        };

        var pw = new PriorityWeights { Recovery = 0.5, Erosion = 0.3, Access = 0.2 };
        var pt = new PriorityThresholds { CokYuksek = 0.75, Yuksek = 0.5, Orta = 0.25 };
        var normModel = new NormalizationReference
        {
            RecoveryGapPred = new NormRange { Min = recoveryMin, Max = recoveryMax },
            SlopeDeg = new NormRange { Min = slopeMin, Max = slopeMax },
            RoadDistanceKm = new NormRange { Min = roadMin, Max = roadMax },
        };

        var inv = CultureInfo.InvariantCulture;
        var csvRows = new List<string>();
        var cellNo = 1;

        foreach (var (recovery, slope, road) in PredictedInputs)
        {
            var score = PriorityCalculator.ComputeScore(recovery, slope, road, normModel, pw);
            var cls = PriorityCalculator.Classify(score, pt);
            csvRows.Add(FormatRow(cellNo++, "predicted", recovery, slope, road, score, cls));
        }
        csvRows.Add(FormatRow(cellNo++, "low_severity", null, 8.0, 0.5, 0.0, "DUSUK"));
        csvRows.Add(FormatRow(cellNo++, "no_data", null, 3.0, 0.3, null, null, elevationMissing: true));

        var cellCount = csvRows.Count;
        var statusCounts = new { predicted = 2, low_severity = 1, no_data = 1 };

        var csv = string.Join("\n",
            new[] { "fire_id,cell_id,lat,lon,tree_cover,tree_cover_annual,burn_severity_dnbr,slope_deg,elevation_m,road_distance_km,ndvi_before,ndvi_after,ndvi_drop,severity_class,land_cover,prediction_status,recovery_gap_pred,priority_score,priority_class" }
                .Concat(csvRows));
        File.WriteAllText(Path.Combine(Dir, $"{FireId}_hucreler.csv"), csv);

        var geoJson = JsonSerializer.Serialize(new
        {
            type = "Feature",
            properties = new { fire_id = FireId, fire_date = "2026-01-01", province = "TestProvince", region = "TestRegion", modis_area_ha = 10.0 },
            geometry = new
            {
                type = "Polygon",
                coordinates = new[]
                {
                    new[] { new[] { 35.0, 37.0 }, new[] { 35.01, 37.0 }, new[] { 35.01, 37.01 }, new[] { 35.0, 37.01 }, new[] { 35.0, 37.0 } },
                },
            },
        });
        File.WriteAllText(Path.Combine(Dir, $"{FireId}_sinir.geojson"), geoJson);

        var metadata = new
        {
            fire_id = FireId,
            fire_date = "2026-01-01",
            province = "TestProvince",
            region = "TestRegion",
            modis_area_ha = 10.0,
            cell_size_m = 250,
            cell_count = cellCount,
            burned_area_ha = 62.5,
            crs = "EPSG:4326",
            schema_version = "1.1",
            model_version = "rf_test",
            generated_at = generatedAt.ToString("O"),
            quality_flag = "ok",
            quality_note = (string?)null,
            in_training_set = false,
            out_of_fold_cells = 0,
            status_counts = statusCounts,
            priority_weights = weights,
            priority_thresholds = thresholds,
            normalization_reference = norm,
            has_perimeter = true,
        };
        File.WriteAllText(Path.Combine(Dir, $"{FireId}_metadata.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

        var manifest = new
        {
            project = "ReGreen / FireRecover (test)",
            generated_at = generatedAt.ToString("O"),
            model_version = "rf_test",
            schema_version = "1.1",
            cell_size_m = 250,
            crs = "EPSG:4326",
            fire_count = 1,
            total_cells = cellCount,
            training_rows = 0,
            training_groups = 0,
            priority_weights = weights,
            priority_thresholds = thresholds,
            files_per_fire = new[] { "{fire_id}_hucreler.csv", "{fire_id}_sinir.geojson", "{fire_id}_metadata.json" },
            fires = new[]
            {
                new
                {
                    fire_id = FireId, fire_date = "2026-01-01", province = "TestProvince", region = "TestRegion",
                    cell_count = cellCount, burned_area_ha = 62.5, quality_flag = "ok", has_perimeter = true,
                    status_counts = statusCounts,
                },
            },
        };
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private string FormatRow(int no, string status, double? recovery, double slope, double road,
        double? score, string? cls, bool elevationMissing = false)
    {
        var inv = CultureInfo.InvariantCulture;
        var cellId = $"{FireId}_{no:D6}";
        var elevation = elevationMissing ? "" : "100.0";
        return string.Join(",",
            FireId, cellId, "37.005", "35.005", "0.5", "0.5",
            (0.3).ToString(inv), slope.ToString(inv), elevation, road.ToString(inv),
            "0.5", "0.3", "0.2", "dusuk", "Tarim", status,
            recovery?.ToString(inv) ?? "", score?.ToString(inv) ?? "", cls ?? "");
    }

    /// <summary>Bu fixture'ın {fire_id}_metadata.json'unu okuyup değiştirip geri yazar — kasıtlı bozuk senaryolar için.</summary>
    public void MutateMetadata(Action<Dictionary<string, object?>> mutate)
    {
        var path = Path.Combine(Dir, $"{FireId}_metadata.json");
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path))!;
        mutate(dict);
        File.WriteAllText(path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>manifest.json'u okuyup değiştirip geri yazar — ör. metadata ile BİRLİKTE
    /// değiştirilmesi gereken alanlar (`priority_weights` gibi) için, aksi halde
    /// PRIORITY_WEIGHTS_MISMATCH devreye girip asıl test edilmek istenen kontrolü maskeler.</summary>
    public void MutateManifest(Action<Dictionary<string, object?>> mutate)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(ManifestPath))!;
        mutate(dict);
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>CSV'nin belirli bir satırını (1-tabanlı, header hariç) elle değiştirir.</summary>
    public void MutateCsvRow(int rowIndex, Func<string[], string[]> mutate)
    {
        var path = Path.Combine(Dir, $"{FireId}_hucreler.csv");
        var lines = File.ReadAllLines(path).ToList();
        var cols = lines[rowIndex].Split(',');
        lines[rowIndex] = string.Join(",", mutate(cols));
        File.WriteAllLines(path, lines);
    }

    public void Cleanup()
    {
        try { Directory.Delete(Dir, recursive: true); } catch { /* best effort */ }
    }
}
