using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadcOms.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the transactional-outbox table (reliable event publishing) and the idempotency-record
/// table (safe retries of status updates). Both are additive and carry no data migration.
/// </summary>
public partial class AddOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Attempts = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IdempotencyRecords",
            columns: table => new
            {
                Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RequestTarget = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StatusCode = table.Column<int>(type: "int", nullable: false),
                ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_IdempotencyRecords", x => x.Key));

        // Filtered index: the dispatcher only ever queries unprocessed rows.
        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Unprocessed",
            table: "OutboxMessages",
            columns: ["ProcessedAt", "OccurredAt"],
            filter: "[ProcessedAt] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessages");
        migrationBuilder.DropTable(name: "IdempotencyRecords");
    }
}
