using Microsoft.AspNetCore.Components;
using Zero72.Blog.Mobile.Services;

namespace Zero72.Blog.Mobile.Components;

/// <summary>
/// 为需要管理员身份的页面提供统一会话检查和登录跳转。
/// </summary>
public abstract class AuthenticatedPageBase : ComponentBase
{
    /// <summary>
    /// 获取注入的博客 API 客户端。
    /// </summary>
    [Inject]
    protected BlogApiClient Api { get; set; } = default!;

    /// <summary>
    /// 获取注入的会话状态。
    /// </summary>
    [Inject]
    protected SessionState Session { get; set; } = default!;

    /// <summary>
    /// 获取注入的页面导航服务。
    /// </summary>
    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// 向服务器确认登录状态，未登录时跳转到登录页。
    /// </summary>
    protected async Task<bool> EnsureAuthenticatedAsync()
    {
        try
        {
            var status = await Api.GetStatusAsync();
            if (status.IsAuthenticated)
            {
                return true;
            }
        }
        catch (HttpRequestException)
        {
            // 页面继续显示自身的网络错误区域，登录页负责重新连接。
        }

        Navigation.NavigateTo("/login");
        return false;
    }
}
