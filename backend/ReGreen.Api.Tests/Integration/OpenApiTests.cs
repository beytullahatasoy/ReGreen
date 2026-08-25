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

    [Fact]
    public async Task CellsResponseSchema_ContainsModelExplanationFields()
    {
        using var doc = await FetchOpenApiDocumentAsync();

        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        var cellsSchema = schemas.GetProperty("CellsResponseDto");
        var properties = cellsSchema.GetProperty("properties");

        string[] expectedRootProperties =
        [
            "model_version", "normalization_reference", "priority_thresholds",
        ];
        foreach (var name in expectedRootProperties)
            Assert.True(properties.TryGetProperty(name, out _), $"CellsResponseDto.{name} OpenAPI şemasında yok.");

        var required = cellsSchema.GetProperty("required")
            .EnumerateArray()
            .Select(p => p.GetString())
            .ToHashSet();
        foreach (var name in expectedRootProperties)
            Assert.Contains(name, required);

        var normalizationSchema = ResolveReferencedSchema(
            schemas, properties.GetProperty("normalization_reference"));
        var normalizationProperties = normalizationSchema.GetProperty("properties");
        foreach (var name in new[] { "recovery_gap_pred", "slope_deg", "road_distance_km" })
            Assert.True(normalizationProperties.TryGetProperty(name, out _), $"normalization_reference.{name} eksik.");

        var rangeSchema = ResolveReferencedSchema(
            schemas, normalizationProperties.GetProperty("recovery_gap_pred"));
        var rangeRequired = rangeSchema.GetProperty("required")
            .EnumerateArray()
            .Select(p => p.GetString())
            .ToHashSet();
        Assert.Contains("min", rangeRequired);
        Assert.Contains("max", rangeRequired);
    }

    private static JsonElement ResolveReferencedSchema(JsonElement schemas, JsonElement propertySchema)
    {
        var reference = propertySchema.GetProperty("$ref").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reference));
        return schemas.GetProperty(reference!.Split('/').Last());
    }
}
