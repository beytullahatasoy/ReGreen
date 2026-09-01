using Microsoft.EntityFrameworkCore;
using ReGreen.Data;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// ImportTool.Tests/Integration/DatabaseFixture.cs ile aynı desen — gerçek dev
/// veritabanına (ReGreen) DOKUNMAZ, her test koşusunda benzersiz adlı ayrı bir LocalDB
/// veritabanı kullanır, migrate eder ve sonunda siler. <see cref="ApiFactory"/> bu
/// veritabanının bağlantı dizesini WebApplicationFactory'ye açıkça enjekte eder —
/// gerçek geliştirme DB'sine yanlışlıkla asla bağlanılmaz.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    public static readonly string DatabaseName = $"ReGreenApiTest_{Environment.ProcessId}_{Guid.NewGuid():N}";

    /// <summary>
    /// Geçici test veritabanının oluşturulacağı SQL Server örneği. Varsayılan
    /// LocalDB'dir; LocalDB Runtime'ın kurulu OLMADIĞI ama SQL Server Express'in
    /// bulunduğu makinelerde REGREEN_TEST_SQL_SERVER ile değiştirilir:
    ///     $env:REGREEN_TEST_SQL_SERVER = '.\SQLEXPRESS'
    /// Bu olmadan testler o makinelerde hiç koşamıyor (error 52).
    /// </summary>
    private static readonly string Server =
        Environment.GetEnvironmentVariable("REGREEN_TEST_SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";

    public static readonly string ConnectionString =
        $"Server={Server};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
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
        // Topluluk tabloları da Fires'a bağlı (FieldActivities.FireId,
        // FieldObservations.FireId) — Fires'tan ÖNCE silinmeliler, yoksa FK ihlali.
        // Organisations DOKUNULMAZ: migration seed'i, her testte var olması bekleniyor.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ActivityParticipants;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FieldObservations;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FieldActivities;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Volunteers;");

        // FK sırası: CellVerdicts/FireNarratives -> Cells/Fires'tan ÖNCE silinmeli
        // (composite FK'ler NoAction — bkz. AppDbContext.ConfigureCellVerdicts/FireNarratives).
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CellVerdicts;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FireNarratives;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM HukumSozlugu;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Predictions;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ModelRuns;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Cells;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Fires;");
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
