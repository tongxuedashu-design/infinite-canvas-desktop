using System.Threading;
using System.Windows;

namespace InfiniteCanvasDesktop;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "InfiniteCanvasDesktop.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("无限画布桌面端已经在运行。", "无限画布", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
