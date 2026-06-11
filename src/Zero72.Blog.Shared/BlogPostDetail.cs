namespace Zero72.Blog.Shared;

public sealed record BlogPostDetail(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string ContentMarkdown,
    DateTimeOffset? PublishedAt);
