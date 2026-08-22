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
}
