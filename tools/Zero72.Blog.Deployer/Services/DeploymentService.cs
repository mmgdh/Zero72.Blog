using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Zero72.Blog.Deployer.Models;

namespace Zero72.Blog.Deployer.Services;

/// <summary>
/// 编排博客发布全过程：校验配置、本地检查、生成安全源码包、上传到 ECS、
/// 在服务器构建新镜像、切换 Compose 服务并执行健康检查；远程脚本在失败时自动回滚。
/// </summary>
public sealed partial class DeploymentService(ProcessRunner processRunner)
{
    private const string SolutionFileName = "Zero72.Blog.slnx";
    private const string ProductionComposeFileName = "docker-compose.prod.yml";

    /// <summary>
    /// 测试 SSH 连接、免密 sudo Docker 权限和远程部署目录。
    /// </summary>
    public async Task TestConnectionAsync(
        DeploymentSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        ValidateSettings(settings, requireProjectFiles: false);
        progress.Report("正在测试 SSH 和 Docker 权限……");

        var command = $"test -d {settings.RemoteRoot} && sudo -n docker version >/dev/null && printf CONNECTION_OK";
        await processRunner.RunAsync(
            "ssh.exe",
            BuildSshArguments(settings, command),
            Environment.CurrentDirectory,
            progress,
            cancellationToken);

        progress.Report("连接测试成功。\r\n");
    }

    /// <summary>
    /// 执行一次完整部署，并返回发布编号及访问地址。
    /// </summary>
    public async Task<DeploymentResult> DeployAsync(
        DeploymentSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        ValidateSettings(settings, requireProjectFiles: true);
        var releaseId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "Zero72BlogDeploy", releaseId);
        var archivePath = Path.Combine(temporaryDirectory, $"zero72-blog-{releaseId}.tar.gz");
        var scriptPath = Path.Combine(temporaryDirectory, $"deploy-{releaseId}.sh");

        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            progress.Report($"开始发布 {releaseId}。\r\n");
            if (settings.RunLocalChecks)
            {
                await RunLocalChecksAsync(settings, progress, cancellationToken);
            }

