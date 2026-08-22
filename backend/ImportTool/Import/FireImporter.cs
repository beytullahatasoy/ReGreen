using ImportTool.Models;
using ImportTool.Priority;
using ImportTool.Validation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReGreen.Data;
using ReGreen.Data.Entities;

namespace ImportTool.Import;

public enum FireImportOutcomeKind { Ok, Validated, AlreadyImported, Failed }

public record FireImportOutcome(
    FireImportOutcomeKind Kind, int CellsInserted, string? ErrorCode = null, string? Reason = null)
{
    public static FireImportOutcome Ok(int cellsInserted) => new(FireImportOutcomeKind.Ok, cellsInserted);
    public static FireImportOutcome Validated() => new(FireImportOutcomeKind.Validated, 0);
    public static FireImportOutcome AlreadyImported() => new(FireImportOutcomeKind.AlreadyImported, 0);
    public static FireImportOutcome Failed(string code, string reason) =>
        new(FireImportOutcomeKind.Failed, 0, code, reason);
}

/// <summary>
/// Bir yangının DB yazma akışı — docs/import-flow.md §4/§5. Kör upsert yok, tek
/// transaction, yazmadan önce idempotency kontrolü.
/// </summary>
public class FireImporter(AppDbContext db, bool dryRun)
{
    public async Task<FireImportOutcome> ImportAsync(FireImportData data)
    {
        var meta = data.Metadata;

        // §5 — transaction'dan ÖNCE idempotency kontrolü.
        var alreadyImported = await db.ModelRuns.AsNoTracking().AnyAsync(m =>
            m.FireId == data.FireId && m.ModelVersion == meta.ModelVersion && m.GeneratedAt == meta.GeneratedAt);
        if (alreadyImported)
            return FireImportOutcome.AlreadyImported();

        // §3.4.5 — DB genelinde başka yangına ait aynı cell_id var mı? Dry-run "TÜM
        // salt-okunur DB kontrolleri çalışır" sözleşmesi gereği bu kontrol dry-run
        // erken dönüşünden ÖNCE yapılır (bkz. import-flow.md §3.7).
        var cellIds = data.CellRows.Select(r => r.CellId).ToList();
        var crossFireConflict = await db.Cells.AsNoTracking()
            .Where(c => cellIds.Contains(c.CellId) && c.FireId != data.FireId)
            .Select(c => new { c.CellId, c.FireId })
            .FirstOrDefaultAsync();

        if (crossFireConflict is not null)
            return FireImportOutcome.Failed("CELL_ID_CROSS_FIRE_CONFLICT",
                $"{crossFireConflict.CellId} zaten '{crossFireConflict.FireId}' yangınına ait.");

        // Dry-run dahil mevcut değişmez kayıtları gerçek import ile aynı biçimde doğrula.
        // Burada hiçbir entity track edilmez ve yazma/transaction yapılmaz.
        var immutableCheck = await VerifyExistingImmutableDataAsync(data);
        if (immutableCheck is not null)
            return immutableCheck;

        if (dryRun)
            return FireImportOutcome.Validated();

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var marker = data.Perimeter.ComputeMarker();
            var markerLat = marker.Y;
            var markerLon = marker.X;

            // 1) Fires insert-or-verify
            var existingFire = await db.Fires.FirstOrDefaultAsync(f => f.FireId == data.FireId);
            if (existingFire is null)
            {
                db.Fires.Add(new Fire
                {
                    FireId = data.FireId,
                    FireDate = meta.FireDate,
                    Province = meta.Province,
                    Region = meta.Region,
                    ModisAreaHa = meta.ModisAreaHa,
                    BurnedAreaHa = meta.BurnedAreaHa,
                    CellSizeM = meta.CellSizeM,
                    HasPerimeter = meta.HasPerimeter,
                    PerimeterGeoJson = data.Perimeter.RawGeoJson,
                    MarkerLat = markerLat,
                    MarkerLon = markerLon,
                    QualityFlag = meta.QualityFlag,
                    QualityNote = meta.QualityNote,
                });
            }
            else
            {
                var diffs = EntityComparer.CompareFire(existingFire, data, markerLat, markerLon);
                if (diffs.Count > 0)
                    return await RollbackAndFail(transaction, "FIRE_MISMATCH",
                        $"Fires.{data.FireId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
            }

            // 2) Cells insert-or-verify
            var existingCells = await db.Cells
                .Where(c => c.FireId == data.FireId)
                .ToDictionaryAsync(c => c.CellId);

            foreach (var row in data.CellRows)
            {
                if (existingCells.TryGetValue(row.CellId, out var existingCell))
                {
                    var diffs = EntityComparer.CompareCell(existingCell, row);
                    if (diffs.Count > 0)
                        return await RollbackAndFail(transaction, "CELL_MISMATCH",
                            $"Cells.{row.CellId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
                }
                else
                {
                    db.Cells.Add(new Cell
                    {
                        CellId = row.CellId,
                        FireId = data.FireId,
                        CenterLat = row.Lat,
                        CenterLon = row.Lon,
                        TreeCover = row.TreeCover,
                        TreeCoverAnnual = row.TreeCoverAnnual,
                        BurnSeverityDnbr = row.BurnSeverityDnbr,
                        SlopeDeg = row.SlopeDeg,
                        ElevationM = row.ElevationM,
                        RoadDistanceKm = row.RoadDistanceKm,
                        NdviBefore = row.NdviBefore,
                        NdviAfter = row.NdviAfter,
                        NdviDrop = row.NdviDrop,
                        SeverityClass = row.SeverityClass,
                        LandCover = row.LandCover,
                    });
                }
            }

            // 3) ModelRuns insert
            var modelRun = new ModelRun
            {
                FireId = data.FireId,
                ModelVersion = meta.ModelVersion,
                GeneratedAt = meta.GeneratedAt,
                SchemaVersion = meta.SchemaVersion,
                InTrainingSet = meta.InTrainingSet,
                OutOfFoldCells = meta.OutOfFoldCells,
                NormRecoveryGapMin = meta.NormalizationReference.RecoveryGapPred.Min ?? 0,
                NormRecoveryGapMax = meta.NormalizationReference.RecoveryGapPred.Max ?? 0,
                NormSlopeMin = meta.NormalizationReference.SlopeDeg.Min ?? 0,
                NormSlopeMax = meta.NormalizationReference.SlopeDeg.Max ?? 0,
                NormRoadDistMin = meta.NormalizationReference.RoadDistanceKm.Min ?? 0,
                NormRoadDistMax = meta.NormalizationReference.RoadDistanceKm.Max ?? 0,
                DefaultWeightRecovery = meta.PriorityWeights.Recovery,
                DefaultWeightErosion = meta.PriorityWeights.Erosion,
                DefaultWeightAccess = meta.PriorityWeights.Access,
                ThresholdVeryHigh = meta.PriorityThresholds.CokYuksek,
                ThresholdHigh = meta.PriorityThresholds.Yuksek,
                ThresholdMedium = meta.PriorityThresholds.Orta,
            };
            db.ModelRuns.Add(modelRun);
            await db.SaveChangesAsync(); // Id üretilsin diye Predictions'tan önce flush

            // 4) Predictions insert
            foreach (var row in data.CellRows)
            {
                db.Predictions.Add(new Prediction
                {
                    FireId = data.FireId,
                    CellId = row.CellId,
                    ModelRunId = modelRun.Id,
                    PredictionStatus = row.PredictionStatus,
                    RecoveryGapPred = row.RecoveryGapPred,
                    DefaultPriorityScore = row.PriorityScore,
                    DefaultPriorityClass = row.PriorityClass,
                });
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return FireImportOutcome.Ok(data.CellRows.Count);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "UQ_ModelRuns"))
        {
            // §5 — yarış durumu: ön kontrol ile INSERT arasında başka bir importer yazmış olabilir.
            await transaction.RollbackAsync();
            return FireImportOutcome.AlreadyImported();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            return FireImportOutcome.Failed("DB_CONSTRAINT_VIOLATION", ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<FireImportOutcome?> VerifyExistingImmutableDataAsync(FireImportData data)
    {
        var marker = data.Perimeter.ComputeMarker();
        var existingFire = await db.Fires.AsNoTracking().FirstOrDefaultAsync(f => f.FireId == data.FireId);
        if (existingFire is not null)
        {
            var diffs = EntityComparer.CompareFire(existingFire, data, marker.Y, marker.X);
            if (diffs.Count > 0)
                return FireImportOutcome.Failed("FIRE_MISMATCH",
                    $"Fires.{data.FireId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
        }

        var incomingIds = data.CellRows.Select(r => r.CellId).ToList();
        var existingCells = await db.Cells.AsNoTracking()
            .Where(c => c.FireId == data.FireId && incomingIds.Contains(c.CellId))
            .ToDictionaryAsync(c => c.CellId);
        foreach (var row in data.CellRows)
        {
            if (!existingCells.TryGetValue(row.CellId, out var existingCell)) continue;
            var diffs = EntityComparer.CompareCell(existingCell, row);
            if (diffs.Count > 0)
                return FireImportOutcome.Failed("CELL_MISMATCH",
                    $"Cells.{row.CellId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
        }

        return null;
    }

    private static async Task<FireImportOutcome> RollbackAndFail(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string code, string reason)
    {
        await tx.RollbackAsync();
        return FireImportOutcome.Failed(code, reason);
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string constraintName) =>
        ex.InnerException is SqlException sqlEx
        && (sqlEx.Number == 2601 || sqlEx.Number == 2627)
        && sqlEx.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
}
