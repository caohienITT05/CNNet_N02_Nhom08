using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaigonChe.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPosPartyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedStaffId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerUserId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "TableId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VoucherId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "OrderItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiningTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    QuantityChange = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EventAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headcount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QuoteAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Consultation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyRequests_AspNetUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PartyRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MinimumOrderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UseLimit = table.Column<int>(type: "int", nullable: true),
                    UseCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartyRequestStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyRequestId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyRequestStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyRequestStatusHistory_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PartyRequestStatusHistory_PartyRequests_PartyRequestId",
                        column: x => x.PartyRequestId,
                        principalTable: "PartyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AssignedStaffId",
                table: "Orders",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerUserId",
                table: "Orders",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId",
                table: "Orders",
                column: "TableId",
                unique: true,
                filter: "[TableId] IS NOT NULL AND [Type] = 1 AND [IsPaid] = 0 AND [Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_Code",
                table: "DiningTables",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ActorId",
                table: "InventoryMovements",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OrderId_ProductId",
                table: "InventoryMovements",
                columns: new[] { "OrderId", "ProductId" },
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId",
                table: "InventoryMovements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_ActorId",
                table: "OrderStatusHistory",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_CreatedAt",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartyRequests_CustomerUserId",
                table: "PartyRequests",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyRequests_OrderId",
                table: "PartyRequests",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PartyRequestStatusHistory_ActorId",
                table: "PartyRequestStatusHistory",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyRequestStatusHistory_PartyRequestId_CreatedAt",
                table: "PartyRequestStatusHistory",
                columns: new[] { "PartyRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_AssignedStaffId",
                table: "Orders",
                column: "AssignedStaffId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_CustomerUserId",
                table: "Orders",
                column: "CustomerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DiningTables_TableId",
                table: "Orders",
                column: "TableId",
                principalTable: "DiningTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_AssignedStaffId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_CustomerUserId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DiningTables_TableId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "DiningTables");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "PartyRequestStatusHistory");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "PartyRequests");

            migrationBuilder.DropIndex(
                name: "IX_Orders_AssignedStaffId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerUserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TableId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Stock",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AssignedStaffId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "OrderItems");
        }
    }
}
