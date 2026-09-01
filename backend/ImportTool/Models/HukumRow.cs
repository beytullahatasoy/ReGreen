using CsvHelper.Configuration.Attributes;

namespace ImportTool.Models;

/// <summary>
/// {fire_id}_hukumler.csv satırı — docs/hukum_sozlesmesi.md, docs/data-contract.md §9.4.
/// 9 sütun, sabit sıra. `{fire_id}_hucreler.csv`'ye `cell_id` üzerinden 1:1 eşlenir.
/// Bu dosya, manifest.json'un `files_per_fire` listesinde BİLEREK YOK — AI ekibinin ayrı
/// "hüküm katmanı" teslimatı, opsiyonel bir yan dosya (bkz. FireValidator).
/// </summary>
public class HukumRow
{
    [Name("cell_id")] public string CellId { get; set; } = "";
    [Name("hukum")] public string Hukum { get; set; } = "";
    [Name("ek_kosullar")] public string? EkKosullar { get; set; }
    [Name("toparlanma_orani")] public double? ToparlanmaOrani { get; set; }
    [Name("tur_onerisi")] public string? TurOnerisi { get; set; }
    [Name("tetikleyen")] public string Tetikleyen { get; set; } = "";
    [Name("ozet")] public string Ozet { get; set; } = "";
    [Name("ayrinti")] public string Ayrinti { get; set; } = "";
    [Name("zamanlama_notu_var")] public bool ZamanlamaNotuVar { get; set; }

    /// <summary>Kaynak dosyadaki 1 tabanlı satır no (header hariç) — hata mesajları için.</summary>
    [Ignore]
    public int SourceLineNumber { get; set; }

    public static readonly string[] ExpectedHeader =
    [
        "cell_id", "hukum", "ek_kosullar", "toparlanma_orani", "tur_onerisi",
        "tetikleyen", "ozet", "ayrinti", "zamanlama_notu_var"
    ];
}
