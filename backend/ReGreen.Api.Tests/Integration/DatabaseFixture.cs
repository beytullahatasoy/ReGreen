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
    public static readonly string ConnectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;";

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
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Predictions;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ModelRuns;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Cells;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Fires;");
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
