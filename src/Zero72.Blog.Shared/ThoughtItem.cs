namespace Zero72.Blog.Shared;

/// <summary>
/// 表示公开页面和管理客户端共用的一条思考记录。
/// </summary>
public sealed record ThoughtItem(
    Guid Id,
    string Content,
    DateTimeOffset OccurredAt,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset UpdatedAt,
    string[] Tags);
