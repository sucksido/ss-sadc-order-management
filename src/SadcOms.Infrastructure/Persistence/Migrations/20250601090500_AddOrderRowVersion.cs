using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadcOms.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds a SQL Server <c>rowversion</c> column to Orders for optimistic concurrency control.
/// The column is auto-maintained by SQL Server, so existing rows are populated automatically
/// and this is an online, additive change.
/// </summary>
public partial class AddOrderRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Orders",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: new byte[0]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RowVersion", table: "Orders");
    }
}
