using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Dtos;
using ReGreen.Api.Errors;
using ReGreen.Core.Priority;
using ReGreen.Data;

namespace ReGreen.Api.Endpoints;

/// <summary>
/// PDF v4.1 §9'daki 3 endpoint. Sözleşme detayları (enum'lar, ağırlık/bounding box
/// kuralları, hata kodları, ModelRun seçim kuralı) docs/api-contract.md'de.
/// </summary>
public static class FireEndpoints
{
    private static readonly string[] ValidQualityFlags = ["ok", "check"];
    private static readonly string[] ValidPredictionStatuses = ["predicted", "low_severity", "no_data"];
    private static readonly string[] ValidPriorityClasses = ["COK_YUKSEK", "YUKSEK", "ORTA", "DUSUK"];

    public static void MapFireEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fires", GetFires)
            .Produces<List<FireSummaryDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/fires/{fireId}/perimeter", GetFirePerimeter)
            .Produces<FirePerimeterDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/fires/{fireId}/cells", GetFireCells)
            .Produces<CellsResponseDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetFires(
        AppDbContext db, string? quality_flag, CancellationToken ct)
    {
        if (quality_flag is not null && !ValidQualityFlags.Contains(quality_flag))
            return ApiProblems.InvalidQueryParameter("quality_flag 'ok' veya 'check' olmalıdır.");

        var query = db.Fires.AsNoTracking().AsQueryable();
        if (quality_flag is not null)
            query = query.Where(f => f.QualityFlag == quality_flag);

        var fires = await query
            .OrderBy(f => f.FireId)
            .Select(f => new FireSummaryDto(
                f.FireId, f.FireDate, f.Province, f.Region,
                f.ModisAreaHa, f.BurnedAreaHa, f.Cells.Count,
                f.HasPerimeter, f.MarkerLat, f.MarkerLon,
                f.QualityFlag, f.QualityNote))
            .ToListAsync(ct);

        return Results.Ok(fires);
    }

    private static async Task<IResult> GetFirePerimeter(
        string fireId, AppDbContext db, CancellationToken ct)
    {
        var fire = await db.Fires.AsNoTracking()
            .Where(f => f.FireId == fireId)
            .Select(f => new { f.MarkerLat, f.MarkerLon, f.PerimeterGeoJson })
            .FirstOrDefaultAsync(ct);

        if (fire is null)
            return ApiProblems.FireNotFound(fireId);

        JsonElement perimeter;
        try
        {
            using var doc = JsonDocument.Parse(fire.PerimeterGeoJson);
            perimeter = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ApiProblems.PerimeterDataCorrupt(fireId);
        }

        return Results.Ok(new FirePerimeterDto(fireId, new MarkerDto(fire.MarkerLat, fire.MarkerLon), perimeter));
    }

