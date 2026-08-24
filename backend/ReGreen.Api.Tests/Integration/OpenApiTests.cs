using System.Text.Json;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// `/openapi/v1.json`'ın gerçekten kullanılabilir bir şema ürettiğini doğrular — daha
/// önce endpoint'ler `Task&lt;IResult&gt;` döndüğü ve query parametreleri elle
/// `HttpRequest.Query`'den okunduğu için TÜM response şemaları `null`, ağırlık/bounding
/// box parametreleri ise hiç görünmüyordu. Bu, dokümanı OpenAPI'den üretecek/frontend'e
/// paylaşacak biri için gerçek bir regresyon olurdu. DB gerektirmez (şema üretimi hiçbir
/// endpoint handler'ını çalıştırmaz), LocalDbFact opt-in'ine tabi değildir.
/// </summary>
public class OpenApiTests
{
    private static async Task<JsonDocument> FetchOpenApiDocumentAsync()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    [Fact]
    public async Task ComponentSchemas_AreNotEmpty()
    {
        using var doc = await FetchOpenApiDocumentAsync();

        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.EnumerateObject().Any(), "components.schemas boş olmamalı.");
    }

    [Theory]
    [InlineData("/api/fires")]
    [InlineData("/api/fires/{fireId}/perimeter")]
    [InlineData("/api/fires/{fireId}/cells")]
    public async Task Get200Response_HasNonNullSchema(string path)
    {
        using var doc = await FetchOpenApiDocumentAsync();

        var okResponse = doc.RootElement
            .GetProperty("paths").GetProperty(path)
            .GetProperty("get").GetProperty("responses").GetProperty("200");

        Assert.True(okResponse.TryGetProperty("content", out var content), "200 response'unda content yok.");
        var schema = content.GetProperty("application/json").GetProperty("schema");
        Assert.NotEqual(JsonValueKind.Null, schema.ValueKind);
    }

    [Fact]
    public async Task CellsEndpoint_ListsAllNineManuallyValidatedQueryParameters()
    {
        using var doc = await FetchOpenApiDocumentAsync();

        var parameters = doc.RootElement
            .GetProperty("paths").GetProperty("/api/fires/{fireId}/cells")
            .GetProperty("get").GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToHashSet();

        string[] expected =
        [
            "prediction_status", "priority_class",
            "recovery", "erosion", "access",
            "min_lon", "min_lat", "max_lon", "max_lat",
        ];
        foreach (var name in expected)
            Assert.Contains(name, parameters);
    }
}
