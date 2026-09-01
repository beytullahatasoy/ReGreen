using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReGreen.Api.Dtos;

/// <summary>
/// docs/hukum_sozlesmesi.md — bir hücrenin hükmü. Öncelik skorundan (CellDto) BAĞIMSIZDIR;
/// ağırlık kaydırıcısı değişse bile aynı kalır, bu yüzden ayrı bir uç noktada döner
/// (`GET /api/fires/{fireId}/cells/{cellId}/hukum`) — `/cells` yanıtını şişirmez.
/// </summary>
public record class CellVerdictDto(
    [property: JsonPropertyName("cell_id")] string CellId,
    [property: JsonPropertyName("model_run_id")] int ModelRunId,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("hukum_version")] string HukumVersion,
    [property: JsonPropertyName("hukum")] string Hukum,
    [property: JsonPropertyName("ek_kosullar")] string[] EkKosullar,
    [property: JsonPropertyName("toparlanma_orani")] double? ToparlanmaOrani,
    [property: JsonPropertyName("tur_onerisi")] string? TurOnerisi,
    [property: JsonPropertyName("tetikleyen")] string Tetikleyen,
    [property: JsonPropertyName("ozet")] string Ozet,
    [property: JsonPropertyName("ayrinti")] string Ayrinti,
    [property: JsonPropertyName("zamanlama_notu_var")] bool ZamanlamaNotuVar);

/// <summary>
/// docs/hukum_sozlesmesi.md "Katman 1 — yangın özeti". `sayi_blogu`, yangin_ozetleri.json'daki
/// bu yangına ait ham JSON bloğudur — `FirePerimeterDto.Perimeter` ile AYNI teknik
/// (`JsonDocument.Parse(...).RootElement.Clone()`) ile aynen geçirilir, alan alan modellenmez.
/// </summary>
public record class FireNarrativeDto(
    [property: JsonPropertyName("fire_id")] string FireId,
    [property: JsonPropertyName("model_run_id")] int ModelRunId,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("narrative_version")] string NarrativeVersion,
    [property: JsonPropertyName("paragraf")] string Paragraf,
    [property: JsonPropertyName("profil")] string Profil,
    [property: JsonPropertyName("onaylandi")] bool Onaylandi,
    [property: JsonPropertyName("uretim")] string Uretim,
    [property: JsonPropertyName("sayi_blogu")] JsonElement SayiBlogu);
