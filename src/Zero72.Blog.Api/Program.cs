using Zero72.Blog.Api.Endpoints;
using Zero72.Blog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => IsAllowedOrigin(origin, allowedOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Environment.WebRootPath ??= Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads"));

app.UseCors("ClientApp");
app.UseHttpsRedirection();
app.UseStaticFiles();

await app.Services.InitializeDatabaseAsync(app.Logger);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapBlogPostEndpoints();
app.MapAdminBlogPostEndpoints();
app.MapAdminAssetEndpoints();
app.MapReadingBookEndpoints();
app.MapReadingRecordEndpoints();
app.MapAdminReadingBookEndpoints();
app.MapAdminReadingRecordEndpoints();

app.Run();

static bool IsAllowedOrigin(string origin, IReadOnlyCollection<string> allowedOrigins)
{
    if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        return true;
    }

    return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));
}
