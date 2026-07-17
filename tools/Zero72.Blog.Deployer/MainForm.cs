using System.Diagnostics;
using Zero72.Blog.Deployer.Models;
using Zero72.Blog.Deployer.Services;

namespace Zero72.Blog.Deployer;

/// <summary>
/// 博客发布工具主窗口：负责采集连接配置、展示实时日志，并协调连接测试、发布、取消和结果跳转。
/// </summary>
public sealed class MainForm : Form
{
    private readonly SettingsStore settingsStore = new();
    private readonly DeploymentService deploymentService = new(new ProcessRunner());
    private readonly TextBox projectRootTextBox = new();
    private readonly TextBox hostTextBox = new();
    private readonly TextBox userNameTextBox = new();
    private readonly NumericUpDown sshPortInput = CreatePortInput(22);
    private readonly TextBox privateKeyTextBox = new();
    private readonly TextBox remoteRootTextBox = new();
    private readonly TextBox composeProjectTextBox = new();
    private readonly NumericUpDown clientPortInput = CreatePortInput(8080);
    private readonly NumericUpDown adminPortInput = CreatePortInput(8081);
    private readonly CheckBox localChecksCheckBox = new();
    private readonly CheckBox noCacheCheckBox = new();
    private readonly Button testConnectionButton = new();
    private readonly Button deployButton = new();
    private readonly Button cancelButton = new();
    private readonly Button openBlogButton = new();
    private readonly Button openAdminButton = new();
    private readonly RichTextBox logTextBox = new();
    private readonly ProgressBar progressBar = new();
    private CancellationTokenSource? operationCancellation;
    private DeploymentResult? lastDeployment;

    /// <summary>
    /// 初始化窗口尺寸、控件布局、视觉样式和交互事件。
    /// </summary>
    public MainForm()
    {
        InitializeWindow();
        Controls.Add(CreateRootLayout());
    }

