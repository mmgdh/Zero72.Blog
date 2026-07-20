using System.Text;

namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 将应用、API 与图片诊断信息写入应用私有目录中的滚动日志文件。
/// 日志写入失败不得影响正常业务，文件超过上限后仅保留最近内容。
/// </summary>
public sealed class AppLogService
{
    private const long MaxFileBytes = 512 * 1024;
    private const int MaxDisplayCharacters = 120_000;
    private readonly SemaphoreSlim fileLock = new(1, 1);
    private readonly string logPath = Path.Combine(FileSystem.AppDataDirectory, "zero72-diagnostics.log");

    /// <summary>
    /// 记录普通运行信息。
    /// </summary>
    public Task InfoAsync(string category, string message)
    {
        return WriteAsync("信息", category, message);
    }

    /// <summary>
    /// 记录不影响当前操作但值得关注的信息。
    /// </summary>
    public Task WarningAsync(string category, string message)
    {
        return WriteAsync("警告", category, message);
    }

    /// <summary>
    /// 记录请求或应用异常。
    /// </summary>
    public Task ErrorAsync(string category, string message)
    {
        return WriteAsync("错误", category, message);
    }

    /// <summary>
    /// 读取最近日志；内容过长时只返回末尾，避免设置页面占用过多内存。
    /// </summary>
    public async Task<string> ReadAsync()
    {
        await fileLock.WaitAsync();
        try
        {
            if (!File.Exists(logPath))
            {
                return "暂无诊断日志。";
            }

            var content = await File.ReadAllTextAsync(logPath, Encoding.UTF8);
            if (content.Length <= MaxDisplayCharacters)
            {
                return content;
            }

            var start = content.Length - MaxDisplayCharacters;
            var nextLine = content.IndexOf('\n', start);
            var recent = nextLine >= 0 ? content[(nextLine + 1)..] : content[start..];
            return $"（日志较长，仅显示最近内容）{Environment.NewLine}{recent}";
        }
        catch (Exception exception)
        {
            return $"读取日志失败：{exception.Message}";
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// 清空当前诊断日志。
    /// </summary>
    public async Task ClearAsync()
    {
        await fileLock.WaitAsync();
        try
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// 串行追加单条日志，并在必要时压缩历史内容。
    /// </summary>
    private async Task WriteAsync(string level, string category, string message)
    {
        var safeCategory = Normalize(category);
        var safeMessage = Normalize(message);
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] [{safeCategory}] {safeMessage}{Environment.NewLine}";

        await fileLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.AppendAllTextAsync(logPath, line, Encoding.UTF8);
            await TrimIfNeededAsync();
        }
        catch
        {
            // 诊断能力不能影响应用本身的操作流程。
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// 日志超过 512 KB 时保留最近约四分之三内容。
    /// </summary>
    private async Task TrimIfNeededAsync()
    {
        var file = new FileInfo(logPath);
        if (!file.Exists || file.Length <= MaxFileBytes)
        {
            return;
        }

        var content = await File.ReadAllTextAsync(logPath, Encoding.UTF8);
        var start = Math.Max(0, content.Length / 4);
        var nextLine = content.IndexOf('\n', start);
        var recent = nextLine >= 0 ? content[(nextLine + 1)..] : content[start..];
        await File.WriteAllTextAsync(logPath, recent, new UTF8Encoding(false));
    }

    /// <summary>
    /// 将换行统一为可阅读形式，并限制异常单条日志无限增长。
    /// </summary>
    private static string Normalize(string value)
    {
        const int maxMessageCharacters = 8_000;
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= maxMessageCharacters
            ? normalized
            : $"{normalized[..maxMessageCharacters]}…（已截断）";
    }
}
