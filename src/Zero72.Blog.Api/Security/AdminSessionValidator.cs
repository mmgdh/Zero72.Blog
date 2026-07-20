using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Zero72.Blog.Api.Security;

/// <summary>
/// 为管理员登录票据生成与当前账号密码绑定的凭据指纹，并在每次 Cookie 认证时重新校验。
/// 服务端密码修改后，旧票据中的指纹不再匹配，从而立即注销已有移动端和网页会话。
/// </summary>
internal static class AdminSessionValidator
{
    public const string CredentialStampClaimType = "zero72:credential-stamp";
    private const int DefaultSessionHours = 24 * 7;
    private const int MaximumSessionHours = 24 * 30;

    /// <summary>
    /// 获取配置的会话有效期，默认7天并限制在1小时至30天之间。
    /// </summary>
    public static TimeSpan GetSessionLifetime(IConfiguration configuration)
    {
        var hours = configuration.GetValue("AdminAuth:SessionHours", DefaultSessionHours);
        return TimeSpan.FromHours(Math.Clamp(hours, 1, MaximumSessionHours));
    }

    /// <summary>
    /// 根据当前管理员账号密码生成只用于票据失效判断的 SHA-256 指纹。
    /// </summary>
    public static string? CreateCredentialStamp(IConfiguration configuration)
    {
        var userName = configuration["AdminAuth:UserName"];
        var password = configuration["AdminAuth:Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var source = Encoding.UTF8.GetBytes($"zero72-admin-session-v1\n{userName}\n{password}");
        return Convert.ToHexString(SHA256.HashData(source));
    }

    /// <summary>
    /// 校验 Cookie 中的用户名和凭据指纹；配置变化或旧票据缺少指纹时拒绝会话。
    /// </summary>
    public static async Task ValidateAsync(
        CookieValidatePrincipalContext context,
        IConfiguration configuration)
    {
        var configuredUserName = configuration["AdminAuth:UserName"];
        var expectedStamp = CreateCredentialStamp(configuration);
        var ticketUserName = context.Principal?.FindFirstValue(ClaimTypes.Name);
        var ticketStamp = context.Principal?.FindFirstValue(CredentialStampClaimType);
        if (!string.IsNullOrWhiteSpace(configuredUserName) &&
            string.Equals(ticketUserName, configuredUserName, StringComparison.Ordinal) &&
            FixedTimeEquals(ticketStamp, expectedStamp))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// 以固定时间比较十六进制指纹，避免泄露有效指纹的匹配位置。
    /// </summary>
    private static bool FixedTimeEquals(string? candidate, string? expected)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(candidate),
                Convert.FromHexString(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
