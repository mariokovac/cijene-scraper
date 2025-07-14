using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CijeneScraper.Migrations
{
    /// <inheritdoc />
    public partial class DbLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitiatedBy",
                table: "ScrapingJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForced",
                table: "ScrapingJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PriceChanges",
                table: "ScrapingJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ScrapingJobLogId",
                table: "ScrapingJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "ScrapingJobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "ApplicationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: true),
                    Properties = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrapingJobLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainID = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Running"),
                    InitiatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsForced = table.Column<bool>(type: "boolean", nullable: false),
                    StoresProcessed = table.Column<int>(type: "integer", nullable: true),
                    ProductsFound = table.Column<int>(type: "integer", nullable: true),
                    PriceChanges = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    SuccessMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorStackTrace = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapingJobLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrapingJobLogs_Chains_ChainID",
                        column: x => x.ChainID,
                        principalTable: "Chains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapingJobs_ScrapingJobLogId",
                table: "ScrapingJobs",
                column: "ScrapingJobLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Category_Timestamp",
                table: "ApplicationLogs",
                columns: new[] { "Category", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Level_Timestamp",
                table: "ApplicationLogs",
                columns: new[] { "Level", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Timestamp",
                table: "ApplicationLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapingJobLogs_ChainID_Date_StartedAt",
                table: "ScrapingJobLogs",
                columns: new[] { "ChainID", "Date", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapingJobLogs_Status_StartedAt",
                table: "ScrapingJobLogs",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapingJobs_ScrapingJobLogs_ScrapingJobLogId",
                table: "ScrapingJobs",
                column: "ScrapingJobLogId",
                principalTable: "ScrapingJobLogs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScrapingJobs_ScrapingJobLogs_ScrapingJobLogId",
                table: "ScrapingJobs");

            migrationBuilder.DropTable(
                name: "ApplicationLogs");

            migrationBuilder.DropTable(
                name: "ScrapingJobLogs");

            migrationBuilder.DropIndex(
                name: "IX_ScrapingJobs_ScrapingJobLogId",
                table: "ScrapingJobs");

            migrationBuilder.DropColumn(
                name: "InitiatedBy",
                table: "ScrapingJobs");

            migrationBuilder.DropColumn(
                name: "IsForced",
                table: "ScrapingJobs");

            migrationBuilder.DropColumn(
                name: "PriceChanges",
                table: "ScrapingJobs");

            migrationBuilder.DropColumn(
                name: "ScrapingJobLogId",
                table: "ScrapingJobs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "ScrapingJobs");
        }
    }
}
