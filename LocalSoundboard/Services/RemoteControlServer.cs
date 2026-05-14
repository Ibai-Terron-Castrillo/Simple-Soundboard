using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LocalSoundboard.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalSoundboard.Services;

public sealed class RemoteControlServer : IAsyncDisposable
{
    private readonly IRemoteSoundboardController _controller;
    private WebApplication? _app;
    private int _port;

    public RemoteControlServer(IRemoteSoundboardController controller)
    {
        _controller = controller;
    }

    public bool IsRunning => _app is not null;

    public string DisplayUrl { get; private set; } = "http://localhost:5050";

    public async Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        if (_app is not null && _port == port)
        {
            return;
        }

        await StopAsync(cancellationToken);

        _port = port;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteControlServer).Assembly.GetName().Name,
            Args = []
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.Logging.ClearProviders();
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.WriteIndented = false;
        });

        var app = builder.Build();
        ConfigureRoutes(app);

        await app.StartAsync(cancellationToken);
        _app = app;
        DisplayUrl = GetDisplayUrl(port);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            return;
        }

        var app = _app;
        _app = null;
        await app.StopAsync(cancellationToken);
        await app.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void ConfigureRoutes(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (RequiresPin(context) && !IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "PIN required or incorrect" });
                return;
            }

            await next();
        });

        app.MapGet("/", () => Results.Content(RemoteWebPage.Html, "text/html; charset=utf-8"));

        app.MapGet("/api/status", () => Results.Ok(new
        {
            library = _controller.LibraryStatusText,
            availableSounds = _controller.AvailableSoundCount,
            totalSounds = _controller.TotalSoundCount,
            isDarkMode = _controller.IsDarkMode,
            pinRequired = !string.IsNullOrWhiteSpace(_controller.RemotePin),
            localOnlyWarning = "Remote control is intended for private local networks."
        }));

        app.MapGet("/api/sounds", (string? search, bool? favorites) =>
        {
            IEnumerable<SoundItem> sounds = _controller.GetSoundSnapshot();
            if (!string.IsNullOrWhiteSpace(search))
            {
                sounds = sounds.Where(sound => sound.SearchText.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (favorites == true)
            {
                sounds = sounds.Where(sound => sound.IsFavorite);
            }

            return Results.Ok(sounds.Select(ToDto));
        });

        app.MapPost("/api/play/{id}", async (string id) =>
        {
            return await _controller.PlayByIdAsync(id)
                ? Results.Ok(new { ok = true })
                : Results.NotFound(new { error = "Sound not found or unavailable" });
        });

        app.MapPost("/api/stop", () =>
        {
            _controller.StopAll();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/favorite/{id}", async (string id) =>
        {
            return await _controller.ToggleFavoriteByIdAsync(id)
                ? Results.Ok(new { ok = true })
                : Results.NotFound(new { error = "Sound not found" });
        });

        app.MapPost("/api/rescan", async () =>
        {
            await _controller.RescanFromRemoteAsync();
            return Results.Ok(new { ok = true });
        });
    }

    private bool RequiresPin(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_controller.RemotePin))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/api/status", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAuthorized(HttpContext context)
    {
        var provided = context.Request.Headers["X-Soundboard-Pin"].FirstOrDefault()
            ?? context.Request.Query["pin"].FirstOrDefault();

        return string.Equals(provided, _controller.RemotePin, StringComparison.Ordinal);
    }

    private static object ToDto(SoundItem sound)
    {
        return new
        {
            sound.Id,
            sound.Name,
            sound.FileName,
            sound.RelativePath,
            sound.Category,
            sound.Extension,
            available = sound.IsAvailable,
            favorite = sound.IsFavorite,
            tags = sound.Tags
        };
    }

    private static string GetDisplayUrl(int port)
    {
        var localAddress = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(address.Address))
            .Select(address => address.Address.ToString())
            .FirstOrDefault();

        return localAddress is null
            ? $"http://localhost:{port}"
            : $"http://{localAddress}:{port}";
    }
}
