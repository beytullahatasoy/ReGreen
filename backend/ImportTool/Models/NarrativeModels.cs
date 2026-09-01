using System.Text.Json.Serialization;

namespace ImportTool.Models;

/// <summary>
/// yangin_metinleri.json — docs/hukum_sozlesmesi.md "Katman 1 — yangın özeti". Paket
/// genelinde BİR KEZ okunur (manifest.json'un yanında, per-fire dosya DEĞİL) — bkz.
/// Orchestrator.RunAsync. `uretim`, tüm yangınlar için ORTAK üst seviye alan (üretim
/// yöntemi — "sablon (deterministik)"); FireNarrative.Uretim buradan gelir.
/// </summary>
public record class NarrativeFile
{
    [JsonPropertyName("surum")] public string? Surum { get; set; }
    [JsonPropertyName("dil")] public string? Dil { get; set; }
    [JsonPropertyName("uretim")] public required string Uretim { get; set; }
    [JsonPropertyName("yanginlar")] public required Dictionary<string, NarrativeEntry> Yanginlar { get; set; }
}

public record class NarrativeEntry
{
    [JsonPropertyName("paragraf")] public required string Paragraf { get; set; }
    [JsonPropertyName("profil")] public required string Profil { get; set; }
    [JsonPropertyName("kaynak")] public string? Kaynak { get; set; }
    [JsonPropertyName("onaylandi")] public required bool Onaylandi { get; set; }
}
