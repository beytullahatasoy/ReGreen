using System.Text.Json;
using ImportTool.Cli;
using ImportTool.Import;
using ImportTool.Models;
using ImportTool.Reporting;
using ImportTool.Validation;
using Microsoft.EntityFrameworkCore;
using ReGreen.Data;

namespace ImportTool;

/// <summary>
/// docs/import-flow.md'nin uçtan uca akışı. `Program.cs` sadece bunu çağırır — mantık
/// burada olduğu için testler gerçek bir process başlatmadan doğrudan çağırabilir.
/// </summary>
public static class Orchestrator
{
    public static async Task<(int ExitCode, ImportReport Report)> RunAsync(
        CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        var runId = Guid.NewGuid();
        var report = new ImportReport
        {
            RunId = runId.ToString(),
            ManifestPath = options.ManifestPath,
            DryRun = options.DryRun,
            AllowPartial = options.AllowPartial,
            StartedAt = DateTimeOffset.UtcNow,
        };

        var reportPath = ReportWriter.DefaultPath(options.ReportPath, runId);

        int FinishFatal(string code, string message)
        {
            stderr.WriteLine($"FATAL [{code}]: {message}");
            report.FatalError = new FatalError { Code = code, Message = message };
            report.FinishedAt = DateTimeOffset.UtcNow;
            report.Finalize_();
            if (!ReportWriter.TryWrite(report, reportPath, out _))
                stderr.WriteLine("Rapor dosyaya yazılamadı.");
            return 2;
        }

        // --- §3.1: manifest oku + FATAL ön kontroller ---
        Manifest manifest;
        string manifestDir;
        try
        {
            if (!File.Exists(options.ManifestPath))
                return (FinishFatal("MANIFEST_NOT_FOUND", $"Manifest bulunamadı: {options.ManifestPath}"), report);

            manifestDir = Path.GetDirectoryName(Path.GetFullPath(options.ManifestPath))!;
            var json = await File.ReadAllTextAsync(options.ManifestPath);
            manifest = JsonSerializer.Deserialize<Manifest>(json)
                ?? throw new JsonException("manifest.json boş/geçersiz.");
        }
        catch (Exception ex)
        {
            return (FinishFatal("MANIFEST_PARSE_ERROR", ex.Message), report);
        }

        (string Code, string Message)? manifestError;
        try
        {
            manifestError = ManifestValidator.Validate(manifest);
        }
        catch (Exception ex)
        {
            return (FinishFatal("MANIFEST_VALIDATION_ERROR", ex.Message), report);
        }
        if (manifestError is not null)
            return (FinishFatal(manifestError.Value.Code, manifestError.Value.Message), report);
        report.SchemaVersionChecked = manifest.SchemaVersion;

        // --- DB bağlantısı ---
        var connectionString = options.ConnectionString
            ?? Environment.GetEnvironmentVariable("REGREEN_CONNECTION_STRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ReGreen;Trusted_Connection=True;";

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;

        await using var db = new AppDbContext(dbOptions);
        try
        {
            var canConnect = await db.Database.CanConnectAsync();
            if (!canConnect)
                return (FinishFatal("DB_UNREACHABLE", $"Veritabanına bağlanılamadı ({connectionString})."), report);
        }
        catch (Exception ex)
        {
            return (FinishFatal("DB_UNREACHABLE", $"Veritabanına bağlanılamadı ({connectionString}): {ex.Message}"), report);
        }

        // --- §3.2-§3.6: yangın başına doğrulama + §4/§5: yazma ---
        var crossFireSeenCellIds = new Dictionary<string, string>();
        var validator = new FireValidator(manifestDir, manifest, crossFireSeenCellIds);
        var importer = new FireImporter(db, options.DryRun);

        stdout.WriteLine($"[{runId}] {manifest.Fires.Count} yangın işlenecek (dry-run={options.DryRun})...");

        foreach (var entry in manifest.Fires)
        {
            // Bir yangının doğrulama/import'unda beklenmeyen bir exception TÜM işlemi
            // düşürmesin — bu yangın 'failed' sayılır, diğerlerine devam edilir.
            try
            {
                var validation = validator.Validate(entry);

                if (validation.Kind == FireValidationKind.Skipped)
                {
                    stdout.WriteLine($"  {entry.FireId}: skipped — {validation.Reason}");
                    report.Fires.Add(new FireReportEntry
                    {
                        FireId = entry.FireId, Status = FireStatus.Skipped, Reason = validation.Reason,
                    });
                    continue;
                }

                if (validation.Kind == FireValidationKind.Failed)
                {
                    stdout.WriteLine($"  {entry.FireId}: failed [{validation.ErrorCode}] — {validation.Reason}");
                    report.Fires.Add(new FireReportEntry
                    {
                        FireId = entry.FireId, Status = FireStatus.Failed,
                        ErrorCode = validation.ErrorCode, Reason = validation.Reason,
                    });
                    continue;
                }

                var data = validation.Data!;
                FireImportOutcome outcome;
                try
                {
                    outcome = await importer.ImportAsync(data);
                }
                catch (Exception ex)
                {
                    outcome = FireImportOutcome.Failed("UNEXPECTED_IMPORT_ERROR", ex.Message);
                }

                var (status, errorCode, reason, cellsInserted) = outcome.Kind switch
                {
                    FireImportOutcomeKind.Ok => (FireStatus.Ok, (string?)null, (string?)null, outcome.CellsInserted),
                    FireImportOutcomeKind.Validated => (FireStatus.Validated, null, null, 0),
                    FireImportOutcomeKind.AlreadyImported => (FireStatus.AlreadyImported, null, null, 0),
                    FireImportOutcomeKind.Failed => (FireStatus.Failed, outcome.ErrorCode, outcome.Reason, 0),
                    _ => throw new InvalidOperationException(),
                };

                stdout.WriteLine($"  {entry.FireId}: {status}" + (reason is not null ? $" — {reason}" : ""));
                report.Fires.Add(new FireReportEntry
                {
                    FireId = entry.FireId,
                    Status = status,
                    ErrorCode = errorCode,
                    Reason = reason,
                    CellsValidated = validation.CellsValidated,
                    CellsInserted = cellsInserted,
                });
            }
            catch (Exception ex)
            {
                stdout.WriteLine($"  {entry.FireId}: failed [UNEXPECTED_VALIDATION_ERROR] — {ex.Message}");
                report.Fires.Add(new FireReportEntry
                {
                    FireId = entry.FireId, Status = FireStatus.Failed,
                    ErrorCode = "UNEXPECTED_VALIDATION_ERROR", Reason = ex.Message,
                });
            }
        }

        report.FinishedAt = DateTimeOffset.UtcNow;
        report.Finalize_();

        if (!ReportWriter.TryWrite(report, reportPath, out var writtenReportPath))
            return (FinishFatal("REPORT_WRITE_FAILED", $"Rapor yazılamadı: {reportPath}"), report);

        stdout.WriteLine();
        stdout.WriteLine($"Özet: ok={report.Summary.Ok} validated={report.Summary.Validated} " +
                          $"already_imported={report.Summary.AlreadyImported} skipped={report.Summary.Skipped} " +
                          $"failed={report.Summary.Failed}");
        stdout.WriteLine($"Rapor: {writtenReportPath}");

        var hasUnallowedSkip = report.Summary.Skipped > 0 && !options.AllowPartial;
        var exitCode = report.Summary.Failed > 0 || hasUnallowedSkip ? 1 : 0;
        return (exitCode, report);
    }
}
