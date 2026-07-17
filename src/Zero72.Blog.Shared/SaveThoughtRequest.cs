namespace Zero72.Blog.Shared;

/// <summary>
/// 表示新增或更新思考记录时提交的数据。
/// </summary>
public sealed record SaveThoughtRequest(
    string Content,
    DateTimeOffset OccurredAt,
    string? ImageUrl,
    bool IsPublished);
