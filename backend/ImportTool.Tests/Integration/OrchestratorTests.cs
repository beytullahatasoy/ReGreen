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
    public async Task WeightSumOverflowsToInfinity_RejectedFatalAtManifestLevel()
    {
        // Regresyon: her ağırlık bileşeni tek başına sonlu (1e308 < double.MaxValue) ama
        // toplamları taşıp double.PositiveInfinity'ye yuvarlanıyor — sadece "toplam <= 0"
        // kontrolü bunu YAKALAMAZ, IsFinite(toplam) da gerekli (ManifestValidator VE
        // FireValidator'da). manifest.json ve metadata.json'un priority_weights'i BİRLİKTE
        // bozuluyor ki ikisi eşit kalsın — aksi halde FireValidator'ın kendi intrinsic
        // kontrolüne varmadan önce PRIORITY_WEIGHTS_MISMATCH devreye girer. Bu, gerçek
        // pipeline'da manifest paket-geneli kontrol AYNI aşamada, per-fire kontrolden ÖNCE
        // çalıştığı için ManifestValidator'da FATAL olarak yakalanır — uçtan uca reddedildiği
        // doğrulanıyor (hangi iç kontrolün önce tetiklendiği değil).
        var fx = new SyntheticFireFixture("TESTF_2026_09");
        var overflowWeights = new { recovery = 1e308, erosion = 1e308, access = 1.0 };
        fx.MutateManifest(d => d["priority_weights"] = overflowWeights);
        fx.MutateMetadata(d => d["priority_weights"] = overflowWeights);
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(2, exitCode);
            Assert.Equal("fatal", report.Status);
            Assert.Equal("WEIGHTS_INVALID", report.FatalError?.Code);
            Assert.Empty(report.Fires);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(0, db.Fires.Count());
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
    public async Task FireWithHukumLayer_Imports_CellVerdictsAndFireNarrativeLandCorrectly()
    {
        // (a) hukumler.csv + narrative dosyaları mevcut bir yangın temiz import edilmeli ve
        // CellVerdicts/FireNarratives satırları doğru şekilde yazılmalı.
        var fx = new SyntheticFireFixture("TESTF_2026_11");
        fx.WriteHukumlerCsv(hukum: "DIKIM_ADAYI", ozet: "Aktif dikim adayı test özeti.");
        fx.WriteHukumSozlugu();
        fx.WriteNarrativeFiles(paragraf: "Test yangın paragrafı.", profil: "dik_arazi", onaylandi: true);
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, report.Summary.Ok);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(4, db.CellVerdicts.Count(v => v.FireId == fx.FireId));
            Assert.All(fx.CellIds, cellId =>
                Assert.True(db.CellVerdicts.Any(v => v.CellId == cellId && v.Hukum == "DIKIM_ADAYI")));

            var narrative = db.FireNarratives.Single(n => n.FireId == fx.FireId);
            Assert.Equal("Test yangın paragrafı.", narrative.Paragraf);
            Assert.Equal("dik_arazi", narrative.Profil);
            Assert.True(narrative.Onaylandi);
            Assert.Equal("sablon (deterministik)", narrative.Uretim);
            Assert.Contains(fx.FireId, narrative.SayiBlogu);

            var sozluk = db.HukumSozlugu.Single();
            Assert.Equal("1.1", sozluk.Surum);
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task FireWithoutHukumLayer_StillImportsFine_BackwardCompatible()
    {
        // (b) hukumler.csv/narrative YOK — mevcut davranış (hüküm katmanından önceki) korunmalı.
        var fx = new SyntheticFireFixture("TESTF_2026_12");
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, report.Summary.Ok);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(4, db.Cells.Count());
            Assert.Equal(0, db.CellVerdicts.Count());
            Assert.Equal(0, db.FireNarratives.Count());
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task HukumCellIdMismatch_FailsWithHukumCellMismatch()
    {
        // (c) hucreler.csv <-> hukumler.csv cell_id uyuşmazlığı.
        var fx = new SyntheticFireFixture("TESTF_2026_13");
        fx.WriteHukumlerCsv(cellIdOverride: "TESTF_2026_13_999999");
        fx.WriteHukumSozlugu();
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(1, exitCode);
            Assert.Equal(1, report.Summary.Failed);
            Assert.Equal("HUKUM_CELL_MISMATCH", report.Fires[0].ErrorCode);

            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(0, db.Fires.Count()); // yangının hiçbir parçası yazılmadı
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task SecondRun_UnchangedHukumData_IsIdempotent()
    {
        // (d) aynı hüküm verisiyle ikinci çalıştırma no-op olmalı (AlreadyImported).
        var fx = new SyntheticFireFixture("TESTF_2026_14");
        fx.WriteHukumlerCsv(hukum: "IZLE");
        fx.WriteHukumSozlugu();
        try
        {
            await Run(fx);
            await using (var db = DatabaseFixture.CreateContext())
                Assert.Equal(4, db.CellVerdicts.Count(v => v.FireId == fx.FireId));

            var options = new CliOptions
            {
                ManifestPath = fx.ManifestPath, ReportPath = Path.Combine(fx.Dir, "r2.json"),
                ConnectionString = DatabaseFixture.ConnectionString,
            };
            var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, report.Summary.AlreadyImported);

            await using var db2 = DatabaseFixture.CreateContext();
            Assert.Equal(4, db2.CellVerdicts.Count(v => v.FireId == fx.FireId)); // çoğalmadı
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task SecondModelRun_DifferentHukumForSameCell_IsVersioned()
    {
        // Aynı hücre, yeni model teslimatında farklı hüküm alabilir; eski run korunur.
        var fx = new SyntheticFireFixture("TESTF_2026_15", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        fx.WriteHukumlerCsv(hukum: "IZLE");
        fx.WriteHukumSozlugu();
        try
        {
            await Run(fx);

            var fx2 = new SyntheticFireFixture(fx.FireId, DateTimeOffset.Parse("2026-02-01T00:00:00+00:00"));
            fx2.WriteHukumlerCsv(hukum: "DIKIM_ADAYI"); // AYNI hücreler için FARKLI hüküm
            fx2.WriteHukumSozlugu();
            try
            {
                var options = new CliOptions
                {
                    ManifestPath = fx2.ManifestPath, ReportPath = Path.Combine(fx2.Dir, "r.json"),
                    ConnectionString = DatabaseFixture.ConnectionString,
                };
                var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(options, TextWriter.Null, TextWriter.Null);

                Assert.Equal(0, exitCode);
                Assert.Equal(1, report.Summary.Ok);

                await using var db = DatabaseFixture.CreateContext();
                Assert.Equal(2, db.ModelRuns.Count(m => m.FireId == fx.FireId));
                Assert.Equal(8, db.CellVerdicts.Count(v => v.FireId == fx.FireId));
                var latestRunId = db.ModelRuns.Where(m => m.FireId == fx.FireId)
                    .OrderByDescending(m => m.GeneratedAt).Select(m => m.Id).First();
                Assert.All(fx.CellIds, cellId =>
                    Assert.True(db.CellVerdicts.Any(v => v.CellId == cellId
                        && v.ModelRunId == latestRunId && v.Hukum == "DIKIM_ADAYI")));
            }
            finally { fx2.Cleanup(); }
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task NarrativeFilePair_WhenOneFileIsMissing_IsFatal()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_17");
        fx.WriteNarrativeFiles();
        File.Delete(Path.Combine(fx.Dir, "yangin_ozetleri.json"));
        try
        {
            var (exitCode, report) = await Run(fx);

            Assert.Equal(2, exitCode);
            Assert.Equal("NARRATIVE_FILE_PAIR_INCOMPLETE", report.FatalError?.Code);
            await using var db = DatabaseFixture.CreateContext();
            Assert.Empty(db.Fires);
        }
        finally { fx.Cleanup(); }
    }

    [LocalDbFact]
    public async Task DryRun_AlreadyImportedRunWithDifferentHukum_IsRejectedWithoutWriting()
    {
        var fx = new SyntheticFireFixture("TESTF_2026_16");
        fx.WriteHukumlerCsv(hukum: "IZLE");
        fx.WriteHukumSozlugu();
        try
        {
            await Run(fx);
            fx.WriteHukumlerCsv(hukum: "DIKIM_ADAYI");

            var options = new CliOptions
            {
                ManifestPath = fx.ManifestPath, DryRun = true,
                ReportPath = Path.Combine(fx.Dir, "dry-hukum-report.json"),
                ConnectionString = DatabaseFixture.ConnectionString,
            };
            var (exitCode, report) = await ImportTool.Orchestrator.RunAsync(
                options, TextWriter.Null, TextWriter.Null);

            Assert.Equal(1, exitCode);
            Assert.Equal("HUKUM_MISMATCH", report.Fires[0].ErrorCode);
            await using var db = DatabaseFixture.CreateContext();
            Assert.Equal(1, db.ModelRuns.Count(m => m.FireId == fx.FireId));
            Assert.All(db.CellVerdicts, verdict => Assert.Equal("IZLE", verdict.Hukum));
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
