using ImportTool.Geo;
using ImportTool.Models;

namespace ImportTool.Validation;

public enum FireValidationKind { Success, Skipped, Failed }

public class FireValidationResult
{
    public required FireValidationKind Kind { get; init; }
    public string? ErrorCode { get; init; }
    public string? Reason { get; init; }
    public FireImportData? Data { get; init; }

    /// <summary>Doğrulamayı geçen (ama henüz DB'ye yazılmamış olabilecek) hücre sayısı.</summary>
    public int CellsValidated => Data?.CellRows.Count ?? 0;

    public static FireValidationResult Success(FireImportData data) =>
        new() { Kind = FireValidationKind.Success, Data = data };

    public static FireValidationResult Skipped(string reason) =>
        new() { Kind = FireValidationKind.Skipped, Reason = reason };

    public static FireValidationResult Failed(string errorCode, string reason) =>
        new() { Kind = FireValidationKind.Failed, ErrorCode = errorCode, Reason = reason };
}

/// <summary>Bir yangının başarıyla doğrulanmış, yazmaya hazır tüm verisi.</summary>
public class FireImportData
{
    public required string FireId { get; init; }
    public required FireMetadata Metadata { get; init; }
    public required FirePerimeter Perimeter { get; init; }
    public required List<CellRow> CellRows { get; init; }

    /// <summary>
    /// {fire_id}_hukumler.csv satırları — YALNIZCA dosya varsa dolu (FireValidator tarafından
    /// çözülür/doğrulanır). Null ise bu yangının henüz hüküm katmanı verisi yok demektir
    /// (backward-compatible no-op, hata değil) — bkz. docs/hukum_sozlesmesi.md.
    /// </summary>
    public List<HukumRow>? HukumRows { get; init; }

    /// <summary>Hüküm satırlarının üretildiği sözlük sürümü; Orchestrator tarafından atanır.</summary>
    public string? HukumSozluguSurum { get; set; }

    /// <summary>
    /// yangin_metinleri.json + yangin_ozetleri.json'daki bu yangına ait girdi. Bu iki dosya
    /// yangın başına DEĞİL, paket genelinde bir kez okunur (bkz. Orchestrator.RunAsync) —
    /// bu yüzden FireValidator tarafından DEĞİL, doğrulama başarıyla döndükten SONRA
    /// Orchestrator tarafından atanır (bilerek `init` değil, mutable).
    /// </summary>
    public FireNarrativeInput? Narrative { get; set; }
}

/// <summary>
/// Bir yangının Katman 1 anlatısı — yangin_metinleri.json'daki paragraf/profil/onay bilgisi
/// + yangin_ozetleri.json'daki ham sayı bloğu (opak JSON, verbatim saklanır).
/// </summary>
public record FireNarrativeInput(
    string Paragraf, string Profil, bool Onaylandi, string Uretim,
    string SayiBlogu, string NarrativeVersion);
