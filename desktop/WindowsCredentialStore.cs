using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace InfiniteCanvasDesktop;

public sealed class WindowsCredentialStore
{
    private const string PrimaryCredentialTarget = "InfiniteCanvasDesktop:PrimaryApiKey";
    private const string ChannelCredentialPrefix = "InfiniteCanvasDesktop:Channel:";
    private readonly string _settingsPath;
    private readonly string _credentialIndexPath;

    public WindowsCredentialStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
        _credentialIndexPath = Path.Combine(dataDirectory, "credential-index.json");
    }

    public ApiConfiguration Load(bool useDefaults = true)
    {
        var metadata = useDefaults ? ApiConfiguration.Default : new ApiConfiguration("", "", "");
        if (File.Exists(_settingsPath))
        {
            var saved = JsonSerializer.Deserialize<ApiConfigurationMetadata>(File.ReadAllText(_settingsPath));
            if (saved is not null)
                metadata = new ApiConfiguration(saved.BaseUrl ?? metadata.BaseUrl, "", saved.Model ?? metadata.Model);
        }
        return metadata with { ApiKey = ReadCredential() };
    }

    public void Save(ApiConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.BaseUrl))
        {
            if (!Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Base URL 不是有效地址。");
            if (uri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Base URL 必须使用 http:// 或 https://。");
        }

        var metadata = new ApiConfigurationMetadata(configuration.BaseUrl.Trim(), configuration.Model.Trim());
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        WriteCredential(PrimaryCredentialTarget, configuration.ApiKey);
    }

    public IReadOnlyDictionary<string, string> HydrateChannels(IEnumerable<ChannelCredential> channels)
    {
        var result = new Dictionary<string, string>();
        var channelIds = new HashSet<string>();
        var isPrimary = true;
        foreach (var channel in channels)
        {
            var id = NormalizeChannelId(channel.Id);
            channelIds.Add(id);
            var target = ChannelCredentialPrefix + id;
            var saved = ReadCredential(target);
            var fallback = !string.IsNullOrEmpty(channel.ApiKey)
                ? channel.ApiKey
                : isPrimary ? ReadCredential(PrimaryCredentialTarget) : "";
            if (string.IsNullOrEmpty(saved) && !string.IsNullOrEmpty(fallback))
            {
                WriteCredential(target, fallback);
                saved = fallback;
            }
            result[id] = saved;
            isPrimary = false;
        }
        foreach (var removedId in ReadCredentialIndex().Except(channelIds))
            WriteCredential(ChannelCredentialPrefix + removedId, "");
        WriteCredentialIndex(channelIds);
        return result;
    }

    public void SyncChannels(IEnumerable<ChannelCredential> channels)
    {
        var items = channels.Select(channel => channel with { Id = NormalizeChannelId(channel.Id) }).ToArray();
        var currentIds = items.Select(channel => channel.Id).ToHashSet();
        foreach (var removedId in ReadCredentialIndex().Except(currentIds))
            WriteCredential(ChannelCredentialPrefix + removedId, "");
        foreach (var channel in items)
        {
            WriteCredential(ChannelCredentialPrefix + channel.Id, channel.ApiKey);
        }
        WriteCredentialIndex(currentIds);
    }

    private static string ReadCredential(string target = PrimaryCredentialTarget)
    {
        if (!CredRead(target, CredentialType.Generic, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return "";
            throw new Win32Exception(error, "无法读取 Windows 凭据管理器中的 API Key。");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return "";
            var bytes = new byte[checked((int)credential.CredentialBlobSize)];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    private static void WriteCredential(string target, string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            if (!CredDelete(target, CredentialType.Generic, 0))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1168) throw new Win32Exception(error, "无法删除 Windows 凭据管理器中的 API Key。");
            }
            return;
        }

        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = CredentialType.Generic,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersist.LocalMachine,
                UserName = Environment.UserName,
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法把 API Key 写入 Windows 凭据管理器。");
        }
        finally
        {
            Marshal.FreeCoTaskMem(blob);
        }
    }

    private static string NormalizeChannelId(string id)
    {
        var value = id.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 128 || value.Any(character => char.IsControl(character)))
            throw new InvalidOperationException("渠道标识无效。");
        return value;
    }

    private HashSet<string> ReadCredentialIndex()
    {
        try
        {
            if (!File.Exists(_credentialIndexPath)) return [];
            return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(_credentialIndexPath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void WriteCredentialIndex(IEnumerable<string> channelIds)
    {
        var normalized = channelIds.Select(NormalizeChannelId).OrderBy(id => id).ToArray();
        File.WriteAllText(_credentialIndexPath, JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record ApiConfigurationMetadata(string? BaseUrl, string? Model);

    private enum CredentialType : uint { Generic = 1 }
    private enum CredentialPersist : uint { LocalMachine = 2 }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public CredentialType Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CredentialPersist Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CredentialType type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}

public sealed record ChannelCredential(string Id, string ApiKey);
