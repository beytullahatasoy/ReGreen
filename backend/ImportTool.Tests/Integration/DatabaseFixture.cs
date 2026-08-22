using Microsoft.EntityFrameworkCore;
using ReGreen.Data;
using Xunit;

namespace ImportTool.Tests.Integration;

/// <summary>
/// Gerçek dev veritabanına (ReGreen) DOKUNMAZ — her test koşusunda benzersiz adlı ayrı
/// bir LocalDB veritabanı kullanır, migrate eder ve sonunda siler. Testler `--allow-partial`,
/// dry-run, idempotency ve insert-or-verify davranışını GERÇEK SQL Server üzerinde
/// doğrular (mock DB değil) — bu davranışların çoğu CHECK/composite FK kısıtlarına
/// dayandığı için (bkz. docs/db-schema.md) in-memory provider'la anlamlı test edilemez.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    public static readonly string DatabaseName = $"ReGreenTest_{Environment.ProcessId}_{Guid.NewGuid():N}";
    public static readonly string ConnectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;";

    public async Task InitializeAsync()
    {
        // xUnit collection fixture'ı skipped testlerde de oluşturabilir. Opt-in yoksa
        // LocalDB'ye bağlantı dahil hiçbir dış durum değişikliği yapma.
        if (!LocalDbFactAttribute.Enabled) return;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (!LocalDbFactAttribute.Enabled) return;
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    public static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Her testten önce çağrılır — testler birbirinin verisini görmesin.</summary>
    public static async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Predictions;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ModelRuns;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Cells;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Fires;");
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
