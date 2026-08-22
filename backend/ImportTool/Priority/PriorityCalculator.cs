using ImportTool.Models;

namespace ImportTool.Priority;

/// <summary>
/// sample-data/backend-data/oncelik.py'nin C# portu. Referans uygulamayla BİREBİR aynı
/// davranmalı — bkz. docs/data-contract.md §8, docs/import-flow.md §3.6.
/// </summary>
public static class PriorityCalculator
{
    public const string CokYuksek = "COK_YUKSEK";
    public const string Yuksek = "YUKSEK";
    public const string Orta = "ORTA";
    public const string Dusuk = "DUSUK";

    public readonly record struct NormalizedWeights(double Recovery, double Erosion, double Access);

    /// <summary>Ağırlıklar toplamı 1 olacak şekilde normalize edilir (oncelik.py: VARSAYILAN_AGIRLIK / toplam).</summary>
    public static NormalizedWeights NormalizeWeights(PriorityWeights weights)
    {
        var total = weights.Recovery + weights.Erosion + weights.Access;
        if (total <= 0)
            throw new ArgumentException("Ağırlıkların toplamı pozitif olmalı.", nameof(weights));

        return new NormalizedWeights(
            weights.Recovery / total,
            weights.Erosion / total,
            weights.Access / total);
    }

    /// <summary>
    /// oncelik.py: _normalize(). max-min &lt; 1e-9 veya min/max yoksa 0.5 döner (yazı-tura
    /// değil, gerçekten "referans yok" durumunun nötr karşılığı — dosyada da böyle davranıyor).
    /// </summary>
    public static double Normalize(double value, double? lo, double? hi)
    {
        if (lo is not { } loVal || hi is not { } hiVal
            || !double.IsFinite(loVal) || !double.IsFinite(hiVal)
            || hiVal - loVal < 1e-9)
        {
            return 0.5;
        }

        var n = (value - loVal) / (hiVal - loVal);
        return Math.Clamp(n, 0.0, 1.0);
    }

    /// <summary>
    /// predicted bir hücre için priority_score. Pandas'ın .round(4)'ü round-half-to-even
    /// kullandığı için burada da MidpointRounding.ToEven kullanılır — CSV'deki değerle
    /// birebir eşleşmesi için gerekli (bkz. import-flow.md §3.6, "ham double eşitliği kullanılmaz").
    /// </summary>
    public static double ComputeScore(
        double recoveryGapPred, double slopeDeg, double roadDistanceKm,
        NormalizationReference norm, PriorityWeights weights)
    {
        var w = NormalizeWeights(weights);

        var nRecovery = Normalize(recoveryGapPred, norm.RecoveryGapPred.Min, norm.RecoveryGapPred.Max);
        var nSlope = Normalize(slopeDeg, norm.SlopeDeg.Min, norm.SlopeDeg.Max);
        var nRoad = Normalize(roadDistanceKm, norm.RoadDistanceKm.Min, norm.RoadDistanceKm.Max);

        var raw = w.Recovery * nRecovery + w.Erosion * nSlope + w.Access * (1 - nRoad);
        return Math.Round(raw, 4, MidpointRounding.ToEven);
    }

    /// <summary>oncelik.py: sinif_ata(). Eşikler fire'ın kendi metadata'sından gelir, sabit değildir.</summary>
    public static string Classify(double score, PriorityThresholds thresholds)
    {
        if (!double.IsFinite(score)) return Dusuk;
        if (score >= thresholds.CokYuksek) return CokYuksek;
        if (score >= thresholds.Yuksek) return Yuksek;
        if (score >= thresholds.Orta) return Orta;
        return Dusuk;
    }

    /// <summary>
    /// İki değeri, ondalık yuvarlama gürültüsüne tolerans tanıyarak karşılaştırır.
    /// docs/import-flow.md §3.6: "ham double eşitliği kullanılmaz".
    /// </summary>
    public static bool ApproximatelyEqual(double a, double b, double tolerance = 1e-9)
        => Math.Abs(a - b) <= tolerance * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
}
