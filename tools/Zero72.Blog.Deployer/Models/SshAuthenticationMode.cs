namespace Zero72.Blog.Deployer.Models;

/// <summary>
/// 定义发布器连接 ECS 时使用的 SSH 身份认证方式。
/// </summary>
public enum SshAuthenticationMode
{
    /// <summary>
    /// 使用当前窗口中输入的登录密码，密码不会持久化。
    /// </summary>
    Password,

    /// <summary>
    /// 使用指定路径的 SSH 私钥，并继续调用系统 OpenSSH。
    /// </summary>
    PrivateKey
}
