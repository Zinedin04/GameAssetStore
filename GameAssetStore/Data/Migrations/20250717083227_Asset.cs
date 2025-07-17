using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAssetStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class Asset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Store_AssetId",
                table: "Store",
                column: "AssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Store_Asset_AssetId",
                table: "Store",
                column: "AssetId",
                principalTable: "Asset",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Store_Asset_AssetId",
                table: "Store");

            migrationBuilder.DropIndex(
                name: "IX_Store_AssetId",
                table: "Store");
        }
    }
}
