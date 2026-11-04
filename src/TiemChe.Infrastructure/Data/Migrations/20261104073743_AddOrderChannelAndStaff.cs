using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiemChe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderChannelAndStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByStaffId",
                table: "Orders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Orders"
                SET "Channel" = 2, "CreatedByStaffId" = "UserId"
                WHERE "UserId" IN (
                    SELECT "Users"."Id"
                    FROM "Users"
                    INNER JOIN "Roles" ON "Users"."RoleId" = "Roles"."Id"
                    WHERE "Roles"."Name" IN ('Staff', 'Admin')
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedByStaffId_CheckoutRequestId",
                table: "Orders",
                columns: new[] { "CreatedByStaffId", "CheckoutRequestId" },
                unique: true,
                filter: "\"Channel\" = 2");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_CreatedByStaffId",
                table: "Orders",
                column: "CreatedByStaffId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_CreatedByStaffId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CreatedByStaffId_CheckoutRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedByStaffId",
                table: "Orders");
        }
    }
}
