using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaigonChe.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistPosCashReceived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountReceived",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountReceived",
                table: "Orders");
        }
    }
}
