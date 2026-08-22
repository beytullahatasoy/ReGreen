namespace ImportTool.Cli;

/// <summary>docs/import-flow.md §1 — CLI arayüzü.</summary>
public record CliOptions
{
    public required string ManifestPath { get; init; }
    public bool DryRun { get; init; }
    public bool AllowPartial { get; init; }
    public string? ReportPath { get; init; }
    public string? ConnectionString { get; init; }

    public static CliOptions Parse(string[] args)
    {
        string? manifestPath = null;
        bool dryRun = false, allowPartial = false;
        string? reportPath = null;
        string? connectionString = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--allow-partial":
                    allowPartial = true;
                    break;
                case "--report":
                    reportPath = RequireValue(args, ref i, "--report");
                    break;
                case "--connection-string":
                    connectionString = RequireValue(args, ref i, "--connection-string");
                    break;
                default:
                    if (args[i].StartsWith("--"))
                        throw new CliArgumentException($"Bilinmeyen parametre: {args[i]}");
                    if (manifestPath is not null)
                        throw new CliArgumentException("Birden fazla manifest yolu verildi.");
                    manifestPath = args[i];
                    break;
            }
        }

        if (manifestPath is null)
            throw new CliArgumentException(
                "Kullanım: ImportTool <manifest-yolu> [--dry-run] [--allow-partial] [--report <yol>] [--connection-string <cs>]");

        return new CliOptions
        {
            ManifestPath = manifestPath,
            DryRun = dryRun,
            AllowPartial = allowPartial,
            ReportPath = reportPath,
            ConnectionString = connectionString,
        };
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new CliArgumentException($"{flag} bir değer bekliyor.");
        return args[++i];
    }
}

public class CliArgumentException(string message) : Exception(message);
