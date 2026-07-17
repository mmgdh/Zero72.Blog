using System.Text.Json;
using Zero72.Blog.Deployer.Models;

namespace Zero72.Blog.Deployer.Services;

/// <summary>
/// 在当前 Windows 用户的本地应用数据目录中读取和保存发布工具的非敏感配置。
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsPath;

    /// <summary>
    /// 初始化配置存储路径。
    /// </summary>
    public SettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zero72.Blog.Deployer");
        settingsPath = Path.Combine(directory, "settings.json");
    }

    /// <summary>
    /// 加载已有配置；首次使用时根据仓库位置生成安全的默认值。
    /// </summary>
    public async Task<DeploymentSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(settingsPath))
        {
            await using var stream = File.OpenRead(settingsPath);
            var saved = await JsonSerializer.DeserializeAsync<DeploymentSettings>(
                stream,
                JsonOptions,
                cancellationToken);
            if (saved is not null)
            {
                return saved;
            }
        }

        var projectRoot = FindProjectRoot();
        return new DeploymentSettings
        {
            ProjectRoot = projectRoot ?? string.Empty,
            PrivateKeyPath = SuggestPrivateKeyPath(projectRoot)
        };
    }

    /// <summary>
    /// 保存非敏感配置，便于下次直接发布。
    /// </summary>
    public async Task SaveAsync(
        DeploymentSettings settings,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }

    /// <summary>
    /// 从程序目录和当前目录向上查找解决方案文件，以自动定位仓库根目录。
    /// </summary>
    private static string? FindProjectRoot()
    {
        var startDirectories = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };

        foreach (var startDirectory in startDirectories)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Zero72.Blog.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据仓库父目录推导默认私钥位置，但不会创建或复制私钥。
    /// </summary>
    private static string SuggestPrivateKeyPath(string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return string.Empty;
        }

        var parent = Directory.GetParent(projectRoot);
        return parent is null
            ? string.Empty
            : Path.Combine(parent.FullName, ".secrets", "zero72blog_aliyun_ed25519");
    }
}
