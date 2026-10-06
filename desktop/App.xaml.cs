using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace InfiniteCanvasDesktop;

public partial class App : System.Windows.Application
{
    internal const int ActivateWindowMessage = 0x8000 + 37;
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "InfiniteCanvasDesktop.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            ActivateExistingWindow();
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

    private static void ActivateExistingWindow()
    {
        var handle = FindWindow(null, "无限画布桌面端");
        if (handle == IntPtr.Zero) return;
        PostMessage(handle, ActivateWindowMessage, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
}
