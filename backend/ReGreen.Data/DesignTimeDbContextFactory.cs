using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReGreen.Data;

/// <summary>
/// Sadece `dotnet ef migrations add/update` için — gerçek çalışma zamanında
/// ImportTool kendi DbContextOptions'ını appsettings.json'daki bağlantı dizesinden kurar.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=ReGreen;Trusted_Connection=True;");
        return new AppDbContext(optionsBuilder.Options);
    }
}
