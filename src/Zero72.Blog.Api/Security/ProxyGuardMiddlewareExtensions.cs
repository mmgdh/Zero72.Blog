using System.Security.Cryptography;
using System.Text;

namespace Zero72.Blog.Api.Security;

public static class ProxyGuardMiddlewareExtensions
{
    private const string DefaultHeaderName = "X-Zero72-Proxy-Secret";

    public static IApplicationBuilder UseInternalProxyGuard(this WebApplication app)
    {
        var secret = app.Configuration["ProxyGuard:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return app;
        }

        var headerName = app.Configuration["ProxyGuard:HeaderName"] ?? DefaultHeaderName;
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        app.Use(async (context, next) =>
        {
            if (!RequiresProxyGuard(context.Request.Path))
            {
                await next();
                return;
            }

            var providedSecret = context.Request.Headers[headerName].FirstOrDefault();
            if (!SecureEquals(providedSecret, expectedHash))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });

        return app;
    }

    private static bool RequiresProxyGuard(PathString path)
    {
        return path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/uploads");
    }

    private static bool SecureEquals(string? candidate, byte[] expectedHash)
    {
        if (candidate is null)
        {
            return false;
        }

        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }
}
