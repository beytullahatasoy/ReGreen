namespace ReGreen.Data.Entities;

/// <summary>
/// Bir yangın + teslimat kombinasyonu (bkz. data-contract §11: normalization_reference
/// fire'a özel olduğu için tek AI teslimatı 53 ayrı ModelRun üretir).
/// Kaynak: docs/data-contract.md §8/§9.3, docs/db-schema.md §4.
/// </summary>
public class ModelRun
{
    public int Id { get; set; }
    public string FireId { get; set; } = null!;
    public Fire Fire { get; set; } = null!;

    public string ModelVersion { get; set; } = null!;
    public DateTimeOffset GeneratedAt { get; set; }
    public string SchemaVersion { get; set; } = null!;

    public bool InTrainingSet { get; set; }
    public int OutOfFoldCells { get; set; }

    public double NormRecoveryGapMin { get; set; }
    public double NormRecoveryGapMax { get; set; }
    public double NormSlopeMin { get; set; }
    public double NormSlopeMax { get; set; }
    public double NormRoadDistMin { get; set; }
    public double NormRoadDistMax { get; set; }

    public double DefaultWeightRecovery { get; set; }
    public double DefaultWeightErosion { get; set; }
    public double DefaultWeightAccess { get; set; }

    public double ThresholdVeryHigh { get; set; }
    public double ThresholdHigh { get; set; }
    public double ThresholdMedium { get; set; }

    public DateTime ImportedAt { get; set; }

    public List<Prediction> Predictions { get; set; } = [];
}
