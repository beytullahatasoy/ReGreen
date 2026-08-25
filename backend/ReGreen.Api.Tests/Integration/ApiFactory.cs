using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        // Test host, Windows Event Log'a yazma izni olmayan geliştirici/CI
        // ortamlarında da çalışabilmeli. Uygulamanın production logging ayarını
        // değiştirmeden yalnızca test host provider'larını taşınabilir tutar.
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

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
