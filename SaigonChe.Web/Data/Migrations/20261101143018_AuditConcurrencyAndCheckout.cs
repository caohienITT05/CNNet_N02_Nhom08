using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaigonChe.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditConcurrencyAndCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PartyRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutToken",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CheckoutToken",
                table: "Orders",
                column: "CheckoutToken",
                unique: true,
                filter: "[CheckoutToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CheckoutToken",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PartyRequests");

            migrationBuilder.DropColumn(
                name: "CheckoutToken",
                table: "Orders");
        }
    }
}
