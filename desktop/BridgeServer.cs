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
    private readonly IConfigurationCoordinator _configuration;
    private readonly Func<BridgeStatusResponse>? _statusProvider;
    private WebApplication? _application;

    public BridgeServer(IConfigurationCoordinator configuration, Func<BridgeStatusResponse>? statusProvider = null)
    {
        _configuration = configuration;
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
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
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

        app.MapGet(DesktopConfigurationContract.ConfigurationEndpoint, () => Results.Json(_configuration.Load(useDefaults: false)));
        app.MapPost(DesktopConfigurationContract.ConfigurationEndpoint, (ApiConfiguration configuration) =>
        {
            _configuration.Save(configuration);
            return Results.NoContent();
        });
        app.MapGet(DesktopConfigurationContract.ConfigurationSnapshotEndpoint, () => Results.Json(_configuration.LoadSnapshot()));
        app.MapPost(DesktopConfigurationContract.ConfigurationSnapshotEndpoint, (DesktopConfigurationSnapshot snapshot) =>
        {
            _configuration.SaveSnapshot(snapshot);
            return Results.NoContent();
        });
        app.MapPost(DesktopConfigurationContract.HydrateCredentialsEndpoint, (ChannelCredentialsRequest request) =>
            Results.Json(new ChannelCredentialsResponse(_configuration.HydrateChannels(request.Channels))));
        app.MapPost(DesktopConfigurationContract.SyncCredentialsEndpoint, (ChannelCredentialsRequest request) =>
        {
            _configuration.SyncChannels(request.Channels);
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
