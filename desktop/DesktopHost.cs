using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace InfiniteCanvasDesktop;

public enum DesktopState { Starting, Running, Stopping, Failed }
public sealed record DesktopStatus(DesktopState State, string Message, int? ProcessId, int? BridgePort);

public sealed class DesktopHost : IDisposable
{
    private readonly string _projectRoot;
    private readonly string _webDirectory;
    private readonly WindowsCredentialStore _credentialStore;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly HttpClient _http = new();
    private readonly string _logPath;
    private StreamWriter? _logWriter;
    private Process? _viteProcess;
    private BridgeServer? _bridge;
    private CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public DesktopHost()
    {
        _projectRoot = FindProjectRoot();
        _webDirectory = Path.Combine(_projectRoot, "web");
        DataDirectory = Path.Combine(Directory.GetParent(_projectRoot)?.FullName ?? _projectRoot, "EdgeData");
        var desktopData = Path.Combine(DataDirectory, "InfiniteCanvasDesktop");
        LogDirectory = Path.Combine(desktopData, "logs");
        Directory.CreateDirectory(LogDirectory);
        _logPath = Path.Combine(LogDirectory, $"desktop-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        _logWriter = new StreamWriter(_logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
        _credentialStore = new WindowsCredentialStore(desktopData);
        WriteLog($"项目目录：{_projectRoot}");
        WriteLog($"Edge 数据目录：{DataDirectory}");
    }

    public event Action<string>? LogReceived;
    public event Action<DesktopStatus>? StatusChanged;
    public string DataDirectory { get; }
    public string LogDirectory { get; }

    public ApiConfiguration LoadConfiguration() => _credentialStore.Load();

    public void SaveConfiguration(ApiConfiguration configuration)
    {
        _credentialStore.Save(configuration);
        WriteLog("API 配置已保存，密钥已写入 Windows 凭据管理器。");
    }

    public async Task StartAsync(bool openCanvas)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            ThrowIfDisposed();
            PublishStatus(DesktopState.Starting, "正在启动", null);
            await EnsureBridgeAsync(_lifetime.Token);

            await StartViteAsync(_lifetime.Token);
            PublishStatus(DesktopState.Running, "服务运行中", _viteProcess?.Id);
            if (openCanvas) OpenCanvas();
        }
        catch
        {
            StopVite();
            PublishStatus(DesktopState.Failed, "启动失败", _viteProcess is { HasExited: false } ? _viteProcess.Id : null);
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task RestartAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            ThrowIfDisposed();
            PublishStatus(DesktopState.Stopping, "正在重新启动", _viteProcess is { HasExited: false } ? _viteProcess.Id : null);
            StopVite();
            await EnsureBridgeAsync(_lifetime.Token);
            await StartViteAsync(_lifetime.Token);
            PublishStatus(DesktopState.Running, "服务运行中", _viteProcess?.Id);
        }
        catch
        {
            StopVite();
            PublishStatus(DesktopState.Failed, "重启失败", null);
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public void OpenCanvas()
    {
        if (_bridge is null || _viteProcess is null || _viteProcess.HasExited)
            throw new InvalidOperationException("Vite 服务尚未启动。");

        var edgePath = FindEdgePath();
        var bridge = Uri.EscapeDataString(_bridge.BaseUrl);
        var token = Uri.EscapeDataString(_bridge.Token);
        var canvasUrl = $"http://localhost:3000/canvas#desktopBridge={bridge}&desktopToken={token}";
        var startInfo = new ProcessStartInfo(edgePath) { UseShellExecute = false };
        startInfo.ArgumentList.Add($"--user-data-dir={DataDirectory}");
        startInfo.ArgumentList.Add("--profile-directory=Default");
        startInfo.ArgumentList.Add($"--app={canvasUrl}");
        startInfo.ArgumentList.Add("--no-first-run");
        startInfo.ArgumentList.Add("--disable-background-mode");
        Process.Start(startInfo);
        WriteLog("已请求 Edge App 模式打开画布。");
    }

    public void WriteLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try { _logWriter?.WriteLine(line); } catch { }
        LogReceived?.Invoke(line);
    }

