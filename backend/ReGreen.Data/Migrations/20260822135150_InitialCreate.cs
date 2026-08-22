using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGreen.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fires",
                columns: table => new
                {
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModisAreaHa = table.Column<double>(type: "float", nullable: false),
                    BurnedAreaHa = table.Column<double>(type: "float", nullable: false),
                    CellSizeM = table.Column<int>(type: "int", nullable: false),
                    HasPerimeter = table.Column<bool>(type: "bit", nullable: false),
                    PerimeterGeoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarkerLat = table.Column<double>(type: "float", nullable: false),
                    MarkerLon = table.Column<double>(type: "float", nullable: false),
                    QualityFlag = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    QualityNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fires", x => x.FireId);
                    table.CheckConstraint("CK_Fires_HasPerimeter", "HasPerimeter = 1");
                    table.CheckConstraint("CK_Fires_QualityFlag", "QualityFlag IN ('ok', 'check')");
                });

            migrationBuilder.CreateTable(
                name: "Cells",
                columns: table => new
                {
                    CellId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CenterLat = table.Column<double>(type: "float", nullable: false),
                    CenterLon = table.Column<double>(type: "float", nullable: false),
                    TreeCover = table.Column<double>(type: "float", nullable: false),
                    TreeCoverAnnual = table.Column<double>(type: "float", nullable: true),
                    BurnSeverityDnbr = table.Column<double>(type: "float", nullable: false),
                    SlopeDeg = table.Column<double>(type: "float", nullable: false),
                    ElevationM = table.Column<double>(type: "float", nullable: true),
                    RoadDistanceKm = table.Column<double>(type: "float", nullable: false),
                    NdviBefore = table.Column<double>(type: "float", nullable: false),
                    NdviAfter = table.Column<double>(type: "float", nullable: false),
                    NdviDrop = table.Column<double>(type: "float", nullable: false),
                    SeverityClass = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LandCover = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cells", x => x.CellId);
                    table.UniqueConstraint("UQ_Cells_FireId_CellId", x => new { x.FireId, x.CellId });
                    table.CheckConstraint("CK_Cells_LandCover", "LandCover IS NULL OR LandCover IN ('Agaclik', 'Ciplak', 'Otlak/calilik', 'Su', 'Sulak alan', 'Tarim', 'Yerlesim')");
                    table.CheckConstraint("CK_Cells_SeverityClass", "SeverityClass IN ('dusuk', 'orta-dusuk', 'orta-yuksek', 'yuksek')");
                    table.ForeignKey(
                        name: "FK_Cells_Fires_FireId",
                        column: x => x.FireId,
                        principalTable: "Fires",
                        principalColumn: "FireId");
                });

            migrationBuilder.CreateTable(
                name: "ModelRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InTrainingSet = table.Column<bool>(type: "bit", nullable: false),
                    OutOfFoldCells = table.Column<int>(type: "int", nullable: false),
                    NormRecoveryGapMin = table.Column<double>(type: "float", nullable: false),
                    NormRecoveryGapMax = table.Column<double>(type: "float", nullable: false),
                    NormSlopeMin = table.Column<double>(type: "float", nullable: false),
                    NormSlopeMax = table.Column<double>(type: "float", nullable: false),
                    NormRoadDistMin = table.Column<double>(type: "float", nullable: false),
                    NormRoadDistMax = table.Column<double>(type: "float", nullable: false),
                    DefaultWeightRecovery = table.Column<double>(type: "float", nullable: false),
                    DefaultWeightErosion = table.Column<double>(type: "float", nullable: false),
                    DefaultWeightAccess = table.Column<double>(type: "float", nullable: false),
                    ThresholdVeryHigh = table.Column<double>(type: "float", nullable: false),
                    ThresholdHigh = table.Column<double>(type: "float", nullable: false),
                    ThresholdMedium = table.Column<double>(type: "float", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelRuns", x => x.Id);
                    table.UniqueConstraint("UQ_ModelRuns_FireId_Id", x => new { x.FireId, x.Id });
                    table.CheckConstraint("CK_ModelRuns_NormRanges", "NormRecoveryGapMin <= NormRecoveryGapMax AND NormSlopeMin <= NormSlopeMax AND NormRoadDistMin <= NormRoadDistMax");
                    table.CheckConstraint("CK_ModelRuns_OutOfFold_InTraining", "InTrainingSet = 1 OR OutOfFoldCells = 0");
                    table.CheckConstraint("CK_ModelRuns_OutOfFoldCells", "OutOfFoldCells >= 0");
                    table.CheckConstraint("CK_ModelRuns_Thresholds", "ThresholdMedium >= 0 AND ThresholdMedium < ThresholdHigh AND ThresholdHigh < ThresholdVeryHigh AND ThresholdVeryHigh <= 1");
                    table.CheckConstraint("CK_ModelRuns_Weights", "DefaultWeightRecovery >= 0 AND DefaultWeightErosion >= 0 AND DefaultWeightAccess >= 0 AND (DefaultWeightRecovery + DefaultWeightErosion + DefaultWeightAccess) > 0");
                    table.ForeignKey(
                        name: "FK_ModelRuns_Fires_FireId",
                        column: x => x.FireId,
                        principalTable: "Fires",
                        principalColumn: "FireId");
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CellId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModelRunId = table.Column<int>(type: "int", nullable: false),
                    PredictionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecoveryGapPred = table.Column<double>(type: "float", nullable: true),
                    DefaultPriorityScore = table.Column<double>(type: "float", nullable: true),
                    DefaultPriorityClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.CheckConstraint("CK_Predictions_PriorityClass_Enum", "DefaultPriorityClass IS NULL OR DefaultPriorityClass IN ('COK_YUKSEK', 'YUKSEK', 'ORTA', 'DUSUK')");
                    table.CheckConstraint("CK_Predictions_PriorityScore_Range", "DefaultPriorityScore IS NULL OR DefaultPriorityScore BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Predictions_RecoveryGapPred_Range", "RecoveryGapPred IS NULL OR RecoveryGapPred BETWEEN -0.5 AND 1.5");
                    table.CheckConstraint("CK_Predictions_Status", "PredictionStatus IN ('predicted', 'low_severity', 'no_data')");
                    table.CheckConstraint("CK_Predictions_StatusConsistency", "(PredictionStatus = 'predicted'\n    AND RecoveryGapPred IS NOT NULL\n    AND DefaultPriorityScore IS NOT NULL\n    AND DefaultPriorityClass IS NOT NULL)\nOR (PredictionStatus = 'low_severity'\n    AND RecoveryGapPred IS NULL\n    AND DefaultPriorityScore IS NOT NULL\n    AND DefaultPriorityScore = 0.0\n    AND DefaultPriorityClass IS NOT NULL\n    AND DefaultPriorityClass = 'DUSUK')\nOR (PredictionStatus = 'no_data'\n    AND RecoveryGapPred IS NULL\n    AND DefaultPriorityScore IS NULL\n    AND DefaultPriorityClass IS NULL)");
                    table.ForeignKey(
                        name: "FK_Predictions_Cells_FireId_CellId",
                        columns: x => new { x.FireId, x.CellId },
                        principalTable: "Cells",
                        principalColumns: new[] { "FireId", "CellId" });
                    table.ForeignKey(
                        name: "FK_Predictions_ModelRuns_FireId_ModelRunId",
                        columns: x => new { x.FireId, x.ModelRunId },
                        principalTable: "ModelRuns",
                        principalColumns: new[] { "FireId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelRuns_FireId",
                table: "ModelRuns",
                columns: new[] { "FireId", "GeneratedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_ModelRuns",
                table: "ModelRuns",
                columns: new[] { "FireId", "ModelVersion", "GeneratedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_FireId_CellId",
                table: "Predictions",
                columns: new[] { "FireId", "CellId" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_FireId_ModelRunId",
                table: "Predictions",
                columns: new[] { "FireId", "ModelRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ModelRunId_CellId",
                table: "Predictions",
                columns: new[] { "ModelRunId", "CellId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Predictions",
                table: "Predictions",
                columns: new[] { "CellId", "ModelRunId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "Cells");

            migrationBuilder.DropTable(
                name: "ModelRuns");

            migrationBuilder.DropTable(
                name: "Fires");
        }
    }
}
