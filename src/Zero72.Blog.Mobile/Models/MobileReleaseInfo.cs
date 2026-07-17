namespace Zero72.Blog.Mobile.Models;

/// <summary>
/// 描述服务器公开的安卓安装包版本、下载地址及更新说明。
/// </summary>
public sealed record MobileReleaseInfo(
    int VersionCode,
    string VersionName,
    string DownloadUrl,
    string ReleaseNotes,
    DateTimeOffset PublishedAtUtc,
    bool Required);
