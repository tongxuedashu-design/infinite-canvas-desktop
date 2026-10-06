using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsSystemIcons = System.Drawing.SystemIcons;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using FormsEventHandler = System.EventHandler;
using WpfMessageBox = System.Windows.MessageBox;

namespace InfiniteCanvasDesktop;

public partial class MainWindow : Window
{
    private readonly DesktopHost _host;
    private readonly FormsNotifyIcon _trayIcon;
    private HwndSource? _windowSource;
    private bool _closing;
    private bool _allowClose;
    private bool _updatingOpenCanvasPreference;

    public MainWindow()
    {
        _updatingOpenCanvasPreference = true;
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        _host = new DesktopHost();
        _trayIcon = CreateTrayIcon();
        _host.LogReceived += Host_LogReceived;
        _host.StatusChanged += Host_StatusChanged;
        DataPathText.Text = _host.DataDirectory;
        AutoOpenCanvasCheckBox.IsChecked = _host.OpenCanvasOnStartup;
        _updatingOpenCanvasPreference = false;
        LoadConfiguration();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowSource = (HwndSource)PresentationSource.FromVisual(this)!;
        _windowSource.AddHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == App.ActivateWindowMessage)
        {
            Dispatcher.Invoke(ShowLauncher);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private FormsNotifyIcon CreateTrayIcon()
    {
        var menu = new FormsContextMenuStrip();
        menu.Items.Add(new FormsToolStripMenuItem("打开启动器", null, new FormsEventHandler((_, _) => Dispatcher.Invoke(ShowLauncher))));
        menu.Items.Add(new FormsToolStripMenuItem("打开画布", null, new FormsEventHandler((_, _) => Dispatcher.Invoke(OpenCanvasFromTray))));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(new FormsToolStripMenuItem("退出无限画布", null, new FormsEventHandler((_, _) => Dispatcher.Invoke(RequestExit))));

        var icon = new FormsNotifyIcon
        {
            Icon = FormsSystemIcons.Application,
            ContextMenuStrip = menu,
            Text = "无限画布",
            Visible = true,
        };
        icon.DoubleClick += new FormsEventHandler((_, _) => Dispatcher.Invoke(ShowLauncher));
        return icon;
    }

    private void ShowLauncher()
    {
        if (HomeView.Visibility == Visibility.Visible)
            LoadConfiguration();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OpenCanvasFromTray()
    {
        ShowLauncher();
        if (OpenCanvasButton.IsEnabled)
            OpenCanvasButton_Click(this, new RoutedEventArgs());
    }

    private void RequestExit()
    {
        _allowClose = true;
        Close();
    }
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _host.StartAsync(openCanvas: _host.OpenCanvasOnStartup);
        }
        catch (OperationCanceledException) when (_closing)
        {
        }
        catch (Exception ex)
        {
            _host.WriteLog($"启动失败：{ex.Message}");
            WpfMessageBox.Show(ex.Message, "无限画布启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool LoadConfiguration()
    {
        try
        {
            var config = _host.LoadConfiguration();
            BaseUrlBox.Text = config.BaseUrl;
            ApiKeyBox.Password = config.ApiKey;
            ModelBox.Text = config.Model;
            UpdateChannelSummary(config);
            HomeNoticeText.Text = "";
            return true;
        }
        catch (Exception ex)
        {
            BaseUrlBox.Clear();
            ApiKeyBox.Clear();
            ModelBox.Clear();
            ChannelsList.ItemsSource = Array.Empty<ChannelDisplay>();
            HomeNoticeText.Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(185, 28, 28));
            HomeNoticeText.Text = "无法读取配置，请到服务与日志查看原因，再重新进入编辑。";
            _host.WriteLog($"读取 API 配置失败：{ex.Message}");
            return false;
        }
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigErrorText.Text = "";
        try
        {
            var configuration = new ApiConfiguration(BaseUrlBox.Text.Trim(), ApiKeyBox.Password, ModelBox.Text.Trim());
            _host.SaveConfiguration(configuration);
            UpdateChannelSummary(configuration);
            ShowView(HomeView);
            HomeNoticeText.Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(21, 128, 61));
            HomeNoticeText.Text = "配置已保存；已打开的画布不会自动应用本次修改。";
        }
        catch (Exception ex)
        {
            ConfigErrorText.Text = $"保存失败：{ex.Message}";
        }
    }

    private void EditConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if (!LoadConfiguration()) return;
        ConfigErrorText.Text = "";
        ModelSettingsExpander.IsExpanded = false;
        ShowView(EditView);
        BaseUrlBox.Focus();
    }

