namespace Zero72.Blog.Mobile;

/// <summary>
/// Android 应用对象，负责创建承载 BlazorWebView 的主窗口。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 初始化 MAUI 应用资源。
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 创建博客助手主窗口。
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Zero72 博客助手" };
    }
}