    private async Task EnsureBridgeAsync(CancellationToken cancellationToken)
    {
        if (_bridge is not null) return;

        var bridge = new BridgeServer(_credentialStore);
        try
        {
            await bridge.StartAsync(cancellationToken);
            _bridge = bridge;
            WriteLog($"本地管理服务已启动：{bridge.BaseUrl}");
        }
        catch
        {
            await bridge.DisposeAsync();
            throw;
        }
    }

    private async Task StartViteAsync(CancellationToken cancellationToken)
    {
        if (_viteProcess is { HasExited: false }) return;
        if (await IsViteReachableAsync())
            throw new InvalidOperationException("端口 3000 已被其他服务占用。请先关闭占用该端口的程序，再点击重新启动服务。");

        var nodePath = FindNodePath();
        var viteScript = Path.Combine(_webDirectory, "node_modules", "vite", "bin", "vite.js");
        if (!File.Exists(viteScript))
            throw new FileNotFoundException("没有找到 Vite。请先在 web 目录执行 npm install。", viteScript);

        var startInfo = new ProcessStartInfo(nodePath)
        {
            WorkingDirectory = _webDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(viteScript);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add("3000");
        startInfo.ArgumentList.Add("--strictPort");
        startInfo.Environment["BROWSER"] = "none";

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) WriteLog($"Vite: {args.Data}"); };
        process.ErrorDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) WriteLog($"Vite 错误: {args.Data}"); };
        process.Exited += (_, _) =>
        {
            WriteLog($"Vite 进程已退出，退出码 {process.ExitCode}。");
            if (!_disposed) PublishStatus(DesktopState.Failed, "服务已停止", null);
        };
        if (!process.Start()) throw new InvalidOperationException("无法启动 Vite 进程。");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _viteProcess = process;
        WriteLog($"Vite 进程已启动，PID {process.Id}。");
        PublishStatus(DesktopState.Starting, "等待画布服务", process.Id);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (process.HasExited)
                throw new InvalidOperationException($"Vite 启动时退出，退出码 {process.ExitCode}。请查看运行日志。");
            if (await IsViteReachableAsync())
            {
                WriteLog("http://localhost:3000 已可访问。");
                return;
            }
            await Task.Delay(250, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<bool> IsViteReachableAsync()
    {
        try
        {
            using var response = await _http.GetAsync("http://127.0.0.1:3000/", HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void StopVite()
    {
        var process = _viteProcess;
        _viteProcess = null;
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                WriteLog($"正在关闭 Vite 进程树 PID {process.Id}。");
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            WriteLog($"关闭 Vite 时出错：{ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private void PublishStatus(DesktopState state, string message, int? processId)
        => StatusChanged?.Invoke(new DesktopStatus(state, message, processId, _bridge?.Port));

    private static string FindProjectRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "web", "package.json"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("无法定位项目根目录（需要包含 web/package.json）。请把桌面程序放在项目目录内运行。");
    }

    private static string FindNodePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
        };
        return candidates.FirstOrDefault(File.Exists) ?? "node.exe";
    }

    private static string FindEdgePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"),
        };
        return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("没有找到 Microsoft Edge。");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DesktopHost));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PublishStatus(DesktopState.Stopping, "正在关闭", _viteProcess is { HasExited: false } ? _viteProcess.Id : null);
        _lifetime.Cancel();
        StopVite();
        if (_bridge is not null)
        {
            try { _bridge.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
            _bridge = null;
        }
        WriteLog("桌面端已关闭其启动的本地服务。");
        _logWriter?.Dispose();
        _logWriter = null;
        _http.Dispose();
    }
}
