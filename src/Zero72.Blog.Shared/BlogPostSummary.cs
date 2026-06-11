namespace Zero72.Blog.Shared;

public sealed record BlogPostSummary(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    DateTimeOffset? PublishedAt);
