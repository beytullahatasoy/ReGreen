using ImportTool.Models;
using ReGreen.Core.Priority;
using ImportTool.Validation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        var existingModelRunId = await db.ModelRuns.AsNoTracking()
            .Where(m => m.FireId == data.FireId && m.ModelVersion == meta.ModelVersion
                && m.GeneratedAt == meta.GeneratedAt)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync();
        if (existingModelRunId is { } importedRunId)
        {
            // Ana veri (Fires/Cells/ModelRuns/Predictions) zaten yazılmış — ama hüküm katmanı
            // (CellVerdicts/FireNarratives) BİLEREK ayrı, SONRADAN gelen bir teslimat olabilir
            // (bkz. docs/hukum_sozlesmesi.md — AI ekibi ana veriden sonra hüküm CSV'lerini ayrı
            // yollayabilir). Bu yüzden "already_imported" ana akışı KISA DEVRE yapmaz; hüküm
            // katmanı varsa kendi küçük transaction'ında ayrıca insert-or-verify edilir.
            var existingCheck = await VerifyExistingImmutableDataAsync(data, importedRunId);
            if (existingCheck is not null)
                return existingCheck;
            return await BackfillHukumLayerAsync(data, importedRunId);
        }

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
        var immutableCheck = await VerifyExistingImmutableDataAsync(data, modelRunId: null);
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

            // 3) ModelRuns insert. Hüküm/anlatı model çıktısına bağlı olduğu için önce run
            // kimliği üretilir, sonra yan katman aynı run'a bağlanır.
            var modelRun = new ModelRun
            {
                FireId = data.FireId,
                ModelVersion = meta.ModelVersion,
                GeneratedAt = meta.GeneratedAt,
                SchemaVersion = meta.SchemaVersion,
                InTrainingSet = meta.InTrainingSet,
                OutOfFoldCells = meta.OutOfFoldCells,
                // FireValidator §3.3.4 min/max null'ı zaten reddettiği için burada güvenle
                // .Value kullanılır — sessiz `?? 0` fallback'i "gerçek 0" ile "referans yok"u
                // ayırt edilemez hale getiriyordu.
                NormRecoveryGapMin = meta.NormalizationReference.RecoveryGapPred.Min!.Value,
                NormRecoveryGapMax = meta.NormalizationReference.RecoveryGapPred.Max!.Value,
                NormSlopeMin = meta.NormalizationReference.SlopeDeg.Min!.Value,
                NormSlopeMax = meta.NormalizationReference.SlopeDeg.Max!.Value,
                NormRoadDistMin = meta.NormalizationReference.RoadDistanceKm.Min!.Value,
                NormRoadDistMax = meta.NormalizationReference.RoadDistanceKm.Max!.Value,
                DefaultWeightRecovery = meta.PriorityWeights.Recovery,
                DefaultWeightErosion = meta.PriorityWeights.Erosion,
                DefaultWeightAccess = meta.PriorityWeights.Access,
                ThresholdVeryHigh = meta.PriorityThresholds.CokYuksek,
                ThresholdHigh = meta.PriorityThresholds.Yuksek,
                ThresholdMedium = meta.PriorityThresholds.Orta,
            };
            db.ModelRuns.Add(modelRun);
            await db.SaveChangesAsync(); // Id üretilsin diye hüküm/Predictions'tan önce flush

            var hukumLayerResult = await UpsertHukumLayerAsync(data, modelRun.Id, transaction);
            if (hukumLayerResult is not null)
                return hukumLayerResult;

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

    /// <summary>
    /// CellVerdicts + FireNarratives insert-or-verify — AKTİF bir transaction içinde çağrılır,
    /// hem taze import akışında (ImportAsync) hem de "ana veri already_imported ama hüküm
    /// katmanı sonradan geldi" senaryosunda (BackfillHukumLayerAsync) KULLANILIR. Uyuşmazlıkta
    /// transaction'ı rollback eder ve Failed outcome döner; yazacak bir şey yoksa/temizse null.
    /// </summary>
    private async Task<FireImportOutcome?> UpsertHukumLayerAsync(
        FireImportData data, int modelRunId, IDbContextTransaction transaction)
    {
        if (data.HukumRows is { } hukumRows)
        {
            if (string.IsNullOrWhiteSpace(data.HukumSozluguSurum))
                return await RollbackAndFail(transaction, "HUKUM_SOZLUGU_REQUIRED",
                    "Hüküm satırları için hukum_sozlugu.json ve sürümü zorunludur.");

            var existingVerdicts = await db.CellVerdicts
                .Where(v => v.FireId == data.FireId && v.ModelRunId == modelRunId
                    && v.HukumSozluguSurum == data.HukumSozluguSurum)
                .ToDictionaryAsync(v => v.CellId);

            foreach (var row in hukumRows)
            {
                if (existingVerdicts.TryGetValue(row.CellId, out var existingVerdict))
                {
                    var diffs = EntityComparer.CompareCellVerdict(
                        existingVerdict, row, data.HukumSozluguSurum!);
                    if (diffs.Count > 0)
                        return await RollbackAndFail(transaction, "HUKUM_MISMATCH",
                            $"CellVerdicts.{row.CellId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
                }
                else
                {
                    db.CellVerdicts.Add(new CellVerdict
                    {
                        CellId = row.CellId,
                        FireId = data.FireId,
                        ModelRunId = modelRunId,
                        HukumSozluguSurum = data.HukumSozluguSurum,
                        Hukum = row.Hukum,
                        EkKosullar = row.EkKosullar,
                        ToparlanmaOrani = row.ToparlanmaOrani,
                        TurOnerisi = row.TurOnerisi,
                        Tetikleyen = row.Tetikleyen,
                        Ozet = row.Ozet,
                        Ayrinti = row.Ayrinti,
                        ZamanlamaNotuVar = row.ZamanlamaNotuVar,
                    });
                }
            }
        }

        if (data.Narrative is { } narrative)
        {
            var existingNarrative = await db.FireNarratives
                .FirstOrDefaultAsync(n => n.FireId == data.FireId && n.ModelRunId == modelRunId
                    && n.NarrativeVersion == narrative.NarrativeVersion);
            if (existingNarrative is null)
            {
                db.FireNarratives.Add(new FireNarrative
                {
                    FireId = data.FireId,
                    ModelRunId = modelRunId,
                    NarrativeVersion = narrative.NarrativeVersion,
                    Paragraf = narrative.Paragraf,
                    Profil = narrative.Profil,
                    Onaylandi = narrative.Onaylandi,
                    Uretim = narrative.Uretim,
                    SayiBlogu = narrative.SayiBlogu,
                });
            }
            else
            {
                var diffs = EntityComparer.CompareFireNarrative(existingNarrative, narrative);
                if (diffs.Count > 0)
                    return await RollbackAndFail(transaction, "FIRE_NARRATIVE_MISMATCH",
                        $"FireNarratives.{data.FireId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
            }
        }

        return null;
    }

    /// <summary>
    /// Ana veri (Fires/Cells/ModelRuns/Predictions) zaten import edilmişken hüküm katmanının
    /// (CellVerdicts/FireNarratives) SONRADAN gelen bir teslimatla tamamlanması — kendi küçük
    /// transaction'ında, UpsertHukumLayerAsync ile AYNI insert-or-verify garantisiyle.
    /// Hüküm verisi hiç yoksa (data.HukumRows/data.Narrative ikisi de null) doğrudan
    /// AlreadyImported döner, gereksiz transaction açılmaz.
    /// </summary>
    private async Task<FireImportOutcome> BackfillHukumLayerAsync(FireImportData data, int modelRunId)
    {
        if (data.HukumRows is null && data.Narrative is null)
            return FireImportOutcome.AlreadyImported();

        // ImportAsync bu noktadan önce aynı ModelRun'ın ana ve hüküm verisini doğrular.
        if (dryRun)
            return FireImportOutcome.AlreadyImported();

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await UpsertHukumLayerAsync(data, modelRunId, transaction);
            if (result is not null)
                return result;

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
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

    private async Task<FireImportOutcome?> VerifyExistingImmutableDataAsync(
        FireImportData data, int? modelRunId)
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

        // Dry-run/ön kontrol, gerçek transaction'la AYNI insert-or-verify garantisini
        // hüküm katmanı için de vermeli — bkz. 2b/2c yukarıda.
        if (data.HukumRows is { } hukumRows && modelRunId is { } runId)
        {
            var existingVerdicts = await db.CellVerdicts.AsNoTracking()
                .Where(v => v.FireId == data.FireId && v.ModelRunId == runId
                    && v.HukumSozluguSurum == data.HukumSozluguSurum)
                .ToDictionaryAsync(v => v.CellId);
            foreach (var row in hukumRows)
            {
                if (!existingVerdicts.TryGetValue(row.CellId, out var existingVerdict)) continue;
                var diffs = EntityComparer.CompareCellVerdict(
                    existingVerdict, row, data.HukumSozluguSurum!);
                if (diffs.Count > 0)
                    return FireImportOutcome.Failed("HUKUM_MISMATCH",
                        $"CellVerdicts.{row.CellId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
            }
        }

        if (data.Narrative is { } narrative && modelRunId is { } narrativeRunId)
        {
            var existingNarrative = await db.FireNarratives.AsNoTracking()
                .FirstOrDefaultAsync(n => n.FireId == data.FireId && n.ModelRunId == narrativeRunId
                    && n.NarrativeVersion == narrative.NarrativeVersion);
            if (existingNarrative is not null)
            {
                var diffs = EntityComparer.CompareFireNarrative(existingNarrative, narrative);
                if (diffs.Count > 0)
                    return FireImportOutcome.Failed("FIRE_NARRATIVE_MISMATCH",
                        $"FireNarratives.{data.FireId} mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");
            }
        }

        return null;
    }

    /// <summary>
    /// hukum_sozlugu.json'un sürüm başına insert-or-verify'ı — yangın döngüsünden
    /// ÖNCE, kendi küçük transaction'ında bir kez çağrılır (bkz. Orchestrator.RunAsync).
    /// Diğer her şeyle aynı felsefe: kör upsert yok, fark varsa loudly reddedilir.
    /// </summary>
    public async Task<FireImportOutcome> ImportHukumSozluguAsync(string surum, string jsonIcerik)
    {
        var existing = await db.HukumSozlugu.AsNoTracking().FirstOrDefaultAsync(h => h.Surum == surum);
        if (existing is not null)
        {
            var diffs = new List<string>();
            if (existing.JsonIcerik != jsonIcerik) diffs.Add("JsonIcerik farklı");
            if (diffs.Count > 0)
                return FireImportOutcome.Failed("HUKUM_SOZLUGU_MISMATCH",
                    $"HukumSozlugu mevcut kayıtla uyuşmuyor: {string.Join("; ", diffs)}");

            return FireImportOutcome.AlreadyImported();
        }

        if (dryRun)
            return FireImportOutcome.Validated();

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.HukumSozlugu.Add(new HukumSozlugu { Surum = surum, JsonIcerik = jsonIcerik });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return FireImportOutcome.Ok(0);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "PK_HukumSozlugu"))
        {
            // Yarış durumu — §5 ile aynı mantık: ön kontrol ile INSERT arasında başka bir
            // importer yazmış olabilir.
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