    private static async Task<IResult> GetFireCells(
        string fireId, AppDbContext db,
        string? prediction_status, string? priority_class,
        double? recovery, double? erosion, double? access,
        double? min_lon, double? min_lat, double? max_lon, double? max_lat,
        CancellationToken ct)
    {
        if (prediction_status is not null && !ValidPredictionStatuses.Contains(prediction_status))
            return ApiProblems.InvalidQueryParameter(
                "prediction_status 'predicted', 'low_severity' veya 'no_data' olmalıdır.");
        if (priority_class is not null && !ValidPriorityClasses.Contains(priority_class))
            return ApiProblems.InvalidQueryParameter(
                "priority_class 'COK_YUKSEK', 'YUKSEK', 'ORTA' veya 'DUSUK' olmalıdır.");

        if (!QueryParsing.TryGetWeights(recovery, erosion, access, out var customWeights, out var weightsError))
            return weightsError!;
        if (!QueryParsing.TryGetBoundingBox(min_lon, min_lat, max_lon, max_lat, out var bbox, out var bboxError))
            return bboxError!;

        var fire = await db.Fires.AsNoTracking()
            .Where(f => f.FireId == fireId)
            .Select(f => new { f.FireId, f.CellSizeM })
            .FirstOrDefaultAsync(ct);
        if (fire is null)
            return ApiProblems.FireNotFound(fireId);

        // En güncel ModelRun: GeneratedAt DESC, eşitlikte Id DESC (deterministik tie-break —
        // docs/api-contract.md'ye yazılı kural).
        var modelRun = await db.ModelRuns.AsNoTracking()
            .Where(m => m.FireId == fireId)
            .OrderByDescending(m => m.GeneratedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync(ct);
        if (modelRun is null)
            return ApiProblems.ModelRunNotFound(fireId);

        var predictionsQuery = db.Predictions.AsNoTracking()
            .Where(p => p.ModelRunId == modelRun.Id);

        if (prediction_status is not null)
            predictionsQuery = predictionsQuery.Where(p => p.PredictionStatus == prediction_status);

        if (bbox is { } b)
            predictionsQuery = predictionsQuery.Where(p =>
                p.Cell.CenterLon >= b.MinLon && p.Cell.CenterLon <= b.MaxLon &&
                p.Cell.CenterLat >= b.MinLat && p.Cell.CenterLat <= b.MaxLat);

        var rows = await predictionsQuery
            .Select(p => new
            {
                p.CellId,
                p.Cell.CenterLat,
                p.Cell.CenterLon,
                p.Cell.TreeCover,
                p.Cell.TreeCoverAnnual,
                p.Cell.BurnSeverityDnbr,
                p.Cell.SlopeDeg,
                p.Cell.ElevationM,
                p.Cell.RoadDistanceKm,
                p.Cell.NdviBefore,
                p.Cell.NdviAfter,
                p.Cell.NdviDrop,
                p.Cell.SeverityClass,
                p.Cell.LandCover,
                p.PredictionStatus,
                p.RecoveryGapPred,
                p.DefaultPriorityScore,
                p.DefaultPriorityClass,
            })
            .ToListAsync(ct);

        // Ağırlık: kullanıcı verdiyse onu kullan, vermediyse ModelRun'ın varsayılanı —
        // her iki durumda da normalize edilip response'ta `applied_weights` olarak görünür
        // (docs/data-contract.md §8.4 kural 3).
        var effectiveWeights = customWeights ?? new PriorityWeights
        {
            Recovery = modelRun.DefaultWeightRecovery,
            Erosion = modelRun.DefaultWeightErosion,
            Access = modelRun.DefaultWeightAccess,
        };
        var normalizedWeights = PriorityCalculator.NormalizeWeights(effectiveWeights);
        var appliedWeights = new AppliedWeightsDto(
            normalizedWeights.Recovery, normalizedWeights.Erosion, normalizedWeights.Access);

        // normalization_reference SABİT (§8.3) — sadece ağırlık değişir, bu ikisi ModelRun'dan gelir.
        // İç hesap (ComputeScore) Core'un nullable NormRange'ini kullanır; response'a giden
        // NormalizationReferenceDto ise API'nin non-nullable wire tipidir (bkz. CellDtos.cs).
        var normReference = new NormalizationReference
        {
            RecoveryGapPred = new NormRange { Min = modelRun.NormRecoveryGapMin, Max = modelRun.NormRecoveryGapMax },
            SlopeDeg = new NormRange { Min = modelRun.NormSlopeMin, Max = modelRun.NormSlopeMax },
            RoadDistanceKm = new NormRange { Min = modelRun.NormRoadDistMin, Max = modelRun.NormRoadDistMax },
        };
        var normReferenceDto = new NormalizationReferenceDto(
            new NormRangeDto(modelRun.NormRecoveryGapMin, modelRun.NormRecoveryGapMax),
            new NormRangeDto(modelRun.NormSlopeMin, modelRun.NormSlopeMax),
            new NormRangeDto(modelRun.NormRoadDistMin, modelRun.NormRoadDistMax));
        var thresholds = new PriorityThresholds
        {
            CokYuksek = modelRun.ThresholdVeryHigh,
            Yuksek = modelRun.ThresholdHigh,
            Orta = modelRun.ThresholdMedium,
        };

        var items = new List<CellDto>(rows.Count);
        foreach (var row in rows)
        {
            double? score = row.DefaultPriorityScore;
            string? cls = row.DefaultPriorityClass;

            // Sadece "predicted" hücreler yeniden hesaplanır — low_severity/no_data AYNEN
            // kalır, backend bunlara kendi yorumunu katmaz (§8.4 kural 4).
            if (customWeights is not null && row.PredictionStatus == "predicted")
            {
                score = PriorityCalculator.ComputeScore(
                    row.RecoveryGapPred!.Value, row.SlopeDeg, row.RoadDistanceKm, normReference, effectiveWeights);
                cls = PriorityCalculator.Classify(score.Value, thresholds);
            }

            items.Add(new CellDto(
                row.CellId, row.CenterLat, row.CenterLon,
                row.TreeCover, row.TreeCoverAnnual, row.BurnSeverityDnbr, row.SlopeDeg,
                row.ElevationM, row.RoadDistanceKm, row.NdviBefore, row.NdviAfter, row.NdviDrop,
                row.SeverityClass, row.LandCover, row.PredictionStatus, row.RecoveryGapPred,
                score, cls));
        }

        // priority_class filtresi özel ağırlıkla yeniden hesaplama SONRASI bir değere
        // bağlı olduğu için SQL'de değil burada (bellekte) uygulanır.
        if (priority_class is not null)
            items = items.Where(c => c.PriorityClass == priority_class).ToList();

        var response = new CellsResponseDto(
            fireId, modelRun.Id, modelRun.ModelVersion, modelRun.GeneratedAt, "EPSG:4326", fire.CellSizeM,
            appliedWeights, normReferenceDto, thresholds, items.Count, items);

        return Results.Ok(response);
    }
}
