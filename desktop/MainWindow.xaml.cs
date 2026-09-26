using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace InfiniteCanvasDesktop;

public partial class MainWindow : Window
{
    private readonly DesktopHost _host;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        _host = new DesktopHost();
        _host.LogReceived += Host_LogReceived;
        _host.StatusChanged += Host_StatusChanged;
        DataPathText.Text = _host.DataDirectory;
        LoadConfiguration();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _host.StartAsync(openCanvas: true);
        }
        catch (Exception ex)
        {
            _host.WriteLog($"启动失败：{ex.Message}");
            MessageBox.Show(ex.Message, "无限画布启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _host.LoadConfiguration();
            BaseUrlBox.Text = config.BaseUrl;
            ApiKeyBox.Password = config.ApiKey;
            ModelBox.Text = config.Model;
        }
        catch (Exception ex)
        {
            _host.WriteLog($"读取 API 配置失败：{ex.Message}");
        }
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigSavedText.Text = "";
        try
        {
            _host.SaveConfiguration(new ApiConfiguration(BaseUrlBox.Text.Trim(), ApiKeyBox.Password, ModelBox.Text.Trim()));
            ConfigSavedText.Text = "已安全保存；重新打开画布后生效";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenCanvasButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _host.OpenCanvas();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "无法打开画布", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(ex.Message, "服务重启失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
            StatusText.Text = status.Message;
            StatusDot.Fill = new SolidColorBrush(status.State switch
            {
                DesktopState.Running => Color.FromRgb(22, 163, 74),
                DesktopState.Failed => Color.FromRgb(220, 38, 38),
                _ => Color.FromRgb(217, 119, 6),
            });
            ProcessText.Text = status.ProcessId is null ? "未启动" : $"PID {status.ProcessId}";
            BridgePortText.Text = status.BridgePort is null ? "-" : status.BridgePort.ToString();
            OpenCanvasButton.IsEnabled = status.State == DesktopState.Running;
            RestartButton.IsEnabled = status.State is DesktopState.Running or DesktopState.Failed;
        });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closing)
        {
            _closing = true;
            _host.Dispose();
        }
        base.OnClosing(e);
    }
}
