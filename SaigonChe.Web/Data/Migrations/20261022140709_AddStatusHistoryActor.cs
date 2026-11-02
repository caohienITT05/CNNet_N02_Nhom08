using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaigonChe.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusHistoryActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangedBy",
                table: "OrderStatusHistories",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangedBy",
                table: "OrderStatusHistories");
        }
    }
}
