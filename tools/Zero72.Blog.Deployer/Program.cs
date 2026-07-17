namespace Zero72.Blog.Deployer;

/// <summary>
/// 发布工具的应用程序入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 根据启动参数执行无界面发布，或初始化 WinForms 并打开主窗口。
    /// </summary>
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--deploy", StringComparer.OrdinalIgnoreCase))
        {
            return await DeploymentCli.RunAsync(args);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
