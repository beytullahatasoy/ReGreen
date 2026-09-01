namespace ImportTool.Validation;

/// <summary>docs/data-contract.md §4/§6 — 53 yangının tamamı taranarak doğrulanmış gerçek enum'lar.</summary>
public static class ContractEnums
{
    public static readonly HashSet<string> SeverityClasses = ["dusuk", "orta-dusuk", "orta-yuksek", "yuksek"];

    public static readonly HashSet<string> LandCovers =
        ["Agaclik", "Ciplak", "Otlak/calilik", "Su", "Sulak alan", "Tarim", "Yerlesim"];

    public static readonly HashSet<string> PredictionStatuses = ["predicted", "low_severity", "no_data"];

    public static readonly HashSet<string> PriorityClasses = ["COK_YUKSEK", "YUKSEK", "ORTA", "DUSUK"];

    public static readonly HashSet<string> QualityFlags = ["ok", "check"];

    /// <summary>docs/hukum_sozlesmesi.md "Hüküm kodları" — 7 değer, sırayla denenen kurallar.</summary>
    public static readonly HashSet<string> HukumCodes =
    [
        "KAPSAM_DISI", "SAHA_KONTROL", "IZLE", "EROZYON_ONCE",
        "DIKIM_ADAYI", "ONCELIGE_GORE", "GENCLESME_IZLE",
    ];

    /// <summary>docs/hukum_sozlesmesi.md "Ek koşul kodları" — hükmün üstüne biner, 0..N tane.</summary>
    public static readonly HashSet<string> EkKosulCodes =
    [
        "ERISIM_ZOR", "ESKIDEN_ORMAN_DEGIL", "SEYREK_ORTU",
        "DIK_YAMAC", "AGIR_YANMIS", "DUSUK_GUVEN",
    ];

    /// <summary>docs/hukum_sozlesmesi.md "Anlatı profili" — yangin_metinleri.json `profil`.</summary>
    public static readonly HashSet<string> FireNarrativeProfiles =
    [
        "yogun_mudahale", "karisik", "kendi_toparlaniyor",
        "dik_arazi", "belirsiz", "kapsam_dar",
    ];
}
