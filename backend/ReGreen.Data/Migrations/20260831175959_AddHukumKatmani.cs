using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGreen.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHukumKatmani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CellVerdicts",
                columns: table => new
                {
                    CellId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Hukum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EkKosullar = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ToparlanmaOrani = table.Column<double>(type: "float", nullable: true),
                    TurOnerisi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Tetikleyen = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Ozet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ayrinti = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZamanlamaNotuVar = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CellVerdicts", x => x.CellId);
                    table.CheckConstraint("CK_CellVerdicts_Hukum", "Hukum IN ('KAPSAM_DISI', 'SAHA_KONTROL', 'IZLE', 'EROZYON_ONCE', 'DIKIM_ADAYI', 'ONCELIGE_GORE', 'GENCLESME_IZLE')");
                    table.CheckConstraint("CK_CellVerdicts_ToparlanmaOrani_Range", "ToparlanmaOrani IS NULL OR ToparlanmaOrani BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_CellVerdicts_Cells_FireId_CellId",
                        columns: x => new { x.FireId, x.CellId },
                        principalTable: "Cells",
                        principalColumns: new[] { "FireId", "CellId" });
                });

            migrationBuilder.CreateTable(
                name: "FireNarratives",
                columns: table => new
                {
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Paragraf = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Profil = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Onaylandi = table.Column<bool>(type: "bit", nullable: false),
                    Uretim = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SayiBlogu = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FireNarratives", x => x.FireId);
                    table.CheckConstraint("CK_FireNarratives_Profil", "Profil IN ('yogun_mudahale', 'karisik', 'kendi_toparlaniyor', 'dik_arazi', 'belirsiz', 'kapsam_dar')");
                    table.ForeignKey(
                        name: "FK_FireNarratives_Fires_FireId",
                        column: x => x.FireId,
                        principalTable: "Fires",
                        principalColumn: "FireId");
                });

            migrationBuilder.CreateTable(
                name: "HukumSozlugu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Surum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JsonIcerik = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HukumSozlugu", x => x.Id);
                    table.CheckConstraint("CK_HukumSozlugu_SingleRow", "Id = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CellVerdicts_FireId_CellId",
                table: "CellVerdicts",
                columns: new[] { "FireId", "CellId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CellVerdicts");

            migrationBuilder.DropTable(
                name: "FireNarratives");

            migrationBuilder.DropTable(
                name: "HukumSozlugu");
        }
    }
}
