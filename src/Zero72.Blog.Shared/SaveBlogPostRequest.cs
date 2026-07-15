namespace Zero72.Blog.Shared;

public sealed record SaveBlogPostRequest(
    string Title,
    string? Slug,
    string Summary,
    string ContentMarkdown,
    bool IsPublished);
