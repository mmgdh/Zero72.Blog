using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Zero72.Blog.Api.Endpoints;
using Zero72.Blog.Api.Security;
using Zero72.Blog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "__Zero72Admin";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    var publicPermitLimit = Math.Max(1, builder.Configuration.GetValue("RateLimiting:PublicPermitLimit", 120));
    var adminAuthPermitLimit = Math.Max(1, builder.Configuration.GetValue("RateLimiting:AdminAuthPermitLimit", 8));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = publicPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy(AdminAuthEndpoints.RateLimitPolicyName, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"admin-auth:{GetClientKey(context)}",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = adminAuthPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(5)
            }));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => IsAllowedOrigin(origin, allowedOrigins))
            .AllowCredentials()
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

app.UseInternalProxyGuard();
app.UseRateLimiter();
app.UseCors("ClientApp");
app.UseAuthentication();
app.UseAuthorization();
app.UseApiExceptionDetails();
app.UseHttpsRedirection();
app.UseStaticFiles();

await app.Services.InitializeDatabaseAsync(app.Logger);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapBlogPostEndpoints();
app.MapAdminAuthEndpoints();
app.MapAdminBlogPostEndpoints();
app.MapAdminAssetEndpoints();
app.MapReadingBookEndpoints();
app.MapReadingRecordEndpoints();
app.MapAdminReadingBookEndpoints();
app.MapAdminReadingRecordEndpoints();
app.MapThoughtEndpoints();

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

static string GetClientKey(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? forwardedFor;
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
