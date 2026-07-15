using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

[DbContext(typeof(BlogDbContext))]
[Migration("20260714000000_NormalizeReadingBooks")]
public partial class NormalizeReadingBooks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reading_books",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Author = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CoverTone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reading_books", book => book.Id);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO reading_books ("Id", "Title", "Author", "CoverTone")
            SELECT
                (
                    substring(md5("BookTitle" || '|' || "Author") from 1 for 8) || '-' ||
                    substring(md5("BookTitle" || '|' || "Author") from 9 for 4) || '-' ||
                    substring(md5("BookTitle" || '|' || "Author") from 13 for 4) || '-' ||
                    substring(md5("BookTitle" || '|' || "Author") from 17 for 4) || '-' ||
                    substring(md5("BookTitle" || '|' || "Author") from 21 for 12)
                )::uuid,
                "BookTitle",
                "Author",
                MIN("CoverTone")
            FROM reading_records
            GROUP BY "BookTitle", "Author";
            """);

        migrationBuilder.AddColumn<Guid>(
            name: "BookId",
            table: "reading_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE reading_records AS record
            SET "BookId" = book."Id"
            FROM reading_books AS book
            WHERE record."BookTitle" = book."Title"
              AND record."Author" = book."Author";
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "BookId",
            table: "reading_records",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.DropColumn(name: "BookTitle", table: "reading_records");
        migrationBuilder.DropColumn(name: "Author", table: "reading_records");
        migrationBuilder.DropColumn(name: "CoverTone", table: "reading_records");

        migrationBuilder.CreateIndex(
            name: "IX_reading_books_Title_Author",
            table: "reading_books",
            columns: ["Title", "Author"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_reading_records_BookId",
            table: "reading_records",
            column: "BookId");

        migrationBuilder.AddForeignKey(
            name: "FK_reading_records_reading_books_BookId",
            table: "reading_records",
            column: "BookId",
            principalTable: "reading_books",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BookTitle",
            table: "reading_records",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<string>(
            name: "Author",
            table: "reading_records",
            type: "character varying(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<string>(
            name: "CoverTone",
            table: "reading_records",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "midnight");

        migrationBuilder.Sql(
            """
            UPDATE reading_records AS record
            SET "BookTitle" = book."Title",
                "Author" = book."Author",
                "CoverTone" = book."CoverTone"
            FROM reading_books AS book
            WHERE record."BookId" = book."Id";
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_reading_records_reading_books_BookId",
            table: "reading_records");

        migrationBuilder.DropIndex(
            name: "IX_reading_records_BookId",
            table: "reading_records");

        migrationBuilder.DropColumn(
            name: "BookId",
            table: "reading_records");

        migrationBuilder.DropTable(name: "reading_books");
    }
}
