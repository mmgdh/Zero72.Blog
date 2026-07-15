using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

[DbContext(typeof(BlogDbContext))]
[Migration("20260715000000_AddReadingBookCoverImage")]
public partial class AddReadingBookCoverImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CoverImageUrl",
            table: "reading_books",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CoverImageUrl",
            table: "reading_books");
    }
}