    /// <summary>
    /// 窗口首次显示时加载上次保存的配置。
    /// </summary>
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var settings = await settingsStore.LoadAsync();
            ApplySettings(settings);
            AppendLog("配置已加载。发布前请确认项目目录和私钥路径。\r\n");
        }
        catch (Exception exception)
        {
            ShowError("无法加载配置", exception);
        }
    }

    /// <summary>
    /// 窗口关闭时取消仍在运行的发布命令，避免遗留本地子进程。
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        operationCancellation?.Cancel();
        base.OnFormClosing(e);
    }

    /// <summary>
    /// 设置窗口基础样式和高 DPI 行为下的合理最小尺寸。
    /// </summary>
    private void InitializeWindow()
    {
        Text = "Zero72 博客 Docker 发布工具";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 680);
        Size = new Size(1040, 780);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(246, 248, 251);
    }

    /// <summary>
    /// 创建由标题、配置区、按钮区、进度条和日志区组成的根布局。
    /// </summary>
    private Control CreateRootLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateHeader(), 0, 0);
        layout.Controls.Add(CreateSettingsPanel(), 0, 1);
        layout.Controls.Add(CreateActionPanel(), 0, 2);

        progressBar.Dock = DockStyle.Top;
        progressBar.Height = 5;
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.MarqueeAnimationSpeed = 0;
        progressBar.Margin = new Padding(0, 10, 0, 10);
        layout.Controls.Add(progressBar, 0, 3);

        ConfigureLogTextBox();
        layout.Controls.Add(logTextBox, 0, 4);
        return layout;
    }

    /// <summary>
    /// 创建窗口标题和流程说明。
    /// </summary>
    private static Control CreateHeader()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 14)
        };
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Zero72 博客一键发布",
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 76, 150)
        });
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "本地检查 → 安全打包 → SSH 上传 → Docker 构建 → 健康检查；失败自动回滚。",
            ForeColor = Color.FromArgb(84, 96, 112),
            Margin = new Padding(1, 5, 0, 0)
        });
        return panel;
    }

    /// <summary>
    /// 创建服务器与项目配置表格。
    /// </summary>
    private Control CreateSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            BackColor = Color.White,
            Padding = new Padding(14),
            ColumnCount = 4,
            RowCount = 6,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        AddPathRow(panel, 0, "项目目录", projectRootTextBox, OnBrowseProjectRoot);
        AddField(panel, 1, 0, "服务器", hostTextBox);
        AddField(panel, 1, 2, "SSH 用户", userNameTextBox);
        AddField(panel, 2, 0, "SSH 端口", sshPortInput);
        AddPathField(panel, 2, 2, "私钥路径", privateKeyTextBox, OnBrowsePrivateKey);
        AddField(panel, 3, 0, "远程目录", remoteRootTextBox);
        AddField(panel, 3, 2, "Compose 名", composeProjectTextBox);
        AddField(panel, 4, 0, "博客端口", clientPortInput);
        AddField(panel, 4, 2, "后台端口", adminPortInput);

        localChecksCheckBox.Text = "发布前执行本地格式与测试检查";
        localChecksCheckBox.AutoSize = true;
        localChecksCheckBox.Margin = new Padding(3, 10, 20, 4);
        noCacheCheckBox.Text = "远程 Docker 禁用缓存（更慢）";
        noCacheCheckBox.AutoSize = true;
        noCacheCheckBox.Margin = new Padding(3, 10, 3, 4);
        panel.Controls.Add(localChecksCheckBox, 1, 5);
        panel.Controls.Add(noCacheCheckBox, 3, 5);
        return panel;
    }

    /// <summary>
    /// 创建连接测试、发布、取消及打开站点按钮。
    /// </summary>
    private Control CreateActionPanel()
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0)
        };

        ConfigureButton(testConnectionButton, "测试连接", Color.White, Color.FromArgb(24, 76, 150));
        testConnectionButton.FlatAppearance.BorderColor = Color.FromArgb(24, 76, 150);
        testConnectionButton.Click += OnTestConnection;

        ConfigureButton(deployButton, "开始一键发布", Color.FromArgb(24, 76, 150), Color.White);
        deployButton.Click += OnDeploy;

        ConfigureButton(cancelButton, "取消", Color.FromArgb(230, 82, 82), Color.White);
        cancelButton.Enabled = false;
        cancelButton.Click += OnCancel;

        ConfigureButton(openBlogButton, "打开博客", Color.FromArgb(236, 242, 251), Color.FromArgb(24, 76, 150));
        openBlogButton.Enabled = false;
        openBlogButton.Click += OnOpenBlog;

        ConfigureButton(openAdminButton, "打开后台", Color.FromArgb(236, 242, 251), Color.FromArgb(24, 76, 150));
        openAdminButton.Enabled = false;
        openAdminButton.Click += OnOpenAdmin;

        panel.Controls.AddRange(
        [
            testConnectionButton,
            deployButton,
            cancelButton,
            openBlogButton,
            openAdminButton
        ]);
        return panel;
    }

    /// <summary>
    /// 为配置表格添加占满剩余列的目录输入行。
    /// </summary>
    private static void AddPathRow(
        TableLayoutPanel panel,
        int row,
        string label,
        TextBox textBox,
        EventHandler browseHandler)
    {
        panel.Controls.Add(CreateFieldLabel(label), 0, row);
        var editor = CreatePathEditor(textBox, "浏览…", browseHandler);
        panel.Controls.Add(editor, 1, row);
        panel.SetColumnSpan(editor, 3);
    }

    /// <summary>
    /// 为配置表格添加带浏览按钮的路径字段。
    /// </summary>
    private static void AddPathField(
        TableLayoutPanel panel,
        int row,
        int column,
        string label,
        TextBox textBox,
        EventHandler browseHandler)
    {
        panel.Controls.Add(CreateFieldLabel(label), column, row);
        panel.Controls.Add(CreatePathEditor(textBox, "选择…", browseHandler), column + 1, row);
    }

    /// <summary>
    /// 为配置表格添加普通文本或数字字段。
    /// </summary>
    private static void AddField(
        TableLayoutPanel panel,
        int row,
        int column,
        string label,
        Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 5, 12, 5);
        panel.Controls.Add(CreateFieldLabel(label), column, row);
        panel.Controls.Add(control, column + 1, row);
    }

    /// <summary>
    /// 创建右对齐的字段标签。
    /// </summary>
    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            ForeColor = Color.FromArgb(55, 65, 81),
            Margin = new Padding(3, 8, 8, 8)
        };
    }

    /// <summary>
    /// 创建由文本框和浏览按钮组成的路径编辑器。
    /// </summary>
    private static Control CreatePathEditor(
        TextBox textBox,
        string buttonText,
        EventHandler browseHandler)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(3, 3, 12, 3)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        textBox.Dock = DockStyle.Fill;
        var button = new Button
        {
            Text = buttonText,
            AutoSize = true,
            Margin = new Padding(6, 0, 0, 0)
        };
        button.Click += browseHandler;
        panel.Controls.Add(textBox, 0, 0);
        panel.Controls.Add(button, 1, 0);
        return panel;
    }

    /// <summary>
    /// 创建具有端口范围限制的数字输入框。
    /// </summary>
    private static NumericUpDown CreatePortInput(int value)
    {
        return new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Value = value,
            ThousandsSeparator = false
        };
    }

    /// <summary>
    /// 应用统一的按钮尺寸、颜色和边框样式。
    /// </summary>
    private static void ConfigureButton(Button button, string text, Color background, Color foreground)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(112, 38);
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = background;
        button.Margin = new Padding(0, 0, 10, 0);
        button.Cursor = Cursors.Hand;
    }

    /// <summary>
    /// 配置适合查看命令输出的只读日志区域。
    /// </summary>
    private void ConfigureLogTextBox()
    {
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.ReadOnly = true;
        logTextBox.BackColor = Color.FromArgb(20, 27, 38);
        logTextBox.ForeColor = Color.FromArgb(220, 228, 240);
        logTextBox.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
        logTextBox.BorderStyle = BorderStyle.None;
        logTextBox.DetectUrls = true;
        logTextBox.WordWrap = false;
        logTextBox.Margin = new Padding(0);
    }

    /// <summary>
    /// 将加载的配置写入对应控件。
    /// </summary>
    private void ApplySettings(DeploymentSettings settings)
    {
        projectRootTextBox.Text = settings.ProjectRoot;
        hostTextBox.Text = settings.Host;
        userNameTextBox.Text = settings.UserName;
        sshPortInput.Value = ClampPort(settings.Port);
        privateKeyTextBox.Text = settings.PrivateKeyPath;
        remoteRootTextBox.Text = settings.RemoteRoot;
        composeProjectTextBox.Text = settings.ComposeProjectName;
        clientPortInput.Value = ClampPort(settings.ClientPort);
        adminPortInput.Value = ClampPort(settings.AdminPort);
        localChecksCheckBox.Checked = settings.RunLocalChecks;
        noCacheCheckBox.Checked = settings.NoCache;
    }

    /// <summary>
    /// 将可能来自旧配置的端口值限制到数字控件的合法范围。
    /// </summary>
    private static decimal ClampPort(int port)
    {
        return Math.Clamp(port, 1, 65535);
    }

    /// <summary>
    /// 从当前界面控件创建不可共享的配置快照。
    /// </summary>
    private DeploymentSettings ReadSettings()
    {
        return new DeploymentSettings
        {
            ProjectRoot = projectRootTextBox.Text.Trim(),
            Host = hostTextBox.Text.Trim(),
            UserName = userNameTextBox.Text.Trim(),
            Port = decimal.ToInt32(sshPortInput.Value),
            PrivateKeyPath = privateKeyTextBox.Text.Trim(),
            RemoteRoot = remoteRootTextBox.Text.Trim().TrimEnd('/'),
            ComposeProjectName = composeProjectTextBox.Text.Trim(),
            ClientPort = decimal.ToInt32(clientPortInput.Value),
            AdminPort = decimal.ToInt32(adminPortInput.Value),
            RunLocalChecks = localChecksCheckBox.Checked,
            NoCache = noCacheCheckBox.Checked
        };
    }

    /// <summary>
    /// 打开文件夹选择器并更新项目目录。
    /// </summary>
    private void OnBrowseProjectRoot(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含 Zero72.Blog.slnx 的博客项目目录",
            SelectedPath = Directory.Exists(projectRootTextBox.Text)
                ? projectRootTextBox.Text
                : Environment.CurrentDirectory,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            projectRootTextBox.Text = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// 打开文件选择器并更新 SSH 私钥路径。
    /// </summary>
    private void OnBrowsePrivateKey(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 SSH 私钥",
            CheckFileExists = true,
            Multiselect = false,
            FileName = File.Exists(privateKeyTextBox.Text) ? privateKeyTextBox.Text : string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            privateKeyTextBox.Text = dialog.FileName;
        }
    }

    /// <summary>
    /// 保存当前配置并运行只读连接测试。
    /// </summary>
    private async void OnTestConnection(object? sender, EventArgs e)
    {
        await RunOperationAsync(async (settings, progress, cancellationToken) =>
        {
            await deploymentService.TestConnectionAsync(settings, progress, cancellationToken);
            return null;
        });
    }

    /// <summary>
    /// 确认发布范围后执行完整部署。
    /// </summary>
    private async void OnDeploy(object? sender, EventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "将更新服务器上的 API、公开博客和管理后台容器。PostgreSQL 数据卷不会删除；失败时会自动回滚。是否继续？",
            "确认发布",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await RunOperationAsync(async (settings, progress, cancellationToken) =>
            await deploymentService.DeployAsync(settings, progress, cancellationToken));
    }

    /// <summary>
    /// 请求取消当前本地命令；若服务器已经开始脚本，SSH 断开不会强制删除服务器数据。
    /// </summary>
    private void OnCancel(object? sender, EventArgs e)
    {
        cancelButton.Enabled = false;
        AppendLog("正在请求取消……");
        operationCancellation?.Cancel();
    }

    /// <summary>
    /// 使用系统默认浏览器打开最近一次成功部署的博客。
    /// </summary>
    private void OnOpenBlog(object? sender, EventArgs e)
    {
        if (lastDeployment is not null)
        {
            OpenUrl(lastDeployment.BlogUrl);
        }
    }

    /// <summary>
    /// 使用系统默认浏览器打开最近一次成功部署的后台。
    /// </summary>
    private void OnOpenAdmin(object? sender, EventArgs e)
    {
        if (lastDeployment is not null)
        {
            OpenUrl(lastDeployment.AdminUrl);
        }
    }

    /// <summary>
    /// 统一运行连接或部署操作，负责保存配置、切换忙碌状态及显示异常。
    /// </summary>
    private async Task RunOperationAsync(
        Func<DeploymentSettings, IProgress<string>, CancellationToken, Task<DeploymentResult?>> operation)
    {
        if (operationCancellation is not null)
        {
            return;
        }

        var settings = ReadSettings();
        operationCancellation = new CancellationTokenSource();
        var progress = new Progress<string>(AppendLog);
        SetBusy(isBusy: true);
        try
        {
            await settingsStore.SaveAsync(settings, operationCancellation.Token);
            var result = await operation(settings, progress, operationCancellation.Token);
            if (result is not null)
            {
                lastDeployment = result;
                openBlogButton.Enabled = true;
                openAdminButton.Enabled = true;
                MessageBox.Show(
                    this,
                    $"发布成功。\r\n版本：{result.ReleaseId}\r\n博客：{result.BlogUrl}",
                    "部署完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("操作已取消。\r\n");
        }
        catch (Exception exception)
        {
            AppendLog($"错误：{exception.Message}\r\n");
            ShowError("操作失败", exception);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            SetBusy(isBusy: false);
        }
    }

    /// <summary>
    /// 在日志末尾追加带时间戳的文本并保持滚动到底部。
    /// </summary>
    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalized = message
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {normalized.TrimEnd()}\r\n");
        logTextBox.SelectionStart = logTextBox.TextLength;
        logTextBox.ScrollToCaret();
    }

    /// <summary>
    /// 根据任务状态启用或禁用配置区操作，并控制进度条动画。
    /// </summary>
    private void SetBusy(bool isBusy)
    {
        testConnectionButton.Enabled = !isBusy;
        deployButton.Enabled = !isBusy;
        cancelButton.Enabled = isBusy;
        progressBar.MarqueeAnimationSpeed = isBusy ? 28 : 0;
    }

    /// <summary>
    /// 通过系统 shell 打开 HTTP 地址。
    /// </summary>
    private static void OpenUrl(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
    }

    /// <summary>
    /// 以统一格式显示错误消息，并保留详细信息到日志而不展示敏感文件内容。
    /// </summary>
    private void ShowError(string title, Exception exception)
    {
        MessageBox.Show(
            this,
            exception.Message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
