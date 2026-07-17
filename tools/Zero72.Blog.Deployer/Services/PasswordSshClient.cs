using Renci.SshNet;
using System.Security.Cryptography;
using System.Text;
using Zero72.Blog.Deployer.Models;

namespace Zero72.Blog.Deployer.Services;

/// <summary>
/// 使用 SSH.NET 完成密码认证、SSH 标准输入流上传和远程命令执行。
/// 密码只存在于当前配置快照的内存中，不会进入命令行、日志或本地配置文件。
/// </summary>
public sealed class PasswordSshClient(KnownHostStore knownHostStore)
{
    private const int UploadChunkSize = 256 * 1024;
    private const int UploadRetryCount = 3;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(45);

    /// <summary>
    /// 使用默认主机指纹存储创建密码 SSH 客户端。
    /// </summary>
    public PasswordSshClient()
        : this(new KnownHostStore())
    {
    }

    /// <summary>
    /// 连接服务器并执行单条命令，完整转发标准输出和错误输出。
    /// </summary>
    public async Task ExecuteCommandAsync(
        DeploymentSettings settings,
        string commandText,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        using var client = CreateSshClient(settings, progress);
        await client.ConnectAsync(cancellationToken);
        using var command = client.CreateCommand(commandText);
        command.CommandTimeout = CommandTimeout;
        var executionTask = command.ExecuteAsync(cancellationToken);
        var outputTask = PumpOutputAsync(command.OutputStream, progress, cancellationToken);
        var errorTask = PumpOutputAsync(command.ExtendedOutputStream, progress, cancellationToken);
        await Task.WhenAll(executionTask, outputTask, errorTask);

        if (command.ExitStatus != 0)
        {
            throw new InvalidOperationException($"远程命令执行失败，退出码：{command.ExitStatus}。");
        }
    }

    /// <summary>
    /// 通过普通 SSH 命令的标准输入流写入文件，完全绕过服务器 SFTP 和 SCP 子系统，
    /// 并在移动到最终路径前使用 SHA-256 校验传输完整性。
    /// </summary>
    public async Task UploadFilesAsync(
        DeploymentSettings settings,
        IReadOnlyList<string> localPaths,
        string remoteDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        foreach (var localPath in localPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remotePath = $"{remoteDirectory.TrimEnd('/')}/{Path.GetFileName(localPath)}";
            await UploadFileInChunksAsync(
                settings,
                localPath,
                remotePath,
                progress,
                cancellationToken);
            progress.Report($"已上传并校验：{Path.GetFileName(localPath)}");
        }
    }

    /// <summary>
    /// 创建并配置使用密码认证的 SSH 命令客户端。
    /// </summary>
    private SshClient CreateSshClient(DeploymentSettings settings, IProgress<string> progress)
    {
        var client = new SshClient(CreateConnectionInfo(settings));
        ConfigureHostKeyValidation(client, settings, progress);
        return client;
    }

