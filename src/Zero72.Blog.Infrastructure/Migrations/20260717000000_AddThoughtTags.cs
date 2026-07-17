using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zero72.Blog.Infrastructure.Migrations;

/// <summary>
/// 为感悟记录增加可自定义的标签数组，已有记录使用空标签集合。
/// </summary>
[DbContext(typeof(BlogDbContext))]
[Migration("20260717000000_AddThoughtTags")]
public partial class AddThoughtTags : Migration
{
    /// <summary>
    /// 添加不能为空的 PostgreSQL 文本数组列并兼容已有数据。
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string[]>(
            name: "Tags",
            table: "thought_entries",
            type: "text[]",
            nullable: false,
            defaultValue: Array.Empty<string>());
    }

    /// <summary>
    /// 移除感悟标签列。
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Tags",
            table: "thought_entries");
    }
}
