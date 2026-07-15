using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zero72.Blog.Domain;
using Zero72.Blog.Infrastructure;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Api.Endpoints;

public static partial class AdminBlogPostEndpoints
{
    public static IEndpointRouteBuilder MapAdminBlogPostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/posts").WithTags("Admin Posts");

        group.MapGet("/", async (BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var posts = await dbContext.BlogPosts
                .AsNoTracking()
                .OrderByDescending(post => post.CreatedAt)
                .Select(post => new AdminBlogPostItem(
                    post.Id,
                    post.Title,
                    post.Slug,
                    post.Summary,
                    post.IsPublished,
                    post.CreatedAt,
                    post.PublishedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(posts);
        });

        group.MapGet("/{id:guid}", async (Guid id, BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var post = await dbContext.BlogPosts
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new AdminBlogPostEdit(
                    item.Id,
                    item.Title,
                    item.Slug,
                    item.Summary,
                    item.ContentMarkdown,
                    item.IsPublished,
                    item.CreatedAt,
                    item.PublishedAt))
                .FirstOrDefaultAsync(cancellationToken);

            return post is null ? Results.NotFound() : Results.Ok(post);
        });

        group.MapPost("/", async (SaveBlogPostRequest request, BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var validationResult = Validate(request);
            if (validationResult is not null)
            {
                return validationResult;
            }

            var now = DateTimeOffset.UtcNow;
            var slug = await CreateUniqueSlugAsync(request, dbContext, null, cancellationToken);
            var post = new BlogPost
            {
                Title = request.Title.Trim(),
                Slug = slug,
                Summary = request.Summary.Trim(),
                ContentMarkdown = request.ContentMarkdown,
                IsPublished = request.IsPublished,
                CreatedAt = now,
                PublishedAt = request.IsPublished ? now : null
            };

            dbContext.BlogPosts.Add(post);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/admin/posts/{post.Id}", ToEdit(post));
        });

        group.MapPut("/{id:guid}", async (Guid id, SaveBlogPostRequest request, BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var validationResult = Validate(request);
            if (validationResult is not null)
            {
                return validationResult;
            }

            var post = await dbContext.BlogPosts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (post is null)
            {
                return Results.NotFound();
            }

            post.Title = request.Title.Trim();
            post.Slug = await CreateUniqueSlugAsync(request, dbContext, id, cancellationToken);
            post.Summary = request.Summary.Trim();
            post.ContentMarkdown = request.ContentMarkdown;

            if (request.IsPublished && !post.IsPublished)
            {
                post.PublishedAt = DateTimeOffset.UtcNow;
            }
            else if (!request.IsPublished)
            {
                post.PublishedAt = null;
            }

            post.IsPublished = request.IsPublished;

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToEdit(post));
        });

        group.MapDelete("/{id:guid}", async (Guid id, BlogDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var post = await dbContext.BlogPosts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (post is null)
            {
                return Results.NotFound();
            }

            dbContext.BlogPosts.Remove(post);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        return app;
    }

    private static IResult? Validate(SaveBlogPostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            return Results.BadRequest("Summary is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentMarkdown))
        {
            return Results.BadRequest("Markdown content is required.");
        }

        return null;
    }

    private static async Task<string> CreateUniqueSlugAsync(
        SaveBlogPostRequest request,
        BlogDbContext dbContext,
        Guid? currentPostId,
        CancellationToken cancellationToken)
    {
        var baseSlug = ToSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);
        var slug = baseSlug;
        var suffix = 2;

        while (await dbContext.BlogPosts.AnyAsync(
            post => post.Slug == slug && (currentPostId == null || post.Id != currentPostId),
            cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    private static string ToSlug(string value)
    {
        var slug = SlugUnsafeCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"post-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}" : slug;
    }

    private static AdminBlogPostEdit ToEdit(BlogPost post)
    {
        return new AdminBlogPostEdit(
            post.Id,
            post.Title,
            post.Slug,
            post.Summary,
            post.ContentMarkdown,
            post.IsPublished,
            post.CreatedAt,
            post.PublishedAt);
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex SlugUnsafeCharacters();
}
