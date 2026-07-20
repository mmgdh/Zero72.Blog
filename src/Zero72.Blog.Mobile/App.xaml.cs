using Zero72.Blog.Mobile.Services;

namespace Zero72.Blog.Mobile;

/// <summary>
/// Android 应用对象，负责创建承载 BlazorWebView 的主窗口。
/// </summary>
public partial class App : Application
{
    private readonly AppLogService logs;

    /// <summary>
    /// 初始化 MAUI 应用资源并注册进程级异常日志。
    /// </summary>
    public App(AppLogService logs)
    {
        this.logs = logs;
        InitializeComponent();
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        _ = logs.InfoAsync(
            "应用",
            $"启动 Zero72 博客助手 {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})；{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}；{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}");
    }

    /// <summary>
    /// 创建博客助手主窗口。
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Zero72 博客助手" };
    }

    /// <summary>
    /// 记录未被业务代码捕获的进程异常。
    /// </summary>
    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        var detail = eventArgs.ExceptionObject is Exception exception
            ? exception.ToString()
            : eventArgs.ExceptionObject?.ToString() ?? "未知异常";
        _ = logs.ErrorAsync("应用异常", detail);
    }

    /// <summary>
    /// 记录未观察到的后台任务异常并将其标记为已处理。
    /// </summary>
    private void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        _ = logs.ErrorAsync("后台任务异常", eventArgs.Exception.ToString());
        eventArgs.SetObserved();
    }
}
