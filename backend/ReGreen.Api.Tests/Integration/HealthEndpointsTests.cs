using System.Net;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

// [Collection("Database")] gerekli: Ready_DbReachable_Returns200, ApiFactory'nin işaret
// ettiği DatabaseFixture.ConnectionString DB'sinin (DatabaseFixture.InitializeAsync ile)
// zaten migrate edilmiş olduğunu varsayıyor — bu sadece aynı koleksiyondaki testler için
// garanti (xUnit fixture'ı ilk testten önce bir kez kurar).
[Collection("Database")]
#pragma warning disable CS9113 // xUnit collection fixture DI'si için gerekli, gövdede kullanılmıyor.
public class HealthEndpointsTests(DatabaseFixture fixture)
#pragma warning restore CS9113
{
    [Fact]
    public async Task Live_AlwaysReturns200_NoDbAccess()
    {
        // ApiFactory'nin bağlantı dizesi gerçekte var olmayan bir test DB'sine işaret eder —
        // /live bunu hiç kullanmadığı için yine de 200 dönmeli.
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_DbUnreachable_Returns503()
    {
        using var factory = new UnavailableDbApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [LocalDbFact]
    public async Task Ready_DbReachable_Returns200()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
