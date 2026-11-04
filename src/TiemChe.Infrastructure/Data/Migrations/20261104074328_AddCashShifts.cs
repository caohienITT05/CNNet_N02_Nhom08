using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiemChe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OpeningCashVnd = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CountedCashVnd = table.Column<long>(type: "INTEGER", nullable: true),
                    ExpectedCashVnd = table.Column<long>(type: "INTEGER", nullable: true),
                    DifferenceVnd = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashShifts", x => x.Id);
                    table.CheckConstraint("CK_CashShifts_Amounts", "\"OpeningCashVnd\" >= 0 AND (\"CountedCashVnd\" IS NULL OR \"CountedCashVnd\" >= 0)");
                    table.ForeignKey(
                        name: "FK_CashShifts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_UserId",
                table: "CashShifts",
                column: "UserId",
                unique: true,
                filter: "\"ClosedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_UserId_OpenedAtUtc",
                table: "CashShifts",
                columns: new[] { "UserId", "OpenedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashShifts");
        }
    }
}
