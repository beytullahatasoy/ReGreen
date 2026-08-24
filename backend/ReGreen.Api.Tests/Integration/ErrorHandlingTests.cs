using System.Net;
using System.Text.Json;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// DB'ye kasıtlı olarak ulaşılamadığı senaryo (<see cref="UnavailableDbApiFactory"/>) ve
/// sayısal olmayan query parametresi senaryosu. İkisi de gerçek/LocalDB gerektirmez
/// (ikinci durumda binding, endpoint gövdesi/DB'ye hiç erişmeden başarısız olur) —
/// her zaman çalışır, LocalDbFact opt-in'i YOK.
/// </summary>
public class ErrorHandlingTests
{
    [Fact]
    public async Task DbUnreachable_Returns503WithDbUnavailableCode()
    {
        using var factory = new UnavailableDbApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/fires");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("DB_UNAVAILABLE", doc.RootElement.GetProperty("code").GetString());
    }

    // Regresyon: minimal API'nin query binding'i sayısal olmayan bir değer için
    // BadHttpRequestException fırlatıyordu ve bu, genel fallback'e düşüp 500
    // UNEXPECTED_ERROR olarak dönüyordu — 400 INVALID_QUERY_PARAMETER olması gerekir.
    [Theory]
    [InlineData("recovery")]
    [InlineData("erosion")]
    [InlineData("access")]
    [InlineData("min_lon")]
    [InlineData("min_lat")]
    [InlineData("max_lon")]
    [InlineData("max_lat")]
    public async Task NonNumericQueryParameter_Returns400WithInvalidQueryParameterCode(string paramName)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/fires/ANY_FIRE/cells?{paramName}=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("INVALID_QUERY_PARAMETER", doc.RootElement.GetProperty("code").GetString());
    }
}
