using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Zero72.Blog.Api.Security;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Api.Endpoints;

public static class AdminAuthEndpoints
{
    public const string RateLimitPolicyName = "admin-auth";

    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/auth").WithTags("Admin Auth");

        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var isAuthenticated = user.Identity?.IsAuthenticated == true;
            return Results.Ok(new AdminAuthStatus(isAuthenticated, isAuthenticated ? user.Identity?.Name : null));
        });

        group.MapPost("/login", async (
            AdminLoginRequest request,
            IConfiguration configuration,
            HttpContext httpContext) =>
        {
            var configuredUserName = configuration["AdminAuth:UserName"];
            var configuredPassword = configuration["AdminAuth:Password"];
            if (string.IsNullOrWhiteSpace(configuredUserName) ||
                string.IsNullOrWhiteSpace(configuredPassword))
            {
                return Results.Problem("Admin authentication is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!SecureEquals(request.UserName, configuredUserName) ||
                !SecureEquals(request.Password, configuredPassword))
            {
                return Results.Unauthorized();
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, configuredUserName),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(
                    AdminSessionValidator.CredentialStampClaimType,
                    AdminSessionValidator.CreateCredentialStamp(configuration)!)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(
                        AdminSessionValidator.GetSessionLifetime(configuration)),
                    IsPersistent = true
                });

            return Results.Ok(new AdminAuthStatus(true, configuredUserName));
        })
        .RequireRateLimiting(RateLimitPolicyName);

        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .RequireAuthorization();

        return app;
    }

    private static bool SecureEquals(string? candidate, string expected)
    {
        if (candidate is null)
        {
            return false;
        }

        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }
}
