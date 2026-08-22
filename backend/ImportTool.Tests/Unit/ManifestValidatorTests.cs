using ImportTool.Models;
using ImportTool.Validation;
using Xunit;

namespace ImportTool.Tests.Unit;

public class ManifestValidatorTests
{
    private static Manifest ValidManifest() => new()
    {
        Project = "ReGreen / FireRecover",
        GeneratedAt = DateTimeOffset.Parse("2026-08-19T17:22:55+00:00"),
        ModelVersion = "rf_v1",
        SchemaVersion = "1.1",
        CellSizeM = 250,
        Crs = "EPSG:4326",
        FireCount = 1,
        TotalCells = 10,
        TrainingRows = 100,
        TrainingGroups = 5,
        PriorityWeights = new PriorityWeights { Recovery = 0.5, Erosion = 0.3, Access = 0.2 },
        PriorityThresholds = new PriorityThresholds { CokYuksek = 0.75, Yuksek = 0.5, Orta = 0.25 },
        FilesPerFire = ["{fire_id}_hucreler.csv", "{fire_id}_sinir.geojson", "{fire_id}_metadata.json"],
        Fires =
        [
            new ManifestFireEntry
            {
                FireId = "TEST_2026_01", FireDate = new DateOnly(2026, 1, 1),
                Province = "Test", Region = "Test", CellCount = 10, BurnedAreaHa = 62.5,
                QualityFlag = "ok", HasPerimeter = true, StatusCounts = new StatusCounts { Predicted = 10 },
            },
        ],
    };

    [Fact]
    public void Validate_ValidManifest_ReturnsNull()
    {
        Assert.Null(ManifestValidator.Validate(ValidManifest()));
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_Fatal()
    {
        var m = ValidManifest() with { SchemaVersion = "9.9" };
        var error = ManifestValidator.Validate(m);
        Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", error?.Code);
    }

    [Fact]
    public void Validate_DuplicateFireId_Fatal()
    {
        var m = ValidManifest();
        m.Fires.Add(m.Fires[0]);
        m.FireCount = 2;
        var error = ManifestValidator.Validate(m);
        Assert.Equal("DUPLICATE_FIRE_ID", error?.Code);
    }

    [Fact]
    public void Validate_FireCountMismatch_Fatal()
    {
        var m = ValidManifest() with { FireCount = 5 };
        var error = ManifestValidator.Validate(m);
        Assert.Equal("FIRE_COUNT_MISMATCH", error?.Code);
    }

    [Fact]
    public void Validate_TotalCellsMismatch_Fatal()
    {
        var m = ValidManifest() with { TotalCells = 999 };
        var error = ManifestValidator.Validate(m);
        Assert.Equal("TOTAL_CELLS_MISMATCH", error?.Code);
    }

    [Fact]
    public void Validate_NegativeWeight_Fatal()
    {
        var m = ValidManifest() with { PriorityWeights = new PriorityWeights { Recovery = -0.1, Erosion = 0.3, Access = 0.2 } };
        var error = ManifestValidator.Validate(m);
        Assert.Equal("WEIGHTS_INVALID", error?.Code);
    }

    [Theory]
    [InlineData(0.25, 0.25, 0.75)] // ORTA < YUKSEK saglanmiyor (esit)
    [InlineData(0.30, 0.25, 0.75)] // ORTA > YUKSEK
    [InlineData(0.10, 0.50, 1.10)] // COK_YUKSEK > 1
    public void Validate_InvalidThresholdOrdering_Fatal(double orta, double yuksek, double cokYuksek)
    {
        var m = ValidManifest() with
        {
            PriorityThresholds = new PriorityThresholds { Orta = orta, Yuksek = yuksek, CokYuksek = cokYuksek },
        };
        var error = ManifestValidator.Validate(m);
        Assert.Equal("THRESHOLDS_INVALID", error?.Code);
    }
}
