namespace Zero72.Blog.Shared;

public sealed record AdminBlogPostItem(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
