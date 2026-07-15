using Zero72.Blog.Shared;

namespace Zero72.Blog.Api.Endpoints;

public static class AdminAssetEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif",
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public static IEndpointRouteBuilder MapAdminAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/assets").WithTags("Admin Assets");

        group.MapPost("/images", async (
            IFormFile file,
            IWebHostEnvironment environment,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest("Image file is empty.");
            }

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return Results.BadRequest("Image file must be 5 MB or smaller.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                return Results.BadRequest("Only gif, jpg, png, and webp images are supported.");
            }

            var uploadMonth = DateTime.UtcNow.ToString("yyyyMM");
            var uploadsRoot = Path.Combine(environment.WebRootPath, "uploads", uploadMonth);
            Directory.CreateDirectory(uploadsRoot);

            var safeFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var targetPath = Path.Combine(uploadsRoot, safeFileName);

            await using var stream = File.Create(targetPath);
            await file.CopyToAsync(stream, cancellationToken);

            var request = httpContext.Request;
            var publicPath = $"/uploads/{uploadMonth}/{safeFileName}";
            var publicUrl = $"{request.Scheme}://{request.Host}{publicPath}";

            return Results.Ok(new UploadImageResponse(publicUrl, file.FileName));
        })
        .DisableAntiforgery();

        return app;
    }
}
