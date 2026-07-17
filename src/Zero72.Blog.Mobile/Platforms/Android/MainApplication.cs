using Android.App;
using Android.Runtime;

namespace Zero72.Blog.Mobile;

/// <summary>
/// Android Application 入口，负责创建共享 MAUI 应用实例。
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
    /// <summary>
    /// 使用 Android 运行时句柄初始化应用。
    /// </summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <summary>
    /// 创建配置完成的 MAUI 应用。
    /// </summary>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
