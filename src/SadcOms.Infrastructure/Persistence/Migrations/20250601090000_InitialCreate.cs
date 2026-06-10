using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadcOms.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial schema: Customers, Orders and OrderLineItems. Optimistic-concurrency and the
/// outbox/idempotency tables are introduced in later migrations to demonstrate evolution.
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                CountryCode = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Customers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.ForeignKey(
                    name: "FK_Orders_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OrderLineItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductSku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderLineItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrderLineItems_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Email",
            table: "Customers",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Name",
            table: "Customers",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_CustomerId_Status_CreatedAt",
            table: "Orders",
            columns: ["CustomerId", "Status", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_Status_CreatedAt",
            table: "Orders",
            columns: ["Status", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_OrderLineItems_OrderId",
            table: "OrderLineItems",
            column: "OrderId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrderLineItems");
        migrationBuilder.DropTable(name: "Orders");
        migrationBuilder.DropTable(name: "Customers");
    }
}
