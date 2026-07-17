namespace Zero72.Blog.Deployer.Services;

/// <summary>
/// 使用首次信任（TOFU）方式保存 ECS 主机密钥指纹，后续连接发现指纹变化时拒绝认证，
/// 防止管理员密码被发送给伪造的 SSH 主机。
/// </summary>
public sealed class KnownHostStore
{
    private static readonly object SyncRoot = new();
    private readonly string storePath;

    /// <summary>
    /// 初始化当前 Windows 用户专用的主机指纹存储路径。
    /// </summary>
    public KnownHostStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zero72.Blog.Deployer");
        storePath = Path.Combine(directory, "known-hosts.txt");
    }

    /// <summary>
    /// 校验已保存的主机指纹；首次连接时记录指纹并返回可信结果。
    /// </summary>
    public HostTrustResult VerifyOrRemember(
        string host,
        int port,
        string algorithm,
        string fingerprint)
    {
        var endpoint = $"{host}:{port}";
        lock (SyncRoot)
        {
            var entries = ReadEntries();
            var existing = entries.FirstOrDefault(entry =>
                entry.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var trusted = existing.Algorithm.Equals(algorithm, StringComparison.Ordinal) &&
                    existing.Fingerprint.Equals(fingerprint, StringComparison.Ordinal);
                return new HostTrustResult(trusted, Remembered: false);
            }

            var directory = Path.GetDirectoryName(storePath)!;
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                storePath,
                $"{endpoint}\t{algorithm}\t{fingerprint}{Environment.NewLine}");
            return new HostTrustResult(Trusted: true, Remembered: true);
        }
    }

    /// <summary>
    /// 读取格式正确的主机指纹条目，忽略空行和损坏行。
    /// </summary>
    private List<KnownHostEntry> ReadEntries()
    {
        if (!File.Exists(storePath))
        {
            return [];
        }

        var entries = new List<KnownHostEntry>();
        foreach (var line in File.ReadLines(storePath))
        {
            var parts = line.Split('\t');
            if (parts.Length == 3 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
            {
                entries.Add(new KnownHostEntry(parts[0], parts[1], parts[2]));
            }
        }

        return entries;
    }

    /// <summary>
    /// 表示一条已经保存的 SSH 主机指纹。
    /// </summary>
    private sealed record KnownHostEntry(string Endpoint, string Algorithm, string Fingerprint);
}

/// <summary>
/// 表示 SSH 主机密钥校验结果及是否为首次记录。
/// </summary>
public readonly record struct HostTrustResult(bool Trusted, bool Remembered);
