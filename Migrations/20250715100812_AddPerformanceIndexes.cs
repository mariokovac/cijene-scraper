using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CijeneScraper.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Stores_Code_ChainId",
                table: "Stores",
                columns: new[] { "Code", "ChainId" });

            migrationBuilder.CreateIndex(
                name: "IX_Prices_ChainProductId",
                table: "Prices",
                column: "ChainProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Prices_Date_StoreId",
                table: "Prices",
                columns: new[] { "Date", "StoreId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stores_Code_ChainId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Prices_ChainProductId",
                table: "Prices");

            migrationBuilder.DropIndex(
                name: "IX_Prices_Date_StoreId",
                table: "Prices");
        }
    }
}
