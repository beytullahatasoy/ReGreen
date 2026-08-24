using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReGreen.Data;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// `Program`'ın kayıtlı `AppDbContext` bağlantısını, hangi ortam değişkeni/appsettings
/// ayarlı olursa olsun, DatabaseFixture'ın benzersiz test veritabanıyla DEĞİŞTİRİR —
/// böylece testler gerçek geliştirme `ReGreen` DB'sine kesinlikle bağlanamaz
/// (Program.cs'deki normal bağlantı dizesi çözümü burada devre dışı bırakılır).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(DatabaseFixture.ConnectionString));
        });
    }
}
