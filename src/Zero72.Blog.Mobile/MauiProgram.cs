using Microsoft.Extensions.Logging;
using Zero72.Blog.Mobile.Services;

namespace Zero72.Blog.Mobile;

/// <summary>
/// 配置 Android MAUI Blazor Hybrid 应用及其依赖服务。
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 创建应用并注册 WebView、设置、会话和博客 API 客户端。
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<MobileSettingsService>();
        builder.Services.AddSingleton<SessionState>();
        builder.Services.AddSingleton<BlogApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
