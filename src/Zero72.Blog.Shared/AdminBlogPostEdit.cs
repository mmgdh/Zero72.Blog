namespace Zero72.Blog.Shared;

public sealed record AdminBlogPostEdit(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string ContentMarkdown,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
