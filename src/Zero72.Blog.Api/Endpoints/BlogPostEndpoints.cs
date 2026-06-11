using Microsoft.EntityFrameworkCore;
using Zero72.Blog.Infrastructure;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Api.Endpoints;

public static class BlogPostEndpoints
{
    public static IEndpointRouteBuilder MapBlogPostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/posts").WithTags("Posts");

        group.MapGet("/", async (BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var posts = await dbContext.BlogPosts
                .AsNoTracking()
                .Where(post => post.IsPublished)
                .OrderByDescending(post => post.PublishedAt)
                .Select(post => new BlogPostSummary(
                    post.Id,
                    post.Title,
                    post.Slug,
                    post.Summary,
                    post.PublishedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(posts);
        });

        group.MapGet("/{slug}", async (string slug, BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var post = await dbContext.BlogPosts
                .AsNoTracking()
                .Where(item => item.IsPublished && item.Slug == slug)
                .Select(item => new BlogPostDetail(
                    item.Id,
                    item.Title,
                    item.Slug,
                    item.Summary,
                    item.ContentMarkdown,
                    item.PublishedAt))
                .FirstOrDefaultAsync(cancellationToken);

            return post is null ? Results.NotFound() : Results.Ok(post);
        });

        return app;
    }
}
