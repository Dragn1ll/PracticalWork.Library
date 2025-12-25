using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PracticalWork.Reports.Data.PostgreSql.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fix1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reports_Name",
                table: "Reports",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_Name",
                table: "Reports");
        }
    }
}
