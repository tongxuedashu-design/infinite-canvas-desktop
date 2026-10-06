using System.IO;
using System.Text;
using System.Text.Json;

namespace InfiniteCanvasDesktop;

/// <summary>
/// The shared configuration seam used by the WPF manager and the local bridge.
/// Secrets stay in Windows Credential Manager; the snapshot only contains non-sensitive metadata.
/// </summary>
public interface IConfigurationCoordinator
{
    ApiConfiguration Load(bool useDefaults = true);
    void Save(ApiConfiguration configuration);
    DesktopConfigurationSnapshot? LoadSnapshot();
    void SaveSnapshot(DesktopConfigurationSnapshot snapshot);
    IReadOnlyDictionary<string, string> HydrateChannels(IEnumerable<ChannelCredential> channels);
    void SyncChannels(IEnumerable<ChannelCredential> channels);
}

/// <summary>
/// Stable names for the desktop configuration contract.
/// </summary>
public static class DesktopConfigurationContract
{
    public const int CurrentSchemaVersion = 1;
    public const string ConfigurationEndpoint = "/api/config";
    public const string ConfigurationSnapshotEndpoint = "/api/config/snapshot";
    public const string HydrateCredentialsEndpoint = "/api/credentials/hydrate";
    public const string SyncCredentialsEndpoint = "/api/credentials/sync";
}

public sealed record ConfigurationEnvelope(
    int SchemaVersion,
    long Revision,
    bool Initialized,
    string BaseUrl,
    string Model,
    IReadOnlyDictionary<string, string> CredentialReferences);

public sealed record DesktopConfigurationSnapshot(
    int SchemaVersion,
    IReadOnlyList<DesktopChannelSnapshot> Channels,
    IReadOnlyDictionary<string, string> SelectedModels,
    DesktopLocalProxySnapshot? LocalProxy = null);

public sealed record DesktopLocalProxySnapshot(bool Enabled, string Url);

public sealed record DesktopChannelSnapshot(
    string Id,
    string Name,
    string BaseUrl,
    string ApiFormat,
    IReadOnlyList<DesktopModelSnapshot> Models);

public sealed record DesktopModelSnapshot(string Name, string Capability, string? Script);

public sealed class ConfigurationCoordinator : IConfigurationCoordinator
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly WindowsCredentialStore _store;
    private readonly string _snapshotPath;

    public ConfigurationCoordinator(WindowsCredentialStore store)
    {
        _store = store;
        _snapshotPath = Path.Combine(store.DataDirectory, "configuration-snapshot.json");
    }

    public ApiConfiguration Load(bool useDefaults = true) => _store.Load(useDefaults);

    public void Save(ApiConfiguration configuration) => _store.Save(configuration);

    public DesktopConfigurationSnapshot? LoadSnapshot()
    {
        if (!File.Exists(_snapshotPath)) return null;
        try
        {
            var snapshot = JsonSerializer.Deserialize<DesktopConfigurationSnapshot>(File.ReadAllText(_snapshotPath), SnapshotJsonOptions);
            if (snapshot is null) return null;
            return snapshot with
            {
                SchemaVersion = snapshot.SchemaVersion <= 0 ? DesktopConfigurationContract.CurrentSchemaVersion : snapshot.SchemaVersion,
                Channels = snapshot.Channels ?? [],
                SelectedModels = snapshot.SelectedModels ?? new Dictionary<string, string>(),
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SaveSnapshot(DesktopConfigurationSnapshot snapshot)
    {
        var normalized = snapshot with
        {
            SchemaVersion = DesktopConfigurationContract.CurrentSchemaVersion,
            Channels = snapshot.Channels ?? [],
            SelectedModels = snapshot.SelectedModels ?? new Dictionary<string, string>(),
        };
        var json = JsonSerializer.Serialize(normalized, SnapshotJsonOptions);
        var temporaryPath = _snapshotPath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            ReplaceFile(temporaryPath, _snapshotPath);
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch { }
            throw;
        }
    }

    private static void ReplaceFile(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
        else
            File.Move(temporaryPath, destinationPath);
    }

    public IReadOnlyDictionary<string, string> HydrateChannels(IEnumerable<ChannelCredential> channels) =>
        _store.HydrateChannels(channels);

    public void SyncChannels(IEnumerable<ChannelCredential> channels) => _store.SyncChannels(channels);
}
