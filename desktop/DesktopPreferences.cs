using System.IO;

namespace InfiniteCanvasDesktop;

public sealed class DesktopPreferencesStore
{
    private readonly string _path;

    public DesktopPreferencesStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "preferences.json");
    }

    public bool LoadOpenCanvasOnStartup()
    {
        if (!File.Exists(_path)) return true;
        try
        {
            var preferences = System.Text.Json.JsonSerializer.Deserialize<DesktopPreferences>(File.ReadAllText(_path));
            return preferences?.OpenCanvasOnStartup ?? true;
        }
        catch
        {
            return true;
        }
    }

    public void SaveOpenCanvasOnStartup(bool value)
    {
        var tempPath = _path + ".tmp";
        var json = System.Text.Json.JsonSerializer.Serialize(new DesktopPreferences(value), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }

    private sealed record DesktopPreferences(bool OpenCanvasOnStartup);
}