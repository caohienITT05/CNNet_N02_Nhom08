using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiemChe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAtUtc",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundedByUserId",
                table: "Payments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RefundedByUserId",
                table: "Payments",
                column: "RefundedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_RefundedByUserId",
                table: "Payments",
                column: "RefundedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_RefundedByUserId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RefundedByUserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedByUserId",
                table: "Payments");
        }
    }
}
