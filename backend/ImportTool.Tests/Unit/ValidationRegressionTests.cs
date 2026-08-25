using System.Text.Json;
using ImportTool.Cli;
using ImportTool.Geo;
using ImportTool.Import;
using ImportTool.Models;
using ImportTool.Reporting;
using ImportTool.Tests.Integration;
using ImportTool.Validation;

namespace ImportTool.Tests.Unit;

public class ValidationRegressionTests
{
    private static Manifest LoadManifest(SyntheticFireFixture fx) =>
        JsonSerializer.Deserialize<Manifest>(File.ReadAllText(fx.ManifestPath))!;

    [Fact]
    public void ManifestValidator_NullRequiredObject_ReturnsControlledError()
    {
        var fx = new SyntheticFireFixture();
        try
        {
            var manifest = LoadManifest(fx);
            manifest.Fires = null!;
            Assert.Equal("MANIFEST_REQUIRED_VALUE_NULL", ManifestValidator.Validate(manifest)?.Code);
        }
        finally { fx.Cleanup(); }
    }

    [Fact]
    public void StatusCounts_UnknownKey_IsRejected()
    {
        const string json = """{"predicted":1,"low_severity":0,"no_data":0,"surprise":1}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StatusCounts>(json));
    }

    [Fact]
    public void FireValidator_SiblingPrefixTraversal_IsRejected()
    {
        var fx = new SyntheticFireFixture();
        try
        {
            var manifest = LoadManifest(fx);
            var sibling = Path.GetFileName(fx.Dir) + "-evil";
            manifest.FilesPerFire[0] = Path.Combine("..", sibling, "{fire_id}_hucreler.csv");
            var result = new FireValidator(fx.Dir, manifest, []).Validate(manifest.Fires[0]);
            Assert.Equal("FILE_PATH_RESOLUTION_ERROR", result.ErrorCode);
        }
        finally { fx.Cleanup(); }
    }

    [Fact]
    public void FireValidator_NaNInCsv_IsRejected()
    {
        var fx = new SyntheticFireFixture();
        try
        {
            fx.MutateCsvRow(1, columns => { columns[2] = "NaN"; return columns; });
            var manifest = LoadManifest(fx);
            var result = new FireValidator(fx.Dir, manifest, []).Validate(manifest.Fires[0]);
            Assert.Equal("NON_FINITE_NUMBER", result.ErrorCode);
        }
        finally { fx.Cleanup(); }
    }

    [Fact]
    public void FireValidator_OutOfRangeCoordinate_IsRejected()
    {
        var fx = new SyntheticFireFixture();
        try
        {
            fx.MutateCsvRow(1, columns => { columns[2] = "91"; return columns; });
            var manifest = LoadManifest(fx);
            var result = new FireValidator(fx.Dir, manifest, []).Validate(manifest.Fires[0]);
            Assert.Equal("COORDINATE_OUT_OF_RANGE", result.ErrorCode);
        }
        finally { fx.Cleanup(); }
    }

    [Fact]
    public void FireValidator_NullNormalizationRange_IsRejected()
    {
        // GPT review bulgusu: ReGreen.Core.Priority.NormRange.Min/Max nullable (oncelik.py'nin
        // teorik "hiç predicted hücre yoksa null" durumunu okuyabilmek için) ama DB kolonları
        // NOT NULL — eskiden FireImporter null'ı sessizce 0'a çeviriyordu. Artık import zamanında
        // reddedilmeli, sessizce 0'a düşmemeli.
        var fx = new SyntheticFireFixture();
        fx.MutateMetadata(d => d["normalization_reference"] = new
        {
            recovery_gap_pred = new { min = (double?)null, max = 0.5 },
            slope_deg = new { min = 1.0, max = 20.0 },
            road_distance_km = new { min = 0.05, max = 2.0 },
        });
        try
        {
            var manifest = LoadManifest(fx);
            var result = new FireValidator(fx.Dir, manifest, []).Validate(manifest.Fires[0]);
            Assert.Equal("NORM_RANGE_NULL", result.ErrorCode);
        }
        finally { fx.Cleanup(); }
    }

    [Fact]
    public void GeometryComparison_IgnoresRingStartAndDirection()
    {
        var reader = new NetTopologySuite.IO.GeoJsonReader();
        var a = reader.Read<NetTopologySuite.Geometries.Geometry>(
            """{"type":"Polygon","coordinates":[[[35,37],[36,37],[36,38],[35,38],[35,37]]]}""");
        var b = reader.Read<NetTopologySuite.Geometries.Geometry>(
            """{"type":"Polygon","coordinates":[[[36,38],[36,37],[35,37],[35,38],[36,38]]]}""");
        Assert.True(EntityComparer.GeometriesEqual(a, b, 1e-9));
    }

    [Fact]
    public void ReportWriter_WhenTargetExists_ReturnsActualNewPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regreen-report-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, "report.json");
            File.WriteAllText(target, "original");
            Assert.True(ReportWriter.TryWrite(new ImportReport(), target, out var written));
            Assert.NotEqual(target, written);
            Assert.True(File.Exists(written));
            Assert.Equal("original", File.ReadAllText(target));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Orchestrator_NullFires_ProducesFatalReportBeforeDbConnection()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regreen-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            File.WriteAllText(manifestPath, """{"project":"x","generated_at":"2026-01-01T00:00:00Z","model_version":"x","schema_version":"1.1","cell_size_m":250,"crs":"EPSG:4326","fire_count":0,"total_cells":0,"training_rows":0,"training_groups":0,"priority_weights":{"recovery":0.5,"erosion":0.3,"access":0.2},"priority_thresholds":{"COK_YUKSEK":0.75,"YUKSEK":0.5,"ORTA":0.25},"files_per_fire":[],"fires":null}""");
            var result = await Orchestrator.RunAsync(new CliOptions
            {
                ManifestPath = manifestPath,
                ReportPath = Path.Combine(dir, "report.json"),
                ConnectionString = "must-not-be-used",
            }, TextWriter.Null, TextWriter.Null);
            Assert.Equal(2, result.ExitCode);
            Assert.Equal("MANIFEST_REQUIRED_VALUE_NULL", result.Report.FatalError?.Code);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CurrentBackendSampleData_All53FiresPassValidation()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var sampleDir = Path.Combine(repositoryRoot, "sample-data", "backend-data");
        var manifest = JsonSerializer.Deserialize<Manifest>(
            File.ReadAllText(Path.Combine(sampleDir, "manifest.json")))!;

        Assert.Null(ManifestValidator.Validate(manifest));
        var validator = new FireValidator(sampleDir, manifest, []);
        var failures = manifest.Fires
            .Select(entry => (entry.FireId, Result: validator.Validate(entry)))
            .Where(x => x.Result.Kind != FireValidationKind.Success)
            .Select(x => $"{x.FireId}: {x.Result.ErrorCode} — {x.Result.Reason}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(53, manifest.Fires.Count);
    }
}
