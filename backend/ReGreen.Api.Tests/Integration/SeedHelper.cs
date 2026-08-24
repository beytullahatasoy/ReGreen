using ReGreen.Data.Entities;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// Testler için küçük, kendi içinde tutarlı yangın/hücre/ModelRun/Prediction verisi
/// üretir — CHECK constraint'leri (bkz. AppDbContext) sağlayacak şekilde. ImportTool
/// pipeline'ından GEÇMEZ (Api testleri import'u değil API'yi doğruluyor); doğrudan
/// EF Core ile ekleniyor.
/// </summary>
public static class SeedHelper
{
    public const string FireId = "TEST_2026_01";

    private const string PerimeterGeoJsonTemplate =
        """{"type":"Feature","properties":{"fire_id":"__FIRE_ID__","fire_date":"2026-01-01","province":"Test","region":"Test","modis_area_ha":100.0},"geometry":{"type":"Polygon","coordinates":[[[35.0,37.0],[35.01,37.0],[35.01,37.01],[35.0,37.01],[35.0,37.0]]]}}""";

    public static string ValidPerimeterGeoJson(string fireId = FireId) =>
        PerimeterGeoJsonTemplate.Replace("__FIRE_ID__", fireId);

    public static Fire BuildFire(string fireId = FireId, string qualityFlag = "ok") => new()
    {
        FireId = fireId,
        FireDate = new DateOnly(2026, 1, 1),
        Province = "Test",
        Region = "Test",
        ModisAreaHa = 100.0,
        BurnedAreaHa = 100.0,
        CellSizeM = 250,
        HasPerimeter = true,
        PerimeterGeoJson = ValidPerimeterGeoJson(fireId),
        MarkerLat = 37.005,
        MarkerLon = 35.005,
        QualityFlag = qualityFlag,
        QualityNote = qualityFlag == "check" ? "test note" : null,
    };

    public static ModelRun BuildModelRun(string fireId, string modelVersion, DateTimeOffset generatedAt) => new()
    {
        FireId = fireId,
        ModelVersion = modelVersion,
        GeneratedAt = generatedAt,
        SchemaVersion = "1.1",
        InTrainingSet = false,
        OutOfFoldCells = 0,
        NormRecoveryGapMin = 0.10,
        NormRecoveryGapMax = 0.50,
        NormSlopeMin = 1.0,
        NormSlopeMax = 20.0,
        NormRoadDistMin = 0.05,
        NormRoadDistMax = 2.0,
        DefaultWeightRecovery = 0.5,
        DefaultWeightErosion = 0.3,
        DefaultWeightAccess = 0.2,
        ThresholdVeryHigh = 0.75,
        ThresholdHigh = 0.50,
        ThresholdMedium = 0.25,
    };

    public static Cell BuildCell(
        string fireId, string cellId, double lat, double lon,
        double slopeDeg = 10.0, double roadDistanceKm = 1.0,
        string severityClass = "orta-yuksek") => new()
    {
        CellId = cellId,
        FireId = fireId,
        CenterLat = lat,
        CenterLon = lon,
        TreeCover = 0.5,
        TreeCoverAnnual = 0.5,
        BurnSeverityDnbr = 0.4,
        SlopeDeg = slopeDeg,
        ElevationM = 200,
        RoadDistanceKm = roadDistanceKm,
        NdviBefore = 0.6,
        NdviAfter = 0.2,
        NdviDrop = 0.4,
        SeverityClass = severityClass,
        LandCover = "Tarim",
    };

    public static Prediction BuildPredictedPrediction(
        string fireId, string cellId, int modelRunId,
        double recoveryGapPred, double priorityScore, string priorityClass) => new()
    {
        FireId = fireId,
        CellId = cellId,
        ModelRunId = modelRunId,
        PredictionStatus = "predicted",
        RecoveryGapPred = recoveryGapPred,
        DefaultPriorityScore = priorityScore,
        DefaultPriorityClass = priorityClass,
    };

    public static Prediction BuildLowSeverityPrediction(string fireId, string cellId, int modelRunId) => new()
    {
        FireId = fireId,
        CellId = cellId,
        ModelRunId = modelRunId,
        PredictionStatus = "low_severity",
        RecoveryGapPred = null,
        DefaultPriorityScore = 0.0,
        DefaultPriorityClass = "DUSUK",
    };

    public static Prediction BuildNoDataPrediction(string fireId, string cellId, int modelRunId) => new()
    {
        FireId = fireId,
        CellId = cellId,
        ModelRunId = modelRunId,
        PredictionStatus = "no_data",
        RecoveryGapPred = null,
        DefaultPriorityScore = null,
        DefaultPriorityClass = null,
    };
}
