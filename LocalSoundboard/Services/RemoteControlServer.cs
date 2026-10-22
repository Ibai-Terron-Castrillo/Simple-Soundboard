using System.Text.Json;
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
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRemoteSoundboardController _controller;
    private readonly Func<int, IReadOnlyList<string>> _getLanUrls;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private WebApplication? _app;
    private CancellationTokenSource? _streamLifetime;
    private int _port = 5050;

    public RemoteControlServer(IRemoteSoundboardController controller)
        : this(controller, RemoteNetworkDiscovery.GetUrls)
    {
    }

    internal RemoteControlServer(IRemoteSoundboardController controller, Func<int, IReadOnlyList<string>> getLanUrls)
    {
        _controller = controller;
        _getLanUrls = getLanUrls;
    }

    public bool IsRunning => _app is not null;

    public IReadOnlyList<string> LanUrls { get; private set; } = [];

    public string LocalUrl => $"http://localhost:{_port}";

    public string DisplayUrl => LanUrls.FirstOrDefault() ?? LocalUrl;

    public async Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_app is not null && _port == port)
            {
                LanUrls = _getLanUrls(port);
                return;
            }

            await StopCoreAsync(cancellationToken);

            _port = port;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(RemoteControlServer).Assembly.GetName().Name,
                Args = []
            });

            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
            builder.Logging.ClearProviders();
            builder.Services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = ShutdownTimeout;
            });
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.WriteIndented = false;
            });

            var app = builder.Build();
            ConfigureRoutes(app);

            try { await app.StartAsync(cancellationToken); }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
            _app = app;
            _streamLifetime = new CancellationTokenSource();
            LanUrls = _getLanUrls(port);
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try { await StopCoreAsync(cancellationToken); }
        finally { _lifecycleGate.Release(); }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        if (_app is null)
        {
            return;
        }

        var app = _app;
        _app = null;
        LanUrls = [];
        if (_streamLifetime is not null)
        {
            await _streamLifetime.CancelAsync();
            _streamLifetime.Dispose();
            _streamLifetime = null;
        }
        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        shutdownCts.CancelAfter(ShutdownTimeout);

        try
        {
            await app.StopAsync(shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void ConfigureRoutes(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var origin = context.Request.Headers.Origin.FirstOrDefault();
            if (HttpMethods.IsPost(context.Request.Method) && origin is not null
                && (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
                    || !string.Equals(originUri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (RequiresPin(context) && !IsAuthorized(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "PIN required or incorrect" });
                return;
            }

            await next();
        });

        app.MapGet("/", () => Results.Content(RemoteWebPage.Html, "text/html; charset=utf-8"));
        app.MapGet("/styles.css", () => Results.Content(RemoteWebPage.Css, "text/css; charset=utf-8"));
        app.MapGet("/app.js", () => Results.Content(RemoteWebPage.Script, "text/javascript; charset=utf-8"));

        app.MapGet("/api/status", () => Results.Ok(new
        {
            library = _controller.LibraryStatusText,
            availableSounds = _controller.AvailableSoundCount,
            totalSounds = _controller.TotalSoundCount,
            isDarkMode = _controller.IsDarkMode,
            playbackPaused = _controller.IsPlaybackPaused,
            playback = _controller.GetPlaybackSnapshot(),
            libraryRevision = _controller.LibraryRevision,
            pinRequired = !string.IsNullOrWhiteSpace(_controller.RemotePin),
            localOnlyWarning = "Remote control is intended for private local networks."
        }));

        app.MapGet("/api/banks", () => Results.Ok(_controller.GetBankSnapshot().Select(bank => new { bank.Id, bank.Name })));

        app.MapGet("/api/events", async (HttpContext context) =>
        {
            var streamToken = _streamLifetime?.Token ?? new CancellationToken(true);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, streamToken);
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(350));
            try
            {
                do
                {
                    if (!string.IsNullOrWhiteSpace(_controller.RemotePin) && !IsAuthorized(context)) break;
                    var payload = JsonSerializer.Serialize(new
                    {
                        playback = _controller.GetPlaybackSnapshot(),
                        libraryRevision = _controller.LibraryRevision,
                        isDarkMode = _controller.IsDarkMode
                    }, StreamJsonOptions);
                    await context.Response.WriteAsync($"data: {payload}\n\n", linked.Token);
                    await context.Response.Body.FlushAsync(linked.Token);
                } while (await timer.WaitForNextTickAsync(linked.Token));
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        });

        app.MapGet("/api/sounds", (string? search, bool? favorites, string? bank) =>
        {
            IEnumerable<SoundItem> sounds = _controller.GetSoundSnapshot();
            if (!string.IsNullOrEmpty(bank))
            {
                var selectedBank = _controller.GetBankSnapshot().FirstOrDefault(item => item.Id == bank);
                var order = (selectedBank?.SoundIds ?? []).Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
                sounds = sounds.Where(sound => order.ContainsKey(sound.Id)).OrderBy(sound => order[sound.Id]);
            }
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
                : Results.UnprocessableEntity(new { error = "Could not start playback. Check the file and audio output on the PC." });
        });

        app.MapPost("/api/sessions/{id:guid}/seek", async (Guid id, SeekRequest request) =>
            await _controller.SeekFromRemoteAsync(id, request.Seconds)
                ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = "Sound is no longer playing or position is invalid." }));
        app.MapPost("/api/sessions/{id:guid}/stop", (Guid id) =>
            _controller.StopSession(id) ? Results.Ok(new { ok = true }) : Results.NotFound());
        app.MapPost("/api/sessions/{id:guid}/pause", (Guid id) =>
            _controller.SetSessionPaused(id, true) ? Results.Ok(new { ok = true }) : Results.NotFound());
        app.MapPost("/api/sessions/{id:guid}/resume", (Guid id) =>
            _controller.SetSessionPaused(id, false) ? Results.Ok(new { ok = true }) : Results.NotFound());

        app.MapPost("/api/stop", () =>
        {
            _controller.StopAll();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/pause", () =>
        {
            _controller.PauseAll();
            return Results.Ok(new { ok = true, playbackPaused = _controller.IsPlaybackPaused });
        });

        app.MapPost("/api/resume", () =>
        {
            _controller.ResumeAll();
            return Results.Ok(new { ok = true, playbackPaused = _controller.IsPlaybackPaused });
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
            name = sound.DisplayName,
            sound.FileName,
            sound.RelativePath,
            sound.Category,
            sound.Extension,
            available = sound.IsAvailable,
            favorite = sound.IsFavorite,
            tags = sound.Tags
        };
    }

}
