using System.Globalization;
using Zero72.Blog.Mobile.Models;

namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 从当前博客服务器读取安卓升级清单，比较版本号，并通过系统浏览器安全打开安装包下载地址。
/// </summary>
public sealed class AppUpdateService(BlogApiClient api, MobileSettingsService settings)
{
    /// <summary>
    /// 返回当前安装版本的展示文本。
    /// </summary>
    public string CurrentVersion => $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    /// <summary>
    /// 查询服务器版本；仅当服务器内部版本号大于当前安装版本时返回升级信息。
    /// </summary>
    public async Task<MobileReleaseInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var release = await api.GetLatestReleaseAsync(cancellationToken);
        if (release is null ||
            release.VersionCode <= GetCurrentVersionCode() ||
            string.IsNullOrWhiteSpace(release.VersionName) ||
            string.IsNullOrWhiteSpace(release.DownloadUrl))
        {
            return null;
        }

        return release;
    }

    /// <summary>
    /// 校验清单中的下载路径属于当前博客的 mobile 目录，再交给 Android 系统浏览器下载。
    /// </summary>
    public async Task OpenDownloadAsync(MobileReleaseInfo release)
    {
        var downloadPath = release.DownloadUrl.Trim();
        if (!downloadPath.StartsWith("/mobile/", StringComparison.OrdinalIgnoreCase) ||
            !downloadPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("服务器返回了不受信任的安卓安装包地址。");
        }

        var downloadUri = new Uri(settings.GetBaseUri(), downloadPath.TrimStart('/'));
        if (!await Launcher.Default.OpenAsync(downloadUri))
        {
            throw new InvalidOperationException("无法打开系统浏览器，请稍后重试或在设置中检查服务器地址。");
        }
    }

    /// <summary>
    /// 将 Android 的整数构建号转换为可比较值；异常值按零处理。
    /// </summary>
    private static int GetCurrentVersionCode()
    {
        return int.TryParse(AppInfo.Current.BuildString, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;
    }
}
