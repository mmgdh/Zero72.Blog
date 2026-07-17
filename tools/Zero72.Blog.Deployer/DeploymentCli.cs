using System.Globalization;
using Zero72.Blog.Deployer.Models;
using Zero72.Blog.Deployer.Services;

namespace Zero72.Blog.Deployer;

/// <summary>
/// 为自动化验证提供无界面发布入口；日常使用仍默认打开 WinForms 界面。
/// 整个流程复用 WinForms 的部署服务，因此具备相同的备份、健康检查和失败回滚能力。
/// </summary>
internal static class DeploymentCli
{
    /// <summary>
    /// 解析命名参数并执行一次完整发布。
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var settings = new DeploymentSettings
            {
                ProjectRoot = GetRequired(options, "project-root"),
                Host = GetOptional(options, "host", "47.114.74.197"),
                UserName = GetOptional(options, "user", "deploy"),
                Port = ParsePort(GetOptional(options, "port", "22")),
                PrivateKeyPath = GetRequired(options, "key"),
                RemoteRoot = GetOptional(options, "remote-root", "/opt/zero72-blog"),
                ComposeProjectName = GetOptional(options, "compose-project", "app"),
                ClientPort = ParsePort(GetOptional(options, "client-port", "8080")),
                AdminPort = ParsePort(GetOptional(options, "admin-port", "8081")),
                RunLocalChecks = false,
                NoCache = options.ContainsKey("no-cache")
            };

            var service = new DeploymentService(new ProcessRunner());
            var result = await service.DeployAsync(settings, new ConsoleProgress(), CancellationToken.None);
            Console.WriteLine($"发布成功：{result.ReleaseId}");
            Console.WriteLine($"博客：{result.BlogUrl}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"发布失败：{exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 将双短横线命名参数转换为不区分大小写的键值表。
    /// </summary>
    private static Dictionary<string, string?> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Equals("--deploy", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = argument[2..];
            var value = index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++index]
                : null;
            options[key] = value;
        }

        return options;
    }

    /// <summary>
    /// 获取必填参数并在缺失时给出明确提示。
    /// </summary>
    private static string GetRequired(IReadOnlyDictionary<string, string?> options, string key)
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"缺少必填参数 --{key}。 ");
        }

        return value;
    }

    /// <summary>
    /// 获取可选参数或使用默认值。
    /// </summary>
    private static string GetOptional(IReadOnlyDictionary<string, string?> options, string key, string defaultValue)
    {
        return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    /// <summary>
    /// 解析并校验端口参数。
    /// </summary>
    private static int ParsePort(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"端口 {value} 无效。 ");
        }

        return port;
    }

    /// <summary>
    /// 将部署服务进度直接写入当前终端。
    /// </summary>
    private sealed class ConsoleProgress : IProgress<string>
    {
        /// <summary>
        /// 输出单条部署进度。
        /// </summary>
        public void Report(string value)
        {
            Console.WriteLine(value);
        }
    }
}