    /// <summary>
    /// 使用独立短连接按固定偏移分块写入远程临时文件，最后验证 SHA-256 并原子改名。
    /// </summary>
    private async Task UploadFileInChunksAsync(
        DeploymentSettings settings,
        string localPath,
        string remotePath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{remotePath}.uploading";
        await using var input = File.OpenRead(localPath);
        var hashBytes = await SHA256.HashDataAsync(input, cancellationToken);
        var expectedHash = Convert.ToHexStringLower(hashBytes);
        input.Position = 0;

        await ExecuteUploadCommandWithRetryAsync(
            settings,
            $"umask 077; : > {QuoteShellArgument(temporaryPath)}",
            ReadOnlyMemory<byte>.Empty,
            progress,
            cancellationToken);

        var buffer = new byte[UploadChunkSize];
        var totalChunks = Math.Max(1, (input.Length + UploadChunkSize - 1) / UploadChunkSize);
        for (var chunkIndex = 0L; ; chunkIndex++)
        {
            var bytesRead = await ReadChunkAsync(input, buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            var writeCommand = string.Join(
                ' ',
                $"dd of={QuoteShellArgument(temporaryPath)}",
                $"bs={UploadChunkSize}",
                $"seek={chunkIndex}",
                "conv=notrunc status=none");
            await ExecuteUploadCommandWithRetryAsync(
                settings,
                writeCommand,
                buffer.AsMemory(0, bytesRead),
                progress,
                cancellationToken);

            var percent = Math.Min(100, (int)((chunkIndex + 1) * 100 / totalChunks));
            progress.Report($"上传 {Path.GetFileName(localPath)}：{percent}%");
        }

        var verifyCommand = string.Join(
            ' ',
            "printf '%s  %s\\n'",
            QuoteShellArgument(expectedHash),
            QuoteShellArgument(temporaryPath),
            "| sha256sum -c -",
            "&& mv -f",
            QuoteShellArgument(temporaryPath),
            QuoteShellArgument(remotePath));
        await ExecuteUploadCommandWithRetryAsync(
            settings,
            verifyCommand,
            ReadOnlyMemory<byte>.Empty,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// 使用新 SSH 短连接执行一个上传步骤，连接中断时安全重试当前幂等分块。
    /// </summary>
    private async Task ExecuteUploadCommandWithRetryAsync(
        DeploymentSettings settings,
        string commandText,
        ReadOnlyMemory<byte> payload,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= UploadRetryCount; attempt++)
        {
            try
            {
                using var client = CreateSshClient(settings, progress);
                await client.ConnectAsync(cancellationToken);
                using var command = client.CreateCommand(commandText);
                command.CommandTimeout = CommandTimeout;
                var executionTask = command.ExecuteAsync(cancellationToken);
                if (!payload.IsEmpty)
                {
                    await using var remoteInput = command.CreateInputStream();
                    await remoteInput.WriteAsync(payload, cancellationToken);
                }

                await executionTask;
                if (command.ExitStatus != 0)
                {
                    throw new InvalidOperationException(
                        $"远程上传步骤失败，退出码：{command.ExitStatus}；{command.Error.Trim()}");
                }

                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (attempt < UploadRetryCount)
            {
                lastException = exception;
                progress.Report($"上传连接中断，正在重试当前分块（{attempt}/{UploadRetryCount}）……");
            }
            catch (Exception exception)
            {
                lastException = exception;
            }
        }

        throw new InvalidOperationException(
            $"分块上传连续失败 {UploadRetryCount} 次：{lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// 尽可能填满分块缓冲区，直到文件结束或缓冲区已满。
    /// </summary>
    private static async Task<int> ReadChunkAsync(
        Stream input,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await input.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    /// <summary>
    /// 将路径转换为 POSIX Shell 单引号参数，防止文件名被解释为命令。
    /// </summary>
    private static string QuoteShellArgument(string value)
    {
        return $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    /// <summary>
    /// 创建不记录密码的 SSH.NET 连接配置。
    /// </summary>
    private static PasswordConnectionInfo CreateConnectionInfo(DeploymentSettings settings)
    {
        var connectionInfo = new PasswordConnectionInfo(
            settings.Host,
            settings.Port,
            settings.UserName,
            settings.Password)
        {
            Timeout = ConnectionTimeout
        };
        ApplyCompatibleAlgorithms(connectionInfo);
        return connectionInfo;
    }

    /// <summary>
    /// 固定使用已在当前 ECS 链路验证通过的密钥交换、主机密钥和加密算法，
    /// 避免 Windows OpenSSH 默认 Curve25519 协商在该链路上超时。
    /// </summary>
    private static void ApplyCompatibleAlgorithms(ConnectionInfo connectionInfo)
    {
        foreach (var algorithm in connectionInfo.KeyExchangeAlgorithms.Keys.ToArray())
        {
            if (!algorithm.Equals("diffie-hellman-group14-sha256", StringComparison.Ordinal))
            {
                connectionInfo.KeyExchangeAlgorithms.Remove(algorithm);
            }
        }

        foreach (var algorithm in connectionInfo.HostKeyAlgorithms.Keys.ToArray())
        {
            if (!algorithm.Equals("ssh-ed25519", StringComparison.Ordinal))
            {
                connectionInfo.HostKeyAlgorithms.Remove(algorithm);
            }
        }

        foreach (var algorithm in connectionInfo.Encryptions.Keys.ToArray())
        {
            if (!algorithm.Equals("chacha20-poly1305@openssh.com", StringComparison.Ordinal))
            {
                connectionInfo.Encryptions.Remove(algorithm);
            }
        }
    }

    /// <summary>
    /// 为客户端启用主机密钥首次信任和后续严格校验。
    /// </summary>
    private void ConfigureHostKeyValidation(
        BaseClient client,
        DeploymentSettings settings,
        IProgress<string> progress)
    {
        client.HostKeyReceived += (_, eventArgs) =>
        {
            var result = knownHostStore.VerifyOrRemember(
                settings.Host,
                settings.Port,
                eventArgs.HostKeyName,
                eventArgs.FingerPrintSHA256);
            eventArgs.CanTrust = result.Trusted;
            if (result.Remembered)
            {
                progress.Report($"已首次信任并保存主机指纹：SHA256:{eventArgs.FingerPrintSHA256}");
            }
            else if (!result.Trusted)
            {
                progress.Report("服务器主机指纹与本地记录不一致，已拒绝连接。");
            }
        };
    }

    /// <summary>
    /// 实时按行转发远程输出并跳过空白内容。
    /// </summary>
    private static async Task PumpOutputAsync(
        Stream stream,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                progress.Report(line);
            }
        }
    }
}
