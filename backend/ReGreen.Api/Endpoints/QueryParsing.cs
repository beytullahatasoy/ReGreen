using ReGreen.Api.Errors;
using ReGreen.Core.Priority;

namespace ReGreen.Api.Endpoints;

internal readonly record struct BoundingBox(double MinLon, double MinLat, double MaxLon, double MaxLat);

/// <summary>
/// `/api/fires/{id}/cells` sorgu parametrelerinin doğrulanması. Parametreler minimal
/// API'nin kendi `double?` query binding'i ile bağlanır (elle `HttpRequest.Query` okuma
/// YOK) — hem OpenAPI şemasında görünürler hem de kod sadeleşir. Kural: ağırlıkların
/// üçü de ya da hiçbiri, bounding box'ın dördü de ya da hiçbiri — aksi 400
/// INVALID_PRIORITY_WEIGHTS / INVALID_BOUNDING_BOX.
/// </summary>
internal static class QueryParsing
{
    public static bool TryGetWeights(
        double? recovery, double? erosion, double? access,
        out PriorityWeights? weights, out IResult? error)
    {
        weights = null;
        error = null;

        var providedCount = Count(recovery.HasValue, erosion.HasValue, access.HasValue);
        if (providedCount == 0)
            return true;

        if (providedCount != 3)
        {
            error = ApiProblems.InvalidPriorityWeights(
                "recovery, erosion ve access birlikte verilmelidir veya hiçbiri verilmemelidir.");
            return false;
        }

        var r = recovery!.Value;
        var e = erosion!.Value;
        var a = access!.Value;

        if (!double.IsFinite(r) || !double.IsFinite(e) || !double.IsFinite(a) || r < 0 || e < 0 || a < 0)
        {
            error = ApiProblems.InvalidPriorityWeights("recovery, erosion ve access sonlu ve negatif olmayan sayılar olmalıdır.");
            return false;
        }

        // Her değer ayrı ayrı sonlu olsa bile toplamları taşabilir (ör. 1e308 + 1e308) ve
        // double.PositiveInfinity'ye yuvarlanabilir — bu yüzden TOPLAM ayrıca kontrol edilir.
        var total = r + e + a;
        if (!double.IsFinite(total) || total <= 0)
        {
            error = ApiProblems.InvalidPriorityWeights(
                "recovery, erosion ve access toplamı sonlu ve sıfırdan büyük olmalıdır.");
            return false;
        }

        weights = new PriorityWeights { Recovery = r, Erosion = e, Access = a };
        return true;
    }

    public static bool TryGetBoundingBox(
        double? minLon, double? minLat, double? maxLon, double? maxLat,
        out BoundingBox? box, out IResult? error)
    {
        box = null;
        error = null;

        var providedCount = Count(minLon.HasValue, minLat.HasValue, maxLon.HasValue, maxLat.HasValue);
        if (providedCount == 0)
            return true;

        if (providedCount != 4)
        {
            error = ApiProblems.InvalidBoundingBox(
                "min_lon, min_lat, max_lon ve max_lat birlikte verilmelidir veya hiçbiri verilmemelidir.");
            return false;
        }

        var minLonV = minLon!.Value;
        var minLatV = minLat!.Value;
        var maxLonV = maxLon!.Value;
        var maxLatV = maxLat!.Value;

        // NaN ile yapılan <, >, >= karşılaştırmaları hep false döner — sonlu değilse aralık
        // kontrolünü hiç denemeden burada reddet (aksi halde NaN sessizce geçer).
        if (!double.IsFinite(minLonV) || !double.IsFinite(minLatV)
            || !double.IsFinite(maxLonV) || !double.IsFinite(maxLatV))
        {
            error = ApiProblems.InvalidBoundingBox("min_lon, min_lat, max_lon, max_lat sonlu sayılar olmalıdır.");
            return false;
        }

        if (minLonV < -180 || maxLonV > 180 || minLonV >= maxLonV
            || minLatV < -90 || maxLatV > 90 || minLatV >= maxLatV)
        {
            error = ApiProblems.InvalidBoundingBox(
                "-180 <= min_lon < max_lon <= 180 ve -90 <= min_lat < max_lat <= 90 olmalıdır.");
            return false;
        }

        box = new BoundingBox(minLonV, minLatV, maxLonV, maxLatV);
        return true;
    }

    private static int Count(params bool[] flags) => flags.Count(f => f);
}