    private void BackToHomeButton_Click(object sender, RoutedEventArgs e)
    {
        // Re-read persisted values so Cancel and Back both discard the editor draft.
        LoadConfiguration();
        ConfigErrorText.Text = "";
        ShowView(HomeView);
    }

    private void ServiceDetailsButton_Click(object sender, RoutedEventArgs e) => ShowView(ServiceDetailsView);

    private void ShowDiagnosticsButton_Click(object sender, RoutedEventArgs e) => ShowView(DiagnosticsPanel);

    private void ShowView(FrameworkElement view)
    {
        HomeView.Visibility = view == HomeView ? Visibility.Visible : Visibility.Collapsed;
        EditView.Visibility = view == EditView ? Visibility.Visible : Visibility.Collapsed;
        ServiceDetailsView.Visibility = view == ServiceDetailsView ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsPanel.Visibility = view == DiagnosticsPanel ? Visibility.Visible : Visibility.Collapsed;
        // Leave the draft intact until the user explicitly saves or cancels.
        ChannelsNavButton.IsEnabled = ServiceNavButton.IsEnabled = view != EditView;
        ChannelsNavButton.Background = (MediaBrush)FindResource(view == HomeView || view == EditView ? "SelectedTabBackground" : "PanelBackground");
        ServiceNavButton.Background = (MediaBrush)FindResource(view == ServiceDetailsView || view == DiagnosticsPanel ? "SelectedTabBackground" : "PanelBackground");
    }

