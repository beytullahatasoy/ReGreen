using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReGreen.Data;

/// <summary>
/// Sadece `dotnet ef migrations add/update` için — gerçek çalışma zamanında
/// ImportTool kendi DbContextOptions'ını appsettings.json'daki bağlantı dizesinden kurar.
///
/// Bağlantı dizesi önceliği Program.cs ve ImportTool ile AYNI olmalı: aksi halde
/// `dotnet ef database update` migration'ı, API'nin okuduğu veritabanına değil
/// sessizce LocalDB'ye uygular ve şema ile çalışan DB birbirinden ayrışır.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(
            Environment.GetEnvironmentVariable("REGREEN_CONNECTION_STRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ReGreen;Trusted_Connection=True;");
        return new AppDbContext(optionsBuilder.Options);
    }
}
