using ImportTool.Models;
using ImportTool.Priority;
using Xunit;

namespace ImportTool.Tests.Unit;

/// <summary>
/// oncelik.py referans davranışına karşı. Değerler sample-data/backend-data'daki GERÇEK
/// satırlardan alınmıştır (AKD_2021_05_000160, AKD_2021_05_metadata.json) — uydurma değil.
/// </summary>
public class PriorityCalculatorTests
{
    private static readonly PriorityWeights DefaultWeights = new() { Recovery = 0.5, Erosion = 0.3, Access = 0.2 };

    private static readonly NormalizationReference Akd202105Norm = new()
    {
        RecoveryGapPred = new NormRange { Min = 0.1796, Max = 0.4305 },
        SlopeDeg = new NormRange { Min = 0.5991507, Max = 16.264103 },
        RoadDistanceKm = new NormRange { Min = 0.02827684, Max = 1.9335308 },
    };

    [Fact]
    public void ComputeScore_MatchesRealCsvRow_AKD_2021_05_000160()
    {
        // AKD_2021_05_hucreler.csv, satır AKD_2021_05_000160: priority_score=0.2511, priority_class=ORTA
        var score = PriorityCalculator.ComputeScore(
            recoveryGapPred: 0.1914, slopeDeg: 5.2742534, roadDistanceKm: 0.6179377,
            Akd202105Norm, DefaultWeights);

        Assert.Equal(0.2511, score, precision: 4);
        Assert.Equal(PriorityCalculator.Orta, PriorityCalculator.Classify(score, DefaultThresholds()));
    }

    [Fact]
    public void ComputeScore_MatchesRealCsvRow_AKD_2021_01_032026_HighestPriority()
    {
        // ornek_hucreler.json'daki dogrulanmis ornek: skor 0,8129, sinif COK_YUKSEK
        var norm = new NormalizationReference
        {
            RecoveryGapPred = new NormRange { Min = 0.1243, Max = 0.5417 },
            SlopeDeg = new NormRange { Min = 0.05127889, Max = 50.46333 },
            RoadDistanceKm = new NormRange { Min = 0.0031909307, Max = 2.9277885 },
        };

        var score = PriorityCalculator.ComputeScore(
            recoveryGapPred: 0.4826, slopeDeg: 37.271362, roadDistanceKm: 0.55603516,
            norm, DefaultWeights);

        Assert.Equal(0.8129, score, precision: 4);
        Assert.Equal(PriorityCalculator.CokYuksek, PriorityCalculator.Classify(score, DefaultThresholds()));
    }

    [Fact]
    public void Normalize_DegenerateRange_ReturnsNeutralHalf()
    {
        // oncelik.py: _normalize() -> max-min < 1e-9 ise 0.5 (yazi-tura degil, "referans yok")
        Assert.Equal(0.5, PriorityCalculator.Normalize(5.0, 3.0, 3.0));
        Assert.Equal(0.5, PriorityCalculator.Normalize(5.0, null, 10.0));
        Assert.Equal(0.5, PriorityCalculator.Normalize(5.0, 3.0, null));
    }

    [Fact]
    public void Normalize_ClampsOutsideRange()
    {
        Assert.Equal(0.0, PriorityCalculator.Normalize(-5.0, 0.0, 10.0));
        Assert.Equal(1.0, PriorityCalculator.Normalize(50.0, 0.0, 10.0));
        Assert.Equal(0.5, PriorityCalculator.Normalize(5.0, 0.0, 10.0));
    }

    [Fact]
    public void NormalizeWeights_SumsToOne_RegardlessOfInputScale()
    {
        // data-contract §8.2: kullanici 0.5/0.5/0.5 girse bile 0.333/0.333/0.333'e cevrilir
        var w = PriorityCalculator.NormalizeWeights(new PriorityWeights { Recovery = 0.5, Erosion = 0.5, Access = 0.5 });
        Assert.Equal(1.0 / 3, w.Recovery, precision: 9);
        Assert.Equal(1.0 / 3, w.Erosion, precision: 9);
        Assert.Equal(1.0 / 3, w.Access, precision: 9);
    }

    [Fact]
    public void NormalizeWeights_ZeroTotal_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PriorityCalculator.NormalizeWeights(new PriorityWeights { Recovery = 0, Erosion = 0, Access = 0 }));
    }

    [Theory]
    [InlineData(0.75, "COK_YUKSEK")]
    [InlineData(0.80, "COK_YUKSEK")]
    [InlineData(0.50, "YUKSEK")]
    [InlineData(0.74, "YUKSEK")]
    [InlineData(0.25, "ORTA")]
    [InlineData(0.49, "ORTA")]
    [InlineData(0.24, "DUSUK")]
    [InlineData(0.0, "DUSUK")]
    public void Classify_UsesFireSpecificThresholds_NotHardcoded(double score, string expected)
    {
        Assert.Equal(expected, PriorityCalculator.Classify(score, DefaultThresholds()));
    }

    [Fact]
    public void Classify_DifferentThresholdsPerModelRun_ChangeResult()
    {
        // Ayni skor, farkli bir ModelRun'in esikleriyle farkli sinifa dusebilir.
        var strictThresholds = new PriorityThresholds { CokYuksek = 0.95, Yuksek = 0.90, Orta = 0.85 };
        Assert.Equal(PriorityCalculator.Dusuk, PriorityCalculator.Classify(0.81, strictThresholds));
        Assert.Equal(PriorityCalculator.CokYuksek, PriorityCalculator.Classify(0.81, DefaultThresholds()));
    }

    [Fact]
    public void ApproximatelyEqual_ToleratesFloatingPointNoise_ButNotRealDifference()
    {
        Assert.True(PriorityCalculator.ApproximatelyEqual(0.1 + 0.2, 0.3));
        Assert.False(PriorityCalculator.ApproximatelyEqual(0.2511, 0.2512, tolerance: 1e-9));
    }

    private static PriorityThresholds DefaultThresholds() => new() { CokYuksek = 0.75, Yuksek = 0.50, Orta = 0.25 };
}
