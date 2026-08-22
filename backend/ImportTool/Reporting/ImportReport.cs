using System.Text.Json.Serialization;

namespace ImportTool.Reporting;

/// <summary>docs/import-flow.md §6 — JSON rapor şeması.</summary>
public class ImportReport
{
    [JsonPropertyName("run_id")] public string RunId { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("status")] public string Status { get; set; } = "success"; // success|partial|failed|fatal
    [JsonPropertyName("manifest_path")] public string ManifestPath { get; set; } = "";
    [JsonPropertyName("dry_run")] public bool DryRun { get; set; }
    [JsonPropertyName("allow_partial")] public bool AllowPartial { get; set; }
    [JsonPropertyName("started_at")] public DateTimeOffset StartedAt { get; set; }
    [JsonPropertyName("finished_at")] public DateTimeOffset FinishedAt { get; set; }
    [JsonPropertyName("schema_version_checked")] public string? SchemaVersionChecked { get; set; }
    [JsonPropertyName("fatal_error")] public FatalError? FatalError { get; set; }
    [JsonPropertyName("summary")] public ImportSummary Summary { get; set; } = new();
    [JsonPropertyName("fires")] public List<FireReportEntry> Fires { get; set; } = [];

    public void Finalize_()
    {
        Summary.TotalFires = Fires.Count;
        Summary.Ok = Fires.Count(f => f.Status == FireStatus.Ok);
        Summary.Validated = Fires.Count(f => f.Status == FireStatus.Validated);
        Summary.AlreadyImported = Fires.Count(f => f.Status == FireStatus.AlreadyImported);
        Summary.Skipped = Fires.Count(f => f.Status == FireStatus.Skipped);
        Summary.Failed = Fires.Count(f => f.Status == FireStatus.Failed);
        Summary.CellsValidated = Fires.Sum(f => f.CellsValidated);
        Summary.CellsInserted = Fires.Sum(f => f.CellsInserted);

        if (FatalError is not null) { Status = "fatal"; return; }
        if (Summary.Failed == 0 && Summary.Skipped == 0) { Status = "success"; return; }
        var anySucceeded = Summary.Ok > 0 || Summary.Validated > 0 || Summary.AlreadyImported > 0;
        Status = anySucceeded ? "partial" : "failed";
    }
}

public static class FireStatus
{
    public const string Ok = "ok";
    public const string Validated = "validated";
    public const string AlreadyImported = "already_imported";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
}

public class ImportSummary
{
    [JsonPropertyName("total_fires")] public int TotalFires { get; set; }
    [JsonPropertyName("ok")] public int Ok { get; set; }
    [JsonPropertyName("validated")] public int Validated { get; set; }
    [JsonPropertyName("already_imported")] public int AlreadyImported { get; set; }
    [JsonPropertyName("skipped")] public int Skipped { get; set; }
    [JsonPropertyName("failed")] public int Failed { get; set; }
    [JsonPropertyName("cells_validated")] public int CellsValidated { get; set; }
    [JsonPropertyName("cells_inserted")] public int CellsInserted { get; set; }
}

public class FireReportEntry
{
    [JsonPropertyName("fire_id")] public string FireId { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("error_code")] public string? ErrorCode { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("cells_validated")] public int CellsValidated { get; set; }
    [JsonPropertyName("cells_inserted")] public int CellsInserted { get; set; }
}

public class FatalError
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}
