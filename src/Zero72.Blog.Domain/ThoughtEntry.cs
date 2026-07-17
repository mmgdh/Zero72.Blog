namespace Zero72.Blog.Domain;

/// <summary>
/// 表示存储在数据库中的临时感悟或日常思考。
/// </summary>
public sealed class ThoughtEntry
{
    /// <summary>
    /// 获取或设置记录标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 获取或设置思考正文。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置想法实际发生的时间。
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// 获取或设置可选的配图地址。
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 获取或设置是否允许在公开博客展示。
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// 获取或设置创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 获取或设置最后更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
