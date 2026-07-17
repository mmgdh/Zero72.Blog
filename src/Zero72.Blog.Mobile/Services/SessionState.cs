namespace Zero72.Blog.Mobile.Services;

/// <summary>
/// 保存当前 App 进程内的管理员登录状态并通知布局刷新。
/// </summary>
public sealed class SessionState
{
    /// <summary>
    /// 当登录状态发生变化时触发。
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 获取当前是否已经通过管理员认证。
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// 获取当前登录用户名。
    /// </summary>
    public string? UserName { get; private set; }

    /// <summary>
    /// 更新会话状态并通知订阅者。
    /// </summary>
    public void Update(bool isAuthenticated, string? userName)
    {
        IsAuthenticated = isAuthenticated;
        UserName = isAuthenticated ? userName : null;
        Changed?.Invoke();
    }
}
