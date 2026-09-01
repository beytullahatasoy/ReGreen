using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGreen.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixHukumModelRunLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_HukumSozlugu",
                table: "HukumSozlugu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HukumSozlugu_SingleRow",
                table: "HukumSozlugu");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts");

            migrationBuilder.DropIndex(
                name: "IX_CellVerdicts_FireId_CellId",
                table: "CellVerdicts");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "HukumSozlugu");

            migrationBuilder.AddColumn<int>(
                name: "ModelRunId",
                table: "FireNarratives",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeVersion",
                table: "FireNarratives",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelRunId",
                table: "CellVerdicts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HukumSozluguSurum",
                table: "CellVerdicts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // AddHukumKatmani daha önce uygulanmış ve backfill edilmiş bir geliştirme DB'sini
            // güvenle yükselt: mevcut hüküm/anlatıyı ilgili yangının en güncel ModelRun'ına bağla.
            migrationBuilder.Sql("""
                UPDATE cv
                SET ModelRunId = mr.Id,
                    HukumSozluguSurum = hs.Surum
                FROM CellVerdicts cv
                CROSS APPLY (
                    SELECT TOP (1) Id FROM ModelRuns
                    WHERE FireId = cv.FireId
                    ORDER BY GeneratedAt DESC, Id DESC
                ) mr
                CROSS APPLY (
                    SELECT TOP (1) Surum FROM HukumSozlugu
                    ORDER BY ImportedAt DESC, Surum DESC
                ) hs;

                UPDATE fn
                SET ModelRunId = mr.Id,
                    NarrativeVersion = '1.0'
                FROM FireNarratives fn
                CROSS APPLY (
                    SELECT TOP (1) Id FROM ModelRuns
                    WHERE FireId = fn.FireId
                    ORDER BY GeneratedAt DESC, Id DESC
                ) mr;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ModelRunId", table: "FireNarratives", type: "int", nullable: false,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "NarrativeVersion", table: "FireNarratives", type: "nvarchar(20)",
                maxLength: 20, nullable: false, oldClrType: typeof(string),
                oldType: "nvarchar(20)", oldMaxLength: 20, oldNullable: true);
            migrationBuilder.AlterColumn<int>(
                name: "ModelRunId", table: "CellVerdicts", type: "int", nullable: false,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "HukumSozluguSurum", table: "CellVerdicts", type: "nvarchar(20)",
                maxLength: 20, nullable: false, oldClrType: typeof(string),
                oldType: "nvarchar(20)", oldMaxLength: 20, oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_HukumSozlugu",
                table: "HukumSozlugu",
                column: "Surum");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives",
                column: "ModelRunId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts",
                columns: new[] { "CellId", "ModelRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_FireNarratives_FireId_ModelRunId",
                table: "FireNarratives",
                columns: new[] { "FireId", "ModelRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CellVerdicts_FireId_CellId",
                table: "CellVerdicts",
                columns: new[] { "FireId", "CellId" });

            migrationBuilder.CreateIndex(
                name: "IX_CellVerdicts_FireId_ModelRunId",
                table: "CellVerdicts",
                columns: new[] { "FireId", "ModelRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_CellVerdicts_HukumSozluguSurum",
                table: "CellVerdicts",
                column: "HukumSozluguSurum");

            migrationBuilder.CreateIndex(
                name: "IX_CellVerdicts_ModelRunId_CellId",
                table: "CellVerdicts",
                columns: new[] { "ModelRunId", "CellId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CellVerdicts_HukumSozlugu_HukumSozluguSurum",
                table: "CellVerdicts",
                column: "HukumSozluguSurum",
                principalTable: "HukumSozlugu",
                principalColumn: "Surum");

            migrationBuilder.AddForeignKey(
                name: "FK_CellVerdicts_ModelRuns_FireId_ModelRunId",
                table: "CellVerdicts",
                columns: new[] { "FireId", "ModelRunId" },
                principalTable: "ModelRuns",
                principalColumns: new[] { "FireId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_FireNarratives_ModelRuns_FireId_ModelRunId",
                table: "FireNarratives",
                columns: new[] { "FireId", "ModelRunId" },
                principalTable: "ModelRuns",
                principalColumns: new[] { "FireId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CellVerdicts_HukumSozlugu_HukumSozluguSurum",
                table: "CellVerdicts");

            migrationBuilder.DropForeignKey(
                name: "FK_CellVerdicts_ModelRuns_FireId_ModelRunId",
                table: "CellVerdicts");

            migrationBuilder.DropForeignKey(
                name: "FK_FireNarratives_ModelRuns_FireId_ModelRunId",
                table: "FireNarratives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_HukumSozlugu",
                table: "HukumSozlugu");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives");

            migrationBuilder.DropIndex(
                name: "IX_FireNarratives_FireId_ModelRunId",
                table: "FireNarratives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts");

            migrationBuilder.DropIndex(
                name: "IX_CellVerdicts_FireId_CellId",
                table: "CellVerdicts");

            migrationBuilder.DropIndex(
                name: "IX_CellVerdicts_FireId_ModelRunId",
                table: "CellVerdicts");

            migrationBuilder.DropIndex(
                name: "IX_CellVerdicts_HukumSozluguSurum",
                table: "CellVerdicts");

            migrationBuilder.DropIndex(
                name: "IX_CellVerdicts_ModelRunId_CellId",
                table: "CellVerdicts");

            migrationBuilder.DropColumn(
                name: "ModelRunId",
                table: "FireNarratives");

            migrationBuilder.DropColumn(
                name: "NarrativeVersion",
                table: "FireNarratives");

            migrationBuilder.DropColumn(
                name: "ModelRunId",
                table: "CellVerdicts");

            migrationBuilder.DropColumn(
                name: "HukumSozluguSurum",
                table: "CellVerdicts");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "HukumSozlugu",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_HukumSozlugu",
                table: "HukumSozlugu",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives",
                column: "FireId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts",
                column: "CellId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HukumSozlugu_SingleRow",
                table: "HukumSozlugu",
                sql: "Id = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CellVerdicts_FireId_CellId",
                table: "CellVerdicts",
                columns: new[] { "FireId", "CellId" },
                unique: true);
        }
    }
}
