using ImportTool.Cli;
using ImportTool.Reporting;
using Xunit;

namespace ImportTool.Tests.Integration;

/// <summary>
/// Gerçek, benzersiz adlı geçici LocalDB veritabanı üzerinden uçtan uca — docs/import-flow.md'nin
/// tarif ettiği tüm exit code/durum sözleşmesini ve v1.1 review'ında bulunan
/// regresyonları (required alan, exception containment, dry-run sırası) kapsar.
/// </summary>
[Collection("Database")]
public class OrchestratorTests
{
    private static async Task<(int ExitCode, ImportReport Report)> Run(SyntheticFireFixture fx, bool dryRun = false, bool allowPartial = false)
    {
        await DatabaseFixture.ResetAsync();
        var options = new CliOptions
        {
            ManifestPath = fx.ManifestPath, DryRun = dryRun, AllowPartial = allowPartial,
            ReportPath = Path.Combine(fx.Dir, "report.json"), ConnectionString = DatabaseFixture.ConnectionString,
        };
        return await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);
    }

    [LocalDbFact]
    public async Task FreshFire_Imports_Ok_ExitCodeZero()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_01");
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(0, exitCode);
            Assert.Equal("success", report.Status);
            Assert.Equal(1, report.Summary.Ok);
            Assert.Equal(4, report.Summary.CellsInserted);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(1, db.Fires.Count());
            Assert.Equal(4, db.Cells.Count());
            Assert.Equal(4, db.Predictions.Count());
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task SecondRun_SamePackage_AlreadyImported_NoDuplication()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_02");
        try
        {
            await Run(fx);
            await using (var db = DatabaseFixture.CreateContext())
                Assert.Equal(4, db.Cells.Count());

            // ResetAsync YOK burada — ayni paketi bilerek TEKRAR calistiriyoruz.
            var options = new CliOptions
            {
                ManifestPath = fx.ManifestPath, ReportPath = Path.Combine(fx.Dir, "r2.json"),
                ConnectionString = DatabaseFixture.ConnectionString,
            };
            var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, report.Summary.AlreadyImported);
            Assert.Equal(0, report.Summary.Ok);

            await using var db2 = DatabaseFixture.CreateContext();
            Assert.Equal(4, db2.Cells.Count()); // cogalmadi
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task DryRun_ValidatesButWritesNothing()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_03");
        try
        {
            var (exitCode, report) = await Run(fx, dryRun: true);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, report.Summary.Validated);
            Assert.Equal(0, report.Summary.Ok);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(0, db.Fires.Count());
            Assert.Equal(0, db.Cells.Count());
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task MissingFile_ReportsSkipped_DefaultExitCodeOne()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_04");
        File.Delete(Path.Combine(fx.Dir, $"{fx.FireId}_sinir.geojson"));
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(1, exitCode); // allow-partial verilmedi
            // Tek yangın vardı ve o da skipped oldu — hiçbir yangın başarıyla
            // sonuçlanmadığı için üst durum "partial" değil "failed" (bkz. Finalize_).
            Assert.Equal("failed", report.Status);
            Assert.Equal(1, report.Summary.Skipped);
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task MissingFile_WithAllowPartial_ExitCodeZero()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_05");
        File.Delete(Path.Combine(fx.Dir, $"{fx.FireId}_sinir.geojson"));
        try
        {
            var (exitCode, report) = await Run(fx, allowPartial: true);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, report.Summary.Skipped);
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task UnsupportedSchemaVersion_FatalExitCodeTwo()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_06");
        var manifestJson = await File.ReadAllTextAsync(fx.ManifestPath);
        var patched = manifestJson.Replace("\"schema_version\": \"1.1\"", "\"schema_version\": \"9.9\"");
        await File.WriteAllTextAsync(fx.ManifestPath, patched);
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(2, exitCode);
            Assert.Equal("fatal", report.Status);
            Assert.Equal("UNSUPPORTED_SCHEMA_VERSION", report.FatalError?.Code);
            Assert.Empty(report.Fires);
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task MissingRequiredMetadataField_FailsThatFire_DoesNotDefaultSilently()
    {
        // v1.1 review bulgusu #1'in regresyon testi: model_version silinirse "" olarak
        // sessizce kabul EDİLMEMELİ, failed olarak raporlanmalı.
        var fx = new SyntheticFireFixture("TESTF_2026_07");
        fx.MutateMetadata(d => d.Remove("model_version"));
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(1, exitCode);
            Assert.Equal(1, report.Summary.Failed);
            Assert.Equal("METADATA_PARSE_ERROR", report.Fires[0].ErrorCode);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(0, db.Fires.Count()); // DB'ye hicbir sey yazilmadi
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task MalformedRow_DoesNotCrashRun_OtherFiresStillProcessed()
    {
        // v1.1 review bulgusu #3'un regresyon testi: bos cell_id NullReferenceException
        // firlatip TUM calismayi dusurmemeli, sadece o yangin failed olmali.
        var fxBad = new SyntheticFireFixture("TESTF_2026_08A");
        var fxGood = new SyntheticFireFixture("TESTF_2026_08B");
        fxBad.MutateCsvRow(1, cols => { cols[1] = ""; return cols; }); // cell_id'yi bosalt

        // İki yangını TEK manifest altında birleştir.
        var combinedDir = Path.Combine(Path.GetTempPath(), "regreen-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(combinedDir);
        foreach (var f in Directory.GetFiles(fxBad.Dir).Where(f => !f.EndsWith("manifest.json")))
            File.Copy(f, Path.Combine(combinedDir, Path.GetFileName(f)));
        foreach (var f in Directory.GetFiles(fxGood.Dir).Where(f => !f.EndsWith("manifest.json")))
            File.Copy(f, Path.Combine(combinedDir, Path.GetFileName(f)));

        var manifestA = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(fxBad.ManifestPath)).RootElement;
        var manifestB = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(fxGood.ManifestPath)).RootElement;
        var combinedManifest = System.Text.Json.Nodes.JsonNode.Parse(manifestA.GetRawText())!;
        combinedManifest["fire_count"] = 2;
        combinedManifest["total_cells"] = manifestA.GetProperty("total_cells").GetInt32() + manifestB.GetProperty("total_cells").GetInt32();
        var firesArray = new System.Text.Json.Nodes.JsonArray();
        firesArray.Add(System.Text.Json.Nodes.JsonNode.Parse(manifestA.GetProperty("fires")[0].GetRawText()));
        firesArray.Add(System.Text.Json.Nodes.JsonNode.Parse(manifestB.GetProperty("fires")[0].GetRawText()));
        combinedManifest["fires"] = firesArray;
        var combinedManifestPath = Path.Combine(combinedDir, "manifest.json");
        await File.WriteAllTextAsync(combinedManifestPath, combinedManifest.ToJsonString());

        try
        {
            await DatabaseFixture.ResetAsync();
            var options = new CliOptions
            {
                ManifestPath = combinedManifestPath, ReportPath = Path.Combine(combinedDir, "report.json"),
                ConnectionString = DatabaseFixture.ConnectionString,
            };
            var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);

            Assert.Equal(1, exitCode);
            Assert.Equal(1, report.Summary.Failed);
            Assert.Equal(1, report.Summary.Ok); // digeri hala islendi, process cokmedi
        }
        finally
        {
            fxBad.Cleanup();
            fxGood.Cleanup();
            try { Directory.Delete(combinedDir, true); } catch { }
        }
    }

    [LocalDbFact]
    public async Task TamperedCell_RejectedOnNewDelivery_InsertOrVerify()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_09", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        try
        {
            await Run(fx);

            // Ayni yangin icin FARKLI bir teslimat (yeni generated_at) hazirla, ama
            // DB'deki hucreyi araya elle bozarak "kor upsert" riskini test et.
            await using (var db = DatabaseFixture.CreateContext())
            {
                var cell = db.Cells.First(c => c.FireId == fx.FireId);
                cell.TreeCover = 0.9999;
                await db.SaveChangesAsync();
            }

            var fx2 = new SyntheticFireFixture(fx.FireId, DateTimeOffset.Parse("2026-02-01T00:00:00+00:00"));
            try
            {
                var options = new CliOptions
                {
                    ManifestPath = fx2.ManifestPath, ReportPath = Path.Combine(fx2.Dir, "r.json"),
                    ConnectionString = DatabaseFixture.ConnectionString,
                };
                var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);

                Assert.Equal(1, exitCode);
                Assert.Equal("CELL_MISMATCH", report.Fires[0].ErrorCode);

                await using var db2 = DatabaseFixture.CreateContext();
                Assert.Equal(1, db2.ModelRuns.Count(m => m.FireId == fx.FireId)); // ikinci ModelRun YAZILMADI (rollback)
            }
            finally { fx2.Cleanup(); }
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task DryRun_TamperedExistingCell_IsRejectedWithoutWriting()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_10", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        try
        {
            await Run(fx);
            await using (var db = DatabaseFixture.CreateContext())
            {
                var cell = db.Cells.First(c => c.FireId == fx.FireId);
                cell.TreeCover = 0.9999;
                await db.SaveChangesAsync();
            }

            var next = new SyntheticFireFixture(fx.FireId, DateTimeOffset.Parse("2026-02-01T00:00:00+00:00"));
            try
            {
                await using var beforeDb = DatabaseFixture.CreateContext();
                var beforeRuns = beforeDb.ModelRuns.Count(m => m.FireId == fx.FireId);
                var options = new CliOptions
                {
                    ManifestPath = next.ManifestPath, DryRun = true,
                    ReportPath = Path.Combine(next.Dir, "dry-report.json"),
                    ConnectionString = DatabaseFixture.ConnectionString,
                };
                var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);

                Assert.Equal(1, exitCode);
                Assert.Equal("CELL_MISMATCH", report.Fires[0].ErrorCode);
                await using var db = DatabaseFixture.CreateContext();
                Assert.Equal(beforeRuns, db.ModelRuns.Count(m => m.FireId == fx.FireId));
            }
            finally { next.Cleanup(); }
        }
        finally { fx.Cleanup(); }
    }
}
