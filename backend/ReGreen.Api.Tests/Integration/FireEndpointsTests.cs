using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Dtos;
using ReGreen.Core.Priority;
using ReGreen.Data.Entities;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// 3 endpoint'i gerçek LocalDB'ye karşı uçtan uca test eder — bkz. plan §6/§8.
/// Her test kendi verisini seed eder, `DatabaseFixture.ResetAsync()` ile öncekinden
/// izole edilir. `ApiFactory`, DB bağlantısını bu testin benzersiz LocalDB'sine
/// sabitler (gerçek geliştirme `ReGreen` DB'sine asla dokunmaz).
/// </summary>
[Collection("Database")]
#pragma warning disable CS9113 // xUnit collection fixture DI'si için gerekli, gövdede kullanılmıyor.
public class FireEndpointsTests(DatabaseFixture fixture) : IAsyncLifetime
#pragma warning restore CS9113
{
    private readonly ApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        if (!LocalDbFactAttribute.Enabled) return;
        await DatabaseFixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Standart senaryo: 1 yangın, 1 ModelRun, 4 hücre — 2 predicted (biri bbox İÇİNDE,
    /// biri DIŞINDA), 1 low_severity, 1 no_data. DefaultPriorityScore/Class, gerçek
    /// PriorityCalculator ile hesaplanıp seed edilir (elle uydurulmuş sayı yok).
    /// </summary>
    private static async Task<ModelRun> SeedStandardFireAsync(string fireId = SeedHelper.FireId)
    {
        await using var db = DatabaseFixture.CreateContext();

        var fire = SeedHelper.BuildFire(fireId);
        var modelRun = SeedHelper.BuildModelRun(fireId, "rf_v1", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        db.Fires.Add(fire);
        db.ModelRuns.Add(modelRun);
        await db.SaveChangesAsync();

        var norm = new NormalizationReference
        {
            RecoveryGapPred = new NormRange { Min = modelRun.NormRecoveryGapMin, Max = modelRun.NormRecoveryGapMax },
            SlopeDeg = new NormRange { Min = modelRun.NormSlopeMin, Max = modelRun.NormSlopeMax },
            RoadDistanceKm = new NormRange { Min = modelRun.NormRoadDistMin, Max = modelRun.NormRoadDistMax },
        };
        var defaultWeights = new PriorityWeights
        {
            Recovery = modelRun.DefaultWeightRecovery,
            Erosion = modelRun.DefaultWeightErosion,
            Access = modelRun.DefaultWeightAccess,
        };
        var thresholds = new PriorityThresholds
        {
            CokYuksek = modelRun.ThresholdVeryHigh, Yuksek = modelRun.ThresholdHigh, Orta = modelRun.ThresholdMedium,
        };

        // C1: bbox İÇİNDE (lat 37 / lon 35 civarı).
        const double c1Recovery = 0.30, c1Slope = 10.0, c1Road = 1.0;
        var c1Score = PriorityCalculator.ComputeScore(c1Recovery, c1Slope, c1Road, norm, defaultWeights);
        var c1Class = PriorityCalculator.Classify(c1Score, thresholds);

        // C2: bbox DIŞINDA (lat 38 / lon 36).
        const double c2Recovery = 0.45, c2Slope = 15.0, c2Road = 0.5;
        var c2Score = PriorityCalculator.ComputeScore(c2Recovery, c2Slope, c2Road, norm, defaultWeights);
        var c2Class = PriorityCalculator.Classify(c2Score, thresholds);

        db.Cells.AddRange(
            SeedHelper.BuildCell(fireId, "C1", 37.00, 35.00, c1Slope, c1Road),
            SeedHelper.BuildCell(fireId, "C2", 38.00, 36.00, c2Slope, c2Road),
            SeedHelper.BuildCell(fireId, "C3", 37.01, 35.01, severityClass: "dusuk"),
            SeedHelper.BuildCell(fireId, "C4", 37.02, 35.02, severityClass: "dusuk"));

        db.Predictions.AddRange(
            SeedHelper.BuildPredictedPrediction(fireId, "C1", modelRun.Id, c1Recovery, c1Score, c1Class),
            SeedHelper.BuildPredictedPrediction(fireId, "C2", modelRun.Id, c2Recovery, c2Score, c2Class),
            SeedHelper.BuildLowSeverityPrediction(fireId, "C3", modelRun.Id),
            SeedHelper.BuildNoDataPrediction(fireId, "C4", modelRun.Id));

        await db.SaveChangesAsync();
        return modelRun;
    }

    [LocalDbFact]
    public async Task GetFires_ReturnsAllFires_WithCorrectCellCount()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var fires = await client.GetFromJsonAsync<List<FireSummaryDto>>("/api/fires");

        var fire = Assert.Single(fires!);
        Assert.Equal(SeedHelper.FireId, fire.FireId);
        Assert.Equal(4, fire.CellCount);
        Assert.True(fire.HasPerimeter);
    }

    [LocalDbFact]
    public async Task GetFires_FiltersByQualityFlag()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            db.Fires.Add(SeedHelper.BuildFire("TEST_2026_01", "ok"));
            db.Fires.Add(SeedHelper.BuildFire("TEST_2026_02", "check"));
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var fires = await client.GetFromJsonAsync<List<FireSummaryDto>>("/api/fires?quality_flag=check");

        var fire = Assert.Single(fires!);
        Assert.Equal("TEST_2026_02", fire.FireId);
    }

    [LocalDbFact]
    public async Task GetFires_InvalidQualityFlag_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/fires?quality_flag=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_QUERY_PARAMETER");
    }

    [LocalDbFact]
    public async Task GetPerimeter_ReturnsValidGeoJsonAndMarker()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var dto = await client.GetFromJsonAsync<FirePerimeterDto>($"/api/fires/{SeedHelper.FireId}/perimeter");

        Assert.Equal(SeedHelper.FireId, dto!.FireId);
        Assert.Equal(37.005, dto.Marker.Lat);
        Assert.Equal("Feature", dto.Perimeter.GetProperty("type").GetString());
        Assert.Equal("Polygon", dto.Perimeter.GetProperty("geometry").GetProperty("type").GetString());
    }

    [LocalDbFact]
    public async Task GetPerimeter_FireNotFound_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/fires/DOES_NOT_EXIST/perimeter");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "FIRE_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetPerimeter_CorruptGeoJson_Returns500()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            var fire = SeedHelper.BuildFire();
            fire.PerimeterGeoJson = "bu gecerli bir JSON degil {{{";
            db.Fires.Add(fire);
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/perimeter");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertProblemCode(response, "PERIMETER_DATA_CORRUPT");
    }

    [LocalDbFact]
    public async Task GetCells_FireNotFound_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/fires/DOES_NOT_EXIST/cells");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "FIRE_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetCells_FireExistsButNoModelRun_Returns404()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            db.Fires.Add(SeedHelper.BuildFire());
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "MODEL_RUN_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetCells_DefaultWeights_ReturnsDbScoresUnchanged()
    {
        var modelRun = await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<CellsResponseDto>($"/api/fires/{SeedHelper.FireId}/cells");

        Assert.Equal(modelRun.Id, response!.ModelRunId);
        Assert.Equal("EPSG:4326", response.Crs);
        Assert.Equal(250, response.CellSizeM);
        Assert.Equal(4, response.Count);
        Assert.Equal(4, response.Items.Count);

        await using var db = DatabaseFixture.CreateContext();
        foreach (var item in response.Items)
        {
            var expected = await db.Predictions.SingleAsync(p => p.CellId == item.CellId);
            Assert.Equal(expected.DefaultPriorityScore, item.PriorityScore);
            Assert.Equal(expected.DefaultPriorityClass, item.PriorityClass);
        }
    }

    [LocalDbFact]
    public async Task GetCells_CustomWeights_RecomputesOnlyPredictedCells()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<CellsResponseDto>(
            $"/api/fires/{SeedHelper.FireId}/cells?recovery=0.8&erosion=0.1&access=0.1");

        Assert.Equal(0.8, response!.AppliedWeights.Recovery, precision: 9);
        Assert.Equal(0.1, response.AppliedWeights.Erosion, precision: 9);
        Assert.Equal(0.1, response.AppliedWeights.Access, precision: 9);

        var norm = new NormalizationReference
        {
            RecoveryGapPred = new NormRange { Min = 0.10, Max = 0.50 },
            SlopeDeg = new NormRange { Min = 1.0, Max = 20.0 },
            RoadDistanceKm = new NormRange { Min = 0.05, Max = 2.0 },
        };
        var customWeights = new PriorityWeights { Recovery = 0.8, Erosion = 0.1, Access = 0.1 };
        var thresholds = new PriorityThresholds { CokYuksek = 0.75, Yuksek = 0.50, Orta = 0.25 };

        var c1 = response.Items.Single(c => c.CellId == "C1");
        var expectedC1Score = PriorityCalculator.ComputeScore(0.30, 10.0, 1.0, norm, customWeights);
        Assert.Equal(expectedC1Score, c1.PriorityScore);
        Assert.Equal(PriorityCalculator.Classify(expectedC1Score, thresholds), c1.PriorityClass);

        // low_severity / no_data AYNEN kalır — ağırlık değişse bile backend yorum katmaz.
        var c3 = response.Items.Single(c => c.CellId == "C3");
        Assert.Equal(0.0, c3.PriorityScore);
        Assert.Equal("DUSUK", c3.PriorityClass);

        var c4 = response.Items.Single(c => c.CellId == "C4");
        Assert.Null(c4.PriorityScore);
        Assert.Null(c4.PriorityClass);
    }

    /// <summary>
    /// `priority_class` filtresinin, DB'deki VARSAYILAN sınıfa değil, özel ağırlıkla
    /// YENİDEN HESAPLANMIŞ sınıfa göre uygulandığını kanıtlar. C1'in normalize edilmiş
    /// recovery_gap_pred değeri tam 0.5 olacak şekilde seed edilmiştir (bkz.
    /// SeedStandardFireAsync: recoveryGapPred=0.30, norm min/max=0.10/0.50) — varsayılan
    /// ağırlıkla (0.5/0.3/0.2) C1'in sınıfı ORTA'dır, ama SADECE recovery ağırlığıyla
    /// (1/0/0) skor tam eşik değeri 0.50'ye çıkar ve sınıf YUKSEK'e döner.
    /// </summary>
    [LocalDbFact]
    public async Task GetCells_PriorityClassFilter_AppliesToRecomputedValue_NotDbDefault()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        await using (var db = DatabaseFixture.CreateContext())
        {
            var c1Default = await db.Predictions.SingleAsync(p => p.CellId == "C1");
            Assert.Equal("ORTA", c1Default.DefaultPriorityClass); // varsayımın hâlâ doğru olduğunu doğrula
        }

        var response = await client.GetFromJsonAsync<CellsResponseDto>(
            $"/api/fires/{SeedHelper.FireId}/cells?recovery=1&erosion=0&access=0&priority_class=YUKSEK");

        var c1 = Assert.Single(response!.Items);
        Assert.Equal("C1", c1.CellId);
        Assert.Equal(0.5, c1.PriorityScore);
        Assert.Equal("YUKSEK", c1.PriorityClass);
    }

    [LocalDbFact]
    public async Task GetCells_OneWeightOnly_Returns400()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells?recovery=0.8");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_PRIORITY_WEIGHTS");
    }

    [LocalDbFact]
    public async Task GetCells_NegativeWeight_Returns400()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/fires/{SeedHelper.FireId}/cells?recovery=-0.1&erosion=0.3&access=0.2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_PRIORITY_WEIGHTS");
    }

    [LocalDbFact]
    public async Task GetCells_ZeroSumWeights_Returns400()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells?recovery=0&erosion=0&access=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_PRIORITY_WEIGHTS");
    }

    [LocalDbFact]
    public async Task GetCells_BoundingBoxFilter_ReturnsOnlyMatchingCells()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        // C1/C3/C4 lat~37/lon~35 civarında, C2 lat=38/lon=36 — bbox C2'yi dışarıda bırakır.
        var response = await client.GetFromJsonAsync<CellsResponseDto>(
            $"/api/fires/{SeedHelper.FireId}/cells?min_lon=34.5&min_lat=36.5&max_lon=35.5&max_lat=37.5");

        Assert.Equal(3, response!.Count);
        Assert.DoesNotContain(response.Items, c => c.CellId == "C2");
    }

    [LocalDbFact]
    public async Task GetCells_PartialBoundingBox_Returns400()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells?min_lon=34.5&min_lat=36.5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_BOUNDING_BOX");
    }

    [LocalDbFact]
    public async Task GetCells_InvalidPredictionStatus_Returns400()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells?prediction_status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_QUERY_PARAMETER");
    }

    [LocalDbFact]
    public async Task GetCells_InvalidPriorityClass_Returns400()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells?priority_class=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCode(response, "INVALID_QUERY_PARAMETER");
    }

    [LocalDbFact]
    public async Task GetCells_PredictionStatusFilter_AppliedInSql()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<CellsResponseDto>(
            $"/api/fires/{SeedHelper.FireId}/cells?prediction_status=predicted");

        Assert.Equal(2, response!.Count);
        Assert.All(response.Items, c => Assert.Equal("predicted", c.PredictionStatus));
    }

    [LocalDbFact]
    public async Task GetCells_SameGeneratedAt_DifferentModelRuns_PicksHigherIdDeterministically()
    {
        const string fireId = "TEST_2026_03";
        var generatedAt = DateTimeOffset.Parse("2026-02-01T00:00:00+00:00");

        await using (var db = DatabaseFixture.CreateContext())
        {
            db.Fires.Add(SeedHelper.BuildFire(fireId));
            var older = SeedHelper.BuildModelRun(fireId, "rf_v1", generatedAt);
            var newer = SeedHelper.BuildModelRun(fireId, "rf_v2", generatedAt);
            db.ModelRuns.AddRange(older, newer);
            await db.SaveChangesAsync();

            // `newer` sonradan eklendiği için Id'si daha büyük — beklenen seçim budur.
            Assert.True(newer.Id > older.Id);
        }

        using var client = _factory.CreateClient();
        var response = await client.GetFromJsonAsync<CellsResponseDto>($"/api/fires/{fireId}/cells");

        await using var verifyDb = DatabaseFixture.CreateContext();
        var expectedNewerId = verifyDb.ModelRuns
            .Where(m => m.FireId == fireId)
            .OrderByDescending(m => m.GeneratedAt).ThenByDescending(m => m.Id)
            .Select(m => m.Id).First();

        Assert.Equal(expectedNewerId, response!.ModelRunId);
    }

    /// <summary>
    /// Regresyon: response compression Program.cs'de eklendi ve canlı API'ye karşı elle
    /// doğrulandı (bkz. docs/api-contract.md §7), ama bunu koruyan otomatik test yoktu —
    /// örn. `UseResponseCompression()` çağrısı yanlışlıkla silinse bile hiçbir test kırılmazdı.
    /// </summary>
    [LocalDbFact]
    public async Task GetCells_ResponseIsCompressed_WhenClientAcceptsIt()
    {
        await SeedStandardFireAsync();
        using var client = _factory.CreateClient();

        var compressedRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/fires/{SeedHelper.FireId}/cells");
        compressedRequest.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
        var compressedResponse = await client.SendAsync(compressedRequest);
        var compressedBytes = (await compressedResponse.Content.ReadAsByteArrayAsync()).Length;

        var identityRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/fires/{SeedHelper.FireId}/cells");
        identityRequest.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
        var identityResponse = await client.SendAsync(identityRequest);
        var identityBytes = (await identityResponse.Content.ReadAsByteArrayAsync()).Length;

        Assert.Contains("gzip", compressedResponse.Content.Headers.ContentEncoding);
        Assert.DoesNotContain("gzip", identityResponse.Content.Headers.ContentEncoding);
        Assert.True(compressedBytes < identityBytes,
            $"Sıkıştırılmış yanıt ({compressedBytes} bayt) sıkıştırılmamıştan ({identityBytes} bayt) küçük olmalı.");
    }

    private static async Task AssertProblemCode(HttpResponseMessage response, string expectedCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("code").GetString());
    }
}
