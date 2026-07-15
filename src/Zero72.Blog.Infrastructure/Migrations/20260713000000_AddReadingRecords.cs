using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

[DbContext(typeof(BlogDbContext))]
[Migration("20260713000000_AddReadingRecords")]
public partial class AddReadingRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reading_records",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReadDate = table.Column<DateOnly>(type: "date", nullable: false),
                BookTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Author = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Chapter = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                StartedAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                FinishedAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                DurationHours = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                Reflections = table.Column<string[]>(type: "text[]", nullable: false),
                CoverTone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reading_records", record => record.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_reading_records_ReadDate",
            table: "reading_records",
            column: "ReadDate");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "reading_records");
    }
}
