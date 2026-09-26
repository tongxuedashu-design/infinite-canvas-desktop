using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfiniteCanvasDesktop;

public sealed class BridgeServer : IAsyncDisposable
{
    private readonly WindowsCredentialStore _credentialStore;
    private readonly Func<BridgeStatusResponse>? _statusProvider;
    private WebApplication? _application;

    public BridgeServer(WindowsCredentialStore credentialStore, Func<BridgeStatusResponse>? statusProvider = null)
    {
        _credentialStore = credentialStore;
        _statusProvider = statusProvider;
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    public string Token { get; }
    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (origin is "http://localhost:3000" or "http://127.0.0.1:3000")
            {
                context.Response.Headers.AccessControlAllowOrigin = origin;
                context.Response.Headers.AccessControlAllowHeaders = "Authorization, Content-Type";
                context.Response.Headers.AccessControlAllowMethods = "GET, POST, OPTIONS";
                context.Response.Headers.Vary = "Origin";
            }

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            if (context.Request.Headers.Authorization != $"Bearer {Token}")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });

        app.MapGet("/api/config", () => Results.Json(_credentialStore.Load(useDefaults: false)));
        app.MapPost("/api/config", (ApiConfiguration configuration) =>
        {
            _credentialStore.Save(configuration);
            return Results.NoContent();
        });
        app.MapPost("/api/credentials/hydrate", (ChannelCredentialsRequest request) =>
            Results.Json(new ChannelCredentialsResponse(_credentialStore.HydrateChannels(request.Channels))));
        app.MapPost("/api/credentials/sync", (ChannelCredentialsRequest request) =>
        {
            _credentialStore.SyncChannels(request.Channels);
            return Results.NoContent();
        });
        app.MapGet("/api/status", () => Results.Json(_statusProvider?.Invoke() ?? new BridgeStatusResponse(true, 3000, null, Port, "running", "服务运行中")));

        await app.StartAsync(cancellationToken);
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault() ?? throw new InvalidOperationException("本地管理服务未返回监听地址。");
        Port = new Uri(address).Port;
        _application = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is null) return;
        await _application.StopAsync().ConfigureAwait(false);
        await _application.DisposeAsync().ConfigureAwait(false);
        _application = null;
    }
}

public sealed record BridgeStatusResponse(bool Running, int VitePort, int? ViteProcessId, int BridgePort, string State, string Message);

public sealed record ChannelCredentialsRequest(IReadOnlyList<ChannelCredential> Channels);
public sealed record ChannelCredentialsResponse(IReadOnlyDictionary<string, string> Channels);
