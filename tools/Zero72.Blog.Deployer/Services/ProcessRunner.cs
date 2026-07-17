using System.Diagnostics;
using System.Text;

namespace Zero72.Blog.Deployer.Services;

/// <summary>
/// 统一启动外部命令、实时转发日志并在取消时终止整个子进程树。
/// </summary>
public sealed class ProcessRunner
{
    /// <summary>
    /// 运行指定命令，并在退出码非零时抛出包含命令名称的异常。
    /// </summary>
    public async Task RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(fileName, arguments, workingDirectory),
            EnableRaisingEvents = true
        };

        progress.Report($"> {Path.GetFileName(fileName)}");
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"无法启动命令：{fileName}");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"找不到或无法启动 {fileName}，请确认它已安装并加入 PATH。",
                exception);
        }

        using var registration = cancellationToken.Register(() => TryKill(process));
        var standardOutputTask = PumpAsync(process.StandardOutput, progress, cancellationToken);
        var standardErrorTask = PumpAsync(process.StandardError, progress, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutputTask, standardErrorTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"命令 {Path.GetFileName(fileName)} 执行失败，退出码：{process.ExitCode}。");
        }
    }

    /// <summary>
    /// 创建使用参数列表的进程配置，避免路径或用户输入被解释为额外命令。
    /// </summary>
    private static ProcessStartInfo CreateStartInfo(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// 按行读取单个输出流并发送到界面日志区。
    /// </summary>
    private static async Task PumpAsync(
        StreamReader reader,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    return;
                }

                progress.Report(line);
            }
        }
        catch (OperationCanceledException)
        {
            // 主命令取消时输出流会同步关闭，无需把它当作额外错误。
        }
    }

    /// <summary>
    /// 尝试结束被取消的命令及其子进程，不覆盖原始取消异常。
    /// </summary>
    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // 进程已退出时无需继续处理。
        }
    }
}
