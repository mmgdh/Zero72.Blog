using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

[DbContext(typeof(BlogDbContext))]
[Migration("20260611000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "blog_posts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_blog_posts", post => post.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_blog_posts_Slug",
            table: "blog_posts",
            column: "Slug",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "blog_posts");
    }
}
