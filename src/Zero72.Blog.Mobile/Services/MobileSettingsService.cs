using Microsoft.Maui.Storage;

namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 持久化服务器地址和用户名等非敏感移动端设置，不保存管理员密码。
/// </summary>
public sealed class MobileSettingsService
{
    private const string BaseUrlKey = "blog_base_url";
    private const string UserNameKey = "admin_user_name";
    private const string DefaultBaseUrl = "http://47.114.74.197:8080/";

    /// <summary>
    /// 获取当前博客服务地址，并确保路径以斜杠结束。
    /// </summary>
    public Uri GetBaseUri()
    {
        var saved = Preferences.Default.Get(BaseUrlKey, DefaultBaseUrl);
        return NormalizeBaseUri(saved);
    }

    /// <summary>
    /// 校验并保存博客服务地址。
    /// </summary>
    public Uri SaveBaseUrl(string value)
    {
        var uri = NormalizeBaseUri(value);
        Preferences.Default.Set(BaseUrlKey, uri.AbsoluteUri);
        return uri;
    }

    /// <summary>
    /// 获取上次成功登录时使用的用户名。
    /// </summary>
    public string GetUserName()
    {
        return Preferences.Default.Get(UserNameKey, string.Empty);
    }

    /// <summary>
    /// 保存用户名以减少重复输入，密码始终不持久化。
    /// </summary>
    public void SaveUserName(string userName)
    {
        Preferences.Default.Set(UserNameKey, userName.Trim());
    }

    /// <summary>
    /// 将用户输入规范化为仅允许 HTTP 或 HTTPS 的绝对根地址。
    /// </summary>
    private static Uri NormalizeBaseUri(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("服务器地址必须是有效的 HTTP 或 HTTPS 地址。");
        }

        var builder = new UriBuilder(uri)
        {
            Path = uri.AbsolutePath.TrimEnd('/') + "/",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }
}
