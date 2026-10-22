using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using Xunit;

namespace LocalSoundboard.Tests;

public sealed class RemoteServerTests
{
    [Fact]
    public async Task RefreshesLanAddressesWithoutStoppingTheServer()
    {
        IReadOnlyList<string> addresses = ["http://192.168.1.10:5050"];
        await using var server = new RemoteControlServer(new Controller(), _ => addresses);
        var port = FreePort();
        await server.StartAsync(port);
        Assert.Equal(addresses, server.LanUrls);
        Assert.Equal(addresses[0], server.DisplayUrl);
        addresses = ["http://192.168.1.20:5050"];
        await server.StartAsync(port);
        Assert.Equal(addresses, server.LanUrls);
        using var client = new HttpClient();
        Assert.True((await client.GetAsync($"http://127.0.0.1:{port}/api/status")).IsSuccessStatusCode);
        await server.StopAsync();
        Assert.Empty(server.LanUrls);
    }

    [Fact]
    public async Task MissingLanAddressStillAllowsLocalTesting()
    {
        await using var server = new RemoteControlServer(new Controller(), _ => []);
        var port = FreePort();
        await server.StartAsync(port);
        Assert.Empty(server.LanUrls);
        Assert.Equal($"http://localhost:{port}", server.LocalUrl);
        using var client = new HttpClient();
        Assert.True((await client.GetAsync(server.LocalUrl + "/api/status")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task CanStopRestartAndChangePortRepeatedly()
    {
        await using var server = new RemoteControlServer(new Controller());
        var port = FreePort();
        using var client = new HttpClient();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            await server.StartAsync(port);
            Assert.True((await client.GetAsync($"http://127.0.0.1:{port}/api/status")).IsSuccessStatusCode);
            await server.StopAsync();
            Assert.False(server.IsRunning);
        }
        var newPort = FreePort();
        await server.StartAsync(newPort);
        Assert.True((await client.GetAsync($"http://127.0.0.1:{newPort}/api/status")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task StreamRequiresPinAndDoesNotDelayShutdown()
    {
        var controller = new Controller { RemotePin = "1234" };
        await using var server = new RemoteControlServer(controller);
        var port = FreePort();
        await server.StartAsync(port);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/stop", null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Soundboard-Pin", "1234");
        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.True(response.IsSuccessStatusCode);
        using var reader = new System.IO.StreamReader(await response.Content.ReadAsStreamAsync());
        Assert.StartsWith("data: ", await reader.ReadLineAsync());
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task RoutesReportPlaybackFailuresAndValidateSessionIdsAndPositions()
    {
        await using var server = new RemoteControlServer(new Controller());
        var port = FreePort();
        await server.StartAsync(port);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync("/api/play/missing", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/sessions/not-an-id/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/sessions/{Guid.NewGuid()}/seek", new { seconds = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/files/C:/Windows/test.wav")).StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "http://unrelated.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/stop", null)).StatusCode);
    }

    [Fact]
    public async Task BankFilterRespectsCustomOrdering()
    {
        await using var server = new RemoteControlServer(new Controller());
        var port = FreePort();
        await server.StartAsync(port);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var sounds = await client.GetFromJsonAsync<SoundDto[]>("/api/sounds?bank=bank");
        Assert.Equal(new[] { "second", "first" }, sounds!.Select(sound => sound.Id));
    }

    private sealed record SoundDto(string Id);
    internal static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class Controller : IRemoteSoundboardController
    {
        public string LibraryStatusText => "Test library";
        public int AvailableSoundCount => 2;
        public int TotalSoundCount => 2;
        public bool IsDarkMode => true;
        public bool IsPlaybackPaused { get; private set; }
        public string RemotePin { get; set; } = string.Empty;
        public long LibraryRevision => 1;
        public IReadOnlyList<SoundItem> GetSoundSnapshot() => [new() { Id = "first", Name = "First", IsAvailable = true }, new() { Id = "second", Name = "Second", IsAvailable = true }];
        public IReadOnlyList<SoundBankSnapshot> GetBankSnapshot() => [new("bank", "Bank", ["second", "first"])];
        public PlaybackSnapshot GetPlaybackSnapshot() => new([]);
        public Task<bool> PlayByIdAsync(string id) => Task.FromResult(false);
        public void StopAll() => IsPlaybackPaused = false;
        public void PauseAll() => IsPlaybackPaused = true;
        public void ResumeAll() => IsPlaybackPaused = false;
        public Task<bool> ToggleFavoriteByIdAsync(string id) => Task.FromResult(false);
        public Task RescanFromRemoteAsync() => Task.CompletedTask;
        public Task<bool> SeekFromRemoteAsync(Guid sessionId, double seconds) => Task.FromResult(false);
        public bool StopSession(Guid sessionId) => false;
        public bool SetSessionPaused(Guid sessionId, bool paused) => false;
    }
}
