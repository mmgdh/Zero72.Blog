using System.Text.Json;
using Zero72.Blog.Deployer.Models;

namespace Zero72.Blog.Deployer.Services;

/// <summary>
/// 调用仓库内的 Android 构建脚本，自动递增版本并生成供服务器远程升级使用的 APK 与版本清单。
/// </summary>
public sealed class AndroidBuildService(ProcessRunner processRunner)
{
    /// <summary>
    /// 生成 Release APK、更新清单和稳定名称副本，并返回可供用户直接安装的 APK 路径。
    /// </summary>
    public async Task<string> BuildAsync(
        DeploymentSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(settings.ProjectRoot, "tools", "Build-Android.ps1");
        if (!File.Exists(scriptPath))
        {
            throw new InvalidOperationException("项目中未找到 tools/Build-Android.ps1。");
        }

        progress.Report("开始生成 Android 远程升级包，成功后自动递增版本……");
        await processRunner.RunAsync(
            "powershell.exe",
            [
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath,
                "-Configuration",
                "Release",
                "-IncrementVersion",
                "-ReleaseNotes",
                "新增远程升级支持，并优化功能与使用体验。"
            ],
            settings.ProjectRoot,
            progress,
            cancellationToken);

        var manifestPath = Path.Combine(
            settings.ProjectRoot,
            "src",
            "Zero72.Blog.ClientHost",
            "wwwroot",
            "mobile",
            "latest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("APK 已构建，但没有生成远程升级清单。");
        }

        await using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = await JsonDocument.ParseAsync(
            manifestStream,
            cancellationToken: cancellationToken);
        var versionName = manifest.RootElement.GetProperty("versionName").GetString();
        if (string.IsNullOrWhiteSpace(versionName))
        {
            throw new InvalidOperationException("远程升级清单中缺少版本号。");
        }

        var apkPath = Path.Combine(
            settings.ProjectRoot,
            "artifacts",
            "mobile",
            $"Zero72.Blog.Mobile-{versionName}.apk");
        if (!File.Exists(apkPath))
        {
            throw new InvalidOperationException($"没有找到生成的 APK：{apkPath}");
        }

        progress.Report($"Android {versionName} 已生成。请点击“仅上传安卓”，无需重新构建 Docker。");
        return apkPath;
    }
}
