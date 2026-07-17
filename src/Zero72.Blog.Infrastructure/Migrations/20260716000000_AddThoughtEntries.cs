using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

/// <summary>
/// 创建用于保存临时感悟和日常思考的数据库表。
/// </summary>
[DbContext(typeof(BlogDbContext))]
[Migration("20260716000000_AddThoughtEntries")]
public partial class AddThoughtEntries : Migration
{
    /// <summary>
    /// 创建思考记录表及查询索引。
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "thought_entries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_thought_entries", item => item.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_thought_entries_IsPublished",
            table: "thought_entries",
            column: "IsPublished");

        migrationBuilder.CreateIndex(
            name: "IX_thought_entries_OccurredAt",
            table: "thought_entries",
            column: "OccurredAt");
    }

    /// <summary>
    /// 删除思考记录表。
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "thought_entries");
    }
}