    private void OpenCanvasButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _host.OpenCanvas();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "无法打开画布", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AutoOpenCanvasCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingOpenCanvasPreference || AutoOpenCanvasCheckBox.IsChecked is not bool enabled) return;
        try
        {
            _host.SetOpenCanvasOnStartup(enabled);
        }
        catch (Exception ex)
        {
            _updatingOpenCanvasPreference = true;
            try { AutoOpenCanvasCheckBox.IsChecked = _host.OpenCanvasOnStartup; }
            finally { _updatingOpenCanvasPreference = false; }
            HomeNoticeText.Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(185, 28, 28));
            HomeNoticeText.Text = $"启动偏好保存失败：{ex.Message}";
        }
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        RestartButton.IsEnabled = false;
        try
        {
            await _host.RestartAsync();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "服务重启失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_host.LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", _host.LogDirectory) { UseShellExecute = true });
    }

    private void Host_LogReceived(string line)
    {
        Dispatcher.InvokeAsync(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });
    }

    private void Host_StatusChanged(DesktopStatus status)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text = status.State switch
            {
                DesktopState.Running => "服务就绪",
                DesktopState.Failed => "启动失败",
                DesktopState.Stopping => "正在停止",
                _ => "正在启动",
            };
            StatusPill.ToolTip = status.Message;
            ServiceStatusText.Text = status.Message;
            var stateColor = status.State switch
            {
                DesktopState.Running => MediaColor.FromRgb(22, 163, 74),
                DesktopState.Failed => MediaColor.FromRgb(220, 38, 38),
                _ => MediaColor.FromRgb(217, 119, 6),
            };
            StatusDot.Fill = new System.Windows.Media.SolidColorBrush(stateColor);
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(status.State switch
            {
                DesktopState.Failed => MediaColor.FromRgb(185, 28, 28),
                DesktopState.Running => MediaColor.FromRgb(35, 115, 68),
                _ => MediaColor.FromRgb(146, 64, 14),
            });
            StatusPill.Background = new System.Windows.Media.SolidColorBrush(status.State == DesktopState.Failed
                ? MediaColor.FromRgb(254, 226, 226)
                : status.State == DesktopState.Running
                    ? MediaColor.FromRgb(231, 246, 236)
                    : MediaColor.FromRgb(255, 247, 237));
            ProcessText.Text = status.ProcessId is null ? "未启动" : $"Vite 进程 · PID {status.ProcessId}";
            BridgePortText.Text = status.BridgePort is null ? "-" : status.BridgePort.ToString();
            OpenCanvasButton.IsEnabled = status.State == DesktopState.Running;
            RestartButton.IsEnabled = status.State is DesktopState.Running or DesktopState.Failed;
        });
    }

    private void UpdateChannelSummary(ApiConfiguration configuration)
    {
        DesktopConfigurationSnapshot? snapshot = null;
        try
        {
            snapshot = _host.LoadConfigurationSnapshot();
        }
        catch (Exception ex)
        {
            _host.WriteLog($"读取渠道模型快照失败：{ex.Message}");
            HomeNoticeText.Foreground = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(185, 28, 28));
            HomeNoticeText.Text = "渠道模型快照读取失败，当前仅显示主渠道配置。";
        }
        ChannelsList.ItemsSource = BuildChannelDisplays(configuration, snapshot);
    }

    private static IReadOnlyList<ChannelDisplay> BuildChannelDisplays(ApiConfiguration configuration, DesktopConfigurationSnapshot? snapshot)
    {
        if (snapshot?.Channels is { Count: > 0 } channels)
        {
            return channels.Select((channel, index) => new ChannelDisplay(
                string.IsNullOrWhiteSpace(channel.Name) ? $"渠道 {index + 1}" : channel.Name,
                string.IsNullOrWhiteSpace(channel.BaseUrl) ? "尚未配置服务地址" : channel.BaseUrl,
                channel.Models.Count == 0 ? "暂无模型" : $"{channel.Models.Count} 个模型",
                index == 0 && !string.IsNullOrWhiteSpace(configuration.ApiKey) ? "密钥已保存" : "密钥由画布注入",
                channel.Models.Select(model => new ModelDisplay(
                    model.Name,
                    $"{CapabilityLabel(model.Capability)}{IsSelected(snapshot.SelectedModels, channel.Id, model.Name) switch { true => " · 当前使用", false => "" }}")).ToArray())).ToArray();
        }

        var fallbackModels = string.IsNullOrWhiteSpace(configuration.Model)
            ? Array.Empty<ModelDisplay>()
            : new[] { new ModelDisplay(configuration.Model, "主渠道 · 当前使用") };
        return new[]
        {
            new ChannelDisplay(
                "主渠道",
                string.IsNullOrWhiteSpace(configuration.BaseUrl) ? "尚未配置服务地址" : configuration.BaseUrl,
                fallbackModels.Length == 0 ? "暂无模型" : $"{fallbackModels.Length} 个模型",
                string.IsNullOrWhiteSpace(configuration.ApiKey) ? "密钥未配置" : "密钥已保存",
                fallbackModels),
        };
    }

    private static bool IsSelected(IReadOnlyDictionary<string, string> selectedModels, string channelId, string modelName)
    {
        var value = $"{channelId}::{modelName}";
        return selectedModels.Values.Any(selected => selected == value || selected == modelName);
    }

    private static string CapabilityLabel(string capability) => capability switch
    {
        "image" => "图像",
        "video" => "视频",
        "text" => "文本",
        "audio" => "音频",
        _ => capability,
    };

    public sealed record ChannelDisplay(
        string Name,
        string BaseUrl,
        string ModelSummary,
        string CredentialText,
        IReadOnlyList<ModelDisplay> Models);

    public sealed record ModelDisplay(string Name, string Detail);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (!_closing)
        {
            _closing = true;
            e.Cancel = true;
            IsEnabled = false;
            _ = FinishClosingAsync();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosed(e);
    }

    private async Task FinishClosingAsync()
    {
        try
        {
            await _host.DisposeAsync();
        }
        finally
        {
            _allowClose = true;
            Close();
        }
    }
}
