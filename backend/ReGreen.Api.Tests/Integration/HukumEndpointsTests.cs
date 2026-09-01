using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ReGreen.Api.Dtos;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// docs/hukum_sozlesmesi.md'deki 3 hüküm katmanı endpoint'ini gerçek LocalDB'ye karşı
/// uçtan uca test eder — FireEndpointsTests ile AYNI fixture/izolasyon deseni
/// (bkz. plan §6/§8). Her test kendi verisini seed eder.
/// </summary>
[Collection("Database")]
#pragma warning disable CS9113 // xUnit collection fixture DI'si için gerekli, gövdede kullanılmıyor.
public class HukumEndpointsTests(DatabaseFixture fixture) : IAsyncLifetime
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

    private static async Task SeedFireWithCellAsync(
        string fireId = SeedHelper.FireId, string cellId = "C1", bool withVerdict = true, bool withNarrative = true)
    {
        await using var db = DatabaseFixture.CreateContext();
        db.Fires.Add(SeedHelper.BuildFire(fireId));
        db.Cells.Add(SeedHelper.BuildCell(fireId, cellId, 37.00, 35.00));
        db.HukumSozlugu.Add(SeedHelper.BuildHukumSozlugu());
        var modelRun = SeedHelper.BuildModelRun(fireId, "ridge_v2", DateTimeOffset.Parse("2026-08-23T16:13:19+00:00"));
        db.ModelRuns.Add(modelRun);
        await db.SaveChangesAsync();

        if (withVerdict)
        {
            db.CellVerdicts.Add(SeedHelper.BuildCellVerdict(
                fireId, cellId, modelRun.Id, hukum: "EROZYON_ONCE", ekKosullar: "AGIR_YANMIS|DIK_YAMAC"));
        }
        if (withNarrative)
        {
            db.FireNarratives.Add(SeedHelper.BuildFireNarrative(fireId, modelRun.Id));
        }
        await db.SaveChangesAsync();
    }

    [LocalDbFact]
    public async Task GetCellVerdict_HappyPath_ReturnsExpectedShape()
    {
        await SeedFireWithCellAsync();
        using var client = _factory.CreateClient();

        var dto = await client.GetFromJsonAsync<CellVerdictDto>(
            $"/api/fires/{SeedHelper.FireId}/cells/C1/hukum");

        Assert.Equal("C1", dto!.CellId);
        Assert.True(dto.ModelRunId > 0);
        Assert.Equal("ridge_v2", dto.ModelVersion);
        Assert.Equal("1.1", dto.HukumVersion);
        Assert.Equal("EROZYON_ONCE", dto.Hukum);
        Assert.Equal(["AGIR_YANMIS", "DIK_YAMAC"], dto.EkKosullar);
        Assert.Equal(0.42, dto.ToparlanmaOrani);
        Assert.Equal("Kızılçam, Karaçam", dto.TurOnerisi);
        Assert.Equal("Test özeti.", dto.Ozet);
        Assert.Equal("Test ayrıntısı.", dto.Ayrinti);
        Assert.True(dto.ZamanlamaNotuVar);
    }

    [LocalDbFact]
    public async Task GetCellVerdict_EmptyEkKosullar_ReturnsEmptyArray()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            db.Fires.Add(SeedHelper.BuildFire());
            db.Cells.Add(SeedHelper.BuildCell(SeedHelper.FireId, "C1", 37.00, 35.00));
            db.HukumSozlugu.Add(SeedHelper.BuildHukumSozlugu());
            var modelRun = SeedHelper.BuildModelRun(SeedHelper.FireId, "ridge_v2", DateTimeOffset.Parse("2026-08-23T16:13:19+00:00"));
            db.ModelRuns.Add(modelRun);
            await db.SaveChangesAsync();
            db.CellVerdicts.Add(SeedHelper.BuildCellVerdict(SeedHelper.FireId, "C1", modelRun.Id, ekKosullar: null));
            await db.SaveChangesAsync();
        }
        using var client = _factory.CreateClient();

        var dto = await client.GetFromJsonAsync<CellVerdictDto>(
            $"/api/fires/{SeedHelper.FireId}/cells/C1/hukum");

        Assert.Empty(dto!.EkKosullar);
    }

    [LocalDbFact]
    public async Task GetCellVerdict_FireNotFound_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/fires/DOES_NOT_EXIST/cells/C1/hukum");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "FIRE_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetCellVerdict_CellNotFound_Returns404()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            db.Fires.Add(SeedHelper.BuildFire());
            await db.SaveChangesAsync();
        }
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells/DOES_NOT_EXIST/hukum");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "CELL_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetCellVerdict_CellExistsButNoVerdict_Returns404()
    {
        await SeedFireWithCellAsync(withVerdict: false);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/cells/C1/hukum");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "CELL_VERDICT_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetFireSummary_HappyPath_ReturnsExpectedShape()
    {
        await SeedFireWithCellAsync();
        using var client = _factory.CreateClient();

        var dto = await client.GetFromJsonAsync<FireNarrativeDto>($"/api/fires/{SeedHelper.FireId}/summary");

        Assert.Equal(SeedHelper.FireId, dto!.FireId);
        Assert.True(dto.ModelRunId > 0);
        Assert.Equal("ridge_v2", dto.ModelVersion);
        Assert.Equal("1.0", dto.NarrativeVersion);
        Assert.Equal("Test paragrafı.", dto.Paragraf);
        Assert.Equal("karisik", dto.Profil);
        Assert.True(dto.Onaylandi);
        Assert.Equal("sablon (deterministik)", dto.Uretim);
        Assert.Equal(SeedHelper.FireId, dto.SayiBlogu.GetProperty("fire_id").GetString());
    }

    [LocalDbFact]
    public async Task GetCellVerdict_MultipleModelRuns_ReturnsLatestRunsVerdict()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            db.Fires.Add(SeedHelper.BuildFire());
            db.Cells.Add(SeedHelper.BuildCell(SeedHelper.FireId, "C1", 37.00, 35.00));
            db.HukumSozlugu.Add(SeedHelper.BuildHukumSozlugu());
            var oldRun = SeedHelper.BuildModelRun(SeedHelper.FireId, "ridge_v2", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
            var newRun = SeedHelper.BuildModelRun(SeedHelper.FireId, "ridge_v3", DateTimeOffset.Parse("2026-02-01T00:00:00+00:00"));
            db.ModelRuns.AddRange(oldRun, newRun);
            await db.SaveChangesAsync();
            db.CellVerdicts.Add(SeedHelper.BuildCellVerdict(SeedHelper.FireId, "C1", oldRun.Id, hukum: "IZLE"));
            db.CellVerdicts.Add(SeedHelper.BuildCellVerdict(SeedHelper.FireId, "C1", newRun.Id, hukum: "DIKIM_ADAYI"));
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var dto = await client.GetFromJsonAsync<CellVerdictDto>(
            $"/api/fires/{SeedHelper.FireId}/cells/C1/hukum");

        Assert.Equal("ridge_v3", dto!.ModelVersion);
        Assert.Equal("DIKIM_ADAYI", dto.Hukum);
    }

    [LocalDbFact]
    public async Task GetFireSummary_FireNotFound_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/fires/DOES_NOT_EXIST/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "FIRE_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetFireSummary_FireExistsButNoNarrative_Returns404()
    {
        await SeedFireWithCellAsync(withNarrative: false);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/{SeedHelper.FireId}/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "FIRE_NARRATIVE_NOT_FOUND");
    }

    [LocalDbFact]
    public async Task GetHukumSozlugu_HappyPath_ReturnsRawJsonPassthrough()
    {
        await using (var db = DatabaseFixture.CreateContext())
        {
            db.HukumSozlugu.Add(SeedHelper.BuildHukumSozlugu());
            await db.SaveChangesAsync();
        }
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/hukum-sozlugu");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1.1", doc.RootElement.GetProperty("surum").GetString());
        Assert.Equal("tr", doc.RootElement.GetProperty("dil").GetString());
    }

    [LocalDbFact]
    public async Task GetHukumSozlugu_NotImported_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/hukum-sozlugu");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemCode(response, "HUKUM_SOZLUGU_NOT_FOUND");
    }

    private static async Task AssertProblemCode(HttpResponseMessage response, string expectedCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("code").GetString());
    }
}
