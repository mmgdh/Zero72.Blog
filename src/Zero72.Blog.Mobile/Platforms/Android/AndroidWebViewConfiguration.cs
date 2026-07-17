using Android.Webkit;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Zero72.Blog.Mobile;

/// <summary>
/// 配置 Android Blazor WebView 的网络图片策略，使 HTTPS 本地壳页面能够显示用户配置博客地址上的 HTTP 图片。
/// 博客服务仍由应用设置限制为 HTTP 或 HTTPS，Android 清单同时只开放应用自身的明文网络访问。
/// </summary>
internal static class AndroidWebViewConfiguration
{
    private static bool isConfigured;

    /// <summary>
    /// 注册一次 WebView 映射，在原生控件创建后允许混合内容图片并启用网络图片下载。
    /// </summary>
    public static void Configure()
    {
        if (isConfigured)
        {
            return;
        }

        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping(
            nameof(AndroidWebViewConfiguration),
            static (handler, _) =>
            {
                handler.PlatformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
                handler.PlatformView.Settings.BlockNetworkImage = false;
            });
        isConfigured = true;
    }
}
