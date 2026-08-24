using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReGreen.Data;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// `ApiFactory`'nin tersi: DB'YE BİLEREK bağlanamayan bir bağlantı dizesi kullanır —
/// `DbUnavailableExceptionHandler`'ın gerçekten 503/DB_UNAVAILABLE ürettiğini doğrulamak
/// için. Kısa `Connect Timeout` ile testin hızlı başarısız olması sağlanır. Gerçek LocalDB
/// gerektirmez (kasıtlı olarak bağlanamıyor), bu yüzden LocalDbFact opt-in'ine tabi DEĞİL.
/// </summary>
public class UnavailableDbApiFactory : WebApplicationFactory<Program>
{
    private const string UnreachableConnectionString =
        "Server=(localdb)\\ReGreen_Intentionally_Unreachable_Instance;Database=X;Trusted_Connection=True;Connect Timeout=2;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(UnreachableConnectionString));
        });
    }
}