            await CreateArchiveAsync(settings, archivePath, progress, cancellationToken);
            var remoteScript = RemoteDeploymentScript.Replace("\r\n", "\n", StringComparison.Ordinal);
            await File.WriteAllTextAsync(
                scriptPath,
                remoteScript,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            await UploadAsync(settings, archivePath, scriptPath, progress, cancellationToken);
            await ExecuteRemoteDeploymentAsync(
                settings,
                releaseId,
                archivePath,
                scriptPath,
                progress,
                cancellationToken);

            var blogUrl = new Uri($"http://{settings.Host}:{settings.ClientPort}/");
            var adminUrl = new Uri($"http://{settings.Host}:{settings.AdminPort}/");
            progress.Report($"\r\n发布成功：{blogUrl}");
            return new DeploymentResult(releaseId, blogUrl, adminUrl);
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
        }
    }

    /// <summary>
    /// 校验路径、端口和远程标识，防止无效值或 shell 元字符进入部署命令。
    /// </summary>
    private static void ValidateSettings(DeploymentSettings settings, bool requireProjectFiles)
    {
        if (requireProjectFiles &&
            (!Directory.Exists(settings.ProjectRoot) ||
             !File.Exists(Path.Combine(settings.ProjectRoot, SolutionFileName)) ||
             !File.Exists(Path.Combine(settings.ProjectRoot, ProductionComposeFileName))))
        {
            throw new InvalidOperationException("项目目录无效，未找到解决方案或生产 Compose 文件。");
        }

        if (string.IsNullOrWhiteSpace(settings.Host) || !HostPattern().IsMatch(settings.Host))
        {
            throw new InvalidOperationException("服务器地址格式无效。");
        }

        if (!UserOrProjectPattern().IsMatch(settings.UserName))
        {
            throw new InvalidOperationException("SSH 用户名只能包含字母、数字、点、下划线和短横线。");
        }

        if (!File.Exists(settings.PrivateKeyPath))
        {
            throw new InvalidOperationException("SSH 私钥文件不存在。");
        }

        if (!RemotePathPattern().IsMatch(settings.RemoteRoot) || settings.RemoteRoot.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("远程目录必须是仅包含安全字符的绝对路径。");
        }

        if (!UserOrProjectPattern().IsMatch(settings.ComposeProjectName))
        {
            throw new InvalidOperationException("Compose 项目名格式无效。");
        }

        ValidatePort(settings.Port, "SSH");
        ValidatePort(settings.ClientPort, "博客");
        ValidatePort(settings.AdminPort, "后台");
    }

    /// <summary>
    /// 校验单个 TCP 端口是否位于合法范围。
    /// </summary>
    private static void ValidatePort(int port, string name)
    {
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"{name}端口必须在 1 到 65535 之间。");
        }
    }

    /// <summary>
    /// 执行不会改变服务器的本地还原、格式和测试检查；生产编译由远程 Docker 强制执行。
    /// </summary>
    private async Task RunLocalChecksAsync(
        DeploymentSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("[1/5] 本地还原、格式和测试检查");
        await processRunner.RunAsync(
            "dotnet.exe",
            ["restore", SolutionFileName, "--nologo"],
            settings.ProjectRoot,
            progress,
            cancellationToken);
        await processRunner.RunAsync(
            "dotnet.exe",
            ["format", SolutionFileName, "--verify-no-changes", "--no-restore", "--verbosity", "minimal"],
            settings.ProjectRoot,
            progress,
            cancellationToken);
        await processRunner.RunAsync(
            "dotnet.exe",
            ["test", SolutionFileName, "-c", "Release", "--no-build", "--no-restore", "-m:1", "--nologo"],
            settings.ProjectRoot,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// 使用系统 tar 创建不包含密钥、环境文件、Git 和编译缓存的源码压缩包。
    /// </summary>
    private async Task CreateArchiveAsync(
        DeploymentSettings settings,
        string archivePath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("[2/5] 打包源码（自动排除密钥、.env、bin/obj 和 Git 数据）");
        var arguments = new List<string>
        {
            "-czf", archivePath,
            "--exclude=.git",
            "--exclude=.vs",
            "--exclude=.tmp",
            "--exclude=.nuget",
            "--exclude=.dotnet_cli_home",
            "--exclude=.secrets",
            "--exclude=.env",
            "--exclude=artifacts",
            "--exclude=bin",
            "--exclude=obj",
            "--exclude=*.user",
            "-C", settings.ProjectRoot,
            "."
        };

        await processRunner.RunAsync(
            "tar.exe",
            arguments,
            settings.ProjectRoot,
            progress,
            cancellationToken);

        var size = new FileInfo(archivePath).Length / 1024d / 1024d;
        progress.Report($"源码包大小：{size:F2} MiB");
    }

    /// <summary>
    /// 通过 SCP 上传源码包和固定内容的部署脚本。
    /// </summary>
    private async Task UploadAsync(
        DeploymentSettings settings,
        string archivePath,
        string scriptPath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("[3/5] 上传源码包和部署脚本");
        var target = $"{settings.UserName}@{settings.Host}:/tmp/";
        var arguments = BuildSshCommonArguments(settings, useUppercasePortOption: true);
        arguments.Add(archivePath);
        arguments.Add(scriptPath);
        arguments.Add(target);

        await processRunner.RunAsync(
            "scp.exe",
            arguments,
            settings.ProjectRoot,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// 在远程主机执行经过参数白名单校验的部署脚本。
    /// </summary>
    private async Task ExecuteRemoteDeploymentAsync(
        DeploymentSettings settings,
        string releaseId,
        string archivePath,
        string scriptPath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("[4/5] 远程构建镜像并切换容器");
        var remoteArchive = $"/tmp/{Path.GetFileName(archivePath)}";
        var remoteScript = $"/tmp/{Path.GetFileName(scriptPath)}";
        var noCache = settings.NoCache ? "1" : "0";
        var command = string.Join(
            ' ',
            "bash",
            remoteScript,
            remoteArchive,
            releaseId,
            settings.RemoteRoot,
            settings.ComposeProjectName,
            settings.ClientPort.ToString(CultureInfo.InvariantCulture),
            settings.AdminPort.ToString(CultureInfo.InvariantCulture),
            noCache);

        await processRunner.RunAsync(
            "ssh.exe",
            BuildSshArguments(settings, command),
            settings.ProjectRoot,
            progress,
            cancellationToken);
        progress.Report("[5/5] 健康检查通过，服务器已完成清理");
    }

    /// <summary>
    /// 生成 SSH 命令参数，包括首次连接自动记录主机密钥和连接超时。
    /// </summary>
    private static List<string> BuildSshArguments(DeploymentSettings settings, string remoteCommand)
    {
        var arguments = BuildSshCommonArguments(settings, useUppercasePortOption: false);
        arguments.Add($"{settings.UserName}@{settings.Host}");
        arguments.Add(remoteCommand);
        return arguments;
    }

    /// <summary>
    /// 生成 SSH 与 SCP 共用的安全连接参数。
    /// </summary>
    private static List<string> BuildSshCommonArguments(
        DeploymentSettings settings,
        bool useUppercasePortOption)
    {
        return
        [
            useUppercasePortOption ? "-P" : "-p",
            settings.Port.ToString(CultureInfo.InvariantCulture),
            "-i",
            settings.PrivateKeyPath,
            "-o",
            "BatchMode=yes",
            "-o",
            "StrictHostKeyChecking=accept-new",
            "-o",
            "ConnectTimeout=15"
        ];
    }

    /// <summary>
    /// 尝试删除本次部署的本地临时目录，清理失败不覆盖部署结果。
    /// </summary>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时文件被安全软件短暂占用时保留，系统稍后可自行清理。
        }
        catch (UnauthorizedAccessException)
        {
            // 清理失败不影响已经完成的服务器部署。
        }
    }

    /// <summary>
    /// 匹配主机名、IPv4 或 IPv6 文本中允许的字符。
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9.:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex HostPattern();

    /// <summary>
    /// 匹配 SSH 用户名和 Compose 项目名允许的字符。
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UserOrProjectPattern();

    /// <summary>
    /// 匹配不含空格和 shell 元字符的远程绝对路径。
    /// </summary>
    [GeneratedRegex(@"^/[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RemotePathPattern();

    private const string RemoteDeploymentScript = """
        #!/usr/bin/env bash
        set -Eeuo pipefail

        archive="$1"
        release_id="$2"
        remote_root="$3"
        project_name="$4"
        client_port="$5"
        admin_port="$6"
        no_cache="$7"

        current="$remote_root/app"
        releases="$remote_root/releases"
        backups="$remote_root/backups"
        staging="$releases/release-$release_id"
        previous="$releases/app-before-$release_id"
        failed="$releases/failed-$release_id"
        backup="$backups/app-before-$release_id.tar.gz"
        compose_file="docker-compose.prod.yml"
        current_moved=0
        new_activated=0

        compose() {
          sudo -n docker compose -p "$project_name" --env-file .env -f "$compose_file" "$@"
        }

        restore_previous_images() {
          for service in api client admin; do
            rollback_image="$project_name-$service:rollback-$release_id"
            if sudo -n docker image inspect "$rollback_image" >/dev/null 2>&1; then
              sudo -n docker tag "$rollback_image" "$project_name-$service:latest"
            fi
          done
        }

        rollback() {
          exit_code=$?
          if [ "$exit_code" = "0" ]; then
            exit_code=1
          fi
          set +e
          echo "部署失败，开始自动回滚……"
          if [ "$new_activated" = "1" ] && [ -d "$current" ]; then
            mv "$current" "$failed"
          fi
          if [ "$current_moved" = "1" ] && [ -d "$previous" ]; then
            mv "$previous" "$current"
          fi
          restore_previous_images
          if [ -d "$current" ]; then
            cd "$current"
            compose up -d --remove-orphans
          fi
          echo "自动回滚已执行，请检查上方失败原因。"
          exit "$exit_code"
        }

        trap rollback ERR HUP INT TERM
        umask 077

        test -f "$archive"
        test -d "$current"
        test -f "$current/.env"
        mkdir -p "$releases" "$backups"
        test ! -e "$staging"
        test ! -e "$previous"
        mkdir "$staging"
        tar -xzf "$archive" -C "$staging"
        cp "$current/.env" "$staging/.env"

        if [ -d "$current/deploy/runtime" ]; then
          mkdir -p "$staging/deploy/runtime"
          cp -a "$current/deploy/runtime/." "$staging/deploy/runtime/"
        fi

        for service in api client admin; do
          image="$project_name-$service:latest"
          if sudo -n docker image inspect "$image" >/dev/null 2>&1; then
            sudo -n docker tag "$image" "$project_name-$service:rollback-$release_id"
          fi
        done

        cd "$staging"
        compose config --quiet
        if [ "$no_cache" = "1" ]; then
          compose build --no-cache api client admin
        else
          compose build api client admin
        fi

        sudo -n docker run --rm --entrypoint /bin/sh "$project_name-client:latest" \
          -c "test -f /app/wwwroot/css/app.css && test -f /app/wwwroot/_framework/blazor.web.js"

        tar -czf "$backup" -C "$current" .
        chmod 600 "$backup"
        mv "$current" "$previous"
        current_moved=1
        mv "$staging" "$current"
        new_activated=1

        cd "$current"
        compose up -d --remove-orphans

        wait_for_url() {
          url="$1"
          label="$2"
          for attempt in $(seq 1 30); do
            if curl -fsSL --max-time 10 "$url" >/dev/null; then
              echo "$label 健康检查通过。"
              return 0
            fi
            sleep 2
          done
          echo "$label 健康检查超时。" >&2
          return 1
        }

        wait_for_url "http://127.0.0.1:$client_port/" "公开博客"
        wait_for_url "http://127.0.0.1:$client_port/api/health" "API"
        wait_for_url "http://127.0.0.1:$client_port/_framework/blazor.web.js" "交互脚本"
        wait_for_url "http://127.0.0.1:$admin_port/" "管理后台"

        test "$(sudo -n docker inspect --format='{{.RestartCount}}' "$project_name-client-1")" = "0"
        compose ps
        rm -f "$archive" "$0"
        trap - ERR HUP INT TERM
        echo "DEPLOYMENT_SUCCESS:$release_id"
        """;
}
