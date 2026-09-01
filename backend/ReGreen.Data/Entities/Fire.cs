namespace ReGreen.Data.Entities;

/// <summary>
/// Bir yangın. Kaynak: docs/data-contract.md §2/§2.1, docs/db-schema.md §2.
/// </summary>
public class Fire
{
    public string FireId { get; set; } = null!;
    public DateOnly FireDate { get; set; }
    public string Province { get; set; } = null!;
    public string Region { get; set; } = null!;
    public double ModisAreaHa { get; set; }
    public double BurnedAreaHa { get; set; }
    public int CellSizeM { get; set; }

    public bool HasPerimeter { get; set; }
    public string PerimeterGeoJson { get; set; } = null!;

    public double MarkerLat { get; set; }
    public double MarkerLon { get; set; }

    public string QualityFlag { get; set; } = null!;
    public string? QualityNote { get; set; }

    public List<Cell> Cells { get; set; } = [];
    public List<ModelRun> ModelRuns { get; set; } = [];

    /// <summary>Model teslimatı başına opsiyonel anlatı.</summary>
    public List<FireNarrative> Narratives { get; set; } = [];

    /// <summary>Bu alan için kurumların açtığı saha etkinlikleri.</summary>
    public List<FieldActivity> Activities { get; set; } = [];

    /// <summary>Bu alan için gönüllülerin kaydettiği saha gözlemleri.</summary>
    public List<FieldObservation> Observations { get; set; } = [];
}
