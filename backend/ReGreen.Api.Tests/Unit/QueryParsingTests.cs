using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ReGreen.Api.Endpoints;
using Xunit;

namespace ReGreen.Api.Tests.Unit;

/// <summary>
/// `/api/fires/{id}/cells` sorgu parametresi doğrulama kuralları — DB'siz, saf mantık
/// testleri. "Üçü birlikte ya da hiçbiri" / "dördü birlikte ya da hiçbiri" kuralları
/// ve NaN/Infinity uç değerleri burada; endpoint'e uçtan uca akışı
/// ReGreen.Api.Tests/Integration'da test ediliyor.
/// </summary>
public class QueryParsingTests
{
    [Fact]
    public void TryGetWeights_NoneProvided_ReturnsTrueWithNullWeights()
    {
        var ok = QueryParsing.TryGetWeights(null, null, null, out var weights, out var error);

        Assert.True(ok);
        Assert.Null(weights);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(0.8, null, null)]
    [InlineData(null, 0.3, null)]
    [InlineData(null, null, 0.2)]
    public void TryGetWeights_OnlyOneProvided_ReturnsFalse(double? recovery, double? erosion, double? access)
    {
        var ok = QueryParsing.TryGetWeights(recovery, erosion, access, out var weights, out var error);

        Assert.False(ok);
        Assert.Null(weights);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryGetWeights_TwoOfThreeProvided_ReturnsFalse()
    {
        var ok = QueryParsing.TryGetWeights(0.5, 0.3, null, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryGetWeights_AllThreeValid_ReturnsTrueWithNormalizableWeights()
    {
        var ok = QueryParsing.TryGetWeights(0.5, 0.3, 0.2, out var weights, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(weights);
        Assert.Equal(0.5, weights!.Recovery);
        Assert.Equal(0.3, weights.Erosion);
        Assert.Equal(0.2, weights.Access);
    }

    [Theory]
    [InlineData(-0.1, 0.3, 0.2)] // negatif
    [InlineData(0.0, 0.0, 0.0)] // toplam sıfır
    [InlineData(double.NaN, 0.3, 0.2)] // tek başına sonlu değil
    [InlineData(double.PositiveInfinity, 0.3, 0.2)] // tek başına sonlu değil
    public void TryGetWeights_InvalidValues_ReturnsFalse(double recovery, double erosion, double access)
    {
        var ok = QueryParsing.TryGetWeights(recovery, erosion, access, out var weights, out var error);

        Assert.False(ok);
        Assert.Null(weights);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryGetWeights_EachFiniteButSumOverflowsToInfinity_ReturnsFalse()
    {
        // 1e308 + 1e308 + 1 taşar ve double.PositiveInfinity'ye yuvarlanır — her değer TEK
        // BAŞINA sonlu olduğu için sadece "her biri IsFinite mi" kontrolü bunu YAKALAMAZ,
        // toplamın kendisi de ayrıca kontrol edilmeli.
        var ok = QueryParsing.TryGetWeights(1e308, 1e308, 1.0, out var weights, out var error);

        Assert.False(ok);
        Assert.Null(weights);
        Assert.NotNull(error);
        var problem = Assert.IsAssignableFrom<ProblemHttpResult>(error);
        Assert.Equal("INVALID_PRIORITY_WEIGHTS", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void TryGetWeights_Invalid_ProducesBadRequestProblemDetails()
    {
        QueryParsing.TryGetWeights(0.8, null, null, out _, out var error);

        var problem = Assert.IsAssignableFrom<ProblemHttpResult>(error);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("INVALID_PRIORITY_WEIGHTS", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void TryGetBoundingBox_NoneProvided_ReturnsTrueWithNullBox()
    {
        var ok = QueryParsing.TryGetBoundingBox(null, null, null, null, out var box, out var error);

        Assert.True(ok);
        Assert.Null(box);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(10.0, null, null, null)]
    [InlineData(null, 10.0, null, null)]
    [InlineData(null, null, 10.0, null)]
    [InlineData(null, null, null, 10.0)]
    public void TryGetBoundingBox_OnlyOneProvided_ReturnsFalse(
        double? minLon, double? minLat, double? maxLon, double? maxLat)
    {
        var ok = QueryParsing.TryGetBoundingBox(minLon, minLat, maxLon, maxLat, out var box, out var error);

        Assert.False(ok);
        Assert.Null(box);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryGetBoundingBox_ThreeOfFourProvided_ReturnsFalse()
    {
        var ok = QueryParsing.TryGetBoundingBox(34, 36, 35, null, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryGetBoundingBox_MinGreaterThanOrEqualMax_ReturnsFalse()
    {
        var ok = QueryParsing.TryGetBoundingBox(35, 37, 35, 38, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(-181.0, 36.0, 35.0, 38.0)] // min_lon dışarıda
    [InlineData(34.0, -91.0, 35.0, 38.0)] // min_lat dışarıda
    [InlineData(34.0, 36.0, 181.0, 38.0)] // max_lon dışarıda
    [InlineData(34.0, 36.0, 35.0, 91.0)] // max_lat dışarıda
    public void TryGetBoundingBox_OutOfEpsg4326Range_ReturnsFalse(
        double minLon, double minLat, double maxLon, double maxLat)
    {
        var ok = QueryParsing.TryGetBoundingBox(minLon, minLat, maxLon, maxLat, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(double.NaN, 36.0, 35.0, 38.0)]
    [InlineData(34.0, double.NaN, 35.0, 38.0)]
    [InlineData(34.0, 36.0, double.PositiveInfinity, 38.0)]
    [InlineData(34.0, 36.0, 35.0, double.NegativeInfinity)]
    public void TryGetBoundingBox_NaNOrInfinity_ReturnsFalse(
        double minLon, double minLat, double maxLon, double maxLat)
    {
        // NaN ile yapılan <, >, >= karşılaştırmaları hep false döndüğü için IsFinite
        // kontrolü olmadan bu değerler sessizce "geçerli" sayılabiliyordu (gerçek bug,
        // düzeltildi) — burada regresyona karşı sabitleniyor.
        var ok = QueryParsing.TryGetBoundingBox(minLon, minLat, maxLon, maxLat, out var box, out var error);

        Assert.False(ok);
        Assert.Null(box);
        Assert.NotNull(error);
        var problem = Assert.IsAssignableFrom<ProblemHttpResult>(error);
        Assert.Equal("INVALID_BOUNDING_BOX", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void TryGetBoundingBox_Valid_ReturnsTrue()
    {
        var ok = QueryParsing.TryGetBoundingBox(34.5, 36.0, 35.5, 37.5, out var box, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(box);
        Assert.Equal(34.5, box!.Value.MinLon);
        Assert.Equal(37.5, box.Value.MaxLat);
    }
}
