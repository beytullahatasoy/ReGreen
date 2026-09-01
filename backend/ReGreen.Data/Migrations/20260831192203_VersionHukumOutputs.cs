using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGreen.Data.Migrations
{
    /// <inheritdoc />
    public partial class VersionHukumOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives");

            migrationBuilder.DropIndex(
                name: "IX_FireNarratives_FireId_ModelRunId",
                table: "FireNarratives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives",
                columns: new[] { "ModelRunId", "NarrativeVersion" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts",
                columns: new[] { "CellId", "ModelRunId", "HukumSozluguSurum" });

            migrationBuilder.CreateIndex(
                name: "IX_FireNarratives_FireId_ModelRunId",
                table: "FireNarratives",
                columns: new[] { "FireId", "ModelRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FireNarratives",
                table: "FireNarratives");

            migrationBuilder.DropIndex(
                name: "IX_FireNarratives_FireId_ModelRunId",
                table: "FireNarratives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CellVerdicts",
                table: "CellVerdicts");

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
        }
    }
}
