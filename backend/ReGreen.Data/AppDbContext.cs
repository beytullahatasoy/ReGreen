using Microsoft.EntityFrameworkCore;
using ReGreen.Data.Entities;

namespace ReGreen.Data;

/// <summary>
/// backend/db/schema.sql'in EF Core Code-First karşılığı. Kolon tipleri, NOT NULL,
/// CHECK ve composite FK kısıtları schema.sql ile birebir eşleşecek şekilde
/// yapılandırılmıştır — bkz. docs/db-schema.md §8 (EF Core notu).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Fire> Fires => Set<Fire>();
    public DbSet<Cell> Cells => Set<Cell>();
    public DbSet<ModelRun> ModelRuns => Set<ModelRun>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<CellVerdict> CellVerdicts => Set<CellVerdict>();
    public DbSet<FireNarrative> FireNarratives => Set<FireNarrative>();
    public DbSet<HukumSozlugu> HukumSozlugu => Set<HukumSozlugu>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureFires(modelBuilder);
        ConfigureCells(modelBuilder);
        ConfigureModelRuns(modelBuilder);
        ConfigurePredictions(modelBuilder);
        ConfigureCellVerdicts(modelBuilder);
        ConfigureFireNarratives(modelBuilder);
        ConfigureHukumSozlugu(modelBuilder);
    }

    private static void ConfigureFires(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fire>(e =>
        {
            e.ToTable("Fires", t => t.HasCheckConstraint(
                "CK_Fires_QualityFlag", "QualityFlag IN ('ok', 'check')"));
            e.ToTable(t => t.HasCheckConstraint(
                "CK_Fires_HasPerimeter", "HasPerimeter = 1"));

            e.HasKey(f => f.FireId);
            e.Property(f => f.FireId).HasMaxLength(50);
            e.Property(f => f.Province).HasMaxLength(100);
            e.Property(f => f.Region).HasMaxLength(100);
            e.Property(f => f.QualityFlag).HasMaxLength(10);
            e.Property(f => f.QualityNote).HasMaxLength(300);
            e.Property(f => f.PerimeterGeoJson).HasColumnType("nvarchar(max)");
        });
    }

    private static void ConfigureCells(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cell>(e =>
        {
            e.ToTable("Cells", t =>
            {
                t.HasCheckConstraint("CK_Cells_SeverityClass",
                    "SeverityClass IN ('dusuk', 'orta-dusuk', 'orta-yuksek', 'yuksek')");
                t.HasCheckConstraint("CK_Cells_LandCover",
                    "LandCover IS NULL OR LandCover IN " +
                    "('Agaclik', 'Ciplak', 'Otlak/calilik', 'Su', 'Sulak alan', 'Tarim', 'Yerlesim')");
            });

            e.HasKey(c => c.CellId);
            e.Property(c => c.CellId).HasMaxLength(50);
            e.Property(c => c.FireId).HasMaxLength(50);
            e.Property(c => c.SeverityClass).HasMaxLength(30);
            e.Property(c => c.LandCover).HasMaxLength(50);

            // Predictions'ın (FireId, CellId) composite FK'si için — bkz. ConfigurePredictions.
            e.HasAlternateKey(c => new { c.FireId, c.CellId }).HasName("UQ_Cells_FireId_CellId");

            e.HasOne(c => c.Fire)
                .WithMany(f => f.Cells)
                .HasForeignKey(c => c.FireId)
                .OnDelete(DeleteBehavior.NoAction); // schema.sql'de ON DELETE belirtilmiyor (varsayılan NO ACTION)

            // Ayrı bir IX_Cells_FireId YOK (bilerek) — UQ_Cells_FireId_CellId zaten FireId
            // önekli bir indeks üretiyor, tekrarı docs/db-schema.md §2/§6'da kaldırıldı.
        });
    }

    private static void ConfigureModelRuns(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModelRun>(e =>
        {
            e.ToTable("ModelRuns", t =>
            {
                t.HasCheckConstraint("CK_ModelRuns_OutOfFoldCells", "OutOfFoldCells >= 0");
                t.HasCheckConstraint("CK_ModelRuns_OutOfFold_InTraining",
                    "InTrainingSet = 1 OR OutOfFoldCells = 0");
                t.HasCheckConstraint("CK_ModelRuns_Weights",
                    "DefaultWeightRecovery >= 0 AND DefaultWeightErosion >= 0 AND DefaultWeightAccess >= 0 " +
                    "AND (DefaultWeightRecovery + DefaultWeightErosion + DefaultWeightAccess) > 0");
                t.HasCheckConstraint("CK_ModelRuns_NormRanges",
                    "NormRecoveryGapMin <= NormRecoveryGapMax " +
                    "AND NormSlopeMin <= NormSlopeMax " +
                    "AND NormRoadDistMin <= NormRoadDistMax");
                t.HasCheckConstraint("CK_ModelRuns_Thresholds",
                    "ThresholdMedium >= 0 AND ThresholdMedium < ThresholdHigh " +
                    "AND ThresholdHigh < ThresholdVeryHigh AND ThresholdVeryHigh <= 1");
            });

            e.HasKey(m => m.Id);
            e.Property(m => m.FireId).HasMaxLength(50);
            e.Property(m => m.ModelVersion).HasMaxLength(50);
            e.Property(m => m.SchemaVersion).HasMaxLength(20);
            e.Property(m => m.GeneratedAt).HasColumnType("datetimeoffset(0)");
            e.Property(m => m.ImportedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // Predictions'ın (FireId, ModelRunId) composite FK'si için — bkz. ConfigurePredictions.
            e.HasAlternateKey(m => new { m.FireId, m.Id }).HasName("UQ_ModelRuns_FireId_Id");

            e.HasIndex(m => new { m.FireId, m.ModelVersion, m.GeneratedAt })
                .IsUnique()
                .HasDatabaseName("UQ_ModelRuns");

            e.HasIndex(m => new { m.FireId, m.GeneratedAt })
                .HasDatabaseName("IX_ModelRuns_FireId")
                .IsDescending(false, true);

            e.HasOne(m => m.Fire)
                .WithMany(f => f.ModelRuns)
                .HasForeignKey(m => m.FireId)
                .OnDelete(DeleteBehavior.NoAction); // schema.sql'de ON DELETE belirtilmiyor (varsayılan NO ACTION)
        });
    }

    private static void ConfigurePredictions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Prediction>(e =>
        {
            e.ToTable("Predictions", t =>
            {
                // Not: \n escape'leri BİLEREK kullanılıyor (çok satırlı raw string DEĞİL) —
                // raw string kullanılsaydı içerik, checkout'un satır sonu ayarına (CRLF/LF)
                // bağlı olarak derlenirdi ve bu durum EF Core'un modeli, \n ile üretilmiş
                // InitialCreate migration'ındaki metinle birebir eşleşmediği için
                // PendingModelChangesWarning ile reddetmesine yol açardı.
                t.HasCheckConstraint("CK_Predictions_StatusConsistency",
                    "(PredictionStatus = 'predicted'\n" +
                    "    AND RecoveryGapPred IS NOT NULL\n" +
                    "    AND DefaultPriorityScore IS NOT NULL\n" +
                    "    AND DefaultPriorityClass IS NOT NULL)\n" +
                    "OR (PredictionStatus = 'low_severity'\n" +
                    "    AND RecoveryGapPred IS NULL\n" +
                    "    AND DefaultPriorityScore IS NOT NULL\n" +
                    "    AND DefaultPriorityScore = 0.0\n" +
                    "    AND DefaultPriorityClass IS NOT NULL\n" +
                    "    AND DefaultPriorityClass = 'DUSUK')\n" +
                    "OR (PredictionStatus = 'no_data'\n" +
                    "    AND RecoveryGapPred IS NULL\n" +
                    "    AND DefaultPriorityScore IS NULL\n" +
                    "    AND DefaultPriorityClass IS NULL)");
                t.HasCheckConstraint("CK_Predictions_Status",
                    "PredictionStatus IN ('predicted', 'low_severity', 'no_data')");
                t.HasCheckConstraint("CK_Predictions_RecoveryGapPred_Range",
                    "RecoveryGapPred IS NULL OR RecoveryGapPred BETWEEN -0.5 AND 1.5");
                t.HasCheckConstraint("CK_Predictions_PriorityScore_Range",
                    "DefaultPriorityScore IS NULL OR DefaultPriorityScore BETWEEN 0 AND 1");
                t.HasCheckConstraint("CK_Predictions_PriorityClass_Enum",
                    "DefaultPriorityClass IS NULL OR DefaultPriorityClass IN " +
                    "('COK_YUKSEK', 'YUKSEK', 'ORTA', 'DUSUK')");
            });

            e.HasKey(p => p.Id);
            e.Property(p => p.FireId).HasMaxLength(50);
            e.Property(p => p.CellId).HasMaxLength(50);
            e.Property(p => p.PredictionStatus).HasMaxLength(20);
            e.Property(p => p.DefaultPriorityClass).HasMaxLength(20);

            e.HasIndex(p => new { p.CellId, p.ModelRunId })
                .IsUnique()
                .HasDatabaseName("UQ_Predictions");

            e.HasIndex(p => new { p.ModelRunId, p.CellId })
                .HasDatabaseName("IX_Predictions_ModelRunId_CellId");

            // Composite FK'ler — "hücre ve model run aynı yangına ait olmalı" kuralını
            // DB seviyesinde garanti eder (bkz. docs/db-schema.md §1).
            // NoAction: SQL Server, Fires -> Cells/ModelRuns -> Predictions üzerinden
            // BİRDEN FAZLA cascade yoluna izin vermez (hata 1785); schema.sql'in
            // kendisi de zaten ON DELETE belirtmiyor (varsayılan NO ACTION).
            e.HasOne(p => p.Cell)
                .WithMany(c => c.Predictions)
                .HasForeignKey(p => new { p.FireId, p.CellId })
                .HasPrincipalKey(c => new { c.FireId, c.CellId })
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(p => p.ModelRun)
                .WithMany(m => m.Predictions)
                .HasForeignKey(p => new { p.FireId, p.ModelRunId })
                .HasPrincipalKey(m => new { m.FireId, m.Id })
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureCellVerdicts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CellVerdict>(e =>
        {
            e.ToTable("CellVerdicts", t =>
            {
                t.HasCheckConstraint("CK_CellVerdicts_Hukum",
                    "Hukum IN ('KAPSAM_DISI', 'SAHA_KONTROL', 'IZLE', 'EROZYON_ONCE', " +
                    "'DIKIM_ADAYI', 'ONCELIGE_GORE', 'GENCLESME_IZLE')");
                t.HasCheckConstraint("CK_CellVerdicts_ToparlanmaOrani_Range",
                    "ToparlanmaOrani IS NULL OR ToparlanmaOrani BETWEEN 0 AND 1");
            });

            e.HasKey(v => new { v.CellId, v.ModelRunId, v.HukumSozluguSurum });
            e.Property(v => v.CellId).HasMaxLength(50);
            e.Property(v => v.FireId).HasMaxLength(50);
            e.Property(v => v.HukumSozluguSurum).HasMaxLength(20);
            e.Property(v => v.Hukum).HasMaxLength(20);
            e.Property(v => v.EkKosullar).HasMaxLength(120);
            e.Property(v => v.TurOnerisi).HasMaxLength(500);
            e.Property(v => v.Tetikleyen).HasMaxLength(300);
            e.Property(v => v.Ozet).HasColumnType("nvarchar(max)");
            e.Property(v => v.Ayrinti).HasColumnType("nvarchar(max)");

            e.HasOne(v => v.Cell)
                .WithMany(c => c.Verdicts)
                .HasForeignKey(v => new { v.FireId, v.CellId })
                .HasPrincipalKey(c => new { c.FireId, c.CellId })
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(v => v.ModelRun)
                .WithMany(m => m.CellVerdicts)
                .HasForeignKey(v => new { v.FireId, v.ModelRunId })
                .HasPrincipalKey(m => new { m.FireId, m.Id })
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(v => v.HukumSozlugu)
                .WithMany(h => h.CellVerdicts)
                .HasForeignKey(v => v.HukumSozluguSurum)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasIndex(v => new { v.ModelRunId, v.CellId })
                .HasDatabaseName("IX_CellVerdicts_ModelRunId_CellId");
        });
    }

    private static void ConfigureFireNarratives(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FireNarrative>(e =>
        {
            e.ToTable("FireNarratives", t => t.HasCheckConstraint("CK_FireNarratives_Profil",
                "Profil IN ('yogun_mudahale', 'karisik', 'kendi_toparlaniyor', " +
                "'dik_arazi', 'belirsiz', 'kapsam_dar')"));

            e.Property(n => n.FireId).HasMaxLength(50);
            e.Property(n => n.NarrativeVersion).HasMaxLength(20);
            e.Property(n => n.Paragraf).HasColumnType("nvarchar(max)");
            e.Property(n => n.Profil).HasMaxLength(30);
            e.Property(n => n.Uretim).HasMaxLength(100);
            e.Property(n => n.SayiBlogu).HasColumnType("nvarchar(max)");

            e.HasOne(n => n.Fire)
                .WithMany(f => f.Narratives)
                .HasForeignKey(n => n.FireId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(n => n.ModelRun)
                .WithMany(m => m.Narratives)
                .HasForeignKey(n => new { n.FireId, n.ModelRunId })
                .HasPrincipalKey(m => new { m.FireId, m.Id })
                .OnDelete(DeleteBehavior.NoAction);

            e.HasKey(n => new { n.ModelRunId, n.NarrativeVersion });
        });
    }

    private static void ConfigureHukumSozlugu(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HukumSozlugu>(e =>
        {
            e.ToTable("HukumSozlugu");
            e.HasKey(h => h.Surum);
            e.Property(h => h.Surum).HasMaxLength(20);
            e.Property(h => h.JsonIcerik).HasColumnType("nvarchar(max)");
            e.Property(h => h.ImportedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}
