using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zero72.Blog.Domain;
using Zero72.Blog.Reading;

namespace Zero72.Blog.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

                await dbContext.Database.MigrateAsync(cancellationToken);

                if (!await dbContext.BlogPosts.AnyAsync(cancellationToken))
                {
                    var now = DateTimeOffset.UtcNow;
                    dbContext.BlogPosts.Add(new BlogPost
                    {
                        Title = "Welcome to Zero72 Blog",
                        Slug = "welcome-to-zero72-blog",
                        Summary = "Your blog backend, PostgreSQL database, and Blazor client are connected.",
                        ContentMarkdown = "# Welcome\n\nThis is the first seeded post.",
                        IsPublished = true,
                        CreatedAt = now,
                        PublishedAt = now
                    });

                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (!await dbContext.ReadingBooks.AnyAsync(cancellationToken) &&
                    !await dbContext.ReadingRecords.AnyAsync(cancellationToken))
                {
                    dbContext.ReadingBooks.AddRange(ReadingSeedData.CreateBooks());
                    dbContext.ReadingRecords.AddRange(ReadingSeedData.CreateRecords());
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database initialization failed on attempt {Attempt}/{MaxAttempts}. Retrying...",
                    attempt,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }
}
