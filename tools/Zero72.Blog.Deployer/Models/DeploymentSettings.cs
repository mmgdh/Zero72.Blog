using System.Text.Json.Serialization;

namespace Zero72.Blog.Deployer.Models;

/// <summary>
/// 保存发布目标和本地项目位置等配置；私钥仅保存路径，登录密码不会序列化。
/// </summary>
public sealed class DeploymentSettings
{
    /// <summary>
    /// 获取或设置博客仓库根目录。
    /// </summary>
    public string ProjectRoot { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 SSH 服务器地址。
    /// </summary>
    public string Host { get; set; } = "47.114.74.197";

    /// <summary>
    /// 获取或设置 SSH 用户名。
    /// </summary>
    public string UserName { get; set; } = "deploy";

    /// <summary>
    /// 获取或设置 SSH 端口。
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// 获取或设置 SSH 身份认证方式。
    /// </summary>
    public SshAuthenticationMode AuthenticationMode { get; set; } = SshAuthenticationMode.Password;

    /// <summary>
    /// 获取或设置当前运行期间使用的 SSH 登录密码；该值不会写入配置文件。
    /// </summary>
    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 SSH 私钥文件路径。
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置服务器上的部署根目录。
    /// </summary>
    public string RemoteRoot { get; set; } = "/opt/zero72-blog";

    /// <summary>
    /// 获取或设置 Docker Compose 项目名称。
    /// </summary>
    public string ComposeProjectName { get; set; } = "app";

    /// <summary>
    /// 获取或设置公开博客端口。
    /// </summary>
    public int ClientPort { get; set; } = 8080;

    /// <summary>
    /// 获取或设置管理后台端口。
    /// </summary>
    public int AdminPort { get; set; } = 8081;

    /// <summary>
    /// 获取或设置是否在打包前执行本地快速检查。
    /// </summary>
    public bool RunLocalChecks { get; set; }

    /// <summary>
    /// 获取或设置远程 Docker 构建是否禁用缓存。
    /// </summary>
    public bool NoCache { get; set; }
}
