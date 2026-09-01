using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Dtos;
using ReGreen.Api.Errors;
using ReGreen.Data;

namespace ReGreen.Api.Endpoints;

/// <summary>
/// docs/hukum_sozlesmesi.md'deki "hüküm katmanı" uç noktaları — `FireEndpoints`'in kapsamı
/// (PDF v4.1 §9) dışında kaldığı için AYRI dosyada. 3 endpoint: hücre hükmü, yangın özeti
/// (anlatı) ve global hüküm sözlüğü.
/// </summary>
public static class HukumEndpoints
{
    public static void MapHukumEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fires/{fireId}/cells/{cellId}/hukum", GetCellVerdict)
            .Produces<CellVerdictDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/fires/{fireId}/summary", GetFireNarrative)
            .Produces<FireNarrativeDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/hukum-sozlugu", GetHukumSozlugu)
            .Produces<JsonElement>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetCellVerdict(
        string fireId, string cellId, AppDbContext db, CancellationToken ct)
    {
        var fireExists = await db.Fires.AsNoTracking().AnyAsync(f => f.FireId == fireId, ct);
        if (!fireExists)
            return ApiProblems.FireNotFound(fireId);

        var cellExists = await db.Cells.AsNoTracking()
            .AnyAsync(c => c.FireId == fireId && c.CellId == cellId, ct);
        if (!cellExists)
            return ApiProblems.CellNotFound(fireId, cellId);

        var modelRun = await LatestModelRun(db, fireId, ct);
        if (modelRun is null)
            return ApiProblems.ModelRunNotFound(fireId);

        var verdict = await db.CellVerdicts.AsNoTracking()
            .Where(v => v.FireId == fireId && v.CellId == cellId && v.ModelRunId == modelRun.Id)
            .OrderByDescending(v => v.HukumSozlugu.ImportedAt)
            .ThenByDescending(v => v.HukumSozluguSurum)
            .Select(v => new
            {
                v.CellId, v.HukumSozluguSurum, v.Hukum, v.EkKosullar, v.ToparlanmaOrani,
                v.TurOnerisi, v.Tetikleyen, v.Ozet, v.Ayrinti, v.ZamanlamaNotuVar,
            })
            .FirstOrDefaultAsync(ct);
        if (verdict is null)
            return ApiProblems.CellVerdictNotFound(cellId);

        var ekKosullar = (verdict.EkKosullar ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries);

        return Results.Ok(new CellVerdictDto(
            verdict.CellId, modelRun.Id, modelRun.ModelVersion, modelRun.GeneratedAt,
            verdict.HukumSozluguSurum, verdict.Hukum, ekKosullar, verdict.ToparlanmaOrani,
            verdict.TurOnerisi, verdict.Tetikleyen, verdict.Ozet, verdict.Ayrinti, verdict.ZamanlamaNotuVar));
    }

    private static async Task<IResult> GetFireNarrative(string fireId, AppDbContext db, CancellationToken ct)
    {
        var fireExists = await db.Fires.AsNoTracking().AnyAsync(f => f.FireId == fireId, ct);
        if (!fireExists)
            return ApiProblems.FireNotFound(fireId);

        var modelRun = await LatestModelRun(db, fireId, ct);
        if (modelRun is null)
            return ApiProblems.ModelRunNotFound(fireId);

        var narrative = await db.FireNarratives.AsNoTracking()
            .Where(n => n.FireId == fireId && n.ModelRunId == modelRun.Id)
            .OrderByDescending(n => n.NarrativeVersion)
            .Select(n => new { n.FireId, n.NarrativeVersion, n.Paragraf, n.Profil, n.Onaylandi, n.Uretim, n.SayiBlogu })
            .FirstOrDefaultAsync(ct);
        if (narrative is null)
            return ApiProblems.FireNarrativeNotFound(fireId);

        // SayiBlogu, ImportTool tarafından yazılan ve import zamanında zaten geçerliliği
        // doğrulanmış bir JSON blob'udur (bkz. docs/hukum_sozlesmesi.md) — FirePerimeterDto'nun
        // aksine burada ayrı bir "corrupt" hata kodu tanımlanmadı (spec'te de istenmedi);
        // beklenmeyen bir parse hatası genel 500 UNEXPECTED_ERROR fallback'ine düşer.
        using var doc = JsonDocument.Parse(narrative.SayiBlogu);
        var sayiBlogu = doc.RootElement.Clone();

        return Results.Ok(new FireNarrativeDto(
            narrative.FireId, modelRun.Id, modelRun.ModelVersion, modelRun.GeneratedAt,
            narrative.NarrativeVersion, narrative.Paragraf, narrative.Profil,
            narrative.Onaylandi, narrative.Uretim, sayiBlogu));
    }

    private static async Task<IResult> GetHukumSozlugu(
        AppDbContext db, string? surum, CancellationToken ct)
    {
        var query = db.HukumSozlugu.AsNoTracking();
        if (surum is not null)
            query = query.Where(h => h.Surum == surum);

        var jsonIcerik = await query
            .OrderByDescending(h => h.ImportedAt)
            .ThenByDescending(h => h.Surum)
            .Select(h => h.JsonIcerik)
            .FirstOrDefaultAsync(ct);
        if (jsonIcerik is null)
            return ApiProblems.HukumSozluguNotFound();

        using var doc = JsonDocument.Parse(jsonIcerik);
        return Results.Ok(doc.RootElement.Clone());
    }

    private static Task<ModelRunIdentity?> LatestModelRun(
        AppDbContext db, string fireId, CancellationToken ct) =>
        db.ModelRuns.AsNoTracking()
            .Where(m => m.FireId == fireId)
            .OrderByDescending(m => m.GeneratedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new ModelRunIdentity(m.Id, m.ModelVersion, m.GeneratedAt))
            .FirstOrDefaultAsync(ct);

    private sealed record ModelRunIdentity(int Id, string ModelVersion, DateTimeOffset GeneratedAt);
}
