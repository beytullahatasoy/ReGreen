using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGreen.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToplulukKatmani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Verified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Volunteers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Volunteers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrganisationId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ScheduledFor = table.Column<DateOnly>(type: "date", nullable: false),
                    MeetingPoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Requirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldActivities", x => x.Id);
                    table.CheckConstraint("CK_FieldActivities_Capacity", "Capacity > 0 AND Capacity <= 1000");
                    table.CheckConstraint("CK_FieldActivities_Kind", "Kind IN ('planting', 'cleanup', 'erosion_observation', 'vegetation_monitoring', 'field_assessment')");
                    table.CheckConstraint("CK_FieldActivities_Status", "Status IN ('open', 'scheduled', 'closed', 'completed')");
                    table.ForeignKey(
                        name: "FK_FieldActivities_Fires_FireId",
                        column: x => x.FireId,
                        principalTable: "Fires",
                        principalColumn: "FireId");
                    table.ForeignKey(
                        name: "FK_FieldActivities_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ActivityParticipants",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    VolunteerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityParticipants", x => new { x.ActivityId, x.VolunteerId });
                    table.ForeignKey(
                        name: "FK_ActivityParticipants_FieldActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "FieldActivities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivityParticipants_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FieldObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FireId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActivityId = table.Column<int>(type: "int", nullable: true),
                    VolunteerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhotoName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Answers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByOrganisationId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldObservations", x => x.Id);
                    table.CheckConstraint("CK_FieldObservations_Answers", "LEN(Answers) > 0");
                    table.CheckConstraint("CK_FieldObservations_ReviewedAt", "(Status = 'pending' AND ReviewedAt IS NULL) OR (Status <> 'pending' AND ReviewedAt IS NOT NULL)");
                    table.CheckConstraint("CK_FieldObservations_Status", "Status IN ('pending', 'accepted', 'needs_clarification', 'rejected')");
                    table.ForeignKey(
                        name: "FK_FieldObservations_FieldActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "FieldActivities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FieldObservations_Fires_FireId",
                        column: x => x.FireId,
                        principalTable: "Fires",
                        principalColumn: "FireId");
                    table.ForeignKey(
                        name: "FK_FieldObservations_Organisations_ReviewedByOrganisationId",
                        column: x => x.ReviewedByOrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FieldObservations_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityParticipants_VolunteerId",
                table: "ActivityParticipants",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldActivities_FireId",
                table: "FieldActivities",
                column: "FireId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldActivities_OrganisationId",
                table: "FieldActivities",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldActivities_Status_ScheduledFor",
                table: "FieldActivities",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldObservations_ActivityId",
                table: "FieldObservations",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldObservations_FireId",
                table: "FieldObservations",
                column: "FireId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldObservations_ReviewedByOrganisationId",
                table: "FieldObservations",
                column: "ReviewedByOrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldObservations_Status_SubmittedAt",
                table: "FieldObservations",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldObservations_VolunteerId",
                table: "FieldObservations",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "UQ_Organisations_Name",
                table: "Organisations",
                column: "Name",
                unique: true);

            // Kimlik dogrulama katmani henuz yok: Organisation ekrani tek bir kurum
            // olarak calisiyor ve o kurumun var olmasi gerekiyor. Uydurma kurum
            // listesi DEGIL — projenin kendi saha ekibi, tek satir.
            migrationBuilder.InsertData(
                table: "Organisations",
                columns: new[] { "Name", "ContactEmail", "Verified" },
                values: new object[] { "ReGreen Saha Ekibi", null, true });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Organisations", keyColumn: "Name", keyValue: "ReGreen Saha Ekibi");

            migrationBuilder.DropTable(
                name: "ActivityParticipants");

            migrationBuilder.DropTable(
                name: "FieldObservations");

            migrationBuilder.DropTable(
                name: "FieldActivities");

            migrationBuilder.DropTable(
                name: "Volunteers");

            migrationBuilder.DropTable(
                name: "Organisations");
        }
    }
}
